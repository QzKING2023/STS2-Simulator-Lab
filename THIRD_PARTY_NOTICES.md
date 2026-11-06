# Third-party notices

## CombatSolver 0.50.1

The Lab integrates with the MIT-licensed CombatSolver reference and includes exact upstream source anchors in local overlay scripts. The external source/build copy is not committed. Retain its LICENSE in local copies and distributions of substantial portions.

Project: https://github.com/Torch1230/CombatSolver
Copyright: Torch1230 and contributors; hotwords123 for portions derived from Random Foreseer.

Lab modifications add strict unpatched-method adapters, version/source audit checks and isolated validation glue. They do not change the ownership of upstream portions.

The separately locked local `terminal-hooks.1` derivative reuses CombatSolver's existing ending predicate at two regular turn-start fast paths. Its build script retains the original MIT license and records the source patch and dependency hashes. It is a Lab derivative of 0.50.1, not an upstream release; no game implementation source is distributed with it.

The local `strict-branch-runtime.1` derivative retains the explicit upstream branch AI and replaces only its generic captured-root conditional fallback with Unsupported. It is built from the separately preserved terminal derivative with the same MIT attribution; no new monster-specific Lab implementations or upstream release claims are introduced.

The local `tainted-lifecycle.1`, `illusion-admission.1`, and `tainted-root.1` CombatSolver derivatives preserve the same MIT license and source attribution. They distinguish attached power removal from death detachment, admit the existing power-owned revival move, and initialize the immutable power-owner baseline at capture. Original parent snapshots remain separate. These are Lab derivatives, not upstream releases; no proprietary game implementation source is included.

## Random Foreseer

CombatSolver's simulator includes portions derived from Random Foreseer. Attribution and source relationship are retained here without copying private permission records.

Project: https://github.com/hotwords123/StS2.RandomForeseer
Author and copyright holder: hotwords123. License: MIT.

The native-run Foreseer adapter also reuses the `PredictRelicRewards(Player, IEnumerable<RelicRarity>, filter)` method body and the already-generated next-act read from RandomForeseer 0.13.19. Copyright (c) 2026 hotwords123. The original source snapshot, version manifest and complete MIT license are hashed locally; distributions of these portions must retain this notice and the MIT text below. UI, telemetry and the Mod initializer are not integrated.

The ThisOrThat scalar preview additionally reuses `Clone(Rng)` and the two `PredictRelicRewards(Player, int[, Rng])` overloads used by the upstream event predictor. Their source and event registration are separately locked. The unchosen preview is not a verified native pickup prediction; the UI and pickup-tip chain remain outside this integration.

The SlipperyBridge adapter additionally reuses the upstream one-step candidate selection from `SlipperyBridgePrediction.GetHoverTips`, with cloned RNG and skipped-card tracking. It returns scalar card identity/RNG instead of UI hover tips and fixes the preview count at one without initializing Mod settings. Native event execution is separate; the pinned source, MIT attribution and license remain retained.

The Reflections adapter reuses the clone-on-write card and RNG projection from RandomForeseer 0.13.19 (`ReflectionsPrediction`, `PredictedCard`, and `PredictionUtils`). It exposes a scalar upgrade/RNG comparison separately from native execution; it does not initialize upstream UI or claim a complete effect prediction. The independent `native05-reflections` local snapshot retains all 13 hashed inputs and the MIT license, and `prepare-foreseer-reflections-inputs.py` reproduces that snapshot from locally supplied upstream sources. Copyright (c) 2026 hotwords123; the MIT text below applies to reused portions.

## External game and Mod dependencies

Slay the Spire 2, BaseLib, RitsuLib and Eleanor inputs are locally supplied dependencies. Their implementation source, binaries, assets and licenses are not replaced by this project's MIT license. The repository includes API-facing adapters and independent behavioral checks, not decompiled game source or redistributed dependency binaries. Consult each dependency's own license before reuse or redistribution.

## Upstream MIT license retained for applicable source anchors/portions

MIT License

Copyright (c) 2026 Torch1230 and contributors
Copyright (c) 2026 hotwords123 (portions derived from Random Foreseer)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.


## Lab ending power boundary derivative

The separately locked `ending-powers.2` compute input derives from the reviewed CombatSolver 0.50.1 `strict-branch-runtime.1` input. Its new change is a common power application terminal guard using the existing branch-local `IsOverOrEnding` predicate. Upstream MIT license and attribution remain in each local input snapshot; no game assemblies or decompiled game sources are redistributed. See `docs/COMBAT_SOLVER_ENEMY_PHASE_VALIDATION.md` for validation scope and unresolved native replay.

## Lab native possession root derivative

The separately locked `possessed-root.1` compute input derives from the existing `ending-powers.2` CombatSolver 0.50.1 input. It seeds the existing forkable stolen-attribute ledgers from native root data and retains upstream refund/fork/fingerprint implementations. This is a local Lab derivative, not an upstream release or a new game implementation. Original MIT attribution is retained; no scoring changes, Mod initialization or deployment target is added.

## Lab side-history and self-cost ordering derivatives

The independently locked `turn-history-side.1` and `self-cost-order.1` inputs derive from the existing MIT CombatSolver 0.50.1 inputs. They reuse its side-aware history predicate, damage hooks, forkable execution continuations, and primary card mirrors. Lab changes invalidate cached per-turn counters on side changes and move the existing sealed HP-cost card group to its before-primary phase. The native bridge also reuses the upstream card execution scope and deferred death settlement. These are local derivatives, not upstream releases; attribution and the complete MIT text remain in the input snapshots. No game binaries, assets, or decompiled game source are included in repository deliverables. Validation scope and retained failures are recorded in `docs/M1_ACCEPTANCE_TERMINAL_POLICY.md` and its acceptance report.

The subsequent `death-amount-sync.1` input uses the same upstream amount-state synchronization and death-removal machinery. Pending predicted power amounts are synchronized before the common authoritative removal boundary, preventing a later synchronization from restoring a removed power. This is a general lifecycle correction, not a monster-specific rule. Its separate input lock retains its parent identity, changed source hashes, compiled output, and upstream MIT license. Validation includes a two-action native fixture and retained natural-run failures followed by an exact corrected continuation; it does not establish coverage of every death or revival hook.

The separate `osty-root-identity.1` input derives from `death-amount-sync.1`. It captures each player's pet identity (including absence) into the upstream forkable dictionary at root construction and reuses its existing fork, summon, creature-state and continuation implementations. It prevents a captured branch from consulting a later live pet reference. This is a local Lab correction, not an upstream release; it changes neither search scoring nor game behavior. The preparer retains the parent lock, exact source changes, compiled hash and original MIT license. The prior M1 input remains intact.


### Local derivative: orb terminal boundary

The performance compatibility pilot reuses the MIT-licensed CombatSolver 0.50.1 source and its existing `osty-root-identity.1` lineage. `orb-terminal-boundary.1` changes one simulator lifecycle method: after the original orb callbacks return, an already-ending combat is committed through the existing `CheckWinCondition` before side-transition bookkeeping. It does not change search scores, game card effects, native game files, or licenses. The reproducible local builder is `tools/native-run/prepare-solver-orb-terminal-inputs.py`; exact parent/source/artifact hashes and MIT attribution remain in its Lab-local lock. Game and bundled proprietary assemblies remain local inputs and are not redistributed.

The local `duration-ending-boundary.1` and `combat-hook-ending.1` CombatSolver derivatives retain the pinned upstream MIT license and attribution. They adapt duration decrement and combat-hook entry termination to the fixed local game API; the preparation scripts preserve parent hashes and source diffs. Local game assemblies and decompiled references are not redistributed.
