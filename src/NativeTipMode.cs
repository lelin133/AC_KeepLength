using H;
using UnityEngine;

namespace KeepLength;

public sealed partial class Plugin
{
    readonly record struct TipAction(long Scene, int Mode, int Kind, int Id);
    TipAction? tipAction;
    bool tipActionKnown;
    readonly Dictionary<long, TipRig> tipRigs = new();

    sealed class TipRig(Transform top, Transform basis)
    {
        public readonly Transform Top = top, Basis = basis;
        public readonly NativeTipLatch Latch = new();
        public float AppliedOffset;
    }

    bool RefreshTipAction()
    {
        tipActionKnown = false;
        if (!scene || scene!.disposed || scene.IsEnded || !scene.gameObject.activeInHierarchy)
        {
            tipAction = null;
            tipRigs.Clear();
            return false;
        }
        long owner = scene.Pointer.ToInt64();
        if (tipAction is { } previous && previous.Scene != owner)
        {
            tipAction = null;
            tipRigs.Clear();
        }
        // Use the current selected posture, not its per-loop animator state,
        // target name, MotionLookAtData wrapper, or Release calls.
        var info = scene.NowAnimationInfo;
        if (info == null || info.ID < 0) return false;
        var current = new TipAction(owner, info.Mode, info.Kind, info.ID);
        if (tipAction != current)
        {
            tipRigs.Clear();
            tipAction = current;
        }
        tipActionKnown = true;
        return true;
    }

    bool TryNativeTip(LookAtPenis look, bool ready, bool changing, bool matches,
        Vector3 local, out float desired)
    {
        desired = 0;
        bool actionKnown = RefreshTipAction();
        if (!actionKnown || !ready || changing || !matches || look._currentData == null
            || !look.isActiveAndEnabled || !look.gameObject.activeInHierarchy
            || !float.IsFinite(local.x) || !float.IsFinite(local.y)
            || !float.IsFinite(local.z) || local.z < 0 || local.z > .5f) return false;

        var top = look._danTop!;
        var basis = look._danBase!;
        long key = top.Pointer.ToInt64();
        if (!tipRigs.TryGetValue(key, out var rig) || rig.Basis.Pointer != basis.Pointer)
            tipRigs[key] = rig = new TipRig(top, basis);
        rig.Latch.Observe(local.z);
        rig.AppliedOffset = 0;
        bool eligible = Mode.Value == LengthMode.NativeTipOffset
            && LengthPolicy.TryTipOffset(Enabled.Value, ready, changing, matches,
                rig.Latch.HasShrunk, local.z, TipOffset.Value, out _);
        if (!eligible) { rig.Latch.Pause(); return false; }

        // Validate the requested full offset first: an invalid result must not
        // be partially applied merely because its fade is still small.
        rig.AppliedOffset = TipOffset.Value * rig.Latch.Advance(Time.unscaledTime);
        if (MathF.Abs(rig.AppliedOffset) < LengthPolicy.ShrinkEpsilon) return false;
        desired = local.z + rig.AppliedOffset;
        return true;
    }

    void TickNativeTip()
    {
        var previous = tipAction;
        bool known = RefreshTipAction();
        if (!known || previous != tipAction)
            foreach (long key in writes.Where(p => p.Value.Mode == LengthMode.NativeTipOffset)
                .Select(p => p.Key).ToArray()) Restore(key);
        foreach (long key in tipRigs.Where(p => !p.Value.Top || !p.Value.Basis
            || !p.Value.Top.parent || p.Value.Top.parent!.Pointer != p.Value.Basis.Pointer)
            .Select(p => p.Key).ToArray()) tipRigs.Remove(key);
        if (!Enabled.Value || Mode.Value != LengthMode.NativeTipOffset)
            foreach (var rig in tipRigs.Values) { rig.Latch.Pause(); rig.AppliedOffset = 0; }
    }
}
