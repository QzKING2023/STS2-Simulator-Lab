# Implementation provenance and isolation

Stage 1 evidence and the immutable source/artifact manifest are documented in [STAGE_1_VALIDATION.md](STAGE_1_VALIDATION.md). Terminal capture now distinguishes the original Victory teardown prefix from a read-only `ProcessPendingLoss(CombatTurnState)` prefix. Native TestMode skips CreatureCmd's run-ended/history/metrics/game-over UI path; no loss method or role reset is suppressed. Combat Death is compared before that boundary; remaining state/subscriptions are observed separately after the actual CombatEnded event. The full retail loss lifecycle is outside this evidence.

The historical candidate is `31F713F7CF538FB9C0A076E24560EE1BDC0C5FF852EFBA872C91BC722BEE7D0A`; independent review rejected its result-gate/progress handling. The replacement frozen identity and repaired evidence are in [STAGE_1_REVIEW_REPAIRS.md](STAGE_1_REVIEW_REPAIRS.md). Wrong-manifest/artifact/input/source controls operate on metadata copies; exception/watchdog/mismatch and mid-query-fault controls remain explicitly labelled. The separate older-role compatibility build is not presented as the main binary. No raw local source snapshots, game inputs or execution artifacts belong in a public release.

## Source boundaries

The core protocol, comparison adapters and validation drivers are Lab code. Integration targets the MIT-licensed CombatSolver 0.50.1 reference, retained as a local dependency with its original license. Overlay scripts contain exact source anchors and Lab extensions; they fail on incompatible upstream layouts. See [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md).

No game DLL, game implementation source, decompiled source, Mod binary, save file, research bundle or raw run artifact is included. Native API names and observed behavioral contracts do not grant rights to the external implementations.

## Combat and progression evidence

VanillaGameActionProbe, VanillaBattleReplayProbe and VanillaRandomTargetProbe compare actual game actions with independent simulator branches. EleanorV040Probe binds version-specific card/power/relic adapters to exact real methods and source identities. Registration rejects mismatched types, manifests, assemblies and patch compositions. Branch state remains separate from actual mutable runtime state.

NativeModProgression executes a single live run through the first actual Victory, teardown, RewardsCmd.OfferForRoomEnd, selection through RewardsSetSynchronizer, native CardPileCmd.Add, legal map travel and first draw of the next battle. It retains the same Player/RunState, not a reconstructed postcombat fixture.

RunProgressRules validates protocol legality only. It is not an independent implementation of reward amounts, reward RNG or run effects. Native progression, protocol checks and combat differentials are reported separately.

## Authentic minimal Mod discovery

NativeModRunLoop uses ModManager's supported test reset/initializer path, authentic locked manifests and actual assembly associations before AssemblyInfo/model IDs. Actual custom model constructors and PoolAttribute register into native ModHelper.

Only BaseLib's original AddCustomCharacters and ModelDbSharedCardPoolsPatch discovery postfixes are installed. Canonical Pool queries now pass in this profile, including the separate StarterCardPool for GuidingPower. No fabricated Mod record or manually populated reward pool is used. Full framework initialization is not claimed.

A frozen discovery signature rejects a real late Harmony postfix and is restored after its removal. The existing unknown-patch audit remains enabled. Earlier relic/temporary-Recording controls use synthetic PatchInfo metadata and are explicitly labelled metadata tests rather than actual runtime weaving.

## Headless environment seams

- TestMode, Steam disabled and verified MemorySaveStore provide the offline host. Progress-save and replay calls are intercepted; ModManager file IO is read-only and rejects path escapes, writes and save copies.
- A single ModManager command-line query is bridged to actual process arguments to avoid Godot Dictionary static initialization. Platform/executable queries refer to the isolated Lab input view.
- MegaInput's unused hotkey StringName initialization is suppressed. The native TestMode selection screen returns null and the standard selector provides decisions. Reward population, alternatives/delegates, synchronization and effects still execute.
- Existing clock, resource, scene and hover-tip cleanup isolation applies to presentation. Damage, enemy actions, win checks and EndCombatInternal execute normally.

The final loop recorded three suppressed progress-save calls and one suppressed replay write across startup, Victory and next-node entry. Persistent save/replay writing was disabled.

## Repeatability and limitations

The cases runner freezes the first actually passed native battle plan and checks identical actions and complete raw continuations across independent processes. Adapter delegates bind directly to their existing static handlers, keeping implementation audit identities stable across processes without stripping audit fields from comparisons.

The five-process milestone has 24 checks: per-action differential/parent isolation, next-root equality and save/discovery isolation for each process; per-policy lifecycle/reward, terminal/root and full fixed-plan repeat checks; shared reward options, shared prefix plan and nonzero Record/Echo teardown checks.

The separate power API fixture does not represent an implemented Echo card. The published minimum-loop snapshot stops at next-combat first action; the subsequent local milestone adapts and executes ManaShield. Neither establishes retail rendering, full framework compatibility, complete character coverage or balance.

The paragraph above describes the published minimum-loop snapshot. The local-only extension adds a strict ManaShield mirror and actual two-battle replays; see [TWO_BATTLE_VALIDATION.md](TWO_BATTLE_VALIDATION.md). It uses real acquired-card DeckVersion checks, native hand resolution, explicit nonterminal prefix results and separate per-policy frozen second-battle plans. It preserves the original audit and partial/full terminal distinction.

The further bounded stages in [EXTENDED_VALIDATION.md](EXTENDED_VALIDATION.md) label their native API setup separately from natural reward acquisition. The six ManaShield boundary fixtures use genuine upgrade/resource commands, then independent simulator branches. Batch seeds are immutable bindings from the actual native scenario argument; they are declared before execution and never replaced after failure.

Opening card identity remains tied to captured native references. Independently generated cards receive stable per-world ordered first-observation identities. Counts, order, model/state and complete continuations must still match. A generated play resolves by an already-observed unique hand identity plus owner/model, after complete root equality, rather than by type/name alone. The retained failing plan verifies real Slimed generation and play with this resolver.

Native reward synchronization is queried before issuing a skip: taking all rewards can already close the reward set. The driver does not alter reward completion or effects. Two naturally offered Mod cards remain explicit unsupported coverage boundaries.

`NativeCombatDecision` wraps the already-used CombatSolver query. A ticket holds the actual source combat and complete live/continuation stamps, verifies search left native state unchanged, and is consumed exactly once. Each online iteration executes one actual action through the existing differential replay before querying the resulting boundary. The separate frozen mode executes recorded one-action chunks without new solver queries. The three-process bounded fixture passed 15 checks; no global optimality, arbitrary-seed support or full-run strategy claim follows.
