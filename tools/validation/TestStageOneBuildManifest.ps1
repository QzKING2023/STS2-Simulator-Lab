param([Parameter(Mandatory)][string]$BuildManifest)
$ErrorActionPreference='Stop';$lock=Get-Content -LiteralPath $BuildManifest -Raw|ConvertFrom-Json
$body=$lock.manifest|ConvertTo-Json -Depth 7 -Compress
$digest=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($body)))
if($digest-ne$lock.buildId){throw 'Build manifest identity mismatch.'}
$cache=[IO.Path]::GetFullPath((Join-Path $lock.manifest.harness '../../..'))
foreach($entry in @(@{rows=$lock.manifest.source;root=$lock.manifest.labRoot},@{rows=$lock.manifest.artifacts;root=$lock.manifest.labRoot},@{rows=$lock.manifest.inputs;root=$lock.manifest.labRoot},@{rows=$lock.manifest.compiledSource;root=$cache})){
 foreach($f in $entry.rows){if((Get-FileHash -LiteralPath (Join-Path $entry.root $f.path)).Hash-ne$f.sha256){throw "Frozen build/input/source changed: $($f.path)"}}
}
Write-Output "Verified frozen build $($lock.buildId)"
