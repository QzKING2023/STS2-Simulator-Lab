#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BuildManifest,
    [Parameter(Mandatory)][string]$InventoryPath,
    [Parameter(Mandatory)][string]$OutputRoot,
    [Parameter(Mandatory)][ValidateSet('Card','Relic','Power','Encounter','Enemy','Event','Character','Potion','Enchantment')][string]$Kind,
    [int]$Skip = 0,
    [int]$Count = 0,
    [string[]]$Types = @()
)
$ErrorActionPreference = 'Stop'

function Read-Json([string]$Path) { Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json }
function Safe([string]$Text) { $Text -replace '[^A-Za-z0-9_.-]', '_' }

$inventory = Read-Json $InventoryPath
$units = @($inventory.registeredUnits | Where-Object {
    [string]$_.kind -eq $Kind -or
    ($Kind -eq 'Potion' -and [string]$_.type -match '\.Potions\.' -and [string]$_.type -notmatch 'PotionPool$') -or
    ($Kind -eq 'Enchantment' -and [string]$_.type -match '\.Enchantments\.')
} | Sort-Object type)
if ($Types.Count -gt 0) { $units = @($units | Where-Object { $Types -contains [string]$_.type }) }
if ($Count -gt 0) { $units = @($units | Select-Object -Skip $Skip -First $Count) }
else { $units = @($units | Select-Object -Skip $Skip) }
$root = [IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Force -Path $root | Out-Null
$env:SIMULATORLAB_KAGUYA_STAGE8 = '1'
$env:SIMULATORLAB_KAGUYA_FULL_INIT = '1'
$env:SIMULATORLAB_KAGUYA_SNAPSHOT_ROOT = [IO.Path]::GetFullPath('local-mods/workshop-snapshot-20261106-163352')
$rows = [Collections.Generic.List[object]]::new()
foreach ($unit in $units) {
    $target = [string]$unit.type
    $leaf = Safe $target
    $out = Join-Path $root $leaf
    if (Test-Path -LiteralPath $out) { throw "Refusing to overwrite probe output: $out" }
    $scenario = "kaguya-$($Kind.ToLowerInvariant())-probe:$target"
    $seed = "LAB_M2_5_KAGUYA_$($Kind.ToUpperInvariant())_$leaf"
    $arguments = @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'tools/native-run/run-native-slice.ps1',
        '-BuildManifest', $BuildManifest, '-OutputDirectory', $out, '-Scenario', $scenario,
        '-RunKind', 'primary', '-Seed', $seed,
        '-SnapshotId', 'workshop-snapshot-20261106-163352',
        '-SnapshotContentSha256', '7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73',
        '-ModId', 'Kaguya', '-ModVersion', '3.2.4',
        '-DependencyClosure', 'BaseLib@3.4.7,Kaguya@3.2.4',
        '-WorkerIdentity', 'SimulatorLab.NativeLifecycleProbe/Kaguya-M2-5',
        '-Adapter', 'kaguya-exhaustive-content/v1',
        '-Collector', 'isolated NativeRun/GameProbe; native-trace/v1'
    )
    $driver = Join-Path $root "$leaf.driver.log"
    & pwsh @arguments *> $driver
    $exit = $LASTEXITCODE
    $resultPath = Join-Path $out 'result.json'
    $result = if (Test-Path -LiteralPath $resultPath) { Read-Json $resultPath } else { $null }
    $rows.Add([ordered]@{
        type = $target
        kind = $Kind
        exit = $exit
        terminal = if ($null -ne $result) { [string]$result.result.Terminal } else { 'Unknown' }
        stop = if ($null -ne $result) { [string]$result.result.Stop } else { 'ProcessCrashed' }
        dataComplete = if ($null -ne $result) { [bool]$result.result.DataComplete } else { $false }
        error = if ($null -ne $result) { [string]$result.result.Error } else { (Get-Content -LiteralPath $driver -Raw) }
        output = $out.Replace((Get-Location).Path + '\', '').Replace('\', '/')
    })
}
$summary = Join-Path $root "$($Kind.ToLowerInvariant())-summary.json"
$rows | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $summary -Encoding utf8
$rows | Group-Object stop | Sort-Object Name | Format-Table -AutoSize
Write-Output "SUMMARY=$summary"
