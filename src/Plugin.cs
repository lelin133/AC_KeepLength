using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using H;
using HarmonyLib;
using UnityEngine;
using UObj = UnityEngine.Object;

namespace KeepLength;

[BepInPlugin(Id, "AC 插入保持长度（测试）", "0.10.1")]
[BepInProcess("Aicomi.exe")]
public sealed partial class Plugin : BasePlugin
{
    public const string Id = "local.ac.keeplength";
    internal static Plugin? Active;
    internal ConfigEntry<bool> Enabled = null!, AllNativeShrink = null!, DebugLog = null!;
    internal ConfigEntry<float> RestLength = null!, Multiplier = null!;
    internal ConfigEntry<KeyCode> ToggleKey = null!, ModeSwitchKey = null!;
    internal ConfigEntry<bool> ClipEnabled = null!;
    internal ConfigEntry<float> ClipOffset = null!;
    internal ConfigEntry<LengthMode> Mode = null!;
    internal ConfigEntry<float> TipOffset = null!;
    readonly Dictionary<long, ClipDisplay> displays = new();
    Harmony? harmony;
    Driver? driver;
    readonly Dictionary<long, WriteState> writes = new();
    HScene? scene;
    float nextDiscovery, nextWarning, nextDebug, nextSkipDebug;
    bool firstHook, firstWrite;

    public override void Load()
    {
        Enabled = Config.Bind("General", "Enabled", true, "插入贴合期间保持长度；关闭后恢复游戏控制。F6可即时切换。");
        Mode = Config.Bind("General", "Mode", LengthMode.FixedLengthClip,
            "FixedLengthClip：固定长度并裁切；NativeTipOffset：保留原生伸缩，真实末端按NativeTip/Offset前后偏移。两模式互选，防翻转共用。");
        TipOffset = Config.Bind("NativeTip", "Offset", .03f,new ConfigDescription(
            "NativeTipOffset模式：当前动作观察到原生缩短后平滑启用，并保持至换动作；同动作恢复完整长度也不撤掉偏移。真实末端=本帧原生末端Z+此偏移，正值向前、负值向根部，默认0.03，0保持原生。完整长度基准0.1898；与Clipping/Offset及Length设置独立，根部和原生X/Y不改。",
            new AcceptableValueRange<float>(-.05f,.05f)));
        RestLength = Config.Bind("Length", "RestLocalZ", LengthPolicy.DefaultLength,
            new ConfigDescription("末端骨骼未压缩时的本地Z。默认0.1898来自AC实测；不从已压缩帧自动取值。人物现有父骨骼缩放继续生效。",
                new AcceptableValueRange<float>(.01f, .5f)));
        Multiplier = Config.Bind("Length", "Multiplier", 1f, new ConfigDescription("基准长度倍率，默认1。只改变长度，不改朝向或粗细。",
            new AcceptableValueRange<float>(.25f, 2f)));
        ToggleKey = Config.Bind("General", "ToggleKey", KeyCode.F6, "保持长度功能开关快捷键；None关闭快捷键。");
        ModeSwitchKey = Config.Bind("General", "ModeSwitchKey", KeyCode.J, "两种模式切换快捷键，默认J；每次按下在FixedLengthClip与NativeTipOffset之间切换并保存选择，不改变总开关。None关闭此快捷键。请与ToggleKey使用不同按键。");
        AllNativeShrink = Config.Bind("General", "AllNativeShrink", true,
            "按原生末端是否实际缩短来触发，覆盖IsTopStick或IsManual任意状态。关闭后恢复旧版TopStick且非Manual门控。");
        DebugLog = Config.Bind("Diagnostics", "DebugLog", false, "每5秒记录一次原生Z、写入Z与读取Z，供排查；不是独立画面验收。");
        FrameTrace = Config.Bind("Diagnostics", "FrameTrace", false, "拔出专项逐帧诊断，保存到BepInEx/KeepLengthTraces；最多20000条或32MiB，切为false后再true可开新轮。");
        PreventHalfTurn = Config.Bind("Rotation", "PreventHalfTurn", true, "抑制近180度长轴跳变，保持同一物理骨骼的连续性，不因瞄准目标刷新丢失纠正。指向接近反向的170至180度不推断新半周；F6总开关也控制此项。");
        AnimationReference = Config.Bind("Rotation", "AnimationReference", true, "旋转通用模式，保持原生瞄准方向。StableAxialReference开启时沿用稳定轴向参考；关闭该子选项则逐帧跟随动画参考并限速。关闭本项回到旧半周跳变检测。");
        StableAxialReference = Config.Bind("Rotation", "StableAxialReference", true, "防往复旋转，默认开启，需AnimationReference开启。同一骨架首次取动画朝向，此后沿用稳定输出并跟随原生指向，不逐帧追随动画轴向变化；原版刻意长轴转动也会被抑制。关闭回到0.7.1参考跟随和限速。角色整体父级转动保留。");
        BindPoseFallback = Config.Bind("Rotation", "BindPoseFallback", true, "首次输入已翻转且无可信历史时，按已验证骨架的静止局部轴选择半周分支，保持原生指向；默认true，需AnimationReference及StableAxialReference开启。仅cf_j_dan101_00直属cf_j_dan100_00；自定义静止轴不同可关闭。");
        ClipEnabled = Config.Bind("Clipping", "Enabled", true, "隐藏原生末端界线以外的前端整段。使用临时网格副本；关闭此项仅保长，可能穿模。");
        ClipOffset = Config.Bind("Clipping", "Offset", .02f, new ConfigDescription("裁切面相对原生末端的局部轴向偏移。默认0.02；0按原生末端，负值多藏一点，正值多露一点。",
            new AcceptableValueRange<float>(-.02f,.05f)));
        Active = this;
        try
        {
            harmony = new Harmony(Id);
            harmony.Patch(AccessTools.Method(typeof(LookAtPenis), nameof(LookAtPenis.LateUpdate))
                ?? throw new MissingMethodException("LookAtPenis.LateUpdate"),
                prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(BeforeLook), BindingFlags.NonPublic | BindingFlags.Static)!) { priority = Priority.First },
                postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(AfterLook), BindingFlags.NonPublic | BindingFlags.Static)!) { priority = Priority.Last });
            harmony.Patch(AccessTools.Method(typeof(LookAtPenis), nameof(LookAtPenis.Release))
                ?? throw new MissingMethodException("LookAtPenis.Release"),
                prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(BeforeRelease), BindingFlags.NonPublic | BindingFlags.Static)!));
            driver = AddComponent<Driver>();
            Log.LogInfo("AC_KeepLength 0.10.1 loaded. Enabled=" + Enabled.Value + "; Mode="+Mode.Value+"; ModeSwitchKey="+ModeSwitchKey.Value+"; AllNativeShrink=" + AllNativeShrink.Value + "; PreventHalfTurn="+PreventHalfTurn.Value+"; AnimationReference="+AnimationReference.Value+"; F6 toggle; game rendering verification pending.");
        }
        catch { Unload(); throw; }
    }

    static void AfterLook(LookAtPenis __instance)
    {
        // Never let managed exceptions escape the native IL2CPP callback.
        try { Active?.Apply(__instance); }
        catch (Exception e) { Active?.Failed(e); }
    }
    static void BeforeLook(LookAtPenis __instance)
    {
        // Feed the next native update its last native value, never our override.
        // Also prevents restoring a stale compressed pose after native un-sticks.
        try
        {
            var p=Active;if(p==null)return;
            p.CaptureBefore(__instance);
            long key=__instance.Pointer.ToInt64();
            p.RememberRollInput(__instance);
            p.RestoreRoll(key);
            var data=__instance._currentData;
            if(p.writes.TryGetValue(key,out var owned)&&owned.Mode==LengthMode.NativeTipOffset)
            {
                // Offset is relative to native input, never last frame's offset
                // result, even if native LateUpdate performs no length write.
                p.Restore(key);
            }
            else if(data!=null && !data.IsTopStick && !data.IsManual)
            {
                // After unsticking, animation may already have restored the full
                // length. Its value can equal our previous override: equality
                // cannot prove ownership. Do not replay the old compressed Z.
                if(p.writes.Remove(key))p.CaptureEvent(key,"native-handoff-without-restore");
            }
            else p.Restore(key);
            p.CaptureRestored(__instance);
        }
        catch (Exception e) { Active?.Failed(e); }
    }
    static void BeforeRelease(LookAtPenis __instance)
    {
        // Release is also used for same-rig motion reconfiguration. Relinquish
        // the active rotation write but retain continuity until the next update;
        // scene invalidation, bone identity and stale-frame cleanup still apply.
        try { Active?.CaptureEvent(__instance.Pointer.ToInt64(),"component-release"); Active?.RestoreRoll(__instance.Pointer.ToInt64()); Active?.Restore(__instance.Pointer.ToInt64()); Active?.StopClip(__instance.Pointer.ToInt64()); }
        catch (Exception e) { Active?.Failed(e); }
    }
    bool SceneReady(out bool changing)
    {
        changing = false;
        float now = Time.unscaledTime;
        if (scene != null && (!scene || scene.disposed || scene.IsEnded || !scene.gameObject.activeInHierarchy))
        { scene = null; nextDiscovery = 0; }
        if (!scene && now >= nextDiscovery)
        {
            var matches = UObj.FindObjectsOfType<HScene>(true)
                .Where(s => s && !s.disposed && !s.IsEnded && s.gameObject.activeInHierarchy).Take(2).ToArray();
            scene = matches.Length == 1 ? matches[0] : null;
            nextDiscovery = now + 1;
        }
        if (!scene || scene!.disposed || scene.IsEnded || !scene.gameObject.activeInHierarchy) return false;
        changing = scene._isNowChangeAnim;
        return scene.Ready;
    }
    void Apply(LookAtPenis look)
    {
        if (!firstHook) { firstHook = true; Log.LogInfo("LookAtPenis.LateUpdate postfix reached."); }
        long key = look.Pointer.ToInt64();
        var data = look._currentData;
        var top = look._danTop;
        var basis = look._danBase;
        bool ready = SceneReady(out bool changing);
        bool matches = top && basis && top!.name == "cf_j_dan109_00"
            && basis!.name == "cf_j_dan101_00" && top.parent && top.parent.Pointer == basis.Pointer;
        Vector3 local = top ? top!.localPosition : Vector3.zero;
        float desired = 0;
        bool tipMode=Mode.Value==LengthMode.NativeTipOffset;
        bool tipEligible = TryNativeTip(look, ready, changing, matches, local, out float tipDesired);
        bool eligible = tipMode
            ? tipEligible
            : Mode.Value==LengthMode.FixedLengthClip && (AllNativeShrink.Value
            ? LengthPolicy.TryNativeShrink(Enabled.Value, ready, changing, data?.IsManual ?? true,
                matches, local.z, RestLength.Value, Multiplier.Value, out desired)
            : data != null && LengthPolicy.TryLength(Enabled.Value, ready, changing, data.IsTopStick,
                data.IsManual, matches, local.z, RestLength.Value, Multiplier.Value, out desired));
        if (tipMode) desired = tipDesired;
        float targetLength = RestLength.Value * Multiplier.Value;
        CaptureNative(look,ready,changing,matches,eligible);
        GuardRoll(look,ready,changing,matches);
        if (!tipMode && DebugLog.Value && data != null && float.IsFinite(local.z) && float.IsFinite(targetLength)
            && local.z < targetLength - LengthPolicy.ShrinkEpsilon && !eligible
            && Time.unscaledTime >= nextSkipDebug)
        {
            nextSkipDebug = Time.unscaledTime + 5;
            Log.LogInfo($"Native shrink not applied: nativeZ={local.z:F6}, target={targetLength:F6}, topStick={data.IsTopStick}, manual={data.IsManual}, ready={ready}, changing={changing}, skeletonMatches={matches}, mode={(AllNativeShrink.Value ? "all-native" : "top-stick")}");
        }
        if (!look || !look.isActiveAndEnabled || !look.gameObject.activeInHierarchy
            || data == null || !float.IsFinite(local.x) || !float.IsFinite(local.y) || !eligible)
        { Restore(key); StopClip(key); return; }

        ClipDisplay? display=null;
        bool clipping=!tipMode && ClipEnabled.Value && local.z<desired-.000001f;
        if(clipping)
        {
            if(!float.IsFinite(ClipOffset.Value) || ClipOffset.Value<-.02f || ClipOffset.Value>.05f)
                throw new InvalidOperationException("Invalid clip offset");
            if(displays.TryGetValue(key,out display) && !display.Matches(look,top!,basis!)) { StopClip(key); display=null; }
            if(display==null)
            {
                display=ClipDisplay.Find(look,top!,basis!);
                if(display==null) throw new InvalidOperationException("No unique visible o_dankon renderer bound to the known bones; keeping native pose.");
                displays.Add(key,display);
                CaptureEvent(key,"clip-created");
            }
        }
        else StopClip(key);
        // Capture the native plane BEFORE changing the endpoint. Offset uses
        // the same local axis/scale as the existing rest-length setting.
        var nativePlane=clipping ? basis!.TransformPoint(new Vector3(local.x,local.y,local.z+ClipOffset.Value)) : Vector3.zero;
        var planeNormal=clipping ? basis!.TransformVector(Vector3.forward).normalized : Vector3.forward;

        // Last native pose is retained for immediate toggle/scene-exit recovery.
        // If native code left our Z untouched, keep the earlier native value.
        if (!writes.TryGetValue(key, out var state) || state.Top.Pointer != top!.Pointer)
        {
            Restore(key);
            state = new WriteState(top!, local.z);
            writes[key] = state;
        }
        else if (!LengthPolicy.StillOwned(local.z, state.WrittenZ)) state.NativeZ = local.z;
        state.Frame = Time.frameCount;
        state.WrittenZ = desired;
        state.Mode=Mode.Value;
        local.z = desired;
        top!.localPosition = local;
        display?.Apply(nativePlane,planeNormal);
        CaptureWrite(look,nativePlane,planeNormal);
        if (!firstWrite || (DebugLog.Value && Time.unscaledTime >= nextDebug))
        {
            firstWrite = true; nextDebug = Time.unscaledTime + 5;
            Log.LogInfo($"KeepLength applied: mode={Mode.Value}, nativeZ={state.NativeZ:F6}, requestedZ={desired:F6}, readbackZ={top.localPosition.z:F6}, target={data.LookAtNullName}, topStick={data.IsTopStick}, manual={data.IsManual}, clipping={clipping}, triangles={display?.TriangleCount ?? -1}, clipOffset={ClipOffset.Value:F4}, tipOffset={TipOffset.Value:F4}");
        }
    }
    void Restore(long key)
    {
        if (!writes.Remove(key, out var state)) return;
        if (!state.Top) return;
        var local = state.Top.localPosition;
        // Do not overwrite a newer native pose or another plugin's update.
        if (!LengthPolicy.StillOwned(local.z, state.WrittenZ)) return;
        local.z = state.NativeZ;
        state.Top.localPosition = local;
    }
    internal void Tick()
    {
        if (ToggleKey.Value != KeyCode.None && Input.GetKeyDown(ToggleKey.Value))
        {
            Enabled.Value = !Enabled.Value;
            Log.LogMessage("AC KeepLength: " + (Enabled.Value ? "ON / 保长开启" : "OFF / 保长关闭"));
        }
        if (ModeSwitchKey.Value != KeyCode.None && Input.GetKeyDown(ModeSwitchKey.Value))
        {
            Mode.Value = Mode.Value == LengthMode.FixedLengthClip
                ? LengthMode.NativeTipOffset : LengthMode.FixedLengthClip;
            Log.LogMessage("AC KeepLength: Mode / 模式 = " + Mode.Value);
        }
        bool ready = SceneReady(out _);
        TickNativeTip();
        foreach(var key in rollInputs.Where(p=>!Enabled.Value||!PreventHalfTurn.Value||!ready||Time.frameCount-p.Value.Frame>2).Select(p=>p.Key).ToArray())rollInputs.Remove(key);
        TickRolls(ready);
        foreach (var key in writes.Where(p => !Enabled.Value || !ready || p.Value.Mode!=Mode.Value || Time.frameCount - p.Value.Frame > 2)
                     .Select(p => p.Key).ToArray()) Restore(key);
        foreach(var key in displays.Where(p=>!Enabled.Value || Mode.Value!=LengthMode.FixedLengthClip || !ClipEnabled.Value || !ready || !p.Value.Valid || Time.frameCount-p.Value.Frame>2)
                    .Select(p=>p.Key).ToArray()) StopClip(key);
    }
    void StopClip(long key) { if(displays.Remove(key,out var display)){CaptureEvent(key,"clip-released");display.Dispose();} }
    void Failed(Exception e)
    {
        tipRigs.Clear();
        try { foreach (long key in writes.Keys.ToArray()) Restore(key); } catch { writes.Clear(); }
        foreach(long key in displays.Keys.ToArray()) { try { StopClip(key); } catch { displays.Remove(key); } }
        if (Time.unscaledTime < nextWarning) return;
        nextWarning = Time.unscaledTime + 5;
        Log.LogWarning("KeepLength skipped/restored after error: " + e);
    }
    internal void LogDriverError(Exception e) => Failed(e);
    public override bool Unload()
    {
        foreach(var key in rolls.Keys.ToArray())RestoreRoll(key,true);
        rollInputs.Clear();
        disabledRollSamples.Clear();
        try { foreach (long key in writes.Keys.ToArray()) Restore(key); }
        finally
        {
            foreach(long key in displays.Keys.ToArray()) { try { StopClip(key); } catch { displays.Remove(key); } }
            writes.Clear(); tipRigs.Clear(); tipAction = null; scene = null;
            harmony?.UnpatchSelf(); harmony = null;
            Active = null;
            if (driver) UObj.Destroy(driver); driver = null;
            try{CloseTrace();}catch(Exception e){Log.LogWarning("Trace close failed: "+e.Message);}
        }
        return true;
    }
    sealed class WriteState
    {
        public readonly Transform Top;
        public float NativeZ, WrittenZ;
        public LengthMode Mode;
        public int Frame;
        public WriteState(Transform top, float nativeZ) { Top = top; NativeZ = nativeZ; }
    }
}

public sealed class Driver : MonoBehaviour
{
    public Driver(IntPtr pointer) : base(pointer) { }
    public void Start()
    {
        try{StartCoroutine(ObserveEnd().WrapToIl2Cpp());}
        catch(Exception e){Plugin.Active?.Log.LogWarning("End-of-frame diagnostics unavailable: "+e.Message);}
    }
    System.Collections.IEnumerator ObserveEnd()
    {
        var end=new WaitForEndOfFrame();
        while(Plugin.Active!=null){yield return end;Plugin.Active?.ObserveFrameEnd();}
    }
    public void Update()
    {
        try { Plugin.Active?.Tick(); }
        catch (Exception e) { Plugin.Active?.LogDriverError(e); }
    }
}
