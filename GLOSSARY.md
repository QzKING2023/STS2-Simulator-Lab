# STS2 Simulator Lab

This glossary defines the domain language for native run execution and Mod compatibility decisions.

## Compatibility

**Flow-compatible**:
A legal, auditable run can proceed through the native lifecycle and end in an actual native `RunVictory` or actual native `Death`. This term alone does not claim that every declared content item has been exercised.
_Avoid_: terminal-compatible, victory-compatible

**Complete content compatibility**:
The Lab can load and use every frozen content unit declared or registered by an in-scope Mod—such as cards, custom events, Ancient encounters, relics, enemies, Powers, Minions, and rewards—without changing the Mod's original code, while also meeting the `flow-compatible` boundary.
_Avoid_: DLL-loaded, static-compatible, partial content compatibility

**Content unit**:
A uniquely identifiable gameplay object or registration supplied by a Mod, tracked individually for discovery, lifecycle evidence, and terminal status.
_Avoid_: content type sample, representative item

**Flow gate**:
The per-Mod acceptance gate requiring an actual native `RunVictory` or actual native `Death` under the fixed run contract.

**Content gate**:
The per-Mod acceptance gate requiring lifecycle evidence for every frozen content unit; loading or construction alone is insufficient.

**Content interaction contract**:
The declared or observed cross-content behavior a content unit must preserve, such as targeting, stacking, registration, reward, or persistence semantics.

**Lifecycle checkpoint**:
An evidence point appropriate to a content type, such as discovery, construction, activation, effect resolution, persistence, or cleanup.

**Registration identity**:
A distinct content identity produced by a Mod under a fixed game version, snapshot, configuration, and runtime condition.

**Platform capability**:
A host or product surface such as UI, save, map, network, Steam, or cloud behavior that is tracked outside the gameplay content gate.

**Generic compatibility interface**:
An external adapter boundary that expresses reusable semantics for more than one Mod or content family without embedding a single-Mod special case.
_Avoid_: Mod-specific shim, one-off adapter

**Library compatibility foundation**:
The shared BaseLib, RitsuLib, and MinionLib behavior required by dependent Mods before per-Mod compatibility work is accepted.

**Foundation gate**:
The prerequisite acceptance gate for the shared library compatibility foundation.

**Eleanor frozen baseline**:
The retained Eleanor v0.4.1 compatibility evidence used as an independent regression baseline; it is not part of the current M2 role-bearing denominator.
_Avoid_: Eleanor main denominator

## Mod scope

**Role-bearing**:
A Mod whose declared or observed content changes the playable character or the run/combat lifecycle and therefore belongs in the M2 full-run denominator.

**Dependency**:
A Mod required to load or execute another in-scope Mod, but not counted as an independent role-bearing entry.

**Functional**:
A gameplay-adjacent Mod that provides utility or behavior without being a role-bearing content package.

**Resource-only**:
A package that supplies non-gameplay resources or presentation assets without entering the role-bearing denominator.

**Excluded**:
An entry deliberately outside the M2 denominator for a documented scope reason.

## Run evidence

**Primary**:
The first new-process run for a fixed Mod, seed, snapshot, dependency closure, and configuration.

**Repeat**:
An independent new-process execution of the same fixed inputs used to check boundary and terminal consistency.

**Replay**:
A reproduction based on the primary route that must pass the complete state, RNG, and trace checks.

**Stable**:
A Mod whose primary, repeat, and replay evidence agree on lifecycle boundaries and terminal classification under the same fixed inputs, with no unexplained budget or unsupported stop.

**Unsupported**:
A deliberate stop because the required native or Mod semantics are not modeled or cannot be safely executed; it is not a terminal success.

**Partial**:
A result where one acceptance gate or a declared content unit remains incomplete, while the evidence and completed scope are retained for follow-up.

**Covered**:
A content unit whose required lifecycle checkpoints and applicable interaction contracts have evidence under the frozen inputs.

**Unrun**:
A content unit that has been identified but has not yet received the required lifecycle evidence.

**RuntimeFailure**:
A content unit or run whose required execution could not complete because the native or Mod process failed, distinct from a deliberate `Unsupported` stop.

**Append-only inventory**:
A frozen content inventory that may gain newly discovered registration identities but never silently drops or rewrites an existing unit.

**Risk-based evidence**:
A rule that every content unit receives a fresh-process lifecycle proof, while stateful, random, cross-room, or interaction-sensitive units receive additional repeat, replay, or probe evidence.

**Execution compatibility**:
Evidence that native execution can load, drive, observe, and clean up a Mod content unit under the fixed run boundary, independent of what information a player-facing strategy may see.

**Visible-policy compatibility**:
Evidence that a strategy can operate within the declared player-visible information boundary; execution probes using hidden or Oracle state do not satisfy this term.

**Lifecycle matrix**:
The per-content-type table of required lifecycle checkpoints, evidence references, interaction contracts, and status.

**Interaction risk table**:
The evidence plan linking each content interaction contract to its normal, boundary, or failure scenarios.

**Sample denominator**:
The separately reported set of new Workshop Mods evaluated under M2-7; it does not silently enlarge the frozen fixed-array denominator.

**Blocked**:
A stage cannot safely proceed because a prerequisite foundation or environment condition is unresolved; the blocking evidence remains part of the report.

**Evidence matrix**:
The traceable record connecting each Mod, content unit, lifecycle checkpoint, interaction contract, input identity, evidence artifact, and status.

**Affected evidence**:
Previously accepted evidence whose input, dependency, adapter, or observation semantics changed and therefore must be revalidated before reuse.

**Canonical input**:
The fixed version, snapshot, dependency closure, configuration, seed, and collector identity used as the reference for a compatibility claim.

**Flow stop**:
A deliberate terminal classification such as `Unsupported`, `RuntimeFailure`, budget exhaustion, or environment blocking that is retained as evidence and never promoted to `RunVictory` or `Death`.

## M2-0 frozen boundaries

**Foundation gate status**:
The shared BaseLib, RitsuLib, and MinionLib semantic prerequisite. `PARTIAL` means some reusable semantics have evidence; `BLOCKED` means dependent full-run acceptance cannot proceed until the missing shared semantics are closed. A Mod-specific adapter cannot satisfy a blocked foundation item.

**Failure path**:
The controlled `failure` execution in the four-path contract. It must terminate with an explicit `Unsupported`, `RuntimeFailure`, budget, or environment classification and must prove that no false `RunVictory` or `Death` was emitted.

**State aggregation**:
The deterministic rule that rolls content-unit states into a Mod state: any `RuntimeFailure` or `Blocked` prerequisite blocks the applicable gate; otherwise any `Unrun` or `Unsupported` required unit makes the content gate incomplete; any `Partial` gate keeps the Mod `Partial`; only both gates and stability can produce `PASS`.

**Random Workshop sample**:
A Mod selected for M2-7 from a newly captured snapshot using a recorded selection seed. Samples have their own denominator and cannot change the frozen 21-entry fixed-array denominator.

**Canonical collector**:
The named evidence producer bound into canonical input. Static evidence uses `SimulatorLab.ModWorker` with `mod-worker-discovery/v1` and `mod-worker-run/v1`; native evidence uses the isolated NativeRun/GameProbe collector and its declared probe schema. A collector change affects prior evidence.

**Exact compatibility variant**:
The version-specific directory selected for the target game, such as RitsuLib `compat/0.111.0` or YukiMakoto `lib/0.111.0`. The runner must fail closed when the exact directory is absent; it may not silently fall back to another game version.

**Irreversible M2 boundary**:
The fixed snapshot, manifest, DLL/PCK/resource bytes, target game version, dependency closure, registration identities, and accepted evidence are append-only inputs. They are never rewritten, replaced, re-packed, patched, or silently reclassified to make a gate pass.

