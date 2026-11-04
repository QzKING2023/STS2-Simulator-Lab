# CombatSolver as an M1 battle policy: feasibility and execution-cost review

Date: 2026-11-04. **Read-only feasibility assessment; not an implementation or an approved new experiment.** Single-battle tuning and new native experiments are stopped. The existing formal roadmap is unchanged.

## Recommendation

**Use the existing CombatSolver offline search core as a replaceable battle-policy candidate. Stop expanding hand-written TestSubject scoring as the primary route to M1.** Keep the native game as the execution authority and the Lab as the run controller and evidence collector. CombatSolver should propose one action and its complete child choices; the Lab validates and executes it natively, checks the resulting boundary, then asks again.

This is feasible enough to justify a small integration gate. It is not a ready drop-in switch, and there is no evidence yet that it wins the current TestSubject fight. Loading the complete gameplay mod or invoking the public precombat forecast worker is not the recommended first path. The Lab already has a narrower offline integration to reuse.

## 1. What the recent tuning achieved and cost

The accounting below covers **five consecutive completed batches**, not all work over the preceding day. Raw process records independently confirm the counts. Byte figures are the batch closeout inventories, including builds and controls; they exclude additional independent-review storage. See `.build/runs/defend-counterfactual-independent-review01/cost-audit.json` for the exact inputs and per-process rows.

| Batch | Actual native processes | Recorded persistent batch GiB | Actual result |
| --- | ---: | ---: | --- |
| Bounded native branch search | 24 | 7.626 | Best recorded Death624 / Boss59; branch and non-first-choice validation |
| Juggernaut effect contrast and contract repairs | 6 | 1.304 | Two real 12-damage block triggers; Death624 / Boss33 |
| Broad Juggernaut policy | 5 | 0.900 | DEV03 Death618 / Boss119; other two seeds died in act 1 |
| Third-form gate and a595 prefix | 3 | 0.836 | Death610 / Boss94; a597 child choice diverged |
| a597 Defend counterfactual | 2 | 0.775 | Death624 / Boss33, exact full replay |
| **Total** | **40** | **11.442** | **Zero new BossVictory or RunVictory; no farther node reached** |

There was also one bounded-search preflight attempt that did not start native gameplay. The 40 processes include interface controls, candidate rollouts, observation comparisons and replays; they are not 40 independent strategy samples.

The native processes consumed **1,029.974 seconds (17.17 minutes)** in aggregate. The interval from the first recorded native start at 22:32:15 UTC to the last batch closeout at 00:30:56 was approximately 119 minutes. This interval is not an active-work time measurement: build, analysis, evidence I/O, review and scheduling/wait time are not individually instrumented. The five closeouts also record 269 production-reader invocations; these are separate from native executions and exclude independent reviews.

The durable gains are real: legal branch replay, non-first exhaust selection, choice ownership, hard process timeout handling, reproducible native deaths, and identification of a block-trigger damage blind spot. However, M1's remaining progression goal did not advance. Re-executing approximately 600-prefix actions for each short tail and retaining full-run traces makes local manual search expensive. Successively adjusting a greedy score does not plan the interaction between block, HP payments, one-use resources, random autoplay and future hands. The later counterfactual also returned to an already observed Boss33 endpoint; the number 33 alone does not identify a nearly solved strategy.

The latest [counterfactual independent review](NATIVE_RUN_DEFEND_COUNTERFACTUAL_INDEPENDENT_REVIEW.md) passes its declared scope: 47 fact checks and eight production-reader controls. It establishes one deterministic intervention's effect, not a general policy improvement or impossibility proof.

## 2. Actual reusable components

The inspected local reference is **CombatSolver 0.50.1**, declaring minimum game version 0.111.0. Both the external local reference and Lab copy exist. `solver-source-observations.json` in the review evidence directory records inspected file hashes. This is a local version assessment, not a claim about the latest upstream release.

| Component | Evidence in the existing code | Reuse and limit |
| --- | --- | --- |
| Offline search initialization | `.build/local-reference/CombatSolver/tools/search/ModRuntime.cs:17,43` | A narrow search-patch allowlist, fixed settings, no need to invoke the full mod entry point. Still needs an audit against the current native host. |
| Root capture and fork | `src/Runtime/CombatRootSnapshot.cs:188` under that copy | Main-thread capture, live/projected continuation comparison, before/after mutation detection and `ForkSimulator`. A current-host root has not been certified. |
| Bounded search | `tools/search/ModRuntime.cs:342,390` | `CombatBeamSolver` Evaluate and `CombatSearchCoordinator.Solve`; beam, expanded-node, branch and time settings already exist. Not a new solver implementation. |
| Executable action description | `src/Search/CombatPlan.cs` | PlayCard, UsePotion, EndTurn; StateKey/occurrences/target combat ID, primary/nested/turn-start choices and ordering. These must map to current Lab identities. |
| Single-use execution ticket | `tools/validation/NativeCombatDecision.cs:18,38` | Rejects consumed, stale and other-combat tickets; checks source unchanged after search; returns only the first action. Currently coupled to the old harness and Mod run-loop stability check. |
| Native/predicted comparison | `tools/validation/VanillaCombatRootAdapter.cs:18,63,78` | Stable reference-based and generated-card identity plus full continuation extension. Its identities/schema are not the newer native trace's `i...` identities. |
| Prior actual online use | `NativeSecondBattle.cs`, `STAGE_1_REVIEW_REPAIRS.md:38`; original `.build/runs/stage-one-repair-final-01/` | Historical second-battle online mode made nine confirmed actions; total loop 18. This is a narrow older Eleanor fixture, not current whole-run certification. |

There is relevant **static** vanilla coverage: TestSubject move effects, revival state, Adaptable/Nemesis, Intangible scheduling, Juggernaut's actual random-target block trigger, Stampede's Shuffle-RNG autoplay, TrueGrit selection and HP-payment cards. For example:

- `src/Prediction/MonsterMoveEffects.cs`, `DeathPowerSupport.cs`, `EndTurnPowerSupport.cs`;
- `src/Search/SimulatedCombatState.DeathLifecycle.cs` and `SimulatedCombatState.MonsterState.cs`;
- `src/Engine/InCombat/Mirrors/Hooks/Block/AfterBlockGainedMirrors.cs:81`;
- `src/Engine/InCombat/Mirrors/Hooks/TurnEnd/AfterAutoPostPlayPhaseEnteredMirrors.cs:63`;
- `src/Search/CardChoiceSupport.cs:103` and `CardEffectSpecRegistry.cs`.

Finding these handlers materially improves feasibility. It does not prove every current relic, enchantment, generated object, private state or listener is supported, or that a particular search budget finds a winning plan. No CombatSolver test or new gameplay was executed in this assessment.

## 3. Real integration work and risks

### Host and audit boundary

`tools/native-run/NativeHost.cs:128` explicitly rejects CombatSolver, RitsuLib, BaseLib and MyMod assemblies. Its patch inventory is also immutable. That is an intentional property of the currently certified native-only host. Simply deleting the guard would invalidate the meaning of earlier isolation evidence.

Introduce a separate, explicit solver-enabled host profile with pinned actual assemblies, permitted initialization and exact patch inventory; retain the native-only profile and its regression unchanged. Do not load Eleanor/BaseLib as a shortcut. CombatSolver's `PredictionModPatchAudit` independently rejects unknown patches. If a Lab observation/UI patch conflicts, identify the exact method and prove or redesign that narrow bridge; never label the whole Lab as gameplay-neutral to evade the audit.

The solver's useful interfaces are internal. The existing build grants access to `OfflineSearchHarness`; the new native artifact has another assembly identity. Reuse the existing internal adapter boundary or a narrow Lab-only bridge, rather than designing a public Mod SDK now. A separate process cannot reconstruct a faithful native root from the current evidence JSON by merely deserializing it: private state, object identity and subscriptions matter. The first integration should capture the actual in-memory state in an isolated experimental host.

### Action and choice execution

Reuse the native legal-action enumeration, request/stamp protocol, stable identities, failure prefixes and event collector. Map solver CardStateKey/occurrence/target identities to the actual native objects at the same confirmed root. Translate all choice fields, including non-first, nested and EndTurn/turn-start choices, in real request order. Reject stale, ambiguous, unsupported, omitted or surplus fields before dependent dispatch when possible; preserve genuine prefixes if a child request occurs after native work.

Do not silently use the current greedy child selector for solver-planned TrueGrit. Do not execute the predicted tail as if it were native proof. Query → execute one root action and its children → compare → re-query is the small reusable integration unit.

### Budget and outcome semantics

The old Evaluate harness uses a **soft** search budget and a `MainLoopContext` wait up to 660 seconds (`ModRuntime.cs:357,373`). The old online query also stops on a budget boundary even when candidates exist. Neither behavior should be silently inherited as a current 120-second hard-stop guarantee or changed to mean a win.

Keep distinct outcomes: supported candidate, no candidate, unsupported prediction, node/time budget, invalid action, native exception and differential mismatch. First integration can stop on solver budget rather than invent a candidate-adoption policy. A future native-only fallback may be explicit and counted; it must not be called diff-certified. Abort the affected execution on a state mismatch. Cancellation must finish or terminate the owned experimental process before another native action can run.

Use deterministic node/branch caps and one search worker initially; also retain a separate hard process watchdog. Wall-time limits may truncate different search frontiers, so record the selected actions and replay them rather than promise bit-identical online search under time contention.

### Information and scope

Root/fork search can inspect draw order, RNG and internal enemy state: label this **Oracle**. M1 allows a practical legal battle policy; it does not require player-observable fairness or a measured win rate. CombatSolver does not replace map, event, shop, reward, room and terminal business execution. `NativeThirdAct.cs:75` currently returns after third Boss rewards; even a real solver-assisted win still requires the same-run terminal continuation to Architect/WinRun and the final M1 acceptance work.

## 4. License and packaging

The inspected source has an MIT license with attribution to Torch1230 and contributors and hotwords123 for portions derived from Random Foreseer. The repository's `THIRD_PARTY_NOTICES.md` already records that relationship. Retain the license/notices for substantial reused portions and distinguish local modifications. This is an observation of the actual local license text, not clearance for unrelated dependencies.

The source project defaults `CopyModOnBuild` to true and includes runtime/UI and ancillary components. The existing Lab scripts already supply `CopyModOnBuild=false`, `DeployToGame=false`, `RitsuLibAutoCopy=false`, local output paths and a no-op memory-cleaner build. Reuse those safeguards; do not invoke a default upstream build or full mod bootstrap. Inspect dependency manifests separately. Legal local game/Godot/Ritsu inputs do not become MIT-distributable because the solver source is MIT. No game DLL, decompiled game source, credentials, user-specific local configuration or raw research should be published.

## 5. Minimum validation path — proposal only, not executed

1. **Prepare the separate profile and source lock.** Freeze one real 0.50.1 source plus the necessary reviewed generic Lab overlays. Record dependency hashes and initialization calls. Preserve the 15 input / 52 compatibility-source locks. Do not reuse an arbitrary cache solely because its manifest version matches.
2. **Prove capture/search purity at a real playable boundary.** First ordinary Ironclad, then the existing TestSubject entry, reached by normal native startup and certified prefix. Reuse the original files for audit; JSON is not a native checkpoint. Check source before/after, RNG, fork isolation, patch inventory and capability failure details. No legal-action dispatch until this gate passes.
3. **Exercise one-action integration.** Cover a real card, EndTurn/autoplay, potion when present, and TrueGrit's exact non-first child identity; include stale-ticket and unsupported-child controls. Check actual native result against the prediction and retain complete prefix on failure. Exercise a real form transition before declaring TestSubject lifecycle support. If the limited slice does not reach one of these cases, report it untested.
4. **Only after the bridge works, run a bounded battle-policy comparison.** Start at the Boss entry or earlier, not only at the already resource-depleted a617 boundary. Hold prefix/information/budget fixed. Preserve death, unsupported and budget results; a finite search failure does not prove the fight unwinnable. After a real win, prioritize native terminal continuation, not more score optimization.

A possible initial gate budget is at most **four native process runs, 20 minutes and 2 GiB**, with at most eight solver queries, beam 8 / 1,000 expanded nodes / 5,000 ms soft budget / one worker per query, and the existing 120-second owned-process hard limit. These are inherited known settings and a proposed bounded feasibility gate, not a performance forecast or permission to run it now. Count all failed starts and replays. Stop at the first root-purity failure, audit conflict requiring broader trust, identity ambiguity, mismatch, unsafe cancellation or any budget limit. Report the exact missing mechanism; do not begin another open-ended adaptation campaign.

Store compact search decisions, rejection reasons, node/transition counts and timings, plus the actual selected native trace. Do not export every simulated branch as a complete 600-action native trace. Reuse immutable prefix evidence by hash/reference; any compression or archive must be lossless and verified, and originals remain preserved. Final M1 same-build complete acceptance remains required; the feasibility gate is not that acceptance.

## 6. Decision and remaining unknowns

The recommended next investment is a **solver-policy integration gate**, replacing manual local score tuning as the investigation method. Retain the current heuristic as a versioned baseline and explicit fallback, without further TestSubject-specific tuning now. This direction is already compatible with the formal M1 plan's optional solver strategy boundary.

Known facts: reusable code and prior narrow online execution exist; current native host deliberately excludes the dependency; relevant vanilla handlers exist; the latest controlled experiment remains a reproducible death. Inferences: internal fork search should explore combinations more economically than repeated native prefixes, and may improve survival; neither claim has been measured on this host. Unknowns: current complete root compatibility, affected audit patches, full current deck/relic coverage, effective search cost, winning-policy reachability and natural RunVictory continuation.

This review changed no production code, formal roadmap, game inputs, real saves, UI, other project or Git history. No new native experiment was started after the stop instruction. There is no running owned experiment to resume. The task now ends with the assessment for the parent/user to decide the next implementation scope.
