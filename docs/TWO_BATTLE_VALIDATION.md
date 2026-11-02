# Two-battle validation — local milestone

The minimum-loop source was committed and pushed to main at **2c819510817f64f7fddc7d315b05282f6463f528** before this stage began. GitHub's branch ref matched that SHA. The commit had no status records, check runs or workflow runs; the combined status endpoint's default pending value did not mean a CI job was running.

This extension is recorded as a local milestone and has not been pushed. Game and Mod pins remain v0.111.0 / 41cef1ea and Eleanor v0.4.1-forms-cardpool.fix, DLL SHA-256 **949320F651298D34399335EB28176578D270139DFF2CCE60FB7FDF35E7DBAD42**.

## Actual execution

Both policies execute the same generated first battle, actual Victory, native Gold/Card/Potion rewards, legal next node and the second combat. No HP, mana, hand order or deck setter is used to create the result.

| Policy | First rewards | Second battle |
| --- | --- | --- |
| Card-skip | Native Gold 99 → 119; skip remaining Card/Potion; deck 11 | Nine actual actions through Victory on turn 3, HP 70 |
| Card-take | Native Gold 99 → 119; take offered ManaShield; skip Potion; deck 12 | 33 actual actions through Victory on turn 10, HP 61 |

The take policy explicitly prioritizes coverage: it defends and ends turns without attacks until the acquired ManaShield is naturally in hand, then plays it before completing the remaining combat plan. This is a declared test policy on the existing generated route/seed, not an optimal strategy or evidence that taking the reward causes greater HP loss. It does not cover all seeds.

The actual reward's DeckVersion reference is checked when resolving each ManaShield play from the native hand. It is first played on turn 3, then on turns 5 and 8 after normal redraws. The mirror reads the real card's dynamic variables, tries the declared ManaCost, applies the appropriate block and finishes its branch-local transaction. Per-action roots/continuations match native execution and parent forks remain unchanged.

Second-battle action prefixes are explicitly reported as boundary, not terminal passes. Each prefix captures a fresh exact root. The overall stage requires a real final CombatWon/Victory. Terminal-capture and hover-tip prefixes are installed idempotently so the second fight does not duplicate callbacks.

## Evidence and checks

Final two-battle cohort: ignored local directory **.build/runs/eleanor-native-loop-20261102-003838-388/**. All five native process executions exited 0. After correcting a PowerShell property filter in the artifact checker, the existing emitted reports passed **41 checks, 0 failed**, without rerunning those successful native processes. The earlier failing summary is retained as repeat-summary-before-review-fix.json. The corrected review is marked reviewOnly=true and retains the original cohort's recorded binary hash.

The cohort includes skip/take twice each plus the real Recording/Echo API fixture. Second-battle chunk plans are frozen separately per policy and compared with complete native/simulator trace data across processes. The power fixture still establishes nonzero Echo in battle one, then verifies native cleanup and a clean second startup.

Each case retains:

- native-run-loop.json: both battles, native rewards, route protocol, full lifecycle and write guards.
- second-battle/chunk-plan.json: frozen actual actions and chunk boundaries.
- second-battle/second-battle.json and chunk reports: per-action roots, continuations, terminal events and reward-card provenance.

Gold, permanent deck and relics persist through both fights. HP follows real native damage and agrees with the simulator. Both native combat teardowns clear form, Mana, Block, Recording powers and Echo. The final guards report MemorySaveStore, TestMode true, Steam false, **2 intercepted replay writes and 4 intercepted progress saves**, with persistent writes disabled.

Regression evidence:

- Existing minimum-loop suite: .build/runs/eleanor-native-loop-20261102-004339-251/, five processes / 24 checks, exit 0.
- Preserved 16-action Victory: .build/runs/eleanor-v041-20261102-004226-320/, execution exit 0 and 122 artifact checks passed. Its strict-registration review now accepts the additional complete ManaShield negative-control row while retaining compatibility with the original nine-row evidence.
- Vanilla/earlier-version regression: .build/runs/vanilla-m1-20261102-004717-863/milestone-summary.json, exit 0; native vanilla Victory/RNG, earlier Eleanor effects/cross-turn/Victory and vanilla M3-skip all passed. NuGet emitted NU1900 for an unavailable vulnerability-data endpoint; the checks completed successfully.

A concurrent attempt to rebuild the same harness cache during execution hit a DLL file lock and failed. It was not counted as a passing regression; the successful baseline run was performed serially. No unrelated process was terminated.

## Reproduction and limits

Use the external configuration in VALIDATION.md. Build with the existing native-loop command plus **-SecondBattle**, then run the cases driver with the printed harness directory and **-SecondBattle**. The default cases command without that switch retains the first-action milestone. **-ReviewOnly -OutputDirectory <existing-suite>** reviews already emitted artifacts and does not execute native game actions.

ManaShield is verified here at its base upgrade level with sufficient mana and the actual fixture's powers/resources. Separate insufficient-mana, upgraded-card and additional power-combination fixtures have not been run. Full character support, six candidate Echo cards, events/shops/later routes, balance and full-climb strategy remain outside this stage.
