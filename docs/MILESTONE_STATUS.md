# Milestone status

## Stage 1: terminal outcomes and failure evidence

The first implementation candidate completed 33 jobs / 93 evidence assertions, then independent review found three result-gate/progress defects. It was not accepted. Those fixes now pass a fresh frozen regression: 34 jobs / 132 artifact assertions, Core 81 checks, 25 failure controls, online/frozen/fault 27 checks, original 16-action/122-check and two-battle/41-check baselines. Seed cohorts retain 8 Victory / 4 unsupported and exit 2. See [STAGE_1_REVIEW_REPAIRS.md](STAGE_1_REVIEW_REPAIRS.md) for exact identity and scope; independent re-review passed on 2026-11-02, closing Stage 1 within its documented scope.

Native loss does not invoke AfterCombatEnd: role resources/subscriptions remain in this pinned TestMode path. This is recorded behavior, not a forced cleanup or full retail run-loss validation. See [STAGE_1_VALIDATION.md](STAGE_1_VALIDATION.md) for hashes, the complete acceptance matrix, commands and limits. The accepted Stage 1 changes are being recorded in a local commit; this milestone has not been pushed.

Validated game: **v0.111.0 / 41cef1ea**, main assembly hash **222455745**.
Eleanor input: **v0.4.1-forms-cardpool.fix**, DLL SHA-256 **949320F651298D34399335EB28176578D270139DFF2CCE60FB7FDF35E7DBAD42**.

## Passed bounded coverage

| Area | Evidence scope |
| --- | --- |
| Vanilla M1 | Fixed native/simulator combat through Victory; two-target random selection and RNG replay |
| Earlier Eleanor baseline | Four named effects, Complete Orb Mentor draw/upkeep, mana-to-block and repeated two-turn Victory; locked to v0.3.0-m3.burst23 |
| Eleanor v0.4.1 combat | Guide's two native choices, conversion/OffensiveDefense, Record/Echo API, temporary Recording expiry, Reverse upkeep success/failure and owned attributes; separate 16-action five-turn native Victory |
| Eleanor minimum run loop | Actual generated-map start, nine native actions through Victory, native reward offer/selection, legal child entry, next combat's first actionable exact-ready root |
| Cross-combat API fixture | Real Recording powers and blocked incoming damage produce nonzero Echo; native combat teardown clears combat resources before next startup |

The preserved 16-action combat baseline passed **122 independent artifact checks**. The minimum-loop suite passed **five independent processes and 24 checks**, with final exit 0. Vanilla/earlier Eleanor regressions also exited 0. Evidence is generated locally and is not distributed with this source repository.

## Native reward policies

Both policies collect the native 20 Gold reward, increasing Gold 99 → 119 while retaining HP 70 and the starting relic.

- **Card-skip:** skip the remaining Card/Potion rewards; permanent deck remains 11 cards.
- **Card-take:** select offered card index 0, ManaShield; skip the remaining Potion reward; native deck addition increases the permanent deck to 12.

The original options are ManaShield, CarefulDeduction and WasteToTreasure. The acquired card is cloned through the game's DeckVersion link into the next combat draw pile; it is not placed into the opening hand by the fixture. The route starts at generated coordinate 3,0 / ActFloor 1 and enters legal Monster child 0,1 / ActFloor 2.

Every combat action retains native/simulator roots, full raw continuation text and parent-fork isolation checks. The first passed native plan is frozen and reused across the repeated policies. The nine-action plan SHA-256 for the recorded milestone is **4F01FC79E9559C6A7FA94A3778C11099A532E75C8674139B853ECA5C6064558C**.

The separate power API case starts with Recording 1 and TemporaryRecording 2. Actual 1 Block and 1 incoming damage produce Echo 3 without HP loss. Echo reaches 15 before Victory; the temporary layer expires at the next player start while permanent Recording persists within the fight. Native teardown clears powers, Block, Mana, form and Echo. The next Cracked Orb combat opens at Mana 3, Neutral and Echo 0.

## Result interpretation

Native reward effects and map travel execute through game APIs. RunProgressRules validates phase, offered choices and legal map edges; it does not independently predict reward effects or reward RNG. Separate native processes implement policy branches; the Lab does not clone the entire RunManager.

The published minimum-loop snapshot stops at the next battle's first actionable state. The subsequent local two-battle milestone adds ManaShield's strict OnPlay mirror and actual play after natural drawing; see the extension below.

The supported bootstrap is a limited offline test profile. It uses genuine manifest/assembly ownership and original BaseLib content-discovery patches, but does not establish full BaseLib/Ritsu startup, PCK loading or retail collection rendering. A short user-reported retail smoke is separate from automated coverage. Earlier v0.4.0 retail integration failure remains tied to that older binary.

## Remaining limits

Complete Orb Reverse upkeep, exact overkill cases, more card effects, full retail callbacks and multiplayer/full loader compatibility remain unverified. No complete role, full-climb, optimal route or balance conclusion is claimed. The six planned Echo cards are absent from the pinned DLL.

Reproduction and output layout are documented in [VALIDATION.md](VALIDATION.md).

## Unpublished second-battle extension

The local milestone adds strict ManaShield OnPlay adaptation and actual acquired-card play after natural drawing, then drives skip/take through the second Victory. The five-process cohort passed 41 artifact/repeat checks. This work is separate from the published minimum-loop commit and has not been pushed. Scope, fixture policy, evidence and regressions are recorded in [TWO_BATTLE_VALIDATION.md](TWO_BATTLE_VALIDATION.md).

## Local bounded validation stages

- ManaShield base/upgraded × Mana 0/1/2, each twice: 12 native/simulator executions and six full-repeat comparisons passed. Insufficient mana still produces base Block 5; successful payment gives Block 10 / upgraded 13. All spend 1 energy, and parent forks remain unchanged.
- Three declared seeds × card-skip/take × twice: all 12 paths recorded, 8 actual two-battle Victories, 4 explicit unsupported, no observed Death or semantic failures, 30 checks passed. ReturnFlow and ManaConversionPrinciple acquisition work natively, but their acquired-card coverage policies are unsupported; they are not replaced with ManaShield.
- A retained-plan regression actually plays generated Slimed twice after fixing independent generated-card identity and action resolution. Exact roots, ordered piles, raw continuations and audit checks remain required.
- Bounded online decisions: two independent nine-query/nine-action executions and a zero-query frozen replay reach actual second Victory; 15 checks passed. The online scope is the original seed's card-skip second combat, with fresh source validation and exact action verification before replanning.

The batch driver exits **2** for unsupported paths, rather than reporting an all-pass result. These fixed policy fixtures do not establish a full-run win rate. Implementation, declarations, output directories and reproduction are in [EXTENDED_VALIDATION.md](EXTENDED_VALIDATION.md). This turn creates no commit or push.
