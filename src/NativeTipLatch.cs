namespace KeepLength;

// Per rig, for one selected posture. Native length crossing the rest length
// must not turn the effect off again during that same posture.
internal sealed class NativeTipLatch
{
    public const float FadeSeconds = .15f;
    public bool HasShrunk { get; private set; }
    public float Weight { get; private set; }
    bool timing;
    float lastTime;

    public void Observe(float nativeZ)
    {
        if (float.IsFinite(nativeZ) && nativeZ >= 0
            && nativeZ < LengthPolicy.DefaultLength - LengthPolicy.ShrinkEpsilon)
            HasShrunk = true;
    }

    public float Advance(float time)
    {
        if (!HasShrunk || !float.IsFinite(time)) return 0;
        if (timing) Weight = MathF.Min(1, Weight + Math.Clamp(time - lastTime, 0, .05f) / FadeSeconds);
        timing = true;
        lastTime = time;
        return Weight * Weight * (3 - 2 * Weight);
    }

    // Disabling an effect restarts its fade, not the posture's observation.
    public void Pause() { Weight = 0; timing = false; }
}
