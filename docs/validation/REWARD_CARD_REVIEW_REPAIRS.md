# Reward-card review repairs

Date: 2026-11-02. The independent review of candidate `074B0CDC444DA4E8E88097BAAFB1C8F018E5B878B05839D5F596647FD1FE56CD` found two required repairs. Its normal card/cohort executions remain valid historical evidence; it did not pass final acceptance. This document records the bounded repairs. Independent re-review is required after the replacement regression.

## Frozen candidate and evidence

Replacement build: `D5FF494B75BF670D2EFD0A335A6E87290F6C5A2C7B3C8511839CD68ACB0DFF1D`.

Manifest: `.build/runs/reward-repair-final-build-01/build-manifest.json`; source snapshot beside it. Unified run: `.build/runs/reward-repair-final-01/`. The full runner and artifact verifier both returned raw exit 0. Independent acceptance re-review is pending.

Game v0.111.0 / 41cef1ea and the existing 15-file Eleanor v0.4.1 input lock are retained. Historical accepted Stage 1 and previous compatibility evidence are preserved. This work remains uncommitted.

## Action-field rejection

Previously only PlayCard parsed `PlanAction`; EndTurn bypassed all choice validation and PlayCard ignored TurnStartChoices. Independent controls in `.build/runs/reward-review-choice-fields-01/` demonstrated two invalid plans executing to actual Victory with raw exit 0.

Every replay action now passes `ParseAction` before dispatch. It rejects nonempty TurnStartChoices, and EndTurn rejects nonempty primary/nested action choices. PlayCard retains ordered primary/nested cursor consumption and rejects unused, missing or mismatched choices. The current native consumer does not implement turn-boundary choice execution; those nonempty fields must be rejected explicitly.

`run-reward-choice-field-controls.ps1` alters a preserved legal route, labels each control, and tests four cases: EndTurn nested choices, EndTurn TurnStartChoices, PlayCard TurnStartChoices at action zero and after a legal prefix. Each child must return InvalidAction and raw exit 1. Replay evidence records native dispatch count and whether rejection left the native continuation unchanged. The declared legal prefixes are preserved and differenced; no rejected action becomes a confirmed action. This script consumes the current unified run's seed-1 route when invoked by the suite.

## Physical identities and independent generation history

Three new API fixture IDs supplement the existing 42 scenarios:

- `ReturnFlow-0-1-identity`: two identical ManaStrike objects plus a third ManaStrike with a native energy modifier; choose physical occurrence 1. A/B/A runs on three children of the same parent, verifies the repeated A's complete root/continuation/state and history projection, different physical B, and unchanged parent. The actual native play must move the selected object to Hand while leaving the other representatives in Draw.
- `ManaConversionPrinciple-0-1-identity` and `ManaConversionPrinciple-1-1-identity`: duplicate physical targets and an existing Essence matching the replacement upgrade. Execute same-parent A/B/A; transform target occurrence 1; require one actual new generation entry referring to the new Essence, local owner/creator, selected CardScope, removed target and no permanent DeckVersion. Then actually play the new Essence while the existing Essence remains in Hand.

The three fixtures run twice in fresh processes, giving 45 declared scenarios / 90 executions / 45 repeat comparisons. They remain API fixtures, not natural reward acquisition evidence.

`RewardCardIdentityProbe.cs` captures actual native CardGeneratedEntry objects and simulator generation/resolution entries added by each action. It compares stable observed instance ID, model, upgrade, owner, creator and DeckVersion status; simulation entries must be resolved. Principle requires exactly one generation for a nonempty transform, zero for a legitimate empty candidate set. These records are carried in every step's `generationHistory`, independently of the empty CombatRootV1.History field. No claim is made that the entire native and simulated history representations are byte-identical.

Permanent-deck validation now compares the ordered original references, complete native ToSerializable projections and ordered choice-state keys (including upgrades/state), before and after effects. Count equality alone is insufficient.

## Regression and interpretation

The replacement suite retains the three literal seeds, reward index 0, skip/take paths, repeats and decision budgets. It retains actual native acquisition, natural reward draws/plays and generated Essence play; actual 16-action Victory, vanilla/RNG/progression, ManaShield, two-battle, Death, budgets, stale tickets, online/frozen/fault-prefix and failure-propagation checks remain required. The new choice-field job is added to exact suite membership, and the evidence checker reads the new direct step/deck/generation and identity records.

Reproduce with a fresh Lab output directory:

```powershell
pwsh -NoProfile -File tools/validation/run-stage-one.ps1 `
  -BuildManifest <replacement-manifest> -OutputDirectory <fresh-run> `
  -RetainedVictoryPlan <retained-sixteen-action-plan> -RewardCompatibility
pwsh -NoProfile -File tools/validation/TestStageOneEvidence.ps1 -RunDirectory <fresh-run>
```

These repairs do not implement general automatic play, arbitrary draw selectors or arbitrary Mod transformation listeners. The upstream fixed transformation/movement helpers skip AfterCardChangedPiles; this bounded evidence does not prove all unknown listeners are rejected. New listener support requires its own semantic adapter or an explicit unsupported boundary. Native Death remains process-isolated because the pinned loss path does not invoke AfterCombatEnd. No character/game/save/UI changes, deployment or remote publication are involved.

## Completion record

The frozen suite completed **36 tasks**, all with their declared exits. `evidence-review.json` records **237 checks / zero failures**, verifier raw exit 0. The runner's raw exit is recorded in `.build/reward-review-repair-suite01-process.json`; the verifier's raw exit is in `.build/reward-review-repair-evidence01-process.json`. Final manifest checks pass. The manifest binds 69 Lab source/tool rows, 868 compiled-source rows, 9 artifacts and 23 host/bootstrap/Mod input rows; the existing Mod lock still has 15 files with zero mismatches. Harness SHA-256: `9EBF76E26E467D763545F374E0AACE73D3F70104BD59BE8EC8AAE568D7951452`.

| Evidence | Actual result |
| --- | --- |
| `reward-choice-fields/summary.json` | Four controls passed. Child exits are 1/1/1/1; legal confirmed prefixes and total native dispatch counts are 2/2/0/1. The rejected action contributes zero new dispatches, native continuation is unchanged, and no control reaches Victory. |
| `reward-cards/summary.json` | 90 processes, 45 repeat comparisons, 112 real card actions, 516 labelled choice controls; all pass. Each process retains its harness hash. |
| `reward-cards/ReturnFlow-0-1-identity-{a,b}/reward-boundary.json` | Source/option occurrence 1; same-parent A/B/A roots, continuations, generation projections and role state; parent unchanged; actual selected physical object retrieved. |
| `reward-cards/ManaConversionPrinciple-{0,1}-1-identity-{a,b}/reward-boundary.json` | Same-parent A/B/A, real removed target/new generation/selected CardScope provenance; old Essence retained; actual new Essence played and exhausted. The step records contain the observed instance IDs and generation-history correspondence. |
| `batch/summary.json`, `slimed-replay/summary.json` | Each retains all original 12 paths, 12 actual second Victories and 320 confirmed native/simulator actions; zero unsupported, budget stops or differences. No seed/reward-index/budget replacement. |
| Retained regressions | Actual 16-action/122-check Victory, two-battle 41 checks, Core 81, failure propagation 25 and online/frozen/fault 27 all pass. Online cumulative actions remain 18; mid-query fault retains 11 total / 2 second-combat actions and Unknown outcomes. |

The artifact verifier directly reads every new card replay's steps, deck projections and generation records, and checks identity proofs; it does not rely only on the matrix summary flags. The prior 35-task/135-check candidate and the independent rejection controls remain unchanged.

Development checks are retained separately in `reward-repair-dev-fields01`, `reward-repair-dev-rfidentity01`, `reward-repair-dev-mcidentity01`, `reward-repair-dev-identity02` and `reward-repair-dev-identity03` under `.build/runs/`. They do not replace the frozen final suite. No execution/permission blocker remains. This completion record describes implementation/regression results; the requested independent re-review must decide acceptance.
