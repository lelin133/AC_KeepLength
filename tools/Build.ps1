param([string]$GameDir=$env:AICOMI_GAME_DIR)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$argsList=@('build',(Join-Path $root 'src/AC_KeepLength.csproj'),'-c','Release')
if($GameDir){$argsList+=('-p:GameDir='+[IO.Path]::GetFullPath($GameDir))}
& dotnet @argsList
if($LASTEXITCODE -ne 0){throw 'Build failed'}
