# M2-3 Ryoshu Beta Full-Run Handoff — 2026-11-11

## Decision

**PASS for the M2-3 gameplay acceptance.** The original target artifact `3747528329/Ryoshu.dll` is the latest-only build and cannot load on the fixed `0.111.0` ABI. The Beta Workshop artifact from `3763951274` is archived as a separate, read-only input and matches the fixed game API. A generic native scene-backed combat-room fixture supplies the real `NRun`/`NCombatRoom` and VFX/UI seams without changing the game or Mod. Primary/repeat/replay/failure, reward flow, map transition, cleanup, native Death, and all 156 standard content units now pass.

Visible-policy compatibility remains unproven: these execution probes do not validate a strategy's player-visible information boundary (ADR-0004). Windowed rendering is a separate, unverified platform capability. No Mod/game DLL/PCK/manifest/resource was modified, repacked, or patched. [Issue #8](https://github.com/QzKING2023/STS2-Simulator-Lab/issues/8) was closed after the [final evidence comment](https://github.com/QzKING2023/STS2-Simulator-Lab/issues/8#issuecomment-6773027887).

## Repository state

- Branch: `main`
- HEAD at evidence collection: `4cae8b4cf90d1be358619bdbf5a9e79d53c896b4`
- Status at evidence collection: pre-existing user changes remained protected; this work added/updated only the Beta archive, probe/evidence outputs, and this handoff. No commit or push had been performed at that point.
- The subsequent phase commit contains the Beta handoff documents only. Probe sources, runtime evidence, and snapshots remain local under ignored `.build/` and `local-mods/`; they are not bundled with this commit.
- Fixed snapshot `local-mods/workshop-snapshot-20261106-163352` and its manifest were not changed.

## Actual modifications

- Archived the user-provided Beta Workshop source under `local-mods/workshop-snapshot-20261110-230411-ryoshu-beta/` with manifest and provenance; the source directory itself was not altered.
- Added a generic scene-backed native combat-room fixture to `.build/diag/m2-3-ryoshu-beta-probe-20261110/`. It preloads assets through the native `PreloadManager`, attaches `NRun`/`NCombatRoom`, activates native combat UI, and exposes `CombatVfxContainer` before Mod callbacks.
- Added fresh process evidence under `.build/runs/m2-3-ryoshu-beta-20261110/`, including the repaired `visual-r7` matrix and r3 full runs.
- Updated machine-readable content, lifecycle, interaction-risk, capability, contract, and evidence indexes. The previous 14 RuntimeFailure outputs remain preserved and are referenced as prior evidence.

## Fixed inputs

| Input | Value |
|---|---|
| Game | `0.111.0`, commit `41cef1ea` |
| Fixed game DLL SHA | `0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9` |
| Original target Mod | Workshop `3747528329`, Ryoshu `0.3.6` |
| Beta compatibility source | Workshop `3763951274`, Ryoshu `0.4.1.beta` |
| Beta DLL SHA | `6f72721a32299faa65a4d925785c2e0c75f229892a68f335c342a0bae4800ecc` |
| Beta PCK SHA | `975ebea065d1df934531cc52106e8fac4d434f04f20234badc54b05b93400cce` |
| Beta snapshot SHA | `1c2c95fb5ade6b7acb78a4578743bfa3dff86abf2f11a4e3ba09e887ab5dfe69` |
| Beta manifest SHA | `98e069651b475025f830ee13420c7a11482fc4c29b7388acde11ce89c18646e7` |
| Full-run seed | `LAB_M2_3_RYOSHU_BETA_01` |
| Failure seed | `LAB_M2_3_RYOSHU_BETA_FAILURE_01` |
| Content repair seed | `LAB_M2_3_RYOSHU_BETA_CONTENT_VISUAL_04` |
| Registration | `REG.RYOSHU.assembly#1@0.111.0` |
| Worker | `SimulatorLab.NativeLifecycleProbe/Ryoshu-M2-3` |
| Adapter | `ryoshu-m2-3-probe/v1-no-runtime-patch` |
| Collector | real Godot .NET process + `SimulatorLab.NativeLifecycleProbe`; `native-trace/v1`; `sentry-disabled-overlay/v1` |

The archive provenance is `.build/runs/m2-3-ryoshu-beta-archive-20261110/workshop-download-provenance.json`. The ABI audit is `.build/runs/m2-3-ryoshu-beta-api-20261110/seam-audit.json`; it confirms the Beta reference and fixed game both use only `AttackCommand.FromCard(CardModel, CardPlay)`. The one-argument overload is absent from all fixed `sts2.dll` copies.

## Commands and exits

- Beta probe build: `dotnet build .build/diag/m2-3-ryoshu-beta-probe-20261110/M23RyoshuProbe.csproj -c Release --no-restore --ignore-failed-sources -p:NuGetAudit=false` → `0` (four nullable warnings, zero errors).
- ABI audit: `dotnet run --project .build/runs/m2-3-ryoshu-beta-api-20261110/SeamAudit.csproj --no-restore --ignore-failed-sources -p:NuGetAudit=false` → `0`.
- Contract regression: `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore` → `0`, all 137 checks passed.
- Native root preparation `prepare-root.ps1 -Suffix '-r22'` → `0`.
- Primary/repeat/replay/failure `run-game.ps1` wrappers and native processes → wrapper `0`, raw exit `0`.
- Repaired 14-unit content matrix `run-content-matrix.ps1 -RootSuffix '-r21' -OutputSuffix 'visual-r7' -VisualCombat` → `0`, 14/14 completed.
- `git diff --check` → `0` (only existing LF/CRLF normalization warnings).

## Primary / repeat / replay / failure

| Run | Terminal / stop | Input | State | RNG | Trace | Route | Replay |
|---|---|---|---|---|---|---|---|
| primary-r3 | `Death / Completed` | `4180b4dfdcbfc75670cc7ef384723e3a3ea7b24248c009e2cf2a485cb67177e5` | `1cf306f3e412ea64d63f6dfa45ebde79da09810e7ce61d00e0fe252c9abff936` | `6f5135b40876e58a5f2a1f41df898e0a919c67b7c4f6890fb125b950fab7b698` | `effbfc18d5c32e16fc43e6e3a59fdb11a1d012097b54de8128935ed71762491b` | `5fdbd535d3d7d8b21bec370429e8ffd9a26801e1c0edb3fd27597efce3921ce5` | — |
| repeat-r3 | `Death / Completed` | same as primary | same as primary | same as primary | same as primary | same as primary | — |
| replay-r3 | `Death / Completed` | same as primary | same as primary | same as primary | same as primary | same as primary | `matched=true` |
| failure-r3 | `Unknown / BudgetExceeded` | `f8c44e582cf638c83673d34896ca6339a8321c9f6684b7e61b1dd9559002051f` | `908d433f247677c57837390896d62314ca63b1a9ddfc5bd09b64e0b0a7de2cb5` | — | `2064b3d44c6555375f50121ab18f18740de4ebcae680d9f23c5d645ff643e8ff` | — | — |

The primary trace contains native role selection, initial state, room opening, map transition, combat entry/actions, battle victory, reward offer/selection/completion, second combat, native Death, and cleanup. The failure run stops at the declared budget boundary and remains `Unknown / BudgetExceeded`; it is not reclassified as a pass.

## Gates

| Gate | Result | Evidence |
|---|---|---|
| Foundation | `PASS` | Shared foundation repair report `docs/mods/stage-reports/m2-2-yuki-foundation-repair-20261110-final.md`; issue #4 is CLOSED; 137 contract checks pass. |
| Flow | `PASS` | r3 native traces and manifests show role, first victory, reward, map transition, second combat, cleanup, and native Death. |
| Content | `PASS` | `content-matrix.json`: 156 Covered, zero RuntimeFailure/Unsupported/Unrun. |
| Stability | `PASS` | primary/repeat/replay state, RNG, trace, and route digests are identical; replay check matched. |
| Input audit | `PASS` | Beta archive manifest/provenance, ABI audit, canonical input manifests, and per-run input digests. |
| Platform capability | Native headless scene/VFX seam observed; windowed rendering unverified | Every `visual-r7` trace reports `native-run-scene=true` and `native-combat-room=true`. Headless renderer diagnostics remain in the raw logs. This does not establish visible-policy compatibility or general platform acceptance. |

## Content lifecycle matrix

The standard denominator is 156: 1 character + 107 cards + 35 powers + 13 relics.

| Status | Count |
|---|---:|
| Covered | 156 |
| RuntimeFailure | 0 |
| Unsupported | 0 |
| Unrun | 0 |

Four non-standard `Other` registry rows remain explicitly `Unrun` and are excluded from the 156 standard denominator. They were not reclassified. The 14 previously failing cards are covered by fresh `visual-r7` runs with `ContentLifecycleCompleted / Completed` and native scene/VFX evidence.

## Interaction risk table

| Risk | Prior observation | Resolution | Evidence |
|---|---|---|---|
| `RYS-POISE-NULL` | 14 cards previously reached `RyoshuPoisePower.AfterPowerAmountChanged` with `NullReferenceException` in a test-only room lacking `NCombatRoom`. | Resolved by the generic native fixture: native assets are preloaded, a real `NRun` and `NCombatRoom` are attached, native `NCombatUi` is activated, and `CombatVfxContainer` is present before the original callback runs. | `interaction-risk.json` v2, `content-visual-repair-summary-r7.json`, all 14 `visual-r7` traces. Prior `content-card-*-r3` RuntimeFailure evidence remains intact. |
| Old 0.3.6 ABI mismatch | `AttackCommand.FromCard(CardModel)` was missing from fixed `0.111.0`. | Resolved by selecting the archived Beta compatibility artifact `3763951274`; no call-site or runtime patch. | `seam-audit.json`, Beta archive manifest/provenance. |

## Platform capability gaps

- Native headless scene, combat room, VFX container, UI hand, and screen-shake target are proven and used for content callbacks.
- Visible-policy compatibility is not proven because the execution probes do not validate the player-visible information boundary. This is independent of whether a window can be displayed.
- The exploratory windowed launch crashed before gameplay and is not accepted rendering evidence. Its cause has not been established by a display-capability test.
- Godot emits recurring certificate-store, invalid-task, dummy-renderer resource-leak, and occasional missing-texture diagnostics in headless mode. The collector found no gameplay `NullReferenceException`, `ObjectDisposedException`, `GameAction ... exception`, or `RuntimeFailure` in the final 14-unit matrix or r3 full runs.

## Evidence index and next unique entry

Machine-readable evidence is indexed by `.build/runs/m2-3-ryoshu-beta-20261110/evidence-index.json`. The principal files are `runs.json`, `content-matrix.json`, `lifecycle-matrix.json`, `interaction-risk.json`, `capability-matrix.json`, `contract-summary.json`, and `content-visual-repair-summary-r7.json`.

The next unique entry for M2-3 gameplay is **none** according to the recorded gate results: all 156 standard content units are marked complete. Visible-policy verification and windowed rendering remain separately documented gaps; this report does not claim either has passed.
