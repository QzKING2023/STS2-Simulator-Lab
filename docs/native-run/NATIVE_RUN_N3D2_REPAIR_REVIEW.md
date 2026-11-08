# 4D2 修复后的独立复审

结论：**未通过，剩余两类生产状态绑定必修项。** 原 R1–R4 的 16 个具体反例均已闭合；正常原生结果、动作归属、清理/到达必需性和任务完成顺序得到保留。新的联动反例显示，奖励子集合入口和任务尚未完成时的外层返回仍缺完整状态约束。不能仅凭原 16 例通过关闭可信数据验收。

审查冻结：`C4B85E2FDDADA7418E49A1A73A6F91256195707415128D3038461B08CF1A563C`。Native DLL SHA256：`98A95310C940354C369E610F50A20F5B0F60A6C2340CBE187D14334B91A5DAC5`。

证据目录：`.build/runs/n3d2-repair-independent-review01/`，入口 `closeout.json` / `summary.json`；逐次生产调用见 `controls.json` 和各副本的 `reader.log`。本轮只写独立审查证据及本文，没有修改生产实现、旧证据或冻结产物，没有运行原生游戏流程、提交或推送。根目录及相关祖先目录未发现 AGENTS.md 或 .agents 指引。

## 已核实的修复与原始执行

- 重新计算冻结 ID；当前 72 源输入、46 编译输入、3 游戏输入、7 产物、72 源码副本全部匹配。相对旧冻结只有两个生产检查器和 `event_flow_controls.py` 三个文件改变，原生执行器和宿主隔离未改。15 个固定输入、52 个兼容源码匹配原锁。
- 独立执行同一生产 DLL 的 `--review` **59 次**，仅处理新的隔离副本。17 个正常/等价副本全部接受；42 个预期拒绝副本中 33 个拒绝、9 个误接受。原 16 个反例全部按语义门拒绝，不依赖坏 seal 或未登记身份。旧 10 个正常/等价包、新冻结四个正常 live/replay 包及三个合法等价控制均通过。
- 9 组已保存的最终构建执行证据对应 **290 个原生进程、3586 项套件检查**；原退出分布 214×0、68×1、4×2、4×3。逐进程 manifest/header/process/result、来源分类、退出及内存宿主隔离一致。**537 项**来源/结果/重放核对无失败。Core98 是保存的实际执行日志，相关 Core 与检查程序产物哈希另行核对；本轮未重跑 Core 或原生矩阵。
- 152 个当前事件控制记录符合原预期（147 拒绝、5 等价接受）；1026 个历史 reader 调用记录及 4020 个历史原文件哈希核对一致。本轮没有把这些保存记录计为新调用。
- 自然片段 80/80，固定 Dummy V1 API 夹具 62/62；live/replay/minimal/off 的 route、choices、reward-choices、final-state 完整 JSON 相等，包含记录 RNG。自然旧 77 动作前缀保持。minimal/off 仍保留必需边界投影，不能称零观察器等价。
- 实际源码 `NativeEventFlow.cs:26` 等待原始 Resume Task；`NativeEventFlow.cs:64` 等待外层 Proceed 后，`:67` 再等实际任务；`MainLoopContext.RunUntilCompleted` 通过 `GetAwaiter().GetResult()` 传播异常。固定游戏 DLL 的实际反编译再次确认 `BattlewornDummy.Resume` 先完成事件、生成奖励，再 `await OfferCustom`；IsFinished 不能单独代表任务完成。
- 故障/预算的真实保存证据仍为 EngineError/exit1 和 BudgetExceeded/exit3，均 60 确认/61 派发，保留原生已发生奖励，未确认 Proceed。新 reader 也拒绝将这两包认证为成功。它们是原生业务完成后的观察包装控制，不是原生异常/天然异步延迟案例。
- 原生清理 HP71→77，以及 Resume 生成奖励时玩家 Rewards RNG 的合法变化，正常包均通过。当前自然/夹具来源、真实死亡旧包、错误前缀与有限支持边界没有被本轮修复削弱。

## 必修 R1：任务待完成的外层返回状态未绑定

位置：`tools/native-run/NativeEventFlowContract.cs:124`。现在只在 `resumeReturned == true` 时比较外层返回与任务完成状态；当外层返回早于真实任务完成时，除一般结构/身份校验外，整个 state 没有与本次执行阶段绑定。

以实现者已允许的合法顺序为基底：将 `ef-room-resume-return` 放在 `ef-resume-start` 之后、奖励集合入口之前，取完整实际开始状态。该合法等价副本 `new-32` 返回 exit0。再分别仅改该返回状态：

| 证据副本 | 改动 | 生产 reader |
| --- | --- | --- |
| `new-33` | gold 110→119，前后实际状态仍110 | exit0 |
| `new-34` | runRng.Niche.s0 翻转1位，其余记录保留 | exit0 |
| `new-35` | 仅该快照新增嵌套状态字段 | exit0 |

这些记录可以污染中间数据，最终结果和任务完成包保持原样。它们不是实际业务新增结果，也不能用“异步任务可能变化”解释固定任务中无来源的金币/Niche RNG变化。

最小修复：给尚未完成的任务维护具体阶段及不可变状态锚点，外层返回绑定当前已知业务阶段的完整状态或严格定义的合法转换。允许真实外层先返回；不要强制它一定晚于任务完成，也不要跳过整个快照的检查。若业务正在执行且现有记录不足以证明其状态，补足最窄业务边界观测/契约。必须保留合法待完成返回控制，拒绝这三个单点矛盾及同类未知嵌套字段。

## 必修 R2：奖励子集合入口没有连接父业务状态，整条子树可共同篡改

位置：`tools/native-run/NativeRewardContract.cs:101`–`:110`，及实际 Resume 开始与 reward-set 入口的协同。父返回和确认已绑定最新子返回，解决了旧反例；但 EventRewards / EventProceed 的 reward-set **入口**仍不绑定其来源状态。子树从错误入口出发、一路保持自洽，尾部比较全部成立，下一根动作又恢复原状态。

| 证据副本 | 联动改动 | 生产 reader |
| --- | --- | --- |
| `new-36/37/38` | a60/r12 原生空集合从 open 至包装 return、confirmed、boundary 一起改变 gold / Niche RNG / 新增嵌套字段；r11 start 与下一根 dispatch 保持原值 | 全部 exit0 |
| `new-39/40/41` | a61/r14 Resume 奖励集合从 open 至实际 Resume return、父返回、confirmed、boundary、leave-ready 一起做同类改动；实际 Resume start 及下一 Travel dispatch 保持原值 | 全部 exit0 |

它们没有删掉必需集合、没改选择来源、没破坏 seal，也没有引入未知对象引用。`new-36` 的具体矛盾是原生空奖励集合凭空让金币110→119，下一动作再回110；`new-39` 在只有真实药水生成/取得的区间产生同样错误。

重新从锁定游戏 DLL 反编译的 `RewardsCmd.cs`、`BattlewornDummy.cs` 及逐状态差异保存于本审查目录，仅为本机研究证据，不加入发布内容。`actual-lifecycle-deltas.json` 记录当前实际结果：

- EventRewards r11 包装入口→r12 空集合入口：完整状态无变化。
- Dummy V1 实际 Resume start→r14 集合入口：只观察到事件 IsFinished、eventOptions，以及玩家 Rewards RNG 的真实变化。金币、Run Niche RNG、其余状态没有对应业务变化。
- 清理前后真实 HP 和牌堆/phase 变化保留；不能给所有父子区间强加相等。

最小修复：从父操作/具体实际任务开始到其集合入口建立可证明的完整状态转换，随后已有子集合→任务完成→父返回→确认链继续生效。当前原生空 EventRewards 应保留其实际无业务变化契约；Resume V1 则保留事件完成和原生奖励生成所需的真实 RNG/选择变化，对其余完整结构绑定。允许变化必须依据锁定原生方法和实际业务事实，不能从被测前后差异自动总结为任意允许值；也不能硬编码本种子的期待药水/金币。若未来奖励生成有其它 Hook，按明确范围验证或显式 unsupported，不用当前空档兼容未知变化。对下一根 dispatch 的确认状态连续性一并核对，可防止这种临时篡改后恢复，但仍需把子集合入口本身与正确父业务阶段连接，不能只修最终恢复点。

## 已闭合项与修复回归门

当前根选择 session/request/index/key/option、实际返回和 boundary 的归属，缺少/重复选择结果，战斗 cleanup 的必需性/完整状态，房间到达必需性/唯一性，EventRewards 子集合存在/唯一及实际 Resume 完成晚于其子集合返回等，本轮原反例和独立同类测试均拒绝。不要为剩余状态门重写或弱化这些约束。

本轮全部9个误接受副本见 `controls.json`；`review.py` 保留精确变换逻辑，历史源文件哈希在 `original-lock.json`。复现应复制对应目录到新的 Lab 审查输出，再调用冻结 DLL `--review`，因为 reader 会写 trace-review.json；不要覆盖本次或旧原始包。修复后需重新冻结，至少使原16及本轮9全部语义拒绝、所有17正常/等价继续接受，再执行适用统一回归。保留真实业务变化、RNG与失败前缀，不用删除数据字段获取通过。

## Boss 与完整 M1 接续

[N3D3 实施交接](NATIVE_RUN_N3D3_IMPLEMENTATION_HANDOFF.md) 的顺序继续可用：先按当前战斗/房间实例修正通用等待边界，再固定一个原生精英与第一幕 Boss 的明确夹具，验证真实 Victory/Death、奖励与 act-ready；随后步骤5同一正常开局 Run 自然跨幕至真正终态，步骤6预登记批测。累计 BattleVictories、旧 Architect 尾部夹具及早死都不能代替这些证据。

本轮不签发进入 Boss 实施的验收通过。由根线程安排上述两项窄修复、同构建回归及复审，再接续已授权 M1；不需要再向用户逐步请批。无权限拒绝或外部执行阻塞；一次只读 PowerShell 枚举无输出的会话已按会话正常取消，后续明确路径读取正常，未影响任何运行证据。
