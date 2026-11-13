#requires -Version 7.0

<#
.SYNOPSIS
Reviews and removes stale, unreferenced native build and game staging output.

.DESCRIPTION
This is the repository-level cleanup boundary.  It deliberately does not
touch .build/runs, local-mods, pinned inputs, source trees, or Git metadata.
Only direct children of the two generated-output roots are candidates, and a
tracked/evidence reference keeps a directory alive.  The reference scan also
reads ignored `.build/runs` files so append-only evidence can keep a staging
root alive.  A plain invocation is a review; -Apply is required for deletion.

The default retention window is fourteen days.  The current cleanup can use a
shorter explicit -BeforeUtc after reviewing the printed candidate list.
#>

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [switch]$Apply,
    [switch]$Force,
    [ValidateRange(1, 3650)][int]$RetentionDays = 14,
    [datetime]$BeforeUtc = [DateTime]::UtcNow.AddDays(-$RetentionDays),
    [ValidateRange(1, 100)][int]$MaxDeleteGiB = 10,
    [switch]$SkipNativeBuildBinObj
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$buildRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot '.build'))
$nativeBuildRoot = [IO.Path]::GetFullPath((Join-Path $buildRoot 'native-build'))
$nativeGameRoot = [IO.Path]::GetFullPath((Join-Path $buildRoot 'native-game'))
$nowUtc = [DateTime]::UtcNow
$cutoffUtc = $BeforeUtc.ToUniversalTime()
$maxDeleteBytes = [decimal]$MaxDeleteGiB * [decimal](1GB)

if ($cutoffUtc -gt $nowUtc) {
    throw "-BeforeUtc cannot be in the future. Received $cutoffUtc; current UTC is $nowUtc."
}

function Format-Bytes {
    param([decimal]$Bytes)

    if ($Bytes -ge [decimal](1TB)) { return '{0:N2} TiB' -f ($Bytes / [decimal](1TB)) }
    if ($Bytes -ge [decimal](1GB)) { return '{0:N2} GiB' -f ($Bytes / [decimal](1GB)) }
    if ($Bytes -ge [decimal](1MB)) { return '{0:N2} MiB' -f ($Bytes / [decimal](1MB)) }
    if ($Bytes -ge [decimal](1KB)) { return '{0:N2} KiB' -f ($Bytes / [decimal](1KB)) }
    return '{0:N0} B' -f $Bytes
}

function Test-ReparsePoint {
    param([Parameter(Mandatory)][IO.FileSystemInfo]$Item)
    return (($Item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
}

function Get-DirectoryStats {
    param([Parameter(Mandatory)][IO.DirectoryInfo]$Directory)

    [decimal]$logicalBytes = 0
    [decimal]$nonHardlinkBytes = 0
    [long]$fileCount = 0
    [datetime]$latestUtc = $Directory.LastWriteTimeUtc
    [bool]$containsReparsePoint = $false
    $pending = [Collections.Generic.Stack[IO.DirectoryInfo]]::new()
    $pending.Push($Directory)

    while ($pending.Count -gt 0) {
        $current = $pending.Pop()
        foreach ($entry in $current.EnumerateFileSystemInfos('*', [IO.EnumerationOptions]@{
            AttributesToSkip = [IO.FileAttributes]::None
            IgnoreInaccessible = $false
            RecurseSubdirectories = $false
            ReturnSpecialDirectories = $false
        })) {
            if (Test-ReparsePoint $entry) {
                $containsReparsePoint = $true
                continue
            }
            if ($entry.LastWriteTimeUtc -gt $latestUtc) { $latestUtc = $entry.LastWriteTimeUtc }
            if ($entry -is [IO.DirectoryInfo]) {
                $pending.Push([IO.DirectoryInfo]$entry)
                continue
            }
            $file = [IO.FileInfo]$entry
            $fileCount++
            $logicalBytes += [decimal]$file.Length
            if ($file.LinkType -ne 'HardLink') { $nonHardlinkBytes += [decimal]$file.Length }
        }
    }

    return [pscustomobject]@{
        LogicalBytes = $logicalBytes
        NonHardlinkBytes = $nonHardlinkBytes
        FileCount = $fileCount
        LatestMtimeUtc = $latestUtc
        ContainsReparsePoint = $containsReparsePoint
    }
}

function Get-ReferenceSets {
    $nativeBuild = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $nativeGame = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $buildChildren = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $searchRoots = @('docs', 'tools', 'tests', 'src', 'README.md', '.build/runs') |
        ForEach-Object { Join-Path $repoRoot $_ }
    $textExtensions = @('.json', '.md', '.txt', '.log', '.csv', '.xml', '.props', '.cs', '.ps1', '.psm1', '.csproj')
    $buildPattern = [regex]::new('native-build[/\\]([A-Za-z0-9._-]+)', [Text.RegularExpressions.RegexOptions]::IgnoreCase)
    $gamePattern = [regex]::new('native-game[/\\]([A-Za-z0-9._-]+)', [Text.RegularExpressions.RegexOptions]::IgnoreCase)
    $childPattern = [regex]::new('\.build[/\\]([A-Za-z0-9._-]+)(?:[/\\]|$)', [Text.RegularExpressions.RegexOptions]::IgnoreCase)

    foreach ($root in $searchRoots) {
        $files = if (Test-Path -LiteralPath $root -PathType Leaf) {
            @(Get-Item -LiteralPath $root -Force)
        }
        elseif (Test-Path -LiteralPath $root -PathType Container) {
            @(Get-ChildItem -LiteralPath $root -File -Recurse -Force -ErrorAction SilentlyContinue |
                Where-Object { $_.Extension.ToLowerInvariant() -in $textExtensions })
        }
        else { @() }

        foreach ($file in $files) {
            try {
                foreach ($line in [IO.File]::ReadLines($file.FullName)) {
                    foreach ($match in $buildPattern.Matches($line)) { [void]$nativeBuild.Add($match.Groups[1].Value) }
                    foreach ($match in $gamePattern.Matches($line)) { [void]$nativeGame.Add($match.Groups[1].Value) }
                    foreach ($match in $childPattern.Matches($line)) { [void]$buildChildren.Add($match.Groups[1].Value) }
                }
            }
            catch {
                # A locked or malformed diagnostic file cannot grant a keep
                # reference; protected roots are excluded independently.
            }
        }
    }
    return [pscustomobject]@{ NativeBuild = $nativeBuild; NativeGame = $nativeGame; BuildChildren = $buildChildren }
}

function New-Candidate {
    param(
        [Parameter(Mandatory)][ValidateSet('native-build', 'native-game', 'transient')][string]$Kind,
        [Parameter(Mandatory)][IO.DirectoryInfo]$Item,
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)]$Stats
    )

    return [pscustomobject]@{
        Kind = $Kind
        Name = $Item.Name
        Path = [IO.Path]::GetFullPath($Item.FullName)
        RelativePath = $RelativePath
        LogicalBytes = [decimal]$Stats.LogicalBytes
        NonHardlinkBytes = [decimal]$Stats.NonHardlinkBytes
        FileCount = [long]$Stats.FileCount
        LatestMtimeUtc = $Stats.LatestMtimeUtc
    }
}

if (-not (Test-Path -LiteralPath $buildRoot -PathType Container)) {
    Write-Output "workspace-cleanup status=NOOP reason=build-root-missing path=$buildRoot"
    return
}

$references = Get-ReferenceSets
$nativeBuildRefs = $references.NativeBuild
$nativeGameRefs = $references.NativeGame
$candidates = [Collections.Generic.List[object]]::new()
$skippedReferenced = 0
$skippedRecent = 0
$skippedUnsafe = 0

if (Test-Path -LiteralPath $nativeBuildRoot -PathType Container) {
    foreach ($item in Get-ChildItem -LiteralPath $nativeBuildRoot -Directory -Force) {
        if ($item.Name -eq 'cache' -or (Test-ReparsePoint $item)) {
            $skippedUnsafe++
            continue
        }
        $stats = Get-DirectoryStats ([IO.DirectoryInfo]$item.FullName)
        $isStaging = $item.Name.StartsWith('.staging-', [StringComparison]::OrdinalIgnoreCase)
        $hasMarkers = (Test-Path -LiteralPath (Join-Path $item.FullName '.ready') -PathType Leaf) -and
            (Test-Path -LiteralPath (Join-Path $item.FullName 'build-manifest.json') -PathType Leaf)
        $referenced = $nativeBuildRefs.Contains($item.Name)
        if ($referenced) { $skippedReferenced++; continue }
        if ($stats.ContainsReparsePoint -or (!$isStaging -and !$hasMarkers)) {
            $skippedUnsafe++
            continue
        }
        if ($stats.LatestMtimeUtc -gt $cutoffUtc) {
            $skippedRecent++
            continue
        }
        [void]$candidates.Add((New-Candidate -Kind 'native-build' -Item $item -RelativePath ('.build/native-build/{0}' -f $item.Name) -Stats $stats))
    }
}

if (Test-Path -LiteralPath $nativeGameRoot -PathType Container) {
    $gameRunning = @(
        Get-Process -Name 'SlayTheSpire2' -ErrorAction SilentlyContinue
    ).Count -gt 0
    foreach ($item in Get-ChildItem -LiteralPath $nativeGameRoot -Directory -Force) {
        if ($item.Name -in @('appdata', 'localappdata') -or (Test-ReparsePoint $item)) {
            $skippedUnsafe++
            continue
        }
        if ($gameRunning) {
            $skippedUnsafe++
            continue
        }
        $stats = Get-DirectoryStats ([IO.DirectoryInfo]$item.FullName)
        if ($nativeGameRefs.Contains($item.Name)) { $skippedReferenced++; continue }
        if ($stats.ContainsReparsePoint) { $skippedUnsafe++; continue }
        if ($stats.LatestMtimeUtc -gt $cutoffUtc) { $skippedRecent++; continue }
        [void]$candidates.Add((New-Candidate -Kind 'native-game' -Item $item -RelativePath ('.build/native-game/{0}' -f $item.Name) -Stats $stats))
    }
}

# These names are throwaway inspection/build scratch, never evidence.
$transientPattern = '^(tmp-|alc-|ilspy-|inspect(?:$|-)|metadata-|load-|godot-scriptbridge|unsafe-test$|phase0-verify$|dotnet-tools$|.*-validation$)'
foreach ($item in Get-ChildItem -LiteralPath $buildRoot -Force) {
    if ($item.Name -in @('native-build', 'native-game', 'runs', 'diag', 'pinned-inputs') -or
        $item.Name -match '^cleanup-') { continue }
    if ($item.Name -notmatch $transientPattern) { continue }
    if (Test-ReparsePoint $item) { $skippedUnsafe++; continue }
    $relativePath = '.build/{0}' -f $item.Name
    $needle = "$relativePath/"
    $referenced = $references.BuildChildren.Contains($item.Name)
    if ($referenced) { $skippedReferenced++; continue }
    $latestUtc = $item.LastWriteTimeUtc
    if ($item -is [IO.DirectoryInfo]) {
        $stats = Get-DirectoryStats ([IO.DirectoryInfo]$item.FullName)
        $latestUtc = $stats.LatestMtimeUtc
        if ($stats.ContainsReparsePoint) { $skippedUnsafe++; continue }
        $logicalBytes = $stats.LogicalBytes
        $nonHardlinkBytes = $stats.NonHardlinkBytes
        $fileCount = $stats.FileCount
    }
    else {
        $logicalBytes = [decimal]$item.Length
        $nonHardlinkBytes = $logicalBytes
        $fileCount = 1
    }
    if ($latestUtc -gt $cutoffUtc) { $skippedRecent++; continue }
    [void]$candidates.Add([pscustomobject]@{
        Kind = 'transient'; Name = $item.Name; Path = [IO.Path]::GetFullPath($item.FullName)
        RelativePath = $relativePath; LogicalBytes = [decimal]$logicalBytes
        NonHardlinkBytes = [decimal]$nonHardlinkBytes; FileCount = [long]$fileCount
        LatestMtimeUtc = $latestUtc
    })
}

$candidateBytes = [decimal]0
foreach ($candidate in $candidates) { $candidateBytes += $candidate.LogicalBytes }
$mode = if ($Apply) { 'APPLY' } else { 'DRY-RUN' }
Write-Output "workspace-cleanup mode=$mode cutoffUtc=$cutoffUtc nativeBuildRefs=$($nativeBuildRefs.Count) nativeGameRefs=$($nativeGameRefs.Count)"
Write-Output "workspace-cleanup candidates=$($candidates.Count) logical=$([string](Format-Bytes $candidateBytes)) skippedReferenced=$skippedReferenced skippedRecent=$skippedRecent skippedUnsafe=$skippedUnsafe"
foreach ($candidate in $candidates | Sort-Object Kind, Path) {
    Write-Output ("candidate kind={0} path={1} logical={2} nonHardlink={3} latestUtc={4:o}" -f
        $candidate.Kind, $candidate.Path, (Format-Bytes $candidate.LogicalBytes),
        (Format-Bytes $candidate.NonHardlinkBytes), $candidate.LatestMtimeUtc)
}

$auditPath = Join-Path $buildRoot 'cleanup-workspace-last.json'
$audit = [ordered]@{
    schema = 'workspace-cleanup/v1'
    mode = $mode
    generatedUtc = $nowUtc.ToString('o')
    cutoffUtc = $cutoffUtc.ToString('o')
    candidates = @($candidates | Select-Object Kind, Name, RelativePath, LogicalBytes, NonHardlinkBytes, FileCount, LatestMtimeUtc)
    protected = @('.build/runs', '.build/pinned-inputs', 'local-mods', 'src', 'tests', '.git')
}
($audit | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $auditPath -Encoding utf8

if (-not $Apply) {
    Write-Output 'workspace-cleanup summary status=NO_DELETE reason=Apply-switch-required'
    return
}

if ($candidateBytes -gt $maxDeleteBytes -and -not $Force) {
    throw "Candidate size $([string](Format-Bytes $candidateBytes)) exceeds MaxDeleteGiB=$MaxDeleteGiB; rerun with -Force after review."
}

if (-not $SkipNativeBuildBinObj) {
    $helper = Join-Path $PSScriptRoot 'cleanup-native-build.ps1'
    $helperLog = Join-Path $buildRoot 'cleanup-native-build-from-workspace.log'
    & pwsh -NoProfile -File $helper -Apply -Confirm:$false -Before $cutoffUtc *> $helperLog
    if ($LASTEXITCODE -ne 0) { throw "cleanup-native-build.ps1 failed; see $helperLog" }
}

[decimal]$deletedBytes = 0
[int]$deletedCount = 0
foreach ($candidate in $candidates) {
    $current = Get-Item -LiteralPath $candidate.Path -Force -ErrorAction Stop
    if ((Test-ReparsePoint $current) -or
        ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($candidate.Path)) -notin @($nativeBuildRoot, $nativeGameRoot, $buildRoot))) {
        throw "Candidate changed or failed direct-child safety check: $($candidate.Path)"
    }
    if (-not $PSCmdlet.ShouldProcess($candidate.Path, ('Delete {0}' -f (Format-Bytes $candidate.LogicalBytes)))) {
        throw "Deletion confirmation declined: $($candidate.Path)"
    }
    Remove-Item -LiteralPath $candidate.Path -Recurse -Force -ErrorAction Stop
    $deletedBytes += $candidate.LogicalBytes
    $deletedCount++
}

Write-Output "workspace-cleanup summary status=SUCCEEDED deleted=$deletedCount logicalReleased=$([string](Format-Bytes $deletedBytes)) helperLog=$helperLog"
