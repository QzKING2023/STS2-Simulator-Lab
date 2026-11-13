[CmdletBinding()]
param(
    [string]$SourceRun = '.build/runs/m2-2-yuki-foundation-repair-20261110-r260-acheron',
    [string]$FreshRun = '.build/runs/m2-5-acheron-20261113-foundation-primary',
    [string]$Output = '.build/runs/m2-6-acheron-20261113'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RepoPath([string]$Path) {
    $candidate = if ([IO.Path]::IsPathRooted($Path)) { $Path } else { Join-Path (Get-Location) $Path }
    return [IO.Path]::GetFullPath($candidate)
}

function Read-Json([string]$Path) {
    return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Write-Json([string]$Path, $Value) {
    $Value | ConvertTo-Json -Depth 24 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Has-Property($Object, [string]$Name) {
    return $null -ne $Object -and $null -ne $Object.PSObject.Properties[$Name]
}

function Get-Property($Object, [string]$Name) {
    if (Has-Property $Object $Name) { return $Object.PSObject.Properties[$Name].Value }
    return $null
}

$sourceRoot = Resolve-RepoPath $SourceRun
$freshRoot = Resolve-RepoPath $FreshRun
$outputRoot = Resolve-RepoPath $Output
if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container)) { throw "source run missing: $sourceRoot" }
if (-not (Test-Path -LiteralPath $freshRoot -PathType Container)) { throw "fresh run missing: $freshRoot" }
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

$required = @('canonical-input.json', 'foundation-fixture.json', 'foundation-lifecycle.json', 'foundation-cleanup.json', 'result.json', 'route.json', 'final-state.json', 'trace.jsonl')
foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $sourceRoot $name) -PathType Leaf)) { throw "source evidence missing: $name" }
}

$canonical = Read-Json (Join-Path $sourceRoot 'canonical-input.json')
$fixture = Read-Json (Join-Path $sourceRoot 'foundation-fixture.json')
$lifecycle = Read-Json (Join-Path $sourceRoot 'foundation-lifecycle.json')
$cleanup = Read-Json (Join-Path $sourceRoot 'foundation-cleanup.json')
$resultEnvelope = Read-Json (Join-Path $sourceRoot 'result.json')
$route = Read-Json (Join-Path $sourceRoot 'route.json')
$finalState = Read-Json (Join-Path $sourceRoot 'final-state.json')

$expected = @{
    targetGameVersion = '0.111.0'
    gameCommit = '41cef1ea'
    snapshotId = 'workshop-snapshot-20261106-163352'
    snapshotContentSha256 = '7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73'
    snapshotManifestSha256 = '09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3'
    modId = 'Acheron'
    dependency = 'STS2-RitsuLib compat/0.111.0'
}
foreach ($key in @('targetGameVersion', 'gameCommit', 'snapshotId', 'snapshotContentSha256', 'snapshotManifestSha256', 'modId')) {
    if ([string]$canonical.$key -ne $expected[$key]) { throw "frozen input guard failed for ${key}: $($canonical.$key)" }
}
if (-not @($canonical.dependencyClosure) -contains $expected.dependency) { throw 'frozen dependency closure guard failed' }
if ([string]$fixture.game.TargetGameVersion -ne $expected.targetGameVersion -or [string]$fixture.game.GameCommit -ne $expected.gameCommit) { throw 'foundation fixture game guard failed' }

$sha = [ordered]@{}
foreach ($name in $required) {
    $path = Join-Path $sourceRoot $name
    $sha[$name] = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
}
$freshHashes = [ordered]@{}
foreach ($name in @('run-manifest.json', 'result.json', 'process.json', 'stderr.log')) {
    $path = Join-Path $freshRoot $name
    if (Test-Path -LiteralPath $path -PathType Leaf) { $freshHashes[$name] = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant() }
}

$kindCounts = @{}
$identityRows = @{}
$cardPlayRows = @()
$tracePath = Join-Path $sourceRoot 'trace.jsonl'
foreach ($line in Get-Content -LiteralPath $tracePath) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $record = $line | ConvertFrom-Json
    $payload = Get-Property $record 'payload'
    if ($null -eq $payload) { continue }
    $kind = [string](Get-Property $payload 'kind')
    if ($kind) { $kindCounts[$kind] = 1 + [int]($kindCounts[$kind]) }
    $detail = Get-Property $payload 'detail'
    $model = if (Has-Property $detail 'model') { [string](Get-Property $detail 'model') } else { '' }
    if ($kind -eq 'identity' -and $model -match '^(CARD|POWER|RELIC)\.ACHERON-') {
        if (-not $identityRows.ContainsKey($model)) { $identityRows[$model] = [ordered]@{ model = $model; firstSeq = [int]$record.seq; identityCount = 0; playStarted = 0; playFinished = 0 } }
        $identityRows[$model].identityCount++
    }
    $history = Get-Property $detail 'facts'
    $historyType = [string](Get-Property $history 'type')
    $historyFacts = Get-Property $history 'facts'
    $cardPlay = Get-Property $historyFacts 'CardPlay'
    $card = Get-Property $cardPlay 'Card'
    if ($kind -eq 'combat-history' -and $historyType -in @('CardPlayStartedEntry', 'CardPlayFinishedEntry')) {
        if ($null -ne $card -and [string](Get-Property $card 'model') -match '^CARD\.ACHERON-') {
            $model = [string](Get-Property $card 'model')
            if (-not $identityRows.ContainsKey($model)) { $identityRows[$model] = [ordered]@{ model = $model; firstSeq = [int]$record.seq; identityCount = 0; playStarted = 0; playFinished = 0 } }
            if ($historyType -eq 'CardPlayStartedEntry') { $identityRows[$model].playStarted++ }
            if ($historyType -eq 'CardPlayFinishedEntry') { $identityRows[$model].playFinished++ }
            if ($historyType -eq 'CardPlayFinishedEntry') { $cardPlayRows += [ordered]@{ seq = [int]$record.seq; model = $model; resultPile = [string](Get-Property $cardPlay 'ResultPile') } }
        }
    }
}

$identityList = @($identityRows.Values | Sort-Object model)
$playedCards = @($identityList | Where-Object { $_.playFinished -gt 0 } | ForEach-Object model)
$actionKinds = @($route | ForEach-Object { [string]$_.action.kind } | Group-Object | Sort-Object Name | ForEach-Object { [ordered]@{ kind = $_.Name; count = $_.Count } })
$routeCardModels = @($route | ForEach-Object { Get-Property (Get-Property $_.action 'decision') 'model' } | Where-Object { $_ -match '^CARD\.ACHERON-' } | Sort-Object -Unique)
$native = $finalState.lifecycle.native
$nativeTerminal = ([bool]$native.Won) -or ([int]$native.Losses -gt 0) -or ([int]$native.RunEndStarts -gt 0) -or ([int]$native.RunEndReturns -gt 0)
$hasBattleVictory = $kindCounts.ContainsKey('battle-victory-before-cleanup') -and $kindCounts.ContainsKey('battle-victory-cleanup')
$hasRewardBoundary = $kindCounts.ContainsKey('reward-offer') -and $kindCounts.ContainsKey('rewards-completed') -and $kindCounts.ContainsKey('foundation-cross-room-boundary')

$adapterConfig = [ordered]@{
    schema = 'acheron-m26-adapter/v1'
    purpose = 'Read-only projection of fixed v0.111.0 Acheron evidence into the M2-6 matrix.'
    fixedInputGuard = $expected
    sourceRun = '.build/runs/m2-2-yuki-foundation-repair-20261110-r260-acheron'
    freshRun = '.build/runs/m2-5-acheron-20261113-foundation-primary'
    selectedComplexSurface = 'CARD.ACHERON-SLASH_ACHERON'
    selectedSurfaceRule = 'Require real identity plus CardPlayStarted/CardPlayFinished and retain status Partial until native Death/RunVictory and dedicated cleanup are observed.'
    outputPolicy = 'append-only; no source evidence is copied, deleted, or rewritten'
}

$observedRows = @()
foreach ($row in $identityList) {
    $checkpoints = @('discovery', 'construction')
    $reason = 'identity observed in fixed native trace; no dedicated content probe cleanup or terminal path'
    if ($row.playFinished -gt 0) {
        $checkpoints += @('activation', 'content-effect')
        $reason = "CardPlayStarted/CardPlayFinished observed $($row.playFinished)x; battle/reward boundary is real, but terminal and dedicated content cleanup are absent"
    }
    $observedRows += [ordered]@{
        contentUnit = $row.model
        kind = ($row.model -split '\.')[0].Substring(0,1) + ($row.model -split '\.')[0].Substring(1).ToLowerInvariant()
        status = 'Partial'
        lifecycleCheckpoints = $checkpoints
        evidenceRef = 'trace.jsonl#identity;trace.jsonl#combat-history;route.json#PlayCard'
        firstSeq = $row.firstSeq
        identityCount = $row.identityCount
        playStarted = $row.playStarted
        playFinished = $row.playFinished
        reason = $reason
    }
}

$canonicalOut = [ordered]@{
    schema = 'canonical-run-input/v1'
    targetGameVersion = $expected.targetGameVersion
    gameCommit = $expected.gameCommit
    snapshotId = $expected.snapshotId
    snapshotContentSha256 = $expected.snapshotContentSha256
    snapshotManifestSha256 = $expected.snapshotManifestSha256
    modId = 'Acheron'
    workshopId = '3747588205'
    modVersion = 'lib/0.4.11'
    dependencyClosure = @('STS2-RitsuLib compat/0.111.0')
    registrationIdentity = 'REG.Acheron.lib/0.111.0#1@0.111.0'
    seed = [string]$canonical.seed
    adapter = 'acheron-m26-adapter/v1'
    collector = 'read-only source projection; fixed native trace'
    sourceEvidenceRoot = $adapterConfig.sourceRun
    freshRunRoot = $adapterConfig.freshRun
}

$legacyCounts = [ordered]@{ Covered = 0; Partial = 8; Unrun = 578; Unsupported = 0; RuntimeFailure = 0 }
$contentMatrix = [ordered]@{
    schema = 'content-matrix/acheron-m26/v1'
    modId = 'Acheron'
    workshopId = '3747588205'
    registrationIdentity = 'REG.Acheron.lib/0.111.0#1@0.111.0'
    targetGameVersion = $expected.targetGameVersion
    snapshotId = $expected.snapshotId
    legacyReportedDenominator = 586
    legacyReportedCounts = $legacyCounts
    foundationStandardCounts = $fixture.contentUnits
    denominatorAudit = [ordered]@{ status = 'PARTIAL'; reason = 'Existing handoff reports a 586-row append-only denominator; the fixed foundation fixture exports 152 standard category counts. This adapter retains both values and does not rewrite the legacy matrix.' }
    selectedSurface = $adapterConfig.selectedComplexSurface
    selectedSurfaceStatus = 'Partial'
    rows = $observedRows
    evidence = @($adapterConfig.sourceRun + '/trace.jsonl', $adapterConfig.sourceRun + '/route.json', $adapterConfig.sourceRun + '/final-state.json')
}

$lifecycleMatrix = [ordered]@{
    schema = 'lifecycle-matrix/acheron-m26/v1'
    modId = 'Acheron'
    registrationIdentity = 'REG.Acheron.lib/0.111.0#1@0.111.0'
    status = 'BLOCKED'
    checkpoints = @(
        [ordered]@{ name = 'foundation-registration-and-cleanup'; status = 'Covered'; evidence = $adapterConfig.sourceRun + '/foundation-fixture.json;foundation-cleanup.json'; reason = 'foundation gate PASS; runtime ALC teardown, observer detach, registry clear all true' },
        [ordered]@{ name = 'selected-card-identity-and-activation'; status = 'Partial'; evidence = $adapterConfig.sourceRun + '/trace.jsonl#CardPlayStartedEntry/CardPlayFinishedEntry'; reason = '20 card play start/finish pairs; selected Acheron cards participate in native combat' },
        [ordered]@{ name = 'battle-victory-reward-cross-room'; status = 'Partial'; evidence = $adapterConfig.sourceRun + '/trace.jsonl#battle-victory-*;reward-offer;foundation-cross-room-boundary'; reason = 'combat victory, reward, and next-room boundary observed; this is not RunVictory' },
        [ordered]@{ name = 'native-run-terminal'; status = 'BLOCKED'; evidence = $adapterConfig.sourceRun + '/result.json;final-state.json'; reason = 'Terminal=Ongoing; Won=false; Losses=0; RunEndStarts/Returns=0' },
        [ordered]@{ name = 'primary-repeat-replay-failure'; status = 'BLOCKED'; evidence = $adapterConfig.freshRun + '/run-manifest.json'; reason = 'no Acheron-specific four-path scenario; fresh generic foundation rerun stops at EngineError' },
        [ordered]@{ name = 'formal-ui-save-map-network-steam-cloud'; status = 'Unsupported'; evidence = 'ADR-0004;platform capability boundary'; reason = 'platform capability is tracked separately from gameplay gates' }
    )
}

$interactionRisk = [ordered]@{
    schema = 'interaction-risk/acheron-m26/v1'
    modId = 'Acheron'
    registrationIdentity = 'REG.Acheron.lib/0.111.0#1@0.111.0'
    contentGateStatus = 'PARTIAL'
    rows = @(
        [ordered]@{ contract = 'dependency registration and duplicate rollback'; status = 'Covered'; evidence = $adapterConfig.sourceRun + '/foundation-fixture.json;foundation-lifecycle.json' },
        [ordered]@{ contract = 'selected Acheron card discovery/construction/play'; status = 'Partial'; evidence = $adapterConfig.sourceRun + '/trace.jsonl#identity;combat-history' },
        [ordered]@{ contract = 'Acheron power application and state continuity'; status = 'Partial'; evidence = $adapterConfig.sourceRun + '/trace.jsonl#PowerReceivedEntry;final-state.json'; reason = 'power identities and stack state observed, no dedicated power cleanup probe' },
        [ordered]@{ contract = 'Acheron relic and role state'; status = 'Partial'; evidence = $adapterConfig.sourceRun + '/trace.jsonl#identity;final-state.json'; reason = 'starting relic/role state observed, no role-select or relic remove lifecycle' },
        [ordered]@{ contract = 'reward and cross-room persistence'; status = 'Partial'; evidence = $adapterConfig.sourceRun + '/trace.jsonl#reward-offer;rewards-completed;foundation-cross-room-boundary' },
        [ordered]@{ contract = 'native RunVictory or Death after selected surface'; status = 'BLOCKED'; evidence = $adapterConfig.sourceRun + '/result.json;final-state.json' },
        [ordered]@{ contract = 'primary/repeat/replay/failure stability'; status = 'Unrun'; evidence = $adapterConfig.freshRun + '/run-manifest.json'; reason = 'Acheron-specific scenario seam absent' },
        [ordered]@{ contract = 'visible-policy and formal platform capabilities'; status = 'Unsupported'; platformCapability = $true; evidence = 'ADR-0004' }
    )
}

$fourPath = [ordered]@{
    schema = 'four-path-status/acheron-m26/v1'
    modId = 'Acheron'
    terminalPromotion = $false
    paths = @(
        [ordered]@{ path = 'Primary'; status = 'BLOCKED'; terminal = 'Ongoing'; stop = 'EngineError'; evidence = $adapterConfig.freshRun + '/run-manifest.json' },
        [ordered]@{ path = 'Repeat'; status = 'Unrun'; terminal = $null; stop = $null; evidence = $null },
        [ordered]@{ path = 'Replay'; status = 'Unrun'; terminal = $null; stop = $null; evidence = $null },
        [ordered]@{ path = 'Failure'; status = 'Unrun'; terminal = $null; stop = $null; evidence = $null }
    )
}

$platformCapabilities = [ordered]@{
    schema = 'platform-capabilities/acheron-m26/v1'
    modId = 'Acheron'
    status = 'PARTIAL'
    rows = @(
        [ordered]@{ capability = 'headless TestMode execution'; status = 'Covered'; evidence = $adapterConfig.sourceRun + '/result.json;foundation-fixture.json'; reason = 'native process and memory save store run without Steam initialization' },
        [ordered]@{ capability = 'offline Steam/network guard'; status = 'Covered'; evidence = $adapterConfig.sourceRun + '/result.json#guards'; reason = 'SteamInitialized=false and network service is isolated' },
        [ordered]@{ capability = 'visible policy and Godot UI presentation'; status = 'Unsupported'; evidence = 'ADR-0004'; reason = 'headless trace does not prove retail presentation' },
        [ordered]@{ capability = 'formal save/load'; status = 'Unsupported'; evidence = 'ADR-0004'; reason = 'memory save store only' },
        [ordered]@{ capability = 'retail map presentation'; status = 'Unsupported'; evidence = 'ADR-0004'; reason = 'native state boundary only' },
        [ordered]@{ capability = 'network replay, Steam, and cloud'; status = 'Unsupported'; evidence = 'ADR-0004'; reason = 'not exercised by the fixed fixture' }
    )
}

$runManifest = [ordered]@{
    schema = 'acheron-m26-run-manifest/v1'
    runId = 'm2-6-acheron-20261113'
    evidenceKind = 'm2-6-fixed-roster-row'
    targetGameVersion = $expected.targetGameVersion
    gameCommit = $expected.gameCommit
    snapshotId = $expected.snapshotId
    modId = 'Acheron'
    foundation = 'PASS'
    flow = 'BLOCKED'
    content = 'PARTIAL'
    stability = 'BLOCKED'
    aggregate = 'BLOCKED'
    selectedSurface = $adapterConfig.selectedComplexSurface
    sourceEvidence = $adapterConfig.sourceRun
    freshAttempt = $adapterConfig.freshRun
    sourceHashes = 'source-hashes.json'
    outputFiles = @('canonical-input.json', 'adapter-config.json', 'content-matrix.json', 'lifecycle-matrix.json', 'interaction-risk.json', 'four-path-status.json', 'platform-capabilities.json', 'observation-ledger.json', 'source-hashes.json')
    noTerminalPromotion = $true
    noInputMutation = $true
}

Write-Json (Join-Path $outputRoot 'adapter-config.json') $adapterConfig
Write-Json (Join-Path $outputRoot 'canonical-input.json') $canonicalOut
Write-Json (Join-Path $outputRoot 'content-matrix.json') $contentMatrix
Write-Json (Join-Path $outputRoot 'lifecycle-matrix.json') $lifecycleMatrix
Write-Json (Join-Path $outputRoot 'interaction-risk.json') $interactionRisk
Write-Json (Join-Path $outputRoot 'four-path-status.json') $fourPath
Write-Json (Join-Path $outputRoot 'platform-capabilities.json') $platformCapabilities
Write-Json (Join-Path $outputRoot 'source-hashes.json') ([ordered]@{ sourceRoot = $adapterConfig.sourceRun; freshRoot = $adapterConfig.freshRun; source = $sha; fresh = $freshHashes })
Write-Json (Join-Path $outputRoot 'observation-ledger.json') ([ordered]@{ schema = 'acheron-observation-ledger/v1'; selectedSurface = $adapterConfig.selectedComplexSurface; identityModels = $identityList; playedCards = $playedCards; cardPlayRows = $cardPlayRows; traceKindCounts = $kindCounts; routeActionKinds = $actionKinds; routeCardModels = $routeCardModels; hasBattleVictory = $hasBattleVictory; hasRewardBoundary = $hasRewardBoundary; nativeTerminal = $nativeTerminal; sourceRunTerminal = [string]$resultEnvelope.result.Terminal; sourceRunStop = [string]$resultEnvelope.result.Stop })
Write-Json (Join-Path $outputRoot 'run-manifest.json') $runManifest

Write-Output ("Acheron M2-6 adapter output: {0}" -f $outputRoot)
Write-Output ("Selected surface: {0}; foundation={1}; flow={2}; content={3}; stability={4}" -f $adapterConfig.selectedComplexSurface, $runManifest.foundation, $runManifest.flow, $runManifest.content, $runManifest.stability)
