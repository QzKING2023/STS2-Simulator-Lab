#requires -Version 7.0

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$logPath = Join-Path $repoRoot '.build\cleanup-workspace-scheduled.log'
$cleanup = Join-Path $PSScriptRoot 'cleanup-workspace.ps1'
$pwsh = Join-Path $PSHOME 'pwsh.exe'

try {
    New-Item -ItemType Directory -Path (Split-Path -Parent $logPath) -Force | Out-Null
    & $pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $cleanup -Apply -RetentionDays 14 -Confirm:$false *> $logPath
    exit $LASTEXITCODE
}
catch {
    $_ | Out-String | Set-Content -LiteralPath $logPath -Encoding utf8
    exit 1
}
