# 4D3 胜利、奖励与跨幕修复的独立复审

结论：**未通过；剩余一项必修：胜利条件观察与实际胜利回调的根动作归属没有闭合。** R2 的跨幕完整状态与新 MapRoom 绑定、R3 的有限 MealTicket 领取契约已通过本轮独立控制。真实原生执行事实仍成立，本结论针对生产证据检查器的误接受。

冻结 `B1FB0F818977EF57C6CF77014F33FC2695D4C56261C3685BF28F50D453917DDD`，Native DLL SHA256 `B24C179B91E5EB222BF8A383B26D6D958E11A016719111DE413F1EB21E44B9C0`。独立证据位于 `.build/runs/n3d3-preparation-repair-independent-review01/`；以其中 `closeout.json`、`reader-summary.json`、`linked/reader-summary.json`、`linked-elite/reader-summary.json`、`verification.json` 为准。

## 必修 R1：旧动作的条件观察可替代本次动作的必需观察

位置：`tools/native-run/NativeMajorContract.cs:186`–`194`，以及 `dispatch` 初始化。处理 `major-victory-condition` 时检查该记录属于当前正在执行的动作，随后保存完整 `victoryState`、历史 epoch/index 和 `victoryAnchored`。但没有保存条件记录的 root/context，也没有在下一根动作派发时使旧条件锚点失效。`battle-victory-before-cleanup` 只检查快照相等和该布尔值，再把 `combatRoot` 设置成本次回调所属动作。

独立控制把目标胜利的所有原生条件记录移到紧邻的前一个战斗动作确认之前，并将记录 root/context 改成那个动作；保留原始条件 facts 和完整 state。当前胜利回调、其后清场/奖励/跨幕，以及所有其他 payload 完全未修改。重算副本的序号/hash/seal 后，实际调用同一冻结生产检查器：

| 证据副本 | 条件记录归属 | 实际胜利回调 | 期望 / 实际退出 |
| --- | --- | --- | --- |
| `linked/case-001`，Boss minimal | a181，3 条条件记录 | a182；本动作条件记录为 0 | 1 / **0** |
| `linked/case-002`，Boss off | a181，3 条条件记录 | a182；本动作条件记录为 0 | 1 / **0** |
| `linked-elite/case-001`，Elite minimal | a111，5 条条件记录 | a112；本动作条件记录为 0 | 1 / **0** |
| `linked-elite/case-002`，Elite off | a111，5 条条件记录 | a112；本动作条件记录为 0 | 1 / **0** |

两个 detailed 对照 `linked/case-000` 和 `linked-elite/case-000` 返回 1，拒绝原因是 `Major victory predicate current native history cursor`。这证明 detailed 模式的游标检查能挡住本反例，minimal/off 仍会借用旧动作锚点。四个误接受不是坏 JSON、坏 seal 或游戏执行失败；生产读取器生成了通过结果。

最小修复：保存条件锚点所属的不可变 root/context，并在回调和关联的清理证据中要求同一本次动作；新动作派发时旧锚点不得继续满足本次胜利必需条件。同一动作中原生条件函数多次返回 true 可以合法更新锚点，不应错误计作多个胜利。不要恢复“最后一条历史必须等于胜利状态”的旧错误假设，也不要要求详细历史开启，或删去 minimal/off 的认证。

将四个误接受和两个 detailed 对照纳入生产回归；补同动作多次条件观察的合法正例、跨动作挪用与缺本次条件的拒绝，保留实际条件事实检查、历史清理游标、战后治疗和错误前缀。此次不需要改变原生业务、角色参数或扩大权限。

## 已闭合的修复与原生证据

原 34 个反例在当前读取器全部拒绝。当前格式的对应破坏也经过生产检查器，避免仅因旧格式缺少新增记录而获得假阴性。存活/敌人终态的联动控制同时修改条件观察和回调，在 detailed/minimal/off 均拒绝 HP0/负值、活敌人、残留敌人列表、已停止战斗及矛盾原生条件 facts。

跨幕入口以不可变 dispatch 推导真实 ready 记账变化；完成以入口完整状态推导新幕 MapRoom 框架。联动修改完成至末尾的 act/node/floor/房间栈/资源/RNG/嵌套字段、把新房改指旧房、篡改登记类型或提前登记均被拒绝。真实进入第二幕 MapRoom 的合法快照通过；没有把之后可能存在的 AfterActEntered 效果都宣称为不变。

MealTicket 取得后的新引用、登记 type/model、提供内容与实际 relics 项相互绑定；联动篡改 HP/RNG/额外字段、重复遗物、移除旧遗物、假成功及指向已持有实例均拒绝。固定版本 MealTicket 没有拾取效果，它的治疗 Hook 在进入 MerchantRoom 时执行；本次是 CombatRoom 领取。当前 Major 只认证 MealTicket，其他遗物在真实领取前明确 Unsupported；不能把此有限契约称为全部遗物支持，后续通用 run 仍须扩充真实取得 Hook 的契约。

实际原生成功路径仍为明确 API 夹具：Elite DEV03 在 5 场合法准备战斗后取得目标胜利，113/113 动作、35 Gold、PommelStrike、新 MealTicket，HP12→18；Boss DEV03 在 9 场准备战斗后取得目标胜利，184/184 动作、100 Gold、DarkEmbrace，HP11→17，原生 ready/EnterNextAct 任务完成至 act1/floor0/node null/MapRoom/mapGeneration2。目标均通过显式 EnterRoomDebug 进入，不是自然 Boss 路线。另两个 Boss 准备样本真实 Death（93/156 动作）和自然 Elite 样本 Death（77 动作）保留。

只读代码核实：IsCombatEnding postfix 不修改返回值；真实 CheckWinCondition 再调用 EndCombatInternal。NativeLifecycle 等待原 Task 并重抛异常；准备路径等待真实 ActTask。领奖调用原生 SelectLocalReward，未手动追加金币/牌/遗物。

## 独立核验范围

生产检查器实际调用 **467 次**：42 个合法/等价样本通过，421 个坏/不完整样本拒绝，**4 个坏副本误接受**。主读取包 461 次全部符合预期，两个 linked 包 6 次暴露上述四项失败；不能用主包的绿色汇总关闭整个审查。主包复验原 34、修复方当前格式控制、历史反例、合法样本和迁移；新增独立控制共 65 次，覆盖三个修复组和旧动作借用。均保留构造脚本、原始来源锁、reseal 日志与生产读取器输出。

冻结与执行核对 1688 项通过：82 源文件、49 编译输入副本、3 游戏输入、7 产物及 dirty 源码快照哈希；323 个实际原生执行 / 4074 项 suite 检查、独立 Core98 的实际日志与产物身份。原始退出码分别为 240×0、70×1、8×2、5×3，失败和预算前缀没有改称成功。13 个最终任务及原 final01 七个格式失败均有保留记录。

格式迁移实际重读七组：旧合法证据在原 FAEC 读取器退出 0，在新读取器因缺少必需条件锚点退出 1，当前格式替代正例退出 0。旧读取器 DLL 哈希为 `3752953ADF7580AC301646D3478B5EE4DA23D0A992773C7E14D9330E34583E0C`。没有改旧证据或削弱新增必需证据门。

15 个锁定输入、52 个兼容源码及最终冻结文件未变；3011 份历史锁中的变化仅为本次已声明六个源文件修复，其余保留。冻结重放、minimal/off 与 detailed 的路线、选择、奖励选择、完整最终状态（含全部 RNG）结构比较一致；五个基础样本与上一冻结的这些原生结果也一致。本次只运行证据读取器，没有重启 323 条原生执行。

## 后续

先完成上述唯一归属修复、同构建回归与独立复验，再接 `NATIVE_RUN_N3D3_PREPARATION_NEXT_HANDOFF.md` 的自然 Boss、下一幕首节点/Ancient 和通用 run 集成。当前未证明自然完整通关、全部默认 Acts 的 RunVictory、通用遗物 Hook 或全部 Boss 终止形式；M1 仍未完成。

复现时先复制上述 case 的四份输入文件到新的 Lab `.build/runs/` 目录，再执行冻结 DLL 的 `--review <新副本目录>`，避免覆盖已有审查副本的 `trace-review.json`。构造脚本为本审查目录的 `linked/review-linked.py` 与 `linked-elite/review-linked-elite.py`。

本轮只新增独立审查资料和本文档，未修改生产实现或原始证据，未启动 UI、真实 Steam/网络/用户存档、Mod 或安装变更，未提交推送。没有执行环境或授权阻塞；阻断项是上述可信数据验收缺口。
