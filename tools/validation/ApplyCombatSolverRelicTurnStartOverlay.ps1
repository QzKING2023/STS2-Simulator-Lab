param(
    [Parameter(Mandatory = $true)]
    [string]$SourceRoot,
    [Parameter(Mandatory = $true)]
    [string]$AdapterSource
)

$ErrorActionPreference = 'Stop'

function Replace-ExactlyOnce([string]$Text, [string]$Old, [string]$New, [string]$Label) {
    $first = $Text.IndexOf($Old, [StringComparison]::Ordinal)
    if ($first -lt 0 -or $Text.IndexOf($Old, $first + $Old.Length, [StringComparison]::Ordinal) -ge 0) {
        throw "CombatSolver 0.50.1 turn-start overlay anchor '$Label' was missing or ambiguous."
    }
    return $Text.Substring(0, $first) + $New + $Text.Substring($first + $Old.Length)
}

$registryPath = Join-Path $SourceRoot 'Prediction\AdaptedCardOnPlayMirrors.cs'
$registry = [IO.File]::ReadAllText($registryPath, [Text.Encoding]::UTF8)
if ($registry.Contains('AdaptedRelicBeforeHandDrawMirrors.CaptureLiveStamp()', [StringComparison]::Ordinal)) {
    throw 'The ignored CombatSolver copy already contains the Relic.BeforeHandDraw overlay; use a fresh local build cache.'
}
$registry = Replace-ExactlyOnce $registry @'
        if (!Seal()) return null;
        StringBuilder text = new();
'@ @'
        bool hasCardAdapters = Seal();
        string? turnStartStamp = AdaptedRelicBeforeHandDrawMirrors.CaptureLiveStamp();
        if (!hasCardAdapters && turnStartStamp is null) return null;
        StringBuilder text = new();
        if (turnStartStamp is not null)
        {
            Field(text, "relic-before-hand-draw");
            Field(text, turnStartStamp);
        }
'@ 'root configuration stamp'
[IO.File]::WriteAllText($registryPath, $registry, [Text.UTF8Encoding]::new($false))

$relicsPath = Join-Path $SourceRoot 'Search\SimulatedCombatState.Relics.cs'
$relics = [IO.File]::ReadAllText($relicsPath, [Text.Encoding]::UTF8)
$relics = Replace-ExactlyOnce $relics @'
            if (simulator.HasPendingChoice)
            {
                simulator.RejectExecutionContinuation();
                return true;
            }
        }
        return false;
    }

    public void CompleteRelicHandDraw
'@ @'
            if (simulator.HasPendingChoice)
            {
                simulator.RejectExecutionContinuation();
                return true;
            }
            AdaptedRelicBeforeHandDrawMirrors.InvokeBeforeHandDraw(simulator, player, relic);
        }
        return false;
    }

    public void CompleteRelicHandDraw
'@ 'before-hand-draw relic dispatch'
[IO.File]::WriteAllText($relicsPath, $relics, [Text.UTF8Encoding]::new($false))

$statePath = Join-Path $SourceRoot 'Search\SimulatedCombatState.cs'
$state = [IO.File]::ReadAllText($statePath, [Text.Encoding]::UTF8)
if ($state.Contains('AttachPredictionBranchSimulator', [StringComparison]::Ordinal)) {
    throw 'The ignored CombatSolver copy already contains branch-local simulator binding; use a fresh local build cache.'
}
$state = Replace-ExactlyOnce $state @'
    private readonly IRunState _runState;
'@ @'
    private readonly IRunState _runState;
    private CombatPredictionSimulator? _predictionBranchSimulator;

    internal CombatPredictionSimulator PredictionBranchSimulator
        => _predictionBranchSimulator
            ?? throw new PredictionUnsupportedException("This simulated combat state is not attached to its owning prediction branch.");

    internal void AttachPredictionBranchSimulator(CombatPredictionSimulator simulator)
        => _predictionBranchSimulator = simulator;
'@ 'branch-local simulator binding'
[IO.File]::WriteAllText($statePath, $state, [Text.UTF8Encoding]::new($false))

$simulatorPath = Join-Path $SourceRoot 'Engine\InCombat\Simulation\CombatPredictionSimulator.cs'
$simulator = [IO.File]::ReadAllText($simulatorPath, [Text.Encoding]::UTF8)
if ($simulator.Contains('AttachPredictionBranchSimulator(this)', [StringComparison]::Ordinal)) {
    throw 'The ignored CombatSolver copy already contains simulator attach calls; use a fresh local build cache.'
}
$simulator = Replace-ExactlyOnce $simulator @'
        if (combatState is ICombatPredictionRootMaterializable materializable)
            materializable.MaterializeRoot(this);
'@ @'
        if (combatState is ICombatPredictionRootMaterializable materializable)
            materializable.MaterializeRoot(this);
        if (State.CombatState is CombatSolver.SimulatedCombatState simulated)
            simulated.AttachPredictionBranchSimulator(this);
'@ 'new prediction branch binding'
$simulator = Replace-ExactlyOnce $simulator @'
        ActionRelicTriggers = actionRelicTriggers;
'@ @'
        ActionRelicTriggers = actionRelicTriggers;
        if (State.CombatState is CombatSolver.SimulatedCombatState simulated)
            simulated.AttachPredictionBranchSimulator(this);
'@ 'forked prediction branch binding'
[IO.File]::WriteAllText($simulatorPath, $simulator, [Text.UTF8Encoding]::new($false))

$powerPath = Join-Path $SourceRoot 'Prediction\PersistentPowerSupport.cs'
$power = [IO.File]::ReadAllText($powerPath, [Text.Encoding]::UTF8)
$power = Replace-ExactlyOnce $power @'
            else
                result = listener.ModifyHandDraw(player, result);
'@ @'
            else if (listener is RelicModel adaptedRelic
                && AdaptedRelicBeforeHandDrawMirrors.TryModifyHandDraw(combat,
                    adaptedRelic, player, result, out decimal adapted))
                result = adapted;
            else
                result = listener.ModifyHandDraw(player, result);
'@ 'branch-local hand-draw callback'
[IO.File]::WriteAllText($powerPath, $power, [Text.UTF8Encoding]::new($false))

$adapterTarget = Join-Path $SourceRoot 'Prediction\AdaptedRelicBeforeHandDrawMirrors.cs'
if (Test-Path -LiteralPath $adapterTarget) {
    throw 'The ignored CombatSolver copy already contains AdaptedRelicBeforeHandDrawMirrors.cs; use a fresh local build cache.'
}
Copy-Item -LiteralPath $AdapterSource -Destination $adapterTarget
