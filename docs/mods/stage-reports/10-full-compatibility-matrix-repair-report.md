# 阶段 10 worker 修复交接报告

## 阶段状态

**PARTIAL：两个 worker 阻塞已修复，阶段整体仍保留玩法未运行和 Eleanor 数据阻塞。**

目标游戏固定为 `0.111.0`。固定 Workshop 快照只读，摘要仍为
`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`。

## 分支、status 和边界

- 分支：`main`，`origin/main` 相对位置为 `ahead 8`。
- 开始时已有的 `README.md` 修改、docs 删除、未跟踪 docs/source/tool 路径，以及 native-run 修改均保留；没有 `reset`、`clean`、覆盖式 `checkout`、提交或推送。
- 本轮实际修改：
  - `tools/mod-worker/ModWorker.csproj`
  - `tools/mod-worker/Program.cs`
  - `src/SimulatorLab.Core/Mods/ModWorkerContracts.cs`（仅 JSON worker 证据契约）
  - `tools/validation/TestModWorker.ps1`
  - 本报告
- 未修改：固定快照目录、快照 manifest、任何 DLL/PCK/资源、Watcher/RitsuLib/YukiMakoto/Shadowverse 输入、核心战斗模型、SnowLuma 和其他无关项目。
- `git diff --check` 通过。`.build/runs/` 下的验证产物被忽略，旧的 `.build/runs/mod-matrix-20261108/` 仍存在且未覆盖。

## 修复内容

1. `ModWorker.csproj` 显式排除 `MinionStage10Program.cs`，保留所有阶段入口文件和独立阶段项目。主 worker 构建不再把 `Program.cs` 与阶段 10 顶层入口同时编译。
2. 静态发现改用 `PEReader` 直接从只读文件流读取程序集名称、版本和 MVID；不依赖会触发大文件限制的整文件读取。
3. 同名程序集按候选分组处理。每个候选都写入 `assemblyCandidates`，包含路径、程序集版本、声明的游戏版本、选择/拒绝状态和理由；只有唯一匹配 `0.111.0` 的候选进入 collectible ALC。`intentgraph2core-lt-0.110.0.dll` 被明确拒绝，`intentgraph2core.dll` 被明确选择。
4. 文件 SHA-256 改为分块流式计算。`shadowversebydmod.pck` 的实际长度为 `3,275,578,050` bytes；不截断、不复制、不重打包、不跳过，也不把加载成功当作玩法兼容。
5. `TestModWorker.ps1` 增加两个针对性断言：`intentgraph2` 的旧版本候选拒绝证据，以及 Shadowverse 超 2 GiB 文件的完整长度证据。

## 红灯和修复结果

| 项目 | 修复前 | 修复后 |
| --- | --- | --- |
| `ModWorker.csproj` | `CS8802`，`Program.cs` 与 `MinionStage10Program.cs` 顶层语句冲突 | Release build 0 warning / 0 error |
| `intentgraph2` | `MOD-W008`，同名 `intentgraph2core` 已加载 | `succeeded`，5 个已加载程序集、6 个候选；`0.110.0` 候选明确 `rejected` |
| `shadowversebydmod` | `MOD-W008`，大文件读取限制 | `succeeded`，13 个文件、4 个程序集；3,275,578,050-byte PCK 完整摘要 |
| 静态矩阵 | 19/21 succeeded，2/21 `MOD-W008` | 21/21 独立进程 `succeeded`，0/21 failed，0/21 blocked |

旧红灯原始消息已保留在旧证据目录和本轮复现目录：
`.build/runs/mod-worker-repro-20261108-intentgraph2`、
`.build/runs/mod-worker-repro-20261108-shadowversebydmod`。

## 执行命令和结果

- `dotnet build tools/mod-worker/ModWorker.csproj -c Release --ignore-failed-sources -p:NuGetAudit=false`：修复后通过，0 warning / 0 error。
- `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-build --no-restore`：`PASS all 137 contract checks`。
- `pwsh -NoProfile -ExecutionPolicy Bypass -File tools/validation/TestModWorker.ps1`：通过；正常发现摘要确定性一致，7 个负控/修复控制通过，`intentgraph2Candidates=6`，Shadowverse 文件长度为 `3275578050`。
- `TestWatcherStage6.ps1`：通过；既有 `Blocked/MOD-010` 语义和确定性保持不变。
- `TestRitsuStage7.ps1`：通过，`mapped=31 partial=31 unsupported=114`。
- `TestKaguyaStage8.ps1`：通过，既有事件/奖励证据和 `unsupported=3` 保持。
- `TestMinionStage9.ps1`：通过。
- `TestMinionStage10.ps1`：通过；真实随从、组件卡、伤害、死亡、移除和清理证据保持通过。

使用固定快照逐个启动了 21 个新的 worker 进程。新矩阵目录为：

`.build/runs/mod-matrix-repair-20261108-20261108-021117-429-01112138a1224ec5aa5dacf7eb533c4a/`

其中 `matrix.json`、`summary.json` 和 21 个条目目录均为新建；每个条目都有独立 `run-manifest.json`、`input-summary.json`、`worker.log`、seed、输入摘要和 worker PID。21 个条目的隔离检查均为 `coreLoadedModAssembly=false`、正式存档/设置/游戏数据访问为 false、无 patch/临时 Mod 文件、ALC teardown 为 true。

## 固定输入复核

快照清单和关键文件均与清单行一致：

- manifest：`09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`
- Watcher DLL：`0888e0eef851ba65300dc8485095e89cd0c2bdb49ae67915f7363ae81c6085c1`
- RitsuLib `compat/0.111.0/STS2-RitsuLib.dll`：`6cc6b9df7b892ed18480ad528168a804af46e24e376f0a77cfa26b57900c1b11`
- YukiMakoto `lib/0.111.0/YukiMakoto.dll`：`442113614bbaf43cc988cf5f917f82be3a41dd57ee0a7d19b1e6b5ccb360caa2`
- Shadowverse DLL：`638d89dcce30139ee4e978a170fb4625aedfaf2cf38e4f3bb5c17d0bd6338749`

## 原版 M1 和 Eleanor

用 `tools/native-run/run-native-slice.ps1`、固定 `minion-stage10-v5/build-manifest.json`、原版 seed `LAB_V0111_STAGE10_M1_20261108` 重新运行 `slice-skip`。新目录为：

`.build/runs/stage10-m1-regression-repair-20261108-originalseed-29b1fd2b6ea94d20a342d66d445f7451/`

结果：退出码 `0`，`22/22` dispatched/confirmed，trace `247`，`SliceBoundary`，证据完整。

Eleanor 冻结证据为 **BLOCKED/未运行**。当前缺少：

- `.build/pinned-inputs/eleanor-v0.4.1-forms-cardpool.fix/input-lock.json`
- `.build/pinned-inputs/eleanor-v041-native-bootstrap/`
- 对应冻结报告和 `.build/runs` 目录

没有把历史报告当作当前通过，也没有伪造复核结果。

## 仍未支持语义和分母

静态 worker 的 `21/21` 只表示受控发现与隔离闭合，不表示玩法兼容。Watcher 的 6 个 canonical-cost token Card、LexNinja2 的 `31 partial / 114 unsupported`、Kaguya 的 3 个 Unsupported，以及 Stage 10 的 UI、Map、Save、ComplexComponent、ReplayNetwork 五类 Unsupported 继续保留。其余未运行角色仍为 `Unrun`；`intentgraph2` 与 Shadowverse 也没有因此获得完整玩法声明。

错误分母为：修复前 `MOD-W008=2/21`，修复后 `MOD-W008=0/21`；静态成功分母由 `19/21` 变为 `21/21`。Eleanor 不计入成功分母，标记为 `BLOCKED/未运行`。

## 下一唯一入口

停止在本阶段。下一唯一入口是人工审阅新的 21 条目矩阵和两个候选/大文件证据，再单独决定是否创建一个后续最小玩法切片；不得从本报告自动进入 YukiMakoto、其他未运行角色、性能或策略工作。

本轮没有提交、没有推送、没有创建远程 PR。
