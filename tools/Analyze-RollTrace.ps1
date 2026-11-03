param([Parameter(Mandatory)][string]$Path)
Add-Type -TypeDefinition @'
using System;
using System.Numerics;
public static class RollTraceMath {
 public static double[] Delta(float[] x,float[] y){
  var a=Quaternion.Normalize(new Quaternion(x[0],x[1],x[2],x[3]));
  var b=Quaternion.Normalize(new Quaternion(y[0],y[1],y[2],y[3]));
  var d=Quaternion.Conjugate(a)*b;
  double roll=2*Math.Atan2(d.Z,d.W)*180/Math.PI;
  roll=(roll+540)%360-180;
  var f=Vector3.Transform(Vector3.UnitZ,a);var g=Vector3.Transform(Vector3.UnitZ,b);
  double swing=Math.Acos(Math.Clamp(Vector3.Dot(f,g),-1,1))*180/Math.PI;
  double total=2*Math.Acos(Math.Clamp(Math.Abs(Quaternion.Dot(a,b)),0,1))*180/Math.PI;
  return new[]{swing,roll,total};
 }
}
'@
$prior=@{};$candidates=[Collections.Generic.List[object]]::new();$invalid=0;$frames=0
foreach($line in [IO.File]::ReadLines((Resolve-Path $Path))){
 try{$r=$line | ConvertFrom-Json -ErrorAction Stop}catch{$invalid++;continue}
 if($r.kind -ne 'frame'){continue};$frames++
 $key=$r.component
 if($prior.ContainsKey($key)){
  $p=$prior[$key];$dt=$r.time-$p.time
  if($r.baseRotation -and $p.baseRotation -and $dt -gt 0 -and $dt -lt .15 -and $r.frame-$p.frame -le 2){
   $d=[RollTraceMath]::Delta([float[]]$p.baseRotation,[float[]]$r.baseRotation)
   if([Math]::Abs($d[1]) -gt 100 -and $d[0] -lt 35){
    $candidates.Add([pscustomobject]@{frame=$r.frame;time=$r.time;dt=$dt;swing=$d[0];roll=$d[1];total=$d[2];ready=$r.ready;changing=$r.changing;topStick=$r.topStick;manual=$r.manual;target=$r.target;previousBase=$p.baseRotation;currentBase=$r.baseRotation;previousTop=$p.topRotation;currentTop=$r.topRotation})
   }
  }
 }
 $prior[$key]=$r
}
[ordered]@{path=$Path;frames=$frames;invalidLines=$invalid;candidates=$candidates.Count;examples=@($candidates.ToArray() | Select-Object -First 25);boundary='World base quaternion candidates only; parent/aim-up changes and animation state identity are not recorded in these versions'} | ConvertTo-Json -Depth 7
