param(
    [string]$MatrixPath = '.build/runs/m2-6-fixed-roster-20261113/fixed-roster-matrix.json'
)

$ErrorActionPreference = 'Stop'
$matrix = Get-Content -Raw $MatrixPath | ConvertFrom-Json
$expected = @('Watcher','Downfall','figure_Saya','Remilia','Ryoshu','LexNinja2','Kaguya','Acheron','shadowversebydmod','Aemeath','St2bs','YukiMakoto')
$allowed = @('PASS','PARTIAL','BLOCKED','UNRUN','Unsupported','RuntimeFailure')

if ($matrix.schema -ne 'sts2-fixed-roster-dual-gate-matrix/v1') { throw "unexpected schema: $($matrix.schema)" }
if ($matrix.target.game_version -ne '0.111.0' -or $matrix.target.game_commit -ne '41cef1ea') { throw 'canonical game identity mismatch' }
if ($matrix.target.snapshot_id -ne 'workshop-snapshot-20261106-163352') { throw 'snapshot id mismatch' }
if ($matrix.rows.Count -ne 12 -or $matrix.target.denominator -ne 12) { throw "expected 12 fixed-roster rows, got $($matrix.rows.Count)" }

$actual = @($matrix.rows | ForEach-Object { $_.mod })
if (@(Compare-Object $expected $actual).Count -ne 0) { throw "fixed roster mismatch: $($actual -join ', ')" }

foreach ($row in $matrix.rows) {
    foreach ($field in @('foundation','flow','content','stability','platform','aggregate')) {
        if ($allowed -notcontains $row.$field) { throw "$($row.mod): invalid $field status '$($row.$field)'" }
    }
    if (-not (Test-Path $row.evidence_report)) { throw "$($row.mod): missing evidence report $($row.evidence_report)" }
    if ($row.aggregate -eq 'PASS' -and @($row.foundation,$row.flow,$row.content,$row.stability | Where-Object { $_ -ne 'PASS' }).Count -gt 0) {
        throw "$($row.mod): aggregate PASS with a non-PASS gate"
    }
}

if ($matrix.aggregate -eq 'PASS' -and @($matrix.rows | Where-Object { $_.aggregate -ne 'PASS' }).Count -gt 0) {
    throw 'aggregate PASS with non-PASS rows'
}

Write-Output "PASS M2-6 fixed roster matrix rows=$($matrix.rows.Count) aggregate=$($matrix.aggregate)"
