# 新会话启动提示词：修复阶段 10 worker 阻塞

你在 `D:\STS2-Simulator-Lab` 以独立 Agent 工作。

开始前读取并遵守：

- `AGENTS.md`
- `docs/agents/domain.md`
- `docs/mods/MOD_INTEGRATION_EXECUTION_PLAN.md`
- `docs/mods/stage-reports/10-full-compatibility-matrix.md`
- `docs/mods/stage-reports/10-full-compatibility-matrix-repair-handoff.md`

开始工作前必须：

1. 读取当前分支和 `git status`。
2. 识别并保护所有已有未提交修改；禁止 `reset`、`clean`、覆盖式 `checkout`。
3. 确认固定快照和 manifest 存在，摘要为 `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`。
4. 将目标游戏固定为 `0.111.0`；禁止回退到其他兼容变体。

本会话只处理阶段 10 的两个 worker 阻塞：

1. 修复 `tools/mod-worker/ModWorker.csproj` 的 `CS8802` 顶层语句冲突。当前 `MinionStage10Program.cs` 未被排除，导致 `Program.cs` 与阶段入口同时编译。保持每个阶段项目独立，不删除入口，不合并成空实现。
2. 修复静态 worker 对 `intentgraph2` 和 `shadowversebydmod` 的受控元数据发现路径：
   - `intentgraph2`：处理同名程序集候选，显式记录候选、选择/拒绝理由和版本，不得静默使用 `0.110.0`。
   - `shadowversebydmod`：处理超过 2 GiB DLL 的元数据读取，不得截断、复制、重打包、跳过或把加载成功冒充玩法兼容。

必须遵守：

- Workshop 快照只读；不修改 DLL、PCK、资源或 manifest。
- 不访问实时 Workshop 目录、玩家存档、正式设置或正式游戏数据。
- 不把 Mod 类型、Godot 对象或外部 DTO 放入核心模拟器。
- 不修改 SnowLuma 或无关项目。
- 不删除文件，不重写历史，不提交，不推送，不创建远程 PR。
- 不能用空实现、静默跳过或“加载成功”替代兼容结论。
- 任何路径、哈希、数据格式、运行时语义或预算阻塞，都在安全点停止并报告精确原因。

执行顺序：

1. 先重跑当前红灯和工程构建，确认 `CS8802`、`intentgraph2 MOD-W008`、`shadowversebydmod MOD-W008` 可复现。
2. 只修改最小必要的 worker 项目/发现代码和对应验证；不要扩大到角色玩法。
3. 运行：

```powershell
dotnet build tools/mod-worker/ModWorker.csproj -c Release --ignore-failed-sources -p:NuGetAudit=false
dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-build --no-restore
pwsh -NoProfile -ExecutionPolicy Bypass -File tools/validation/TestModWorker.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File tools/validation/TestWatcherStage6.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File tools/validation/TestRitsuStage7.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File tools/validation/TestKaguyaStage8.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File tools/validation/TestMinionStage9.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File tools/validation/TestMinionStage10.ps1
```

4. 用固定快照逐个启动 21 个新的 worker 进程，生成新的 `.build/runs` 矩阵；不得覆盖旧的 `mod-matrix-20261108` 证据。
5. 重新检查快照 manifest、Watcher DLL、RitsuLib `compat/0.111.0`、YukiMakoto `lib/0.111.0` 和 Shadowverse DLL 的 SHA-256。
6. 重新运行原版 `slice-skip` M1 回归。Eleanor 冻结证据若仍缺失，标记 `BLOCKED/未运行`，不要伪造通过。

验收重点：

- `ModWorker.csproj` 构建必须 0 错误。
- 21 个条目均有独立 run manifest、seed、输入摘要和 worker 身份。
- 两个原 `MOD-W008` 条目必须得到可审计的成功、`Unsupported` 或 `RuntimeFailure`；无静默跳过。
- 所有 worker 保持核心不加载 Mod 程序集、无正式数据访问、无 patch/临时文件残留、ALC teardown 完成。
- 核心 137 项和阶段 7/8/9/10 检查保持通过。
- 不进入 YukiMakoto、其他未运行角色、性能或策略工作。

完成后写交接报告并停止。报告必须包含：阶段状态、当前分支和 status、实际修改与关键未修改文件、执行命令及结果、逐项验收、两个阻塞的新旧结果、错误码/成功失败分母、仍未支持语义、Eleanor 数据阻塞、下一唯一入口、是否提交或推送。

