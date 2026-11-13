# M2-6 Acheron matrix handoff — 2026-11-13

## Gate result

The Acheron row is **BLOCKED** overall:

| Gate | Status | Evidence |
| --- | --- | --- |
| Foundation | **PASS** | `m2-2-yuki-foundation-repair-20261110-r260-acheron/foundation-fixture.json`, `foundation-cleanup.json` |
| Flow | **BLOCKED** | No native `RunVictory` or `Death` after the selected surface; the fresh generic attempt is `Ongoing / EngineError`. |
| Content | **PARTIAL** | Acheron identities and real card play are present in the fixed native trace; the selected surface is `CARD.ACHERON-SLASH_ACHERON`. |
| Stability | **BLOCKED** | Acheron-specific Primary/Repeat/Replay/Failure scenarios are not available. |
| Platform | **PARTIAL** | Headless/offline guards are covered; retail UI, formal save/load, map presentation, network replay, Steam, and cloud remain separate unsupported capabilities. |
| Aggregate | **BLOCKED** | Flow and stability are not closed. |

This handoff is an append-only M2-6 row. It does not close GitHub #14 and does not promote a slice boundary or a battle victory to `RunVictory`/`Death`.

## Fixed inputs and adapter

The adapter enforces the frozen input before reading evidence:

- game `v0.111.0`, commit `41cef1ea`;
- snapshot `workshop-snapshot-20261106-163352`;
- snapshot content SHA-256 `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`;
- snapshot manifest SHA-256 `09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`;
- Acheron `lib/0.4.11`, Workshop `3747588205`;
- dependency closure `STS2-RitsuLib compat/0.111.0`;
- registration identity `REG.Acheron.lib/0.111.0#1@0.111.0`.

The read-only Mod-specific adapter is [`AcheronM26Adapter.ps1`](../../../tools/validation/AcheronM26Adapter.ps1). It reads the existing fixed foundation evidence and writes only the unique output directory [`m2-6-acheron-20261113`](../../../.build/runs/m2-6-acheron-20261113/). It never reads the current Steam install, modifies the snapshot or Mod bytes, or rewrites old evidence.

## What the fixed evidence proves

The retained native trace contains real Acheron identities and 20 `CardPlayStartedEntry`/`CardPlayFinishedEntry` pairs. The observed cards include `CARD.ACHERON-SLASH_ACHERON`, `CARD.ACHERON-SHEATHE_ACHERON`, `CARD.ACHERON-VERMILION_SEAL_ACHERON`, `CARD.ACHERON-DRAW_SWORD_ACHERON`, `CARD.ACHERON-SCATTERED_CRIMSON_ACHERON`, and `CARD.ACHERON-NETHER_REQUIEM_ACHERON`. A battle victory, reward offer/completion, and a next-room boundary are present in the same trace.

The trace stops at `Ongoing / SliceBoundary`; the final native lifecycle has `Won=false`, `Losses=0`, and zero run-end starts/returns. These facts support `content=PARTIAL` and `flow=BLOCKED`. They do not establish a native terminal after the selected content.

The fixed foundation fixture reports `runtimeAlcTeardown=true`, `alcAlive=false`, `assemblyAlive=false`, `observerDetached=true`, and `registryCleared=true`, so the foundation row remains `PASS`.

## Matrix artifacts

The output directory contains:

- [`canonical-input.json`](../../../.build/runs/m2-6-acheron-20261113/canonical-input.json) and [`adapter-config.json`](../../../.build/runs/m2-6-acheron-20261113/adapter-config.json);
- [`run-manifest.json`](../../../.build/runs/m2-6-acheron-20261113/run-manifest.json) with the final gate statuses;
- [`content-matrix.json`](../../../.build/runs/m2-6-acheron-20261113/content-matrix.json), [`lifecycle-matrix.json`](../../../.build/runs/m2-6-acheron-20261113/lifecycle-matrix.json), and [`interaction-risk.json`](../../../.build/runs/m2-6-acheron-20261113/interaction-risk.json);
- [`four-path-status.json`](../../../.build/runs/m2-6-acheron-20261113/four-path-status.json), [`platform-capabilities.json`](../../../.build/runs/m2-6-acheron-20261113/platform-capabilities.json), and [`observation-ledger.json`](../../../.build/runs/m2-6-acheron-20261113/observation-ledger.json);
- [`source-hashes.json`](../../../.build/runs/m2-6-acheron-20261113/source-hashes.json), which seals the source evidence files and the fresh attempt manifest.

The existing handoff retained a 586-row append-only denominator (`Covered=0`, `Partial=8`, `Unrun=578`, `Unsupported=0`, `RuntimeFailure=0`). The fixed foundation fixture exports 152 standard category counts (`Card=98`, `Character=1`, `Power=35`, `Relic=14`, `Other=4`). The adapter keeps both values and records the denominator mismatch as an explicit `PARTIAL` audit; it does not delete, fold, or silently reclassify the legacy denominator.

## Fresh attempt and next seam

The fresh generic foundation attempt is [`m2-5-acheron-20261113-foundation-primary`](../../../.build/runs/m2-5-acheron-20261113-foundation-primary/). It loaded Acheron and RitsuLib but stopped with `Expected Play, actual EventDecision` in the current natural-flow coordinator. Repeat, replay, and failure Acheron-specific cases were not run.

The smallest next implementation is a config-driven generic content-slice seam, to be integrated by the main session:

1. Let the coordinator consume `EventDecision` before calling `Ready()`'s Play-only path.
2. Add a generic content-slice worker contract that receives a selected model, emits discovery/construction/activation/effect/cleanup events, and can request a native death fixture.
3. Bind Acheron through the independent adapter config, then run Primary, Repeat, Replay, and Failure in separate output directories.
4. Promote the row only after a native `RunVictory` or `Death` follows the selected surface and all applicable cleanup evidence is present.

No common dispatcher or `Program.cs` change was made in this handoff.
