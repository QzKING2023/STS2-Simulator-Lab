# M1 固定样本：原生事件、常量意图与第二批验收

2026-11-04。**本批有实质推进，但 M1 整体仍未通过。** 不提交推送，不启动性能或策略里程碑。前一批原始失败和版本证据保留，未替换固定种子。

## 固定构建与执行边界

- 最终宿主：`4AAE93CA76DA304CA19EB2B7F0DCE26A3FCFF25884779A5DA8DFCF5DB6D0BF83`，清单 `.build/native-build/m1-acceptance-b02-final06/build-manifest.json`。
- CombatSolver 0.50.1 的 Lab 衍生 `constant-intent.1`，DLL SHA256 `B6A3AE711C0CA2338BD6BF0710128FED47278D5BE34D2ABCFD4F00A3392A87CC`。以已锁定 `possessed-root.1` 为基础，保留 MIT 和上游归属；只修改上游 `IntentForecaster` 的常量构造来源识别。旧依赖及原版游戏程序集未改。
- 本批目录 `.build/runs/m1-acceptance-work02/`；`plan.json` 在执行前登记 18 次原生、45 分钟、4 GiB。实际 18 次原生，最后一次结束于 15:43:49 UTC，距登记约 34 分钟，逻辑证据约 3.25 GiB。
- 共 19 次包装器调用，其中一次在创建输出目录、启动原生进程前因夹具不允许 480 秒全局运行参数而拒绝。原日志保留；纠正为 120 秒后只运行一次夹具。未增加原生额度。
- 游戏、策略搜索档位、节点预算和线程配置保持既有固定值。全局 1200 动作 / 768 查询 / 450 秒 / 512 MiB trace，搜索 beam 8 / node 256 / DOP 2。原整战遗留的 64 查询限制改为使用已经登记的全局查询预算，修改前 BudgetExceeded 原样保留。
- 1155 项源码、编译副本、输入与输出哈希匹配；15 个 Eleanor 输入及 52 个兼容源码未变。Core 98 检查通过，使用的 Core DLL 与最终宿主 Core DLL 哈希相同。

## 原生修复及实际覆盖

1. **VineShambler 不是放宽近似门。** 原版 `SingleAttackIntent(int)` 用闭包保存常量，上游 `DamageCalc.Target != null` 将它误判为动态。新识别要求精确原版类型、构造器、回调方法和闭包类型来源，且无未知 Harmony 补丁。任意动态委托仍拒绝精确认证，不通过调用委托猜测常量，不按怪物名特判。
   - 每次初始化运行 3 正 / 3 负控制，包含真实 Harmony 注入后拒绝、解除后恢复；这是审计 API 控制，业务派发为零。
   - 最终构建 EVAL02 / 04 / 06 分别有 21 / 37 / 9 个 VineShambler 战斗动作：forecastExact、原生差分和父 fork 隔离均通过。原始对应 query、step 和根动作保留在 `targeted-runtime-evidence.json`。
2. **区分隐式空选择和显式空计划。** 没有上游选择时，仅原生隐式、空候选、min=0 可以返回空；上游已经产生空 `PlanCardChoice` 时必须实际消费。后者修复 NeowsFury 空弃牌堆的未消费计划错误；原 InvalidAction 保留。最终 EVAL02 有 3 个显式空选择实证。
3. **原生事件复用。** 审计并复用 BrainLeech、Wellspring、TheLanternKey、Bugslayer、SapphireSeed 的继承空入口和原版选项。无重写事件效果；SapphireSeed 复用原生牌组附魔候选过滤和选择叶子。原 TestMode 的展示空对象返回是已有行为，不增加展示绕过。
4. **事件子战斗。** TheLanternKey 单选项遵循实际选项集合，原生进入 MysteriousKnight 子战斗，正常执行战斗和奖励；只有原版明确不恢复父事件的分支可继续。需要 Resume 的其他分支没有伪造完成。
5. **先古持久遗物。** 复用原版 `AncientEventModel.RelicOption` 回调和已有默认拾取来源校验。最终 EVAL01 实际取得 Vakuu 的 WhisperingEarring；EVAL06 实际取得 Pael 的 PaelsWing，之后继续原生战斗。
   - `HasUponPickupEffect=false` 不是“无拾取效果”的证明。中间构建 Orobas 的 TouchOfOrobas 实际替换初始遗物，触发 DataError；保留原始状态。最终构建要求真正的基类 AfterObtained 来源，因此这类特殊拾取在派发前 Unsupported。没有把替换事实当成普通新增。

这批新事件/先古执行有原生完整状态和选择记录，**尚需补齐 generic Ancient 与 event-child 的独立生产生命周期契约及负控**。当前完整文件检查器通过不等于这些新契约已单独验收。

## 六个预登记样本的最新状态

种子仍是 `LAB_V0111_NATIVE_EVAL_01` 至 `06`；均为 PrefixCount=0 的自然开局、在线通用 Coordinator，Oracle 信息模式。未删失败分母，未改为成功种子。

| 样本 | 构建目录后缀 | 确认 / 派发 | 实际范围与停止 |
|---|---|---:|---|
| 01 | b02-final06 | 630 / 631 | 前两幕 Boss 胜利、奖励及跨幕；第三幕 Vakuu 后继续。WarHistorianRepy 入口 Unsupported |
| 02 | b02-final06 | 213 / 213 | 第一幕 Boss 胜利和奖励；Pael 三选项均不在默认拾取能力内，Unsupported |
| 03 | b02-final06 | 207 / 207 | 第一幕 Boss 胜利和奖励；Orobas 特殊拾取仍 Unsupported |
| 04 | b02-final06 | 350 / 351 | 第一幕通过，第二幕继续；宝箱实际金币受修正，不满足旧未修正金币契约，Unsupported |
| 05 | b02-final04 | 58 / 58 | Stratagem 的 PlayerTurnStart / AfterShuffle 选择传输尚不支持；保留原查询，未派发下一动作 |
| 06 | b02-final06 | 358 / 359 | 第一幕通过，Pael 拾取后第二幕继续；CrystalSphere 入口 Unsupported |

第五份仍是较早构建，**不能拼为同构建六样本矩阵**。这些样本都没有达到真实终态，不能以 Unsupported 的正确退出代替正常队列完成。

## 当前构建回归

- 两个新进程沿同一完整冻结路线执行，detailed / minimal 均 **499/499 原生动作、RunVictory、退出 0**。都是冻结动作重放，不是当前构建两次独立在线自然通关。
- 每次 393 个执行步的完整动作、前后状态、RNG、预测/实际及父 fork 不变一致；499 个确认边界完整状态一致。detailed 有 7615 个状态观察，minimal 有 4055 个；差额仅为 minimal 不采集的 3560 个 combat-history 事件，所有共同事件的完整状态一致。没有删状态字段比较。
- 每次 SaveProgress 73、replay 22 次离线拦截；MemorySaveStore，faults 空、真实保存关闭、Steam 未初始化、UI 未创建。顶层 Comparison=NotCompared 保留原生报告语义，战斗差分由 393 份 step 单独证明。
- 当前构建 TrueGrit 非首项选择与 FirePotion 夹具两步差分通过，15 项选择拒绝控制通过；夹具不计为自然奖励或在线求解器主动选药水。
- 5 份当前固定样本前缀及 2 份完整重放通过现有生产 full-run 检查器；7 项计划控制、全局预算边界控制通过。
- 旧版本终态正常控制在新检查器被精确依赖绑定拒绝，**不给任何终态负控通过信用**。用新构建原生末端动作直接调用同一编译生产 `NativeFullRunTerminal.Review`：正常通过，原 4 反例在状态/归属/必需记录对应门拒绝。该单元入口与完整文件审查分列。
- 比较脚本首次错误地按工作目录解析清单内相对产物路径；失败报告保留在 `final-comparison-path-error.json`。修正解析基准后 21 项比较通过；没有更改产物或原始执行。

主要证据：`closeout.json`、`final-comparison.json`、`production-review-summary.json`、`terminal-unit-results.json`、`targeted-runtime-evidence.json`、`plan-controls-final06.json`、`core-check-receipt.json`、各代 `evaluation-*-ledger.json`。

## 顺带采集的性能和策略资料

只整理本来必需运行已有的 process / timing / query / route / trace；没有 profiler、策略调整或额外采样运行。`telemetry-summary.json` 区分构建、自然在线、冻结重放和夹具，并保留实际结果、动作、幕/节点、查询统计、成员工作量、分配量、峰值工作集、策略轨迹路径。CPU 时间和磁盘 I/O 未采集，标未知。

例如当前自然 EVAL01：125.53 秒、547 个查询、记录的 searchElapsed 合计 59.36 秒、峰值工作集约 1.79 GiB，终态未完成。两份冻结重放分别 116.29 / 114.99 秒，仍各运行 393 次求解校验，不能将它们与在线墙钟相减得到搜索耗时。原生等待、采集和序列化计时可能嵌套；成员时间可能重叠，也不是进程 CPU 时间。部分证据检查与重放重叠，不能据此作隔离性能基准或胜率结论。

## 下一批依赖与验收门

见 [后续交接](M1_ACCEPTANCE_EVENT_INTENT_NEXT_HANDOFF.md)。先完成本批通用边界，再固定当前六种子实际终态与同构建重放，最后完成原计划各类回归及数据查询门。M1 通过后才执行已授权整体提交推送。
