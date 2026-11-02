# Implementation provenance and isolation

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

The separate power API fixture does not represent an implemented Echo card. ManaShield is acquired natively but its effect is not yet adapted. The milestone stops at next-combat first action, and does not establish retail rendering, full framework compatibility, complete character coverage or balance.

The paragraph above describes the published minimum-loop snapshot. The local-only extension adds a strict ManaShield mirror and actual two-battle replays; see [TWO_BATTLE_VALIDATION.md](TWO_BATTLE_VALIDATION.md). It uses real acquired-card DeckVersion checks, native hand resolution, explicit nonterminal prefix results and separate per-policy frozen second-battle plans. It preserves the original audit and partial/full terminal distinction.
