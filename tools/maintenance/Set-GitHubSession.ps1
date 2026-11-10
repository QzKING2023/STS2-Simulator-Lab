#requires -Version 7.0

[CmdletBinding()]
param(
    [switch]$Verify,
    [switch]$VerifyGit,
    [string]$Repository = 'QzKING2023/STS2-Simulator-Lab'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-UserEnvironmentValue {
    param([Parameter(Mandatory)][string]$Name)

    return [Environment]::GetEnvironmentVariable($Name, 'User')
}

function Set-SessionEnvironmentValue {
    param(
        [Parameter(Mandatory)][string]$Name,
        [AllowNull()][string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        Remove-Item -LiteralPath ("Env:{0}" -f $Name) -ErrorAction SilentlyContinue
        return
    }

    Set-Item -LiteralPath ("Env:{0}" -f $Name) -Value $Value
}

$userHttpProxy = Get-UserEnvironmentValue -Name 'HTTP_PROXY'
$userHttpsProxy = Get-UserEnvironmentValue -Name 'HTTPS_PROXY'
$proxy = if (-not [string]::IsNullOrWhiteSpace($userHttpsProxy)) { $userHttpsProxy } else { $userHttpProxy }

if ([string]::IsNullOrWhiteSpace($proxy)) {
    throw 'No user-level HTTP(S)_PROXY is configured. Start the local proxy or set HTTPS_PROXY for the current Windows user before using GitHub CLI.'
}

try {
    $proxyUri = [Uri]$proxy
}
catch {
    throw ("The user-level proxy value is not a valid URI: {0}" -f $proxy)
}

if (-not $proxyUri.IsAbsoluteUri -or [string]::IsNullOrWhiteSpace($proxyUri.Host)) {
    throw ("The user-level proxy value is not an absolute URI: {0}" -f $proxy)
}

Set-SessionEnvironmentValue -Name 'HTTP_PROXY' -Value $(if ([string]::IsNullOrWhiteSpace($userHttpProxy)) { $proxy } else { $userHttpProxy })
Set-SessionEnvironmentValue -Name 'HTTPS_PROXY' -Value $(if ([string]::IsNullOrWhiteSpace($userHttpsProxy)) { $proxy } else { $userHttpsProxy })
Set-SessionEnvironmentValue -Name 'ALL_PROXY' -Value (Get-UserEnvironmentValue -Name 'ALL_PROXY')
Set-SessionEnvironmentValue -Name 'NO_PROXY' -Value (Get-UserEnvironmentValue -Name 'NO_PROXY')
Set-SessionEnvironmentValue -Name 'GIT_HTTP_PROXY' -Value $(if ([string]::IsNullOrWhiteSpace((Get-UserEnvironmentValue -Name 'GIT_HTTP_PROXY'))) { $proxy } else { Get-UserEnvironmentValue -Name 'GIT_HTTP_PROXY' })
Set-SessionEnvironmentValue -Name 'GIT_HTTPS_PROXY' -Value $(if ([string]::IsNullOrWhiteSpace((Get-UserEnvironmentValue -Name 'GIT_HTTPS_PROXY'))) { $proxy } else { Get-UserEnvironmentValue -Name 'GIT_HTTPS_PROXY' })
Set-SessionEnvironmentValue -Name 'GIT_SSH_COMMAND' -Value (Get-UserEnvironmentValue -Name 'GIT_SSH_COMMAND')

$gh = Get-Command gh.exe -ErrorAction Stop
$proxyDescription = '{0}:{1}' -f $proxyUri.Host, $proxyUri.Port
Write-Output ("GitHub session configured: proxy {0}; repository {1}" -f $proxyDescription, $Repository)

if (-not $Verify) {
    return
}

& $gh.Source auth status
if ($LASTEXITCODE -ne 0) {
    throw 'GitHub CLI authentication check failed. Re-authenticate with gh auth refresh -h github.com only after confirming the proxy is running.'
}

$repo = & $gh.Source api ("repos/{0}" -f $Repository) --jq '{full_name,default_branch,permissions,visibility}'
if ($LASTEXITCODE -ne 0) {
    throw ("GitHub repository permission check failed for {0}." -f $Repository)
}
Write-Output $repo

if (-not $VerifyGit) {
    return
}

$head = git ls-remote origin HEAD
if ($LASTEXITCODE -ne 0) {
    throw 'Git HTTPS access check failed for origin. Confirm that origin uses https://github.com and rerun this helper.'
}
Write-Output ("origin HEAD: {0}" -f $head)
