using BepInEx;
using UnityEngine;

namespace KeepLength;

public sealed partial class Plugin
{
    internal BepInEx.Configuration.ConfigEntry<bool> FrameTrace = null!;
    TraceJournal? journal;
    bool traceArmed, traceFault;
    internal string? TracePath;
    readonly Dictionary<long, TraceFrame> traceFrames = new();

    sealed class TraceFrame
    {
        public LookAtRef Ref;
        public Dictionary<string,object?> Fields = new();
        public List<string> Events = new();
        public TraceFrame(H.LookAtPenis look){Ref=new(look);}
    }
    // Retain only the component for the current frame, not character data.
    readonly record struct LookAtRef(H.LookAtPenis Look);

    void TraceSafe(Action action)
    {
        try
        {
            if(!FrameTrace.Value)
            {
                if(traceArmed){CloseTrace();traceArmed=false;traceFault=false;}
                return;
            }
            if(traceFault)return;
            if(!traceArmed)
            {
                traceArmed=true;
                TracePath=Path.Combine(Paths.GameRootPath,"BepInEx","KeepLengthTraces",
                    $"withdrawal-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.jsonl");
                journal=new TraceJournal(TracePath);
                journal.Write(new {kind="session",utc=DateTime.UtcNow,version="0.10.1",maxRecords=20000,maxBytes=32*1024*1024,
                    boundary="end-of-frame is WaitForEndOfFrame; next-prefix may include next-frame animation; equal values do not prove absence of a native write"});
                Log.LogInfo("KeepLength frame trace: "+TracePath);
            }
            if(journal is {Closed:false})action();
            else traceFrames.Clear();
        }
        catch(Exception e)
        {
            traceFault=true;
            try{CloseTrace();}catch{}
            Log.LogWarning("KeepLength diagnostics stopped; pose logic unchanged: "+e.Message);
        }
    }
    static float? Z(H.LookAtPenis look)=>look && look._danTop ? look._danTop!.localPosition.z : null;
    static float[]? Rotation(Transform? bone)
    {
        if(!bone)return null;
        var q=bone!.rotation;return new[]{q.x,q.y,q.z,q.w};
    }
    void Emit(TraceFrame frame,string completion)
    {
        frame.Fields["events"]=frame.Events;
        frame.Fields["completion"]=completion;
        journal!.Write(frame.Fields);
    }
    void CaptureBefore(H.LookAtPenis look)=>TraceSafe(()=>
    {
        long key=look.Pointer.ToInt64();
        if(traceFrames.Remove(key,out var previous))
        {
            previous.Fields["nextPrefixZ"]=Z(look);
            Emit(previous,"next-prefix-without-end-of-frame");
        }
        var sample=new TraceFrame(look);
        sample.Fields["kind"]="frame";sample.Fields["utc"]=DateTime.UtcNow;
        sample.Fields["frame"]=Time.frameCount;sample.Fields["time"]=Time.unscaledTime;sample.Fields["component"]=key.ToString("x");
        sample.Fields["beforeRestoreZ"]=Z(look);
        sample.Fields["savedNativeZ"]=writes.TryGetValue(key,out var write)?write.NativeZ:null;
        sample.Fields["savedWrittenZ"]=write?.WrittenZ;
        sample.Fields["rest"]=RestLength.Value;sample.Fields["multiplier"]=Multiplier.Value;
        sample.Fields["offset"]=ClipOffset.Value;sample.Fields["enabled"]=Enabled.Value;
        sample.Fields["clipEnabled"]=ClipEnabled.Value;sample.Fields["allNativeShrink"]=AllNativeShrink.Value;
        sample.Fields["lengthMode"]=Mode.Value.ToString();sample.Fields["tipOffset"]=TipOffset.Value;
        sample.Fields["clipBefore"]=displays.ContainsKey(key);
        sample.Fields["beforeBaseRotation"]=Rotation(look._danBase);
        sample.Fields["beforeBaseLocalRotation"]=look._danBase ? LocalRotation(look._danBase!):null;
        traceFrames[key]=sample;
    });
    void CaptureRestored(H.LookAtPenis look)=>TraceSafe(()=>
    {
        if(traceFrames.TryGetValue(look.Pointer.ToInt64(),out var s))s.Fields["afterRestoreZ"]=Z(look);
    });
    void CaptureNative(H.LookAtPenis look,bool ready,bool changing,bool matches,bool eligible)=>TraceSafe(()=>
    {
        long key=look.Pointer.ToInt64();
        if(!traceFrames.TryGetValue(key,out var s))return;
        s.Fields["afterNativeZ"]=Z(look);s.Fields["nativeFrame"]=Time.frameCount;s.Fields["ready"]=ready;s.Fields["changing"]=changing;
        s.Fields["skeletonMatches"]=matches;s.Fields["eligible"]=eligible;
        s.Fields["topStick"]=look._currentData?.IsTopStick;s.Fields["manual"]=look._currentData?.IsManual;
        s.Fields["target"]=look._currentData?.LookAtNullName;
        s.Fields["actionId"]=tipAction?.Id;s.Fields["actionMode"]=tipAction?.Mode;s.Fields["actionKind"]=tipAction?.Kind;
        s.Fields["actionKnown"]=tipActionKnown;
        TipRig? rig=null;
        if(look._danTop)tipRigs.TryGetValue(look._danTop!.Pointer.ToInt64(),out rig);
        s.Fields["tipActionHasShrunk"]=rig?.Latch.HasShrunk;
        s.Fields["tipBlendWeight"]=rig?.Latch.Weight;
        s.Fields["tipAppliedOffset"]=eligible&&Mode.Value==LengthMode.NativeTipOffset ? rig?.AppliedOffset:0;
        s.Fields["baseRotation"]=Rotation(look._danBase);s.Fields["topRotation"]=Rotation(look._danTop);
        s.Fields["nativeBaseLocalRotation"]=look._danBase ? LocalRotation(look._danBase!):null;
        s.Fields["parentRotation"]=look._danBase ? Rotation(look._danBase!.parent):null;
        var aim=look._penisInfo;
        s.Fields["aimUpRotation"]=aim==null ? null:Rotation(aim.TrfUpAxis);
        s.Fields["aimSourceRotation"]=aim==null ? null:Rotation(aim.TrfLookAt);
    });
    void CaptureWrite(H.LookAtPenis look,Vector3 point,Vector3 normal)=>TraceSafe(()=>
    {
        long key=look.Pointer.ToInt64();
        if(!traceFrames.TryGetValue(key,out var s))return;
        s.Fields["afterWriteZ"]=Z(look);
        s.Fields["planePoint"]=new[]{point.x,point.y,point.z};s.Fields["planeNormal"]=new[]{normal.x,normal.y,normal.z};
        s.Fields["triangles"]=displays.TryGetValue(key,out var d)?d.TriangleCount:-1;
    });
    static float[] LocalRotation(Transform bone){var q=bone.localRotation;return new[]{q.x,q.y,q.z,q.w};}
    void CaptureAnimationRoll(H.LookAtPenis look,bool fresh,System.Numerics.Quaternion reference,System.Numerics.Quaternion output,bool corrected)=>TraceSafe(()=>
    {
        if(!traceFrames.TryGetValue(look.Pointer.ToInt64(),out var s))return;
        s.Fields["rollReferenceSource"]=fresh?"animation-input":"transported-output";
        s.Fields["stableAxialReference"]=StableAxialReference.Value;
        s.Fields["rollReferenceRotation"]=new[]{reference.X,reference.Y,reference.Z,reference.W};
        s.Fields["rollOutputLocalRotation"]=new[]{output.X,output.Y,output.Z,output.W};
        s.Fields["rollCorrected"]=corrected;s.Fields["guardedBaseRotation"]=Rotation(look._danBase);
    });
    void CaptureRollIdentity(H.LookAtPenis look,Transform bone,long parent,long target,string? reason)=>TraceSafe(()=>
    {
        if(!traceFrames.TryGetValue(look.Pointer.ToInt64(),out var s))return;
        s.Fields["rollBone"]=bone.Pointer.ToInt64().ToString("x");s.Fields["rollParent"]=parent.ToString("x");
        s.Fields["rollTarget"]=target.ToString("x");s.Fields["rollResetReason"]=reason;
    });
    void CaptureRoll(H.LookAtPenis look,float swing,float twist,bool corrected)=>TraceSafe(()=>
    {
        if(!traceFrames.TryGetValue(look.Pointer.ToInt64(),out var s))return;
        s.Fields["rollAimDelta"]=swing;s.Fields["rollTwistDelta"]=twist;s.Fields["rollCorrected"]=corrected;
        s.Fields["guardedBaseRotation"]=Rotation(look._danBase);
    });
    void CaptureEvent(long key,string name)=>TraceSafe(()=>
    {
        if(traceFrames.TryGetValue(key,out var s))s.Events.Add(name);
        else journal!.Write(new {kind="event",frame=Time.frameCount,time=Time.unscaledTime,component=key.ToString("x"),name});
    });
    internal void ObserveFrameEnd()
    {
        TraceSafe(()=>
    {
        foreach(var key in traceFrames.Keys.ToArray())
        {
            var s=traceFrames[key];var look=s.Ref.Look;
            s.Fields["endFrame"]=Time.frameCount;s.Fields["endZ"]=Z(look);
            s.Fields["clipEnd"]=displays.ContainsKey(key);
            if(look){s.Fields["endBaseRotation"]=Rotation(look._danBase);s.Fields["endTopRotation"]=Rotation(look._danTop);}
            Emit(s,"end-of-frame");traceFrames.Remove(key);
        }
        });
    }
    void CloseTrace()
    {
        try
        {
            if(journal is {Closed:false})
            {
                foreach(var s in traceFrames.Values)Emit(s,"trace-close");
                journal.Write(new {kind="end",utc=DateTime.UtcNow});
            }
        }
        finally{traceFrames.Clear();journal?.Dispose();journal=null;}
    }
}
