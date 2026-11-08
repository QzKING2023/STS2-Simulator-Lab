# STS2 Simulator Lab

An offline laboratory for Slay the Spire 2 native execution, reproducible decisions and traceable combat/run data. It uses legally supplied local game assemblies without launching the game UI.

See the [documentation map](docs/README.md) for the report directories and current entry points.

## Current status

**M1 accepted on 2026-11-04 within its declared scope:** Ironclad / A0 / game v0.111.0 (`41cef1ea`), isolated single-player runs. A fresh natural run reached RunVictory in 464 actions across 22 battles, with 358 exact combat differential steps and two same-build replays. Six predeclared evaluation seeds each reached actual Death and reproduced in a new process. These results establish execution and data coverage, not a strong policy or a reliable win-rate estimate.

CombatSolver supplies optional Oracle combat decisions; native-only slices remain available. RandomForeseer is reused for bounded, separately checked predictions. Unknown native behavior or inaccurate prediction stops explicitly. The project does not claim complete content coverage, all-character support, a public Mod SDK or retail UI/save equivalence.

- [Final M1 acceptance](docs/milestones/M1_FINAL_ACCEPTANCE.md): scope, fixed inputs, results, failures, budget, reproduction and limits.
- [Roadmap](docs/milestones/LONG_TERM_ROADMAP.md) and [M1 plan](docs/milestones/MILESTONE_1_NATIVE_RUN_PLAN.md): goals and acceptance gates.
- [Data contract](docs/architecture/NATIVE_RUN_DATA_CONTRACT.md) and [evidence trust boundary](docs/architecture/NATIVE_RUN_EVIDENCE_TRUST_BOUNDARY.md): native facts, optional predictions, identity, integrity and layered validation.
- [Development approach](docs/architecture/DEVELOPMENT_APPROACH.md): investigate reuse, licenses, pinned dependencies and adaptation costs before custom implementation.
- [PhotonCut and terminal-hook repairs](docs/performance/PERFORMANCE_PHOTON_DOOM_VALIDATION.md): original-seed Regent and Necrobinder terminal samples, generic fixes, failures and bounded replay. [Whole-run visible-policy work package](docs/performance/PERFORMANCE_VISIBLE_RUN_NEXT.md) describes the remaining isolation gate; it is not yet certified.
- [Search settings, cross-character runs and visible-input pilot](docs/performance/PERFORMANCE_SEARCH_AND_VISIBLE_PILOT.md), [prior repairs and costs](docs/performance/PERFORMANCE_PILOT_REPAIRS_AND_COSTS.md), [initial pilot](docs/performance/PERFORMANCE_STRATEGY_PILOT_VALIDATION.md), [plan](docs/performance/PERFORMANCE_STRATEGY_PILOT_PLAN.md) and [information boundary](docs/architecture/STRATEGY_INFORMATION_BOUNDARY.md): 80 same-root searches and a completed Silent death/replay; two unsupported character paths and one Defect differential failure retained. Visible DTO controls passed; end-to-end player-visible isolation and all-character full runs remain unvalidated.
- [Setup and validation](docs/validation/VALIDATION.md), [milestone history](docs/milestones/MILESTONE_STATUS.md), [provenance](docs/validation/PROBE_PROVENANCE.md).
- [Implementation history](docs/history/M1_PROGRESS_HISTORY.md): the previous entry page and its per-stage evidence links; historical pending statements are not the latest status.
- [M2 full-run Mod plan](docs/mods/MOD_FULL_RUN_EXECUTION_PLAN.md) and [phase prompts](docs/prompts/MOD_FULL_RUN_PHASE_PROMPTS.md): fixed role-Mod terminal compatibility first, then a pinned Workshop admission sample.

## Run locally

Core protocol checks do not require game or Mod assemblies:

~~~powershell
dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj
~~~

Game-bound runs require locally supplied pinned dependencies and the prepared upstream harness. Use `tools/native-run/build-native-run.ps1` and `run-native-slice.ps1`; the [final report](docs/milestones/M1_FINAL_ACCEPTANCE.md#6-复现入口与证据位置) describes the exact responsibilities and current replay mode. Build/run outputs must use fresh directories. Local locks, binaries and original evidence stay in ignored `.build/` directories; hashes in reports do not imply those assets are bundled.

## Retained Mod foundation

Eleanor v0.4.1 has bounded form/resource/card and two-battle compatibility evidence, retained on its separate frozen build. It is not the new public Mod interface or complete character support. See [two battles](docs/validation/TWO_BATTLE_VALIDATION.md) and [reward-card repairs](docs/validation/REWARD_CARD_REVIEW_REPAIRS.md). New Mod expansion remains deferred.

## Licensing and publication

Original Lab code is [MIT licensed](LICENSE). [Third-party notices](THIRD_PARTY_NOTICES.md) retain CombatSolver / RandomForeseer attribution and distinguish external game and Mod dependencies. No game binaries, assets, decompiled game sources, Agent material, private authorization records or original run products belong in the repository.

M1 is published. A separately authorized performance/strategy pilot is now underway; its measurements and failures remain separate from sealed M1 evidence.

- [职业兼容修复与玩家可见首战试点](docs/performance/PERFORMANCE_COMPATIBILITY_AND_VISIBLE_EXECUTION.md)：Duplicate/Retain、球终态、同构建终局重放、实际可见策略闭环与剩余缺口。
