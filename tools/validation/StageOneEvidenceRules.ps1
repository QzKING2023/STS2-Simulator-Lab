function Test-ConfirmedBoundary($replay,[int]$expectedActions=1) {
    if($null-eq$replay-or$replay.status-ne'boundary'-or$expectedActions-le0-or$replay.steps.Count-ne$expectedActions){return $false}
    if($replay.outcome.ConfirmedActions-ne$expectedActions-or$replay.outcome.NativeOutcome-ne1-or$replay.outcome.SimulatorOutcome-ne1-or$replay.outcome.Comparison-ne1-or-not$replay.outcome.VerificationPassed){return $false}
    foreach($step in $replay.steps){if(-not$step.exactStateMatch-or-not$step.nativeAccepted-or-not$step.simulatedAccepted-or-not$step.parentBranchUnchanged-or$step.nativeContinuation-cne$step.simulatorContinuation){return $false}}
    return $true
}
