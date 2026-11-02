using System.Numerics;
namespace KeepLength;

internal sealed class ClipMesh
{
    public Vector3[] Positions = Array.Empty<Vector3>(), Normals = Array.Empty<Vector3>();
    public Vector4[] Tangents = Array.Empty<Vector4>(), Colors = Array.Empty<Vector4>();
    public Vector4[][] UV = Array.Empty<Vector4[]>();
    public int[][] Submeshes = Array.Empty<int[]>();
}

// Clip actual triangles, including interpolated boundary vertices. Never move
// retained vertices or scale their UVs. No cap: this is an invisible front.
internal static class PlaneClip
{
    public static ClipMesh Cut(ClipMesh input, Vector3 normal, float distance)
    {
        int count = input.Positions.Length;
        if (!Finite(normal) || !float.IsFinite(distance) || normal.LengthSquared() < 1e-16f)
            throw new ArgumentException("Invalid clipping plane");
        var positions = new List<Vector3>(input.Positions);
        var normals = Stream(input.Normals, count);
        var tangents = Stream(input.Tangents, count);
        var colors = Stream(input.Colors, count);
        var uv = input.UV.Select(channel => Stream(channel, count)).ToArray();
        var signed = new float[count];
        for (int i = 0; i < count; i++)
        {
            if (!Finite(input.Positions[i])) throw new ArgumentException("Non-finite vertex");
            signed[i] = Vector3.Dot(normal, input.Positions[i]) + distance;
            if (!float.IsFinite(signed[i])) throw new ArgumentException("Non-finite projection");
        }
        var edges = new Dictionary<long, int>();
        int Intersection(int a, int b)
        {
            if (signed[a] == 0) return a;
            if (signed[b] == 0) return b;
            long key = ((long)Math.Min(a,b) << 32) | (uint)Math.Max(a,b);
            if (edges.TryGetValue(key, out int existing)) return existing;
            float t = Math.Clamp(signed[a] / (signed[a] - signed[b]), 0, 1);
            int index = positions.Count;
            positions.Add(Vector3.Lerp(input.Positions[a], input.Positions[b], t));
            if (normals.Count != 0)
            {
                var n = Vector3.Lerp(input.Normals[a], input.Normals[b], t);
                normals.Add(n.LengthSquared() > 1e-16f ? Vector3.Normalize(n) : input.Normals[a]);
            }
            if (tangents.Count != 0)
            {
                var v = Vector4.Lerp(input.Tangents[a], input.Tangents[b], t);
                var xyz = new Vector3(v.X,v.Y,v.Z);
                if(xyz.LengthSquared()>1e-16f) xyz = Vector3.Normalize(xyz);
                tangents.Add(new Vector4(xyz, t < .5f ? input.Tangents[a].W : input.Tangents[b].W));
            }
            if (colors.Count != 0) colors.Add(Vector4.Lerp(input.Colors[a],input.Colors[b],t));
            for(int channel=0;channel<uv.Length;channel++)
                if(uv[channel].Count!=0) uv[channel].Add(Vector4.Lerp(input.UV[channel][a],input.UV[channel][b],t));
            edges.Add(key,index); return index;
        }
        var output = new int[input.Submeshes.Length][];
        // Reuse one four-vertex polygon; a clipped triangle has at most four.
        var polygon = new int[4];
        for(int sub=0;sub<output.Length;sub++)
        {
            var triangles=input.Submeshes[sub];
            if(triangles.Length%3!=0) throw new ArgumentException("Non-triangle topology");
            var indices=new List<int>(triangles.Length);
            for(int i=0;i<triangles.Length;i+=3)
            {
                int size=0;
                for(int edge=0;edge<3;edge++)
                {
                    int a=triangles[i+edge], b=triangles[i+(edge+1)%3];
                    if((uint)a>=(uint)count || (uint)b>=(uint)count) throw new ArgumentException("Invalid index");
                    bool insideA=signed[a]<=0, insideB=signed[b]<=0;
                    if(insideA) polygon[size++]=a;
                    if(insideA!=insideB)
                    {
                        int intersection=Intersection(a,b);
                        if(size==0 || polygon[size-1]!=intersection) polygon[size++]=intersection;
                    }
                }
                // Degenerate boundary cases (vertices exactly on the plane).
                if(size>1 && polygon[0]==polygon[size-1]) size--;
                for(int k=1;k<size-1;k++)
                {
                    int a=polygon[0],b=polygon[k],c=polygon[k+1];
                    if(a==b || b==c || c==a) continue;
                    if(Vector3.Cross(positions[b]-positions[a],positions[c]-positions[a]).LengthSquared()<=1e-20f) continue;
                    indices.Add(a);indices.Add(b);indices.Add(c);
                }
            }
            output[sub]=indices.ToArray();
        }
        return new ClipMesh { Positions=positions.ToArray(),Normals=normals.ToArray(),Tangents=tangents.ToArray(),
            Colors=colors.ToArray(),UV=uv.Select(x=>x.ToArray()).ToArray(),Submeshes=output };
    }
    static List<T> Stream<T>(T[] values,int count)
    {
        if(values.Length!=0 && values.Length!=count) throw new ArgumentException("Vertex stream length mismatch");
        return new List<T>(values);
    }
    static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
}
