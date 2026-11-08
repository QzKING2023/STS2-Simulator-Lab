# Stage 1 review repairs

The independent review rejected candidate `stage-one-final-04` because typed failures could disappear in batch aggregation, a budget fixture could accept a failed inner replay, and outcome envelopes lost cumulative progress. The old artifacts are retained. This document tracks the bounded repair candidate, accepted by independent re-review on 2026-11-02.

## Repair contracts

| Review finding | Implemented contract | Evidence entry |
| --- | --- | --- |
| Batch failure swallowing | The production classifier counts every status, rejects unknown/missing/duplicate identities and typed-status contradictions, checks raw child exits, and requires a declared card/phase/case identity for unsupported. Budget/no-candidate/cancel/interrupt/environment stops are incomplete (exit 3), execution/verification errors exit 1, declared unsupported exit 2, complete verified outcomes exit 0. | `failure-propagation-controls.json`, `batch/summary.json`, `slimed-replay/summary.json` |
| Budget accepts failed or empty replay | Production `ReplayVerification.RequireBoundary` requires exactly one confirmed ongoing action. The fixture additionally requires a living native Play boundary. Typed inner failures propagate. The artifact reviewer requires a nonempty boundary and one confirmed action. | Core checks, negative controls, `decision-budget-*/stage-one-case.json`, `turn-budget-*/stage-one-case.json` |
| Cumulative prefix lost | Replay counts only the contiguous accepted/exact prefix; second-battle counts sum its chunks; run-loop counts sum both battles. Query errors finalize the second-battle report and preserve the last confirmed roots/continuations. Current EngineError outcomes remain Unknown/OutcomeUncertain on both sides. | `online/summary.json`, each native-run-loop and second-battle report |

`ConfirmedActions` excludes fixture setup APIs, rewards and travel. Each envelope has a companion `confirmedActionsScope` explaining its domain. The two directed upkeep actions are counted across their separately recorded prefixes; native setup between prefixes is explicit. A query fault does not imply that a previously confirmed prefix became uncertain.

The online regression includes two normal executions (nine queries/actions each), a zero-query frozen replay, and two **injected-fault controls**. Each fault occurs before query 3, after two real, differentially verified native actions. Raw fault child exits remain 1; the suite verifies the exact injection identity and retained prefix. This is not a naturally occurring solver defect or safe native-action interruption demonstration.

## Frozen repair candidate

- Build ID: `0C4EAC45A499E41D975316D756C3865C5ABF2BAE59AA7943CBAFE8E57530FF72`.
- Harness SHA-256: `08E0B2CEF9FA081EE36379B8E734D8EB9BABF9BBFD257B178108AE8580166845`.
- CombatSolver SHA-256: `09F7E04A0B72F7D45DE0B894BBF6A8F725A604DBA4BDE4A281AFABDCE427EB65`.
- Core SHA-256 (harness and Core checks): `33406BC55A5B3DA5D07062083AD5F41F81E407DC72FEBF49AA9C6C310DC2C3ED`.
- Manifest: `.build/runs/stage-one-repair-final-build-01/build-manifest.json`.
- Results: `.build/runs/stage-one-repair-final-01/summary.json`.

The manifest binds 60 Lab/support sources, 863 compiled source/project files, nine artifacts and 23 input/lock files. All 15 Eleanor input hashes remain bound to v0.4.1; game v0.111.0 / 41cef1ea is unchanged. The controls and actual suites use that same frozen set. The Core runner is a separate executable referencing the identical Core DLL.

The final unified runner exited **0**, completing **34 declared jobs** with no unexpected exits. Its artifact reviewer exited **0** with **132 checks / zero failures**, verifier SHA-256 `40152F9FA713EF3976AA4879B244D42B58120C1A227DE4AFA6216974845F165E`. The additional outer job is the failure-propagation control suite; the online suite now includes two extra fault processes. These counts are nested verification work, not independent statistical samples or an external acceptance decision.

The final run contains:

- Core **81** checks (previous 72 plus nine replay/prefix checks).
- Failure propagation **25** in-memory controls, using the production batch classifier and artifact boundary gate; rejected states include EngineError, InvalidAction, differential failure, unknown status, undeclared unsupported, inconsistent raw exit, missing/duplicate identity, empty replay and failed inner replay. Budget and other incomplete outcomes remain explicit exit 3 controls.
- **24** directed processes, including twice each enemy Death, upkeep Death, enemy-phase Victory, solver/controller budget, ticket/invalid-action rejection and pre-dispatch error control. Each death process exits after recording residue.
- Original **16 actual actions / 122 checks**, and the existing **five-process two-battle / 41 checks**.
- ManaShield **12 native API executions / six repeat checks** with independent literal payment/block expectations.
- Each fixed/replayed cohort **12 paths / 30 checks / 8 actual Victory / 4 unsupported**, child-suite exit **2**. All 12 outcomes are counted and all raw exits are checked.
- Online/frozen/fault suite **five processes / 27 checks**. Normal second-battle counts are 9 and whole-loop counts are 18 (9+9); fault second-battle counts are 2 and whole-loop counts are 11 (9+2). Fault native children keep exit **1** and Unknown/OutcomeUncertain current outcomes. Last-confirmed roots and continuations remain available separately.
- Same-artifact vanilla battle/M3 and RNG replays. The 24 directed processes recorded **288** loaded gameplay assemblies, all matching frozen input/artifact hashes; `loaded-assembly-review.json` has zero mismatches.

The separate old-Eleanor compatibility runner is recorded independently below; it is not part of the 34 main-artifact jobs. Development attempts and the earlier rejected candidate remain intact. The first repair build had a namespace compile error and did not execute native tests; the final harness build and Core build each completed with zero warnings/errors.

## Acceptance mapping on the repaired artifact

| Plan IDs | Repaired-run evidence and boundary |
| --- | --- |
| F01 | Freeze/source snapshot/input/artifact guards and four corrupted metadata controls pass. All 15 character input hashes match. |
| O01 | 81 Core checks and 25 failure controls; exhaustive batch classification, raw-exit/denominator checks, prefix counts and fail-closed boundary validation. |
| D01/D02 | Two actual enemy Deaths and two upkeep Deaths, matching terminal roots/continuations and unchanged parent forks. Upkeep passes through enemy phase to player turn 2. |
| D03 | Actual nonzero Record/Echo fixture and observed absence of AfterCombatEnd on loss; residue retained, independent process exits. No cleanup/reset is invented. |
| V01/V02 | Original 16-step Victory plus two real enemy-phase Thorns Victories; terminal stamps do not require a following Play. |
| B01 | Decision and turn fixture stops each repeat twice after one accepted exact ongoing action, ConfirmedActions=1, living native Play boundary. |
| B02/B04 | Inclusive elapsed/empty-result classification controls remain controls; no claim of natural wall expiry or solver-empty coverage. |
| B03 | Two actual 100-node NodeLimit queries with ten candidate actions each; source unchanged, zero candidate actions executed. |
| A01/A02 | Duplicate/stale/ended/cross-combat and invalid card/target/turn rejection retained, with source/fork/continuation checks. |
| C01/C02 | Independent ManaShield 5/10/13 expectations and mutated/shared-wrong observation controls remain; fixture setup uses native APIs. |
| X01/X02/X03 | Two pre-dispatch injected faults, differential-import controls and owned waiting-worker interruption remain labelled. Two additional mid-query faults retain a real two-action prefix. No native-action cancellation claim. |
| U01/R01 | Both exact declared cohorts retain 8/4 denominators and exit 2. Retained seed2-skip actions actually play generated Slimed in both repeats. |
| R02 | Two-battle 41 checks and online/frozen/fault 27 checks; known gaps remain explicit. |
| R03 | Vanilla/M3/RNG and original16 run on the main artifact; old-Eleanor compatibility has a separate pinned profile/artifact. |
| S01 | Existing strict source/method/manifest/patch/registry/fork/discovery and persistence guards retained. Actual late discovery weave is distinguished from synthetic PatchInfo controls. |

The original acceptance IDs and their scope are preserved; no new card, node or route capability is required by these repairs. The three requested fixes are implemented and the declared local gates pass. **Independent re-review passed on 2026-11-02; Stage 1 is accepted within the stated boundaries.** The review independently checked the frozen sources/inputs/artifacts, actual budget and cumulative-prefix evidence, reran the 81 Core checks, and applied in-memory mutations to real cohort reports through the production classifier and boundary gate. Execution errors, unknown statuses, raw-exit contradictions and undeclared gaps failed closed; budgets remained exit 3. No Stage 1 blocking issue remained.

## Supporting artifacts and local changes

`summary.json`, `evidence-review.json`, `failure-propagation-controls.json`, `online/summary.json`, `loaded-assembly-review.json` and `review-repair-change-list.json` reside under `.build/runs/stage-one-repair-final-01/`. Console logs are retained under `.build/stage-one-repair-*.log`. The copied artifact reviewer and the frozen source snapshot make the review implementation traceable.

Relative to the rejected candidate's frozen sources, nine source/driver files changed and five were added. Six existing project documents were updated and this document added (15 modified / six new files in this repair scope). Prior uncommitted work and historical runs remain; HEAD is still `2d8b7362b8f344b7be85178c82541ceb967cb713`. The roadmap content hash remains `F9A5956658D4F6A67C7F16D518E5BE959BD0DA41CD94531B45F2707F91BC5DC2`.

## Separate legacy compatibility regression

The existing legacy runner was also rerun after the main suites and exited **0**. Its summary is `.build/runs/vanilla-m1-20261102-050115-058/milestone-summary.json`, covering vanilla M1/RNG/M3 and the existing Eleanor v0.3.0-m3.burst23 profile. That Mod SHA-256 is `BAA69E4DCBA52BEA99287AE5D366D1DDA4578A63E6F4AC0B4FB4BC8B6DE65150`; its independent harness SHA-256 is `3D55D0DCDB3F165392A7A6F5722BC14156C0F0253E570D0A258C752A1970EE83`, solver SHA-256 `C154D56B545D7C9B317B5E8B06BB6DEB704D46D90401607EB8758014F6D43904`.

`legacy-compatibility.json` records this distinction next to the repaired main results. It does not count as a main-build job or new v0.4.1 coverage. After it finished, the main frozen source/artifact/input guard passed again; `final-binding-review.json` records zero mismatches for the 15 Mod inputs and both source snapshot sets.

## Reproduce the frozen candidate

```powershell
pwsh -NoProfile -File tools/validation/run-stage-one.ps1 `
  -BuildManifest .build/runs/stage-one-repair-final-build-01/build-manifest.json `
  -OutputDirectory .build/runs/stage-one-repair-reproduction `
  -RetainedVictoryPlan .build/runs/eleanor-v041-two-turn-probe-20261101/reverse/route.json
pwsh -NoProfile -File tools/validation/TestStageOneEvidence.ps1 `
  -RunDirectory .build/runs/stage-one-repair-reproduction
```

The historical acceptance matrix is in [STAGE_1_VALIDATION.md](STAGE_1_VALIDATION.md). The old 33 jobs / 93 review assertions remain historical candidate counts.

## Boundaries retained

Native Death still follows the actual pending-loss path without AfterCombatEnd. Its Mana/Echo/subscriptions remain observed residue. Every death fixture terminates its own process; no dead-run worker is reused. Full retail loss handling, a reuse/reset protocol, natural solver empty-result and wall-limit cases, new acquired-card support, six candidate Echo cards and act strategy remain outside this repair. MemorySaveStore, TestMode, replay/progress intercepts, source audit and fork isolation remain required. No role source, game installation, actual saves, UI, deployment, commit or push is changed.
