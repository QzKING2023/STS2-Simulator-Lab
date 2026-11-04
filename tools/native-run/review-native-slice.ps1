param([Parameter(Mandatory)][string]$RunDirectory)
$ErrorActionPreference='Stop'
$report=Get-Content (Join-Path $RunDirectory 'result.json') -Raw|ConvertFrom-Json
$process=Get-Content (Join-Path $RunDirectory 'process.json') -Raw|ConvertFrom-Json
$events=@(Get-Content (Join-Path $RunDirectory 'events.json') -Raw|ConvertFrom-Json)
$route=@(Get-Content (Join-Path $RunDirectory 'route.json') -Raw|ConvertFrom-Json)
$checks=[Collections.Generic.List[object]]::new()
function Check($name,$passed){$checks.Add(@{name=$name;passed=[bool]$passed})}
Check 'raw exit and independent result agree' ($process.rawExit-eq0 -and $process.exitConsistent -and $report.rawExit-eq0)
Check 'actual ongoing slice, no simulated outcome invented' ($report.result.Schema-eq'native-run-result/v1' -and $report.result.Stop-eq'SliceBoundary' -and $report.result.Terminal-eq'Ongoing' -and $report.result.Comparison-eq'NotCompared')
Check 'nonempty confirmed prefix matches actual route and dispatches' ($route.Count-gt5 -and $route.Count-eq$report.result.Confirmed -and $report.result.Dispatched-eq$route.Count -and @($events|Where-Object kind -eq 'confirmed').Count-eq$route.Count -and @($events|Where-Object kind -eq 'dispatch').Count-eq$route.Count)
$seqOK=$true;for($i=0;$i-lt$events.Count;$i++){if($events[$i].seq-ne$i+1){$seqOK=$false}}
Check 'event sequence continuous' $seqOK
$last=0;foreach($kind in @('new-run','opening-finished','first-battle-opening','battle-victory-before-cleanup','battle-victory-cleanup','reward-offer','rewards-completed','next-combat-first-play')){
 $entry=@($events|Where-Object kind -eq $kind)
 Check ('actual lifecycle '+$kind) ($entry.Count-eq1 -and $entry[0].seq-gt$last)
 if($entry.Count){$last=$entry[0].seq}
}
$start=($events|Where-Object kind -eq 'new-run').state
Check 'independent Ironclad starting contract' ($start.hp-eq80 -and $start.maxHp-eq80 -and $start.gold-eq99 -and $start.deck.Count-eq10 -and @($start.deck|Where-Object model -eq 'CARD.STRIKE_IRONCLAD').Count-eq5 -and @($start.deck|Where-Object model -eq 'CARD.DEFEND_IRONCLAD').Count-eq4 -and @($start.deck|Where-Object model -eq 'CARD.BASH').Count-eq1 -and $start.relics[0].model-eq'RELIC.BURNING_BLOOD')
Check 'Neow native options executed' (@($route|Where-Object {$_.action.kind-eq'EventOption' -and $_.action.decision.model-eq'EVENT.NEOW'}).Count-gt0)
Check 'real enemy phase executed' (@($route|Where-Object {$_.action.kind-eq'EndTurn'}).Count-gt0)
Check 'history captured before cleanup' (@($events|Where-Object kind -eq 'combat-history').Count-gt0 -and @($events|Where-Object {$_.kind-eq'combat-history' -and $_.seq-lt($events|Where-Object kind -eq 'battle-victory-cleanup').seq}).Count-gt0)
$gold=@(($events|Where-Object kind -eq 'reward-offer').detail|Where-Object kind -eq 'GoldReward')
$reward=($events|Where-Object kind -eq 'rewards-completed').detail
Check 'native gold amount matches actual resource delta' ($gold.Count-eq1 -and $reward.goldAfter-$reward.goldBefore-eq$gold[0].amount -and $gold[0].amount-gt0)
$before=($events|Where-Object kind -eq 'rewards-completed').state
$after=($events|Where-Object kind -eq 'next-combat-first-play').state
Check 'native next combat at Play and resources persist' ($after.room-eq'CombatRoom' -and $after.combat.phase-eq'Play' -and $after.gold-eq$before.gold -and $after.hp-eq$before.hp -and ($after.deck|ConvertTo-Json -Depth 8 -Compress)-eq($before.deck|ConvertTo-Json -Depth 8 -Compress) -and ($after.relics|ConvertTo-Json -Depth 8 -Compress)-eq($before.relics|ConvertTo-Json -Depth 8 -Compress))
Check 'two distinct ordinary map travels' (@($route|Where-Object {$_.action.kind-eq'Travel'}).Count-eq2 -and $after.node-ne($events|Where-Object kind -eq 'first-battle-opening').state.node)
Check 'no Mod or solver assembly loaded' (@($report.loadedAssemblies|Where-Object {$_-in@('MyMod','CombatSolver','BaseLib') -or $_-like'STS2-RitsuLib*'}).Count-eq0)
Check 'async fault gate quiet in actual run' ($report.guards.faults.Count-eq0)
Check 'progress and replay intercepted' ($report.guards.ProgressWrites-gt0 -and $report.guards.ReplayWrites-gt0)
$failed=@($checks|Where-Object {!$_.passed})
@{schema='native-slice-evidence-review/v1';checks=$checks;count=$checks.Count;failed=$failed.Count;buildId=$process.buildId}|ConvertTo-Json -Depth 6|Set-Content (Join-Path $RunDirectory 'evidence-review.json') -Encoding utf8
if($failed.Count){$failed|ConvertTo-Json;exit 1}
Write-Output "PASS $($checks.Count) native slice evidence checks";exit 0
