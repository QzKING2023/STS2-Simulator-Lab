param([Parameter(Mandatory)][string]$HarnessRoot,[Parameter(Mandatory)][string]$OutputDirectory,[string]$OnlyCase='')
$ErrorActionPreference='Stop'
$lab=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'));$out=[IO.Path]::GetFullPath($OutputDirectory)
if(-not$out.StartsWith((Join-Path $lab '.build/runs')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Use Lab runs.'}
if(Test-Path $out){throw 'Use fresh evidence directory.'}
New-Item -ItemType Directory -Path $out|Out-Null
$cases=@(foreach($u in 0,1){foreach($m in 0,1,2){"ReturnFlow-$u-$m-multiple"}})
$cases+=@('ReturnFlow-0-1-empty','ReturnFlow-0-1-single','ReturnFlow-0-1-draw','ReturnFlow-0-1-discard','ReturnFlow-0-1-full','ReturnFlow-0-1-variant')
$cases+=@('ReturnFlow-0-1-duplicates','ReturnFlow-0-0-shield','ReturnFlow-0-1-shield','ReturnFlow-0-0-mentor','ReturnFlow-0-1-mentor','ReturnFlow-0-1-reverse')
$cases+=@(foreach($u in 0,1){foreach($layout in 'multiple','single','eternal','empty','variant'){"ManaConversionPrinciple-$u-1-$layout"}})
$cases+=@(foreach($u in 0,1){foreach($layout in 'multiple','shuffle','empty','full'){"ManaEssence-$u-1-$layout"}})
$cases+=@('ManaEssence-0-1-nodraw','ManaEssence-1-1-mentor')
$cases+=@('ReturnFlow-0-0-complete','ReturnFlow-0-1-complete','ReturnFlow-0-1-solver','ManaConversionPrinciple-0-1-solver')
$cases+=@('ReturnFlow-0-1-identity','ManaConversionPrinciple-0-1-identity','ManaConversionPrinciple-1-1-identity')
if($OnlyCase){$cases=@($OnlyCase)}
$cases|ConvertTo-Json|Set-Content (Join-Path $out 'case-declaration.json') -Encoding utf8
$results=@();$checks=@();$reports=@{}
foreach($case in $cases){foreach($repeat in 'a','b'){
 $id="$case-$repeat";$dir=Join-Path $out $id
 & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'run-stage-one-case.ps1') -HarnessRoot $HarnessRoot -Case reward-boundary -RewardCase $case -OutputDirectory $dir *> (Join-Path $out "$id.log")
 $code=$LASTEXITCODE;$path=Join-Path $dir 'reward-boundary.json';$r=if(Test-Path $path){Get-Content $path -Raw|ConvertFrom-Json}else{$null};$reports[$id]=$r
 $results+=@{id=$id;exit=$code;status=$r.status;path=$path};Write-Output "$id exit=$code status=$($r.status)"
}}
foreach($case in $cases){$a=$reports["$case-a"];$b=$reports["$case-b"]
 $aSteps=@($a.replays|ForEach-Object{$_.steps});$bSteps=@($b.replays|ForEach-Object{$_.steps})
 $checks+=@{id="$case-repeat";passed=($null-ne$a-and$null-ne$b-and($a.action|ConvertTo-Json -Depth 100 -Compress)-ceq($b.action|ConvertTo-Json -Depth 100 -Compress)-and($aSteps|ConvertTo-Json -Depth 100 -Compress)-ceq($bSteps|ConvertTo-Json -Depth 100 -Compress))}
}
$okay=@($results|Where-Object{$_.exit-ne0-or$_.status-ne'passed'}).Count-eq0-and@($checks|Where-Object{-not$_.passed}).Count-eq0
@{schema='reward-card-boundaries/v1';status=if($okay){'passed'}else{'failed'};cases=$results;checks=$checks}|ConvertTo-Json -Depth 8|Set-Content (Join-Path $out 'summary.json') -Encoding utf8
if(-not$okay){exit 1}
