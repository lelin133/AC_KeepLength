using H;
using UnityEngine;
using UObj=UnityEngine.Object;
using N3=System.Numerics.Vector3;
using N4=System.Numerics.Vector4;

namespace KeepLength;

// Owns only transient display geometry. The original mesh/materials stay intact.
internal sealed class ClipDisplay : IDisposable
{
    readonly SkinnedMeshRenderer source;
    readonly Mesh sourceMesh;
    readonly Mesh baked=null!;
    readonly Mesh output=null!;
    readonly GameObject proxy=null!;
    readonly MeshRenderer renderer=null!;
    readonly MaterialPropertyBlock properties=null!;
    readonly long owner,topBone,baseBone;
    readonly bool previousForceOff;
    public int Frame;
    public int TriangleCount { get; private set; }

    public static ClipDisplay? Find(LookAtPenis look,Transform top,Transform basis)
    {
        var human=look._maleTarget?.Body?._human;
        if(human==null || human.disposed || human.isReloading || !human.gameObject) return null;
        var candidates=human.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .Where(r=>r && r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff
                && r.name=="o_dankon" && r.sharedMesh
                && r.bones.Any(b=>b && b.Pointer==top.Pointer)
                && r.bones.Any(b=>b && b.Pointer==basis.Pointer)).Take(2).ToArray();
        return candidates.Length==1 ? new ClipDisplay(candidates[0],human.Pointer.ToInt64(),top.Pointer.ToInt64(),basis.Pointer.ToInt64()) : null;
    }
    ClipDisplay(SkinnedMeshRenderer source,long owner,long topBone,long baseBone)
    {
        this.source=source; sourceMesh=source.sharedMesh; previousForceOff=source.forceRenderingOff;
        this.owner=owner;this.topBone=topBone;this.baseBone=baseBone;
        try
        {
            baked=new Mesh {name="AC_KeepLength_Baked"};
            output=new Mesh {name="AC_KeepLength_Clipped"};output.MarkDynamic();
            proxy=new GameObject("AC_KeepLength_ClipDisplay");
            proxy.transform.SetParent(source.transform,false);
            proxy.AddComponent<MeshFilter>().sharedMesh=output;
            renderer=proxy.AddComponent<MeshRenderer>();renderer.enabled=false;
            properties=new MaterialPropertyBlock();
        }
        catch { Dispose();throw; }
    }
    public bool Valid=>source && source.sharedMesh && source.sharedMesh.Pointer==sourceMesh.Pointer
        && source.enabled && source.gameObject.activeInHierarchy && proxy;
    public bool Matches(LookAtPenis look,Transform top,Transform basis)=>Valid
        && look._maleTarget?.Body?._human?.Pointer.ToInt64()==owner
        && top.Pointer.ToInt64()==topBone && basis.Pointer.ToInt64()==baseBone;

    public void Apply(Vector3 planePoint,Vector3 worldNormal)
    {
        if(!Valid) throw new InvalidOperationException("Clip source changed");
        source.BakeMesh(baked,false);
        var matrix=source.transform.localToWorldMatrix;
        var localNormal=matrix.transpose.MultiplyVector(worldNormal);
        float distance=Vector3.Dot(worldNormal,source.transform.position-planePoint);
        var data=Read(baked);
        var clipped=PlaneClip.Cut(data,new N3(localNormal.x,localNormal.y,localNormal.z),distance);
        Write(output,clipped);
        proxy.layer=source.gameObject.layer;
        renderer.sharedMaterials=source.sharedMaterials;
        renderer.shadowCastingMode=source.shadowCastingMode;renderer.receiveShadows=source.receiveShadows;
        renderer.lightProbeUsage=source.lightProbeUsage;renderer.reflectionProbeUsage=source.reflectionProbeUsage;
        renderer.probeAnchor=source.probeAnchor;renderer.renderingLayerMask=source.renderingLayerMask;
        source.GetPropertyBlock(properties);renderer.SetPropertyBlock(properties);
        for(int sub=0;sub<source.sharedMaterials.Length;sub++)
        { properties.Clear();source.GetPropertyBlock(properties,sub);renderer.SetPropertyBlock(properties,sub); }
        // Commit visibility only once the full replacement is ready.
        renderer.enabled=true;source.forceRenderingOff=true;
        Frame=Time.frameCount;TriangleCount=clipped.Submeshes.Sum(t=>t.Length/3);
    }
    static ClipMesh Read(Mesh mesh)
    {
        var uv=new N4[8][];
        var list=new Il2CppSystem.Collections.Generic.List<Vector4>();
        for(int channel=0;channel<8;channel++)
        {
            list.Clear();mesh.GetUVs(channel,list);uv[channel]=new N4[list.Count];
            for(int i=0;i<list.Count;i++) uv[channel][i]=N(list[i]);
        }
        return new ClipMesh {
            Positions=mesh.vertices.Select(v=>new N3(v.x,v.y,v.z)).ToArray(),
            Normals=mesh.normals.Select(v=>new N3(v.x,v.y,v.z)).ToArray(),
            Tangents=mesh.tangents.Select(N).ToArray(), Colors=mesh.colors.Select(c=>new N4(c.r,c.g,c.b,c.a)).ToArray(),
            UV=uv,Submeshes=Enumerable.Range(0,mesh.subMeshCount).Select(s=>mesh.GetTriangles(s).ToArray()).ToArray()
        };
    }
    static void Write(Mesh mesh,ClipMesh data)
    {
        mesh.Clear();mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices=data.Positions.Select(v=>new Vector3(v.X,v.Y,v.Z)).ToArray();
        if(data.Normals.Length!=0) mesh.normals=data.Normals.Select(v=>new Vector3(v.X,v.Y,v.Z)).ToArray();
        if(data.Tangents.Length!=0) mesh.tangents=data.Tangents.Select(U).ToArray();
        if(data.Colors.Length!=0) mesh.colors=data.Colors.Select(v=>new Color(v.X,v.Y,v.Z,v.W)).ToArray();
        for(int channel=0;channel<data.UV.Length;channel++)
            if(data.UV[channel].Length!=0) mesh.SetUVs(channel,data.UV[channel].Select(U).ToArray());
        mesh.subMeshCount=data.Submeshes.Length;
        for(int sub=0;sub<data.Submeshes.Length;sub++) mesh.SetTriangles(data.Submeshes[sub],sub,false);
        mesh.RecalculateBounds();
    }
    static N4 N(Vector4 v)=>new(v.x,v.y,v.z,v.w);
    static Vector4 U(N4 v)=>new(v.X,v.Y,v.Z,v.W);
    public void Dispose()
    {
        if(source && source.forceRenderingOff) source.forceRenderingOff=previousForceOff;
        if(renderer)renderer.enabled=false;
        if(proxy)UObj.Destroy(proxy);
        if(baked)UObj.Destroy(baked);
        if(output)UObj.Destroy(output);
    }
}
