using System.Numerics;

namespace KeepLength;

// Measure physical rotation, not quaternion sign changes. All inputs are in
// the same parent space so whole-character motion is not classified as roll.
internal static class RollPolicy
{
    public static bool Normalize(Quaternion q,out Quaternion result)
    {
        result=Quaternion.Identity;
        float n=q.LengthSquared();
        if(!float.IsFinite(n)||n<1e-12f)return false;
        result=Quaternion.Normalize(q);return true;
    }
    public static bool HalfTurn(Quaternion previous,Quaternion current,out float swing,out float twist)
    {
        swing=twist=0;
        if(!Normalize(previous,out previous)||!Normalize(current,out current))return false;
        var a=Vector3.Transform(Vector3.UnitZ,previous);var b=Vector3.Transform(Vector3.UnitZ,current);
        swing=MathF.Acos(Math.Clamp(Vector3.Dot(a,b),-1,1))*180/MathF.PI;
        var d=Quaternion.Conjugate(previous)*current;
        if(d.Z*d.Z+d.W*d.W<1e-12f)return false;
        twist=2*MathF.Atan2(d.Z,d.W)*180/MathF.PI;
        twist=(twist%360+540)%360-180;
        // A visible half turn can coincide with a substantial insertion swing.
        // Keep excluding near-opposite forward axes, where twist is ambiguous.
        return swing<=170 && MathF.Abs(twist)>=150;
    }
    public static Quaternion Correct(Quaternion native,bool flipped)=>flipped
        ? Quaternion.Normalize(native*new Quaternion(0,0,1,0)):native;
    public static bool SameRotation(Quaternion a,Quaternion b)=>Normalize(a,out a)&&Normalize(b,out b)
        && MathF.Abs(Quaternion.Dot(a,b))>=.999999f;
    // Keep the solver's forward vector; remove only the twist it added around
    // that vector relative to an animation/transported reference.
    public static bool AlignTwist(Quaternion reference,Quaternion native,out Quaternion output)
    {
        output=native;
        if(!Normalize(reference,out reference)||!Normalize(native,out native))return false;
        var d=Quaternion.Conjugate(reference)*native;
        float n=d.Z*d.Z+d.W*d.W;
        if(n<1e-8f)return false; // Antiparallel forward axes have no unique twist.
        var twist=new Quaternion(0,0,d.Z/MathF.Sqrt(n),d.W/MathF.Sqrt(n));
        output=Quaternion.Normalize(native*Quaternion.Conjugate(twist));return true;
    }
    // Compare equivalent axial branches after transporting the last output to
    // the new forward. Animation references can jump by less than 150 degrees,
    // so the old half-turn detector is not a continuity rule for this path.
    public static bool ContinuousTwist(Quaternion previous,Quaternion candidate,Quaternion native,
        float maxStepDegrees,out Quaternion output)
    {
        output=candidate;
        if(!float.IsFinite(maxStepDegrees)||maxStepDegrees<0
            ||!AlignTwist(previous,native,out var transported)||!Normalize(candidate,out candidate))return false;
        var d=Quaternion.Conjugate(transported)*candidate;
        float angle=2*MathF.Atan2(d.Z,d.W)*180/MathF.PI;
        angle=(angle%180+270)%180-90;
        angle=Math.Clamp(angle,-maxStepDegrees,maxStepDegrees);
        output=Quaternion.Normalize(transported*Quaternion.CreateFromAxisAngle(Vector3.UnitZ,angle*MathF.PI/180));
        return true;
    }
    public static bool TransportTwist(Quaternion reference,Quaternion native,out Quaternion output)
    {
        if(AlignTwist(reference,native,out output))return true;
        if(!Normalize(reference,out reference)||!Normalize(native,out native))return false;
        // Exact opposite pointing directions have no unique shortest swing.
        // Preserve the reference up axis to choose a deterministic half swing.
        var reversed=Quaternion.Normalize(reference*new Quaternion(0,1,0,0));
        return AlignTwist(reversed,native,out output);
    }
}
