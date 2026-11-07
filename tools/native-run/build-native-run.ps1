#requires -Version 7.0

<#
.SYNOPSIS
Builds the SimulatorLab native host under the minimal storage policy in
docs/NATIVE_BUILD_STORAGE_POLICY.md.

.DESCRIPTION
A normal build keeps only what later stages need: the runtime output under
bin/Release/net9.0, the hash-bound compiled sources copied into the build
root, build-manifest.json (verified by run-native-slice.ps1), the small
run-manifest.json metadata file, run.log (the single detailed log) and the
ready marker (.ready). The regenerable obj/ tree is pruned before the
output is published, and no evidence snapshot, compiled copy or duplicate
log is retained. A full evidence package (source-snapshot/, compiled/,
evidence-manifest.json) is produced only when -CaptureEvidence is passed
explicitly; it is staged, hash-verified and published manifest-last.

The build stage receives no seed. Seeds belong to the run stage
(run-native-slice.ps1 -Seed); this script records seed=not-supplied and the
actual seed source instead of inventing a value.

.PARAMETER GamePath
Pinned game root containing data_sts2_windows_x86_64 and release_info.json.

.PARAMETER HostSource
Prepared local upstream host source directory (see docs/M1_FINAL_ACCEPTANCE.md).

.PARAMETER OutputRoot
Optional explicit output directory. Empty or whitespace builds into the
stable content-addressed cache (.build/native-build/cache/<contentKey>). An
explicit value must resolve under the Lab root, must NOT already exist, and is
never silently rewritten to a different directory: content is built in a
staging sibling and atomically moved into place. Relative paths resolve
against the current working directory and must still land under the Lab root.
Out-of-Lab and existing directories are rejected.

.PARAMETER SolverSnapshot
Optional solver snapshot directory holding solver-lock.json
(schema solver-compute-inputs/v1).

.PARAMETER ForeseerSnapshot
Optional foreseer snapshot directory holding input-lock.json
(schema foreseer-source-reuse/v1).

.PARAMETER CaptureEvidence
Off by default. When set, captures the full evidence package
(source-snapshot/, compiled/, evidence-manifest.json with schema
native-build-evidence/v2) into the build output.

.PARAMETER DryRun
Prints the planned output, output-root rule, content key/cache state, version
summary, log format version, seed policy and the planned retention/prune set
without creating any file.

.PARAMETER Cleanup
Cache maintenance review: lists ready cache entries past the retention cutoff
and stale interrupted .staging-* directories. Deletes nothing by itself.

.PARAMETER ApplyCleanup
Requires -Cleanup; applies the reviewed removals.

.PARAMETER RetentionDays
Retention window in days for -Cleanup (default 14, UTC mtimes).

.NOTES
Script version: native-build-run/v3. Log format: native-build-run-log/v1.
Storage policy: docs/NATIVE_BUILD_STORAGE_POLICY.md.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GamePath,
    [Parameter(Mandatory)][string]$HostSource,
    [string]$OutputRoot = '',
    [string]$SolverSnapshot = '',
    [string]$ForeseerSnapshot = '',
    [switch]$CaptureEvidence,
    [switch]$DryRun,
    [switch]$Cleanup,
    [switch]$ApplyCleanup,
    [ValidateRange(1, 3650)][int]$RetentionDays = 14
)

$ErrorActionPreference = 'Stop'
$scriptVersion = 'native-build-run/v3'
$logFormat = 'native-build-run-log/v1'
$labRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$nativeRoot = [IO.Path]::GetFullPath((Join-Path $labRoot '.build/native-build'))
$cacheRoot = [IO.Path]::GetFullPath((Join-Path $nativeRoot 'cache'))
$runStartedUtc = [DateTime]::UtcNow
$script:logLines = [System.Collections.Generic.List[string]]::new()
$failurePhase = 'startup'
$dotnetStderr = @()
$dotnetExitCode = 'not-executed'

function Write-Status {
    param([Parameter(Mandatory)][string]$Message)

    $line = '[{0}] {1}' -f [DateTime]::UtcNow.ToString('o'), $Message
    $script:logLines.Add($line)
    Write-Output $line
}

function Add-RawLogLines {
    param([string[]]$Lines = @())

    # Raw (untimestamped) lines, e.g. captured dotnet output, still belong in
    # the single detailed log and on the console exactly once.
    foreach ($line in $Lines) {
        $script:logLines.Add($line)
        Write-Output $line
    }
}

function Get-Sha256Text {
    param([Parameter(Mandatory)][string]$Text)

    return [Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Text))
    )
}

function Get-FileSha256 {
    param([Parameter(Mandatory)][string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Write-AtomicText {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Text
    )

    $parent = Split-Path -Parent $Path
    if (!(Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    $temporary = '{0}.tmp-{1}' -f $Path, [Guid]::NewGuid().ToString('N')
    try {
        [IO.File]::WriteAllText($temporary, $Text, [Text.UTF8Encoding]::new($false))
        Move-Item -LiteralPath $temporary -Destination $Path -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporary) {
            Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue
        }
    }
}

function Write-AtomicJson {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)]$Value,
        [int]$Depth = 12
    )

    Write-AtomicText -Path $Path -Text ($Value | ConvertTo-Json -Depth $Depth)
}

function Test-ReparsePoint {
    param([Parameter(Mandatory)][IO.FileSystemInfo]$Item)

    return (($Item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
}

function Assert-UnderLab {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Description
    )

    $full = [IO.Path]::GetFullPath($Path)
    $prefix = $labRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (!$full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description must stay in Lab"
    }
    return $full
}

function Remove-ValidatedDirectory {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$RequiredParent,
        [Parameter(Mandatory)][string]$RequiredLeafName,
        [Parameter(Mandatory)][string]$Description
    )

    # Every deletion goes through this gate: an exact leaf name, an exact
    # direct-child relationship with the expected parent, a real directory
    # (never a reparse point) and a literal-path Remove-Item.
    $full = [IO.Path]::GetFullPath($Path)
    $parent = [IO.Path]::GetFullPath((Split-Path -Parent $full))
    if (![string]::Equals($parent, [IO.Path]::GetFullPath($RequiredParent), [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description failed the direct-parent check: $full"
    }
    if (![string]::Equals([IO.Path]::GetFileName($full), $RequiredLeafName, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description failed the leaf-name check: $full"
    }
    $item = Get-Item -LiteralPath $full -Force -ErrorAction Stop
    if (!($item -is [IO.DirectoryInfo]) -or (Test-ReparsePoint -Item $item)) {
        throw "$Description is not a deletable plain directory: $full"
    }
    Remove-Item -LiteralPath $full -Recurse -Force -ErrorAction Stop
}

function Get-OptionalGitValue {
    param([Parameter(Mandatory)][string[]]$Arguments)

    try {
        $value = (& git @Arguments 2>$null | Out-String).Trim()
        if ($LASTEXITCODE -eq 0 -and $value) {
            return $value
        }
    }
    catch {
        # Dry-run and detached source folders can be inspected without Git.
    }
    return 'unavailable'
}

function Get-ToolchainVersion {
    try {
        $command = Get-Command dotnet -ErrorAction Stop
        $value = (& $command.Source --version 2>$null | Out-String).Trim()
        if ($LASTEXITCODE -eq 0 -and $value) {
            return $value
        }
    }
    catch {
        # A dry-run should still report its planned cache key when the SDK is absent.
    }
    return 'unavailable'
}

function Get-SnapshotInfo {
    param(
        [Parameter(Mandatory)][string]$SnapshotPath,
        [Parameter(Mandatory)][ValidateSet('solver', 'foreseer')][string]$Kind
    )

    $info = [ordered]@{
        path       = $SnapshotPath
        version    = 'none'
        lockSha256 = 'none'
        lock       = $null
    }
    if ([string]::IsNullOrWhiteSpace($SnapshotPath)) {
        return [pscustomobject]$info
    }

    $full = [IO.Path]::GetFullPath($SnapshotPath)
    $info.path = $full
    $buildRoot = [IO.Path]::GetFullPath((Join-Path $labRoot '.build'))
    $buildPrefix = $buildRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (!$full.StartsWith($buildPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Kind snapshot must stay in Lab"
    }

    $lockName = if ($Kind -eq 'solver') { 'solver-lock.json' } else { 'input-lock.json' }
    $lockPath = Join-Path $full $lockName
    if (!(Test-Path -LiteralPath $lockPath -PathType Leaf)) {
        if ($DryRun) {
            $info.version = 'missing'
            return [pscustomobject]$info
        }
        throw "$Kind snapshot lock is missing: $lockPath"
    }

    $lockText = Get-Content -LiteralPath $lockPath -Raw
    $lock = $lockText | ConvertFrom-Json
    $info.lock = $lock
    $info.lockSha256 = Get-FileSha256 -Path $lockPath
    if ($Kind -eq 'solver') {
        if ($lock.schema -ne 'solver-compute-inputs/v1' -or $lock.version -ne '0.50.1') {
            throw 'Unexpected solver lock'
        }
    }
    else {
        if ($lock.schema -ne 'foreseer-source-reuse/v1' -or $lock.version -ne '0.13.19') {
            throw 'Unsupported Foreseer source lock'
        }
    }
    $info.version = [string]$lock.version
    return [pscustomobject]$info
}

function Get-ReadyMarker {
    param([Parameter(Mandatory)][string]$Root)

    $markerPath = Join-Path $Root '.ready'
    $marker = Get-Item -LiteralPath $markerPath -Force -ErrorAction SilentlyContinue
    if ($null -eq $marker -or !($marker -is [IO.FileInfo]) -or (Test-ReparsePoint -Item $marker)) {
        return $null
    }
    return $marker
}

function Test-ReadyOutput {
    param([Parameter(Mandatory)][string]$Root)

    $rootItem = Get-Item -LiteralPath $Root -Force -ErrorAction SilentlyContinue
    if ($null -eq $rootItem -or !($rootItem -is [IO.DirectoryInfo]) -or (Test-ReparsePoint -Item $rootItem)) {
        return $false
    }
    if ($null -eq (Get-ReadyMarker -Root $Root)) {
        return $false
    }
    foreach ($required in @('NativeRun.csproj', 'build-manifest.json', 'bin')) {
        $item = Get-Item -LiteralPath (Join-Path $Root $required) -Force -ErrorAction SilentlyContinue
        if ($null -eq $item -or (Test-ReparsePoint -Item $item)) {
            return $false
        }
    }
    return $true
}

function Get-DirectChildCacheDirectories {
    param([Parameter(Mandatory)][string]$Root)

    if (!(Test-Path -LiteralPath $Root -PathType Container)) {
        return @()
    }
    $rootItem = Get-Item -LiteralPath $Root -Force
    if (Test-ReparsePoint -Item $rootItem) {
        throw "Refusing to inspect a reparse-point cache root: $Root"
    }
    $options = [System.IO.EnumerationOptions]::new()
    $options.AttributesToSkip = [IO.FileAttributes]::None
    $options.IgnoreInaccessible = $false
    $options.RecurseSubdirectories = $false
    $options.ReturnSpecialDirectories = $false
    return @($rootItem.EnumerateDirectories('*', $options))
}

function Invoke-CacheCleanup {
    $cutoff = [DateTime]::UtcNow.AddDays(-$RetentionDays)
    Write-Status ("cache-cleanup mode={0} root={1} cutoffUtc={2:o}" -f ($(if ($ApplyCleanup) { 'APPLY' } else { 'DRY-RUN' }), $cacheRoot, $cutoff))
    if (!(Test-Path -LiteralPath $cacheRoot -PathType Container)) {
        Write-Status 'cache-cleanup status=NOOP reason=cache-root-missing'
        return
    }

    $candidates = 0
    $removed = 0
    foreach ($entry in Get-DirectChildCacheDirectories -Root $cacheRoot) {
        if ($entry.Name.StartsWith('.staging-', [StringComparison]::Ordinal)) {
            # Interrupted-build staging directories have no .ready marker by
            # design; the staging pass below handles them with its own rule.
            continue
        }
        if (Test-ReparsePoint -Item $entry) {
            Write-Status "cache-cleanup skip=$($entry.FullName) reason=reparse-point"
            continue
        }
        $ready = Get-ReadyMarker -Root $entry.FullName
        if ($null -eq $ready) {
            Write-Status "cache-cleanup skip=$($entry.FullName) reason=not-ready"
            continue
        }
        $runManifest = Get-Item -LiteralPath (Join-Path $entry.FullName 'run-manifest.json') -Force -ErrorAction SilentlyContinue
        $observedUtc = $ready.LastWriteTimeUtc
        if ($null -ne $runManifest -and $runManifest.LastWriteTimeUtc -gt $observedUtc) {
            $observedUtc = $runManifest.LastWriteTimeUtc
        }
        if ($observedUtc -gt $cutoff) {
            Write-Status ("cache-cleanup keep={0} newestMtimeUtc={1:o} reason=within-retention" -f $entry.FullName, $observedUtc)
            continue
        }
        $candidates++
        Write-Status ("cache-cleanup candidate={0} newestMtimeUtc={1:o}" -f $entry.FullName, $observedUtc)
        if ($ApplyCleanup) {
            $resolved = [IO.Path]::GetFullPath($entry.FullName)
            $parent = [IO.Path]::GetFullPath((Split-Path -Parent $resolved))
            if (![string]::Equals($parent, [IO.Path]::GetFullPath($cacheRoot), [StringComparison]::OrdinalIgnoreCase)) {
                throw "Cache cleanup target failed direct-child check: $resolved"
            }
            Remove-Item -LiteralPath $resolved -Recurse -Force
            $removed++
            Write-Status "cache-cleanup removed=$resolved"
        }
    }
    Write-Status ("cache-cleanup summary candidates={0} removed={1} mode={2}" -f $candidates, $removed, ($(if ($ApplyCleanup) { 'APPLY' } else { 'DRY-RUN' })))

    $stagingCandidates = 0
    $stagingRemoved = 0
    foreach ($entry in Get-DirectChildCacheDirectories -Root $cacheRoot) {
        if (!$entry.Name.StartsWith('.staging-', [StringComparison]::Ordinal)) {
            continue
        }
        if (Test-ReparsePoint -Item $entry) {
            Write-Status "staging-cleanup skip=$($entry.FullName) reason=reparse-point"
            continue
        }
        if ($entry.LastWriteTimeUtc -gt $cutoff) {
            Write-Status ("staging-cleanup keep={0} mtimeUtc={1:o} reason=within-retention" -f $entry.FullName, $entry.LastWriteTimeUtc)
            continue
        }
        $stagingCandidates++
        Write-Status ("staging-cleanup candidate={0} mtimeUtc={1:o}" -f $entry.FullName, $entry.LastWriteTimeUtc)
        if ($ApplyCleanup) {
            $resolved = [IO.Path]::GetFullPath($entry.FullName)
            $parent = [IO.Path]::GetFullPath((Split-Path -Parent $resolved))
            if (![string]::Equals($parent, [IO.Path]::GetFullPath($cacheRoot), [StringComparison]::OrdinalIgnoreCase) -or
                ![IO.Path]::GetFileName($resolved).StartsWith('.staging-', [StringComparison]::Ordinal)) {
                throw "Staging cleanup target failed the direct-child/name check: $resolved"
            }
            Remove-Item -LiteralPath $resolved -Recurse -Force
            $stagingRemoved++
            Write-Status "staging-cleanup removed=$resolved"
        }
    }
    Write-Status ("staging-cleanup summary candidates={0} removed={1} mode={2}" -f $stagingCandidates, $stagingRemoved, ($(if ($ApplyCleanup) { 'APPLY' } else { 'DRY-RUN' })))
}

if ($ApplyCleanup -and !$Cleanup) {
    throw '-ApplyCleanup requires -Cleanup.'
}
if ($Cleanup) {
    Invoke-CacheCleanup
    return
}

$failurePhase = 'input-validation'
$gameRoot = [IO.Path]::GetFullPath($GamePath)
$hostRoot = [IO.Path]::GetFullPath($HostSource)
$data = Join-Path $gameRoot 'data_sts2_windows_x86_64'
$gameAssemblyPath = Join-Path $data 'sts2.dll'
$expectedGameAssemblySha256 = '0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9'
$releasePath = Join-Path $gameRoot 'release_info.json'
$releaseVersion = 'missing'
$releaseCommit = 'missing'
$gameAssemblySha256 = 'missing'

if (Test-Path -LiteralPath $gameAssemblyPath -PathType Leaf) {
    $gameAssemblySha256 = Get-FileSha256 -Path $gameAssemblyPath
    if (!$DryRun -and $gameAssemblySha256 -ne $expectedGameAssemblySha256) {
        throw 'Pinned game assembly mismatch'
    }
}
elseif (!$DryRun) {
    throw "Pinned game assembly is missing: $gameAssemblyPath"
}

if (Test-Path -LiteralPath $releasePath -PathType Leaf) {
    $release = Get-Content -LiteralPath $releasePath -Raw | ConvertFrom-Json
    $releaseVersion = [string]$release.version
    $releaseCommit = [string]$release.commit
    if (!$DryRun -and ($releaseVersion -ne 'v0.111.0' -or $releaseCommit -ne '41cef1ea')) {
        throw 'Pinned release mismatch'
    }
}
elseif (!$DryRun) {
    throw "Pinned release metadata is missing: $releasePath"
}

$sharedNames = @('GameBootstrap.cs', 'MemorySaveStore.cs', 'MainLoopContext.cs', 'OfflineLocalization.cs')
$sharedPaths = @($sharedNames | ForEach-Object { Join-Path $hostRoot $_ })
$licensePath = [IO.Path]::GetFullPath((Join-Path $hostRoot '../../../LICENSE'))
$coreProjectPath = Join-Path $labRoot 'src/SimulatorLab.Core/SimulatorLab.Core.csproj'
if (!$DryRun) {
    if (!(Test-Path -LiteralPath $hostRoot -PathType Container)) { throw "Host source directory is missing: $hostRoot" }
    foreach ($path in $sharedPaths) {
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Host source is missing: $path" }
    }
    if (!(Test-Path -LiteralPath $licensePath -PathType Leaf)) { throw 'Upstream MIT license required' }
    if (!(Test-Path -LiteralPath $coreProjectPath -PathType Leaf)) { throw "Core project is missing: $coreProjectPath" }
}

$solverInfo = if ([string]::IsNullOrWhiteSpace($SolverSnapshot)) {
    [pscustomobject]@{ path = ''; version = 'none'; lockSha256 = 'none'; lock = $null }
} else { Get-SnapshotInfo -SnapshotPath $SolverSnapshot -Kind solver }
$foreseerInfo = if ([string]::IsNullOrWhiteSpace($ForeseerSnapshot)) {
    [pscustomobject]@{ path = ''; version = 'none'; lockSha256 = 'none'; lock = $null }
} else { Get-SnapshotInfo -SnapshotPath $ForeseerSnapshot -Kind foreseer }
$sourceRevision = Get-OptionalGitValue -Arguments @('-C', $labRoot, 'rev-parse', 'HEAD')
$dirtyText = Get-OptionalGitValue -Arguments @('-C', $labRoot, 'status', '--porcelain', '--untracked-files=no')
$dirtyHash = if ($dirtyText -eq 'unavailable' -or !$dirtyText) { 'clean-or-unavailable' } else { Get-Sha256Text -Text $dirtyText }
$toolchainVersion = Get-ToolchainVersion
$scriptSha256 = Get-FileSha256 -Path $PSCommandPath

function Get-SourceInputDigest {
    param(
        [Parameter(Mandatory)][string[]]$HostFiles,
        [Parameter(Mandatory)][string]$HostLicense,
        [Parameter(Mandatory)][string]$GameDataDirectory,
        [Parameter(Mandatory)]$ForeseerInfo,
        [Parameter(Mandatory)][switch]$TolerateMissing
    )

    # Canonical, sorted (absolute path, sha256) rows over exactly the inputs
    # that build-manifest.json binds later. This closes the gap where a cache
    # key derived only from git revision + dirty-file list could miss a
    # content edit inside an already-modified file.
    $entries = [System.Collections.Generic.List[object]]::new()
    function Add-DigestEntry([string]$EntryPath) {
        $full = [IO.Path]::GetFullPath($EntryPath)
        $hash = 'missing'
        if (Test-Path -LiteralPath $full -PathType Leaf) {
            $hash = Get-FileSha256 -Path $full
        }
        elseif (!$TolerateMissing) {
            throw "Cache-key source input is missing: $full"
        }
        $entries.Add([ordered]@{ path = $full; sha256 = $hash })
    }

    foreach ($file in $HostFiles) { Add-DigestEntry $file }
    Add-DigestEntry $HostLicense
    Add-DigestEntry $PSCommandPath
    foreach ($gameAssembly in @('sts2.dll', 'GodotSharp.dll', '0Harmony.dll')) {
        Add-DigestEntry (Join-Path $GameDataDirectory $gameAssembly)
    }
    foreach ($file in @(Get-ChildItem -LiteralPath $PSScriptRoot -File -ErrorAction SilentlyContinue | Sort-Object FullName)) { Add-DigestEntry $file.FullName }
    foreach ($file in @(Get-ChildItem -LiteralPath (Join-Path $labRoot 'src/SimulatorLab.Core') -Filter '*.cs' -File -ErrorAction SilentlyContinue | Sort-Object FullName)) { Add-DigestEntry $file.FullName }
    Add-DigestEntry (Join-Path $labRoot 'src/SimulatorLab.Core/SimulatorLab.Core.csproj')
    Add-DigestEntry (Join-Path $labRoot 'tests/SimulatorLab.Core.Checks/Program.cs')
    Add-DigestEntry (Join-Path $labRoot 'tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj')
    if ($null -ne $ForeseerInfo.lock) {
        Add-DigestEntry (Join-Path $ForeseerInfo.path 'input-lock.json')
        foreach ($row in @($ForeseerInfo.lock.inputs)) { Add-DigestEntry (Join-Path $ForeseerInfo.path $row.path) }
    }

    $canonical = @($entries | Sort-Object -Property path -Unique) | ConvertTo-Json -Depth 4 -Compress
    return Get-Sha256Text -Text $canonical
}

$seedRecord = [ordered]@{
    seed       = 'not-supplied'
    seedSource = 'run stage only: tools/native-run/run-native-slice.ps1 -Seed (default LAB_V0111_NATIVE_DEV_01); the build stage receives and consumes no seed'
}
$versionSummary = [ordered]@{
    script             = $scriptVersion
    scriptSha256       = $scriptSha256
    logFormat          = $logFormat
    sourceRevision     = $sourceRevision
    toolchain          = $toolchainVersion
    solverVersion      = [string]$solverInfo.version
    foreseerVersion    = [string]$foreseerInfo.version
    gameVersion        = $releaseVersion
    gameCommit         = $releaseCommit
    gameAssemblySha256 = $gameAssemblySha256
}
$parametersSummary = [ordered]@{
    gamePath         = $gameRoot
    hostSource       = $hostRoot
    solverSnapshot   = if ([string]::IsNullOrWhiteSpace($SolverSnapshot)) { '' } else { [string]$solverInfo.path }
    foreseerSnapshot = if ([string]::IsNullOrWhiteSpace($ForeseerSnapshot)) { '' } else { [string]$foreseerInfo.path }
    outputRoot       = $OutputRoot
    captureEvidence  = [bool]$CaptureEvidence
    dryRun           = [bool]$DryRun
    retentionDays    = $RetentionDays
}
$runOutputBoundary = [ordered]@{
    buildLog      = 'run.log'
    buildMetadata = 'run-manifest.json'
    runConsoleLog = '.build/runs/<fresh-run-dir>/console.log'
    runStderrLog  = '.build/runs/<fresh-run-dir>/stderr.log'
    inRunTrace    = '.build/runs/<fresh-run-dir>/trace.jsonl'
    producedBy    = 'tools/native-run/run-native-slice.ps1'
    policy        = 'run outputs live in their own fresh .build/runs directory and are never duplicated into the build output'
}

$relativeHostSource = try { [IO.Path]::GetRelativePath($labRoot, $hostRoot) } catch { $hostRoot }
$sourceDigest = Get-SourceInputDigest -HostFiles $sharedPaths -HostLicense $licensePath -GameDataDirectory $data -ForeseerInfo $foreseerInfo -TolerateMissing:$DryRun
$keyPayload = [ordered]@{
    schema             = 'native-cache-key/v2'
    sourceDigest       = $sourceDigest
    sourceRevision     = $sourceRevision
    workingTree        = $dirtyHash
    scriptVersion      = $scriptVersion
    scriptSha256       = $scriptSha256
    toolchainVersion   = $toolchainVersion
    solverVersion      = [string]$solverInfo.version
    solverLockSha256   = [string]$solverInfo.lockSha256
    foreseerVersion    = [string]$foreseerInfo.version
    foreseerLockSha256 = [string]$foreseerInfo.lockSha256
    gameRelease        = $releaseVersion
    gameCommit         = $releaseCommit
    gameAssemblySha256 = $gameAssemblySha256
    hostSource         = $relativeHostSource
}
$keyCanonical = $keyPayload | ConvertTo-Json -Depth 8 -Compress
$contentKey = (Get-Sha256Text -Text $keyCanonical).Substring(0, 32).ToLowerInvariant()
$cachePath = Join-Path $cacheRoot $contentKey
$explicitOutput = ![string]::IsNullOrWhiteSpace($OutputRoot)
$plannedOutput = if ($explicitOutput) { Assert-UnderLab -Path ([IO.Path]::GetFullPath($OutputRoot)) -Description 'Build output' } else { $cachePath }
$outputRuleText = if ($explicitOutput) {
    'explicit -OutputRoot: must resolve under the Lab root and must not already exist; never silently rewritten'
}
else {
    'default stable cache: .build/native-build/cache/<contentKey>'
}

if ($DryRun) {
    $cacheState = if (Test-ReadyOutput -Root $cachePath) { 'ready-cache-hit' } elseif (Test-Path -LiteralPath $cachePath) { 'present-but-not-ready' } else { 'cache-miss' }
    Write-Status ("dry-run output={0} mode={1} contentKey={2} sourceRevision={3} toolchain={4} solverVersion={5} captureEvidence={6}" -f $plannedOutput, ($(if ($explicitOutput) { 'explicit-output-root' } else { $cacheState })), $contentKey, $sourceRevision, $toolchainVersion, $solverInfo.version, [bool]$CaptureEvidence)
    Write-Status ("dry-run scriptVersion={0} logFormat={1} foreseerVersion={2} gameVersion={3} gameCommit={4} sourceDigest={5}" -f $scriptVersion, $logFormat, $foreseerInfo.version, $releaseVersion, $releaseCommit, $sourceDigest)
    Write-Status ("dry-run seed={0} seedSource={1}" -f $seedRecord.seed, $seedRecord.seedSource)
    Write-Status "dry-run outputRule=$outputRuleText"
    Write-Status 'dry-run planned-retain=bin/Release/net9.0; compiled sources (*.cs, NativeRun.csproj, license files, foreseer-lock.json); build-manifest.json; run-manifest.json; run.log; .ready'
    Write-Status 'dry-run planned-prune=obj (regenerable intermediate output)'
    Write-Status 'dry-run planned-evidence=off; pass -CaptureEvidence to stage, hash-verify and publish source-snapshot/, compiled/ and evidence-manifest.json'
    Write-Output "NATIVE_BUILD_DRY_RUN=$plannedOutput"
    Write-Output "NATIVE_BUILD_DRY_RUN_KEY=$contentKey"
    return
}

if ($toolchainVersion -eq 'unavailable') {
    throw 'dotnet SDK is required for a non-dry build'
}

$solverLock = $solverInfo.lock
$foreseerLock = $foreseerInfo.lock
$solverProperties = ''
$solverReferences = ''
$assemblyName = 'SimulatorLab.NativeRun'
if ($null -ne $solverLock) {
    foreach ($r in $solverLock.assemblies) {
        $f = Join-Path $solverInfo.path $r.file
        if (!(Test-Path -LiteralPath $f -PathType Leaf) -or (Get-FileSha256 -Path $f) -ne $r.sha256) { throw 'Solver input changed' }
        $solverReferences += "<Reference Include=`"$($r.name)`" HintPath=`"$f`" Private=`"false`"/>"
    }
    foreach ($r in $solverLock.source) {
        $f = Join-Path $solverInfo.path ('source/' + $r.path)
        if (!(Test-Path -LiteralPath $f -PathType Leaf) -or (Get-FileSha256 -Path $f) -ne $r.sha256) { throw 'Solver snapshot source changed' }
    }
    $assemblyName = 'OfflineSearchHarness'
    $solverProperties = '<DefineConstants>$(DefineConstants);SOLVER_GATE</DefineConstants>'
}

$out = $plannedOutput
$buildMode = if ($explicitOutput) { 'explicit-output-root' } else { 'stable-cache' }
$cacheHit = !$explicitOutput -and (Test-ReadyOutput -Root $out)
$staging = $null
$rows = [System.Collections.Generic.List[object]]::new()

if ($cacheHit) {
    $buildMode = 'cache-hit'
    Write-Status "cache-hit output=$out contentKey=$contentKey"
}
else {
    $failurePhase = 'cache-conflict'
    if ($explicitOutput -and (Test-Path -LiteralPath $out)) {
        throw "Use a fresh immutable build directory: $out"
    }
    if (!$explicitOutput -and (Test-Path -LiteralPath $out)) {
        $existing = Get-Item -LiteralPath $out -Force
        if (!(Test-ReparsePoint -Item $existing)) {
            throw "Cache key exists without a ready marker; remove or review it before rebuilding: $out"
        }
        throw "Cache key is a reparse point and cannot be replaced: $out"
    }
    $stageParent = if ($explicitOutput) { Split-Path -Parent $out } else { $cacheRoot }
    New-Item -ItemType Directory -Path $stageParent -Force | Out-Null
    $stageName = '.staging-{0}-{1}' -f ($(if ($explicitOutput) { [IO.Path]::GetFileName($out) } else { $contentKey }), [Guid]::NewGuid().ToString('N'))
    $staging = Join-Path $stageParent $stageName
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    $out = $staging
}

function New-RunLogText {
    param([Parameter(Mandatory)]$Manifest)

    # run.log is the single detailed log: a machine-readable key=value header
    # mirroring run-manifest.json, then the complete buffered status/build
    # output. Nothing else copies this content.
    $headerEntries = [ordered]@{
        'logFormat'                   = $Manifest.logFormat
        'schema'                      = $Manifest.schema
        'runId'                       = $Manifest.runId
        'startedUtc'                  = $Manifest.startedUtc
        'finishedUtc'                 = $Manifest.finishedUtc
        'exitStatus'                  = $Manifest.exitStatus
        'seed'                        = $Manifest.seed.seed
        'seedSource'                  = $Manifest.seed.seedSource
        'mode'                        = $Manifest.mode
        'output'                      = $Manifest.output
        'outputRule'                  = $Manifest.outputRule
        'contentKey'                  = $Manifest.contentKey
        'cacheHit'                    = $Manifest.cacheHit
        'cacheState'                  = $Manifest.cacheState
        'captureEvidence'             = $Manifest.captureEvidence
        'sourceRevision'              = $Manifest.version.sourceRevision
        'scriptVersion'               = $Manifest.version.script
        'scriptSha256'                = $Manifest.version.scriptSha256
        'toolchain'                   = $Manifest.version.toolchain
        'solverVersion'               = $Manifest.version.solverVersion
        'foreseerVersion'             = $Manifest.version.foreseerVersion
        'gameVersion'                 = $Manifest.version.gameVersion
        'gameCommit'                  = $Manifest.version.gameCommit
        'parameters.gamePath'         = $Manifest.parameters.gamePath
        'parameters.hostSource'       = $Manifest.parameters.hostSource
        'parameters.solverSnapshot'   = $Manifest.parameters.solverSnapshot
        'parameters.foreseerSnapshot' = $Manifest.parameters.foreseerSnapshot
        'parameters.outputRoot'       = $Manifest.parameters.outputRoot
        'parameters.captureEvidence'  = $Manifest.parameters.captureEvidence
        'parameters.retentionDays'    = $Manifest.parameters.retentionDays
        'build.dotnetExit'            = $Manifest.build.dotnetExit
        'build.stderrLineCount'       = $Manifest.build.stderrLineCount
        'runOutputs.runConsoleLog'    = $Manifest.runOutputs.runConsoleLog
        'runOutputs.runStderrLog'     = $Manifest.runOutputs.runStderrLog
        'runOutputs.inRunTrace'       = $Manifest.runOutputs.inRunTrace
        'runOutputs.policy'           = $Manifest.runOutputs.policy
    }
    if ($Manifest.buildId) { $headerEntries['buildId'] = $Manifest.buildId }
    $builder = [System.Text.StringBuilder]::new()
    foreach ($key in @($headerEntries.Keys)) {
        [void]$builder.AppendLine(('{0}={1}' -f $key, $headerEntries[$key]))
    }
    if ($Manifest.failure) {
        [void]$builder.AppendLine(('failure.classification={0}' -f $Manifest.failure.classification))
        [void]$builder.AppendLine(('failure.phase={0}' -f $Manifest.failure.phase))
        [void]$builder.AppendLine(('failure.message={0}' -f $Manifest.failure.message))
    }
    [void]$builder.AppendLine('----- detailed log -----')
    return $builder.ToString() + (($script:logLines -join [Environment]::NewLine) + [Environment]::NewLine)
}

function Write-FailureRecord {
    param(
        [Parameter(Mandatory)][string]$TargetDirectory,
        [Parameter(Mandatory)][string]$Classification,
        [Parameter(Mandatory)][string]$Message,
        [Parameter(Mandatory)][datetime]$FinishedUtc
    )

    # Minimal failure diagnostics retained in staging: one small metadata
    # file plus the single detailed log. The bulky regenerable bin/obj trees
    # are pruned by the caller; this record must survive them.
    $stderrLines = @($dotnetStderr)
    $failureRunManifest = [ordered]@{
        schema          = 'native-build-run/v2'
        runId           = [Guid]::NewGuid().ToString('N')
        startedUtc      = $runStartedUtc.ToString('o')
        finishedUtc     = $FinishedUtc.ToString('o')
        mode            = $buildMode
        output          = $plannedOutput
        outputRule      = $outputRuleText
        contentKey      = $contentKey
        cacheHit        = [bool]$cacheHit
        cacheState      = $buildMode
        captureEvidence = [bool]$CaptureEvidence
        exitStatus      = 'failed'
        failure         = [ordered]@{ classification = $Classification; phase = $failurePhase; message = $Message }
        seed            = $seedRecord
        version         = $versionSummary
        parameters      = $parametersSummary
        build           = [ordered]@{
            dotnetExit      = $dotnetExitCode
            stderrLineCount = $stderrLines.Count
            stderrExcerpt   = (@($stderrLines | Select-Object -Last 100) -join [Environment]::NewLine)
            detail          = 'full dotnet output is embedded in run.log (single detailed log)'
        }
        runOutputs      = $runOutputBoundary
        sourceRevision  = $sourceRevision
        toolchain       = $toolchainVersion
        solverVersion   = [string]$solverInfo.version
        foreseerVersion = [string]$foreseerInfo.version
        runLog          = 'run.log'
    }
    if ($buildId) { $failureRunManifest['buildId'] = $buildId }
    Write-AtomicJson -Path (Join-Path $TargetDirectory 'run-manifest.json') -Value $failureRunManifest -Depth 12
    Write-AtomicText -Path (Join-Path $TargetDirectory 'run.log') -Text (New-RunLogText -Manifest $failureRunManifest)
}

try {
    if (!$cacheHit) {
        $failurePhase = 'build'
        foreach ($path in $sharedPaths) {
            $rows.Add(@{ path = $path; sha256 = Get-FileSha256 -Path $path })
            Copy-Item -LiteralPath $path -Destination $out
        }
        foreach ($source in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File) {
            Copy-Item -LiteralPath $source.FullName -Destination $out
        }
        Copy-Item -LiteralPath $licensePath -Destination (Join-Path $out 'UPSTREAM-LICENSE')
        $rows.Add(@{ path = $licensePath; sha256 = Get-FileSha256 -Path $licensePath })

        if ($null -ne $foreseerLock) {
            $rf = $foreseerInfo.path
            foreach ($r in $foreseerLock.inputs) {
                $p = Join-Path $rf $r.path
                if (!(Test-Path -LiteralPath $p -PathType Leaf) -or (Get-FileSha256 -Path $p) -ne $r.sha256) { throw 'Foreseer input changed' }
                $rows.Add(@{ path = $p; sha256 = $r.sha256 })
            }
            $rows.Add(@{ path = (Join-Path $rf 'input-lock.json'); sha256 = Get-FileSha256 -Path (Join-Path $rf 'input-lock.json') })
            Copy-Item -LiteralPath (Join-Path $rf 'input-lock.json') -Destination (Join-Path $out 'foreseer-lock.json')
            Copy-Item -LiteralPath (Join-Path $rf 'LICENSE') -Destination (Join-Path $out 'RANDOM-FORESEER-LICENSE')
        }

        $bootstrapPath = Join-Path $out 'GameBootstrap.cs'
        $text = [IO.File]::ReadAllText($bootstrapPath)
        $start = $text.IndexOf('        if (!NativeModRunLoop.Enabled) {')
        if ($start -ge 0) {
            $end = $text.IndexOf('        Trace("mod_manager_state");', $start)
            if ($end -lt 0) { throw 'Reviewed upstream bootstrap anchor changed' }
            $text = $text.Remove($start, $end - $start).Insert($start, @'
        AccessTools.Field(typeof(ReflectionHelper), "_modTypes").SetValue(null, Array.Empty<Type>());
        AccessTools.PropertySetter(typeof(ModManager), nameof(ModManager.State))!.Invoke(null, [ModManagerState.Initialized]);
'@ + [Environment]::NewLine)
        }
        $text = $text -replace '(?m)^.*CombatSolver\.SolverController\.DisplayServerNameProvider =.*\r?\n', ''
        $text = $text -replace '(?m)^.*_bypasses\.Add\("SolverController.DisplayServerNameProvider.*\r?\n', ''
        $text = $text.Replace('        if (NativeModRunLoop.Enabled) NativeModRunLoop.Initialize();', '')
        $text = $text.Replace('ModelDb.Init(EleanorModBootstrap.IncludeModels(AbstractModelSubtypes.All.ToArray()));', 'ModelDb.Init(AbstractModelSubtypes.All.ToArray());')
        $text = $text.Replace('combatsolver.offline.harness', 'simulatorlab.native.host')
        if ($text -cmatch 'EleanorModBootstrap\.|NativeModRunLoop\.|CombatSolver\.') { throw 'Native bootstrap still depends on Mod/solver' }
        [IO.File]::WriteAllText($bootstrapPath, $text)

        $core = [IO.Path]::GetFullPath($coreProjectPath)
        $project = @"
<Project Sdk="Microsoft.NET.Sdk">
 <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><LangVersion>13.0</LangVersion><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><AssemblyName>$assemblyName</AssemblyName>$solverProperties</PropertyGroup>
 <ItemGroup><PackageReference Include="Krafs.Publicizer" Version="2.3.0" PrivateAssets="all"/><Publicize Include="sts2" IncludeVirtualMembers="false" IncludeCompilerGeneratedMembers="false"/></ItemGroup>
 <ItemGroup><Reference Include="sts2" HintPath="$data/sts2.dll" Private="false"/><Reference Include="GodotSharp" HintPath="$data/GodotSharp.dll" Private="false"/><Reference Include="0Harmony" HintPath="$data/0Harmony.dll" Private="false"/><ProjectReference Include="$core"/>$solverReferences</ItemGroup>
 <ItemGroup><RuntimeHostConfigurationOption Include="Sts2DataDir" Value="$data"/><RuntimeHostConfigurationOption Include="LabRoot" Value="$labRoot"/></ItemGroup>
</Project>
"@
        [IO.File]::WriteAllText((Join-Path $out 'NativeRun.csproj'), $project)

        $dotnetRaw = @(& dotnet build (Join-Path $out 'NativeRun.csproj') -c Release --nologo --ignore-failed-sources -p:NuGetAudit=false 2>&1)
        $dotnetExitCode = $LASTEXITCODE
        $dotnetStderr = @($dotnetRaw | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] } | ForEach-Object { $_.ToString() })
        Add-RawLogLines -Lines @($dotnetRaw | ForEach-Object { $_.ToString() })
        if ($dotnetExitCode -ne 0) { throw "Native build failed (dotnet exit $dotnetExitCode); the full build output is embedded in run.log" }

        # sts2.dll runs a module initializer before the harness resolver can
        # service every managed dependency. Keep the small game-owned runtime
        # closure beside the executable and bind each copied file below.
        $runtimeBin = Join-Path $out 'bin/Release/net9.0'
        $runtimeDataNames = @(
            'Sentry.dll', 'Sentry.Godot.dll', 'Steamworks.NET.dll',
            'SmartFormat.dll', 'SmartFormat.ZString.dll', 'JetBrains.Annotations.dll',
            'MonoMod.Backports.dll', 'MonoMod.ILHelpers.dll',
            'SharpGen.Runtime.dll', 'SharpGen.Runtime.COM.dll',
            'Vortice.DirectX.dll', 'Vortice.DXGI.dll', 'Vortice.Mathematics.dll',
            'System.IO.Hashing.dll'
        )
        foreach ($name in $runtimeDataNames) {
            $source = Join-Path $data $name
            if (!(Test-Path -LiteralPath $source -PathType Leaf)) { throw "Pinned runtime dependency is missing: $source" }
            Copy-Item -LiteralPath $source -Destination (Join-Path $runtimeBin $name) -Force
        }
        $runtimeRootNames = @(
            'libsentry.windows.release.x86_64.dll',
            'libGodotFmod.windows.template_release.x86_64.dll',
            'libspine_godot.windows.template_release.x86_64.dll'
        )
        foreach ($name in $runtimeRootNames) {
            $source = Join-Path $gameRoot $name
            if (!(Test-Path -LiteralPath $source -PathType Leaf)) { throw "Pinned native dependency is missing: $source" }
            Copy-Item -LiteralPath $source -Destination (Join-Path $runtimeBin $name) -Force
        }

        if ($null -ne $solverLock) {
            foreach ($r in $solverLock.assemblies) { Copy-Item (Join-Path $solverInfo.path $r.file) (Join-Path $out 'bin/Release/net9.0') }
            Copy-Item (Join-Path $solverInfo.path 'solver-lock.json') (Join-Path $out 'bin/Release/net9.0')
            Copy-Item (Join-Path $solverInfo.path 'LICENSE') (Join-Path $out 'SOLVER-LICENSE')
            Copy-Item (Join-Path $solverInfo.path 'CombatSolver.json') (Join-Path $out 'bin/Release/net9.0')
        }
        if ($null -ne $foreseerLock) { Copy-Item -LiteralPath (Join-Path $foreseerInfo.path 'input-lock.json') -Destination (Join-Path $out 'bin/Release/net9.0/foreseer-lock.json') }

        # The obj/ tree is regenerable intermediate output: it is not bound by
        # build-manifest.json and not needed to run. Normal builds never
        # publish it. A prune failure fails the build (classification
        # cleanup-failure) instead of silently publishing extra bulk.
        $failurePhase = 'cleanup'
        $objPath = Join-Path $out 'obj'
        if (Test-Path -LiteralPath $objPath -PathType Container) {
            Remove-ValidatedDirectory -Path $objPath -RequiredParent $out -RequiredLeafName 'obj' -Description 'obj prune'
            Write-Status "pruned-regenerable-output path=$objPath reason=obj-not-retained-by-storage-policy"
        }
    }

    $failurePhase = 'manifest'
    $rows.AddRange(@(Get-ChildItem -LiteralPath $PSScriptRoot -File | ForEach-Object { @{ path = $_.FullName; sha256 = Get-FileSha256 -Path $_.FullName } }))
    $rows.AddRange(@(Get-ChildItem -LiteralPath (Join-Path $labRoot 'src/SimulatorLab.Core') -Filter '*.cs' -File | ForEach-Object { @{ path = $_.FullName; sha256 = Get-FileSha256 -Path $_.FullName } }))
    $rows.AddRange(@(Get-Item (Join-Path $labRoot 'src/SimulatorLab.Core/SimulatorLab.Core.csproj'), (Join-Path $labRoot 'tests/SimulatorLab.Core.Checks/Program.cs'), (Join-Path $labRoot 'tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj') | ForEach-Object { @{ path = $_.FullName; sha256 = Get-FileSha256 -Path $_.FullName } }))
    $inputs = @('sts2.dll', 'GodotSharp.dll', '0Harmony.dll') | ForEach-Object { @{ path = (Join-Path $data $_); sha256 = Get-FileSha256 -Path (Join-Path $data $_) } }
    $inputs += @('Sentry.dll', 'Sentry.Godot.dll', 'Steamworks.NET.dll', 'SmartFormat.dll', 'SmartFormat.ZString.dll', 'JetBrains.Annotations.dll', 'MonoMod.Backports.dll', 'MonoMod.ILHelpers.dll', 'SharpGen.Runtime.dll', 'SharpGen.Runtime.COM.dll', 'Vortice.DirectX.dll', 'Vortice.DXGI.dll', 'Vortice.Mathematics.dll', 'System.IO.Hashing.dll') | ForEach-Object { @{ path = (Join-Path $data $_); sha256 = Get-FileSha256 -Path (Join-Path $data $_) } }
    $inputs += @('libsentry.windows.release.x86_64.dll', 'libGodotFmod.windows.template_release.x86_64.dll', 'libspine_godot.windows.template_release.x86_64.dll') | ForEach-Object { @{ path = (Join-Path $gameRoot $_); sha256 = Get-FileSha256 -Path (Join-Path $gameRoot $_) } }
    $compiled = @(Get-ChildItem -LiteralPath $out -File | Where-Object { $_.Extension -in @('.cs', '.csproj') -or $_.Name -eq 'UPSTREAM-LICENSE' } | ForEach-Object { @{ path = $_.Name; sha256 = Get-FileSha256 -Path $_.FullName } })
    $artifacts = @(Get-ChildItem -LiteralPath (Join-Path $out 'bin/Release/net9.0') -File | ForEach-Object { @{ path = [IO.Path]::GetRelativePath($out, $_.FullName); sha256 = Get-FileSha256 -Path $_.FullName } })
    $payload = [ordered]@{
        schema             = 'native-build/v2'
        profile            = if ($null -ne $solverLock) { 'solver-gate/v1' } else { 'native-only/v1' }
        executable         = $assemblyName + '.dll'
        head               = $sourceRevision
        sourceRevision     = $sourceRevision
        contentKey         = $contentKey
        cacheKey           = $keyPayload
        source             = @($rows)
        inputs             = @($inputs)
        compiled           = @($compiled)
        artifacts          = @($artifacts)
        toolchain          = $toolchainVersion
        solverVersion      = [string]$solverInfo.version
        foreseerVersion    = [string]$foreseerInfo.version
        game               = [ordered]@{ version = $releaseVersion; commit = $releaseCommit; sts2Sha256 = $gameAssemblySha256 }
        captureEvidence    = [bool]$CaptureEvidence
        builtUtc           = [DateTime]::UtcNow.ToString('o')
    }
    $canonical = $payload | ConvertTo-Json -Depth 14 -Compress
    $buildId = Get-Sha256Text -Text $canonical

    if ($cacheHit) {
        # The cache key now pins every manifest input, so a recomputed row set
        # that differs from the entry's recorded rows means the entry (or a
        # bound input) drifted. Refuse instead of refreshing a manifest over
        # unknown content.
        $failurePhase = 'cache-integrity'
        $existingManifestPath = Join-Path $out 'build-manifest.json'
        $existingManifest = $null
        try { $existingManifest = Get-Content -LiteralPath $existingManifestPath -Raw | ConvertFrom-Json }
        catch { throw "Existing cache entry manifest is unreadable; remove or review it before reuse: $existingManifestPath" }
        foreach ($section in @('source', 'inputs', 'compiled', 'artifacts')) {
            $existingRows = @($existingManifest.manifest.$section | ForEach-Object { [ordered]@{ path = [string]$_.path; sha256 = [string]$_.sha256 } }) | ConvertTo-Json -Depth 8 -Compress
            $freshRows = @($payload.$section | ForEach-Object { [ordered]@{ path = [string]$_.path; sha256 = [string]$_.sha256 } }) | ConvertTo-Json -Depth 8 -Compress
            if ($existingRows -cne $freshRows) {
                throw "Cache entry content drifted from its cache key (section=$section); remove or review it before reuse: $out"
            }
        }
        $failurePhase = 'manifest'
    }

    $manifest = [ordered]@{ buildId = $buildId; manifest = $payload }
    Write-AtomicJson -Path (Join-Path $out 'build-manifest.json') -Value $manifest -Depth 16
    Write-AtomicText -Path (Join-Path $out '.ready') -Text ("contentKey={0}`nbuildId={1}`nschema=native-build/v2`nlogFormat={2}`nscriptVersion={3}`ncompletedUtc={4}`n" -f $contentKey, $buildId, $logFormat, $scriptVersion, [DateTime]::UtcNow.ToString('o'))

    $failurePhase = 'publish'
    if (!$explicitOutput) {
        if (Test-Path -LiteralPath $cachePath) {
            $existingReady = Test-ReadyOutput -Root $cachePath
            if ($existingReady) {
                # Our own staged tree loses the race; discard it (it lives in
                # cacheRoot under a .staging- name and has never been published).
                Remove-Item -LiteralPath $out -Recurse -Force
                $out = $cachePath
                $cacheHit = $true
                $buildMode = 'cache-race-hit'
            }
            else {
                $failurePhase = 'cache-conflict'
                throw "Cache key appeared while building and is not ready: $cachePath"
            }
        }
        else {
            Move-Item -LiteralPath $out -Destination $cachePath
            $out = $cachePath
        }
    }
    elseif ($staging) {
        $destination = [IO.Path]::GetFullPath($plannedOutput)
        $destinationParent = Split-Path -Parent $destination
        New-Item -ItemType Directory -Path $destinationParent -Force | Out-Null
        Move-Item -LiteralPath $out -Destination $destination
        $out = $destination
    }
}
catch {
    $failureMessage = $_.Exception.Message
    $failureClassification = switch ($failurePhase) {
        'input-validation' { 'input-validation-failure' }
        'cache-conflict' { 'cache-conflict' }
        'cache-integrity' { 'cache-integrity-failure' }
        'build' { 'build-failure' }
        'cleanup' { 'cleanup-failure' }
        'manifest' { 'metadata-write-failure' }
        'publish' { 'publish-failure' }
        'run-metadata' { 'metadata-write-failure' }
        'evidence-capture' { 'evidence-capture-failure' }
        default { 'build-failure' }
    }
    $failureFinishedUtc = [DateTime]::UtcNow
    Write-Status ("build-failed classification={0} phase={1} reason={2}" -f $failureClassification, $failurePhase, $failureMessage)

    if ($staging -and (Test-Path -LiteralPath $staging)) {
        # Best-effort cleanup of the bulky regenerable trees, with explicit
        # per-path reporting; cleanup failures are never swallowed.
        foreach ($leaf in @('bin', 'obj')) {
            $bulkPath = Join-Path $staging $leaf
            if (Test-Path -LiteralPath $bulkPath -PathType Container) {
                try {
                    Remove-ValidatedDirectory -Path $bulkPath -RequiredParent $staging -RequiredLeafName $leaf -Description "failure staging prune ($leaf)"
                    Write-Status "failure-staging-pruned path=$bulkPath"
                }
                catch {
                    Write-Status ("failure-staging-prune-failed path={0} error={1} action=retained-for-review" -f $bulkPath, $_.Exception.Message)
                }
            }
        }
        try {
            Write-FailureRecord -TargetDirectory $staging -Classification $failureClassification -Message $failureMessage -FinishedUtc $failureFinishedUtc
            Write-Status "failure-diagnostics-retained=$staging"
        }
        catch {
            Write-Status ("failure-metadata-write-failed path={0} error={1} action=console-record-only" -f $staging, $_.Exception.Message)
        }
    }
    elseif ($staging) {
        Write-Status "staging-already-removed=$staging"
    }
    else {
        Write-Status 'failure-record-location=console-only reason=no-output-directory-exists-yet; nothing is created that could poison the cache key'
    }
    throw
}

function Write-Evidence {
    param([Parameter(Mandatory)][string]$Root)

    $sourceDestination = Join-Path $Root 'source-snapshot'
    $compiledDestination = Join-Path $Root 'compiled'
    $manifestDestination = Join-Path $Root 'evidence-manifest.json'

    $existingManifestItem = Get-Item -LiteralPath $manifestDestination -Force -ErrorAction SilentlyContinue
    if ($null -ne $existingManifestItem) {
        if (!($existingManifestItem -is [IO.FileInfo]) -or (Test-ReparsePoint -Item $existingManifestItem)) {
            throw "Evidence manifest is not a safe regular file: $manifestDestination"
        }
        $existing = $null
        try { $existing = Get-Content -LiteralPath $manifestDestination -Raw | ConvertFrom-Json }
        catch { throw "Existing evidence manifest is unreadable; remove or review it manually: $manifestDestination" }
        if ([string]$existing.schema -eq 'native-build-evidence/v2') {
            Write-Status "evidence-capture status=already-captured manifest=$manifestDestination"
            return
        }
        throw "Existing evidence manifest has unexpected schema '$($existing.schema)'; refusing to overwrite: $manifestDestination"
    }
    if ((Test-Path -LiteralPath $sourceDestination) -or (Test-Path -LiteralPath $compiledDestination)) {
        throw "Partial evidence exists without evidence-manifest.json; remove or review manually before recapturing: $sourceDestination ; $compiledDestination"
    }

    $evidenceStage = Join-Path $Root ('.evidence-staging-' + [Guid]::NewGuid().ToString('N'))
    $sourceStage = Join-Path $evidenceStage 'source-snapshot'
    $compiledStage = Join-Path $evidenceStage 'compiled'
    try {
        New-Item -ItemType Directory -Path $sourceStage -Force | Out-Null
        New-Item -ItemType Directory -Path $compiledStage -Force | Out-Null

        $stagedCopies = [System.Collections.Generic.List[object]]::new()
        foreach ($file in @(Get-ChildItem -LiteralPath $Root -File | Where-Object { $_.Extension -in @('.cs', '.csproj') -or $_.Name -in @('UPSTREAM-LICENSE', 'RANDOM-FORESEER-LICENSE', 'SOLVER-LICENSE') } | Sort-Object Name)) {
            Copy-Item -LiteralPath $file.FullName -Destination $sourceStage
            $stagedCopies.Add(@{ source = $file.FullName; staged = (Join-Path $sourceStage $file.Name); relative = ('source-snapshot/' + $file.Name) })
        }
        # Copy the CONTENTS of bin/Release/net9.0 so evidence paths stay flat
        # under compiled/ (item-by-item copy: Copy-Item of the directory itself
        # would nest an extra net9.0/ level).
        foreach ($item in @(Get-ChildItem -LiteralPath (Join-Path $Root 'bin/Release/net9.0'))) {
            Copy-Item -LiteralPath $item.FullName -Destination $compiledStage -Recurse
        }
        foreach ($file in @(Get-ChildItem -LiteralPath $compiledStage -Recurse -File | Sort-Object FullName)) {
            $relativeInCompiled = [IO.Path]::GetRelativePath($compiledStage, $file.FullName) -replace '\\', '/'
            $stagedCopies.Add(@{ source = (Join-Path $Root (Join-Path 'bin/Release/net9.0' $relativeInCompiled)); staged = $file.FullName; relative = ('compiled/' + $relativeInCompiled) })
        }

        # Verify every staged byte before publishing anything.
        $outputs = [System.Collections.Generic.List[object]]::new()
        foreach ($copy in $stagedCopies) {
            $expected = Get-FileSha256 -Path $copy.source
            $observed = Get-FileSha256 -Path $copy.staged
            if ($observed -ne $expected) { throw "Evidence staging hash mismatch for $($copy.relative)" }
            $outputs.Add([ordered]@{ path = $copy.relative; sha256 = $observed; bytes = (Get-Item -LiteralPath $copy.staged).Length })
        }
        foreach ($anchor in @('build-manifest.json', 'run-manifest.json', 'run.log')) {
            $anchorPath = Join-Path $Root $anchor
            if (Test-Path -LiteralPath $anchorPath -PathType Leaf) {
                $outputs.Add([ordered]@{ path = $anchor; sha256 = (Get-FileSha256 -Path $anchorPath); bytes = (Get-Item -LiteralPath $anchorPath).Length })
            }
        }

        $evidenceManifest = [ordered]@{
            schema         = 'native-build-evidence/v2'
            logFormat      = $logFormat
            buildId        = $buildId
            contentKey     = $contentKey
            createdUtc     = $runStartedUtc.ToString('o')
            capturedUtc    = [DateTime]::UtcNow.ToString('o')
            exitStatus     = 'build-succeeded'
            seed           = $seedRecord
            version        = $versionSummary
            config         = $parametersSummary
            inputs         = @($inputs)
            outputs        = @($outputs)
            sourceSnapshot = 'source-snapshot'
            compiled       = 'compiled'
        }
        Write-AtomicJson -Path (Join-Path $evidenceStage 'evidence-manifest.json') -Value $evidenceManifest -Depth 14

        # Publish content first, manifest last: evidence-manifest.json is the
        # completion marker, so an interrupted publish can never look complete.
        Move-Item -LiteralPath $sourceStage -Destination $sourceDestination
        Move-Item -LiteralPath $compiledStage -Destination $compiledDestination
        Move-Item -LiteralPath (Join-Path $evidenceStage 'evidence-manifest.json') -Destination $manifestDestination
        Write-Status ("evidence-capture published sourceSnapshot={0} compiled={1} manifest={2} files={3}" -f $sourceDestination, $compiledDestination, $manifestDestination, $outputs.Count)
    }
    finally {
        if (Test-Path -LiteralPath $evidenceStage) {
            $cleanupError = $null
            try { Remove-Item -LiteralPath $evidenceStage -Recurse -Force -ErrorAction Stop }
            catch { $cleanupError = $_.Exception.Message }
            if (Test-Path -LiteralPath $evidenceStage) {
                Write-Status ("evidence-staging-cleanup-failed path={0} error={1} action=retained-for-review" -f $evidenceStage, $(if ($cleanupError) { $cleanupError } else { 'unknown' }))
            }
        }
    }
}

$failurePhase = 'run-metadata'
$runFinishedUtc = [DateTime]::UtcNow
$runManifest = [ordered]@{
    schema          = 'native-build-run/v2'
    runId           = [Guid]::NewGuid().ToString('N')
    startedUtc      = $runStartedUtc.ToString('o')
    finishedUtc     = $runFinishedUtc.ToString('o')
    mode            = $buildMode
    output          = $out
    outputRule      = $outputRuleText
    contentKey      = $contentKey
    cacheHit        = [bool]$cacheHit
    cacheState      = $buildMode
    captureEvidence = [bool]$CaptureEvidence
    exitStatus      = 'succeeded'
    buildId         = $buildId
    seed            = $seedRecord
    version         = $versionSummary
    parameters      = $parametersSummary
    build           = [ordered]@{
        dotnetExit      = $dotnetExitCode
        stderrLineCount = @($dotnetStderr).Count
        detail          = 'full dotnet output is embedded in run.log (single detailed log); dotnetExit=not-executed means a cache hit'
    }
    runOutputs      = $runOutputBoundary
    sourceRevision  = $sourceRevision
    toolchain       = $toolchainVersion
    solverVersion   = [string]$solverInfo.version
    foreseerVersion = [string]$foreseerInfo.version
    runLog          = 'run.log'
}
try {
    Write-AtomicJson -Path (Join-Path $out 'run-manifest.json') -Value $runManifest -Depth 12
    Write-AtomicText -Path (Join-Path $out 'run.log') -Text (New-RunLogText -Manifest $runManifest)
}
catch {
    Write-Status ("run-metadata-write-failed path={0} error={1} action=console-record-only; the published build output remains ready" -f $out, $_.Exception.Message)
    throw
}
if ($CaptureEvidence) {
    $failurePhase = 'evidence-capture'
    try {
        Write-Evidence -Root $out
    }
    catch {
        Write-Status ("evidence-capture-failed reason={0}; the published build output itself remains ready and reusable" -f $_.Exception.Message)
        throw
    }
}

Write-Output "NATIVE_BUILD=$out"
Write-Output "NATIVE_BUILD_KEY=$contentKey"
