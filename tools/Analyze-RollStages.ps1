param([Parameter(Mandatory)][string]$Path)
Add-Type -TypeDefinition @'
using System;
using System.Numerics;
public static class RollStageMath {
 public static double[] Delta(float[] x,float[] y){
  var a=Quaternion.Normalize(new Quaternion(x[0],x[1],x[2],x[3]));
  var b=Quaternion.Normalize(new Quaternion(y[0],y[1],y[2],y[3]));
  var d=Quaternion.Conjugate(a)*b;
  double roll=(2*Math.Atan2(d.Z,d.W)*180/Math.PI+540)%360-180;
  double swing=Math.Acos(Math.Clamp(Vector3.Dot(Vector3.Transform(Vector3.UnitZ,a),Vector3.Transform(Vector3.UnitZ,b)),-1,1))*180/Math.PI;
  double total=2*Math.Acos(Math.Clamp(Math.Abs(Quaternion.Dot(a,b)),0,1))*180/Math.PI;
  return new[]{swing,roll,total};
 }
}
'@
function Difference($a,$b){
 if($a -and $b){[RollStageMath]::Delta([float[]]$a,[float[]]$b)}else{@($null,$null,$null)}
}
$prior=@{};$flips=[Collections.Generic.List[object]]::new();$within=[Collections.Generic.List[object]]::new();$invalid=0;$count=0;$corrected=0;$eventCounts=@{}
foreach($line in [IO.File]::ReadLines((Resolve-Path $Path))){
 try{$r=$line | ConvertFrom-Json -ErrorAction Stop}catch{$invalid++;continue}
 if($r.kind -ne 'frame'){continue};$count++
 if($r.rollCorrected){$corrected++}
 foreach($e in $r.events){if(-not $eventCounts.ContainsKey($e)){$eventCounts[$e]=0};$eventCounts[$e]++}
 $guarded=if($r.guardedBaseRotation){$r.guardedBaseRotation}else{$r.baseRotation}
 $late=Difference $guarded $r.endBaseRotation
 if($null -ne $late[2] -and $late[2] -gt 10){$within.Add([pscustomobject]@{frame=$r.frame;swing=$late[0];roll=$late[1];total=$late[2];corrected=$r.rollCorrected;events=$r.events})}
 if($prior.ContainsKey($r.component)){
  $p=$prior[$r.component];$dt=$r.time-$p.time
  if($dt -gt 0 -and $dt -lt .15 -and $r.frame-$p.frame -le 2){
   $raw=Difference $p.baseRotation $r.baseRotation
   $output=Difference $p.endBaseRotation $r.endBaseRotation
   if(($null -ne $raw[1] -and [math]::Abs($raw[1]) -gt 140) -or ($null -ne $output[1] -and [math]::Abs($output[1]) -gt 140)){
    $flips.Add([pscustomobject]@{frame=$r.frame;time=$r.time;dt=$dt;raw=$raw;output=$output;local=(Difference $p.nativeBaseLocalRotation $r.nativeBaseLocalRotation);parent=(Difference $p.parentRotation $r.parentRotation);up=(Difference $p.aimUpRotation $r.aimUpRotation);previousCorrected=$p.rollCorrected;corrected=$r.rollCorrected;guardDelta=@($r.rollAimDelta,$r.rollTwistDelta);events=$r.events;previousEvents=$p.events;ready=$r.ready;changing=$r.changing;manual=$r.manual;topStick=$r.topStick;previousManual=$p.manual;previousTopStick=$p.topStick})
   }
  }
 }
 $prior[$r.component]=$r
}
[ordered]@{frames=$count;invalidLines=$invalid;correctedFrames=$corrected;eventCounts=$eventCounts;lateChanges=$within.Count;lateExamples=@($within.ToArray() | Select-Object -First 10);flips=@($flips.ToArray() | Select-Object -First 50);boundary='angles are [forward swing, axial twist, total rotation] in degrees; animation and posture labels require user context'} | ConvertTo-Json -Depth 8
