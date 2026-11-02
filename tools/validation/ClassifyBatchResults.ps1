# Pure aggregation; also exercised with labelled in-memory negative controls.
function Get-BatchClassification($Cases,$Reports,$ExpectedIds,$KnownGaps,$Checks) {
    $states=@('passed','death','unsupported','budget-exceeded','no-candidate','invalid-action','engine-error','failed','differential-mismatch','environment-blocked','cancelled','interrupted','not-run','unknown')
    $counts=[ordered]@{};foreach($s in $states){$counts[$s]=0}
    $errors=[Collections.Generic.List[string]]::new();$incomplete=0
    $ids=@($Cases|ForEach-Object {$_.id})
    if($ids.Count-ne$ExpectedIds.Count-or@($ids|Sort-Object -Unique).Count-ne$ExpectedIds.Count-or@($ExpectedIds|Where-Object {$_-notin$ids}).Count){$errors.Add('Missing, duplicate or unexpected case identity.')}
    foreach($case in $Cases){
        $status=[string]$case.status;$r=$Reports[$case.id]
        if($status-notin$states-or$status-eq'unknown'){$counts.unknown++;$errors.Add("Unknown status: $($case.id):$status");continue}
        $counts[$status]++
        if($null-eq$r-or$r.status-cne$status){$errors.Add("Missing or inconsistent report: $($case.id)");continue}
        $expectedExit=if($status-eq'passed'){0}else{1}
        if($null-eq$case.exit-or$case.exit-ne$expectedExit){$errors.Add("Raw process exit mismatch: $($case.id)")}
        $stopCodes=@{'passed'=0;'death'=0;'unsupported'=2;'budget-exceeded'=1;'no-candidate'=9;'invalid-action'=3;'engine-error'=4;'differential-mismatch'=5;'environment-blocked'=6;'cancelled'=7;'interrupted'=8}
        if($stopCodes.ContainsKey($status)-and($null-eq$r.outcome-or$r.outcome.StopReason-ne$stopCodes[$status])){$errors.Add("Typed outcome contradicts status: $($case.id)")}
        switch($status){
            'passed' {if(-not$r.outcome.VerificationPassed-or$r.outcome.NativeOutcome-ne2-or$r.outcome.SimulatorOutcome-ne2-or$r.outcome.Comparison-ne1-or$r.outcome.Coverage-ne1){$errors.Add("Unverified terminal pass: $($case.id)")}}
            'death' {if(-not$r.outcome.VerificationPassed-or$r.outcome.NativeOutcome-ne3-or$r.outcome.SimulatorOutcome-ne3-or$r.outcome.Comparison-ne1){$errors.Add("Unverified Death: $($case.id)")}}
            'unsupported' {
                $gap=@($KnownGaps|Where-Object {$_.id-ceq$case.id})
                if($gap.Count-ne1-or$r.phase-cne$gap[0].phase-or$r.reason-notlike('*'+$gap[0].reasonToken+'*')-or$r.outcome.Coverage-ne2-or$r.outcome.StopReason-ne2){$errors.Add("Undeclared or inconsistent unsupported: $($case.id)")}
            }
            {$_-in'budget-exceeded','no-candidate','environment-blocked','cancelled','interrupted'} {$incomplete++}
            default {$errors.Add("Execution or verification failure: $($case.id):$status")}
        }
    }
    $sum=0;foreach($n in $counts.Values){$sum+=$n};if($sum-ne$Cases.Count){$errors.Add('Outcome denominator mismatch.')}
    if(@($Checks|Where-Object {-not$_.passed}).Count){$errors.Add('A semantic check failed.')}
    $integrity=$errors.Count-eq0;$complete=$integrity-and$incomplete-eq0
    [ordered]@{counts=$counts;caseCount=$Cases.Count;countedPaths=$sum;errors=@($errors);executionIntegrityPassed=$integrity;
        correctnessChecksPassed=$complete;incompletePaths=$incomplete;
        status=if(-not$integrity){'failed'}elseif($incomplete){'incomplete'}elseif($counts.unsupported){'completed-with-unsupported'}else{'completed'};
        exitCode=if(-not$integrity){1}elseif($incomplete){3}elseif($counts.unsupported){2}else{0}}
}
