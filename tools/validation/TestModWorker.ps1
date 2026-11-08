[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$lab = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$snapshot = [IO.Path]::GetFullPath((Join-Path $lab 'local-mods/workshop-snapshot-20261106-163352'))
$snapshotManifest = $snapshot + '.manifest.json'
$workerProject = Join-Path $lab 'tools/mod-worker/ModWorker.csproj'
$worker = Join-Path $lab 'tools/mod-worker/bin/Release/net9.0/SimulatorLab.ModWorker.dll'
$build = Join-Path $lab '.build/mod-worker-validation-build-manifest.json'
$inputs = Join-Path $lab '.build/mod-worker-validation-inputs'
$runs = Join-Path $lab '.build/runs'

if (!(Test-Path -LiteralPath $worker -PathType Leaf)) {
    & dotnet build $workerProject -c Release --nologo --ignore-failed-sources -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw 'Mod worker build failed' }
}
New-Item -ItemType Directory -Path $inputs -Force | Out-Null
New-Item -ItemType Directory -Path $runs -Force | Out-Null
@{buildId='MOD-WORKER-VALIDATION';manifest=@{contentKey='MOD-WORKER-VALIDATION-CONTENT';source=@();inputs=@()}} |
    ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $build -Encoding utf8

function Invoke-WorkerCase([string]$Name, [hashtable]$Payload) {
    $inputPath = Join-Path $inputs ($Name + '-' + [Guid]::NewGuid().ToString('N') + '.json')
    $outputPath = Join-Path $runs ('mod-worker-validation-' + $Name + '-' + [Guid]::NewGuid().ToString('N'))
    $Payload | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $inputPath -Encoding utf8
    & dotnet $worker --input $inputPath --output $outputPath
    $exitCode = $LASTEXITCODE
    $manifest = Get-Content -LiteralPath (Join-Path $outputPath 'run-manifest.json') -Raw | ConvertFrom-Json
    if ([int]$manifest.exitCode -ne $exitCode) { throw "${Name}: process/run manifest exit mismatch" }
    return @{ ExitCode = $exitCode; Root = $outputPath; Manifest = $manifest }
}

$base = @{snapshotDirectory=$snapshot;snapshotManifestPath=$snapshotManifest;modId='Watcher';targetGameVersion='0.111.0';seed='MOD-WORKER-DETERMINISTIC'}
$normalA = Invoke-WorkerCase 'normal-a' $base
$normalB = Invoke-WorkerCase 'normal-b' $base
if ($normalA.ExitCode -ne 0 -or $normalA.Manifest.status -ne 'succeeded') { throw 'normal static loading path did not succeed' }
if ($normalA.Manifest.discovery.digest -ne $normalB.Manifest.discovery.digest) { throw 'same worker input produced different discovery digest' }
if ($normalA.Manifest.isolation.coreLoadedModAssembly -ne $false -or $normalA.Manifest.isolation.modRegistrationCount -ne 0 -or $normalA.Manifest.isolation.harmonyPatchCount -ne 0) { throw 'core/patch isolation evidence failed' }
if ($normalA.Manifest.isolation.temporaryFileCount -ne 0 -or $normalA.Manifest.isolation.assemblyLoadContextUnloaded -ne $true) { throw 'worker teardown evidence failed' }
if (@(Get-ChildItem -LiteralPath $normalA.Root -Recurse -File | Where-Object Extension -in @('.tmp','.dll','.pck')).Count -ne 0) { throw 'worker run retained a temporary or Mod binary' }

$missingManifest = Invoke-WorkerCase 'missing-manifest' @{snapshotDirectory=$snapshot;snapshotManifestPath=($snapshotManifest + '.missing');modId='Watcher';targetGameVersion='0.111.0';seed='MOD-WORKER-DETERMINISTIC'}
if ($missingManifest.Manifest.errors.stableCode -notcontains 'MOD-W001') { throw 'missing manifest was not classified as MissingFile' }
$badDllPath = Invoke-WorkerCase 'path-escape' @{snapshotDirectory=$snapshot;snapshotManifestPath=$snapshotManifest;modId='Watcher';targetGameVersion='0.111.0';seed='MOD-WORKER-DETERMINISTIC';requestedDllPath='..\..\Windows\System32\kernel32.dll'}
if ($badDllPath.Manifest.errors.stableCode -notcontains 'MOD-W005') { throw 'path escape was not rejected' }
$badDllType = Invoke-WorkerCase 'bad-dll-type' @{snapshotDirectory=$snapshot;snapshotManifestPath=$snapshotManifest;modId='Watcher';targetGameVersion='0.111.0';seed='MOD-WORKER-DETERMINISTIC';requestedDllPath='3737335127/BaseLib/BaseLib.pck'}
if ($badDllType.Manifest.errors.stableCode -notcontains 'MOD-W002') { throw 'non-DLL requested as DLL was not rejected' }
$missingDependency = Invoke-WorkerCase 'dependency-missing' @{snapshotDirectory=$snapshot;snapshotManifestPath=$snapshotManifest;modId='NoSuchMod';targetGameVersion='0.111.0';seed='MOD-WORKER-DETERMINISTIC'}
if ($missingDependency.Manifest.errors.stableCode -notcontains 'MOD-W004') { throw 'missing dependency was not classified' }
$blocked = Invoke-WorkerCase 'blocked-io' @{snapshotDirectory=$snapshot;snapshotManifestPath=$snapshotManifest;modId='Watcher';targetGameVersion='0.111.0';seed='MOD-WORKER-DETERMINISTIC';accessAttempts=@(@{operation='enumerate-workshop';path='D:\SteamLibrary\steamapps\workshop\content\2868840'});writeAttempts=@(@{operation='save';path='C:\formal\saves\save.json'})}
if ($blocked.ExitCode -ne 2 -or $blocked.Manifest.status -ne 'blocked' -or $blocked.Manifest.errors.stableCode -notcontains 'MOD-W006' -or $blocked.Manifest.errors.stableCode -notcontains 'MOD-W007') { throw 'blocked access/write attempts were not isolated' }
$intent = Invoke-WorkerCase 'intentgraph2-candidates' @{snapshotDirectory=$snapshot;snapshotManifestPath=$snapshotManifest;modId='intentgraph2';targetGameVersion='0.111.0';seed='MOD-WORKER-DETERMINISTIC'}
if ($intent.ExitCode -ne 0 -or $intent.Manifest.status -ne 'succeeded') { throw 'intentgraph2 candidate selection did not succeed' }
$intentCandidates = @($intent.Manifest.discovery.assemblyCandidates)
$legacyIntent = $intentCandidates | Where-Object { $_.relativePath -eq 'intentgraph2core-lt-0.110.0.dll' }
$selectedIntent = $intentCandidates | Where-Object { $_.relativePath -eq 'intentgraph2core.dll' }
if ($legacyIntent.selection -ne 'rejected' -or $legacyIntent.declaredGameVersion -ne '0.110.0' -or $legacyIntent.selectionReason -notmatch 'does not match target game 0\.111\.0') { throw 'intentgraph2 legacy candidate was not explicitly rejected' }
if ($selectedIntent.selection -ne 'selected' -or @($intent.Manifest.discovery.assemblies | Where-Object { $_.relativePath -eq 'intentgraph2core-lt-0.110.0.dll' }).Count -ne 0) { throw 'intentgraph2 selected the wrong duplicate assembly' }
$shadow = Invoke-WorkerCase 'shadowverse-large-file' @{snapshotDirectory=$snapshot;snapshotManifestPath=$snapshotManifest;modId='shadowversebydmod';targetGameVersion='0.111.0';seed='MOD-WORKER-DETERMINISTIC'}
if ($shadow.ExitCode -ne 0 -or $shadow.Manifest.status -ne 'succeeded' -or $shadow.Manifest.errors.Count -ne 0) { throw 'shadowverse large-file discovery did not succeed' }
$shadowPck = @($shadow.Manifest.discovery.files | Where-Object { $_.relativePath -eq 'shadowversebydmod.pck' })
if ($shadowPck.Count -ne 1 -or [int64]$shadowPck[0].length -le 2147483648) { throw 'shadowverse large PCK was not hashed without truncation' }

Write-Output "PASS mod-worker normal=$($normalA.Manifest.discovery.digest) deterministic=True failures=7 intentgraph2Candidates=$($intentCandidates.Count) shadowverseBytes=$($shadowPck[0].length)"
