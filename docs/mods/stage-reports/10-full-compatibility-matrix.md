# 阶段 10：全量兼容矩阵、回归和停点报告

> **历史基线说明（2026-11-08）**：本文保留 worker 修复前的 `19/21` 静态成功结果和对应停点。修复后的当前结果为 `21/21`，以[阶段 10 worker 修复交接报告](10-full-compatibility-matrix-repair-report.md)为准；本文中的旧分母不应作为当前矩阵状态引用。

## 阶段状态

**PARTIAL**。本阶段完成固定快照 21 个 Workshop 条目的批量静态解析、依赖闭包、只读 worker 隔离矩阵、阶段 6 至 10 既有证据汇总和原版回归。19 个条目的静态 worker 发现成功，2 个条目以稳定 `MOD-W008` 停止；角色和复杂内容没有把静态加载当作玩法兼容。Eleanor 冻结证据因历史运行目录和固定输入不在当前工作区而未能重新运行。

本报告只覆盖阶段 10。没有进入性能、策略或大规模内容接入。

## 分支、工作区和文件边界

- 分支：`main`，相对 `origin/main` 为 ahead 8。
- 开始和结束时均保留已有 `README.md` 修改、大量 docs 删除、未跟踪 docs/source/tool 路径以及 `tools/mod-worker/ModWorker.csproj` 等既有改动；没有 `reset`、`clean` 或 `checkout` 覆盖它们。
- 本阶段实际持久化修改：仅新增本报告 `docs/mods/stage-reports/10-full-compatibility-matrix.md`。
- `.build/runs/mod-matrix-20261108/`、`.build/runs/stage10-m1-regression-20261108/` 和阶段脚本生成的运行目录是忽略的验证产物，不是源代码修改。
- 未修改的关键输入：固定快照目录、固定快照清单、全部 DLL/PCK/资源、`src/SimulatorLab.Core` 核心模型、原版 M1/Eleanor fixture；未修改 SnowLuma 或无关项目。

## 固定输入和身份链

| 项目 | 值 |
| --- | --- |
| 目标游戏 | `0.111.0` |
| 快照目录 | `local-mods/workshop-snapshot-20261106-163352` |
| 快照清单 | `local-mods/workshop-snapshot-20261106-163352.manifest.json` |
| 条目数 | 21 |
| 文件数 | 142 |
| 内容大小 | 6,974,558,103 bytes |
| 快照摘要 | `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73` |
| 静态清单结果 | 21 个来源、24 个 JSON manifest；核心检查确认解析稳定 |
| 适配器版本 | `mod-adapter/v1`（阶段适配器）；静态 worker schema `mod-worker-run/v1` |
| worker 身份 | `SimulatorLab.ModWorker` 独立进程；每个条目一个新进程，PID、seed 和 run manifest 见矩阵 |

快照目录和清单只读。没有读取实时 Workshop 目录，也没有写入玩家存档、正式设置或正式游戏数据。

## 全量条目矩阵

状态按内容表面解释：`Supported` 只表示已声明范围内的加载/隔离或已完成的窄语义；`Partial` 表示有真实运行时证据但仍有明确未支持语义；`Unsupported` 表示本次已运行边界被明确拒绝；`Unrun` 表示尚无该条目的玩法运行时证据。

| Workshop ID | Mod | manifest/依赖 | 精确 `0.111.0` 变体 | 只读 worker | 已发现内容表面 | 当前状态 | 未运行/下一最小工作单元 | seed / run manifest |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 3737322022 | QuickRestart | `v2.0.0`; `BaseLib>=3.4.5`；闭包 `BaseLib>QuickRestart` | 未声明 | 成功；6 文件/2 程序集/2 PCK；无写入、无 patch、ALC 已卸载 | QoL DLL/PCK | Supported（加载与隔离范围） | 无玩法语义；若继续，仅做 QoL 资源表面探针 | `LAB_V0111_MOD_MATRIX_3737322022`; `.build/runs/mod-matrix-20261108/3737322022-QuickRestart/run-manifest.json` |
| 3737335127 | BaseLib | `v3.4.7`；闭包 `BaseLib` | 未声明 | 成功；3 文件/1 程序集/1 PCK | 基础库 | Supported（依赖加载范围） | 仅作为依赖使用；不单独宣称玩法兼容 | `LAB_V0111_MOD_MATRIX_3737335127`; `.build/runs/mod-matrix-20261108/3737335127-BaseLib/run-manifest.json` |
| 3737336234 | MintySpire2 | `v1.2.0`; `BaseLib>=3.4.5`；闭包 `BaseLib>MintySpire2` | 未声明 | 成功；7 文件/2 程序集/2 PCK | QoL/表现层 | Supported（加载与隔离范围） | 无玩法语义；若继续，仅做声明的表现层资源探针 | `LAB_V0111_MOD_MATRIX_3737336234`; `.build/runs/mod-matrix-20261108/3737336234-MintySpire2/run-manifest.json` |
| 3747492505 | Watcher | `1.5.14`; `BaseLib>=3.4.7`；闭包 `BaseLib>Watcher` | 未声明 | 成功；6 文件/2 程序集/2 PCK；静态摘要 `7ea316da…` | 真实 Godot probe：角色、129 identities、10 战斗动作、奖励、离开、清理、replay | Partial；Stage 6 runner `Blocked/MOD-010` | 6 个负 canonical-cost token Card 仍未映射；另需可审计 Godot .NET NativeRun 宿主 | `LAB_V0111_MOD_MATRIX_3747492505`; `.build/runs/mod-matrix-20261108/3747492505-Watcher/run-manifest.json`；probe `.build/runs/watcher-stage6-native-primary2-20261107/watcher-runtime-probe.json` |
| 3747508091 | Downfall | `0.1.24`; `BaseLib>=3.4.7`；闭包 `BaseLib>Downfall` | 未声明 | 成功；22 文件/10 程序集/2 PCK | 多角色包静态程序集 | Unrun | 选择一个角色做精确 `0.111.0` 原生首战探针 | `LAB_V0111_MOD_MATRIX_3747508091`; `.build/runs/mod-matrix-20261108/3747508091-Downfall/run-manifest.json` |
| 3747508952 | figure_Saya | `2.1.0`；闭包 `figure_Saya` | 未声明 | 成功；4 文件/1 程序集/1 PCK | 角色 DLL/PCK 静态发现 | Unrun | 角色选择和起始状态的最小原生 probe | `LAB_V0111_MOD_MATRIX_3747508952`; `.build/runs/mod-matrix-20261108/3747508952-figure_Saya/run-manifest.json` |
| 3747515571 | Remilia | `v1.0.3`; `STS2-RitsuLib>=0.5.2`；闭包 `Remilia>STS2-RitsuLib` | 未声明 | 成功；10 文件/3 程序集/1 PCK | RitsuLib 角色静态程序集 | Unrun | 复用 RitsuLib 精确变体，先做角色/卡牌/遗物发现 | `LAB_V0111_MOD_MATRIX_3747515571`; `.build/runs/mod-matrix-20261108/3747515571-Remilia/run-manifest.json` |
| 3747528152 | intentgraph2 | `1.6.0`；manifest 无依赖；包内含 `0.110.0` 核心 DLL | 无；仅发现 `0.110.0` 文件 | 启动但失败；`MOD-W008/RuntimeFailure`：`intentgraph2core` 同名程序集已加载 | 0 个运行时程序集表面（静态包含 8 文件、4 DLL） | Unsupported（worker 边界）；玩法 Unrun | 先决定是否允许该包的重复程序集身份和 `0.110.0` 变体；再做 0.111.0 兼容审计 | `LAB_V0111_MOD_MATRIX_3747528152`; `.build/runs/mod-matrix-20261108/3747528152-intentgraph2/run-manifest.json` |
| 3747528329 | Ryoshu | `0.3.6`；闭包 `Ryoshu` | 未声明 | 成功；4 文件/1 程序集/1 PCK | 角色 DLL/PCK 静态发现 | Unrun | 角色选择、初始卡组和首战最小 probe | `LAB_V0111_MOD_MATRIX_3747528329`; `.build/runs/mod-matrix-20261108/3747528329-Ryoshu/run-manifest.json` |
| 3747530622 | LexNinja2 | `v3.0.11-beta-0-g358712f`; `BaseLib>=3.4.5`, `STS2-RitsuLib>=0.5.12`；闭包 `BaseLib>LexNinja2>STS2-RitsuLib` | 未声明 | 成功；13 文件/4 程序集/2 PCK | Stage 7：31 mapped、31 partial、114 unsupported | Partial；Stage 7 Pass 但能力矩阵未闭合 | 从 31 partial 中选一个 Power/卡牌语义完成并收窄 114 unsupported | `LAB_V0111_MOD_MATRIX_3747530622`; `.build/runs/mod-matrix-20261108/3747530622-LexNinja2/run-manifest.json`；stage report `.build/runs/ritsu-stage7-evaluation-native-20261107-final2/stage7-report.json` |
| 3747548629 | Kaguya | `v3.2.4`; `BaseLib>=3.4.7`；闭包 `BaseLib>Kaguya` | 未声明 | 成功；6 文件/2 程序集/2 PCK | Stage 8：事件 `EVENT.TAKETORI_MONOGATARI_EVENT`、奖励、资源账本 | Partial；2 mapped、3 unsupported（UI/Map/Save） | 只扩展一个 Map 或 Save 边界；保留 UI/地图/保存 Unsupported | `LAB_V0111_MOD_MATRIX_3747548629`; `.build/runs/mod-matrix-20261108/3747548629-Kaguya/run-manifest.json`；stage report `.build/runs/kaguya-stage8-evaluation-v24-20261107/stage8-report.json` |
| 3747588205 | Acheron | `v0.4.11`；闭包 `Acheron` | 未声明 | 成功；3 文件/1 程序集/1 PCK | 角色 DLL/PCK 静态发现 | Unrun | 先确认角色初始化是否依赖额外库，再做单战斗 probe | `LAB_V0111_MOD_MATRIX_3747588205`; `.build/runs/mod-matrix-20261108/3747588205-Acheron/run-manifest.json` |
| 3747595039 | MinionLib | `0.6.3`；闭包 `MinionLib` | 未声明 | 成功；3 文件/1 程序集/1 PCK | Stage 9/10 依赖初始化和随从生命周期 | Partial；已有复合运行时证据，但不是完整库覆盖 | 把现有 Shadowverse 真实随从语义抽成一个可复用 MinionLib 生命周期面 | `LAB_V0111_MOD_MATRIX_3747595039`; `.build/runs/mod-matrix-20261108/3747595039-MinionLib/run-manifest.json` |
| 3747602295 | RitsuLib | `0.6.10`；闭包 `STS2-RitsuLib` | **存在且选中 `compat/0.111.0`** | 成功；7 文件/2 程序集/0 PCK；精确变体摘要已锁定 | `STS2-RitsuLib` 与 Runtime 程序集 | Supported（依赖与精确变体范围） | 仅在下一 RitsuLib 角色中扩展 hook；不单独宣称角色玩法 | `LAB_V0111_MOD_MATRIX_3747602295`; `.build/runs/mod-matrix-20261108/3747602295-STS2-RitsuLib/run-manifest.json` |
| 3747644378 | shadowversebydmod | `1.6.0`; `STS2-RitsuLib>=0.6.2`, `MinionLib>=0.6.2`；闭包由 Stage 10 验证为三者 | 未声明；依赖选择 RitsuLib `compat/0.111.0` | 静态 worker 启动但失败；`MOD-W008/RuntimeFailure`：DLL 超过 2 GiB 读取限制 | Stage 10 真实战斗：Minion、组件卡、伤害、死亡、移除、清理 | Partial；Stage 10 Pass，静态 worker 失败已隔离 | 先替换超大 DLL 的受控元数据读取路径，再扩展 5 个 Unsupported 表面 | `LAB_V0111_MOD_MATRIX_3747644378`; `.build/runs/mod-matrix-20261108/3747644378-shadowversebydmod/run-manifest.json`；stage report `.build/runs/minion-stage10-evaluation-20261108/stage10-report.json` |
| 3747725225 | Aemeath | `1.0.2`; `BaseLib>=3.2.1`；闭包 `Aemeath>BaseLib` | 未声明 | 成功；6 文件/2 程序集/2 PCK | 角色 DLL/PCK 静态发现 | Unrun | 角色选择和首战最小 probe；不从静态程序集推断卡牌语义 | `LAB_V0111_MOD_MATRIX_3747725225`; `.build/runs/mod-matrix-20261108/3747725225-Aemeath/run-manifest.json` |
| 3747952763 | ATA_IronClad | `0.1.12`；闭包 `ATA_IronClad` | 未声明 | 成功；3 文件/1 程序集/1 PCK | 非玩法 Ironclad 外观替换 | Supported（加载与隔离范围） | 无玩法替换语义；若继续，仅验证资源替换表面 | `LAB_V0111_MOD_MATRIX_3747952763`; `.build/runs/mod-matrix-20261108/3747952763-ATA_IronClad/run-manifest.json` |
| 3749061519 | St2bs | `v2.3.0`; `BaseLib`（版本下限为空）；闭包 `BaseLib>St2bs` | 未声明；manifest 明确只支持 `0.111.0` | 成功；6 文件/2 程序集/2 PCK | 复杂角色/内容包静态发现 | Unrun | 先做角色或单敌人的最小 native content probe | `LAB_V0111_MOD_MATRIX_3749061519`; `.build/runs/mod-matrix-20261108/3749061519-St2bs/run-manifest.json` |
| 3749627968 | Necrobinder 相关包 | 4 manifests：CardPortraits 1.4.0、OstyAnim 1.4.0、OrchisSkin 1.0.0、RestSiteCursor 1.4.0；无依赖 | 未声明 | 主 manifest worker 成功；9 文件/4 程序集/3 PCK；整条目静态清单共 12 文件 | 卡图、动画、皮肤、篝火光标；`affects_gameplay=false` | Supported（加载与隔离范围） | 不做玩法兼容；若继续，按资源类型做独立 PCK/资源发现 probe | `LAB_V0111_MOD_MATRIX_3749627968`; `.build/runs/mod-matrix-20261108/3749627968-NecrobinderCardPortraits/run-manifest.json` |
| 3787753911 | LieRenTVmod | `v0.1.3`；闭包 `LieRenTVmod` | 未声明 | 成功；2 文件/0 程序集/1 PCK | 只有 PCK，manifest 标为非玩法 | Supported（加载与隔离范围） | 无玩法语义；无需进入角色接入 | `LAB_V0111_MOD_MATRIX_3787753911`; `.build/runs/mod-matrix-20261108/3787753911-LieRenTVmod/run-manifest.json` |
| 3829924471 | YukiMakoto | `v0.0.7-hot`; `STS2-RitsuLib>=0.6.10`；闭包 `STS2-RitsuLib>YukiMakoto` | **存在且选中 `lib/0.111.0`** | 成功；9 文件/3 程序集/0 PCK；精确变体摘要已锁定 | 角色 DLL 静态发现，未执行角色语义 | Unrun | 使用已选 `lib/0.111.0` 做 RitsuLib 角色首战 probe | `LAB_V0111_MOD_MATRIX_3829924471`; `.build/runs/mod-matrix-20261108/3829924471-YukiMakoto/run-manifest.json` |

### worker 和隔离汇总

- 21/21 条目均由新 `SimulatorLab.ModWorker` 进程启动并生成独立 run manifest；19 个 `succeeded`，2 个 `failed`。
- 21/21 的 `workerProcess=true`、`coreLoadedModAssembly=false`、Workshop 枚举/正式存档/正式设置/正式游戏数据访问均为 false、`harmonyPatchCount=0`、`temporaryFileCount=0`、`assemblyLoadContextUnloaded=true`。
- 成功分母：静态 worker `19/21`；失败分母：`2/21`。失败分类均为 `MOD-W008`（映射到 `RuntimeFailure`），不是依赖缺失：
  - `intentgraph2`：`intentgraph2core, Version=1.0.0.0` 同名程序集已加载。
  - `shadowversebydmod`：DLL 超过 .NET 当前读取 API 的 2 GiB 限制。
- 失败进程没有写入快照，也没有产生 Mod 二进制副本；输出只保留 run manifest、日志和输入摘要。

## 阶段能力和错误分母

| 范围 | 证据 | Supported | Partial | Unsupported | 未运行 |
| --- | --- | ---: | ---: | ---: | ---: |
| 全量条目（按条目主表面） | 本报告矩阵 | 7 | 5 | 1（intentgraph2 worker 边界） | 8 |
| Watcher | `watcher-runtime-probe/v1` + Stage 6 runner | 角色/战斗原生事实已观察 | 6 个 token Card 未映射 | UI/资源 headless 语义和负成本 Card 映射 | 合法标准模型闭环、Stage 6 runner Pass |
| LexNinja2 | Stage 7 report | 31 capability mapped | 31 | 114 | 其余角色/内容表面 |
| Kaguya | Stage 8 report | Event、Reward、Ledger | 0 | UI、Map、Save（3） | 其他角色/Boss/第四幕 |
| Shadowverse | Stage 10 report | 8 capability mapped，真实随从战斗闭环 | 0 | UI、Map、Save、ComplexComponent、ReplayNetwork（5） | 全包内容和静态 worker 超大 DLL 读取 |

阶段 7/8/10 runner 结果均为 `Pass`，但各自能力矩阵显式保留 `Unsupported`，所以不能把 runner `Pass` 解读为整个 Mod 包完全兼容。Watcher 的 Stage 6 runner 仍为 `Blocked/MOD-010`，尽管真实 Godot probe 已证明首战事实存在。

## 执行过的命令及结果

- `git branch --show-current; git status --short --branch`：`main`，保留了开始时已有的修改、删除和未跟踪路径。
- 读取 `AGENTS.md`、`docs/agents/domain.md`、`docs/mods/MOD_INTEGRATION_EXECUTION_PLAN.md`、固定快照和清单：成功；根目录没有 `GLOSSARY.md`，按仓库规则继续。
- 固定清单检查：目录和清单存在，`Items=21`、`FileCount=142`、摘要为 `7d23eddd…48a73`。
- `dotnet build tools/mod-worker/ModWorker.csproj -c Release --ignore-failed-sources -p:NuGetAudit=false`：失败，`CS8802`（多个顶层语句）；没有覆盖已有产物。
- 阶段专用项目 `WatcherStage6.csproj`、`RitsuStage7.csproj`、`KaguyaStage8.csproj`、`MinionStage9.csproj`、`MinionStage10.csproj` Release build：均成功，0 错误。
- `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-build --no-restore`：`PASS all 137 contract checks`。
- `tools/validation/TestModWorker.ps1`：通过；正常静态发现摘要稳定，错误控制 5 项通过。
- `tools/validation/TestWatcherStage6.ps1`：通过；两次新进程均 `Blocked/MOD-010`，停止摘要相同。
- `tools/validation/TestRitsuStage7.ps1`、`TestKaguyaStage8.ps1`、`TestMinionStage9.ps1`、`TestMinionStage10.ps1`：均通过；阶段 7/8/10 的 deterministic digest 和能力分母符合既有证据。
- 批量 worker：21 个独立输入/输出目录，19 个 `succeeded`、2 个 `MOD-W008`；结果索引在 `.build/runs/mod-matrix-20261108/matrix.json`。
- `tools/native-run/run-native-slice.ps1 -Scenario slice-skip`：退出 0；新 M1 回归目录为 `.build/runs/stage10-m1-regression-20261108`。
- `tools/validation/TestEleanorVictoryReplayEvidence.ps1` 指向历史冻结报告：退出 1，报告路径不存在；未伪造通过结果。
- 批量后关键快照哈希检查：manifest、Watcher、RitsuLib `compat/0.111.0`、YukiMakoto `lib/0.111.0` 和 Shadowverse DLL 均保持清单值。

## 回归

| 回归 | 新结果 | 旧结果 | 结论 |
| --- | --- | --- | --- |
| 核心检查 | `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-build --no-restore`；`PASS all 137 contract checks` | 既有阶段报告记录 `134`，当前工作区新增 3 项后为 137 | PASS；新旧核心契约均未出现失败 |
| 原版 M1 slice | `run-native-slice.ps1`，新目录 `.build/runs/stage10-m1-regression-20261108`；退出 0，22/22 dispatched/confirmed，trace 247，`MemorySaveStore`，Steam=false，ProgressWrites=5、ReplayWrites=1 | M1 最终验收记录完整 RunVictory464、22 场战斗、358 差分步、两次同构建重放一致 | PASS；本轮重跑的是原版 `slice-skip` 回归，不冒充完整 M1 RunVictory464 重演 |
| Eleanor 冻结证据 | `TestEleanorVictoryReplayEvidence.ps1 -ReportPath .build/runs/eleanor-v041-20261101-165958-362/reverse/eleanor-v041-fixed-route-victory-replay.json`；退出 1，报告路径不存在 | 历史设计复核引用 122 checks/zero failed，但当前工作区没有对应 `.build` 证据和 `eleanor-v0.4.1-forms-cardpool.fix` 输入锁 | BLOCKED/未运行；历史文档不能替代当前冻结证据 |

## 已知缺口和未支持语义

- `intentgraph2` 的包内 `intentgraph2core.dll` 与 `intentgraph2core-lt-0.110.0.dll` 造成同名程序集装载冲突；不能通过跳过 DLL 或静默选择 `0.110.0` 变体来宣称 `0.111.0` 兼容。
- `shadowversebydmod.dll` 的静态元数据读取触发超过 2 GiB 限制；Stage 10 的真实 Godot 战斗证据与静态 worker 失败并存，不能互相覆盖。
- Watcher 的真实 probe 记录 129 identities 和完整首战边界，但标准适配器对 6 个负 canonical-cost token Card 返回 `Unsupported`；没有把它们强行映射为普通卡牌。
- LexNinja2、Kaguya、Shadowverse 的 Stage runner 通过只代表已声明窄切片；各自能力矩阵中的 Partial/Unsupported 必须保留。
- 8 个条目只有静态 worker 证据，没有角色、敌人、事件或奖励玩法运行时证据；它们均列为 `Unrun`。
- `dotnet build tools/mod-worker/ModWorker.csproj` 当前失败于 `CS8802`：`Program.cs` 与阶段顶层入口被通配编译到同一项目。批量验证使用已有已构建 worker DLL，阶段专用项目构建和运行均通过；该工程性阻塞未在本阶段修复。

## 验收标准逐项结果

1. **全部 21 个条目进入矩阵**：通过；清单解析 21 个来源、24 个 manifest，矩阵逐条列出。
2. **manifest 可解析**：通过；核心 ModCatalog 检查通过，清单摘要和文件分母一致。
3. **依赖闭合**：通过静态解析；所有声明依赖均能在固定快照中解析，RitsuLib/MinionLib/Shadowverse 闭包由阶段 9/10 复核。`intentgraph2` 的失败是程序集装载冲突，不是缺依赖。
4. **精确 `0.111.0` 兼容变体**：通过；RitsuLib 只选 `compat/0.111.0`，YukiMakoto 只选 `lib/0.111.0`；未对其他条目静默回退。
5. **worker 只读启动**：部分通过；21/21 新进程和 run manifest，19/21 静态发现成功，2/21 以 `MOD-W008` 停止。
6. **内容表面和能力状态**：通过；资源/QoL 仅记加载与隔离，角色/复杂内容按真实 probe 和能力矩阵记 `Supported`、`Partial`、`Unsupported` 或 `Unrun`。
7. **稳定错误码、成功分母、失败分母**：通过；静态 worker `19/21` 成功、`2/21` 失败，失败码均为 `MOD-W008`，阶段能力错误分母见上表。
8. **快照摘要、seed、适配器版本、worker 身份、run manifest 引用**：通过；每个条目表中均有 seed 和 run manifest，身份链固定到 `0.111.0` 与快照摘要。
9. **原版 M1 回归**：通过当前 `slice-skip`；退出 0、22/22 确认，未把该窄回归写成完整 M1 RunVictory464。
10. **Eleanor 冻结证据回归**：阻塞/未运行；历史报告与输入锁缺失，复核脚本精确退出 1。
11. **性能、策略、大规模内容接入**：未执行，符合本阶段边界。

## 已完成与未完成阶段

- 已完成或有当前证据：阶段 0 快照固定、阶段 1 catalog/版本/依赖解析、阶段 2 标准模型与错误码、阶段 5 只读 worker、阶段 6 Watcher 事实探针、阶段 7 LexNinja2/RitsuLib 窄切片、阶段 8 Kaguya 事件、阶段 9 MinionLib/Shadowverse 生命周期、阶段 10 批量矩阵与回归。
- 未完成：全量角色玩法兼容；Downfall、figure_Saya、Remilia、Ryoshu、Acheron、Aemeath、St2bs、YukiMakoto 仍为 `Unrun`；intentgraph2 的 0.111.0 兼容性未建立；Watcher、LexNinja2、Kaguya、Shadowverse 的未支持能力仍未闭合；Eleanor 冻结证据未能重跑。

## 下一阶段唯一建议入口

先修复 `ModWorker.csproj` 的顶层入口编译隔离，并为 `intentgraph2`/`shadowversebydmod` 建立受控的大文件与重复程序集元数据读取路径；完成后只选择一个未运行角色（建议 `YukiMakoto` 的 `lib/0.111.0` 变体）做下一条最小原生首战纵向切片。不要并行扩大其他角色或进入性能/策略工作。

## 仍需用户决定的边界

- 是否接受非玩法 Mod 以“加载与隔离通过”作为终态，还是要求每个 PCK/资源包都增加独立资源探针。
- 是否允许为超过 2 GiB 的 DLL 引入分段或内存映射元数据读取；这会改变当前 worker 的实现边界。
- 是否允许对 `intentgraph2` 的内置 `0.110.0` 核心变体进行兼容审计，还是直接将其维持为 `Unsupported`。
- Watcher 的负 canonical-cost token Card 是否要扩展标准模型；在决定前不能把 Stage 6 升为完整 Pass。
- Eleanor 冻结证据是否由用户重新提供固定输入和报告目录；在此之前不能把 Eleanor 回归写成当前通过。

## 是否提交或推送

否。仅新增本交接报告；未提交、未推送、未创建远程 PR，未删除文件，未重写历史，未修改固定快照。
