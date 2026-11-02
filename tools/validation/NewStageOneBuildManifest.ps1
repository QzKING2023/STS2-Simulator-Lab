param([Parameter(Mandatory)][string]$HarnessRoot,[Parameter(Mandatory)][string]$UpstreamRoot,[Parameter(Mandatory)][string]$OutputPath)
$ErrorActionPreference='Stop'
$lab=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'));$harness=[IO.Path]::GetFullPath($HarnessRoot)
$cache=[IO.Path]::GetFullPath((Join-Path $harness '../../..'))
function Rows($files,$root){@($files|Sort-Object FullName -Unique|ForEach-Object{[ordered]@{path=[IO.Path]::GetRelativePath($root,$_.FullName);sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})}
$sourceFiles=@(Get-ChildItem (Join-Path $lab 'src'),(Join-Path $lab 'tests'),(Join-Path $lab 'tools') -Recurse -File|Where-Object {$_.FullName-notmatch'[\\/](bin|obj)[\\/]' -and $_.Extension-in'.cs','.csproj','.ps1','.json','.props'})
$sourceFiles+=Get-Item (Join-Path $lab '.build/local-reference/NoOpMemoryCleaner/NoOpMemoryCleaner.csproj')
$artifactFiles=@(Get-ChildItem (Join-Path $harness 'bin/Release/net9.0') -File|Where-Object {$_.Extension-in'.dll','.json'})
$runtime=Get-Content (Join-Path $harness 'bin/Release/net9.0/OfflineSearchHarness.runtimeconfig.json') -Raw|ConvertFrom-Json
$artifactFiles+=Get-Item -LiteralPath $runtime.runtimeOptions.configProperties.CombatSolverDll
$artifactFiles+=Get-ChildItem (Join-Path $lab 'tests/SimulatorLab.Core.Checks/bin/Release/net9.0') -File|Where-Object {$_.Extension-in'.dll','.json'}
$pins=@(Get-ChildItem (Join-Path $lab '.build/pinned-inputs/eleanor-v0.4.1-forms-cardpool.fix'),(Join-Path $lab '.build/pinned-inputs/eleanor-v041-native-bootstrap') -File -Recurse)
$pins+=Get-Item -LiteralPath (Join-Path $runtime.runtimeOptions.configProperties.Sts2DataDir 'sts2.dll')
$pins+=Get-Item (Join-Path $runtime.runtimeOptions.configProperties.Sts2DataDir '0Harmony.dll'),(Join-Path $runtime.runtimeOptions.configProperties.Sts2DataDir 'GodotSharp.dll')
$upstreamFiles=@(Get-ChildItem (Join-Path $UpstreamRoot 'src'),(Join-Path $UpstreamRoot 'tools/search') -Recurse -File|Where-Object {$_.FullName-notmatch'[\\/](bin|obj|\.git)[\\/]' -and $_.Extension-in'.cs','.csproj','.props','.json'})
$upstreamFiles+=Get-Item (Join-Path $UpstreamRoot 'LICENSE'),(Join-Path $UpstreamRoot 'CombatSolver.csproj')
$compiledSources=@(Get-ChildItem (Join-Path $cache 'src'),$harness -Recurse -File|Where-Object {$_.FullName-notmatch'[\\/](bin|obj)[\\/]' -and $_.Extension-in'.cs','.csproj','.props'})
$compiledSources+=Get-Item (Join-Path $cache 'CombatSolver.csproj'),(Join-Path $cache 'lab-overlay-lock.json')
$snapshot=Join-Path (Split-Path ([IO.Path]::GetFullPath($OutputPath))) 'source-snapshot'
if(Test-Path -LiteralPath $snapshot){throw 'Use a fresh build snapshot directory.'}
foreach($set in @(@{files=$sourceFiles;root=$lab;name='lab'},@{files=$compiledSources;root=$cache;name='compiled'})){
 foreach($file in $set.files){$target=Join-Path (Join-Path $snapshot $set.name) ([IO.Path]::GetRelativePath($set.root,$file.FullName));New-Item -ItemType Directory -Path (Split-Path $target) -Force|Out-Null;Copy-Item -LiteralPath $file.FullName -Destination $target}
}
$toolchain=[ordered]@{sdk=(& dotnet --version|Out-String).Trim();runtimes=@(& dotnet --list-runtimes);os=[Environment]::OSVersion.VersionString;architecture=[Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()}
$body=[ordered]@{schema='stage-one-build/v1';head=(git -C $lab rev-parse HEAD);harness=$harness;labRoot=$lab;upstreamRoot=[IO.Path]::GetFullPath($UpstreamRoot);source=Rows $sourceFiles $lab;upstream=Rows $upstreamFiles ([IO.Path]::GetFullPath($UpstreamRoot));compiledSource=Rows $compiledSources $cache;artifacts=Rows $artifactFiles $lab;inputs=Rows $pins $lab;toolchain=$toolchain}
$json=$body|ConvertTo-Json -Depth 7 -Compress
$id=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($json)))
[ordered]@{buildId=$id;frozenUtc=[DateTime]::UtcNow.ToString('o');sourceSnapshot=$snapshot;manifest=$body}|ConvertTo-Json -Depth 9|Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Output "Frozen build $id; $OutputPath"
