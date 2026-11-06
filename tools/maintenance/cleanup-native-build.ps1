#requires -Version 7.0

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    # Nothing is removed unless this switch is present.  -WhatIf remains available
    # through SupportsShouldProcess and also prevents removal when combined with -Apply.
    [switch]$Apply,

    # Explicitly acknowledge a size guard or the 5 GiB normal-run ceiling.
    [switch]$Force,

    # This is a review/deletion budget, not a path permission.  The whitelist below
    # is always enforced independently of this value.
    [long]$MaxDeleteBytes = 5GB,

    # DateTime values are compared in UTC.  The default is fourteen days before start.
    [datetime]$Before = [DateTime]::UtcNow.AddDays(-14)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$defaultHardLimitBytes = [decimal](5GB)
$softReviewLimitBytes = [decimal](2GB)
$nowUtc = [DateTime]::UtcNow
$beforeUtc = $Before.ToUniversalTime()

if ($MaxDeleteBytes -le 0) {
    throw '-MaxDeleteBytes must be greater than zero.'
}

if ($beforeUtc -gt $nowUtc) {
    throw ("-Before cannot be in the future. Received {0:u}; current UTC is {1:u}." -f $beforeUtc, $nowUtc)
}

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$nativeRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot '.build\native-build'))
$maxDeleteDecimal = [decimal]$MaxDeleteBytes

function Format-Bytes {
    param([decimal]$Bytes)

    if ($Bytes -lt 0) {
        return ('-{0}' -f (Format-Bytes (-$Bytes)))
    }

    if ($Bytes -ge [decimal](1TB)) { return ('{0:N2} TiB' -f ($Bytes / [decimal](1TB))) }
    if ($Bytes -ge [decimal](1GB)) { return ('{0:N2} GiB' -f ($Bytes / [decimal](1GB))) }
    if ($Bytes -ge [decimal](1MB)) { return ('{0:N2} MiB' -f ($Bytes / [decimal](1MB))) }
    if ($Bytes -ge [decimal](1KB)) { return ('{0:N2} KiB' -f ($Bytes / [decimal](1KB))) }
    return ('{0:N0} B' -f $Bytes)
}

function Write-AuditLine {
    param([Parameter(Mandatory)][string]$Message)

    $stamp = [DateTime]::UtcNow.ToString('o', [Globalization.CultureInfo]::InvariantCulture)
    Write-Output ('[{0}] {1}' -f $stamp, $Message)
}

function Test-ReparsePoint {
    param([Parameter(Mandatory)][System.IO.FileSystemInfo]$Item)

    return (($Item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
}

function Test-AllowedTarget {
    param(
        [Parameter(Mandatory)][string]$TargetPath,
        [Parameter(Mandatory)][string]$BatchPath,
        [Parameter(Mandatory)][ValidateSet('bin', 'obj')][string]$Kind
    )

    $targetFull = [IO.Path]::GetFullPath($TargetPath)
    $batchFull = [IO.Path]::GetFullPath($BatchPath)
    $expected = [IO.Path]::GetFullPath((Join-Path $batchFull $Kind))
    $batchParent = [IO.Path]::GetFullPath((Split-Path -Parent $batchFull))
    $nativeFull = [IO.Path]::GetFullPath($script:nativeRoot)

    if (-not [string]::Equals($targetFull, $expected, [StringComparison]::OrdinalIgnoreCase)) { return $false }
    if (-not [string]::Equals([IO.Path]::GetFileName($targetFull), $Kind, [StringComparison]::OrdinalIgnoreCase)) { return $false }
    if (-not [string]::Equals($batchParent, $nativeFull, [StringComparison]::OrdinalIgnoreCase)) { return $false }
    return $true
}

function Get-DirectoryStats {
    <#
      This is deliberately a bounded recursive walk: the caller supplies one
      already-whitelisted bin/obj directory.  It does not walk the repository or
      enumerate any sibling/top-level tree.  EnumerateFileSystemInfos is used in
      streaming mode so file entries are not accumulated in memory.
    #>
    param([Parameter(Mandatory)][System.IO.DirectoryInfo]$Directory)

    [decimal]$sizeBytes = 0
    [long]$fileCount = 0
    [long]$directoryCount = 1
    [datetime]$latestMtimeUtc = $Directory.LastWriteTimeUtc
    [bool]$containsReparsePoint = $false
    [string]$scanError = $null

    $options = [System.IO.EnumerationOptions]::new()
    $options.AttributesToSkip = [IO.FileAttributes]::None
    $options.IgnoreInaccessible = $false
    $options.RecurseSubdirectories = $false
    $options.ReturnSpecialDirectories = $false

    $pending = [System.Collections.Generic.Stack[System.IO.DirectoryInfo]]::new()
    $pending.Push($Directory)

    while ($pending.Count -gt 0) {
        $current = $pending.Pop()
        try {
            foreach ($entry in $current.EnumerateFileSystemInfos('*', $options)) {
                if (Test-ReparsePoint -Item $entry) {
                    # A nested link is a safety boundary.  It is not followed and
                    # the containing candidate is disqualified from deletion.
                    $containsReparsePoint = $true
                    continue
                }

                if ($entry.LastWriteTimeUtc -gt $latestMtimeUtc) {
                    $latestMtimeUtc = $entry.LastWriteTimeUtc
                }

                if ($entry -is [System.IO.DirectoryInfo]) {
                    $directoryCount++
                    $pending.Push([System.IO.DirectoryInfo]$entry)
                    continue
                }

                if ($entry -is [System.IO.FileInfo]) {
                    $fileCount++
                    $sizeBytes += [decimal]$entry.Length
                }
            }
        }
        catch {
            $scanError = '{0}: {1}' -f $current.FullName, $_.Exception.Message
            break
        }
    }

    return [pscustomobject]@{
        SizeBytes             = $sizeBytes
        FileCount             = $fileCount
        DirectoryCount        = $directoryCount
        LatestMtimeUtc        = $latestMtimeUtc
        ContainsReparsePoint  = $containsReparsePoint
        ScanError             = $scanError
    }
}

function Get-AvailableFreeBytes {
    param([Parameter(Mandatory)][string]$Path)

    $driveRoot = [IO.Path]::GetPathRoot($Path)
    if ([string]::IsNullOrWhiteSpace($driveRoot)) {
        throw ("Cannot determine the volume for '{0}'." -f $Path)
    }

    $drive = [IO.DriveInfo]::new($driveRoot)
    if (-not $drive.IsReady) {
        throw ("The volume '{0}' is not ready." -f $driveRoot)
    }

    return [long]$drive.AvailableFreeSpace
}

function Get-MarkerFile {
    param(
        [Parameter(Mandatory)][string]$BatchPath,
        [Parameter(Mandatory)][string]$Name
    )

    $markerPath = Join-Path $BatchPath $Name
    $marker = Get-Item -LiteralPath $markerPath -Force -ErrorAction SilentlyContinue
    if ($null -eq $marker -or -not ($marker -is [System.IO.FileInfo])) {
        return $null
    }
    if (Test-ReparsePoint -Item $marker) {
        return $null
    }
    return $marker
}

if (-not (Test-Path -LiteralPath $nativeRoot -PathType Container)) {
    Write-AuditLine ("mode={0} status=NOOP reason=native-build-root-missing root={1}" -f ($(if ($Apply) { 'APPLY' } else { 'DRY-RUN' })), $nativeRoot)
    return
}

$nativeRootItem = Get-Item -LiteralPath $nativeRoot -Force
if (Test-ReparsePoint -Item $nativeRootItem) {
    throw ("Refusing to scan a reparse-point native-build root: {0}" -f $nativeRoot)
}

$whatIfRequested = [bool]$WhatIfPreference
$mode = if (-not $Apply) { 'DRY-RUN' } elseif ($whatIfRequested) { 'WHATIF' } else { 'APPLY' }
Write-AuditLine ("mode={0} root={1} beforeUtc={2:o} maxDelete={3} force={4}" -f $mode, $nativeRoot, $beforeUtc, (Format-Bytes $maxDeleteDecimal), [bool]$Force)
Write-AuditLine 'whitelist=.build/native-build/<batch>/{bin,obj}; batch markers=NativeRun.csproj+build-manifest.json; reparse-points=reject'
Write-AuditLine 'protected=.build/local-reference,.build/decompiled,.build/core-study01,.build/runs,.build/review,.build/pinned-inputs,.build/source-snapshot,.build/compiled,.build/logs,src,tests,.git,licenses,manifests'

$topOptions = [System.IO.EnumerationOptions]::new()
$topOptions.AttributesToSkip = [IO.FileAttributes]::None
$topOptions.IgnoreInaccessible = $false
$topOptions.RecurseSubdirectories = $false
$topOptions.ReturnSpecialDirectories = $false

$candidateRecords = [System.Collections.Generic.List[object]]::new()
$eligibleCandidates = [System.Collections.Generic.List[object]]::new()

try {
    $batchEnumerator = $nativeRootItem.EnumerateDirectories('*', $topOptions)
    foreach ($batch in $batchEnumerator) {
        if (Test-ReparsePoint -Item $batch) {
            Write-AuditLine ("batch-skip path={0} reason=batch-is-reparse-point" -f $batch.FullName)
            continue
        }

        $projectMarker = Get-MarkerFile -BatchPath $batch.FullName -Name 'NativeRun.csproj'
        $manifestMarker = Get-MarkerFile -BatchPath $batch.FullName -Name 'build-manifest.json'
        if ($null -eq $projectMarker -or $null -eq $manifestMarker) {
            $missing = [System.Collections.Generic.List[string]]::new()
            if ($null -eq $projectMarker) { [void]$missing.Add('NativeRun.csproj') }
            if ($null -eq $manifestMarker) { [void]$missing.Add('build-manifest.json') }
            Write-AuditLine ("batch-skip path={0} reason=missing-or-unsafe-marker:{1}" -f $batch.FullName, ($missing -join ','))
            continue
        }

        foreach ($kind in @('bin', 'obj')) {
            $targetPath = Join-Path $batch.FullName $kind
            $target = Get-Item -LiteralPath $targetPath -Force -ErrorAction SilentlyContinue
            if ($null -eq $target) {
                continue
            }

            $record = [ordered]@{
                Path             = [IO.Path]::GetFullPath($targetPath)
                BatchPath        = [IO.Path]::GetFullPath($batch.FullName)
                Kind             = $kind
                SizeBytes        = [decimal]0
                SizeKnown        = $false
                RootMtimeUtc     = $target.LastWriteTimeUtc
                LatestMtimeUtc   = $target.LastWriteTimeUtc
                Eligible         = $false
                Reason           = ''
            }
            $reasonParts = [System.Collections.Generic.List[string]]::new()

            if (-not (Test-AllowedTarget -TargetPath $record.Path -BatchPath $record.BatchPath -Kind $kind)) {
                [void]$reasonParts.Add('path-failed-whitelist-check')
            }
            if (-not ($target -is [System.IO.DirectoryInfo])) {
                [void]$reasonParts.Add('target-is-not-a-directory')
            }
            elseif (Test-ReparsePoint -Item $target) {
                [void]$reasonParts.Add('target-is-reparse-point')
            }
            else {
                $stats = Get-DirectoryStats -Directory ([System.IO.DirectoryInfo]$target)
                $record.SizeBytes = $stats.SizeBytes
                $record.SizeKnown = $true
                $record.LatestMtimeUtc = $stats.LatestMtimeUtc
                if ($stats.ScanError) {
                    [void]$reasonParts.Add(('scan-failed:{0}' -f $stats.ScanError))
                }
                if ($stats.ContainsReparsePoint) {
                    [void]$reasonParts.Add('nested-reparse-point-present')
                }
            }

            if ($record.LatestMtimeUtc -gt $beforeUtc) {
                [void]$reasonParts.Add(('newest-mtime-after-cutoff:{0:o}' -f $record.LatestMtimeUtc))
            }

            if ($reasonParts.Count -eq 0) {
                $record.Eligible = $true
                $record.Reason = 'eligible: direct whitelist path; marker pair present; no reparse points; newest observed mtime is at or before cutoff'
            }
            else {
                $record.Reason = $reasonParts -join '; '
            }

            $candidate = [pscustomobject]$record
            [void]$candidateRecords.Add($candidate)
            $sizeText = if ($candidate.SizeKnown) { Format-Bytes $candidate.SizeBytes } else { 'unavailable' }
            Write-AuditLine ("candidate path={0} size={1} mtimeUtc={2:o} eligible={3} reason={4}" -f $candidate.Path, $sizeText, $candidate.LatestMtimeUtc, $candidate.Eligible, $candidate.Reason)
            if ($candidate.Eligible) {
                [void]$eligibleCandidates.Add($candidate)
            }
        }
    }
}
catch {
    throw ("Top-level native-build enumeration failed: {0}" -f $_.Exception.Message)
}

[decimal]$eligibleBytes = 0
foreach ($candidate in $eligibleCandidates) {
    $eligibleBytes += $candidate.SizeBytes
}

$candidateCount = $candidateRecords.Count
$eligibleCount = $eligibleCandidates.Count
Write-AuditLine ("scan-summary candidates={0} eligible={1} eligibleBytes={2} cutoffUtc={3:o}" -f $candidateCount, $eligibleCount, (Format-Bytes $eligibleBytes), $beforeUtc)

if ($eligibleBytes -gt $softReviewLimitBytes) {
    Write-AuditLine ("review-required reason=eligible-size-exceeds-soft-limit softLimit={0} total={1}" -f (Format-Bytes $softReviewLimitBytes), (Format-Bytes $eligibleBytes))
}

$exceedsConfiguredLimit = $eligibleBytes -gt $maxDeleteDecimal
$exceedsDefaultHardLimit = $eligibleBytes -gt $defaultHardLimitBytes
if ($exceedsConfiguredLimit) {
    Write-AuditLine ("size-guard configured-limit-exceeded limit={0} total={1} action=requires-Force-and-Apply" -f (Format-Bytes $maxDeleteDecimal), (Format-Bytes $eligibleBytes))
}
if ($exceedsDefaultHardLimit) {
    Write-AuditLine ("size-guard default-5GiB-limit-exceeded limit={0} total={1} action=requires-Force-and-Apply" -f (Format-Bytes $defaultHardLimitBytes), (Format-Bytes $eligibleBytes))
}

if (-not $Apply) {
    Write-AuditLine ("summary mode=DRY-RUN status=NO_DELETE candidates={0} eligible={1} wouldDelete={2} reason=Apply-switch-required" -f $candidateCount, $eligibleCount, (Format-Bytes $eligibleBytes))
    return
}

if (($exceedsConfiguredLimit -or $exceedsDefaultHardLimit) -and -not $Force) {
    Write-AuditLine 'summary mode=APPLY status=BLOCKED candidates-not-deleted reason=size-guard; rerun the same review with explicit -Force after manual review'
    return
}

if ($whatIfRequested) {
    foreach ($candidate in $eligibleCandidates) {
        [void]$PSCmdlet.ShouldProcess($candidate.Path, ('Delete {0}' -f (Format-Bytes $candidate.SizeBytes)))
    }
    Write-AuditLine ("summary mode=WHATIF status=NO_DELETE candidates={0} eligible={1} wouldDelete={2}" -f $candidateCount, $eligibleCount, (Format-Bytes $eligibleBytes))
    return
}

$freeBeforeBytes = Get-AvailableFreeBytes -Path $repoRoot
Write-AuditLine ("disk-check beforeDelete free={0} volume={1}" -f (Format-Bytes ([decimal]$freeBeforeBytes)), [IO.Path]::GetPathRoot($repoRoot))

[decimal]$deletedBytes = 0
[int]$deletedCount = 0
[int]$confirmationStop = 0
$deleteFailure = $null

foreach ($candidate in $eligibleCandidates) {
    try {
        # Re-check the exact direct-child path and its markers immediately before
        # removal.  A changed/replaced target is a hard stop, never a retry.
        $currentBatch = Get-Item -LiteralPath $candidate.BatchPath -Force -ErrorAction Stop
        $currentTarget = Get-Item -LiteralPath $candidate.Path -Force -ErrorAction Stop
        if (-not ($currentBatch -is [System.IO.DirectoryInfo]) -or (Test-ReparsePoint -Item $currentBatch)) {
            throw ("Batch changed or became unsafe: {0}" -f $candidate.BatchPath)
        }
        if (-not ($currentTarget -is [System.IO.DirectoryInfo]) -or (Test-ReparsePoint -Item $currentTarget)) {
            throw ("Target changed or became unsafe: {0}" -f $candidate.Path)
        }
        if (-not (Test-AllowedTarget -TargetPath $candidate.Path -BatchPath $candidate.BatchPath -Kind $candidate.Kind)) {
            throw ("Target failed the whitelist re-check: {0}" -f $candidate.Path)
        }
        if ($null -eq (Get-MarkerFile -BatchPath $candidate.BatchPath -Name 'NativeRun.csproj') -or
            $null -eq (Get-MarkerFile -BatchPath $candidate.BatchPath -Name 'build-manifest.json')) {
            throw ("Required provenance markers changed or disappeared: {0}" -f $candidate.BatchPath)
        }
        if ($currentTarget.LastWriteTimeUtc -ne $candidate.RootMtimeUtc) {
            throw ("Target mtime changed after review; refusing deletion: {0}" -f $candidate.Path)
        }

        if (-not $PSCmdlet.ShouldProcess($candidate.Path, ('Delete {0}' -f (Format-Bytes $candidate.SizeBytes)))) {
            $confirmationStop = 1
            Write-AuditLine ("delete-stop path={0} reason=ShouldProcess-declined" -f $candidate.Path)
            break
        }

        Remove-Item -LiteralPath $candidate.Path -Recurse -Force -ErrorAction Stop
        $deletedBytes += $candidate.SizeBytes
        $deletedCount++
        Write-AuditLine ("deleted path={0} estimatedBytes={1}" -f $candidate.Path, (Format-Bytes $candidate.SizeBytes))
    }
    catch {
        $deleteFailure = $_
        Write-AuditLine ("delete-failure path={0} error={1} action=stopped-no-retry" -f $candidate.Path, $_.Exception.Message)
        break
    }
}

$freeAfterBytes = $null
try {
    $freeAfterBytes = Get-AvailableFreeBytes -Path $repoRoot
    $observedDelta = [decimal]$freeAfterBytes - [decimal]$freeBeforeBytes
    Write-AuditLine ("disk-check afterDelete free={0} observedFreeDelta={1}" -f (Format-Bytes ([decimal]$freeAfterBytes)), (Format-Bytes $observedDelta))
}
catch {
    Write-AuditLine ("disk-check afterDelete status=unavailable error={0}" -f $_.Exception.Message)
}

if ($null -ne $deleteFailure) {
    Write-AuditLine ("summary mode=APPLY status=FAILED candidates={0} eligible={1} deleted={2} estimatedReleased={3} action=stopped-no-retry" -f $candidateCount, $eligibleCount, $deletedCount, (Format-Bytes $deletedBytes))
    throw $deleteFailure.Exception
}

if ($confirmationStop -eq 1) {
    Write-AuditLine ("summary mode=APPLY status=CONFIRMATION-STOPPED candidates={0} eligible={1} deleted={2} estimatedReleased={3}" -f $candidateCount, $eligibleCount, $deletedCount, (Format-Bytes $deletedBytes))
    return
}

Write-AuditLine ("summary mode=APPLY status=SUCCEEDED candidates={0} eligible={1} deleted={2} estimatedReleased={3}" -f $candidateCount, $eligibleCount, $deletedCount, (Format-Bytes $deletedBytes))
