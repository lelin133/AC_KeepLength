using System.Reflection;
using KeepLength;
using H;
using UnityEngine;
int checks=0;
void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
void Near(float a,float b,string message)=>Check(MathF.Abs(a-b)<1e-6,message);
void Call(string name,LookAtPenis look)=>typeof(Plugin).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{look});
var scene=new HScene();UnityEngine.Object.Objects.Add(scene);
var basis=new Transform{name="cf_j_dan101_00"};
var top=new Transform{name="cf_j_dan109_00",parent=basis,localPosition=new(.012f,.023f,.064f)};
var look=new LookAtPenis{_danTop=top,_danBase=basis};
var plugin=new Plugin();plugin.Load();
void After(){Time.frameCount++;Time.unscaledTime+=.02f;Call("AfterLook",look);}
Near(plugin.ClipOffset.Value,.02f,"requested default offset");
After();Near(ClipDisplay.LastPoint.z,.084f,"positive default advances plane beyond native endpoint");
Call("BeforeLook",look);plugin.ClipOffset.Value=.05f;After();
Near(ClipDisplay.LastPoint.z,.114f,"new upper boundary 0.05 moves clip plane forward");Check(ClipDisplay.Live==1,"upper boundary keeps clipping active");
Call("BeforeLook",look);plugin.ClipOffset.Value=-.02f;After();
Near(ClipDisplay.LastPoint.z,.044f,"lower boundary stays at -0.02");
Call("BeforeLook",look);plugin.ClipOffset.Value=.0501f;After();
Near(top.localPosition.z,.064f,"value above 0.05 returns native pose");Check(ClipDisplay.Live==0,"above-range value releases clipping");
Call("BeforeLook",look);plugin.ClipOffset.Value=-.0201f;After();
Near(top.localPosition.z,.064f,"value below -0.02 returns native pose");
Call("BeforeLook",look);plugin.ClipOffset.Value=0;
After();Near(top.localPosition.z,.1898f,"first write full rest");Near(top.localPosition.x,.012f,"X unchanged");Near(top.localPosition.y,.023f,"Y unchanged");
Near(ClipDisplay.LastPoint.z,.064f,"plane captured before length override");Check(ClipDisplay.Live==1,"clip display created");
Call("BeforeLook",look);Near(top.localPosition.z,.064f,"native receives its original Z");
top.localPosition=new(.02f,.03f,.028f);After();Near(top.localPosition.z,.1898f,"second frame retains full length");
plugin.Enabled.Value=false;plugin.Tick();Near(top.localPosition.z,.028f,"disable restores latest native Z");Near(top.localPosition.x,.02f,"restore preserves current X");
Check(ClipDisplay.Live==0,"disable releases display");
plugin.Enabled.Value=true;After();Call("BeforeLook",look);top.localPosition=new(.02f,.03f,.1898f);look._currentData!.IsTopStick=false;After();Near(top.localPosition.z,.1898f,"unstick cannot resurrect old compressed pose");
top.localPosition=new(0,0,.07f);After();Near(top.localPosition.z,.1898f,"native shrink is covered even when TopStick is false");
look._currentData.IsTopStick=true;top.localPosition=new(0,0,.07f);plugin.Multiplier.Value=1.2f;After();Near(top.localPosition.z,.22776f,"live multiplier");
Call("BeforeLook",look);top.localPosition=new(0,0,.08f);look._currentData.IsManual=true;After();Near(top.localPosition.z,.22776f,"manual-marked native shrink covered");
Near(ClipDisplay.LastPoint.z,.08f,"manual path clips at the native endpoint");
Call("BeforeLook",look);Near(top.localPosition.z,.08f,"manual path restores original Z before native update");
plugin.AllNativeShrink.Value=false;After();Near(top.localPosition.z,.08f,"legacy mode still excludes manual");Check(ClipDisplay.Live==0,"legacy manual fallback releases clip display");
plugin.AllNativeShrink.Value=true;
look._currentData.IsManual=false;top.name="other";After();Near(top.localPosition.z,.08f,"unknown skeleton excluded");
top.name="cf_j_dan109_00";After();top.localPosition=new(0,0,.11f);plugin.Enabled.Value=false;plugin.Tick();Near(top.localPosition.z,.11f,"later external write preserved");
plugin.Enabled.Value=true;After();scene.Ready=false;plugin.Tick();Near(top.localPosition.z,.11f,"scene not ready restores");
scene.Ready=true;After();Call("BeforeRelease",look);Near(top.localPosition.z,.11f,"release restores");
After();Time.frameCount+=3;plugin.Tick();Near(top.localPosition.z,.11f,"stale component releases its override");
After();Input.PressedKey=KeyCode.F6;plugin.Tick();Input.PressedKey=KeyCode.None;Check(!plugin.Enabled.Value,"F6 toggles off");Near(top.localPosition.z,.11f,"F6 immediate restore");Check(ClipDisplay.Live==0,"F6 restores display");
plugin.Enabled.Value=true;plugin.ClipOffset.Value=-.005f;After();Near(ClipDisplay.LastPoint.z,.105f,"negative clip offset hides more");
Call("BeforeLook",look);plugin.ClipEnabled.Value=false;After();Near(top.localPosition.z,.22776f,"clipping off retains keep length");Check(ClipDisplay.Live==0,"clipping off restores original renderer");
Call("BeforeLook",look);plugin.ClipEnabled.Value=true;ClipDisplay.Available=false;After();Near(top.localPosition.z,.11f,"missing renderer leaves native pose");Check(ClipDisplay.Live==0,"missing renderer no leaked display");
ClipDisplay.Available=true;ClipDisplay.ThrowOnApply=true;After();Near(top.localPosition.z,.11f,"bake failure restores native pose");Check(ClipDisplay.Live==0,"bake failure releases resources");ClipDisplay.ThrowOnApply=false;
plugin.Enabled.Value=true;After();
plugin.FrameTrace.Value=true;
Call("BeforeLook",look);Near(top.localPosition.z,.11f,"tracing does not change restored input");
After();Near(top.localPosition.z,.22776f,"native no-op still follows unchanged production policy");
top.localPosition=new(0,0,.16f);plugin.ObserveFrameEnd();
// A deliberate later writer is visible at end of frame, before the next prefix.
plugin.FrameTrace.Value=false;plugin.ObserveFrameEnd();
using(var trace=System.Text.Json.JsonDocument.Parse(File.ReadAllLines(plugin.TracePath!).Single(l=>l.Contains("\"kind\":\"frame\""))))
{
    var f=trace.RootElement;
    Near(f.GetProperty("savedNativeZ").GetSingle(),.11f,"trace captures saved native input");
    Near(f.GetProperty("afterRestoreZ").GetSingle(),.11f,"trace captures restored input");
    Near(f.GetProperty("afterNativeZ").GetSingle(),.11f,"trace identifies unchanged native value");
    Near(f.GetProperty("afterWriteZ").GetSingle(),.22776f,"trace captures override");
    Near(f.GetProperty("endZ").GetSingle(),.16f,"trace captures later external writer");
    Check(f.GetProperty("completion").GetString()=="end-of-frame","end-of-frame is explicitly labeled");
}
Call("BeforeLook",look);top.localPosition=new(0,0,.11f);After();
plugin.FrameTrace.Value=true;Call("BeforeLook",look);After();
// Without an end-of-frame callback, the following prefix flushes the previous
// observation with an explicit fallback label rather than inventing frame end.
Call("BeforeLook",look);After();
Call("BeforeLook",look);top.localPosition=new(0,0,.22776f);After();plugin.ObserveFrameEnd();
plugin.FrameTrace.Value=false;plugin.ObserveFrameEnd();
var fallbackLines=File.ReadAllLines(plugin.TracePath!);
Check(fallbackLines.Any(l=>l.Contains("next-prefix-without-end-of-frame")),"missing end-of-frame uses explicit fallback");
Check(fallbackLines.Any(l=>l.Contains("clip-released")),"clip lifecycle release is captured");
Check(ClipDisplay.Live==0,"diagnostics did not preserve stale clipping on native full length");
// Trace an actual creation separately from retained displays.
plugin.FrameTrace.Value=true;Call("BeforeLook",look);top.localPosition=new(0,0,.11f);After();plugin.ObserveFrameEnd();
plugin.FrameTrace.Value=false;plugin.ObserveFrameEnd();
Check(File.ReadAllLines(plugin.TracePath!).Any(l=>l.Contains("clip-created")),"clip creation is captured");
// File creation failure must leave normal length and clip logic operational.
string blocker=Path.Combine(AppContext.BaseDirectory,"BepInEx","KeepLengthTraces");
Directory.Move(blocker,blocker+"-"+Guid.NewGuid().ToString("N"));File.WriteAllText(blocker,"test failure");
plugin.FrameTrace.Value=true;Call("BeforeLook",look);After();Near(top.localPosition.z,.22776f,"trace I/O failure leaves pose logic active");
File.Delete(blocker);plugin.FrameTrace.Value=false;plugin.ObserveFrameEnd();
string quota=Path.Combine(AppContext.BaseDirectory,"quota-"+Guid.NewGuid().ToString("N")+".jsonl");
using(var j=new TraceJournal(quota,maxRecords:2,maxBytes:512))
{j.Write(new{n=1});j.Write(new{n=2});j.Write(new{n=3});Check(j.Closed,"record limit stops capture");}
Check(File.ReadAllLines(quota).Length==2,"record quota respected");
string byteQuota=quota+".bytes";
using(var j=new TraceJournal(byteQuota,maxRecords:100,maxBytes:32))
{j.Write(new{text=new string('x',100)});Check(j.Closed,"byte limit stops capture");}
Check(new FileInfo(byteQuota).Length<=32,"byte quota respected");
plugin.Unload();Near(top.localPosition.z,.11f,"unload restores");Check(Plugin.Active==null,"unload releases callbacks");
Check(ClipDisplay.Live==0,"unload releases display");
// Reproduce the captured withdrawal boundary: the native full length equals
// our override, but the saved native value is the last compressed length.
scene.Ready=true;look._currentData!.IsTopStick=true;look._currentData.IsManual=false;
top.localPosition=new(0,0,.18804462f);
var withdrawal=new Plugin();withdrawal.Load();After();
Near(top.localPosition.z,.1898f,"captured boundary starts with an owned override");
look._currentData.IsTopStick=false;
Call("BeforeLook",look);
Near(top.localPosition.z,.1898f,"withdrawal does not overwrite full animation length with stale native Z");
After();Check(ClipDisplay.Live==0,"withdrawal releases clipping without changing RestLocalZ");
for(int i=0;i<20;i++){Call("BeforeLook",look);After();}
Near(top.localPosition.z,.1898f,"withdrawal remains at full length");Check(ClipDisplay.Live==0,"withdrawal does not restart old clipping");
// All-native coverage still handles real shrink in TopStick=false frames.
top.localPosition=new(0,0,.07f);Call("BeforeLook",look);After();
Near(top.localPosition.z,.1898f,"actual non-TopStick native shrink remains covered");
Near(ClipDisplay.LastPoint.z,.09f,"actual non-TopStick shrink uses the native plane with offset");
top.localPosition=new(0,0,.06f);Call("BeforeLook",look);Near(top.localPosition.z,.06f,"handoff preserves fresh animation-authored shrink");After();
Near(ClipDisplay.LastPoint.z,.08f,"fresh non-TopStick shrink updates clipping plane");
top.localPosition=new(0,0,.1898f);Call("BeforeLook",look);After();Check(ClipDisplay.Live==0,"next withdrawal releases clip again");
// The riding/manual path stays on the existing restore strategy.
look._currentData.IsTopStick=true;look._currentData.IsManual=true;
top.localPosition=new(0,0,.059231f);After();Call("BeforeLook",look);
Near(top.localPosition.z,.059231f,"manual riding path still restores its original input");After();
Near(top.localPosition.z,.1898f,"manual riding path still keeps length");
withdrawal.Enabled.Value=false;withdrawal.Tick();Near(top.localPosition.z,.059231f,"manual riding disable still restores native length");
withdrawal.Unload();Check(ClipDisplay.Live==0,"withdrawal regression leaves no proxy");
// Quaternion continuity: a sign change is not a visible turn, and the guard
// must preserve the pointing direction while removing a half-turn branch.
var identity=System.Numerics.Quaternion.Identity;
var halfTurn=new System.Numerics.Quaternion(0,0,1,0);
Check(!RollPolicy.HalfTurn(identity,new(0,0,0,-1),out _,out _),"quaternion sign does not trigger guard");
Check(RollPolicy.HalfTurn(identity,halfTurn,out _,out _),"pure axial half-turn detected");
Check(!RollPolicy.HalfTurn(identity,System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitZ,.3f),out _,out _),"gradual roll is preserved");
Check(!RollPolicy.HalfTurn(identity,System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY,MathF.PI),out _,out _),"aim reversal is not an axial half turn");
Check(!RollPolicy.Normalize(new(float.NaN,0,0,1),out _),"invalid rotations rejected");
var rng=new Random(180);
for(int i=0;i<200;i++)
{
    var q=System.Numerics.Quaternion.CreateFromYawPitchRoll((float)rng.NextDouble()*6,(float)rng.NextDouble()*6,(float)rng.NextDouble()*6);
    var flipped=q*halfTurn;
    var corrected=RollPolicy.Correct(flipped,true);
    Check(RollPolicy.HalfTurn(q,flipped,out _,out _),"half-turn detection in arbitrary parent space");
    Check(RollPolicy.SameRotation(q,corrected),"half-turn correction restores same physical rotation");
    Check(System.Numerics.Vector3.Distance(System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitZ,flipped),System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitZ,corrected))<1e-5,"forward direction unchanged");
}
var rotationPlugin=new Plugin();rotationPlugin.Load();rotationPlugin.AnimationReference.Value=false;
basis.parent=new Transform();basis.localRotation=new(0,0,0,1);
look._currentData.IsTopStick=true;look._currentData.IsManual=true;top.localPosition=new(0,0,.07f);
After();Call("BeforeLook",look);basis.localRotation=new(0,0,1,0);After();
Check(RollPolicy.SameRotation(new(basis.localRotation.x,basis.localRotation.y,basis.localRotation.z,basis.localRotation.w),identity),"runtime half-turn is canceled");
Near(top.localPosition.z,.1898f,"rotation correction retains keep length");
Call("BeforeLook",look);Check(MathF.Abs(basis.localRotation.z)>.99f,"native rotation is restored before solver");After();
Check(MathF.Abs(basis.localRotation.w)>.99f,"half-turn branch remains continuously corrected");
look._lookAtNullT=new Transform();Call("BeforeLook",look);After();
Check(MathF.Abs(basis.localRotation.w)>.99f,"aim-target instance refresh does not discard correction on the same bone");
// Regression from frame 30265: raw aim moved by 30.49 degrees without a new
// twist. The existing correction must survive that threshold crossing.
var turnedAim=System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY,30.49f*MathF.PI/180);
var turnedNative=turnedAim*halfTurn;
Call("BeforeLook",look);basis.localRotation=new(turnedNative.X,turnedNative.Y,turnedNative.Z,turnedNative.W);After();
Check(RollPolicy.SameRotation(new(basis.localRotation.x,basis.localRotation.y,basis.localRotation.z,basis.localRotation.w),turnedAim),"large aim change retains existing half-turn branch");
for(int i=0;i<20;i++){Call("BeforeLook",look);basis.localRotation=new(turnedNative.X,turnedNative.Y,turnedNative.Z,turnedNative.W);After();}
Check(RollPolicy.SameRotation(new(basis.localRotation.x,basis.localRotation.y,basis.localRotation.z,basis.localRotation.w),turnedAim),"no delayed half-turn after large aim change");
rotationPlugin.PreventHalfTurn.Value=false;rotationPlugin.Tick();Check(RollPolicy.SameRotation(new(basis.localRotation.x,basis.localRotation.y,basis.localRotation.z,basis.localRotation.w),turnedNative),"turning guard off restores native rotation");
rotationPlugin.PreventHalfTurn.Value=true;basis.localRotation=new(0,0,0,1);After();
Call("BeforeLook",look);basis.localRotation=new(0,0,1,0);scene._isNowChangeAnim=true;After();
Check(MathF.Abs(basis.localRotation.w)>.99f,"same-bone transition now suppresses axial half-turn");
var savedData=look._currentData;look._currentData=null;
Call("BeforeLook",look);After();Check(MathF.Abs(basis.localRotation.w)>.99f,"transient missing motion data preserves correction");
look._currentData=savedData;Time.unscaledTime+=.5f;Call("BeforeLook",look);After();
Check(MathF.Abs(basis.localRotation.w)>.99f,"long gap retains established correction without classifying new flips");
scene._isNowChangeAnim=false;rotationPlugin.Unload();
// First observation must not accept a solver half-turn relative to this
// frame's animation, even when there is no previous native sample.
var startup=new Plugin();startup.Load();startup.AnimationReference.Value=false;basis.localRotation=new(0,0,0,1);
Time.frameCount++;Time.unscaledTime+=.02f;Call("BeforeLook",look);basis.localRotation=new(0,0,1,0);Call("AfterLook",look);
Check(MathF.Abs(basis.localRotation.w)>.99f,"first solver half-turn uses same-frame animation reference");
Call("BeforeRelease",look);
basis.localRotation=new(0,0,0,1);Time.frameCount++;Time.unscaledTime+=.02f;Call("BeforeLook",look);basis.localRotation=new(0,0,1,0);Call("AfterLook",look);
Check(MathF.Abs(basis.localRotation.w)>.99f,"same-bone Release retains continuity across motion rebinding");
startup.Enabled.Value=false;startup.Tick();Check(MathF.Abs(basis.localRotation.z)>.99f,"startup correction restores raw rotation on disable");startup.Unload();
if(args.Length>0)
{
    // Replay the actual local rotations around the detected native flip and
    // delayed output flip through the production wrapper, with a stable parent.
    var actual=new Plugin();actual.Load();actual.AnimationReference.Value=false;basis.localRotation=new(0,0,0,1);
    System.Numerics.Quaternion? previousOutput=null;
    int replayed=0;float withdrawalTwist=float.NaN;
    foreach(string line in File.ReadLines(args[0]))
    {
        using var json=System.Text.Json.JsonDocument.Parse(line);var r=json.RootElement;
        if(r.GetProperty("kind").GetString()!="frame")continue;
        int frame=r.GetProperty("frame").GetInt32();if(frame<30204||frame>30270)continue;
        Time.frameCount=frame;Time.unscaledTime=r.GetProperty("time").GetSingle();
        var v=r.GetProperty("nativeBaseLocalRotation").EnumerateArray().Select(x=>x.GetSingle()).ToArray();
        scene.Ready=r.GetProperty("ready").GetBoolean();scene._isNowChangeAnim=r.GetProperty("changing").GetBoolean();
        look._currentData!.IsTopStick=r.GetProperty("topStick").GetBoolean();look._currentData.IsManual=r.GetProperty("manual").GetBoolean();
        Call("BeforeLook",look);basis.localRotation=new(v[0],v[1],v[2],v[3]);Call("AfterLook",look);
        var output=new System.Numerics.Quaternion(basis.localRotation.x,basis.localRotation.y,basis.localRotation.z,basis.localRotation.w);
        if(previousOutput.HasValue){RollPolicy.HalfTurn(previousOutput.Value,output,out _,out var twist);if(frame==30265)withdrawalTwist=twist;}
        previousOutput=output;replayed++;
    }
    Check(replayed==67,"observed withdrawal segment replayed");
    Check(float.IsFinite(withdrawalTwist)&&MathF.Abs(withdrawalTwist)<1,"captured 30265 output now turns without axial flip");
    Console.WriteLine($"Captured roll replay: {replayed} frames passed; frame30265 axial twist={withdrawalTwist:F6} degrees. Managed wrapper only.");
    actual.Unload();
}
for(int traceIndex=1;traceIndex<Math.Min(args.Length,4);traceIndex++)
{
    var insertion=new Plugin();insertion.Load();insertion.AnimationReference.Value=false;basis.localRotation=new(0,0,0,1);
    System.Numerics.Quaternion? previous=null;
    int checkedFlips=0,replayed=0;
    var observed=traceIndex==1 ? new HashSet<int>{19084,19290,19388,21441,21896,22747}
        :traceIndex==2?new HashSet<int>{24479,24480,24760,24761,25324,27290,27945}
        :new HashSet<int>{10956,10957};
    var values=new List<string>();
    foreach(string line in File.ReadLines(args[traceIndex]))
    {
        System.Text.Json.JsonDocument doc;
        try{doc=System.Text.Json.JsonDocument.Parse(line);}catch(System.Text.Json.JsonException){continue;}
        using(doc)
        {
            var r=doc.RootElement;
            if(r.GetProperty("kind").GetString()=="event"&&traceIndex>=3&&r.GetProperty("name").GetString()=="component-release")
            {Call("BeforeRelease",look);continue;}
            if(r.GetProperty("kind").GetString()!="frame")continue;
            int frame=r.GetProperty("frame").GetInt32();
            Time.frameCount=frame;Time.unscaledTime=r.GetProperty("time").GetSingle();
            scene.Ready=r.GetProperty("ready").GetBoolean();scene._isNowChangeAnim=r.GetProperty("changing").GetBoolean();
            bool hasData=r.GetProperty("manual").ValueKind!=System.Text.Json.JsonValueKind.Null;
            look._currentData=hasData?new MotionData{IsTopStick=r.GetProperty("topStick").GetBoolean(),IsManual=r.GetProperty("manual").GetBoolean(),LookAtNullName=r.GetProperty("target").GetString()!}:null;
            // Old traces lack the identities behind reset events. Inject a
            // target rebind to verify the new semantic rule, not to assert
            // that the original reset's exact identity cause was recorded.
            if(traceIndex==2&&r.GetProperty("events").EnumerateArray().Any(e=>e.GetString()=="roll-reference-reset"))
                look._lookAtNullT=new Transform();
            var a=r.GetProperty("nativeBaseLocalRotation").EnumerateArray().Select(x=>x.GetSingle()).ToArray();
            if(traceIndex>=3)
            {
                var input=r.GetProperty("beforeBaseLocalRotation").EnumerateArray().Select(x=>x.GetSingle()).ToArray();
                basis.localRotation=new(input[0],input[1],input[2],input[3]);
            }
            Call("BeforeLook",look);basis.localRotation=new(a[0],a[1],a[2],a[3]);Call("AfterLook",look);
            var output=new System.Numerics.Quaternion(basis.localRotation.x,basis.localRotation.y,basis.localRotation.z,basis.localRotation.w);
            if(observed.Contains(frame)&&previous.HasValue)
            {
                RollPolicy.HalfTurn(previous.Value,output,out _,out var twist);
                Check(MathF.Abs(twist)<1,$"recorded insertion frame {frame} no longer flips axially");
                values.Add($"{frame}:{twist:F6}");checkedFlips++;
            }
            previous=output;replayed++;
        }
    }
    Check(checkedFlips==observed.Count,"captured insertion/transition candidates checked");
    Console.WriteLine($"Captured insertion replay: {replayed} frames; axial twist degrees [{string.Join(",",values)}]. Managed wrapper only.");
    insertion.Unload();
}
// The general reference method must work for arbitrary solver rolls, not only
// exact half-turns or named posture IDs, while preserving aim and animation roll.
for(int i=0;i<300;i++)
{
    var reference=System.Numerics.Quaternion.CreateFromYawPitchRoll((float)rng.NextDouble()*6,(float)rng.NextDouble()*6,(float)rng.NextDouble()*6);
    var swing=System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY,(float)rng.NextDouble()*2.8f-1.4f);
    var twist=System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitZ,(float)rng.NextDouble()*6.2f-3.1f);
    var native=reference*swing*twist;
    Check(RollPolicy.AlignTwist(reference,native,out var result),"general reference accepts arbitrary twist");
    Check(RollPolicy.SameRotation(result,reference*swing),"general reference removes only solver twist");
    Check(System.Numerics.Vector3.Distance(System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitZ,native),System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitZ,result))<1e-5,"general reference retains solver forward");
}
Check(!RollPolicy.AlignTwist(identity,new(1,0,0,0),out _),"exact aim reversal has explicit fallback");
for(int generalIndex=4;generalIndex<args.Length;generalIndex++)
{
    var general=new Plugin();general.Load();Check(general.AnimationReference.Value,"general mode default enabled");
    bool coldReplay=Path.GetFileName(args[generalIndex]).Contains("cold-entry");
    bool previousEnabled=true;
    if(coldReplay)basis.parent=new(){name="cf_j_dan100_00"};
    basis.localRotation=new(0,0,0,1);scene.Ready=true;
    System.Numerics.Quaternion? previous=null;float previousTime=0,maxTwist=0,maxAimError=0;int count=0,windowFrames=0;double windowTravel=0;
    foreach(var line in File.ReadLines(args[generalIndex]))
    {
        System.Text.Json.JsonDocument doc;try{doc=System.Text.Json.JsonDocument.Parse(line);}catch(System.Text.Json.JsonException){continue;}
        using(doc)
        {
            var r=doc.RootElement;var kind=r.GetProperty("kind").GetString();
            if(kind=="event"&&r.GetProperty("name").GetString()=="component-release"){Call("BeforeRelease",look);continue;}
            if(kind!="frame")continue;
            Time.frameCount=r.GetProperty("frame").GetInt32();Time.unscaledTime=r.GetProperty("time").GetSingle();
            if(coldReplay)
            {
                general.Enabled.Value=r.GetProperty("enabled").GetBoolean();
                scene.NowAnimationInfo=new(){ID=r.GetProperty("actionId").GetInt32(),Mode=r.GetProperty("actionMode").GetInt32(),Kind=r.GetProperty("actionKind").GetInt32()};
            }
            scene.Ready=r.GetProperty("ready").GetBoolean();scene._isNowChangeAnim=r.GetProperty("changing").GetBoolean();
            bool hasData=r.GetProperty("manual").ValueKind!=System.Text.Json.JsonValueKind.Null;
            look._currentData=hasData?new MotionData{IsTopStick=r.GetProperty("topStick").GetBoolean(),IsManual=r.GetProperty("manual").GetBoolean(),LookAtNullName=r.GetProperty("target").GetString()!}:null;
            var a=r.GetProperty("beforeBaseLocalRotation").EnumerateArray().Select(x=>x.GetSingle()).ToArray();
            var b=r.GetProperty("nativeBaseLocalRotation").EnumerateArray().Select(x=>x.GetSingle()).ToArray();
            basis.localRotation=new(a[0],a[1],a[2],a[3]);Call("BeforeLook",look);
            var native=new System.Numerics.Quaternion(b[0],b[1],b[2],b[3]);basis.localRotation=new(b[0],b[1],b[2],b[3]);Call("AfterLook",look);
            var output=new System.Numerics.Quaternion(basis.localRotation.x,basis.localRotation.y,basis.localRotation.z,basis.localRotation.w);
            float aimError=System.Numerics.Vector3.Distance(System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitZ,native),System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitZ,output));maxAimError=MathF.Max(maxAimError,aimError);
            if(previous.HasValue&&Time.unscaledTime-previousTime<.15f&&scene.Ready&&(!coldReplay||general.Enabled.Value&&previousEnabled))
            {
                RollPolicy.HalfTurn(previous.Value,output,out var swing,out var twist);
                if(swing<=170)maxTwist=MathF.Max(maxTwist,MathF.Abs(twist));
                if(Path.GetFileName(args[generalIndex]).Contains("085955")&&Time.frameCount>30121&&Time.frameCount<=30210)
                {windowTravel+=MathF.Abs(twist);windowFrames++;}
            }
            if(coldReplay&&Time.frameCount==26219)
                Check(RollPolicy.HalfTurn(native,output,out _,out _),"recorded first-enable frame26219 now chooses opposite axial branch");
            if(coldReplay&&!general.Enabled.Value)Check(RollPolicy.SameRotation(native,output),"recorded disabled frames remain native");
            previous=output;previousTime=Time.unscaledTime;previousEnabled=general.Enabled.Value;count++;
        }
    }
    Check(count>1000,"multi-pose sample replay contains substantial data");
    Check(maxAimError<1e-5,"multi-pose replay keeps native aim in every recorded frame");
    Check(maxTwist<90.01f,"multi-pose replay avoids half-turn output jumps in nonsingular consecutive frames");
    if(windowFrames>0){Check(windowFrames>=80&&windowTravel<.1,"recorded late oscillation window has negligible cumulative axial motion");Console.WriteLine($"Late oscillation window: {windowFrames} steps, cumulative axial travel={windowTravel:F6} degrees (recorded old output 105.605891).");}
    Console.WriteLine($"General reference replay: {count} frames; maxForwardVectorError={maxAimError:E3}; maxConsecutiveAxialStep={maxTwist:F4} degrees. Stable parent, recorded animation inputs; no posture ID mapping.");
    general.Unload();
}
// Changing animation inputs must not drive continuous axial oscillation.
scene.Ready=true;scene._isNowChangeAnim=false;basis.parent=new Transform();
look._currentData=new(){IsTopStick=false,IsManual=true};top.localPosition=new(0,0,.1898f);basis.localRotation=new(0,0,0,1);
var stablePlugin=new Plugin();stablePlugin.Load();
Check(stablePlugin.StableAxialReference.Value,"stable axial reference defaults on");
Call("BeforeLook",look);Call("AfterLook",look);
for(int i=0;i<120;i++)
{
    var animated=System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitZ,MathF.Sin(i*.4f)*2.9f);
    basis.localRotation=new(animated.X,animated.Y,animated.Z,animated.W);Time.frameCount++;Time.unscaledTime+=.02f;Call("BeforeLook",look);
    basis.localRotation=new(0,0,0,1);Call("AfterLook",look);
    Check(RollPolicy.SameRotation(identity,new(basis.localRotation.x,basis.localRotation.y,basis.localRotation.z,basis.localRotation.w)),"oscillatory animation reference cannot steer stable output");
}
Call("BeforeRelease",look);Time.frameCount++;Call("BeforeLook",look);basis.localRotation=new(0,0,1,0);Call("AfterLook",look);
Check(MathF.Abs(basis.localRotation.w)>.99f,"stable reference survives same-rig Release");
stablePlugin.StableAxialReference.Value=false;Time.frameCount++;basis.localRotation=new(0,0,0,1);Call("BeforeLook",look);Call("AfterLook",look);
Time.frameCount++;Time.unscaledTime+=.02f;basis.localRotation=new(0,0,.5f,.8660254f);Call("BeforeLook",look);basis.localRotation=new(0,0,0,1);Call("AfterLook",look);
Check(MathF.Abs(basis.localRotation.z)>.001f,"disabling stable reference restores reference following");
stablePlugin.Enabled.Value=false;stablePlugin.Tick();Check(MathF.Abs(basis.localRotation.w)>.99f,"stable mode off restores native orientation");stablePlugin.Unload();
Check(RollPolicy.TransportTwist(identity,new(1,0,0,0),out var reverseTransport),"stable transport handles opposite aim");
Check(System.Numerics.Vector3.Distance(System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitZ,reverseTransport),-System.Numerics.Vector3.UnitZ)<1e-5,"opposite-aim transport preserves native forward");
// Selectable native-tip mode: real native length plus offset, never clipping.
scene.NowAnimationInfo=new(){ID=1};
scene.Ready=true;scene._isNowChangeAnim=false;look._currentData=new MotionData{IsTopStick=false,IsManual=false};
top.name="cf_j_dan109_00";basis.localRotation=new(0,0,0,1);top.localPosition=new(.012f,.023f,.064f);
var tipPlugin=new Plugin();tipPlugin.Load();tipPlugin.PreventHalfTurn.Value=false;
void SettleTip(){for(int i=0;i<10;i++){Call("BeforeLook",look);After();}}
Check(tipPlugin.Mode.Value==LengthMode.FixedLengthClip,"existing mode remains default");
Near(tipPlugin.TipOffset.Value,.03f,"native tip mode defaults to requested offset");
Check(tipPlugin.ModeSwitchKey.Value==KeyCode.J,"default mode switch key is J");
After();Check(ClipDisplay.Live==1,"fixed mode still creates clipping before switch");
tipPlugin.Tick();Check(tipPlugin.Mode.Value==LengthMode.FixedLengthClip,"no key leaves mode unchanged");
Input.PressedKey=KeyCode.J;tipPlugin.Tick();Input.PressedKey=KeyCode.None;
Check(tipPlugin.Mode.Value==LengthMode.NativeTipOffset,"J selects mode two without F6 consuming input");
Check(tipPlugin.Enabled.Value,"mode shortcut preserves enabled state");
Near(top.localPosition.z,.064f,"mode switch releases the old full-length write");Check(ClipDisplay.Live==0,"switch removes fixed-mode proxy");
Input.PressedKey=KeyCode.J;tipPlugin.Tick();Input.PressedKey=KeyCode.None;
Check(tipPlugin.Mode.Value==LengthMode.FixedLengthClip,"second J returns to mode one");
tipPlugin.ModeSwitchKey.Value=KeyCode.F7;
Input.PressedKey=KeyCode.J;tipPlugin.Tick();Input.PressedKey=KeyCode.None;
Check(tipPlugin.Mode.Value==LengthMode.FixedLengthClip,"rebinding disables original J shortcut");
Input.PressedKey=KeyCode.F7;tipPlugin.Tick();Input.PressedKey=KeyCode.None;
Check(tipPlugin.Mode.Value==LengthMode.NativeTipOffset,"custom mode shortcut works");
tipPlugin.ModeSwitchKey.Value=KeyCode.None;
Input.PressedKey=KeyCode.J;tipPlugin.Tick();Input.PressedKey=KeyCode.None;
Check(tipPlugin.Mode.Value==LengthMode.NativeTipOffset,"None disables mode shortcut");
tipPlugin.ModeSwitchKey.Value=KeyCode.J;tipPlugin.Enabled.Value=false;
Input.PressedKey=KeyCode.J;tipPlugin.Tick();Input.PressedKey=KeyCode.None;
Check(!tipPlugin.Enabled.Value&&tipPlugin.Mode.Value==LengthMode.FixedLengthClip,"mode can be selected while disabled without enabling plugin");
tipPlugin.Enabled.Value=true;tipPlugin.Mode.Value=LengthMode.NativeTipOffset;
tipPlugin.TipOffset.Value=0;
Call("BeforeLook",look);After();Near(top.localPosition.z,.064f,"zero tip offset leaves native length untouched");
tipPlugin.TipOffset.Value=.02f;Call("BeforeLook",look);After();
Near(top.localPosition.z,.064f,"first activation starts at native length");
Call("BeforeLook",look);After();
Check(top.localPosition.z>.064f&&top.localPosition.z<.084f,"first activation fades in rather than jumping");
SettleTip();
Near(top.localPosition.z,.084f,"tip follows native Z plus offset");Near(top.localPosition.x,.012f,"tip offset preserves native X");Near(top.localPosition.y,.023f,"tip offset preserves native Y");
Check(ClipDisplay.Live==0,"tip mode never applies clipping even if clipping is enabled");
for(int i=0;i<30;i++){Call("BeforeLook",look);After();}
Near(top.localPosition.z,.084f,"native no-op frames do not accumulate offset");
Call("BeforeLook",look);top.localPosition=new(.012f,.023f,.10f);After();Near(top.localPosition.z,.12f,"moving native endpoint remains dynamic");
// Return to an uncompressed/no-op pose after a previously offset frame.
tipPlugin.TipOffset.Value=.03f;
Call("BeforeLook",look);top.localPosition=new(.012f,.023f,.1898f);After();
Near(top.localPosition.z,.2198f,"same action retains offset at full native length");
for(int i=0;i<30;i++){Call("BeforeLook",look);After();}
Near(top.localPosition.z,.2198f,"same-action full-length no-op frames retain offset without accumulation");
Call("BeforeLook",look);top.localPosition=new(.012f,.023f,.21f);After();
Near(top.localPosition.z,.24f,"same action follows even longer native endpoints");
// Wrapper/loop/target/Release churn does not represent a different posture.
Call("BeforeLook",look);top.localPosition=new(0,0,.1898f);
scene.NowAnimationInfo=new(){ID=1};look._currentData=new(){IsTopStick=false,IsManual=false,LookAtNullName="other-target"};After();
Near(top.localPosition.z,.2198f,"equivalent action descriptor and new target/data retain qualification");
Call("BeforeRelease",look);Near(top.localPosition.z,.1898f,"Release still restores native write");
Call("BeforeLook",look);After();Near(top.localPosition.z,.2198f,"same-rig Release does not forget action qualification");
scene._isNowChangeAnim=true;Call("BeforeLook",look);After();Near(top.localPosition.z,.1898f,"transient changing protection hands back native length");
scene._isNowChangeAnim=false;Call("BeforeLook",look);After();Near(top.localPosition.z,.2198f,"same action resumes qualification after a loop reconfiguration");
scene.Ready=false;tipPlugin.Tick();Near(top.localPosition.z,.1898f,"not-ready still relinquishes active write");
scene.Ready=true;Call("BeforeLook",look);After();Near(top.localPosition.z,.2198f,"temporary not-ready does not erase the posture");
var knownAction=scene.NowAnimationInfo;scene.NowAnimationInfo=null;Call("BeforeLook",look);After();
Near(top.localPosition.z,.1898f,"unknown action identity never applies an old offset");
scene.NowAnimationInfo=knownAction;Call("BeforeLook",look);After();Near(top.localPosition.z,.2198f,"same identity can resume after a transient unavailable descriptor");
// A genuinely different selected posture starts unqualified.
Call("BeforeLook",look);scene.NowAnimationInfo=new(){ID=2};After();
Near(top.localPosition.z,.1898f,"new non-shrinking posture does not inherit old qualification");
SettleTip();Near(top.localPosition.z,.1898f,"new no-op posture remains native indefinitely");
Call("BeforeLook",look);scene.NowAnimationInfo=new(){ID=1};After();
Near(top.localPosition.z,.1898f,"revisiting an old posture does not reuse a historical qualification");
Call("BeforeLook",look);top.localPosition=new(.012f,.023f,.10f);After();
Near(top.localPosition.z,.10f,"new shrink begins a fresh fade");
SettleTip();
Near(top.localPosition.z,.13f,"offset resumes when native shrink resumes");
tipPlugin.TipOffset.Value=.02f;Call("BeforeLook",look);After();
tipPlugin.RestLength.Value=.4f;tipPlugin.Multiplier.Value=2;tipPlugin.ClipOffset.Value=.05f;
Call("BeforeLook",look);After();Near(top.localPosition.z,.12f,"fixed-mode settings do not affect native offset");
look._currentData.IsTopStick=true;look._currentData.IsManual=true;Call("BeforeLook",look);top.localPosition=new(0,0,.03f);After();Near(top.localPosition.z,.05f,"manual-marked native tip path remains supported");
tipPlugin.TipOffset.Value=-.02f;Call("BeforeLook",look);After();Near(top.localPosition.z,.01f,"negative offset moves endpoint toward base");
tipPlugin.TipOffset.Value=-.05f;Call("BeforeLook",look);After();Near(top.localPosition.z,.03f,"nonpositive output fails back to native instead of reversing tip");
tipPlugin.TipOffset.Value=.0501f;Call("BeforeLook",look);After();Near(top.localPosition.z,.03f,"out-of-range tip offset rejected");
tipPlugin.TipOffset.Value=float.NaN;Call("BeforeLook",look);After();Near(top.localPosition.z,.03f,"invalid tip offset rejected");
tipPlugin.TipOffset.Value=.02f;Call("BeforeLook",look);After();scene._isNowChangeAnim=true;Call("BeforeLook",look);After();Near(top.localPosition.z,.03f,"tip mode respects animation change protection");
scene._isNowChangeAnim=false;Call("BeforeLook",look);After();tipPlugin.Enabled.Value=false;tipPlugin.Tick();Near(top.localPosition.z,.03f,"tip mode off restores latest native Z");
tipPlugin.Enabled.Value=true;Call("BeforeLook",look);After();Call("BeforeRelease",look);Near(top.localPosition.z,.03f,"tip mode release restores native Z");
Call("BeforeLook",look);After();top.localPosition=new(0,0,.14f);tipPlugin.Enabled.Value=false;tipPlugin.Tick();Near(top.localPosition.z,.14f,"tip disable preserves newer external write");
tipPlugin.Enabled.Value=true;tipPlugin.TipOffset.Value=0;Call("BeforeLook",look);After();Near(top.localPosition.z,.14f,"returning offset to zero has no residual length");
tipPlugin.RestLength.Value=.1898f;tipPlugin.Multiplier.Value=1;
tipPlugin.Mode.Value=LengthMode.FixedLengthClip;tipPlugin.Tick();Call("BeforeLook",look);top.localPosition=new(0,0,.07f);After();
Near(top.localPosition.z,.1898f,"returning to original mode restores full length");Check(ClipDisplay.Live==1,"original clipping returns after mode switch");
tipPlugin.Mode.Value=LengthMode.NativeTipOffset;tipPlugin.Tick();
ClipDisplay.Available=false;tipPlugin.TipOffset.Value=.02f;Call("BeforeLook",look);After();SettleTip();Near(top.localPosition.z,.09f,"tip mode has no clipping renderer dependency");ClipDisplay.Available=true;
tipPlugin.PreventHalfTurn.Value=true;basis.localRotation=new(0,0,0,1);
Time.frameCount++;Time.unscaledTime+=.02f;Call("BeforeLook",look);basis.localRotation=new(0,0,1,0);Call("AfterLook",look);
Check(MathF.Abs(basis.localRotation.w)>.99f,"shared rotation stabilization also works in tip mode");Near(top.localPosition.z,.09f,"shared rotation preserves tip offset");
// Stable action IDs, rather than animator flags, own the qualification.
Call("BeforeLook",look);top.localPosition=new(0,0,.1898f);After();Near(top.localPosition.z,.2098f,"qualified full length before action identity checks");
scene.NowAnimationInfo=new(){ID=1,Mode=1};tipPlugin.Tick();
Near(top.localPosition.z,.1898f,"Tick immediately releases an old write when action mode changes");
Call("BeforeLook",look);After();Near(top.localPosition.z,.1898f,"same numeric ID in different mode starts unqualified");
Call("BeforeLook",look);top.localPosition=new(0,0,.1f);After();SettleTip();Near(top.localPosition.z,.12f,"new mode can qualify from its own native shrink");
Call("BeforeLook",look);top.localPosition=new(0,0,.1898f);scene.NowAnimationInfo=new(){ID=1,Mode=1,Kind=1};After();
Near(top.localPosition.z,.1898f,"kind change with identical numeric ID also starts unqualified");
Call("BeforeLook",look);top.localPosition=new(0,0,.1f);After();SettleTip();
Call("BeforeLook",look);top.localPosition=new(0,0,.1898f);After();Near(top.localPosition.z,.2098f,"current kind retains its new observation");
Input.PressedKey=KeyCode.F6;tipPlugin.Tick();Input.PressedKey=KeyCode.None;
Near(top.localPosition.z,.1898f,"F6 hands back native full length");
Input.PressedKey=KeyCode.F6;tipPlugin.Tick();Input.PressedKey=KeyCode.None;
Call("BeforeLook",look);After();Near(top.localPosition.z,.1898f,"reenabling at rest begins a new fade");
SettleTip();Near(top.localPosition.z,.2098f,"F6 does not erase this action's qualification");
Input.PressedKey=KeyCode.J;tipPlugin.Tick();Input.PressedKey=KeyCode.None;
Near(top.localPosition.z,.1898f,"J releases old full-length offset immediately");
Input.PressedKey=KeyCode.J;tipPlugin.Tick();Input.PressedKey=KeyCode.None;
Call("BeforeLook",look);After();SettleTip();Near(top.localPosition.z,.2098f,"J back to mode two retains observation and fades in at rest");
// Rig replacement must not inherit another rig's observation.
var oldTop=top;
Call("BeforeRelease",look);top=new(){name="cf_j_dan109_00",parent=basis,localPosition=new(0,0,.1898f)};look._danTop=top;
Call("BeforeLook",look);After();Near(top.localPosition.z,.1898f,"new rig in same action does not inherit old rig qualification");
Call("BeforeLook",look);top.localPosition=new(0,0,.1f);After();SettleTip();
Call("BeforeLook",look);top.localPosition=new(0,0,.1898f);After();Near(top.localPosition.z,.2098f,"new rig can qualify independently");
scene.IsEnded=true;tipPlugin.Tick();Near(top.localPosition.z,.1898f,"scene end restores offset");
scene.IsEnded=false;Time.unscaledTime+=2;
Call("BeforeLook",look);After();Near(top.localPosition.z,.1898f,"scene end clears action history even if same instance is reused");
// Restore the fixture for the original unload regression.
top=oldTop;look._danTop=top;top.localPosition=new(0,0,.07f);Call("BeforeLook",look);After();SettleTip();
tipPlugin.Unload();Near(top.localPosition.z,.07f,"tip unload restores native length");Check(ClipDisplay.Live==0,"tip mode leaves no display resources");
Check(LengthPolicy.TryTipOffset(true,true,false,true,true,.10f,.05f,out var tipUpper)&&MathF.Abs(tipUpper-.15f)<1e-6,"upper tip offset boundary");
Check(LengthPolicy.TryTipOffset(true,true,false,true,true,.10f,-.05f,out var tipLower)&&MathF.Abs(tipLower-.05f)<1e-6,"lower tip offset boundary");
Check(!LengthPolicy.TryTipOffset(true,true,false,false,true,.1f,.02f,out _),"tip unknown skeleton excluded");
Check(!LengthPolicy.TryTipOffset(true,false,false,true,true,.1f,.02f,out _),"tip scene not ready excluded");
Check(!LengthPolicy.TryTipOffset(true,true,false,true,false,.1898f,.03f,out _),"never-shrinking action at rest stays native");
Check(!LengthPolicy.TryTipOffset(true,true,false,true,false,.21f,-.03f,out _),"unqualified action also excludes negative offsets");
Check(LengthPolicy.TryTipOffset(true,true,false,true,true,.1898f,.03f,out var fullTip)&&MathF.Abs(fullTip-.2198f)<1e-6,"qualified action at rest retains offset");
Check(LengthPolicy.TryTipOffset(true,true,false,true,true,.1898f,-.03f,out var negativeTip)&&MathF.Abs(negativeTip-.1598f)<1e-6,"qualified action negative offset remains at rest too");
var envelope=new NativeTipLatch();envelope.Observe(.1897995f);Check(!envelope.HasShrunk,"sub-epsilon rest noise does not qualify action");
envelope.Observe(float.NaN);envelope.Observe(-.1f);Check(!envelope.HasShrunk,"invalid lengths cannot qualify action");
envelope.Observe(.1897f);Check(envelope.HasShrunk,"genuine small shrink qualifies action");
Near(envelope.Advance(0),0,"envelope starts with no added length");
float lastWeight=0;
for(int i=1;i<=10;i++){float w=envelope.Advance(i*.02f);Check(w>=lastWeight&&w<=1,"fade weight is bounded and monotonic");lastWeight=w;}
Near(lastWeight,1,"fade completes after configured duration");
envelope.Observe(.1898f);Check(envelope.HasShrunk,"rest length cannot dequalify current action");
envelope.Pause();Check(envelope.HasShrunk,"pause retains observation");Near(envelope.Advance(1),0,"pause restarts activation fade");
foreach(float angle in new[]{140f,145.245f,148.738f,149.9f,150.1f,179.9f,-140.3f,-179.9f})
{
    var nativeAim=System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY,.15f);
    var candidate=nativeAim*System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitZ,angle*MathF.PI/180);
    Check(RollPolicy.ContinuousTwist(identity,candidate,nativeAim,3,out var continuous),"continuous branch accepts sub-threshold reference jumps");
    RollPolicy.HalfTurn(nativeAim,continuous,out _,out var residual);
    Check(MathF.Abs(residual)<=3.001f,"animation-reference residual is bounded rather than flipping");
    Check(System.Numerics.Vector3.Distance(System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitZ,nativeAim),System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitZ,continuous))<1e-5,"reference limiter preserves native aim");
}
var slow=System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitZ,.5f*MathF.PI/180);
Check(RollPolicy.ContinuousTwist(identity,slow,identity,3,out var slowOutput)&&RollPolicy.SameRotation(slow,slowOutput),"slow intentional animation roll remains unchanged");
// Regression: toggling off must restore native, but not forget the corrected
// reference and then bootstrap from that flipped native on re-enable.
scene.Ready=true;scene.IsEnded=false;scene._isNowChangeAnim=false;scene.NowAnimationInfo=new(){ID=41,Mode=2,Kind=0};
look._currentData=new(){IsTopStick=false,IsManual=true};basis.parent=new();basis.localRotation=new(0,0,0,1);
top.name="cf_j_dan109_00";top.parent=basis;
var resumePlugin=new Plugin();resumePlugin.Load();
Time.frameCount++;Time.unscaledTime+=.02f;Call("BeforeLook",look);basis.localRotation=new(0,0,1,0);Call("AfterLook",look);
Check(MathF.Abs(basis.localRotation.w)>.99f,"resume fixture starts corrected");
Input.PressedKey=KeyCode.F6;resumePlugin.Tick();Input.PressedKey=KeyCode.None;
Check(MathF.Abs(basis.localRotation.z)>.99f,"off still restores flipped native orientation");
for(int i=0;i<300;i++){Time.frameCount++;Time.unscaledTime+=.02f;Call("BeforeLook",look);basis.localRotation=new(0,0,1,0);Call("AfterLook",look);resumePlugin.Tick();}
Check(MathF.Abs(basis.localRotation.z)>.99f,"disabled frames remain under native control");
Input.PressedKey=KeyCode.F6;resumePlugin.Tick();Input.PressedKey=KeyCode.None;
Call("BeforeLook",look);basis.localRotation=new(0,0,1,0);Call("AfterLook",look);
Check(MathF.Abs(basis.localRotation.w)>.99f,"F6 re-enable after long idle restores stable reference");
resumePlugin.PreventHalfTurn.Value=false;resumePlugin.Tick();Time.frameCount+=100;Time.unscaledTime+=2;resumePlugin.Tick();
resumePlugin.PreventHalfTurn.Value=true;resumePlugin.Tick();Call("BeforeLook",look);basis.localRotation=new(0,0,1,0);Call("AfterLook",look);
Check(MathF.Abs(basis.localRotation.w)>.99f,"individual rotation toggle also resumes old reference");
for(int i=0;i<4;i++){resumePlugin.Enabled.Value=false;resumePlugin.Tick();resumePlugin.Enabled.Value=true;resumePlugin.Tick();Call("BeforeLook",look);basis.localRotation=new(0,0,1,0);Call("AfterLook",look);Check(MathF.Abs(basis.localRotation.w)>.99f,"same-frame repeated toggle preserves correction");}
resumePlugin.Enabled.Value=false;resumePlugin.Tick();scene.NowAnimationInfo.ID=42;resumePlugin.Tick();
resumePlugin.Enabled.Value=true;resumePlugin.Tick();Time.frameCount++;basis.localRotation=new(0,0,1,0);Call("BeforeLook",look);Call("AfterLook",look);
Check(MathF.Abs(basis.localRotation.z)>.99f,"different action while disabled cannot reuse old stable anchor");
resumePlugin.Unload();
// First entry into a posture while disabled must also observe an anchor.
var coldStart=new Plugin();coldStart.Load();coldStart.Enabled.Value=false;
scene.NowAnimationInfo=new(){ID=41,Mode=2,Kind=0};basis.localRotation=new(0,0,0,1);
Time.frameCount++;Time.unscaledTime+=.02f;Call("BeforeLook",look);basis.localRotation=new(0,0,1,0);Call("AfterLook",look);
Check(MathF.Abs(basis.localRotation.z)>.99f,"disabled first-entry observation does not correct native pose");
for(int i=0;i<180;i++){Time.frameCount++;Time.unscaledTime+=.02f;Call("BeforeLook",look);Call("AfterLook",look);coldStart.Tick();}
Check(MathF.Abs(basis.localRotation.z)>.99f,"disabled observed reference does not write through idle frames");
coldStart.Enabled.Value=true;coldStart.Tick();Call("BeforeLook",look);Call("AfterLook",look);
Check(MathF.Abs(basis.localRotation.w)>.99f,"first enable after disabled entry corrects flipped idle");
coldStart.Enabled.Value=false;coldStart.Tick();scene.NowAnimationInfo.ID=43;coldStart.Tick();
basis.localRotation=new(0,0,0,1);Time.frameCount++;Call("BeforeLook",look);basis.localRotation=new(0,0,1,0);Call("AfterLook",look);
coldStart.Enabled.Value=true;coldStart.Tick();Call("BeforeLook",look);Call("AfterLook",look);
Check(MathF.Abs(basis.localRotation.w)>.99f,"different posture entered disabled gathers its own fresh reference");
coldStart.Unload();
var rotationOffEntry=new Plugin();rotationOffEntry.Load();rotationOffEntry.PreventHalfTurn.Value=false;
basis.localRotation=new(0,0,0,1);Time.frameCount++;Call("BeforeLook",look);basis.localRotation=new(0,0,1,0);Call("AfterLook",look);
Check(MathF.Abs(basis.localRotation.z)>.99f,"individual guard off also observes without writing");
rotationOffEntry.PreventHalfTurn.Value=true;rotationOffEntry.Tick();Call("BeforeLook",look);Call("AfterLook",look);
Check(MathF.Abs(basis.localRotation.w)>.99f,"individual guard first enable uses disabled observation");rotationOffEntry.Unload();
var ambiguousEntry=new Plugin();ambiguousEntry.Load();ambiguousEntry.Enabled.Value=false;
basis.parent=new(){name="unknown-custom-parent"};
basis.localRotation=new(0,0,1,0);Time.frameCount++;Call("BeforeLook",look);Call("AfterLook",look);
ambiguousEntry.Enabled.Value=true;ambiguousEntry.Tick();Call("BeforeLook",look);Call("AfterLook",look);
Check(MathF.Abs(basis.localRotation.z)>.99f,"identical already-flipped no-op input does not invent a correct reference");ambiguousEntry.Unload();
var animationFlipOff=new Plugin();animationFlipOff.Load();animationFlipOff.Enabled.Value=false;
basis.localRotation=new(0,0,0,1);Time.frameCount++;Call("BeforeLook",look);Call("AfterLook",look);
// Animator changes the branch before both native observation stages.
basis.localRotation=new(0,0,1,0);Time.frameCount++;Call("BeforeLook",look);Call("AfterLook",look);
Check(MathF.Abs(basis.localRotation.z)>.99f,"disabled animation half-turn is only observed, not corrected");
animationFlipOff.Enabled.Value=true;animationFlipOff.Tick();Call("BeforeLook",look);Call("AfterLook",look);
Check(MathF.Abs(basis.localRotation.w)>.99f,"first enable also uses a half-turn observed between disabled animation frames");animationFlipOff.Unload();
var bindStart=new Plugin();bindStart.Load();bindStart.Enabled.Value=false;
basis.parent=new(){name="cf_j_dan100_00"};basis.localRotation=new(3.2694097E-05f,.8397069f,-.54304f,4.7044337E-05f);
Time.frameCount++;Call("BeforeLook",look);Call("AfterLook",look);
var recordedNative=new System.Numerics.Quaternion(basis.localRotation.x,basis.localRotation.y,basis.localRotation.z,basis.localRotation.w);
Check(MathF.Abs(basis.localRotation.z)>.5f,"verified rest fallback still does not write while disabled");
bindStart.Enabled.Value=true;bindStart.Tick();Call("BeforeLook",look);Call("AfterLook",look);
Check(RollPolicy.HalfTurn(recordedNative,new(basis.localRotation.x,basis.localRotation.y,basis.localRotation.z,basis.localRotation.w),out var bindAim,out _)&&bindAim<.1f,"known rest fallback flips initial wrong branch while retaining aim");bindStart.Unload();
var bindOptOut=new Plugin();bindOptOut.Load();bindOptOut.BindPoseFallback.Value=false;
basis.localRotation=new(recordedNative.X,recordedNative.Y,recordedNative.Z,recordedNative.W);Time.frameCount++;Call("BeforeLook",look);Call("AfterLook",look);
Check(RollPolicy.SameRotation(recordedNative,new(basis.localRotation.x,basis.localRotation.y,basis.localRotation.z,basis.localRotation.w)),"rest fallback opt-out preserves prior bootstrap behavior");bindOptOut.Unload();
Console.WriteLine($"Runtime managed substitutes: {checks} checks passed. Native patch execution and visual skinning remain untested.");
