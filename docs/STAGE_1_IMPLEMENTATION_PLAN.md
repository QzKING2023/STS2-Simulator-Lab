# 阶段 1 实施方案：可信终态、停止与失败证据

设计日期：2026-11-02。状态：**设计交接，尚未实施、编译或运行本阶段测试。**

当前状态：替换候选已于 2026-11-02 通过独立复审，阶段 1 在已声明范围内完成。原始设计和首候选失败历史保留；最终身份、修复及验收证据见 [STAGE_1_REVIEW_REPAIRS.md](STAGE_1_REVIEW_REPAIRS.md)。

上位规划：[长期路线图](LONG_TERM_ROADMAP.md)。当前证据：[里程碑状态](MILESTONE_STATUS.md)、[实现来源](PROBE_PROVENANCE.md)、[两战验证](TWO_BATTLE_VALIDATION.md)、[边界／批测／在线模式](EXTENDED_VALIDATION.md)。项目入口为 [README](../README.md)。

本文将用户批准的“阶段 1”定义为路线图 P0/P1 与 R0/R1，加上必要独立契约和最终统一回归；不把长期表中的全部 P1 优先级条目都纳入本阶段。新的通用选择协议、两张奖励卡适配、实验与节点扩面属于后续阶段。

## 1. 基线、范围和完成定义

本轮只读核查确认 HEAD 为 `2d8b7362b8f344b7be85178c82541ceb967cb713`，本地 `origin/main` 引用为 `2c819510817f64f7fddc7d315b05282f6463f528`，未联网刷新远程。原有 19 个未提交实现／验证文件和长期路线图仍在工作区；下一轮必须从这些内容继续，不能只检出 HEAD。当前路线图 SHA-256 为 `F9A5956658D4F6A67C7F16D518E5BE959BD0DA41CD94531B45F2707F91BC5DC2`。

固定输入继续为游戏 v0.111.0 / 41cef1ea 与 Eleanor v0.4.1-forms-cardpool.fix：

- 游戏 DLL：`0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9`。
- 角色 DLL：`949320F651298D34399335EB28176578D270139DFF2CCE60FB7FDF35E7DBAD42`。
- 本轮重读 `.build/pinned-inputs/eleanor-v0.4.1-forms-cardpool.fix/input-lock.json`，15 个文件哈希均匹配。旧版本 pin 和原始报告不覆盖。
- 既有三种子批测为 8 Victory / 4 unsupported；阶段 1 保留 ReturnFlow 和 ManaConversionPrinciple 的既有覆盖停点，不要求它们转为通过。死亡也不是策略成功的必要条件。

**阶段完成条件：** 所有本阶段必需验收项有实际证据或明确的负面测试证据；Victory 与 Death 均有真实原生路径；预算、unsupported、非法动作、执行错误、差分不一致能分别报告和定位；旧能力在冻结的最终构建上统一复核；安全审计与输入锁不退化。已知两个奖励能力缺口单列为覆盖不完整，不能以 overall pass 隐藏。若必需项遇具体不可继续的阻塞，应汇报“阶段未闭合”，不能仅更新文档为完成。

本阶段只允许 Lab 内后续实现和测试。不改角色源码／数值、游戏安装、用户存档，不部署、不启动零售 UI、不提交推送。MemorySaveStore、TestMode、Steam 未初始化、SaveProgress/replay 拦截和精确来源审计继续保留。

## 2. 已证实的代码位置与设计结论

以下是本轮静态审查事实，不是本阶段运行结果。

| 位置／符号 | 现状 | 实施结论 |
| --- | --- | --- |
| [EleanorV040Probe.cs](../tools/validation/EleanorV040Probe.cs)：`RunVictoryReplay` | EndTurn 先调用私有 `AdvanceRound`，随后要求 `_lastTurnStartSimulator` 和下一回合 Play；已有 `nativeDeath`／`simulatorDeath`，但此路径尚未证明可达 | 用终态／下一稳定边界分流，不能总要求下一玩家回合 |
| 当前锁定 CombatSolver：`CombatBeamSolver.Expansion.Replay.AdvanceRound` | 敌方阶段胜利或死亡可能返回 `SearchBoundaryReason.None` | 返回 `None` 不代表下一回合；必须联合 `IsInProgress`、`TerminalStamp` 与实际 continuation。`PendingChoice` 等停止原因也不能被强改为 terminal |
| 当前锁定 CombatSolver：`CombatPredictionSimulator.CheckWinCondition` | 已有 Victory/Defeat terminal stamp；明确不模拟 `EndCombatInternal` 后的清理、奖励和 run-loss | 战斗终态差分与原生清理契约分开，不为阶段 1 重写完整战后仿真 |
| [VanillaCombatRootAdapter.cs](../tools/validation/VanillaCombatRootAdapter.cs)：`CaptureLive`、`CapturePredicted`、`ResolvePhase` | 已支持 `terminalDefeat` 和 Core `CombatPhase.Defeat` | 复用该能力；报告可统一称 Death，但不要无必要迁移现有 Core 枚举 |
| `InstallVictoryHeadlessTerminalCapture` | 精确匹配 `EndCombatInternal(CombatTurnState)`，只读 prefix 当前只接受活玩家／敌人死亡的 Victory | 将观察器推广为有 outcome 的只读捕获；先核实 pinned game 的死亡实际调用链，不能假定与胜利完全相同 |
| 角色锁定实现：`CrystalOrb.AfterCombatEnd`、`ManaRuntime.End` | 原生 hook 调用 End；End 解除 EnergyChanged/PowerRemoved/PowerApplied 订阅，清理交易栈、ledger 并 Reset | 死亡是否经过该 hook 必须由原生轨迹确认；禁止手动调用 End 补一个看似正确的清理结果 |
| 角色 `ManaRuntime.StartTurn` 与 Lab `ApplyBranchTurnStart` | 维持失败伤害后若已死亡会直接返回，跳过后续 orb grant／属性恢复 | 可用现有机制建立回合开始致死夹具；必须走真实 EndTurn 调度，不能只调用 StartTurn 作为完整路径证据 |
| [NativeCombatDecision.cs](../tools/validation/NativeCombatDecision.cs) | Query 固定 1000 nodes／5000 ms；票据校验 combat 引用、源 live/continuation 和已消费状态；空 route 抛 unsupported | 保留源绑定，增加结构化查询结果与预算原因；补 stale/cross-combat/terminal 负测，不能仅重复已有 duplicate 测试 |
| [NativeSecondBattle.cs](../tools/validation/NativeSecondBattle.cs) | 决策／回合／90 秒限制多以 unsupported 结束；90 秒在查询前检查，不是硬期限 | 将边界预算与实际 solver budget 分开，提供可测试的有界配置，保持默认值 |
| [NativeModProgression.cs](../tools/validation/NativeModProgression.cs)、[ApplyNativeRunLoopOverlay.ps1](../tools/validation/ApplyNativeRunLoopOverlay.ps1) | 上层将非 passed 结果统一抛异常或提前返回 | 统一报告需要贯穿顶层入口，不能只改叶子 probe；Death 不进入奖励或 next-node |
| [ManaShieldBoundaryProbe.cs](../tools/validation/ManaShieldBoundaryProbe.cs) | expectedBlock 从当前 DynamicVars 推导 | 增加独立锁定数值表，仍保留原生／仿真差分和原始变量诊断 |
| 两个构建脚本与现有 drivers | 不同 suite 曾使用不同 harness 哈希；runner 可能构建并立即执行 | 分离构建与运行，以明确 artifact manifest 启动，最终回归期间不重新构建 |

本轮用于核对后端的本地副本来自现有最终报告指向的 `.build/local-reference/CombatSolver-v040-86e107119fa8/`，harness SHA 为 `E3836899E656574E8055DBCFEFF669FB4115F675FB1BD2C4E245F342248FC2C0`。这是旧证据输入，不能把它命名为本阶段最终构建。

## 3. 最小实现边界与结果协议

### 3.1 增量文件职责

建议新增 `src/SimulatorLab.Core/ValidationOutcome.cs` 与相应 Core checks，定义无游戏依赖的报告类型／校验。Lab harness 可新增 `NativeTerminalProbe.cs`、`StageOneFailureProbe.cs`，将新观察器和诊断控制从大 probe 中分离；现有 replay、decision、progression 调用这些小组件。文件名是建议，不要求为命名重构已工作的代码。

建议新增 `tests/fixtures/stage-one-cases.json`、`tests/fixtures/eleanor-v041-contracts.json`、`tools/validation/run-stage-one.ps1` 与构建 manifest 生成辅助脚本。单一 manifest 调度现有 suites；保留旧脚本入口和原始 JSON，增加新结果 envelope，不重写旧证据。禁止顺带抽象所有 Mod、全 run snapshot 或重写搜索器。

### 3.2 每个 case 必须保存的正交字段

| 字段 | 建议值／语义 |
| --- | --- |
| `schema`、`caseId`、`buildId`、`inputLockSha256` | 新版本报告 schema；绑定不可变输入／构建 |
| `scenarioKind` | `native-flow`、`native-api-fixture`、`frozen-replay`、`contract-control`、`injected-fault`；不能把后两者记为真实游戏故障 |
| `nativeOutcome`、`simulatorOutcome` | Victory、Death、Ongoing、Unknown；保留两端，不能由 sim 推定 native |
| `comparison` | Exact、Mismatch、NotCompared；另存 compared-through action/boundary，prefix exact 不代表整场已比较 |
| `coverage` | Complete、Unsupported、NotRun；未知奖励仍保留已执行前缀 |
| `stopReason`、`reasonCode` | Completed、BudgetExceeded、Unsupported、InvalidAction、EngineError、DifferentialMismatch、EnvironmentBlocked、Cancelled、Interrupted、NoCandidate；稳定 code 与人类说明分开 |
| `executionState` | NotStarted、AtBoundary、ActionEnqueued、Draining、TerminalCaptured、OutcomeUncertain；记录最后确认动作、边界和执行阶段 |
| `verificationStatus` | Passed、Failed、NotRun；与策略结局独立。预期的死亡／拒绝／注入错误可通过该 case 的断言 |
| `budget` | 请求值、实际 policy、消耗、触发限制、候选数、是否采用候选；不把 elapsed 自动解释为 solver 的内部停止原因 |
| `evidence` | before/after roots、continuations、RNG、parent fork、选择／动作、生命周期、异常和安全 guard 事件的本地相对路径与哈希 |

死亡与差分不一致可以同时出现；应保留 native Death，并使 comparison=Mismatch、verificationStatus=Failed。先发生差分后又出现 unsupported 时，主错误仍是差分，不能降级。非法动作在原生派发前拒绝，和入队后执行异常严格区分。

`NoCandidate` 不自动意味着 Unsupported：必须有 capability gap 才记后者；若确实耗尽预算且没有候选，stopReason=BudgetExceeded、candidateCount=0；无预算原因也无 gap 的空结果记 NoCandidate 并保持进行中／未决状态，不擅自 EndTurn。

### 3.3 退出码与预期失败

保留项目已有 0/1/2 的外层兼容：0 表示声明的检查全部完成且没有未覆盖路径；1 表示非预期错误、差分或必需检查失败；2 表示没有非预期正确性失败，但仍有明确未覆盖／未完成路径。完整三种子 suite 保留 4 条已知 unsupported 时仍退出 2。阶段总报告可判“必需验收满足、保留已知覆盖缺口”，不能改称整个 cohort 全通过。

负面 case 可以预先声明准确的 reasonCode／注入点／状态不变量，得到这些结果后 verification=Passed；子进程实际退出、原始 EngineError/Mismatch 分类必须保留。只允许清单中的精确负面控制按此处理；任何同名异常、无报告退出或未知故障不能被 catch-all 吞成通过。

写入报告应在既有 Lab run 目录内，按动作递增 checkpoint；退出时保留最后确认边界。原生动作可能已执行时不自动重发。时间戳、PID、输出路径和耗时可从确定性比较中明确排除；战斗状态、RNG、动作顺序、停止原因和终态不能排除。

## 4. 真实终态与死亡生命周期方案

### 4.1 执行顺序

1. 用 native API 或真实合法动作建立已声明的初始场景，记录 setup 操作与前后状态；完成后捕获 native root，再从该 root 生成 parent／child。不要给仿真手工填预期死亡状态。
2. 在 child 执行对应动作／回合调度；返回后先识别 terminal stamp，再处理正常 next-player boundary 或 unsupported。死亡分支通常不会触发下一回合 callback，不能据此否决已经发生的终局。
3. 在 native 上实际执行同一合法动作／EndTurn，并 drain。即使 sim 预测死亡，也不能提前把 native 标成死亡；若 sim 已有未知效果，则停在执行前并报告 unsupported。
4. 原生只读观察器在与后端相同的战斗终态语义点捕获状态。不要直接拿 teardown 后空牌堆／零资源与仿真战斗末尾比较。Victory 与 Defeat 分别验证实际调用链、原因和阶段；不要通过手动设置 terminal flags、调用 EndCombat 或改 HP 直接构造结论。
5. 在自然清理之后，单独观察 hook／run-loss／队列／角色订阅和持久状态；验证无奖励生成／领取、无 next-node。不得为了 Death 跳过原生必要业务清理，也不启动 UI。

**三个证明层分别报告：** A 战斗终态 native/sim 全状态差分；B 原生生命周期独立契约；C 宿主隔离与安全审计。B/C 通过不等于模拟器独立预测战后清理。当前后端明确不模拟 teardown，本阶段不把此事实包装为完整生命周期差分。

### 4.2 两条必需 Death 路径

- **D1 敌方攻击致死：** 使用已支持的固定遭遇及 seed，优先选可复现的固定合法动作前缀；也可在 fork 前用真实 `CreatureCmd.Damage` 建立非致死低 HP 的明确 API 夹具。致死动作必须是实际 EndTurn 后的敌方攻击。禁止直接赋 HP、手动 Kill、拼接终态或用“只剩 0 HP”代替 native defeat 流程。
- **D2 Reverse 维持失败致死：** 通过实际 Guide 选择进入 Reverse，真实 Mana 支付 API 排空，再经真实 EndTurn 到玩家回合开始。可以用原生 HP／Block API 建立边界夹具，必须声明全部 setup；敌方阶段不能先把玩家打死，否则只是 D1，不能算 D2。断言维持失败的伤害来源、Neutral 与自有 Str/Dex 撤销、死亡后没有 orb grant／恢复及下一 Play。已有 direct StartTurn probe 只作机制辅助，不替代此路径。

两条路径各在新进程重复两次，固定设置与动作。D2 的 HP／Block 条件应由 pinned native 规则预先审定；若 setup 未达到预期因果点，判 FixturePreconditionFailed，不靠更改结果标签过关。必要的少量初始状态调整只在明确 API 夹具中完成，不改角色玩法，也不污染自然两战种子的报告。

### 4.3 清理的真实契约

先静态确认 pinned `CombatManager`／run-loss 调用链，再登记“哪些事件在死亡时本应发生”。目前只确认角色 `AfterCombatEnd → ManaRuntime.End` 的关系，不能先假定死亡必然调用它。应捕获：终态前 HP／form/Mana/Record/Echo／ledger，调用到的 native hook、终态后的资源与订阅、战斗是否结束、是否还有排队动作、奖励及节点请求计数。

首次执行 Death 前还应审查该调用链的保存、run history、replay 与平台服务入口，确认它们仍落在 MemorySaveStore／已审计拦截范围；不能用胜利路径已安全来推定死亡路径安全。事件计数按实际生命周期记录，不把胜利的固定次数套到死亡。若出现未受控的文件／云服务入口，先停止该 probe 并说明具体目标，不运行风险不明的初始化。

至少一条 Death 夹具预先通过现有原生 Recording/Block/Damage API 建立非零 Echo 和永久 Recording，并记录 setup；临时 Recording 在回合开始自行到期必须与战后清理区分。若死亡实际调用 End，检查其 Reset／退订语义；若原生没有调用，报告真实残留与调用缺口，不手动清零。引擎原有行为或角色 bug 需要跨项目修改时，保留证据并暂停该依赖，不能在 Lab 假装修复角色。

复用原有只读 terminal observer 和展示清理 seam，新增精确观察时登记方法、版本、owner 与注册 stamp；不得屏蔽 `Die`、`CheckWinCondition`、`EndCombatInternal` 或奖励语义来躲过 headless 错误。未知 UI/native 依赖先分类 EnvironmentBlocked；只有经静态证据确认纯展示且未扩大宿主权限的窄处理才进入现有 Lab 适配范围。

## 5. 预算与动作票据方案

### 5.1 预算分层

保留默认 Evaluate／beam8／1000 nodes／5000 ms／DoP1，以及 48 决策、12 回合、90 秒查询前检查。新增经过校验的小型配置对象供专门 case 使用；不得只改报告标签而未传入 solver bootstrap。每次保存 actual policy，检查有效值是否被后端下限或 profile 覆盖。

- **求解预算：** 最低合法节点预算等真实配置触发实际后端停止，保留候选与停止依据。优先节点预算以获得可重放原因；wall-time 结果可能不确定，单独报告。若结果 API 不暴露原因，可在 Lab 复用后端中增加只读 telemetry，不更改搜索规则，不仅凭“节点数看起来接近上限”推断。
- **控制器预算：** MaxDecisions／MaxTurns／ElapsedStop 控制是否发起下一查询。用 1 次决策或一个回合的明确 case 验证停止；ElapsedStop 的判定可用 Lab 时钟接口作合同控制，但不能声称它是实际 solver 时间预算测试。
- **执行保护：** 既有 45 秒 native drain 超时属于执行状态不确定／执行保护，不自动归类为策略预算耗尽。可由父进程对自己创建的 worker 设置硬 watchdog，超时正常取消或结束该 worker、保留日志并确认退出；不终止无关进程，也不重试未确认动作。

阶段 1 默认在预算触发时停止执行候选；报告有／无候选但不自动采用兜底 EndTurn。已有普通成功查询继续执行首动作。未来允许“预算用尽仍执行合法候选”应另声明策略；本阶段不借机改变策略质量比较。

### 5.2 票据与派发

扩展现有 `Ticket` 的结构化拒绝原因；仍须绑定 combat 对象／ID、完整 live/continuation、注册／输入身份和消费状态。给 Lab 控制的每个已完成原生动作递增边界序号，票据携带该序号，避免状态恰好回到相同 hash 时旧票据重新有效。序号只描述控制器观察到的生命周期，不替代完整 root 校验，也不声称追踪所有外部 UI 动作。

至少覆盖：已消费票据再次消费、同 combat 执行另一合法动作后消费旧票据、来源 combat 结束后消费、切到真实第二战后消费第一战票据。拒绝前后 root／continuation／RNG 不变，原生入队计数为 0；不能只断言异常字符串。正面控制是查询新边界后只派发一次，后续候选尾部仍丢弃。

消费后 native 调用若结果不确定，票据保持不可重用，worker 标记 OutcomeUncertain；记录 last-confirmed boundary。错误卡实例、目标或回合等派发前校验也应输出 InvalidAction，已有缺 handler 则保持 Unsupported，二者不混淆。通用 ChoiceRequestId 与生成对象 lineage 留到阶段 2。

## 6. 验收矩阵

所有“必需”项必须在最终 manifest 中逐项列出，不能只给一个检查总数。重复不增加独立样本量。带 fault/control 的 case 明示来源。

| ID | 场景／证据类型 | 必需断言与结果 | 重复／说明 |
| --- | --- | --- | --- |
| F01 | 输入／源码／构建快照 | 15 个 pin、游戏／bootstrap锁、Lab dirty 内容、上游源码／构建配方和产物可对应；更改输入副本的控制被 preflight 拒绝 | 一次正式快照；篡改仅用测试副本，旧 pin 不动 |
| O01 | 结果协议合同控制 | 全部分类可序列化／导入；不能把 Unknown 当 Death、prefix exact 当完成；旧报告不丢信息 | Core checks，包含非法组合拒绝 |
| D01 | 真实敌方致死 | 实际 enemy action、两端 Death、终态 root/continuation exact、parent 不变、正确原生清理、无胜利奖励／节点 | 2 个独立进程；native-flow 或明确 API 夹具 |
| D02 | 真实 Reverse upkeep 致死 | 经过敌方阶段到 upkeep；实际伤害致死、来源属性撤销、无死亡后 orb grant／Play、终态差分和清理 | 2 个独立进程；明确 API 夹具 |
| D03 | 原生死亡后非零资源清理 | 至少一个 D01/D02 含实际建立的非零 Recording/Echo；证明 native hook／退订和资源处理，未发生的 hook 不伪造 | 可复用 D01/D02，不额外杜撰独立执行 |
| V01 | 既有 player-action Victory | 16 步固定战斗基准、实际 CombatWon、终态前差分、清理后契约、安全 guard | 保留原 122 项检查及其具体范围 |
| V02 | 敌方／回合边界 Victory | 用已支持原版持续伤害等最小 API 夹具，真实 EndTurn 内结束战斗；没有下一 Play 仍正确处理 | 2 次；具体效果先核对已有 handler，不能自动新增卡机制 |
| B01 | 控制器决策／回合预算 | 各自到边界停止；BudgetExceeded/Ongoing，未发生下一查询或原生动作；guard 原值不被改写 | 每项 2 次，使用明确短上限 |
| B02 | 查询前 elapsed guard | 阈值前允许、达到／超过后拒绝新查询；原生状态不变 | 确定性时钟合同控制；与真实时间测量分栏 |
| B03 | 实际 solver 预算且有候选 | 真实受限求解，actual policy 与停止 telemetry 证明限制触发；有合法候选但按本阶段政策不执行，源不变 | 2 次，优先节点限；无法触发应报告未满足，不能伪造结果 |
| B04 | 预算无候选／无预算空结果 | 分类与不派发的合同控制各一项；真实端自然可达时另补证据 | 若用注入结果必须标 contract-control；不宣称已验证 solver 自然空结果 |
| A01 | duplicate／stale／ended／cross-combat ticket | 精确 InvalidAction code；拒绝时 roots/RNG/队列不变；新票据合法控制正常 | 每个拒绝路径 2 次或随重复两战复用 |
| A02 | 非法卡／目标／turn 前置 | 派发前 InvalidAction；无原生副作用；不误报 Unsupported | 受控坏动作，不修改真实状态 |
| C01 | ManaShield 独立数值契约 | 普通／升级 × Mana0/1/2，Block增量 5/10/13，能量-1、Mana失败不扣／成功-1；actual upgrade API；parent 隔离 | 原六场景各 2 次；明确 API 夹具 |
| C02 | 独立检查器负面控制 | 给检查器的记录副本错一个 Block／Mana 数值，原生与 sim 副本即使同时错也被独立表拒绝；错 continuation 可被差分检查发现 | injected-fault，仅验证检查器，不称原生变异执行 |
| X01 | Lab 执行异常控制 | 指定 Lab 边界主动抛出确定异常，EngineError、阶段／last-confirmed 边界可重放；禁止用对 game method 打坏补丁制造异常 | 2 次；原生派发前与结果未知分类的合同控制分开 |
| X02 | 差分错误控制 | 对报告／sim 捕获测试副本引入差异，首个不同字段和 action index 可见，不能降级 unsupported | 2 次；不改真实 pin 或原生对象 |
| X03 | 自有 worker timeout／取消控制 | 专用 Lab 测试 worker 在声明 barrier 等待；watchdog 留证并只结束自己创建的进程；标 Interrupted/Unknown | 控制测试，不故意卡死真实游戏动作 |
| U01 | 固定三种子 cohort | 全 12 路径保留；既有两张卡的 4 条 unsupported 仍在，正常路径无新差分；两次执行模式清楚 | generated/frozen 均留证，不要求新增死亡或全胜 |
| R01 | 冻结 Slimed 失败路线 | 实际到达并打出生成的 Slimed，不能只重算一条绕过该动作的新路线 | 原 seed2 skip 两次 |
| R02 | 现有最小 loop／两战／在线回归 | 24／41 项基准所覆盖合同、原两次9查询与冻结模式、Record/Echo生命周期；安全计数按事件解释 | 同最终产物；检查数改变须列出映射 |
| R03 | 原版与旧版本回归 | 原版 Victory/RNG/M3，既有旧 Eleanor 功能；各版本独立进程和 pin | 不将旧 Mod 输入当 v0.4.1；说明 artifact/profile |
| S01 | 审计和宿主隔离回归 | manifest/assembly/method不符、已有／后加patch、注册改变、root/fork不变及 MemorySaveStore/SaveProgress/replay；actual weave 与元数据控制分开 | 保留既有正负测，不以“未写盘”布尔标签替代证据 |

V02 不要求加入新的机制支持。先从已有原版 handler 选择最小可达效果；若必须扩新卡／新 power 才能建立该路径，列出具体缺口并停报该项。D01/D02 为阶段必需，不以控制测试替代。B04/X01/X02/X03 的控制证据也不替代实际故障的未来覆盖。

独立 ManaShield 契约文件应绑定版本／DLL哈希和审查依据，期望值不能从 DynamicVars、mirror 或这次实际输出生成。DynamicVars 仍输出用于定位；原生与契约冲突时保留冲突，不自动更新 golden。本阶段只加这些必要断言，不引入无界性质／全卡变异测试项目。

## 7. 构建冻结、统一回归与失败重放

### 7.1 两次冻结的用途

**开发前快照：** 记录当前 19 文件、路线图、HEAD、dirty patch／内容哈希和既有报告索引；它用于保护工作与还原出发点，不代表最终可执行产物。原始资料留在 ignored `.build`，公开文档不含个人路径、授权材料或游戏内容。

**最终候选快照：** 功能与针对性测试稳定后，以唯一 buildId 冻结 Lab源码、全部实际参与编译的 upstream源／项目文件、overlay、Core、fixture、driver、SDK／runtime、game／Mod／依赖／bootstrap输入锁。保存修改前上游内容和应用后的 source manifest，以及 harness、CombatSolver、Core、deps/runtimeconfig 和实际加载依赖的哈希。版本字符串和文件时间戳只能辅助，不能替代内容校验。

最终 buildId 的 canonical manifest 排除自身 hash 和运行输出，避免自引用；Lab执行代码和测试声明改变即产生新 buildId，报告汇总文字变更另外记录。运行前后复核产物哈希，各 case 使用不同输出目录与新进程。

### 7.2 同一个最终构建的具体要求

本阶段新增用例、v0.4.1 单战／loop／两战／矩阵／三种子／在线模式都必须指向同一份最终 harness、CombatSolver 和 Core 二进制。driver 接受显式 `HarnessRoot`／manifest，执行时不自动重建；构建完成后才串行启动 suite。某个用例失败后改代码，剩余结果不能混入原 buildId，修复后须对新最终产物重新跑必需矩阵。

为了覆盖原版，优先在最终 harness 配方中加入已有 `VanillaBattleReplayProbe`、`VanillaRandomTargetProbe`、`NativeRunLoopProbe` 等现成入口（实际文件名以源码为准），通过互斥 profile 关闭 Mod 加载；保持旧行为和原版断言。旧 Eleanor 如能通过已有 bootstrap 选择对应 pin，也应使用同一最终配方和独立进程。这里只允许整合现成入口、避免重复 overlay 的小型构建改动，不改变游戏语义。

若旧版 profile 确需独立二进制，则其结果作为**明确独立的兼容回归**列出 artifact hash，不能宣称与主 harness 同二进制。本阶段 v0.4.1 主矩阵的同构建门槛仍必须满足；若连主矩阵也只能靠随测随编译不同产物完成，应报告阻塞，不以“同一个 commit”替代要求。原版必须有本次源码下的真实回归证据，不能只引用历史日志。

Core checks 的可执行程序本来独立，列入同一次构建的 artifact set，并确认其引用的 Core hash 与主 harness 相同。不要为满足字面“一个 DLL”把无游戏检查改成依赖游戏。

### 7.3 失败工件

每条失败至少保留：case declaration、输入／build manifest、setup API trace、冻结动作、首个拒绝／分歧、两端 before/after及最后确认 continuation、RNG、parent proof、native event timeline、异常堆栈／阶段、安全事件、子进程 exit和完整stderr。报告指向真实证据文件并带哈希。

固定动作失败使用相同输入与动作重放，比较稳定 reasonCode、action index 和状态差分；不要靠重新求解绕过失败。墙钟／watchdog 的精确毫秒不作为确定性承诺，其分类和未继续派发才是合同。部分执行的 case 不从中间猜测恢复；新进程重放已确认前缀，明确原故障是否再次出现。

## 8. 实施工单与依赖顺序

| 工单 | 具体修改和交付物 | 前置与完成门槛 |
| --- | --- | --- |
| W0 保护与构建清单 | 快照当前工作区／证据；补完整 source/build/input manifest；分离 build 与 run，小型整合已有 probe入口 | 不改旧 pin／报告；能识别构建过期和输入不符；先不全套重跑 |
| W1 结果模型 | Core outcome/schema 校验、harness envelope、顶层退出码与错误映射、预期负测登记 | O01 通过；所有终止分支落盘且无 silent pass；旧 JSON 仍可用 |
| W2 终态执行与捕获 | 泛化只读 terminal observer；EndTurn 同时处理 terminal 与 next-turn；原生 Death 调用链审查；D01/D02/V02 | 依赖 W1；终态每步差分／parent isolation；清理独立验证，无手工 reset；任何新增宿主依赖精确评估 |
| W3 预算与票据 | 有界配置、实际 budget telemetry、控制器限制、拒绝 reason code／边界序号和派发前验证 | 依赖 W1；B/A项通过；不改变默认策略或用隐藏兜底动作 |
| W4 独立契约与负面控制 | 独立 ManaShield 表、C/X控制、日志／重放 importer；保留actual weave vs元数据区别 | 可在 W2/W3 阻塞时独立推进；注入控制不得被统计成 native 功能覆盖 |
| W5 冻结候选与统一回归 | 固定最终构建；串行执行完整manifest矩阵、旧基准和固定失败路线；逐 case校验输入与artifact | W2–W4针对性门通过后开始；任何运行代码修复使候选作废并生成新buildId |
| W6 文档收尾 | 新增 `STAGE_1_VALIDATION.md`，更新 MILESTONE_STATUS/PROBE_PROVENANCE/VALIDATION并链接执行入口 | 交付最终目录、命令、精确哈希、每项结果／exit、known gaps、未提交文件列表；不提交推送，不扩下一阶段 |

W2 若只需利用现有 Defeat 模型和局部观察器，应在已授权 Lab 范围实施；若必须重设计后端 terminal/continuation 的关键架构，先交付接口差异、最小复现和替代方案，暂停该依赖。W3 同样不能借预算测试改搜索算法。每个工单完成留一个短状态：已经运行的项、正在修复的项、未运行和具体阻塞；长 suite 记录当前 case／worker，不重复启动。

建议未来统一入口形态如下，**目前尚不存在，不能当作现有命令执行**：

```text
build-stage-one.ps1 -> immutable artifact manifest
run-stage-one.ps1 -BuildManifest <manifest> -CaseManifest <cases> -OutputDirectory <fresh Lab run>
run-stage-one.ps1 -BuildManifest <same manifest> -ReplayCase <retained case bundle>
```

实现可复用现有脚本命名，但最终文档必须给出真实可执行命令和完整参数，不留下占位符式“已验证”。

## 9. 交接与停止边界

本轮新增本方案并给 README 增加长期路线图、阶段 1 方案和最近证据入口；长期路线图内容保持不变。未修改运行代码、未运行测试、未提交推送。下一轮编码应先检查实际状态，再按 W0→W1→W2/W3/W4→W5→W6 推进；已有授权无需再次确认。

仍需在实施时核验、不能预先保证的事项：原生死亡到 teardown/run-loss 的具体方法链及 headless依赖；低预算是否能在允许参数内实际触发并保留候选；现成原版 probe能否以小型配方合并使用；死亡是否自然调用角色 End，以及清理时序与订阅是否符合契约。这些是有界静态审查／最小探针任务，不是允许放松门槛的理由。

停止对应动作的条件：输入／注册审计不符、需放宽安全规则或新增未授权宿主访问、真实持久化后端被启用、动作结果未知且没有可核查边界、需修改角色源码或扩展阶段 2 能力。保留日志和最后确认状态，完成不依赖阻塞的工单，向父线程给出动作、目标、错误、是否已执行及最小所需协调；不绕过拒绝。
