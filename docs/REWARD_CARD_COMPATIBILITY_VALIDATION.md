# Existing reward-card compatibility

Date: 2026-11-02. Implementation baseline: local commit `d73039d688eb54c9513f5b9508a03947e689aa18`, plus the uncommitted repair described here. The candidate below passed its declared regression, then independent review found action-field rejection and identity/history evidence gaps. It is historical normal-path evidence, not final acceptance. The replacement implementation and results are tracked in [review repairs](REWARD_CARD_REVIEW_REPAIRS.md). See [repair plan](REWARD_CARD_COMPATIBILITY_PLAN.md).

## Historical candidate: declared regression result

Build ID: **`074B0CDC444DA4E8E88097BAAFB1C8F018E5B878B05839D5F596647FD1FE56CD`**.

- Manifest and immutable source snapshot: `.build/runs/reward-final-build-02/build-manifest.json` and its `source-snapshot/` sibling. The manifest binds 67 Lab source/tool rows, 867 compiled source rows and 9 artifact rows, including identical Core dependency hashes for harness and Core checks. All 15 Mod input hashes match their existing lock.
- Unified run: `.build/runs/reward-final-02/summary.json`: 35 declared jobs completed with their expected exits, final runner exit 0. The expected injected engine-error controls retain their own child exit 1 and their explicit control labels.
- Independent artifact review: `.build/runs/reward-final-02/evidence-review.json`: **135 checks, zero failures**, reviewer exit 0. Final source/output/input guard exit 0. Reviewer identity and checksum are recorded separately from execution artifacts.
- Directed cards: `reward-cards/summary.json`: **84 native processes / 42 semantic repeat comparisons, all passed**. `case-declaration.json` records exact membership; source/selector and invalid-choice controls are retained in each case's `reward-boundary.json` and rejected replay reports.
- Original cohort: `batch/summary.json`: **12 Victory, zero unsupported, budget stops, execution errors or differences**, suite exit 0. `slimed-replay/summary.json` replays the whole new cohort on the same artifact, also 12 Victory / exit 0. Four seed-2 replay paths include actual Slimed play.
- Retained checks: 16 native actions / 122 artifact checks; two-battle 41 checks; Core 81; failure-propagation controls 25; online/frozen/fault suite 27. Actual Death, budget and stale-action cases remain in the unified run. Vanilla combat/RNG/minimum progression checks also passed.

Actual new-card evidence: `batch/seed2-take-a/second-battle/second-battle.json` and its chunk traces contain ReturnFlow's real acquired-card play and selection; the corresponding `seed3-take-a` files contain Principle plus real generated Essence play. Both repeats preserve reward DeckVersion provenance. These are natural acquired-card paths, distinct from the directed API fixtures.

## Inputs and implementation

The game remains v0.111.0 / 41cef1ea. All 15 Eleanor v0.4.1 inputs remain locked; MyMod.dll SHA-256 is `949320F651298D34399335EB28176578D270139DFF2CCE60FB7FDF35E7DBAD42`. The historical Stage 1 build and its 8 Victory / 4 unsupported cohort are retained unchanged.

The observed old stop was a coverage-policy restriction before second-combat query. Native rewards had already been generated and acquired. Three missing effect mirrors and the replay consumer also needed implementation; removing the restriction alone would have left unresolved choices.

- `EleanorRewardCardMirrors.cs` registers exact ReturnFlow, ManaConversionPrinciple and ManaEssence methods with the existing real manifest/assembly/unpatched contract. Source, existing patch, late actual patch, asynchronous state-machine patch and root-stamp restoration controls execute before native scenarios.
- `AdapterCardSelection.cs`, installed by a checked Lab overlay, only resolves selections from the active ordered cursor. It validates the exact strict registration, eligible branch instances, StateKey, source and option occurrences, source/context/timing and exact choice consumption. It does not register unaudited effect callbacks.
- ReturnFlow pays mana and applies actual-payment shielding before Draw then current Discard selection. The second set is enumerated after the first movement. Principle uses the existing fixed replacement helper, with ManaEssence upgrade following Principle. Essence gains mana before the ordinary draw pipeline and exhausts normally.
- Pending new-card choices reject optional continuation capture and replay the full action from the parent with the extended choices. No suspended opaque role transaction is treated as a complete action. Existing continuation support for other content is retained.
- `RewardCardSelectionBridge.cs` keeps complete action choices, simulates the whole action first, and holds a native selector through the awaited action drain. Empty/singleton requests are implicit; explicit native offers must match ordered state keys and actual selected positions. New coverage actions additionally carry live/continuation/boundary-sequence stamps. Existing online tickets remain single use.

The fixed replacement is executed natively through the locked card's real CardScope and `CardCmd.Transform`. Simulator replacement uses the upstream fixed transformation path, not random replacement generation. Before/after roots and continuations verify live piles, generated identities, resource state, RNG and unchanged parent; actual transformed Essence is subsequently played. This bounded evidence does not establish every possible transformation listener or arbitrary Mod CardScope behavior.

## Directed matrix

The driver declares 42 cases before execution and runs each twice in a fresh process (84 native processes). These are labelled API fixtures, created using native create/move/upgrade/resource/form/power/relic APIs. They are not evidence of natural reward acquisition.

| Group | Coverage |
| --- | --- |
| ReturnFlow | Base/upgraded x mana 0/1/2; empty piles, Draw only, Discard only, singleton, multiple candidates, duplicate physical cards, alternative selection, full initial hand; payment shielding success/failure; Mentor cracked/complete income including failed payment; Reverse state. |
| Principle | Base/upgraded, upgraded and ordinary target candidates, several/forced/zero candidates, alternative choices, Eternal combat candidate, actual fixed Essence creation followed by actual Essence play. |
| Essence | Base/upgraded gain, ordinary draw, shuffle, empty deck/discard, full initial hand, no-draw power and Mentor income; Exhaust placement. |
| Choice controls | Missing, extra, wrong state, source, context, pile and physical occurrence; stale source boundary. Every invalid plan must leave native root and continuation unchanged. A separate labelled selector contract control rejects an unexpected option offer; it is not an injected native engine fault. |
| Search | Each target card has a small fixture requiring actual bounded CombatSolver choice discovery. Coverage construction also reuses bounded candidate enumeration with whole-action replay. It is labelled coverage construction, separate from online re-solving. |

Independent literal assertions cover energy/mana, payment shielding, retrieval count/hand cap, Retain, upgrade-dependent Essence gain, Mentor income, Exhaust and permanent deck count. Native/simulator differential and parent checks complement these assertions. Repetition compares complete action payloads and semantic step records, excluding per-process output paths.

The full-hand manual-play fixture starts with ten cards: playing ReturnFlow frees one slot, the first retrieval fills it, and the second overflows to Discard. A separate native autoplay with an already full hand at effect entry is not asserted. The shared AddToPile routing supports this branch, but these runs do not certify all automatic-play listeners.

The locked native `IsTransformable` predicate permits GuidingPower as a combat target despite Eternal; the permanent deck remains intact. The Eternal case follows that observed predicate. Empty Hand after removing Principle supplies the zero-candidate case; Eternal alone is not assumed to mean zero combat candidates.

## Natural cohort and regression

`tests/fixtures/reward-card-batch.json` is a new versioned coverage declaration. It retains all three original literal seeds, card offer index 0, native gold collection, skip/take and two repeats. Reward cards are naturally drawn, legally played with bounded selections, and Principle requires actual generated Essence play. The second process replays the first process's frozen routes. Original budgets are unchanged: beam 8, nodes 1000, 5000 ms, DoP 1 and existing turn/chunk limits.

The new profile permits no known unsupported gaps. The existing exhaustive classifier still checks raw child exits, typed outcomes and the complete denominator; unknown outcomes and undeclared unsupported fail. Budget stops remain incomplete. Death is a strategy terminal only when actual matched evidence confirms it. A twelve-path result is not a win-rate estimate.

The unified entry is:

```powershell
pwsh -NoProfile -File tools/validation/run-stage-one.ps1 `
  -BuildManifest <new-frozen-manifest> -OutputDirectory <fresh-lab-run> `
  -RetainedVictoryPlan <retained-sixteen-action-route> -RewardCompatibility
pwsh -NoProfile -File tools/validation/TestStageOneEvidence.ps1 -RunDirectory <fresh-lab-run>
```

It retains original combat/RNG/M3, 16-step Victory, actual Death and terminal stamps, budgets/stale actions, failure propagation, Core checks, ManaShield, two-battle, online/frozen/fault-prefix checks. It adds the new cohort, frozen cohort replay and directed card matrix. Historical v0.3 compatibility has its own older build and is not represented as part of the new artifact.

## Development evidence and remaining bounds

- `.build/runs/reward-dev-batch01/`: 12/12 actual second Victory, including both acquired cards and generated Essence. Development artifact only.
- `.build/runs/reward-dev-matrix01/`: all 60 processes passed their native scenarios; aggregate repeat comparison failed because whole reports included distinct output paths. Retained as a reviewer defect, not an engine defect.
- `.build/runs/reward-dev-matrix02/`: expanded 76 processes and semantic repeats passed on a later development artifact.
- `.build/runs/reward-dev-complete01/` and `reward-dev-solver01/`: startup audit-control failure, before native combat. The control incorrectly expected stamp capture to throw on a direct patch and sealed registration too early. The corrected control tests stamp changes and dispatch rejection after registration; production audit rules were not weakened.
- `.build/runs/reward-dev-complete02/` and `reward-dev-solver02/`: actual complete-orb failure-payment income and ReturnFlow search-discovery cases passed twice.
- `.build/runs/reward-final-01/`: vanilla passed; the 16 actual actions passed, but the artifact reviewer still required the old registration-row count. It was updated to require both exact control rows for each new card. Core checks were also rebuilt so their Core dependency matches the harness's assembly hash before the replacement freeze.

No character/game/save files, UI, deployment, commits or pushes are part of this repair. No full-character, arbitrary-listener, full-act strategy or balance claim is made. Native Death still uses the documented isolated-process boundary; worker reuse after loss remains outside the accepted scope. General automatic play and selectors introduced by unrelated draw listeners are not certified by this bounded matrix; unsupported execution still stops explicitly.
