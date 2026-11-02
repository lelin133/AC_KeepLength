using UnityEngine;
using NQ=System.Numerics.Quaternion;

namespace KeepLength;

public sealed partial class Plugin
{
    internal BepInEx.Configuration.ConfigEntry<bool> PreventHalfTurn=null!;
    internal BepInEx.Configuration.ConfigEntry<bool> AnimationReference=null!;
    internal BepInEx.Configuration.ConfigEntry<bool> StableAxialReference=null!;
    internal BepInEx.Configuration.ConfigEntry<bool> BindPoseFallback=null!;
    readonly Dictionary<long,RollState> rolls=new();
    readonly Dictionary<long,(long Bone,long Parent,int Frame,NQ Rotation,bool Valid)> rollInputs=new();
    readonly Dictionary<long,(long Bone,long Parent,int Frame,TipAction Scope,NQ Native)> disabledRollSamples=new();
    void RememberRollInput(H.LookAtPenis look)
    {
        long key=look.Pointer.ToInt64();var bone=look._danBase;
        // Observe animation inputs while disabled as well; observing must not
        // require that the correction has already been enabled once.
        if(!bone||!bone!.parent){rollInputs.Remove(key);return;}
        var q=N(bone.localRotation);
        bool valid=RollPolicy.Normalize(q,out q);
        if(rolls.TryGetValue(key,out var owned)&&owned.Owns&&RollPolicy.SameRotation(q,owned.Written))valid=false;
        rollInputs[key]=(bone.Pointer.ToInt64(),bone.parent.Pointer.ToInt64(),Time.frameCount,q,valid);
    }
    sealed class RollState
    {
        public Transform Bone=null!;
        public long Parent,Target;
        public string? TargetName;
        public NQ Previous,Native,Written;
        public float Time;
        public int Frame;
        public bool Flipped,Owns,AnimationMode,StableMode,Suspended;
        public TipAction? Scope;
        public NQ Output;
    }
    static NQ N(Quaternion q)=>new(q.x,q.y,q.z,q.w);
    static Quaternion U(NQ q)=>new(q.X,q.Y,q.Z,q.W);
    void RestoreRoll(long key,bool forget=false)
    {
        if(!rolls.TryGetValue(key,out var s))return;
        if(s.Owns&&s.Bone&&RollPolicy.SameRotation(N(s.Bone.localRotation),s.Written))
            s.Bone.localRotation=U(s.Native);
        s.Owns=false;
        if(forget)rolls.Remove(key);
    }
    TipAction? CurrentRollScope()
    {
        if(!scene||scene!.disposed||scene.IsEnded)return null;
        var info=scene.NowAnimationInfo;
        return info==null ? null:new TipAction(scene.Pointer.ToInt64(),info.Mode,info.Kind,info.ID);
    }
    void SuspendRoll(long key,TipAction? scope)
    {
        if(!rolls.TryGetValue(key,out var s))return;
        bool valid=scope.HasValue&&s.Scope==scope&&s.Bone&&s.Bone.parent&&s.Bone.parent.Pointer.ToInt64()==s.Parent;
        if(valid)s.Suspended=true;
        RestoreRoll(key,!valid);
    }
    void ObserveDisabledRoll(H.LookAtPenis look,TipAction? scope)
    {
        long key=look.Pointer.ToInt64();var bone=look._danBase!;
        SuspendRoll(key,scope);
        if(!scope.HasValue||rolls.ContainsKey(key)||!bone.parent
            ||!rollInputs.TryGetValue(key,out var input)||!input.Valid||input.Frame!=Time.frameCount
            ||input.Bone!=bone.Pointer.ToInt64()||input.Parent!=bone.parent.Pointer.ToInt64()
            ||!RollPolicy.Normalize(N(bone.localRotation),out var native))return;
        // Identical input/output is not evidence of an animation baseline:
        // an already flipped idle can simply be retained by native code.
        NQ output;bool flipped=false;
        if(RollPolicy.SameRotation(input.Rotation,native))
        {
            bool hasPrevious=disabledRollSamples.TryGetValue(key,out var previous)
                &&previous.Scope==scope.Value&&previous.Bone==input.Bone&&previous.Parent==input.Parent
                &&Time.frameCount-previous.Frame<=2&&Time.frameCount>previous.Frame;
            disabledRollSamples[key]=(input.Bone,input.Parent,Time.frameCount,scope.Value,native);
            if(!hasPrevious||!RollPolicy.HalfTurn(previous.Native,native,out _,out _)
                ||!RollPolicy.TransportTwist(previous.Native,native,out output))
            {
                if(!TryBindBranch(bone,native,out output))return;
            }
            flipped=true;
        }
        else if(AnimationReference.Value)
        {
            if(!RollPolicy.AlignTwist(input.Rotation,native,out output))return;
        }
        else
        {
            flipped=RollPolicy.HalfTurn(input.Rotation,native,out _,out _);
            output=RollPolicy.Correct(native,flipped);
        }
        rolls[key]=new RollState{Bone=bone,Parent=input.Parent,Scope=scope,
            Native=native,Previous=native,Written=native,Output=output,Flipped=flipped,
            Frame=Time.frameCount,Time=Time.unscaledTime,Suspended=true,Owns=false,
            AnimationMode=AnimationReference.Value,StableMode=StableAxialReference.Value};
        disabledRollSamples.Remove(key);
        CaptureEvent(key,"roll-disabled-reference-observed");
        Log.LogInfo("Rotation reference observed while disabled; no pose written. Action="+scope.Value.Id);
    }
    bool TryBindBranch(Transform bone,NQ native,out NQ output)
    {
        output=native;
        // Verified source cf_j_dan101_00 has identity local rest rotation.
        // Select only an equivalent half-turn branch; never replace its aim.
        if(!BindPoseFallback.Value||!AnimationReference.Value||!StableAxialReference.Value
            ||bone.name!="cf_j_dan101_00"||!bone.parent||bone.parent!.name!="cf_j_dan100_00"
            ||!RollPolicy.HalfTurn(NQ.Identity,native,out _,out _))return false;
        output=RollPolicy.Correct(native,true);return true;
    }
    void TickRolls(bool ready)
    {
        var scope=CurrentRollScope();
        foreach(long key in disabledRollSamples.Where(p=>!ready||!scope.HasValue||p.Value.Scope!=scope.Value
            ||Time.frameCount-p.Value.Frame>2).Select(p=>p.Key).ToArray())disabledRollSamples.Remove(key);
        foreach(long key in rolls.Keys.ToArray())
        {
            var s=rolls[key];
            if(!ready||!s.Bone||!s.Bone.parent||s.Bone.parent.Pointer.ToInt64()!=s.Parent)
            {RestoreRoll(key,true);continue;}
            if(!Enabled.Value||!PreventHalfTurn.Value){SuspendRoll(key,scope);continue;}
            if(s.Suspended)
            {
                if(!scope.HasValue||s.Scope!=scope)RestoreRoll(key,true);
            }
            else if(Time.frameCount-s.Frame>2)RestoreRoll(key,true);
        }
    }
    void GuardRoll(H.LookAtPenis look,bool ready,bool changing,bool skeleton)
    {
        long key=look.Pointer.ToInt64();
        try
        {
            var bone=look._danBase;var data=look._currentData;
            if(!ready||!skeleton
                ||!look.isActiveAndEnabled||!look.gameObject.activeInHierarchy||!bone||!bone!.parent)
            {RestoreRoll(key,true);return;}
            var scope=CurrentRollScope();
            if(!Enabled.Value||!PreventHalfTurn.Value)
            {
                if(rolls.TryGetValue(key,out var paused)&&paused.Bone.Pointer!=bone.Pointer)RestoreRoll(key,true);
                else ObserveDisabledRoll(look,scope);
                return;
            }
            if(!RollPolicy.Normalize(N(bone.localRotation),out var native)){RestoreRoll(key,true);return;}
            long parent=bone.parent.Pointer.ToInt64(),target=look._lookAtNullT ? look._lookAtNullT!.Pointer.ToInt64():0;
            string? targetName=data?.LookAtNullName;
            // Roll continuity belongs to the physical bone. Aim targets can be
            // recreated or rebound during insertion without replacing that bone.
            bool exists=rolls.TryGetValue(key,out var s);
            string? resetReason=!exists ? "first-observation" : s!.Bone.Pointer!=bone.Pointer ? "source-bone-changed"
                : s.Parent!=parent ? "parent-bone-changed" : s.AnimationMode!=AnimationReference.Value||s.StableMode!=StableAxialReference.Value?"rotation-mode-changed"
                :s.Suspended&&(!scope.HasValue||s.Scope!=scope)?"suspended-action-changed":null;
            CaptureRollIdentity(look,bone,parent,target,resetReason);
            if(resetReason!=null)
            {
                s=new RollState{Bone=bone,Parent=parent,Target=target,TargetName=targetName,
                    Native=native,Previous=native,Time=Time.unscaledTime,Frame=Time.frameCount,AnimationMode=AnimationReference.Value,StableMode=StableAxialReference.Value,Scope=scope};
                rolls[key]=s;
                s.Output=native;
                CaptureEvent(key,"roll-reference-reset");
                if(ApplyAnimationReference(look,s,native,parent,true))return;
                if(rollInputs.TryGetValue(key,out var input)&&input.Valid&&input.Frame==Time.frameCount
                    &&input.Bone==bone.Pointer.ToInt64()&&input.Parent==parent
                    &&RollPolicy.HalfTurn(input.Rotation,native,out var inputSwing,out var inputTwist))
                {
                    s.Flipped=true;s.Written=RollPolicy.Correct(native,true);
                    bone.localRotation=U(s.Written);s.Owns=true;
                    s.Output=s.Written;
                    CaptureEvent(key,"roll-initial-animation-reference-correction");
                    CaptureRoll(look,inputSwing,inputTwist,true);
                }
                return;
            }
            bool resumed=s!.Suspended;
            s.Suspended=false;s.Scope=scope;
            if(Time.frameCount==s.Frame&&!resumed)return;
            if(resumed)
            {
                s.Previous=native;
                if(!AnimationReference.Value&&RollPolicy.TransportTwist(s.Output,native,out var resumedOutput))
                    s.Flipped=MathF.Abs(NQ.Dot(resumedOutput,RollPolicy.Correct(native,true)))>MathF.Abs(NQ.Dot(resumedOutput,native));
                CaptureEvent(key,"roll-resumed-stable-reference");
            }
            if(ApplyAnimationReference(look,s,native,parent,false,resumed))return;
            bool half=RollPolicy.HalfTurn(s.Previous,native,out var swing,out var twist);
            bool gap=Time.frameCount-s.Frame>2||Time.unscaledTime-s.Time>.15f||Time.unscaledTime<s.Time;
            // A large change in pointing direction is not evidence that an
            // existing half-turn correction should be removed. Clearing it
            // here caused a delayed 180-degree pop during withdrawal.
            if(gap){CaptureEvent(key,"roll-time-gap-preserve-branch");}
            else if(swing>170){CaptureEvent(key,"roll-large-aim-change-preserve-branch");}
            else if(half)
            {
                s.Flipped=!s.Flipped;
                CaptureEvent(key,"roll-half-turn-branch-switched");
                Log.LogInfo($"Half-turn guard: frame={Time.frameCount}, aimDelta={swing:F2}, rollDelta={twist:F2}, correction={s.Flipped}, manual={data?.IsManual}, changing={changing}");
            }
            s.Previous=s.Native=native;s.Time=Time.unscaledTime;s.Frame=Time.frameCount;s.Owns=false;
            if(target!=0)s.Target=target;if(targetName!=null)s.TargetName=targetName;
            if(s.Flipped)
            {
                s.Written=RollPolicy.Correct(native,true);bone.localRotation=U(s.Written);s.Owns=true;
            }
            s.Output=s.Flipped?s.Written:native;
            CaptureRoll(look,swing,twist,s.Flipped);
        }
        catch(Exception e)
        {
            try{RestoreRoll(key,true);}catch{rolls.Remove(key);}
            Log.LogWarning("Half-turn guard skipped: "+e.Message);
        }
    }
    bool ApplyAnimationReference(H.LookAtPenis look,RollState s,NQ native,long parent,bool initial,bool resumed=false)
    {
        if(!AnimationReference.Value)return false;
        long key=look.Pointer.ToInt64();
        bool fresh=rollInputs.TryGetValue(key,out var input)&&input.Valid&&input.Frame==Time.frameCount
            &&input.Bone==s.Bone.Pointer.ToInt64()&&input.Parent==parent;
        bool stable=(StableAxialReference.Value||resumed)&&!initial;
        if(stable)fresh=false;
        NQ reference=fresh?input.Rotation:s.Output;
        bool bindBranch=initial&&(!fresh||RollPolicy.SameRotation(input.Rotation,native))
            &&TryBindBranch(s.Bone,native,out var _);
        if(bindBranch)
        {
            reference=RollPolicy.Correct(native,true);fresh=false;
            CaptureEvent(key,"roll-initial-bind-branch");
            Log.LogInfo("Initial half-turn branch selected from verified local rest axes. Action="+s.Scope?.Id);
        }
        if(!fresh&&initial&&!bindBranch)return false;
        NQ output;
        if(stable)
        {
            if(!RollPolicy.TransportTwist(reference,native,out output))return false;
        }
        else if(!RollPolicy.AlignTwist(reference,native,out output))return false;
        // Use the nearest axial branch, then bound reference-only roll speed.
        // This also handles 140-149 degree reference jumps, which were below
        // the legacy detector threshold. Native pointing is never rate-limited.
        if(!initial&&!stable)
        {
            float dt=Math.Clamp(Time.unscaledTime-s.Time,0,.05f);
            if(RollPolicy.ContinuousTwist(s.Output,output,native,180*dt,out var continuous))output=continuous;
            else if(RollPolicy.HalfTurn(s.Output,output,out _,out _))output=RollPolicy.Correct(output,true);
        }
        s.Native=s.Previous=native;s.Written=s.Output=output;s.Time=Time.unscaledTime;s.Frame=Time.frameCount;
        s.Owns=!RollPolicy.SameRotation(native,output);
        if(s.Owns)s.Bone.localRotation=U(output);
        CaptureAnimationRoll(look,fresh,reference,output,s.Owns);
        return true;
    }
}
