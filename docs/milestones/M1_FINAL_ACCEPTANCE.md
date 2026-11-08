# M1：原版离线完整流程与可信数据验收

日期：2026-11-04。**按下列声明范围，M1 验收通过。** 本轮是对前批实现的独立证据复核与补齐：没有修改运行代码、搜索评分或原生观察器。六个固定样本、正常开局完整胜利、同构建重放与适用回归均完成；历史失败没有删除或改记为成功。提交与推送属于随后的发布步骤，其结果另记，不由本报告预先宣称。

## 1. 范围、版本和策略

首个原版角色 Ironclad，A0，单人 Standard，独立已解锁实验档案，正常开局、默认三幕，使用合法本机游戏程序集。没有启动游戏 UI、写用户存档、初始化真实 Steam/网络，或开展新的 Mod 适配。

| 项目 | 固定身份 |
| --- | --- |
| 游戏 | v0.111.0 / 41cef1ea；DLL `0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9` |
| 最终原生生成器与检查器 | `F7C4A5B6270122BC0E4551310BD71C3FD7880B28A47B53933D16EE511BC1AD98` |
| CombatSolver | 0.50.1，本地 `death-amount-sync.1` 衍生；DLL `A5AEFA38D53A84B4F67BF71EDA2F3B1EEF7B0EE0FFCE3FC0C09CDE3413D2C35E` |
| RandomForeseer | 0.13.19 / `native05-reflections`，13 项本地输入锁 |
| 工具链 | .NET SDK 10.0.201；运行目标 net9.0 |
| 源码 | 原 HEAD `d73039d` 加冻结实际源码；不是仅用 HEAD 代表构建。1,041 源文件/依赖记录、137 编译输入、16 产物与 3 游戏输入重新核对一致 |

原生业务执行为主线，CombatSolver 提供 **Oracle** 战斗决策与一步影子差分；战斗外只复用已验证的 RandomForeseer 窄预测，其余原生执行明确 `NotCompared`。全 run 的 `NotCompared` 不会吞掉逐动作的独立差分报告。

Guarded 仍为默认。验收队列显式使用统一 `RunToTerminal`：允许合法、受支持且一步影子可核对的候选继续至真实终态，即使有限搜索预测死亡。取消、TimeLimit、无候选、未建模风险、非法动作和差分不一致仍停止。搜索 beam 8、成员 nodes 256、10 秒请求、DOP 2；成员累计展开返回准入 16,384。后者不是搜索中精确熔断，也不是上游 Low 档位。策略改变与三项正确性修复共同影响轨迹，不能据此声称求解质量提高。

## 2. 最终自然执行与重放

同一最终构建的正常开发路径 `LAB_V0111_NATIVE_DEV_01`，零冻结前缀、无资源改写、无按 Boss 调评分：

- **RunVictory，464 根动作，22 场实际战斗胜利，358 步完整原生/分支差分。** 原生奖励、合法行进、跨幕、第三幕 Boss、Architect 真实选项及 WinRun 均在同一个 run 内。
- 两个新进程分别以 detailed / minimal 观察模式重放，仍为 RunVictory464。完整根动作、子选择、奖励、最终状态和 RNG 一致；detailed 的 **7,239 个完整状态观察点**一致，模式共有的 **3,935 个观察点**一致。
- 真实 WinRun 之前玩家存活，之后原生胜利清理导致最终 HP0；Won=true、Losses=0，不能把清理后的 HP0 误判为 Death。
- 36 项胜利复核通过，包含真实终局调用链、358 步完整 continuation、父 fork 不变与生产检查器。证据：`m1-acceptance-work07/new-victory-comparison.json`。

冻结重放仍会重新调用求解器，并校验其动作与冻结输入一致。它不是关闭搜索的直接动作驱动器；跨构建重新求解可能合法改变建议，不能把这种模式当作搜索性能基线。

### 六个预登记评估种子

前缀均为 `LAB_V0111_NATIVE_EVAL_`。每个种子一次自然在线执行、一次新进程同构建冻结重放；不换种子、奖励 index 或按种子改策略。重复不是独立样本。

| 后缀 | 实际终局 | 每次确认/派发 | 每次完整差分步 | 每次状态/RNG 观察 | live/replay |
| --- | --- | ---: | ---: | ---: | --- |
| 01 | Death | 495 / 495 | 424 | 6,569 | 一致 |
| 02 | Death | 215 / 215 | 181 | 2,756 | 一致 |
| 03 | Death | 415 / 415 | 345 | 5,417 | 一致 |
| 04 | Death | 498 / 498 | 430 | 6,789 | 一致 |
| 05 | Death | 467 / 467 | 366 | 6,946 | 一致 |
| 06 | Death | 406 / 406 | 338 | 5,540 | 一致 |

90 项队列复核通过；01/02 本批补齐，03–06 复用前批**同一最终构建**的不可变原始执行，本批再次调用生产检查器并比较完整轨迹。六条 live 合计 2,496 根动作、2,084 差分步、34,017 状态观察。它们证明声明队列的执行和复现能力，**不能给出可靠胜率，也不能说明这些局不可获胜**。

## 3. 统一验收矩阵

| 门 | 证据与结论 |
| --- | --- |
| N0 安全宿主/版本 | 固定输入、审计、MemorySaveStore、SaveProgress/replay 拦截、独立进程；当前源码与输出一致 |
| N1 无求解器前置 | 最终产物的 native-only skip24 / take25 自然切片：首战 Victory、原生领奖、下一战 Play；2 个生产证据检查通过。编译含可选 solver，切片不初始化或调用它 |
| N2 选择与数据 | 当前生产逻辑复跑 142 个适用历史控制，覆盖生命周期、事件、选择、身份、完整状态、奖励/交易、阶段、未知补丁；另有本批 12 个死亡控制、10 个独立策略控制。预期拒绝与真实业务分列 |
| N3 节点类别 | 正常路径和保留的窄夹具覆盖休息升级、事件及嵌套任务、商店、宝箱、遗物、药水、精英/Boss；旧子阶段逐项范围见其报告，未声称全部内容分支 |
| N4 跨幕 | 当前完整自然路径实际进入三幕，原生转换、奖励、持久状态和 RNG 连续，重放一致 |
| N5 真实终局 | RunVictory464 + 两次同构建重放；六个实际 Death + 各自重放。死亡用例独立进程，不复用污染的死亡 worker |
| N6 样本/数据/回归 | 六固定种子12路径、开发胜利3路径、Q1–Q3、Core98、适用原版与独立冻结 Mod 回归均完成；没有未解释的 Engine/Data/Diff/Invalid 错误 |

这里的最终同构建验收包含全部固定样本、完整胜利重放和当前契约回归；没有机械再执行历次约数百个原生开发实验。变更涉及的实际生成路径在最终构建重跑，历史生产控制在最终检查器重验，旧业务证据保留原生成身份。旧缺玩家身份锚点的记录仍不予新契约认证，不能拿哈希存在替代语义证据。

本批自伤夹具实际 7 根动作、2 步差分，前批同构建能力死亡夹具仍保留。三项修复的独立审查见 [终态复核](M1_ACCEPTANCE_TERMINAL_REVIEW.md)。未逐项执行自伤组内其他两张卡或费用 Hook 子选择续算；不扩大为全卡证明。

### 单列兼容构建

旧构建 `D5FF494B75BF670D2EFD0A335A6E87290F6C5A2C7B3C8511839CD68ACB0DFF1D` 的 5 个实际运行产物仍匹配。重新执行原版9边界 Victory/下一节点、随机目标 RNG、Eleanor16动作 Victory（122检查），各一次通过。旧双重复证据保留，但不冒称本批重复两次，也不把此构建混入最终原版生成器。15 个 Mod 输入与52个兼容源码均未改变，没有新增 Mod 专项。

## 4. 保留的失败与事故

原 Guarded 旧胜利路线在新构建进行严格重放时，a51 **派发之前**返回 InvalidAction/exit1：q36 新求解器建议 ShipInABottle，冻结路线要求 Bash+。此前50个确认状态/RNG与旧原始记录逐项相等，35个实际战斗差分通过。该记录保留为**已解释的跨构建建议不匹配**，没有重标为通过。随后先登记同一开发种子的零前缀自然执行，再取得上面的新胜利和两次同构建重放。原 Guarded 胜利回归本身仍为未通过，当前最终胜利声明适用于显式 RunToTerminal。

前批五条质量保护停止、一条预算停止，以及换边历史、自伤顺序、死亡能力缓存的实际 DataError 失败均保留。前批一次派生控制文件覆盖事故也保留：原始轨迹未覆盖，事故前派生输出无法靠当前哈希恢复，不声称恢复。此次使用新的独立目录，最终复查旧批04/05的46,679份文件与批06的29,975份原生输出均未变化。

## 5. 数据可用性和初步成本

`m1-acceptance-work06/data-query-examples.json` 使用当前最终构建 EVAL03：

1. Q1：从 a415 的真实伤害历史、玩家身份、HP4→0和 loss 回调解释实际死亡，不从同时存在的能力猜伤害来源。
2. Q2：将原生取得的 Feral 永久牌实例 i24，关联至战斗副本 i147、a58实际打出和能量/能力变化；不推断奖励对胜率的因果收益。
3. Q3：定位版本、输入、动作/选择、完整 RNG、原生退出与已有运行时间，允许重新核验。

本批 **14/14 次原生启动**（包含3个单列旧兼容进程与1个保留失败），从登记到最后原生结束 **15.11分钟**，原生输出 **1,908,338,539字节**，低于75分钟/8GiB上限。当前 batch07封签12,391个输出文件。最终源/输入/产物/历史检查77,918项，无不一致；这只是字节一致性，真实执行依赖原始进程状态、调用链和新进程重放。

没有新性能实验或 profiler。已有日志中，开发 live114.87秒、detailed重放112.86秒、minimal重放109.90秒；记录的进程峰值分别约1.61/1.62/0.96GiB。每次搜索成员累计159,733展开。各阶段计时可重叠，其他检查可能同时运行；不能用重放与在线相减估算搜索耗时，也不能用单路径宣称优化收益。机器总CPU、每时刻子进程峰值、独占搜索耗时未采集，标为未知。

## 6. 复现入口与证据位置

大体积原始产物、游戏/Mod DLL、上游源码及本机路径不进入仓库。持有合法本机依赖时：

- 构建：`tools/native-run/build-native-run.ps1` 的 `GamePath`、`HostSource`、`SolverSnapshot`、`ForeseerSnapshot` 与新的 `OutputRoot`；版本、许可和源锚点必须匹配。`HostSource` 是已准备的本地上游 harness，不能把任意新版源码当作兼容输入。
- 执行：`tools/native-run/run-native-slice.ps1`，明确 `BuildManifest`、新的 `OutputDirectory`、seed、scenario及 `SolverPlan`。完整在线场景为 `solver-native-darv-act2`，plan `PrefixCount=0`，初始 route/choices/rewards各为 `[]`；当前命名保留历史，实际控制器可完成三幕。
- 重放：复制真实完成路径的 route/choices/reward-choices 到独立输入目录，记录SHA256并更新plan，场景 `solver-native-darv-act2-replay`。不覆盖输入或旧输出。
- 检查：`dotnet <冻结目录>/bin/Release/net9.0/OfflineSearchHarness.dll --review-full-run <已完成完整run目录>`，生产检查器只读原始文件。基础24/25动作切片使用同一DLL的 `--review` 入口。另有 `tools/native-run/review-native-slice.ps1 -RunDirectory <新副本目录>` 的旧窄切片检查；该脚本会写派生结果，不能指向封存目录，也不适用于完整run。Core入口为 `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj`。

最终构建：`.build/native-build/m1-acceptance-b06-final06/build-manifest.json`。

本地批06：`.build/runs/m1-acceptance-work06/`；本批：`.build/runs/m1-acceptance-work07/`。核心索引：`closeout.json`、`execution-ledger.json`、`cohort-comparison-final06.json`、`new-victory-comparison.json`、`independent-terminal-review.json`、`death-controls-final06.json`、`historical-controls-summary.json`、`remaining-controls-summary.json`、`legacy-compat-summary.json`、`telemetry-summary.json`、`final-binding-audit.json`、`native-evidence-seal.json`。原始进程的 `process.json`、`result.json`、`trace.jsonl`、solver-query与solver-online-step分别保存。

## 7. 限制及交付判断

M1通过的是固定版本、首个角色和声明样本/类别的原生完整流程与可信采集能力，不是全原版内容覆盖、全部组合精确预测、强策略、可靠胜率或平衡结论。未支持内容必须明确停机。RandomForeseer不承诺多步隔离精确预测；战斗外纯原生状态不冒称独立效果差分。死亡进程隔离不等于零售run-loss清理完全等价。公开Mod SDK、全角色、多人、存档恢复、用户游戏运行验收和性能/策略新实验均未开展。

本轮独立复核没有发现声明范围内仍需修复的正确性阻断。下一步先审查和发布已授权的M1提交；后续性能与策略工作另按用户确定的里程碑开展。历史“待独审/未完成”文档是当时的记录，以本报告和机器化收口索引为最新状态。
