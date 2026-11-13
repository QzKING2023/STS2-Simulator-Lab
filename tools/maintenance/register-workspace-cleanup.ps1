#requires -Version 7.0

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [string]$TaskName = 'STS2 Simulator Lab - workspace cleanup',
    [ValidatePattern('^([01]\d|2[0-3]):[0-5]\d$')][string]$At = '03:30'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$taskRunner = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'run-workspace-cleanup-task.ps1'))
$pwsh = (Get-Command pwsh -ErrorAction Stop).Source
$userId = "$env:USERDOMAIN\$env:USERNAME"
$arguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "{0}"' -f $taskRunner

$action = New-ScheduledTaskAction -Execute $pwsh -Argument $arguments -WorkingDirectory $repoRoot
$trigger = New-ScheduledTaskTrigger -Daily -At $At
$principal = New-ScheduledTaskPrincipal -UserId $userId -LogonType Interactive -RunLevel Limited

if ($PSCmdlet.ShouldProcess($TaskName, 'Register daily workspace cleanup task')) {
    Register-ScheduledTask `
        -TaskName $TaskName `
        -Action $action `
        -Trigger $trigger `
        -Principal $principal `
        -Description 'Review-first cleanup of stale unreferenced STS2 Simulator Lab build and staging output. Preserves evidence and pinned inputs.' `
        -Force | Out-Null
}

Get-ScheduledTask -TaskName $TaskName | Select-Object TaskName, State, TaskPath
