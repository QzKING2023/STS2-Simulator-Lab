# ReturnFlow / ManaConversionPrinciple compatibility repair

Date: 2026-11-02. Status: implementation handoff; no compatibility implementation or new native execution in this review. Baseline commit: `d73039d688eb54c9513f5b9508a03947e689aa18`. Stage 1 is accepted; see [review repairs](STAGE_1_REVIEW_REPAIRS.md). This bounded work covers two existing reward cards and their existing ManaEssence dependency. It does not add character designs or cards.

This document preserves the design review. Subsequent implementation, actual evidence and bounded deviations are recorded in [compatibility validation](REWARD_CARD_COMPATIBILITY_VALIDATION.md).

## 1. Fixed evidence and actual root causes

Keep game v0.111.0 / 41cef1ea and all 15 Eleanor v0.4.1 inputs. Game DLL SHA-256: `0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9`; Mod DLL: `949320F651298D34399335EB28176578D270139DFF2CCE60FB7FDF35E7DBAD42`. Accepted build: `0C4EAC45A499E41D975316D756C3865C5ABF2BAE59AA7943CBAFE8E57530FF72`.

The following are established facts, not diagnoses inferred from card names:

| Layer | Evidence | Consequence |
| --- | --- | --- |
| Coverage policy | `tools/validation/NativeSecondBattle.cs`, acquired-card check in `RunSecondBattle`, rejects any reward other than ManaShield before query. | The four old unsupported paths did not attempt the new cards. Removing this check alone cannot establish compatibility. |
| Missing effects | `EleanorV040Probe.RegisterFromLoadedMod` registers Guide, OffensiveDefense, ManaStrike, ManaGather and ManaShield; neither target nor ManaEssence is registered. `rewardCardSupport` reports both OnPlay support flags false. | Add three strictly sourced effect adapters. |
| Missing replay consumption | `EleanorV040Probe.RunVictoryReplay` parses basic action fields, calls `ManualPlay`, and installs a native selector only for Guide. | Consume the complete ordered action choices on both sides; keep the native selector alive through asynchronous action drain. |
| Shared mechanism is present but not directly sufficient | CombatSolver already has `PlanCardChoice`, choice cursors, pending-choice search, fixed transformations, generated-card identities and replay fallbacks. | Reuse these. Do not build a second search engine or silently route a fixed replacement into random transformation. |
| Audit gap in a tempting extension | `Search/CardChoiceMirrors.cs` is an unsealed dictionary of spec/apply delegates with no manifest/method/root-signature checks. | Do not register new opaque callbacks there as a shortcut around the existing strict OnPlay registration. |

Old native evidence: `.build/runs/stage-one-repair-final-01/batch/seed2-take-{a,b}/native-run-loop.json` and `seed3-take-{a,b}/native-run-loop.json`. Seeds are respectively `LAB_V0111_ELEANOR_BATCH_02` and `LAB_V0111_ELEANOR_BATCH_03`; first battles contain 18 and 9 actual actions. Rewards are acquired through native commands, copied into next-combat state, then rejected at `before-query`, with zero second-combat chunks. The acquired-card check is the observed stop; deeper compatibility failures have not yet been observed dynamically.

Local-only implementation references: `.build/decompiled/eleanor-v0.4.1-forms-cardpool.fix/`, especially `MyMod.MyModCode.Cards/{ReturnFlow,ManaConversionPrinciple,ManaEssence,M1Card}.cs` and `MyMod.MyModCode.Combat/{ManaRuntime,ManaState,ManaTransaction}.cs`. Review also read the pinned game's enum and command IL through ILSpy stdout. Do not commit these sources or game binaries.

## 2. Native contracts to preserve

### ReturnFlow

- Base and upgraded energy cost: 1. ManaCost: 1. Upgrade adds **Retain** (`CardKeyword` value 5 in the pinned game); it does not reduce cost or add another selection.
- The normal card-play pipeline spends energy and opens the role transaction. OnPlay then calls `Pay() -> ManaRuntime.PayMana(1)`. Successful payment records actual ManaPaid and awaits the existing ManaToShield side effect before either selection. Failure returns from OnPlay without selection or movement; the normal card finish still occurs.
- Process Draw then Discard. For each currently nonempty pile, select one card and call native `CardPileCmd.Add` to Hand with Bottom placement. This moves a combat card; it is not a draw or an explicit discard-cost effect.
- Re-enumerate Discard after the first movement. At the hand cap the first selected Draw card is routed into Discard and can therefore appear in the second selection. Do not precompute both candidate sets or clamp selection count to free hand slots. A full-hand Discard-to-Hand attempt may reorder that card within Discard.
- Native `FromCombatPile` sorts Draw candidates by Rarity then Id when calling the selector; equal keys keep their stable source order. Discard uses its current pile order. Empty sets return empty; one forced candidate can be selected without a selector callback.
- `ManaState.FinishCard` grants Mentor income after the complete card effect, including the insufficient-mana path: 1 for cracked orb / 2 for complete orb. It is not based on energy spent in this pinned implementation. No refund may be applied before ReturnFlow's payment decision or between its two choices.

### ManaConversionPrinciple and ManaEssence

- Principle costs 1 energy, with no additional mana debit in its OnPlay. It selects one other hand card satisfying the actual `IsTransformable` predicate. Do not replace that predicate with an Eternal-only check. No eligible card means no transform, while ordinary play/finish still happens.
- Create ManaEssence through the selected target's actual `CardScope`; upgrade and finalize the replacement when Principle is upgraded, then invoke native `CardCmd.Transform(target, replacement)`. Replacement upgrade follows Principle, not the target. There is no random replacement-pool selection.
- Native combat transform removes the target, inserts its replacement at the same hand index, records generated-card history, and invokes combat-entry/pile-change/transformation hooks. TestMode skips hand VFX. It transforms the combat instance, leaving the permanent deck intact. Validate owner, scope, replacement identity, removed target, DeckVersion relationship and permanent-deck equality from actual evidence.
- ManaEssence costs 0 energy, has **Exhaust** (`CardKeyword` value 1), grants 1 mana / upgraded 2, then draws 1 through the normal native draw command. The gain precedes draw. Apply normal no-draw, empty-deck, shuffle, hand-cap, draw hooks, exhaust and role-finish behavior. Mentor finish income is additional to Essence's base gain.
- Generated Essence is an existing token from TokenCardPool. Do not register a new reward card or add it to the permanent deck/reward pool.

These are independent expected contracts. Runtime DynamicVars and adapter outputs can diagnose differences, but must not generate expected values for the assertions.

## 3. Chosen implementation approach

Use strict OnPlay adapters plus the existing pending-choice search and ordered choice cursor. For this bounded repair, prefer **full-action replay from the immutable parent when discovering unresolved choices**. Existing solver continuation reuse is an optimization; opaque Mod transactions must not be captured in an unsafe continuation merely to retain that optimization.

For these new handlers, explicitly reject optional execution-continuation capture when a suspension cannot describe all remaining work. Return pending to search, discard that incomplete child, and let the existing search replay the whole action with the extended choice sequence. Do not finish the role transaction on a suspended partial attempt. A complete replay opens and finishes it once. Verify fallback, branch budgets and unchanged parent state. Do not globally disable continuation capture or weaken `AssertForkable`.

This avoids introducing a generic Mod transaction-frame serialization architecture in this task. If profiling later motivates true continuation reuse, it requires separately remapped transaction data and frames plus resume-versus-full-replay equivalence tests. It is not required to close these two cards.

### Minimal shared interface

Add a narrow internal **selection-only** helper on `SimulatedCombatState` (proposed name `TryResolveAdapterCardSelection`). It is called inside an already audited OnPlay handler and uses `_activeActionChoices`, `TurnStartChoiceRequest`, `CardChoiceSpec` and the existing pending-request enumeration. It returns selected branch-local cards or a pending result; it does not invoke arbitrary callbacks or execute a native command.

Required inputs: actual played predicted card, exact registered source identity/contract key, request source/context/timing, source pile, current eligible options in native selector order, source-card order, and min/max. Required outputs: selected cards or a pending request, plus an immutable evidence description. Resolve source identity against the active strict registration; never accept an unrelated card owner/type or a free-form manifest as proof.

- No new mutable global selection registry. Contract/schema constants and resolver code are frozen with the build. If implementation instead needs a persistent registry, it must gain sealing, exact source/method/manifest identity, delegate identity, patch rejection and root-signature integration before use; this is the less preferred, larger option.
- Missing choices during **search discovery** produce a pending request. Missing choices in an imported/frozen executable plan are `InvalidAction`, rejected before native dispatch. Unused, duplicate, wrong-order or wrong-context choices also fail. No candidate is a legitimate native no-op where the native API says so, not an unsupported selection or fabricated empty screen.
- Reuse `CardChoiceSupport.BuildChoices` and whole-action/nested budgets. Do not enumerate an unbounded Cartesian product or collapse all choices to the first card. Small directed fixtures must demonstrate both siblings and at least two sequential choice combinations.
- Applying selected effects stays in the exact card's audited OnPlay handler. ReturnFlow uses `AddToPile`; Principle uses `CardChoiceSupport.TransformCards` with the exact canonical ManaEssence and upgrade flag. Review that helper's native hook/history correspondence before declaring it sufficient: it already records generation and transformation callbacks, but static comparison identifies pile-change hook behavior as a point to verify. Its fixed path must not consume replacement-selection RNG.
- `TurnStartChoiceSupport.Resolve(... Transform ...)` creates a **random** replacement, so it is unsuitable for Principle. Existing `CardChoiceMirrors.Register` alone does not preserve the Lab's audit contract.

Selection helper implementation and exports can be added to the local CombatSolver overlay with exact, version-checked anchors. Keep MIT/upstream attribution. The repository stores Lab overlays/adapters, not the local upstream copy.

### Card adapters and resource lifetime

Register exact ReturnFlow, ManaConversionPrinciple and ManaEssence OnPlay implementations using the existing unpatched manifest/assembly/method contract. Preserve all late patch/state-machine patch and root-registration guards. Support the existing upgrade and keyword model, rather than reconstructing it from localized text.

ReturnFlow: begin branch transaction -> pay mana once -> apply successful-payment shielding -> resolve/move Draw -> resolve/move current Discard -> finish transaction and sync existing role state. Principle: begin -> resolve eligible Hand -> fixed transform when present -> finish. Essence: begin -> branch mana gain -> simulator Draw(1) -> finish; a pending draw-related choice must return to the same safe full-action replay discipline. Preserve existing normal result-pile/exhaust handling.

Use branch-local ManaState and existing power projections; never call `ManaRuntime.For(nativePlayer)` to execute a simulated branch. Keep paid/gained transaction fields accurate if included in new evidence. Nested automatic plays require a stack-safe transaction model or explicit unsupported with evidence; they must not reuse a mutable transaction across siblings. The bounded fixtures use supported listeners, and any new required listener is reported rather than suppressed.

## 4. Stable choice identity and actual execution bridge

Reuse the existing `PlanAction` payload: `Choice`, `NestedChoices`, `NestedChoicesBeforePrimary`, `TurnStartChoices`, and `GetActionChoicesInExecutionOrder()`. Do not discard these when importing a plan or consuming one online ticket. A one-step ticket is one card action including its complete choice sequence, not the first child choice.

Existing `PlanCardToken` carries CardId, UpgradeLevel, StateKey, SourceOccurrence and OptionOccurrence. However, current `CardChoiceSupport.MatchesToken` checks only CardId and UpgradeLevel. The new bounded resolver must additionally require the expected StateKey and verify that source and option occurrences resolve to the **same physical branch instance**. Do not globally change old occurrence grouping without regression evidence. Titles are display metadata, never identity.

Bind a choice transcript to combat identity, action boundary sequence, root/continuation stamps, registration stamp, card instance and play occurrence, selector ordinal, source pile and a versioned context (for example Draw return / Discard return / Essence replacement). Keep the existing public PlanAction schema where sufficient; store additional validation metadata in a versioned Lab action envelope or transcript. Never place expected numeric outcomes into the action identity.

Generated cards need deterministic per-world first-observation identities plus creation provenance, using `VanillaCombatRootAdapter`'s existing mechanism. Conversion must create a new identity without silently inheriting the removed target's identity; equal-looking Essences remain distinguishable. Persist this lineage in evidence and verify it through a subsequent real Essence play and forks.

Execution sequence:

1. Validate source ticket/root and deserialize the complete action. Resolve the action card by existing strict source/state occurrence, not only the first card name match.
2. On a fork, run the complete action with `BeginActionChoices(orderedChoices)` and proper card-execution scope, using the solver replay's completion rules. End the cursor and assert exact consumption and no pending choice. This validates stale/illegal/missing choices before native resources can change. Record the selection transcript and predicted stable result.
3. Install a scoped native `ICardSelector` for the **whole awaited native action** (through `FinishedExecutingActions`), resolving returned native option references against the planned transcript. It may choose only the requested, eligible actual card. Keep Guide behavior compatible and restore selector state even on errors. Do not install broad NativeChoiceRuntime Harmony patches or retail UI.
4. Distinguish semantic requests from callbacks. Native empty/singleton auto-selection may not call the provider. The transcript records these as implicit; do not demand a callback or consume the next explicit request in their place. For multi-option callbacks, verify min/max, options/order and identity. Draw uses the native Rarity/Id ordering; source occurrence uses actual pile order.
5. Record actual selector calls and implicit effects separately; verify native resulting card identities/piles, state, RNG and relevant history. Then perform the full root/continuation/parent comparison and advance the confirmed action counter once. No partially selected action is a confirmed stable boundary.

Preflight-invalid plans return InvalidAction with zero native dispatch. Unexpected native options after dispatch stop immediately, retain last-confirmed prefix, and report observed mismatch/exception with current uncertainty; do not claim a rollback or retry the partially executed native action. Every case process is disposable.

## 5. Coverage policy and original cohort

Keep native gold collection, offered-card index 0, and all three literal seeds. Extend the acquired-card coverage policy to the three audited reward types. Generalize `ExpectedSecondReward`/DeckVersion provenance checking and the required actual-play assertion; changing a white list without these requirements is insufficient.

Coverage prefix: use legal existing defend/draw/resource actions until the actual reward is naturally in hand and affordable, then execute it with an explicit bounded choice plan. For Principle, subsequently execute its actual generated Essence when legally reachable. For ReturnFlow, the insufficient-mana play is a separate boundary fixture; the representative acquired-card path should demonstrate successful mana payment and actual retrieval. No hand rearrangement, permanent-deck append, HP adjustment or replacement reward is allowed in this natural cohort.

Use the solver's candidate enumeration and a declared deterministic ranking to construct coverage selections; freeze the selected full action for repeat B. Do not choose option index 0 as a hidden policy. A chosen ranking is a coverage decision, not an optimality claim. Normal continued combat can use the existing bounded Evaluate route; the online mode remains separately labelled.

Initial bounds retain beam 8 / nodes 1000 / 5000 ms / DoP 1 and existing turn/chunk limits. Declare the two-card coverage policy before running. If these bounds cannot demonstrate the required effect or terminal, retain BudgetExceeded/NoCandidate/Death; diagnose reachability separately. Additional API fixtures may prove card semantics, but cannot replace the unchanged natural cohort or erase a failing seed. Any later budget revision is a new explicitly recorded cohort, preserving the original results.

Expected repair goal: the original four paths no longer stop at the acquired-card gate and actually use the relevant card; all 12 paths remain counted. Victory is not mandatory for a policy path, but missing required card execution cannot become a completed coverage pass. Actual matched Death is a valid strategy outcome with explicit coverage status; full repair closure still needs the directed semantic coverage and representative acquired-card executions. Budget/unsupported stops are not completion.

## 6. Acceptance matrix

Declare case IDs and expected contracts before execution. Repeat each native semantic case twice in fresh processes; sibling simulator forks are additional isolation evidence, not independent statistical samples. All cases bind the same final artifact and 15-file input lock. API setup must be labelled and use real upgrade/resource/pile APIs; never synthesize the expected after-state.

| IDs | Required scenarios and assertions |
| --- | --- |
| RF01 | Base/upgraded x mana 0/1/2: energy payment, failure no selection/no moves, successful mana -1, Draw then Discard, Retain upgrade; ordinary role finish exactly once. |
| RF02 | Both piles empty; only Draw; only Discard; singleton automatic; multiple candidates. Full source/option order and no extra/missing requests. |
| RF03 | Two cards sharing ID/upgrade but different state, and two identical instances. Choose distinct physical representatives; sibling order A/B/A produces identical A and unchanged parent. |
| RF04 | Boundary hand capacities (space for both, space for one, full at effect time). Native overflow goes to Discard; second candidates are re-enumerated and may include the first chosen card. Full hand fixture must explain actual API/autoplay setup. |
| RF05 | ManaToShield present/absent and mana payment success/failure; independent block expectation from actual paid mana. Mentor cracked/complete finish income occurs only after both choices, including failed payment. Reverse state/source stats remain consistent. |
| MC01 | Base/upgraded Principle, unupgraded/upgraded targets; energy -1, no mana cost, replacement gain value follows Principle; actual hand slot/owner/scope and permanent deck unchanged. |
| MC02 | No eligible card; only an Eternal/untransformable other card; one eligible (implicit) and several eligible cards. Source card excluded, filtered targets cannot be selected. |
| MC03 | Duplicate/equal-looking targets and pre-existing Essence; new fixed-generated identity, removed target absent from live combat, no replacement RNG draw; repeated fork lineage and generated-history correspondence. |
| ME01 | Actually transformed base/upgraded Essence played: cost 0, gain 1/2 before draw, one draw when possible, exhaust result and Mentor additional income exactly once. |
| ME02 | Draw pile empty with discard shuffle; both empty; hand-cap and supported draw-prevention cases. Compare RNG state/history, exhaust and resource gain even when no card is drawn. |
| CH01 | Wrong card/state/upgrade/owner/pile/occurrence/context/timing; duplicate selection; reordered or missing second choice; trailing extra choice; stale root/ticket/cross-combat action. Typed InvalidAction and no native dispatch. |
| CH02 | Empty/singleton native callback suppression, explicit multi-option callback matching, provider scoped through async drain and restored on failure. Illegal native offer injection/control must preserve prefix and uncertainty. |
| CH03 | Actual solver finds choices for both target cards, includes full choices in action JSON and replays them. At least two semantic siblings and two ReturnFlow combinations on small fixtures; declared caps/telemetry retained. Full-action fallback leaves parent, RNG, history and role transactions unchanged. |
| AU01 | Wrong manifest/assembly/method, existing/late patch including async state machine, late registration/root invalidation, changed contract/version and wrong replacement type rejected. Existing audit controls remain meaningful; synthetic metadata controls remain labelled. |
| CO01 | Original three seeds x skip/take x two repeats = 12 paths. Actual native acquisition, natural draw/play, choice/effect evidence, second terminal or explicit classified stop, entire denominator, deterministic frozen repeats. No seed/reward replacement. |
| RG01 | Accepted Stage 1 terminal/budget/stale/failure controls; original 16-step/122 baseline, two-battle/41, ManaShield matrix, vanilla/M3/RNG, retained Slimed actual play and online/frozen/fault prefix behavior. Main scope uses new single frozen artifact; old-role compatibility remains a separately identified build. |

Each semantic action records before/after root, raw continuation, resource values, source/ordered candidate/selected identities, implicit-vs-explicit choices, generation lineage, relevant native history and simulator history, RNG, unchanged parent and actual result. Histories with different representations use a documented semantic comparison, not fabricated byte equality. Independent literal contracts complement native/sim differential to catch shared errors.

## 7. Work packages and regression transition

1. **W1 selection/replay contract:** add the narrow selection helper and strict token resolver; integrate complete PlanAction import, simulated preflight and scoped native provider. Core/negative controls first; no new full batch while this gate fails.
2. **W2 three strict mirrors:** implement ReturnFlow, Principle and Essence against the fixed identity; use full-action replay fallback for unresolved execution. Run the directed RF/MC/ME/CH/AU cases to resolve ordinary Lab defects. Do not add unrelated cards to satisfy routes.
3. **W3 natural coverage:** generalize reward provenance and coverage execution; create a new versioned cohort declaration retaining seeds/policies/repeats. Make missing mandatory card use fail explicitly. Verify original failed paths without replacing the sample.
4. **W4 final freeze:** after repairs converge, freeze actual dirty source, overlays, compiled source, binary outputs, tools, contracts and input hashes. Run the complete declared directed and regression suites serially on this one artifact. Any subsequent runtime fix invalidates that candidate and requires a new freeze/regression.
5. **W5 evidence/report:** map case IDs to actual outcomes, raw child and suite exits, first mismatch/stopping point, coverage status and replay inputs. Summarize differences from the accepted Stage 1 baseline and request independent review through the parent.

Candidate file groups: a new Lab reward-card probe/adapter and selector module; a version-checked CombatSolver selection overlay; `EleanorV040Probe`, `NativeSecondBattle`, existing action importer/decision bridge as needed; new fixtures/driver/reviewer. Avoid adding card-specific branches throughout the search engine. Add document links when implementation results exist.

Implementation map (symbols were read in this review; the selection-only helper is a proposal, not an existing API):

| Area | Existing anchor to reuse or extend |
| --- | --- |
| Strict registration | `tools/validation/ApplyCombatSolverNoPatchCardMirrorOverlay.ps1`: `RegisterUnpatched<TCard>`, sealed registration descriptors and live patch checks; `EleanorV040Probe.RegisterFromLoadedMod` and existing ManaShield resource adapter. |
| Plan and cursor | Local CombatSolver `src/Search/CombatPlan.cs`: `PlanAction.GetActionChoicesInExecutionOrder`; `src/Prediction/TurnStartChoiceSupport.cs`: `TurnStartChoiceCursor`; `src/Search/SimulatedCombatState.ActionChoices.cs`: active cursor, pending requests and exact-consumption checks. |
| Candidate construction | Local CombatSolver `src/Search/CardChoiceSupport.cs`: `CardChoiceSpec`, `BuildChoices`, token occurrences and bounded physical representatives. |
| Fixed replacement | Local CombatSolver `src/Search/CardChoiceResolution.cs`: `CardChoiceSupport.TransformCards` / `ReplaceTransformedCard`; compare hook order and history against native `CardCmd.Transform`. |
| Search replay | Local CombatSolver `src/Search/CombatBeamSolver.Expansion.Replay.cs`: card-execution scope, ordered choices, pending fallback and stable boundary completion. |
| Native bridge | `tools/validation/EleanorV040Probe.cs`: `RunVictoryReplay`; `NativeCombatDecision.cs`: `Query` already serializes whole actions and `Ticket.Consume` retains the first complete JSON action. The loss occurs downstream, so do not create a second ticket protocol. |
| Natural coverage and evidence | `tools/validation/NativeSecondBattle.cs`: `ExpectedSecondReward`, acquired-card gate, coverage prefix and confirmed-action accounting; `VanillaCombatRootAdapter.cs`: generated instance identity. |
| Freeze and result gates | `NewStageOneBuildManifest.ps1`, `TestStageOneBuildManifest.ps1`, `ClassifyBatchResults.ps1`, `StageOneEvidenceRules.ps1` and the existing batch/Stage 1 drivers under `tools/validation/`. |

These local CombatSolver paths refer to `.build/local-reference/CombatSolver-v040-7bf941d71ad7/` (version 0.50.1). Deliver edits through checked Lab overlays, never by publishing that ignored copy. Verify every overlay anchor against the actual local version before applying.

Preserve old `stage-one-repair-final-*`, source snapshots and v1 cohort/known-gap declarations unchanged. Historical 8 Victory / 4 unsupported remains historical truth. Current regression must consume a versioned expected-coverage profile: newly supported cards are no longer allowed gaps in the new cohort; old unsupported classifications are still exercised by controlled reports. Do not leave a hardcoded expectation of exit 2 or exactly 8/4 in the new evidence reviewer, and do not loosen the classifier to accept any status. Keep exact job membership, denominator and raw-exit checks. The number of jobs/assertions will change; publish the new manifest and counts instead of reusing 34/132 as an automatic acceptance threshold.

The old manifest hashes workspace source paths as well as cached binaries. Once implementation changes sources, do not run its guard against changed files and claim a broken historical build. Reproduce the old baseline from its preserved snapshot/commit in an isolated Lab location when needed. The new final artifact must pass the retained semantic matrix, with legacy compatibility explicitly separate.

## 8. Remaining uncertainty and stopping rules

Static evidence supports the plan; no new card effect has been executed in this review. Native-vs-simulator fixed-transform hooks, callback ordering for implicit choices, generated scope/identity, pending-choice fallback acceptance and exact natural reachability under the original budgets remain dynamic gates. Hand-cap routing, keyword values and native selector sorting were confirmed directly from the pinned game; they are not open design questions.

Ordinary Lab implementation changes described here are within the authorized repair. If safe execution requires new host access, weakening patch/source audit, native UI startup, mutation of the character/game/save files, or an unbounded transaction continuation redesign, stop that dependency with exact evidence and a minimal alternative. Do not solve unknown behavior by hardcoding a selected card, copying native after-state into the simulator, disabling comparison, or reclassifying failures as supported. No commit/push, deployment, full-act strategy or new Echo cards is included in the implementation authorization for this round.
