# ADR-0005: M2-0 freezes the role denominator and evidence contract

## Status

Accepted as the M2-0 scope decision. The GitHub ticket mutation (assignee/comment/close) remains an external integration operation and is reported separately when unavailable.

## Context

The fixed Workshop snapshot contains 21 top-level entries and 142 files. Several entries are shared libraries, utilities, or presentation packages rather than playable role content. Existing Stage 6 through Stage 10 evidence also mixes static worker discovery with narrow runtime probes. Treating those observations as one denominator would turn load evidence into gameplay compatibility and would allow a library or resource package to bypass the foundation gate.

## Decisions

1. The M2 role-bearing denominator is exactly these 12 snapshot entries: `Watcher`, `Downfall`, `figure_Saya`, `Remilia`, `Ryoshu`, `LexNinja2`, `Kaguya`, `Acheron`, `shadowversebydmod`, `Aemeath`, `St2bs`, and `YukiMakoto`. The denominator is frozen for the fixed snapshot and target game `0.111.0`.
2. `BaseLib`, `STS2-RitsuLib`, and `MinionLib` are dependencies and shared foundation entries. They are never independent role-bearing successes. Their gameplay semantics must pass the foundation gate before dependent full-run work can be accepted.
3. `QuickRestart`, `MintySpire2`, and `intentgraph2` are functional entries. Their worker evidence can establish loading and isolation or an explicit unsupported boundary, but it cannot satisfy the role-bearing flow/content gates.
4. The Necrobinder bundle, `LieRenTVmod`, and `ATA_IronClad` are resource-only entries for this snapshot. They remain outside the role denominator even when a DLL is present.
5. Eleanor v0.4.1 is an excluded independent regression baseline. It is not one of the 21 snapshot entries and is not part of the M2 role denominator; missing frozen inputs keep its regression status `BLOCKED/Unrun`.
6. Every role-bearing entry receives an append-only content-unit inventory. A registration identity is keyed by Mod ID, exact game version, snapshot digest, dependency closure, configuration, runtime condition, assembly/PCK identity, and the observed native identity. A new identity is appended and invalidates any dependent acceptance until revalidated.
7. The two acceptance gates remain independent: `flow gate` requires a native `RunVictory` or native `Death`; `content gate` requires lifecycle and applicable interaction evidence for every frozen content unit. A flow-only result is `Partial`.
8. Static discovery, runtime execution compatibility, visible-policy compatibility, and platform capabilities are separate dimensions. UI, save, map, network, Steam, cloud, and presentation resources do not pass either gameplay gate implicitly.
9. Full-run evidence uses fresh-process `primary`, `repeat`, `replay`, and `failure` paths. `Unsupported`, `RuntimeFailure`, budget exhaustion, and environment blocking are flow stops, never terminal success.
10. M2-7 random Workshop samples use a separate sample denominator. The first three samples are selected by a recorded selection seed from a newly created snapshot; they never enlarge or mutate the fixed 21-entry denominator.

## Foundation gate

The foundation gate is `PARTIAL/BLOCKED for dependent full-run acceptance` at M2-0. Narrow evidence exists for BaseLib loading/model registration, RitsuLib `compat/0.111.0` loading and LexNinja2 hook boundaries, and MinionLib initialization plus Minion/Shadowverse lifecycle probes. The complete shared semantic export lists below still contain unobserved lifecycle, cross-room, cleanup, and failure cases. No single-Mod adapter may bypass these gaps.

## Consequences

- The next agent can select one role-bearing Mod without reclassifying the snapshot.
- A static worker result of `21/21 succeeded` is recorded as discovery/isolation evidence only.
- Existing narrow Stage 6/7/8/9/10 results remain valid at their stated scope and remain `Partial`, `Unsupported`, or `Unrun` where their matrices say so.
- Any change to game version, snapshot digest, exact compatibility directory, dependency closure, adapter, collector, or runtime condition creates affected evidence and requires revalidation.

