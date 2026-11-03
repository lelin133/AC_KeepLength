param([Parameter(Mandatory)][string]$Path)
$rows=[Collections.Generic.List[object]]::new()
$invalid=0
foreach($line in [IO.File]::ReadLines((Resolve-Path $Path))){
 try{$rows.Add(($line | ConvertFrom-Json -ErrorAction Stop))}catch{$invalid++}
}
$frames=@($rows | Where-Object kind -EQ 'frame')
function Brief($r){
 $r | Select-Object frame,time,rest,beforeRestoreZ,savedNativeZ,afterRestoreZ,afterNativeZ,afterWriteZ,endZ,topStick,manual,eligible,clipBefore,clipEnd,triangles,events,completion
}
function Different($a,$b){$null -ne $a -and $null -ne $b -and [Math]::Abs($a-$b) -gt 0.000001}
$transitions=[Collections.Generic.List[object]]::new()
$prior=''
$sameNative=0
$late=[Collections.Generic.List[object]]::new()
foreach($r in $frames){
 $state=@($r.rest,$r.multiplier,$r.offset,$r.enabled,$r.clipEnabled,$r.eligible,$r.topStick,$r.manual,$r.ready,$r.changing) | ConvertTo-Json -Compress
 if($state -cne $prior){$transitions.Add((Brief $r))}
 $prior=$state
 if($null -ne $r.afterNativeZ -and $null -ne $r.afterRestoreZ -and -not (Different $r.afterNativeZ $r.afterRestoreZ)){$sameNative++}
 if(Different $r.afterWriteZ $r.endZ){$late.Add((Brief $r))}
}
[ordered]@{path=$Path;records=$rows.Count;invalidLines=$invalid;frames=$frames.Count;
 completion=@($frames | Group-Object completion | Select-Object Name,Count);nativeValueUnchanged=$sameNative;
 lateZChanges=$late.Count;lateZExamples=@($late | Select-Object -First 4);transitions=@($transitions.ToArray());
 first=(Brief $frames[0]);last=(Brief $frames[-1])} | ConvertTo-Json -Depth 7
