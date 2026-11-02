param(
    [ValidateSet('Mentor','Reverse')][string]$Form = 'Reverse',
    [Parameter(Mandatory)][string]$PinnedPlan
)

$ErrorActionPreference = 'Stop'
$runner = Join-Path $PSScriptRoot 'run-v0111-eleanor-v040.ps1'
& pwsh -NoProfile -File $runner `
    -PinnedModDirectory 'eleanor-v0.4.1-forms-cardpool.fix' `
    -ExpectedModVersion 'v0.4.1-forms-cardpool.fix' `
    -VictoryReplayPlanPath $PinnedPlan `
    -VictoryReplayForm $Form
exit $LASTEXITCODE
