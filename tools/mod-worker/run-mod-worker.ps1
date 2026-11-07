[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InputFile,
    [Parameter(Mandatory)][string]$BuildManifest,
    [string]$Seed = 'LAB_V0111_MOD_WORKER_01',
    [string]$ReplayPath = '',
    [ValidateRange(1,480)][int]$ProcessTimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
$lab = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$inputPath = [IO.Path]::GetFullPath($InputFile)
$buildPath = [IO.Path]::GetFullPath($BuildManifest)
$runsRoot = [IO.Path]::GetFullPath((Join-Path $lab '.build/runs'))
$workerProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'ModWorker.csproj'))
$workerDll = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'bin/Release/net9.0/SimulatorLab.ModWorker.dll'))

if (!(Test-Path -LiteralPath $inputPath -PathType Leaf)) { throw "Worker input is missing: $inputPath" }
if (!(Test-Path -LiteralPath $buildPath -PathType Leaf)) { throw "Build manifest is missing: $buildPath" }
$buildJson = Get-Content -LiteralPath $buildPath -Raw | ConvertFrom-Json
if (!$buildJson.buildId -and !$buildJson.manifest.buildId) { throw "Build manifest has no buildId: $buildPath" }

# Bind the existing NativeRun build manifest before creating a worker. This
# keeps the worker's approved input chain tied to the same build identity while
# avoiding any read of the live Workshop directory.
foreach ($section in @('source','inputs')) {
    foreach ($row in @($buildJson.manifest.$section)) {
        if ($row.path -and !(Test-Path -LiteralPath ([IO.Path]::GetFullPath([string]$row.path)) -PathType Leaf)) {
            throw "Build manifest input is missing ($section): $($row.path)"
        }
        if ($row.path -and $row.sha256) {
            $observed = (Get-FileHash -LiteralPath ([IO.Path]::GetFullPath([string]$row.path)) -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($observed -ne ([string]$row.sha256).ToLowerInvariant()) { throw "Build manifest input hash changed ($section): $($row.path)" }
        }
    }
}

New-Item -ItemType Directory -Path $runsRoot -Force | Out-Null
$runRoot = Join-Path $runsRoot ('mod-worker-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '-' + [Guid]::NewGuid().ToString('N'))
if (Test-Path -LiteralPath $runRoot) { throw "Refusing to overwrite worker run directory: $runRoot" }

if (!(Test-Path -LiteralPath $workerDll -PathType Leaf)) {
    $buildOutput = @(& dotnet build $workerProject -c Release --nologo --ignore-failed-sources -p:NuGetAudit=false 2>&1)
    if ($LASTEXITCODE -ne 0) { $buildOutput | ForEach-Object { Write-Error $_ }; throw "Mod worker build failed" }
}

$psi = [Diagnostics.ProcessStartInfo]::new('dotnet')
$psi.UseShellExecute = $false
$psi.CreateNoWindow = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$arguments = [Collections.Generic.List[string]]::new()
foreach ($argument in @($workerDll, '--input', $inputPath, '--output', $runRoot, '--build-manifest', $buildPath, '--seed', $Seed)) { [void]$arguments.Add([string]$argument) }
if ($ReplayPath) { [void]$arguments.Add('--replay'); [void]$arguments.Add([IO.Path]::GetFullPath($ReplayPath)) }
$psi.Arguments = (($arguments | ForEach-Object { '"' + ([string]$_).Replace('"', '\"') + '"' }) -join ' ')

$child = [Diagnostics.Process]::new()
$child.StartInfo = $psi
$startedUtc = [DateTime]::UtcNow
[void]$child.Start()
$stdoutTask = $child.StandardOutput.ReadToEndAsync()
$stderrTask = $child.StandardError.ReadToEndAsync()
if (!$child.WaitForExit($ProcessTimeoutSeconds * 1000)) {
    $child.Kill($true)
    $stdoutTask.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $runRoot 'launcher.stdout.log') -Encoding utf8
    $stderrTask.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $runRoot 'launcher.stderr.log') -Encoding utf8
    throw "Mod worker exceeded timeout; run directory retained for review: $runRoot"
}
$stdoutTask.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $runRoot 'launcher.stdout.log') -Encoding utf8
$stderrTask.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $runRoot 'launcher.stderr.log') -Encoding utf8
$exitCode = $child.ExitCode

$runManifestPath = Join-Path $runRoot 'run-manifest.json'
if (!(Test-Path -LiteralPath $runManifestPath -PathType Leaf)) { throw "Worker exited without run-manifest.json (exit=$exitCode): $runRoot" }
$manifest = Get-Content -LiteralPath $runManifestPath -Raw | ConvertFrom-Json
if ([int]$manifest.exitCode -ne $exitCode) { throw "Worker exit and run manifest disagree (process=$exitCode manifest=$($manifest.exitCode))" }
if ($manifest.status -eq 'succeeded' -and $exitCode -ne 0) { throw "Worker reported success with a non-zero process exit" }

Write-Output ("MOD_WORKER_RUN={0}" -f $runRoot)
Write-Output ("MOD_WORKER_EXIT={0}" -f $exitCode)
Write-Output ("MOD_WORKER_STARTED_UTC={0}" -f $startedUtc.ToString('o'))
exit $exitCode
