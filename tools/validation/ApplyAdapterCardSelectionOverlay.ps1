param([Parameter(Mandatory)][string]$TargetRoot)
$ErrorActionPreference='Stop'
$target=Join-Path $TargetRoot 'src/Search/AdapterCardSelection.cs'
if(Test-Path -LiteralPath $target){throw 'Selection overlay must be installed once into a fresh cache.'}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'AdapterCardSelection.cs') -Destination $target
$path=Join-Path $TargetRoot 'src/Search/SimulatedCombatState.ActionChoices.cs'
$text=[IO.File]::ReadAllText($path)
$anchor='        ClearPendingTurnStartChoice();'
$index=$text.IndexOf($anchor)
if($index-lt0){throw 'Missing pinned BeginActionChoices anchor.'}
$text=$text.Insert($index,"        AdapterSelections.Clear();`r`n")
[IO.File]::WriteAllText($path,$text,[Text.UTF8Encoding]::new($false))
