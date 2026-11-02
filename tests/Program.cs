using System.Text.Json;
using KeepLength;

int checks = 0;
void Check(bool condition, string name) { checks++; if (!condition) throw new Exception(name); }
bool Run(bool enabled=true, bool ready=true, bool changing=false, bool stick=true,
    bool manual=false, bool skeleton=true, float native=.064f, float rest=.1898f, float multiplier=1f)
    => LengthPolicy.TryLength(enabled, ready, changing, stick, manual, skeleton, native, rest, multiplier, out _);
Check(Run(), "sampled insertion eligible");
Check(!Run(enabled:false), "disabled");
Check(!Run(ready:false), "not ready or outside scene");
Check(!Run(changing:true), "animation changing");
Check(!Run(stick:false), "native not sticking");
Check(!Run(manual:true), "manual pose preserved");
Check(!Run(skeleton:false), "unknown skeleton untouched");
foreach (float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
{
    Check(!Run(native:invalid), "invalid native");
    Check(!Run(rest:invalid), "invalid baseline");
    Check(!Run(multiplier:invalid), "invalid multiplier");
}
Check(!Run(rest:0) && !Run(rest:.501f), "length range");
Check(!Run(multiplier:.24f) && !Run(multiplier:2.01f), "multiplier range");
Check(LengthPolicy.TryLength(true,true,false,true,false,true,.028f,.1898f,1.2f,out var scaled)
    && MathF.Abs(scaled-.22776f)<1e-6, "preserves full length even under severe native compression");
Check(LengthPolicy.TryNativeShrink(true,true,false,false,true,.064f,.1898f,1,out var rider)
    && MathF.Abs(rider-.1898f)<1e-6, "native shrink applies without TopStick");
Check(!LengthPolicy.TryNativeShrink(true,true,false,false,true,.1898f,.1898f,1,out _),
    "native target length is left to the game");
Check(!LengthPolicy.TryNativeShrink(true,true,false,false,true,.189801f,.1898f,1,out _),
    "native longer than target is left to the game");
Check(LengthPolicy.TryNativeShrink(true,true,false,true,true,.064f,.1898f,1,out _),
    "manual-marked native animation is included in all-native mode");
Check(!LengthPolicy.TryNativeShrink(true,true,true,true,true,.064f,.1898f,1,out _), "changing still excluded");
Check(!LengthPolicy.TryNativeShrink(true,false,false,true,true,.064f,.1898f,1,out _), "not ready still excluded");
Check(!LengthPolicy.TryNativeShrink(true,true,false,true,false,.064f,.1898f,1,out _), "unknown skeleton still excluded");
Check(LengthPolicy.StillOwned(.1898f,.1898f), "owned position can restore");
Check(!LengthPolicy.StillOwned(.064f,.1898f), "later native/third-party pose must not be restored over");
Check(!LengthPolicy.StillOwned(float.NaN,.1898f), "invalid ownership");

if (args.Length == 0)
{
    Console.WriteLine($"Policy checks: {checks} passed. Replay input not supplied; use the recorded JSONL path to run replay checks.");
    return;
}

int samples=0, applied=0, unchanged=0;
foreach(string line in File.ReadLines(args[0]))
{
    using var json=JsonDocument.Parse(line);
    var r=json.RootElement;
    if(r.GetProperty("kind").GetString()!="sample") continue;
    var d=r.GetProperty("data"); var l=d.GetProperty("positions").GetProperty("lookers")[0];
    var current=l.GetProperty("current"); var t=l.GetProperty("top"); var b=l.GetProperty("basis");
    float native=t.GetProperty("localPosition")[2].GetSingle();
    bool stick=current.GetProperty("topStick").GetBoolean(), manual=current.GetProperty("manual").GetBoolean();
    bool skeleton=t.GetProperty("name").GetString()=="cf_j_dan109_00"
        && t.GetProperty("parent").GetString()==b.GetProperty("name").GetString();
    bool eligible=LengthPolicy.TryNativeShrink(true,d.GetProperty("ready").GetBoolean(),d.GetProperty("changing").GetBoolean(),
        manual,skeleton,native,.1898f,1,out var output);
    samples++;
    if(eligible) { applied++; Check(MathF.Abs(output-.1898f)<1e-7, "replay full rest length"); }
    else { unchanged++; Check(!d.GetProperty("ready").GetBoolean() || d.GetProperty("changing").GetBoolean() || !skeleton || native >= .1898f - LengthPolicy.ShrinkEpsilon, "replay excluded for known reason"); }
}
Check(samples==1318 && applied>945 && unchanged>0,"native shrink replay expands coverage beyond the old TopStick gate");
int manualLogSamples=0;
if(args.Length>1)
{
    foreach(string line in File.ReadLines(args[1]))
    {
        var m=System.Text.RegularExpressions.Regex.Match(line,@"Native shrink not applied: nativeZ=([\d.]+), target=([\d.]+), topStick=True, manual=True, ready=True, changing=False, skeletonMatches=True, mode=all-native");
        if(!m.Success) continue;
        float native=float.Parse(m.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture);
        float target=float.Parse(m.Groups[2].Value,System.Globalization.CultureInfo.InvariantCulture);
        Check(LengthPolicy.TryNativeShrink(true,true,false,true,true,native,target,1,out var length) && MathF.Abs(length-target)<1e-6,
            "observed manual-marked shrink now applies");
        Check(!LengthPolicy.TryLength(true,true,false,true,true,true,native,target,1,out _), "legacy manual protection remains");
        manualLogSamples++;
    }
    Check(manualLogSamples>0,"diagnostic log contains observed manual shrink");
}
Console.WriteLine(JsonSerializer.Serialize(new {checks,samples,applied,unchanged,manualLogSamples,boundary="Offline policy and diagnostic-log replay only; native hook, skinning and game visuals not tested"}));
