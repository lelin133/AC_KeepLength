param([Parameter(Mandatory)][string]$GameDir,[switch]$UseDefaultClipOffset,[switch]$EnableFrameTrace)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$game=[IO.Path]::GetFullPath($GameDir)
if(-not (Test-Path -LiteralPath (Join-Path $game 'Aicomi.exe'))){throw 'GameDir must contain Aicomi.exe'}
$target=Join-Path $game 'BepInEx/plugins/AC_KeepLength/AC_KeepLength.dll'
$source=Join-Path $root 'src/bin/Release/net6.0/AC_KeepLength.dll'
$config=Join-Path $game 'BepInEx/config/local.ac.keeplength.cfg'
if(Get-Process -Name Aicomi -ErrorAction SilentlyContinue){throw '游戏正在运行，未安装。请退出后再运行此脚本。'}
# Process enumeration can miss the running game in this environment. Confirm
# both executable and installed DLL are free before any backup or copy.
foreach($probePath in @((Join-Path $game 'Aicomi.exe'),$target)){
 if(Test-Path -LiteralPath $probePath){
  $probeHandle=$null
  try{$probeHandle=[IO.File]::Open($probePath,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}
  catch{throw "文件仍被占用，未安装：$probePath。请退出游戏后再试。"}
  finally{if($probeHandle){$probeHandle.Dispose()}}
 }
}
if(-not (Test-Path -LiteralPath $source)){throw '缺少已构建DLL'}
$duplicates=@(Get-ChildItem -LiteralPath (Join-Path $game 'BepInEx/plugins') -Recurse -Filter AC_KeepLength.dll | Where-Object FullName -NE $target)
if($duplicates.Count){throw '发现其他位置的同名DLL，先核对，避免重复加载。'}
$before=@(Get-ChildItem -LiteralPath (Join-Path $game 'BepInEx/plugins') -Recurse -Filter '*.dll' | Where-Object FullName -NE $target | ForEach-Object { [pscustomobject]@{path=$_.FullName;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} })
$stamp=Get-Date -Format 'yyyyMMdd-HHmmss'
$backup=Join-Path $root "backups/$stamp"
New-Item -ItemType Directory -Path $backup,(Split-Path $target) -Force | Out-Null
foreach($old in @($target,$config)){if(Test-Path -LiteralPath $old){Copy-Item -LiteralPath $old -Destination $backup}}
New-Item -ItemType Directory -Path (Join-Path $root 'dist') -Force | Out-Null
Copy-Item -LiteralPath $source -Destination (Join-Path $root 'dist/AC_KeepLength.dll') -Force
Copy-Item -LiteralPath $source -Destination $target -Force
# Keep an existing user's settings. This file is new on first install. Add the
# 0.2.0 clipping section when upgrading an existing 0.1.x configuration.
if(-not (Test-Path -LiteralPath $config)){
 $text=@'
## AC KeepLength 0.10.1 - initial test settings
[General]
Enabled = true
ToggleKey = F6
ModeSwitchKey = J
AllNativeShrink = true
Mode = FixedLengthClip

[NativeTip]
Offset = 0.03

[Rotation]
PreventHalfTurn = true
AnimationReference = true
StableAxialReference = true
BindPoseFallback = true

[Length]
RestLocalZ = 0.1898
Multiplier = 1

[Diagnostics]
DebugLog = false

[Clipping]
Enabled = true
Offset = 0.02
'@
 [IO.File]::WriteAllText($config,$text,[Text.UTF8Encoding]::new($false))
}
else {
 $existing=[IO.File]::ReadAllText($config)
 if($existing -notmatch '(?im)^AllNativeShrink\s*='){
  if($existing -match '(?ms)^\[General\]\s*\r?\n'){
   $existing=[regex]::Replace($existing,'(?ms)^(\[General\]\s*\r?\n)','${1}AllNativeShrink = true'+[Environment]::NewLine,1)
  }
 }
 if($existing -notmatch '(?im)^\[Clipping\]\s*$'){
  $suffix="`r`n[Clipping]`r`nEnabled = true`r`nOffset = 0.02`r`n"
  $existing=$existing.TrimEnd()+$suffix
 }
 if($UseDefaultClipOffset){
  $existing=[regex]::Replace($existing,'(?ms)(^\[Clipping\][ \t]*\r?\n)(.*?)(?=^\[|\z)',
   [Text.RegularExpressions.MatchEvaluator]{param($m)
    $m.Groups[1].Value+[regex]::Replace($m.Groups[2].Value,'(?m)^Offset\s*=\s*[^\r\n]*','Offset = 0.02')
   })
 }
 if($existing -notmatch '(?m)^PreventHalfTurn\s*='){
  if($existing -match '(?m)^\[Rotation\][ \t]*\r?$'){
   $existing=[regex]::Replace($existing,'(?m)^(\[Rotation\][ \t]*\r?\n)','${1}PreventHalfTurn = true'+[Environment]::NewLine)
  }else{$existing=$existing.TrimEnd()+"`r`n`r`n[Rotation]`r`nPreventHalfTurn = true`r`n"}
 }
 if($existing -notmatch '(?m)^AnimationReference\s*='){
  $existing=[regex]::Replace($existing,'(?m)^(\[Rotation\][ \t]*\r?\n)','${1}AnimationReference = true'+[Environment]::NewLine)
 }
 if($existing -notmatch '(?m)^StableAxialReference\s*='){
  $existing=[regex]::Replace($existing,'(?m)^(\[Rotation\][ \t]*\r?\n)','${1}StableAxialReference = true'+[Environment]::NewLine)
 }
 if($existing -notmatch '(?m)^BindPoseFallback\s*='){
  $existing=[regex]::Replace($existing,'(?m)^(\[Rotation\][ \t]*\r?\n)','${1}BindPoseFallback = true'+[Environment]::NewLine)
 }
 # Remove only the retired hand-specific section/key, preserving core settings.
 $existing=[regex]::Replace($existing,'(?ims)^\[RightHand\][ \t]*\r?\n.*?(?=^\[|\z)','')
 $existing=[regex]::Replace($existing,'(?ms)(^\[Diagnostics\][ \t]*\r?\n)(.*?)(?=^\[|\z)',
  [Text.RegularExpressions.MatchEvaluator]{param($m)
   $m.Groups[1].Value+[regex]::Replace($m.Groups[2].Value,'(?im)^(?:[ \t]*#[^\r\n]*\r?\n)*HandIkTrace[ \t]*=[^\r\n]*(?:\r?\n|$)','')
  })
 if($existing -notmatch '(?m)^Mode\s*='){
  $existing=[regex]::Replace($existing,'(?m)^(\[General\][ \t]*\r?\n)','${1}Mode = FixedLengthClip'+[Environment]::NewLine)
 }
 if($existing -notmatch '(?m)^ModeSwitchKey\s*='){
  $existing=[regex]::Replace($existing,'(?m)^(\[General\][ \t]*\r?\n)','${1}ModeSwitchKey = J'+[Environment]::NewLine)
 }
 if($existing -notmatch '(?im)^\[NativeTip\][ \t]*\r?$'){
  $existing=$existing.TrimEnd()+"`r`n`r`n[NativeTip]`r`nOffset = 0.03`r`n"
 }
 [IO.File]::WriteAllText($config,$existing,[Text.UTF8Encoding]::new($false))
}
if($EnableFrameTrace){
 $traceConfig=[IO.File]::ReadAllText($config)
 if($traceConfig -match '(?m)^FrameTrace\s*='){
  $traceConfig=[regex]::Replace($traceConfig,'(?m)^FrameTrace\s*=\s*[^\r\n]*','FrameTrace = true')
 }elseif($traceConfig -match '(?m)^\[Diagnostics\][ \t]*\r?$'){
  $traceConfig=[regex]::Replace($traceConfig,'(?m)^(\[Diagnostics\][ \t]*\r?\n)','${1}FrameTrace = true'+[Environment]::NewLine)
 }else{$traceConfig += "`r`n[Diagnostics]`r`nFrameTrace = true`r`n"}
 [IO.File]::WriteAllText($config,$traceConfig,[Text.UTF8Encoding]::new($false))
}
$unchanged=@($before | Where-Object {(Get-FileHash -LiteralPath $_.path).Hash -NE $_.sha256})
$sourceHash=(Get-FileHash -LiteralPath $source).Hash
if($unchanged.Count -or (Get-FileHash -LiteralPath $target).Hash -NE $sourceHash){throw '安装哈希校验未通过'}
$record=[ordered]@{installedAt=(Get-Date -Format o);source=$source;target=$target;sha256=$sourceHash;bytes=(Get-Item -LiteralPath $target).Length;otherPluginDllsUnchanged=$before.Count;backup=$backup;config=$config;gameStarted=$false;boundary='Offline installation verified; game execution and rendering pending user test'}
New-Item -ItemType Directory -Path (Join-Path $root 'checks') -Force | Out-Null
$record | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $root 'checks/install.json') -Encoding utf8
$before | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $root 'checks/other-plugin-hashes.json') -Encoding utf8
$record | ConvertTo-Json -Depth 4
