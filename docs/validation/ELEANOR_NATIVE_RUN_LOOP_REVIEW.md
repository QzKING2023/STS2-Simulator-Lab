# Eleanor v0.4.1 offline native run-loop review

Date: 2026-11-01. Status: **design reviewed; implementation and runtime acceptance NOT RUN in this review**.

## Decision and scope

Proceed in three ordered gates: (1) genuine Mod identity and canonical pool discovery, (2) native rewards with injected decisions, (3) legal map travel and the next battle's first actionable boundary. Keep each gate separately reportable. Do not begin a full battle replay until the pool-only gate passes.

Use the pinned game v0.111.0 / 41cef1ea and Eleanor `v0.4.1-forms-cardpool.fix`. MyMod.dll SHA-256 remains `949320F651298D34399335EB28176578D270139DFF2CCE60FB7FDF35E7DBAD42`; manifest SHA-256 is `D6267641F5CDB4A4B90B64E20164801DDE0E57A9CCEB818A9DB8C000E1C40637`. Preserve the existing 15-input lock and all earlier evidence. Add a separate bootstrap/profile lock if additional dependency manifests are needed; do not rewrite the old lock to conceal a changed input set.

The deliverable is a **native game-core progression driver with simulator battle handoffs**. `RunProgressRulesV1` currently validates phase, reward-choice and map-edge legality; it does not independently calculate gold, reward RNG, card acquisition or combat startup. Calling its state transitions is not an independent reward-effects differential. Reports must distinguish native command evidence, protocol checks, deterministic repeat checks, and native/simulator combat-state comparisons. Full run-state prediction/forking is outside this milestone.

No role source changes, game installation writes, user saves, Steam Cloud, UI, deployment, commits or pushes. Keep MemorySaveStore, Steam disabled, and SaveProgress/replay interception. Do not retry BaseLib's full initializer in the non-Godot process.

## Evidence and code anchors

Existing baselines remain valid only within their recorded scope:

- v0.4.1: `.build/runs/eleanor-v041-20261101-165958-362/reverse/eleanor-v041-fixed-route-victory-replay.json`, schema v2; companion `victory-evidence-review.json`: 122 checks, zero failed. Sixteen real actions, five turns, four enemy phases, actual CombatWon and completed teardown. The fixed plan SHA-256 is `975F7371F401AE9987FF1746E5A4D9F2A320D72BE9A05E2784A26B589C034D2F`.
- Vanilla M3: `.build/runs/vanilla-m1-20261101-132258-875/native-run-loop/native-run-loop-differential.json`: explicit native room-end offer, Gold/Card skip, legal generated-map child entered. It does not establish card acquisition or the next actionable combat state.
- v0.4.1 user-reported short retail smoke passed. This review performs no retail verification. Historical v0.4.0 UI/draw failure, current headless Pool failure, and absent six candidate Echo cards remain recorded.

| Source | Confirmed behavior / implementation anchor |
| --- | --- |
| `tools/validation/EleanorModBootstrap.cs:34,82,103` | Loads actual pinned DLL/manifest, invokes Eleanor initializer, injects model types. Existing association occurs without a native Mod record; it cannot repair ModelDb pool enumeration by itself. |
| `tools/validation/EleanorV040Probe.cs:285` | Existing real Victory replay and root/fork comparison to reuse. Keep its pre-teardown snapshot distinct from post-teardown state. |
| `tools/validation/NativeRunLoopProbe.cs:36,53,116,182` | Existing explicit OfferForRoomEnd, next-node entry and skip selector. Extend as a new scenario; restore selectors in finally. |
| `src/SimulatorLab.Core/RunProgressProtocol.cs` | Phase/choice/edge validation only; extend evidence/schema for actual run-resource snapshots and next-combat readiness. |
| Game `ModManager.ResetForTests`, `Initialize`, `TryLoadMod`, `AssociateAssemblyWithMod` | Public test reset enables initialization. Native loader parses manifests, checks dependencies/version/consent and calls `TestInitializers` in TestMode. Association must precede AssemblyInfo/model discovery. |
| Game `ModelDb.Init/InitIds/AllCardPools`, `CardPoolModel.AllCards`, `ModHelper.AddModelToPool/ConcatModelsFromMods` | Constructors precede IDs; pool membership is registered then frozen at first consumption. AllCardPools combines character and shared pools. Late registration must fail, not be repaired by editing caches. |
| Pinned BaseLib `CustomCardModel`, `CustomCharacterModel`, `CustomCardPoolModel`, `CustomContentDictionary` | Real constructors register cards by inherited PoolAttribute, characters, and shared pools. Membership reaches native ModHelper. |
| Pinned BaseLib `AddCustomCharacters` and `ModelDbSharedCardPoolsPatch` | Original Harmony postfixes target **ModelDb.AllCharacters getter** and **ModelDb.AllSharedCardPools getter**. Targets confirmed with reference-resolved decompilation. These expose the real constructor registries. |
| Game `RewardsCmd.OfferForRoomEnd`, `RewardsSet.GenerateWithoutOffering/Offer`, `CardFactory.CreateForReward` | Native encounter rewards, population, hooks, odds and RNG. RewardsSet.testSelector runs after generation in TestMode. |
| Game `CardReward.OnSelect`, `RewardsSetSynchronizer.SelectLocalReward/SkipLocalRewardsSet` | Native choice synchronization, actual CardPileCmd.Add to Deck, pick/skip history, reward completion. |
| Game `RunManager.GenerateMap/EnterMapCoord/EnterMapPointInternal/ProceedFromTerminalRewardsScreen` | Native map, encounter selection, room exit/history/startup; proceeding from rewards alone merely opens the absent map UI in a normal combat. |
| Game `Player.AfterCombatEnd/ResetCombatState/PopulateCombatState` | Clears all combat powers and Block; new combat state clones the current permanent deck and sets each clone's DeckVersion. |

Game methods above were inspected read-only from the installed pinned sts2.dll through ILSpy, without executing the target assembly. BaseLib and Eleanor sources inspected under `.build/decompiled/baselib-3.4.7/` and `.build/decompiled/eleanor-v0.4.1-forms-cardpool.fix/` correspond to the pinned inputs. Preserve MIT/upstream attribution when reusing the existing harness.

## Gate 1 — minimal truthful loading and pool discovery

### Initialization order

1. Fresh process; verify game and all pinned file hashes, enable TestMode, assert Steam false, install the existing audited presentation shims and MemorySaveStore/write guards **before any initialization**. Never read real settings/progress.
2. Prepare a read-only `IModManagerFileIo` view of only the approved pinned manifests and DLL paths. Native enumeration must receive the original manifest bytes, not invented manifests. Writes/copies through this interface must fail and be recorded. The original 15-file pin lacks BaseLib/RitsuLib manifests: capture and hash their authentic, version-matching manifests in a new Lab bootstrap snapshot before attempting native discovery.
3. Call `ModManager.ResetForTests()` and its real `Initialize(fileIo, inMemorySettings, explicitGameVersion)`. Use test-only in-memory enabled entries for the actual manifest IDs; no user setting is changed. Native dependency validation must succeed for MyMod → BaseLib 3.4.7 / STS2-RitsuLib 0.6.10. Reject loader errors even if its state happens to say Loaded.
4. Use the documented `TestInitializers` seam to load the exact pinned assemblies, associate each actual assembly with its real manifest ID, and execute the declared headless initialization profile. MyMod's actual MainFile.Initialize must execute once. For BaseLib, invoke the **original two discovery patch processors**, with their original BaseLib owner/assembly/method identity, and real constructors/registries; do not write handcrafted pool arrays. Do not call BaseLibMain.Initialize, PatchAll(BaseLib), OS.AddLogger, NodeFactory or save/config initialization.
5. Before claiming this profile sufficient, inventory the dependency hooks reachable in this bounded scenario. RitsuLib's RegisterModAssembly records its own discovery mapping; it is not itself the game's ModManager association. Do not label an empty callback as full framework initialization. Any required Ritsu/BaseLib gameplay hook must run through its original method or a strictly audited, explicitly scoped adapter. Missing semantics are unsupported, not silently omitted. Report the exact partial-initialization profile separately from native loader status.
6. Perform AssemblyInfo and native model discovery only after association. Remove the new scenario's shortcut that manually sets ModManager.State to Initialized / substitutes an empty ReflectionHelper cache. Prefer native discovery; the existing public `ModelDb.Init(injectedModelTypes)` test seam is acceptable only with a complete, provenance-checked model inventory for the supported profile, never manually invented pool membership. All actual Eleanor models must remain present.
7. Complete ModelDb.Init, model-ID cache and InitIds before warming any AllCards/AllCardPools. Register all constructor-derived membership before ModHelper freezes the pools. Use fresh processes instead of clearing private caches to repair a bad order.

### Environment seams that need bounded implementation checks

`ModManager.Initialize` still calls CommandLineHelper, `Godot.OS.GetExecutablePath()` and `OS.IsDebugBuild()` before/around scanning. A read-only file IO adapter alone cannot make it safe in a plain .NET host. Use narrowly scoped, documented environment-query replacements consistent with this Lab process, mapping the loader's virtual mods directory to the pinned view. Do not let it enumerate the real installation/workshop. Existing platform-branch shims must not override explicit game/version checks.

The loader can also call `CopyUnmoddedSaveFilesIfNeeded` on a first modded profile. Supply a nonempty in-memory ModList of the genuine enabled mods and install an assertion guard that rejects any attempted copy before path traversal. A copy attempt fails the run; returning success for a fake copy is forbidden. These are proposed Lab isolation measures, **not runtime-proven in this review**. If a narrow environment seam cannot be installed safely, stop with its exact target and unsupported reason; do not widen host access.

Authentic dependency manifest locations inspected: `D:\STS2-Local-Research\references\libs\BaseLib\BaseLib.json` (v3.4.7) and `D:\STS2 Mod\libs\RitsuLib\mod_manifest.json` (0.6.10). Their bytes are not yet locked for the new profile. The latter source tree is reference material, not proof that every source method equals the pinned runtime DLL; verify any reused hook against that DLL.

### Pool acceptance before battle

- Real ModManager record/assembly ownership exists before model IDs; no absent-association warning. Report DLL hash, MVID, manifest hash, load order, callback results and exact installed patch inventory.
- ModelDb.AllCharacters contains the actual Eleanor canonical type once. AllCharacterCardPools contains MyModCardPool; AllSharedCardPools contains the actual StarterCardPool once.
- Query Pool for all starting cards and all native reward-candidate cards, including GuidingPower. No caught exception/default fallback qualifies as success.
- GuidingPower.Pool is StarterCardPool and VisualCardPool is MyModCardPool. It is **Common in this binary**, not Basic. Native `CardCreationOptions.ForRoom` uses `player.Character.CardPool`, so GuidingPower must not leak into the ordinary MyMod reward pool. Keep the real PoolAttribute memberships; do not filter a wrongly merged pool to hide the error.
- Record native unlocked candidate identities/rarities and enough distinct eligible candidates for the requested offer. FormChoice tokens and the six absent Echo cards must not be invented as rewards. Do not use CardReward's explicit-card constructor, WithCardPools, RNG overrides or hand-assigned choices.
- Snapshot the pool/patch/profile identity in the run provenance and root/session stability checks. Reuse existing strict unknown-patch audit; a manifest's affects_gameplay=false flag is not a blanket exemption. If an original discovery patch needs exact recognition, implement a narrow signature-bound descriptor and negative controls, not owner-wide trust.

## Gate 2 — one actual run through native rewards

Create a new M3 fixture using native run setup, GenerateMap and entry at the actual generated starting point. The existing v0.4.1 route began via EnterRoomDebug; old vanilla M3 also records floorBeforeNextNode=0 while its starting coordinate is row 1. Preserve those baselines, but do not transplant a fake visited coordinate/history into the new fixture to claim natural progression.

Generate the map and enter a real Monster node through `EnterMapCoord`. With MemorySaveStore verified, its normal SaveRun is in-memory; retain the separate persistent Progress/replay guards. The explicit test alternative used by old M3 is AddVisitedMapCoord plus EnterMapPointInternal(saveGame:false); if retained, assert all public-entry-equivalent coordinate/floor/history behavior and label the seam. Never use EnterRoomDebug or force an encounter to claim legal map selection.

The first encounter and shuffle may differ from the old debug fixture. Use a fixed legal action script for this new opening and validate every native action against a fork; preserve the old 16-action baseline separately. A solver may supply the plan, but actual native execution must reach CombatWon, drain its queue and complete teardown. Do not change HP/resources or force a terminal flag. Unsupported actions or actual Death end that scenario truthfully.

After actual Victory and completed teardown, call `RewardsCmd.OfferForRoomEnd(player, sameCombatRoom)` exactly once. In this offline host there is no retail UI callback; this explicit command is the intended driver. If a native callback already offered the set, consume that one rather than generating twice. Capture the offered reward IDs, concrete types, full native card options/upgrades, gold amount, potion result/odds, alternatives, RNG and history before making decisions.

Use two policies in **fresh isolated processes**, each executing the full shared prefix:

| Policy | Native decision sequence |
| --- | --- |
| Card-skip | Select GoldReward via RewardsSetSynchronizer.SelectLocalReward; then SkipLocalRewardsSet for pending card/other rewards. |
| Take-card | Select the same gold reward; select CardReward through SelectLocalReward; `ICardSelector.GetSelectedCardReward` returns one actual offered option using a recorded deterministic rule; then skip any remaining optional rewards. |

Keep the older all-reward-skip vanilla case as a separate regression. Choosing the first offered index is an adequate policy; this is not reward optimization. A chosen card that lacks a simulator mirror must be reported explicitly at the next capture. Adapt only the required card within existing strict Lab scope, or report unsupported. Never replace it with a supported card that was not offered, discard it, or pretend root capture supports it.

Install `RewardsSet.testSelector` and `CardSelectCmd.UseSelector` in a scoped lifetime; restore both on failure and completion. CardReward.OnSelect invokes the real choice synchronizer, then CardPileCmd.Add to Deck and pick/skip map history. Verify the result of the real addition, including any legitimate replacement hook, rather than assuming an identical object/reference. No manual deck append.

### Reward UI boundary

`NCardRewardSelectionScreen.ShowScreen` already returns null in TestMode; CardReward then uses the selector. However **CardReward.Populate already constructs CardRewardAlternative**, whose constructor reads MegaInput's Godot StringName hotkeys. This occurs before the decision provider and is a separate headless blocker.

The pinned MegaInput type contains only input-name StringName fields and an AllInputs display mapping. A precisely scoped suppression of its presentation-only static initialization is a candidate alongside the harness's existing node-static-constructor isolation, leaving unused hotkeys unset. Verify no accepted path dereferences those hotkeys; record the shim/signature and fail if it does. Do not replace CardRewardAlternative.Generate, its option IDs/delegates, reward hooks, CardFactory, population, OnSelect or the synchronization methods. Never instantiate a fake StringName/native pointer. Runtime feasibility of this seam is NOT RUN.

## Gate 3 — teardown, legal travel and next first action

Call ProceedFromTerminalRewardsScreen only after the native reward set is complete. Select a deterministic Monster child from the **actual current point's Children** (coordinate order is enough). Validate it against the captured graph and RunProgressRules. If no supported Monster child exists, report fixture unsupported; do not alter the map or substitute another room type. Execute native map entry and drain the action scheduler until the next real player boundary, with a bounded action/time budget.

Required next-boundary evidence: a different combat/room instance at the chosen coordinate, expected ActFloor/history advance, combat in progress, player side/turn 1, first draw completed, no unresolved reward/choice/end-turn work, living enemies/player, and at least one legal action from the current state. Capture the complete exact-ready simulator root/continuation from that state; verify a child fork leaves its parent unchanged. Do not stop at RoomType=Monster or at an Evaluate-predicted next battle. Do not reuse the old combat's simulator state store or relic instance keys for the new combat.

| State | Actual code contract to assert at checkpoints |
| --- | --- |
| HP/max HP, gold, permanent deck, relics | Same native Player/run persists; carry actual postcombat/hook values. Gold delta follows the selected GoldReward, take-card follows the actual Deck addition, skip leaves deck unchanged. Do not assert an arbitrary reset to starting HP/gold. |
| Combat deck | Player.PopulateCombatState clones permanent Deck into the new combat and sets DeckVersion. Trace the acquired reward card into the new combat's cards/piles through this link; it need not be in the first hand. |
| Mana, form, Echo, StartDrawBonus | CrystalOrb.AfterCombatEnd → ManaRuntime.End → ManaState.Reset: Mana0, Neutral, Echo0, StartDrawBonus0, CompleteOrbfalse. Next CrackedOrb BeforeCombatStart calls StartBattle(false): Mana3/Neutral/Echo0; observe later first-turn hooks rather than manually applying these values. |
| Runtime listeners | End unsubscribes from the old PlayerCombatState and creature events, clears per-play state/Reverse ledger. Next Start must bind exactly once to the new combat state. Old combat energy changes must not affect the new runtime; no duplicate callback effect. |
| Block, Recording, temporary Recording, Strength/Dexterity | Native Player.AfterCombatEnd removes powers and Block. “Permanent Recording” means retained within this battle, not between battles. Temporary Recording expires next player turn; Echo persists within battle but is reset at battle end. Reverse-owned stats must not reappear in the new Neutral battle. |

The old fixed Victory ends with Echo0/Neutral, so it cannot by itself prove nonzero Echo/form/Recording cleanup. Add a clearly labelled lifecycle fixture using the **real existing power/Mana APIs and real damage/blocked-damage callbacks** to establish nonzero state, then let real native end-combat teardown execute. No setter/reflection mutation that fabricates the expected cleanup. This fixture supplements the unmodified natural route; it is not a claim that unimplemented Echo cards exist. If it cannot be completed within this scenario, mark that reset coverage not run and do not advertise it as passed.

## Determinism, isolation and acceptance

Capture run seed, unlock/progress fixture, character, ascension, native map/encounter choices, script/hash, mod/profile identities, reward policy, ordered options and exact choices. Capture run/player RNG streams and counters plus card/potion odds at initial entry, pre/post Victory, pre/post generation, post-selection, next-room entry and first draw. Logging must not roll RNG or call reward generation a second time. Compare stable model/instance provenance, not addresses/timestamps.

Repeat each policy from a new process with identical inputs. The two policies must match through the pre-choice reward snapshot; after that compare each policy to its own repeat. Deck size can legitimately change the subsequent shuffle. Do not demand identical skip/take next hands or claim in-process RunManager/static-state branch isolation. Continue full parent-root and continuation checks at every combat fork.

| Acceptance item | Required evidence / failure |
| --- | --- |
| Baselines | Existing vanilla M1/RNG/M3-skip and v0.4.1 16-action/122-check baseline retained and regressed when shared bootstrap/adapters change. No replacement of old reports. |
| Bootstrap | All authentic hashes/ownership/dependencies valid; canonical pools complete; Guide pool separation; no Godot crash or absent-association warning. Otherwise unsupported or failed, before battle. |
| Negative identity/audit | Wrong manifest/assembly/version, extra/late Harmony patch, changed registration/profile and late pool membership rejected; frozen roots stale on changes. Existing actual-weave versus synthetic-metadata labels remain accurate. |
| First battle | Real native Victory, all per-action roots/continuations exact, parent unchanged, actual teardown complete. Death is a valid recorded terminal outcome but fails the requested Victory loop. |
| Rewards | One actual native offer; valid populated options; gold selected; skip and take completion/history exact. Out-of-offer choice, duplicate claim or unresolved set rejected. |
| Cross-node state | HP/gold/deck/relic snapshots and deltas consistent with commands/hooks; acquired card traced to permanent deck and new combat; explicit reset/listener assertions. |
| Travel/start | Chosen edge exists; correct node/floor/history/encounter; actual enemy setup and first draw; next state actionable and simulator exact-ready. Illegal edge rejected before native entry. |
| Repeatability | Two independent executions per policy match their semantic traces/RNG; shared prefix matches across policies. No seed reroll or RNG override hidden in setup. |
| Save isolation | MemorySaveStore identity throughout, Steam false, replay writer disabled, installed SaveProgress/WriteReplay guards verified. Record invocation counts by phase; do not hardcode old fight's 1+1 as the new loop's total. New-game/map entry may add calls. No real file IO/copy allowed. |
| Presentation | Enumerate every shim and exact target; no combat/reward semantic suppression. Native EndCombatInternal still executes; hover-tip cleanup remains presentation-only. |

Recommended runner result contract: exit 0 only if every required gate for both policies and repeats passes; exit 1 for a completed assertion/differential failure; exit 2 for explicit unsupported/precondition failure; any crash/timeout is nonzero with the last checkpoint. Preserve the established wrapper convention if it already reserves codes, but never turn unsupported into a successful summary. Per-case JSON must retain passed/failed/unsupported/not-run separately.

## Implementation handoff and stop conditions

1. Add a new, separately versioned bootstrap profile and **pool-only preflight**. Resolve the two native discovery getters first; do not begin with another long Victory replay. Review and lock dependency manifests, narrowly isolate OS/save calls, inventory real library hooks.
2. Prove ordinary native reward population and selection at the already-safe offline boundaries, including the MegaInput presentation seam. Keep fixtures labelled; final acceptance still requires the same-run real Victory.
3. Add a native-map-entry run fixture and bounded action plan, then append the native reward policies and next-battle readiness capture. Extend run-progress evidence rather than teaching its pure ledger fake reward effects.
4. Add nonzero lifecycle reset coverage, repeat policies, run affected vanilla/Eleanor regressions, update MILESTONE_STATUS and PROBE_PROVENANCE only with actual outcomes.

Main unresolved implementation risks: safe native ModManager OS access; completeness of the limited dependency profile; native input-hotkey initialization during reward population; selected reward-card support at next root; differing native-map encounter/script; and run/Mod cleanup across combats. None was exercised in this read-only review. A need for wider host access, a full Godot/BaseLib initialization path, fake loaded identity, arbitrary pool injection or weaker audit is a stop-and-report condition, not an alternative implementation path.

This review added only this design document. No gameplay test, build, risky initializer, role/game/save write, or configuration change was performed. Implementation is handed back to the parent for the approved next execution stage; no further feature expansion is proposed.
