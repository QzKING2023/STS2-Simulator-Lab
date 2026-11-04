> 2026-11-04 最终状态：M1 在固定原版 Ironclad/A0 声明范围验收通过。最终构建自然 RunVictory464、两次同构建重放和六固定种子各自真实 Death/重放已核验；旧失败、独立 legacy 构建和未覆盖项保留。详见 [M1_FINAL_ACCEPTANCE.md](M1_FINAL_ACCEPTANCE.md)。以下按时间保留历史记录；其中未完成声明不是最新状态。

# Validation setup

Stage 1's historical evidence and acceptance matrix are in [STAGE_1_VALIDATION.md](STAGE_1_VALIDATION.md); the mandatory independent-review repairs and replacement artifact are in [STAGE_1_REVIEW_REPAIRS.md](STAGE_1_REVIEW_REPAIRS.md). `run-stage-one.ps1` consumes a frozen manifest, fresh output directory and the retained sixteen-action plan; `TestStageOneEvidence.ps1` reviews its artifacts. Intermediate summaries have `completed=false`. Batch exit 2 is reserved for declared unsupported paths; incomplete budget/cancel/environment/no-candidate outcomes use exit 3, execution/verification failures exit 1, verified complete outcomes exit 0. Expected fault controls keep their original child exit 1.

Use .NET 9 and PowerShell 7. Protocol-only checks need no game inputs:

~~~powershell
dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj
~~~

## 默认验收范围（2026-11-03）

每轮先明确改动范围、受影响依赖和通过/失败/未完成标准，再按[原生流程证据的信任边界与分层验收](NATIVE_RUN_EVIDENCE_TRUST_BOUNDARY.md)选择检查器重验、受影响生成链重跑或扩大/全量回归。报告必须区分历史原始证据重验与本构建新进程执行；记录版本、输入、采集口径、结果、未覆盖风险及范围选择理由。当前切片通过不关闭最终同构建全量、完整自然 run 和里程碑评估门。

## External inputs

Supply legally obtained local game v0.111.0 / 41cef1ea, Godot 4.5.1, CombatSolver 0.50.1 source with its LICENSE, BaseLib 3.4.7 and RitsuLib 0.6.10 for game 0.111.0. Inputs are not downloaded or redistributed.

Configure these environment variables, or use matching command parameters:

| Variable | Meaning |
| --- | --- |
| STS2_GAME_PATH | Game installation root, read-only input |
| STS2_COMBAT_SOLVER_SOURCE | Local CombatSolver 0.50.1 source root |
| STS2_GODOT_PATH | Existing Godot executable |
| STS2_RITSU_ROOT | Ritsu reference root for the vanilla/earlier Eleanor runner |
| STS2_BASELIB_MANIFEST | Authentic BaseLib manifest, needed to create the native bootstrap snapshot |
| STS2_RITSU_MANIFEST | Authentic Ritsu manifest, needed to create the native bootstrap snapshot |

The runners verify the game's release metadata and DLL SHA-256 **0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9**. Builds explicitly disable deployment and automatic Mod copying.

## Local input locks

Place the independently prepared Eleanor snapshots under ignored .build/pinned-inputs:

- eleanor-v0.3.0-m3.burst23 for the earlier baseline.
- eleanor-v0.4.1-forms-cardpool.fix for the current native loop.
- eleanor-v041-native-bootstrap for authentic BaseLib/Ritsu/MyMod manifest copies.

The Mod input-lock.json uses schema simulatorlab-pinned-mod-inputs/v1 and declares gameMinVersion, modId, modVersion and files. Each file entry supplies relativePath, length and sha256. The runner checks the files before execution. The snapshot includes MyMod.dll/json/deps.json/pdb, BaseLib, matching Ritsu dependencies, their reference props and Godot reference assemblies. The native bootstrap has a separate native-bootstrap-inputs/v1 manifest lock.

No private evidence or raw reports are needed in the public repository. Input availability remains a prerequisite; the tools fail explicitly rather than downloading replacements or silently choosing another version.

## Run commands

~~~powershell
pwsh -NoProfile -File tools/validation/run-v0111-offline-kernel.ps1
pwsh -NoProfile -File tools/validation/run-v0111-eleanor-v040.ps1 -PinnedModDirectory eleanor-v0.4.1-forms-cardpool.fix -ExpectedModVersion v0.4.1-forms-cardpool.fix -NativeLoopStage Pool
pwsh -NoProfile -File tools/validation/run-v0111-eleanor-v040.ps1 -PinnedModDirectory eleanor-v0.4.1-forms-cardpool.fix -ExpectedModVersion v0.4.1-forms-cardpool.fix -NativeLoopStage Loop -NativeRewardPolicy take
~~~

The builder prints the resulting isolated harness directory. Use that exact directory for repeated cases:

~~~powershell
pwsh -NoProfile -File tools/validation/run-v041-native-loop-cases.ps1 -HarnessRoot <printed-harness-directory>
~~~

The cases runner verifies source/binary freshness and locked inputs. It executes card-skip twice, card-take twice and the Record/Echo API lifecycle fixture. Its first actually passed plan and hash are frozen for subsequent processes.

For the older fixed Victory baseline, pass a previously generated matching route explicitly:

~~~powershell
pwsh -NoProfile -File tools/validation/run-v041-victory-replay.ps1 -PinnedPlan <local-route.json>
pwsh -NoProfile -File tools/validation/TestEleanorVictoryReplayEvidence.ps1 -ReportPath <local-v2-report.json>
~~~

## Output and interpretation

Outputs stay under ignored .build/runs. Each case contains native-run-loop.json and battle/eleanor-v041-fixed-route-victory-replay.json, with roots, raw continuations, lifecycle/RNG snapshots and terminal events. repeat-summary.json records per-process exit/status and the 24 milestone checks.

Exit 0 confirms the required cases passed. Nonzero exits and explicit unsupported/not-run states must be retained. No retail executable/UI, deployment, actual save or Steam Cloud interaction is part of the validation workflow.

## Bounded boundary, batch and decision stages

Build the current native-loop harness with `-NativeLoopStage Pool`, then use the printed directory with:

~~~powershell
pwsh -NoProfile -File tools/validation/run-extended-validation.ps1 -HarnessRoot <harness-directory> -Stage ShieldMatrix
pwsh -NoProfile -File tools/validation/run-fixed-strategy-batch.ps1 -HarnessRoot <harness-directory>
pwsh -NoProfile -File tools/validation/run-online-decision-cases.ps1 -HarnessRoot <harness-directory>
~~~

Run these serially. The batch has three preregistered seeds, two policies and two repeats; exit 2 records explicit unsupported paths. `-ReplayPlansFrom <earlier-cohort-directory>` preserves declared seeds/policies and reuses emitted plans to target earlier failures. The online cases compare independent one-action replanning with a separately labelled frozen replay. See [EXTENDED_VALIDATION.md](EXTENDED_VALIDATION.md) for bounds, results and limitations.
