# Bounded validation stages

This work extends local commit `2d8b7362b8f344b7be85178c82541ceb967cb713`. It does not publish a new commit. Game and Eleanor input locks remain v0.111.0 / 41cef1ea and v0.4.1-forms-cardpool.fix.

## 1. ManaShield boundary matrix

The six fixtures are base/upgraded crossed with starting Mana 0/1/2, each executed twice in fresh native processes. Setup uses actual `CombatState.CreateCard`, `CardPileCmd.Add`, `CardCmd.Upgrade` and `ManaState.TryPayMana`. This is an explicit API fixture, separate from natural reward acquisition in the two-battle baseline.

| Upgrade | Starting Mana | Final Mana | Block gained | Energy paid |
| --- | --- | --- | --- | --- |
| Base | 0 | 0 | 5 | 1 |
| Base | 1 | 0 | 10 | 1 |
| Base | 2 | 1 | 10 | 1 |
| Upgraded | 0 | 0 | 5 | 1 |
| Upgraded | 1 | 0 | 13 | 1 |
| Upgraded | 2 | 1 | 13 | 1 |

All 12 executions passed with exact per-action roots/continuations and unchanged parent forks. Six comparisons of repeated traces passed. Evidence: `.build/runs/extended-ShieldMatrix-20261102-015131-773/summary.json`. The old two-battle suite was rerun serially: `.build/runs/eleanor-native-loop-20261102-015311-001/repeat-summary.json`, five processes and 41 checks passed.

The vanilla runner previously tried to apply a relic overlay twice when reusing its cache. The runner now records overlay source hashes and verifies them on reuse, following the existing v0.4 runner's cache contract. This does not change combat semantics or audit rules.

Use the external configuration described in [VALIDATION.md](VALIDATION.md), build the native-loop harness, and pass its printed directory:

~~~powershell
pwsh -NoProfile -File tools/validation/run-extended-validation.ps1 -HarnessRoot <harness-directory> -Stage ShieldMatrix
~~~

The matrix covers a neutral, power-free opening with Cracked Orb. Additional powers, other relics and all-card support are separate coverage.

The vanilla/earlier Eleanor regression also exited 0: `.build/runs/vanilla-m1-20261102-015821-738/milestone-summary.json`. Its output includes NU1900 warnings from an unavailable NuGet vulnerability-data endpoint; no validation case was suppressed.

## 2. Declared fixed-strategy batch

`tests/fixtures/fixed-strategy-batch.json` declares three literal seeds, two card-reward policies and two process executions per pair before the batch starts. It retains the original seed plus `LAB_V0111_ELEANOR_BATCH_02` and `LAB_V0111_ELEANOR_BATCH_03`. The driver copies this declaration and records its hash before running any case.

Both policies collect native gold. Card-take chooses native option index 0, regardless of which card the seed offers. The second-battle acquired-card coverage policy currently supports ManaShield; another card must produce explicit unsupported rather than being replaced with a preferred reward. Enemy and room generation remain native.

The first execution generates bounded combat plans; the second freezes and replays any plans emitted by the first. Reports distinguish this replay check from independent replanning. Search uses Low, beam 8, 1000 nodes, 5000 ms, DoP 1; second battle is bounded to 12 chunks / 12 turns and acquired-card draw coverage to turn 5. Failed seeds are retained. Native/simulator mismatches are failures; an actually executed and matched Death is a strategy outcome. Predicted deaths are not reported as native deaths.

~~~powershell
pwsh -NoProfile -File tools/validation/run-fixed-strategy-batch.ps1 -HarnessRoot <harness-directory>
~~~

Exit 0 means the declared cohort completed without semantic/check failures or unsupported paths. Exit 2 means it completed with explicitly reported unsupported paths. Exit 1 is a failed correctness check or execution. The cohort is not a win-rate estimate or evidence of global optimality.

The first cohort is retained at `.build/runs/fixed-strategy-20261102-020310-031/summary.json`: six completed, four failed and two unsupported. It revealed two Lab defects, which were not counted as strategy deaths:

- The adapter used a shared counter for native and independently created simulated card objects. The second seed's native Slimed was assigned 011 and its simulated counterpart 012 despite matching state and raw continuations. Opening cards still use native-reference identity. New cards now receive stable, independent ordered first-observation identities per world, with unchanged checks of every card's state, ordered pile membership, counts and complete continuations. No native card state is copied into the simulator.
- Selecting the last native reward automatically completes that reward set. The driver now queries native `IsRewardsSetCompleted` before issuing a skip and verifies completion afterward, so it does not skip a set already closed by the native synchronizer.

The third seed takes ManaConversionPrinciple at index 0. That native acquisition and next-combat handoff are recorded, but this specific acquired-card coverage policy is explicitly unsupported. The reward is not substituted with ManaShield.

Final generated-plan cohort: `.build/runs/fixed-strategy-20261102-021310-857/summary.json`. Twelve paths completed: **8 two-battle Victories, 4 unsupported, 0 Death, 0 semantic failures**; 30 checks passed. The unsupported cases are seed 2 take (ReturnFlow) and seed 3 take (ManaConversionPrinciple), each twice. All skip paths and the original ManaShield take path reached actual Victory. These are finite policy results, not a reliable full-run win rate.

The generated-card action resolver also needed a one-to-one lookup by already-observed root identity when native and simulated generated objects differ. It requires a unique matching hand identity and owner/model, after exact full-root comparison; it does not match by card name alone. The later generated plan chose a route that did not play Slimed, so that route alone was not treated as validation of this resolver.

A separate frozen regression reuses the retained pre-fix plans with `-ReplayPlansFrom .build/runs/fixed-strategy-20261102-020903-886`. Evidence: `.build/runs/fixed-strategy-20261102-021549-792/summary.json`; again 8 Victory / 4 unsupported and 30 checks passed, exit 2. Seed 2 skip actually plays generated Slimed on turn 4 in both repeats and then reaches Victory. Every action and parent fork matches. Earlier failed/unsupported cohorts remain intact.

## 3. Bounded online decisions

CombatSolver already generated routes for this project. `NativeCombatDecision` adds single-use tickets tied to the actual combat and complete source live/continuation stamps. The query verifies its returned source and proves that search left native state unchanged. Consumption rechecks the current source, executes only the first action, and rejects reuse. The remaining candidate actions are recorded but discarded.

The online loop uses an exact native/simulator action replay, then captures the actual resulting boundary and queries again. Its scope is the original card-skip fixture's second combat; the first battle remains a separate recorded route. Coverage policy, frozen chunks and online replanning have distinct reported modes.

Bounds are Evaluate / beam 8 / 1000 requested nodes / 5000 ms per query / DoP 1, at most 48 decisions and 12 turns. An elapsed stop is checked before each query at 90 seconds; each native action drain retains its own 45-second timeout. This is a scheduling stop, not a hard process deadline. Empty/unplayable/unsupported actions and unresolved boundaries stop explicitly. Finite-budget routes carry no global optimality claim.

~~~powershell
pwsh -NoProfile -File tools/validation/run-online-decision-cases.ps1 -HarnessRoot <harness-directory>
~~~

The cases are two independent online executions plus a frozen one-action replay of the first online plan. Checks cover actual Victory, one action per query, source immutability, single-use rejection, each new query's equality with the preceding actual action boundary, deterministic traces, frozen replay without online queries, and offline save/replay guards.

Evidence: `.build/runs/online-decisions-20261102-022356-476/summary.json`, exit **0**, all three native processes and **15 checks passed**. Each online execution performs nine fresh queries and nine actual actions, reaching second-combat Victory on turn 3 at HP 70. The frozen execution performs the same actions and has zero online queries. Full traces and lifecycle agree across all three.

Observed node counts for online-a were 143, 109, 83, 75, 55, 32, 17, 7 and 2; all nine queries ended below their configured budget. Candidate tails are actually discarded (eight unused actions after the first query, decreasing thereafter). This fixture does not validate exhausting the wall/decision budget, native Death handling, online card-take, arbitrary seeds or full-run decisions.

## Final regressions and boundaries

The final native-loop build compiled with zero warnings/errors. The final matrix rerun also passed all 12 executions and six repeat comparisons: `.build/runs/extended-ShieldMatrix-20261102-022443-357/summary.json`.

The final five-process two-battle suite passed all 41 checks, exit 0: `.build/runs/eleanor-native-loop-20261102-022603-354/repeat-summary.json`. This includes skip/take repeats and the nonzero Recording/Echo teardown fixture, after adding the separate online mode.

After generated-card adapter repair, the vanilla/earlier Eleanor runner again exited 0: `.build/runs/vanilla-m1-20261102-022000-159/milestone-summary.json`. This covers native vanilla Victory/RNG, the earlier Eleanor effects/cross-turn/Victory baseline and vanilla M3-skip. NU1900 vulnerability-data lookup warnings remain environmental warnings.

Each successful two-battle run retains MemorySaveStore, TestMode true, Steam false, two intercepted replay writes and four intercepted progress saves, with persistent writes disabled. The actual role input remains the original 15-file v0.4.1 lock. No role source, installation, real save, UI, deployment, commit or push is part of these stages.
