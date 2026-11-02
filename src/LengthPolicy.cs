namespace KeepLength;

// Engine-independent rules also exercised against the recorded AC samples.
internal static class LengthPolicy
{
    public const float DefaultLength = .1898f;
    public const float ShrinkEpsilon = .000001f;
    public static bool TryTipOffset(bool enabled,bool ready,bool changing,bool skeleton,bool actionHasShrunk,
        float nativeZ,float offset,out float length)
    {
        length=0;
        if(!enabled||!ready||changing||!skeleton||!float.IsFinite(nativeZ)||!float.IsFinite(offset)
            ||nativeZ<0||nativeZ>.5f||offset<-.05f||offset>.05f||MathF.Abs(offset)<ShrinkEpsilon)return false;
        if(!actionHasShrunk)return false;
        length=nativeZ+offset;
        // Do not invert the tip through its base or accept unreasonable output.
        return length>ShrinkEpsilon&&length<=.5f;
    }
    public static bool TryLength(bool enabled, bool sceneReady, bool changing,
        bool topStick, bool manual, bool skeletonMatches, float nativeZ,
        float rest, float multiplier, out float length)
    {
        length = 0;
        if (!enabled || !sceneReady || changing || !topStick || manual || !skeletonMatches
            || !float.IsFinite(nativeZ) || !float.IsFinite(rest) || !float.IsFinite(multiplier)
            || rest < .01f || rest > .5f || multiplier < .25f || multiplier > 2) return false;
        length = rest * multiplier;
        return true;
    }
    public static bool TryNativeShrink(bool enabled, bool sceneReady, bool changing,
        bool manual, bool skeletonMatches, float nativeZ,
        float rest, float multiplier, out float length)
    {
        length = 0;
        // IsManual is also used by native H animation paths. In all-shrink mode,
        // actual shortening is the trigger regardless of that flag.
        if (!enabled || !sceneReady || changing || !skeletonMatches
            || !float.IsFinite(nativeZ) || !float.IsFinite(rest) || !float.IsFinite(multiplier)
            || rest < .01f || rest > .5f || multiplier < .25f || multiplier > 2) return false;
        length = rest * multiplier;
        return nativeZ < length - ShrinkEpsilon;
    }
    public static bool StillOwned(float current, float written) =>
        float.IsFinite(current) && MathF.Abs(current - written) < .000001f;
}
