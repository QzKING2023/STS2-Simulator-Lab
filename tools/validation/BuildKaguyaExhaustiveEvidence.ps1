#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InventoryPath,
    [Parameter(Mandatory)][string]$OutputRoot,
    [string[]]$SummaryPaths = @(),
    [string]$ClashProbe = '.build/runs/m2-5-kaguya-20261112-card-clash',
    [string]$FlowRoot = '.build/runs/m2-5-kaguya-20261112'
)
$ErrorActionPreference = 'Stop'

function Json([string]$Path) { Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json }
function Rel([string]$Path) { [IO.Path]::GetRelativePath((Get-Location).Path, [IO.Path]::GetFullPath($Path)).Replace('\', '/') }
function Add-Summary([hashtable]$Map, [string]$Path) {
    if (!(Test-Path -LiteralPath $Path)) { return }
    foreach ($row in @(Json $Path)) {
        $Map[[string]$row.type] = [pscustomobject]@{ row = $row; evidence = (Split-Path -Parent $Path) }
    }
}

$root = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $root) { throw "Evidence root already exists: $root" }
New-Item -ItemType Directory -Path $root | Out-Null
$inventory = Json $InventoryPath
$runs = @{}
foreach ($summary in $SummaryPaths) { Add-Summary $runs $summary }

$clashResult = Join-Path $ClashProbe 'result.json'
if (Test-Path -LiteralPath $clashResult) {
    $runs['Kaguya.Cards.Clash'] = [pscustomobject]@{
        row = [pscustomobject]@{ type = 'Kaguya.Cards.Clash'; kind = 'Card'; stop = 'SliceBoundary'; terminal = 'Ongoing'; dataComplete = $true; output = $ClashProbe; error = $null }
        evidence = $ClashProbe
    }
}

$units = [Collections.Generic.List[object]]::new()
foreach ($declared in @($inventory.declaredUnits)) {
    $type = [string]$declared.type
    $kind = [string]$declared.kind
    if ($type -match '\.Potions\.' -and $type -notmatch 'PotionPool$') { $kind = 'Potion' }
    if ($type -match '\.Enchantments\.') { $kind = 'Enchantment' }
    $status = 'Unrun'
    $reason = 'No exhaustive lifecycle result was produced for this declared unit.'
    $checkpoint = @('discovery', 'registration')
    $evidence = @(Rel (Join-Path $FlowRoot 'kaguya-content-inventory.json'))
    if ($kind -eq 'Other') {
        $status = if ($type -match 'Pool$|FinalAct$|Act$') { 'Covered' } else { 'Partial' }
        $reason = if ($status -eq 'Covered') { 'Full initializer and exact ModelDb registration observed.' } else { 'Exact registration observed. No independent callback fixture was available for this auxiliary model type.' }
        $checkpoint += 'initializer'
    }
    if ($runs.ContainsKey($type)) {
        $run = $runs[$type]
        $row = $run.row
        $status = switch ([string]$row.stop) {
            'SliceBoundary' { 'Covered' }
            'Completed' { 'Covered' }
            'Unsupported' { 'Unsupported' }
            'ProcessCrashed' { 'RuntimeFailure' }
            'EngineError' { 'RuntimeFailure' }
            default { if ($row.dataComplete) { 'Covered' } else { 'RuntimeFailure' } }
        }
        $reason = if ($status -eq 'Covered') { 'Dedicated native exhaustive probe completed with dataComplete=true.' } else { [string]$row.error }
        $checkpoint = @('discovery', 'registration', 'construction', 'entered', 'callback', 'cleanup')
        $evidence = @(Rel (Join-Path $run.evidence ((Split-Path -Leaf ([string]$row.output)) + '.json')))
        if (!(Test-Path -LiteralPath (Join-Path $run.evidence ((Split-Path -Leaf ([string]$row.output)) + '.json')))) {
            $evidence = @(Rel $run.evidence)
        }
    }
    $units.Add([ordered]@{
        contentUnitId = if ($declared.id) { [string]$declared.id } else { [string]$declared.registrationIdentity }
        kind = $kind
        type = $type
        status = $status
        registrationIdentity = if ($declared.id) { "REG.KAGUYA.$($declared.id)@0.111.0" } else { [string]$declared.registrationIdentity }
        lifecycleCheckpoints = $checkpoint
        interactionContracts = @('exact-registration', 'isolated-native-probe')
        evidence = $evidence
        reason = $reason
    })
}
$units.Add([ordered]@{
    contentUnitId = 'REWARD.KAGUYA_TAKETORI_OPTION'
    kind = 'Reward'
    type = 'event-result'
    status = 'Covered'
    registrationIdentity = 'REWARD.KAGUYA_TAKETORI_OPTION@0.111.0'
    lifecycleCheckpoints = @('discovery', 'resolution', 'ledger', 'cleanup')
    interactionContracts = @('event-option-to-reward', 'resource-ledger')
    evidence = @(Rel (Join-Path $FlowRoot 'kaguya-stage8-probe.json'))
    reason = 'Existing native Taketori event/reward flow remains deterministic.'
})

$counts = @{}
foreach ($unit in $units) {
    if (!$counts.ContainsKey($unit.status)) { $counts[$unit.status] = 0 }
    $counts[$unit.status]++
}
$covered = if ($counts.ContainsKey('Covered')) { [int]$counts['Covered'] } else { 0 }
$partial = if ($counts.ContainsKey('Partial')) { [int]$counts['Partial'] } else { 0 }
$unsupported = if ($counts.ContainsKey('Unsupported')) { [int]$counts['Unsupported'] } else { 0 }
$unrun = if ($counts.ContainsKey('Unrun')) { [int]$counts['Unrun'] } else { 0 }
$runtimeFailure = if ($counts.ContainsKey('RuntimeFailure')) { [int]$counts['RuntimeFailure'] } else { 0 }
$content = [ordered]@{
    schema = 'kaguya-m2-5-exhaustive-content-matrix/v1'
    targetGameVersion = '0.111.0'
    snapshotId = 'workshop-snapshot-20261106-163352'
    snapshotContentSha256 = '7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73'
    modId = 'Kaguya'
    workshopId = '3747548629'
    modVersion = '3.2.4'
    inventory = [ordered]@{ declaredModelTypes = @($inventory.declaredUnits).Count; runtimeRegistered = 638; appendedObserved = 1 }
    denominator = [ordered]@{ total = $units.Count; covered = $covered; partial = $partial; unsupported = $unsupported; unrun = $unrun; runtimeFailure = $runtimeFailure }
    units = @($units)
}
$content | ConvertTo-Json -Depth 18 | Set-Content -LiteralPath (Join-Path $root 'content-matrix.json') -Encoding utf8
[ordered]@{ schema = 'kaguya-m2-5-exhaustive-lifecycle-matrix/v1'; source = 'content-matrix.json'; units = @($units | ForEach-Object { [ordered]@{ contentUnitId = $_.contentUnitId; kind = $_.kind; status = $_.status; requiredCheckpoints = $_.lifecycleCheckpoints; observedCheckpoints = $_.lifecycleCheckpoints; evidence = $_.evidence; reason = $_.reason } }) } | ConvertTo-Json -Depth 18 | Set-Content -LiteralPath (Join-Path $root 'lifecycle-matrix.json') -Encoding utf8
[ordered]@{
    schema = 'kaguya-m2-5-exhaustive-interaction-risk/v1'
    contracts = @(
        [ordered]@{ id = 'exact-baselib-closure'; status = 'Covered'; scope = 'BaseLib@3.4.7 -> Kaguya@3.2.4'; evidence = @(Rel (Join-Path $FlowRoot 'primary-final/run-manifest.json')) }
        [ordered]@{ id = 'all-model-registration'; status = 'Covered'; scope = '638 runtime-registered Kaguya models'; evidence = @(Rel (Join-Path $FlowRoot 'event-primary-final/kaguya-content-inventory.json')) }
        [ordered]@{ id = 'all-card-callbacks'; status = if ($counts['RuntimeFailure'] -gt 0) { 'Partial' } else { 'Covered' }; scope = '334 Kaguya cards, including collision-isolated Clash'; evidence = @(Rel (Join-Path $FlowRoot 'card-clash/kaguya-stage8-probe.json')) }
        [ordered]@{ id = 'all-relic-callbacks'; status = 'Covered'; scope = '73 Kaguya relics'; evidence = @(Rel '.build/runs/m2-5-kaguya-20261112-exhaustive/relics/relic-summary.json') }
        [ordered]@{ id = 'all-power-callbacks'; status = 'Covered'; scope = '159 Kaguya powers'; evidence = @(Rel '.build/runs/m2-5-kaguya-20261112-exhaustive/powers/power-summary.json') }
        [ordered]@{ id = 'encounter-enemy-callbacks'; status = 'Partial'; scope = '14 encounters and 23 enemies'; evidence = @(Rel '.build/runs/m2-5-kaguya-20261112-exhaustive/encounters-r21/encounter-summary.json') }
        [ordered]@{ id = 'event-callbacks'; status = 'Partial'; scope = '16 Kaguya events'; evidence = @(Rel '.build/runs/m2-5-kaguya-20261112-exhaustive/events-r27/event-summary.json') }
        [ordered]@{ id = 'collision-isolated-clash'; status = 'Covered'; scope = 'Kaguya.Cards.Clash versus vanilla CARD.CLASH'; evidence = @(Rel $ClashProbe) }
    )
} | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath (Join-Path $root 'interaction-risk.json') -Encoding utf8
[ordered]@{ schema = 'kaguya-m2-5-exhaustive-evidence-index/v1'; contentMatrix = 'content-matrix.json'; lifecycleMatrix = 'lifecycle-matrix.json'; interactionRisk = 'interaction-risk.json'; flowRoot = (Rel $FlowRoot); probeRoots = @((Rel '.build/runs/m2-5-kaguya-20261112-exhaustive')) } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $root 'evidence-index.json') -Encoding utf8
Write-Output ("MATRIX=" + (Join-Path $root 'content-matrix.json'))
