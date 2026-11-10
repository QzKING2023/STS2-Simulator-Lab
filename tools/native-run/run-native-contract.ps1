param(
    [Parameter(Mandatory)][string]$BuildManifest,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$Seed = 'LAB_M2_1_BASELINE_01',
    [string]$Scenario = 'natural-death',
    [ValidateRange(1,480)][int]$ProcessTimeoutSeconds = 120,
    [string]$SnapshotId = 'workshop-snapshot-20261106-163352',
    [string]$SnapshotContentSha256 = '7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73',
    [string]$ModId = 'vanilla',
    [string]$ModVersion = '0.111.0',
    [string[]]$DependencyClosure = @(),
    [string]$WorkerIdentity = 'SimulatorLab.NativeRun',
    [string]$Adapter = 'native-run/v1',
    [string]$Collector = 'isolated NativeRun/GameProbe; native-trace/v1'
)
$ErrorActionPreference = 'Continue'
$lab = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$root = [IO.Path]::GetFullPath($OutputDirectory)
$runsRoot = [IO.Path]::GetFullPath((Join-Path $lab '.build/runs'))
if (!$root.StartsWith($runsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Contract output must be under .build/runs' }
if (Test-Path -LiteralPath $root) { throw 'Refusing to overwrite previous contract evidence' }
New-Item -ItemType Directory -Path $root | Out-Null
$slice = Join-Path $lab 'tools/native-run/run-native-slice.ps1'
$manifest = [IO.Path]::GetFullPath($BuildManifest)
$rows = [ordered]@{}
function Invoke-Case {
    param([string]$Name,[string]$Kind,[string]$Control = '',[string]$Replay = '')
    $out = Join-Path $root $Name
    $args = @('-NoProfile','-File',$slice,'-BuildManifest',$manifest,'-OutputDirectory',$out,'-Seed',$Seed,'-Scenario',$Scenario,'-RunKind',$Kind,'-ProcessTimeoutSeconds',$ProcessTimeoutSeconds,'-SnapshotId',$SnapshotId,'-SnapshotContentSha256',$SnapshotContentSha256,'-ModId',$ModId,'-ModVersion',$ModVersion,'-WorkerIdentity',$WorkerIdentity,'-Adapter',$Adapter,'-Collector',$Collector)
    if ($DependencyClosure.Count) { $args += '-DependencyClosure'; $args += $DependencyClosure }
    if ($Control) { $args += '-Control'; $args += $Control }
    if ($Replay) { $args += '-ReplayPath'; $args += $Replay }
    & pwsh @args *> (Join-Path $root "$Name.wrapper.log")
    $exit = $LASTEXITCODE
    $runManifest = if (Test-Path (Join-Path $out 'run-manifest.json')) { Get-Content -Raw (Join-Path $out 'run-manifest.json') | ConvertFrom-Json } else { $null }
    $rows[$Name] = [ordered]@{kind=$Kind;control=$Control;exitCode=$exit;directory=$out;manifest=$runManifest}
    return $runManifest
}
$primary = Invoke-Case -Name 'primary' -Kind 'primary'
$repeat = Invoke-Case -Name 'repeat' -Kind 'repeat'
$replay = $null
if ($primary -and (Test-Path (Join-Path $root 'primary/route.json'))) {
    $replay = Invoke-Case -Name 'replay' -Kind 'replay' -Replay (Join-Path $root 'primary/route.json')
} else {
    $rows['replay'] = [ordered]@{kind='replay';control='';exitCode=$null;directory=(Join-Path $root 'replay');manifest=$null}
}
$failure = Invoke-Case -Name 'failure' -Kind 'failure' -Control 'budget'
function Get-Value($object,[string]$name) { if ($null -eq $object) { return $null }; return $object.$name }
function Compare-Manifest($left,$right) {
    if ($null -eq $left -or $null -eq $right) { return [ordered]@{exact=$false;differences=@('missing-manifest')} }
    $diff = [Collections.Generic.List[string]]::new()
    if ($left.inputDigest -ne $right.inputDigest) { [void]$diff.Add('canonical-input') }
    if ($left.terminal -ne $right.terminal -or $left.stop -ne $right.stop) { [void]$diff.Add('terminal') }
    if ($left.lastBoundary -ne $right.lastBoundary -or (($left.lifecycleBoundaries -join '|') -ne ($right.lifecycleBoundaries -join '|'))) { [void]$diff.Add('lifecycle-boundary') }
    foreach ($field in @('stateDigest','rngDigest','traceDigest')) { if ($left.$field -ne $right.$field) { [void]$diff.Add($field) } }
    return [ordered]@{exact=$diff.Count -eq 0;differences=@($diff)}
}
$primaryRepeat = Compare-Manifest $primary $repeat
$primaryReplay = Compare-Manifest $primary $replay
$failureClassification = $null -ne $failure -and $failure.stop -in @('BudgetExceeded','Unsupported','RuntimeFailure','EnvironmentBlocked','EngineError','StrategyStopped','ProcessCrashed','InvalidAction') -and $failure.terminal -notin @('RunVictory','Death')
$summary = [ordered]@{
    schema='native-run-contract/v1';scenario=$Scenario;seed=$Seed;output=$root
    canonicalInput=if($primary){$primary.canonicalInput}else{$null}
    primary=$rows.primary;repeat=$rows.repeat;replay=$rows.replay;failure=$rows.failure
    primaryRepeat=$primaryRepeat;primaryReplay=$primaryReplay;failureClassification=$failureClassification
    acceptance=[ordered]@{actualNativeTerminal=($null-ne$primary -and $primary.stop -eq 'Completed' -and $primary.terminal -in @('RunVictory','Death'));repeatStable=$primaryRepeat.exact;replayExact=$primaryReplay.exact;failureRetained=$failureClassification}
}
$summary | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $root 'contract-summary.json') -Encoding utf8
$contractManifest = [ordered]@{schema='native-run-contract-manifest/v1';scenario=$Scenario;seed=$Seed;canonicalInput=if($primary){$primary.canonicalInput}else{$null};cases=@('primary','repeat','replay','failure');summary='contract-summary.json';collector=$Collector;adapter=$Adapter}
$contractManifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $root 'contract-manifest.json') -Encoding utf8
if (!$summary.acceptance.actualNativeTerminal -or !$summary.acceptance.repeatStable -or !$summary.acceptance.replayExact -or !$summary.acceptance.failureRetained) { exit 1 }
exit 0
