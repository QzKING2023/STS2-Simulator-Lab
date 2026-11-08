# Stage 1 verification

This document preserves the historical candidate `stage-one-final-04`. Its 33 jobs / 93 evidence checks did not detect three result-gate/progress defects found by independent review; that candidate was **not accepted**. The bounded repair, its new frozen identity and current verification state are in [STAGE_1_REVIEW_REPAIRS.md](STAGE_1_REVIEW_REPAIRS.md). The evidence and acceptance matrix below remain traceable historical results. Acceptance IDs are in [STAGE_1_IMPLEMENTATION_PLAN.md](STAGE_1_IMPLEMENTATION_PLAN.md).

## Final immutable build

| Item | Identity |
| --- | --- |
| Build ID | `31F713F7CF538FB9C0A076E24560EE1BDC0C5FF852EFBA872C91BC722BEE7D0A` |
| Harness SHA-256 | `69B9F3C4636DC9EFB5579A9A872AE69D453E3B7E265C4117C7536920A1260AD3` |
| CombatSolver SHA-256 | `C2D9B426805C735B6909D9F174EE721C838143E7CC7A0B0D5123565F8428EBEB` |
| Core SHA-256, identical in harness and Core checks | `85BF830A6405CAC7477509D2E735EBE4AE380FAE2395B7BBD4054AE9674821F8` |
| Evidence verifier SHA-256 | `E39993F1BBD712EE6FCAC13C3731576A02C7DB90415CF5EA2199C386458C7B5E` |
| Manifest / local source snapshot | `.build/runs/stage-one-final-build-04/build-manifest.json` / adjacent `source-snapshot/` |
| Results / evidence review | `.build/runs/stage-one-final-04/summary.json` / `evidence-review.json` |

The manifest records **55 Lab/support source files, 863 copied compiled-source/project files, 9 artifacts and 23 input/lock files**, original upstream fingerprints and toolchain. The character snapshot still has 15 matching locked files. Every actual assembly recorded by the 24 directed processes matched an input/artifact hash. The baseline HEAD remains `2d8b7362b8f344b7be85178c82541ceb967cb713`; the manifest and source snapshot identify the dirty implementation actually tested, rather than treating HEAD as that source version.

The main directed cases, vanilla compatibility, original 16-action baseline, two-battle, matrix, seed cohort, online decisions and frozen Slimed replay all used this exact harness/solver/Core artifact set. Earlier candidates remain archived. Candidate 1 exposed the compatibility-entry/plan-selection defects; candidate 2 retained a missing vanilla reward-selector setup; candidate 3 stopped at a completed case boundary when a reporting-source change invalidated its freeze. None substitutes for the final results.

## Implemented behavior

- The replay accepts an actual terminal stamp from `AdvanceRound`, including a `None` return with a terminal state. It no longer requires a next player turn after a terminal enemy phase.
- Read-only, signature-checked observers capture Victory before `EndCombatInternal` and Defeat before `ProcessPendingLoss`. The original game methods execute. Combat terminal roots, full continuations and parent isolation remain exact checks.
- Result envelopes distinguish native/simulator outcome, comparison, coverage, stop reason and verification. A matched Death verifies semantics; it does not represent policy success. An exact prefix does not complete unsupported coverage.
- Online tickets bind the combat reference, complete source stamps and the Lab's executed-action sequence. Duplicate, changed-source, ended-combat and cross-combat consumption are rejected.
- Budget reports separate actual solver `NodeLimit` from controller decision/turn limits, elapsed guard controls and process interruption. A query's actual policy, expansion count and remaining candidates are retained.
- ManaShield expected values come from a version/hash-bound independent contract: base block 5, extra block 5 or upgraded 8, energy 1, Mana 1 when available. The six API fixtures still compare native and simulator state.

## Death lifecycle scope

The pinned game's `CreatureCmd.Kill` marks pending loss. In native `TestMode`, it skips the retail game-over path that would call `RunManager.OnEnded`, metrics/history and UI. `CheckWinCondition` then calls `ProcessPendingLoss`, clears combat-in-progress and emits `CombatEnded`.

This loss path does **not** call the Victory routine's `Hook.AfterCombatEnd`, clear combat history, or invoke the role's `ManaRuntime.End`. Observed Mana/Echo and role subscriptions therefore remain after combat Death. Lab records that residue; it never invokes a role reset to make the result look clean. This is a native loss-lifecycle contract, not independent simulator prediction of teardown, full run-loss persistence, or retail UI acceptance.

The enemy-death fixture uses actual nonlethal native damage before taking an enemy hit. The upkeep-death fixture uses the real Guide choice, actual Mana payment, explicit native Block and nonlethal HP setup; it then executes EndTurn through the enemy phase to lethal Reverse upkeep on player turn 2. Both are labelled API fixtures. Recording/Echo are established by actual power/block/damage APIs; no candidate Echo card is added.

## Evidence and reproducibility

The final build manifest records dirty Lab source contents, copied compiled sources, original upstream fingerprints, toolchain, locked inputs and binary hashes. Sources are retained under an ignored local source snapshot. These files are local evidence, not public release dependencies.

Build with `run-v0111-eleanor-v040.ps1 -NativeLoopStage Pool -BuildOnly`, using the external configuration in [VALIDATION.md](VALIDATION.md), explicitly selecting `-PinnedModDirectory eleanor-v0.4.1-forms-cardpool.fix -ExpectedModVersion v0.4.1-forms-cardpool.fix`. Build the Core checks in Release, then use `NewStageOneBuildManifest.ps1` to freeze the printed harness. The following replay commands use the existing retained local inputs and produce a fresh result directory:

```powershell
pwsh -NoProfile -File tools/validation/run-stage-one.ps1 `
  -BuildManifest .build/runs/stage-one-final-build-04/build-manifest.json `
  -OutputDirectory .build/runs/stage-one-reproduction `
  -RetainedVictoryPlan .build/runs/eleanor-v041-two-turn-probe-20261101/reverse/route.json
pwsh -NoProfile -File tools/validation/TestStageOneEvidence.ps1 `
  -RunDirectory .build/runs/stage-one-reproduction
```

The retained 16-action plan SHA-256 is `975F7371F401AE9987FF1746E5A4D9F2A320D72BE9A05E2784A26B589C034D2F`. Use the actual plan referenced by the old replay report; the general harness can overwrite its own `route.json` with a later search result. New builds need a new manifest and fresh output directory. The default runner rejects a full-stage invocation without the retained plan. These local artifacts and binaries are not distributed with the repository.

The outer stage runner checks expected exits, including injected error exit 1 and known unsupported cohort exit 2. Its own exit 0 means that its declared verification checks passed. It does not convert those child outcomes into native success or complete character coverage.

## Acceptance matrix

| ID | Final result and evidence scope |
| --- | --- |
| F01 | Passed: source/build/input hashes verified before/after cases; controlled wrong manifest identity, artifact hash, input hash and source hash all rejected without altering originals. Source-freeze stop was also exercised during development. |
| O01 | Passed: **72 Core checks**, including terminal invariants, all typed stop mappings, empty-result/budget distinction and schema/enum validation implementation. |
| D01 | Passed twice: native enemy attack causes Death; exact terminal roots/continuations, unchanged parent, one CombatEnded and no CombatWon. |
| D02 | Passed twice: real EndTurn reaches player turn 2; failed Reverse upkeep causes Death, Neutral/zero Mana and removal of owned Strength/Dexterity. |
| D03 | Passed as native contract: actual nonzero Recording/Echo setup; loss does not call AfterCombatEnd, Echo remains (3 / 15 in these fixtures), subscriptions remain active. No Lab reset. This validates observed absence of cleanup, not cleared resources. |
| V01 | Passed: original **16 actions / 122 artifact checks**, actual Victory and native teardown. |
| V02 | Passed twice: actual enemy attack triggers native Thorns, ends combat during enemy phase; no next player boundary required. |
| B01 | Passed twice each: decision and turn controllers stop after actual native execution at their declared short limits. The production online loop uses the same DecisionBounds gate. |
| B02 | Passed contract controls: before/inclusive elapsed edges. Not a claim of naturally hitting the solver's wall limit. |
| B03 | Passed twice: actual `NodeLimit`, actual/requested nodes 100, expanded 100, **10 candidate actions**, unchanged native source, zero candidate execution. |
| B04 | Passed contract controls: exhausted budget without candidates, no-budget empty result and unsupported gap remain distinct. Natural solver empty-result coverage remains untested. |
| A01 | Passed: stale and duplicate tickets twice; ended/cross-combat checks in all five two-battle processes, with unchanged continuations. |
| A02 | Passed twice each: missing card, invalid target and wrong turn rejected without native state mutation. |
| C01 | Passed: **12 native executions / six repeat comparisons**, independent ManaShield 5/10/13 block and payment/upgrade assertions. |
| C02 | Passed controls: wrong shared observed block is inconsistent with independent expectation; mutated differential records are rejected. No role/Game DLL mutation. |
| X01 | Passed twice as injected fault: exact pre-dispatch Lab exception retained with child exit 1, EngineError and full error evidence. |
| X02 | Passed contract controls: importer rejects post-action drift; mismatch cannot be marked successful. Not a claim of a naturally occurring engine differential fault. |
| X03 | Passed control: own waiting worker reaches a barrier, watchdog stops it and confirms exit; native outcome Unknown/Interrupted. |
| U01 | Passed declared checks: **12 paths, 8 Victory / 4 unsupported, 30 checks**, suite exit 2. No seed replacement. |
| R01 | Passed frozen regression: same 8/4/30 and exit 2; seed2 skip actually plays generated Slimed on turn 4 in both repeats. |
| R02 | Passed: five-process two-battle **41 checks** (includes prior minimum-loop contracts), online three processes **15 checks**, two online executions each nine actual queries/actions and frozen replay with zero queries. |
| R03 | Passed on main artifact: two vanilla battle/M3 and two random-target cases, plus V01. Separate earlier-role compatibility regression also exited 0 at `.build/runs/vanilla-m1-20261102-035532-664/milestone-summary.json`; its independent artifact is not counted as the main binary. |
| S01 | Passed retained strict registration/patch/fork/discovery and persistence guards. Synthetic PatchInfo tests retain that label; real late discovery patch rejection remains actual weave evidence. No audit loosening. |

Independent evidence review performed **93 checks, zero failures**: complete job membership, deterministic action/lifecycle/outcome repetitions, terminal/prefix roots and continuations, parent roots, actual budget evidence, literal payment/block contracts, cross-combat rejection, preserved cohort denominators and watchdog ownership. Full reviewed source is copied next to `evidence-review.json`.

Input pins remain game v0.111.0 / 41cef1ea and Eleanor v0.4.1-forms-cardpool.fix (`949320F6…5E7DBAD42`). The independent legacy compatibility run uses its own older Mod pin. Repeated executions establish reproducibility and are not independent statistical samples.

## Local change scope

Relative to the saved stage-start manifest, this stage modifies 13 existing project files and adds 13 files. Changes cover the Core result/stop contract and checks, terminal replay, decision tickets/budgets, loop classification, independent ManaShield expectations, the isolated build/runner/reviewer, two fixture declarations and documentation. The exact phase-specific list is `.build/runs/stage-one-final-04/workspace-change-list.json`. Earlier uncommitted work and all prior run evidence remain present; the roadmap content hash is unchanged. No new implementation of unsupported reward cards was added.

There is no remaining Lab execution blocker for this bounded acceptance. Native loss residue, natural empty-result/wall-limit coverage, full retail loss handling and the existing reward-card gaps remain the explicit limits above. Runtime source and artifacts still match the final manifest after documentation updates. HEAD is unchanged and all work remains local and uncommitted.

## Limits retained

- ReturnFlow and ManaConversionPrinciple remain unsupported acquired-card coverage policies. The declared seed cohort is preserved, including its denominator and failures.
- A real node-budget stop with candidates is tested. Budget-without-candidate, elapsed clock edges and mismatch detection have explicit contract controls; they are not claims of naturally occurring native faults or exhaustive wall-time behavior.
- The watchdog stops only a deliberately waiting Lab-owned worker. It does not demonstrate safe recovery from an interrupted native action; uncertain native outcomes are not retried.
- Current decision information remains OracleReplay. Guide subchoice protocol, generated-object lineage expansion, new reward cards, nodes, balance experiments and act strategy remain later work.
- No character source, installation, actual save, UI, deployment, commit or push is part of this stage.
