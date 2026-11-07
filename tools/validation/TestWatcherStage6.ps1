#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$SnapshotDirectory = (Join-Path (Get-Location) 'local-mods/workshop-snapshot-20261106-163352'),
    [string]$SnapshotManifestPath = (Join-Path (Get-Location) 'local-mods/workshop-snapshot-20261106-163352.manifest.json'),
    [string]$WorkerRunManifestPath = ''
)
$ErrorActionPreference = 'Stop'
$lab = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$project = Join-Path $lab 'tools/mod-worker/WatcherStage6.csproj'
$build = Join-Path $lab '.build/watcher-stage6-validation'
New-Item -ItemType Directory -Force $build | Out-Null
dotnet build $project -c Release --ignore-failed-sources -p:NuGetAudit=$false | Out-Host
$runner = Join-Path $lab 'tools/mod-worker/bin/Release/net9.0/WatcherStage6.dll'
if (!(Test-Path $runner)) { throw "stage 6 runner was not built: $runner" }

if ([string]::IsNullOrWhiteSpace($WorkerRunManifestPath)) {
    $WorkerRunManifestPath = (Get-ChildItem (Join-Path $lab '.build/runs') -Recurse -Filter run-manifest.json -File |
        ForEach-Object { try { $j = Get-Content -Raw $_.FullName | ConvertFrom-Json; if ($j.status -eq 'succeeded' -and $j.modId -eq 'Watcher') { $_ } } catch {} } |
        Select-Object -First 1).FullName
}
if ([string]::IsNullOrWhiteSpace($WorkerRunManifestPath)) { throw 'a successful Watcher worker run manifest is required' }

$requestPath = Join-Path $build 'request.json'
$request = [ordered]@{
    snapshotDirectory = [IO.Path]::GetFullPath($SnapshotDirectory)
    snapshotManifestPath = [IO.Path]::GetFullPath($SnapshotManifestPath)
    seed = 'LAB_V0111_WATCHER_STAGE6_01'
    actions = @(
        [ordered]@{ kind = 'select-character'; choiceIndex = 0 }
        [ordered]@{ kind = 'start-run' }
        [ordered]@{ kind = 'play-card'; cardId = 'runtime-discovered'; targetId = 'primary-enemy' }
        [ordered]@{ kind = 'end-turn' }
        [ordered]@{ kind = 'claim-reward'; choiceIndex = 0 }
        [ordered]@{ kind = 'leave-combat' }
        [ordered]@{ kind = 'cleanup' }
    )
    workerRunManifestPath = [IO.Path]::GetFullPath($WorkerRunManifestPath)
}
$request | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $requestPath -Encoding utf8

$before = @{}
foreach ($path in @($SnapshotManifestPath, (Join-Path $SnapshotDirectory '3747492505/Watcher.json'), (Join-Path $SnapshotDirectory '3747492505/Watcher.dll'), (Join-Path $SnapshotDirectory '3747492505/Watcher.pck'))) {
    $before[$path] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}

function Invoke-Case([string]$name) {
    $out = Join-Path $lab ".build/runs/watcher-stage6-$name-$([guid]::NewGuid().ToString('N'))"
    $p = Start-Process dotnet -ArgumentList @($runner, '--request', $requestPath, '--output', $out) -Wait -PassThru -NoNewWindow
    if ($p.ExitCode -ne 2) { throw "expected Unsupported exit 2 for $name, got $($p.ExitCode)" }
    $report = Get-Content -Raw (Join-Path $out 'stage6-report.json') | ConvertFrom-Json
    if ($report.Report.Status -ne 'Blocked' -or $report.Report.StopCode -ne 'MOD-010') { throw "missing precise Unsupported stop for $name" }
    if ($report.Report.Deterministic -ne $false) { throw "unexecuted runtime must not claim determinism for $name" }
    if (@($report.Report.Errors | Where-Object { $_.Code -eq 'Unsupported' }).Count -eq 0) { throw "Unsupported evidence missing for $name" }
    if ((Get-ChildItem -LiteralPath $out -Recurse -File | Where-Object Extension -in @('.dll','.pck')).Count -ne 0) { throw "stage 6 output copied Mod binaries for $name" }
    return $report.Report
}
$a = Invoke-Case 'a'
$b = Invoke-Case 'b'
if ($a.DeterminismDigest -ne $b.DeterminismDigest -or $a.InputDigest -ne $b.InputDigest) { throw 'identical stage 6 inputs did not produce identical stop digests' }
foreach ($pair in $before.GetEnumerator()) {
    if ((Get-FileHash -LiteralPath $pair.Key -Algorithm SHA256).Hash -ne $pair.Value) { throw "fixed snapshot changed: $($pair.Key)" }
}
Write-Output "PASS watcher-stage6 unsupported stop deterministic=$($a.DeterminismDigest) worker=$WorkerRunManifestPath"
