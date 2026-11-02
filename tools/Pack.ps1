param([string]$DllPath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
if(-not $DllPath){$DllPath=Join-Path $root 'src/bin/Release/net6.0/AC_KeepLength.dll'}
$DllPath=(Resolve-Path -LiteralPath $DllPath).Path
if(-not (Test-Path -LiteralPath (Join-Path $root 'LICENSE'))){throw 'LICENSE is required for packaging'}
$assembly=[Reflection.AssemblyName]::GetAssemblyName($DllPath)
if($assembly.Name -ne 'AC_KeepLength'){throw 'Expected AC_KeepLength.dll'}
$version=$assembly.Version.ToString(3)
$outputDir=Join-Path $root 'artifacts'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$zipPath=Join-Path $outputDir "AC_KeepLength-v$version.zip"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$stream=[IO.File]::Open($zipPath,[IO.FileMode]::Create,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
$zip=$null
try {
 $zip=[IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create,$false)
 [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$DllPath,'BepInEx/plugins/AC_KeepLength/AC_KeepLength.dll') | Out-Null
 [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $root 'INSTALL.md'),'INSTALL.md') | Out-Null
 [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $root 'LICENSE'),'LICENSE') | Out-Null
} finally {if($zip){$zip.Dispose()}else{$stream.Dispose()}}
$hash=(Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($zipPath+'.sha256'),$hash+'  '+[IO.Path]::GetFileName($zipPath)+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
Write-Output $zipPath
