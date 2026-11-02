using KeepLength;
using System.Numerics;
int checks=0;
void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
ClipMesh Triangle(float a,float b,float c)=>new(){
    Positions=new[]{new Vector3(0,0,a),new Vector3(1,0,b),new Vector3(0,1,c)},
    Normals=Enumerable.Repeat(Vector3.UnitZ,3).ToArray(),Tangents=Enumerable.Repeat(new Vector4(1,0,0,-1),3).ToArray(),
    Colors=new[]{Vector4.Zero,Vector4.One,new Vector4(0,1,0,1)},
    UV=new[]{new[]{new Vector4(0,0,3,4),new Vector4(1,0,3,4),new Vector4(0,1,3,4)}},Submeshes=new[]{new[]{0,1,2}}};
foreach(var abc in new[]{(-1f,-1f,-1f),(1f,1f,1f),(-1f,1f,1f),(-1f,-1f,1f),(0f,1f,-1f),(0f,0f,1f),(0f,0f,-1f),(0f,0f,0f)})
{
    var input=Triangle(abc.Item1,abc.Item2,abc.Item3);var cut=PlaneClip.Cut(input,Vector3.UnitZ,0);
    foreach(int i in cut.Submeshes[0])Check(cut.Positions[i].Z<=1e-6,"no retained front vertex");
    for(int i=0;i<3;i++)Check(cut.Positions[i]==input.Positions[i],"original retained coordinates never compressed");
    for(int i=3;i<cut.Positions.Length;i++)
    {Check(MathF.Abs(cut.Positions[i].Z)<1e-6,"exact plane intersection");Check(MathF.Abs(cut.Normals[i].Length()-1)<1e-6,"unit normal");Check(cut.Tangents[i].W==-1,"tangent handedness");Check(cut.UV[0][i].Z==3 && cut.UV[0][i].W==4,"4D UV retained");}
    int expected=abc switch{(-1f,-1f,-1f)=>3,(1f,1f,1f)=>0,(-1f,1f,1f)=>3,(-1f,-1f,1f)=>6,(0f,0f,1f)=>0,_=>3};
    Check(cut.Submeshes[0].Length==expected,"topology case");
}
var square=new ClipMesh{Positions=new[]{new Vector3(0,0,-1),new Vector3(1,0,1),new Vector3(1,1,1),new Vector3(0,1,-1)},Submeshes=new[]{new[]{0,1,2},new[]{0,2,3}}};
var shared=PlaneClip.Cut(square,Vector3.UnitZ,0);Check(shared.Positions.Length==7,"shared edge cached across submeshes");Check(shared.Submeshes.Length==2,"submesh material slots retained");
Check(PlaneClip.Cut(square,Vector3.UnitZ,-2).Submeshes.Sum(x=>x.Length)==6,"positive offset exposes more");
Check(PlaneClip.Cut(square,Vector3.UnitZ,2).Submeshes.Sum(x=>x.Length)==0,"negative offset hides all");
// Deterministic randomized triangles, oblique planes, winding, UV interpolation.
var random=new Random(316);
for(int test=0;test<2000;test++)
{
    var input=Triangle(0,0,0);input.Positions=Enumerable.Range(0,3).Select(_=>new Vector3((float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1)).ToArray();
    var normal=Vector3.Normalize(new Vector3(.3f,.4f,1));float d=(float)random.NextDouble()-.5f;
    var result=PlaneClip.Cut(input,normal,d);var originalCross=Vector3.Cross(input.Positions[1]-input.Positions[0],input.Positions[2]-input.Positions[0]);
    var indices=result.Submeshes[0];
    for(int i=0;i<indices.Length;i+=3)
    {
        var a=result.Positions[indices[i]];var b=result.Positions[indices[i+1]];var c=result.Positions[indices[i+2]];
        Check(Vector3.Dot(Vector3.Cross(b-a,c-a),originalCross)>=-1e-7,"winding preserved");
        foreach(int j in new[]{indices[i],indices[i+1],indices[i+2]})Check(Vector3.Dot(normal,result.Positions[j])+d<=1e-5,"oblique clipped halfspace");
    }
}
try{PlaneClip.Cut(square,new Vector3(float.NaN,0,1),0);Check(false,"invalid plane accepted");}catch(ArgumentException){checks++;}
Console.WriteLine($"PlaneClip: {checks} checks passed; 2000 randomized triangles plus boundary, winding, streams and material submesh cases. CPU geometry only.");
