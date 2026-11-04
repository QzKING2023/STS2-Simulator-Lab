# 4D2 独立对抗审查

结论：**未通过，需要修复生产数据检查器的 R1–R4。** 本轮确认了已执行的原生正常路径，没有发现这些成功样本使用伪造胜利、手工资源或外层 Task 返回替代真实 Resume。阻断点是检查器仍接受不可能的时序、相互矛盾的选择记录及缺失的生命周期证据，因而不能据此关闭“可信数据”验收。

审查冻结：`12F0036561A3AF6AA801BB2D894EE5B3FFBED04D4246E8DECC1DFB54CD545004`。Native DLL SHA256：`65F71CE91ABBE29B24E0C4EC23A51F535EB943982B6CAA05A1B37D53A109F99F`。

独立证据目录：`.build/runs/n3d2-independent-review01/`，以 `closeout.json` / `summary.json` 为汇总。没有修改生产源码、冻结产物、已有证据或正式路线图，没有提交或推送。

## 已核实的事实

- 独立重算 build ID，并核对 72 个源输入、46 个编译输入、3 个游戏输入、7 个产物、72 份源码快照。15 个固定 Mod 输入和 52 个兼容源码仍匹配原锁。
- 9 组汇总对应 290 个原生子进程、3540 项套件检查。逐进程核对实际 process/result/header/build-manifest、退出状态和隔离记录，退出分布为 214×0、68×1、4×2、4×3；预期失败、Unsupported、预算停止仍在分母内。Core98 是已保存的同构建执行证据，本轮没有重新运行 Core 或原生游戏流程。
- 本轮 537 项独立来源、退出和重放核对通过。1026 次旧证据 reader 调用的记录与冻结一致；4020 个历史原文件重新计算哈希一致。不是声称本轮又执行了全部历史 reader。
- event-natural 的旧 77 步前缀与此前证据逐项一致，延续至 Hollow 原生 Gold/两瓶药水，再合法进入下一战 Play，80/80。event-combat 为明确的 API 夹具：真实前序奖励牌/药水、Dummy V1 原生击杀、原生空战后奖励集合、实际 Resume 的 Ashwater 奖励、合法地图继续到下一战 Play，62/62。二者不能拼称正常开局完整通关。
- live/replay/minimal/off 的 route、choices、reward-choices、final-state 完整 JSON 相等，包括记录的 RNG。minimal/off 仍保留必需身份及边界投影，不等于完全移除观察器。
- `NativeEventFlow.Track` 等待原始 `BattlewornDummy.Resume` Task；执行器另外等待该 Task，外层返回单独记录。固定 DLL 的 Resume 在设置事件完成后 `await RewardsCmd.OfferCustom`，因此 IsFinished=true 本身不证明任务完成。
- 故障和预算是明确的“原生业务完成后”包装层控制，实际记录 EngineError/exit1 与 BudgetExceeded/exit3，均保留 60 确认/61 派发、未确认 Proceed 和已经发生的奖励。它们不是原生游戏缺陷或天然异步延迟复现。
- 复制并实际用最终生产 reader 重读原有自然 Death 13/13、自然多节点 Death 138/138、两条新路径的 frozen replay/off，均通过；两份失败/预算记录不能被 reader 认证为完整成功。真实死亡证据来自原有自然路径，没有向不攻击的 Dummy 人造死亡。
- 新增 Resume 方法的已保存实际 Harmony 审计 API 探针包含事前/事后未知补丁拒绝，目标业务调用数为 0；该探针不能算事件业务执行。

## 实际生产检查器反例

本轮调用最终 DLL 的 `--review` 共 **56 次**，全部在新的证据副本上运行：10 个正常/等价副本全部通过；46 个预期拒绝副本中 28 个拒绝、18 个仍返回 exit0。18 个中有 **16 个明确必修反例**，另 2 个作为非阻断说明。每份改动副本都重新生成合法 seal/计数，再调用生产 reader；没有通过无效 hash 链制造“拒绝成功”。

原始 packet 的哈希锁见 `original-lock-final.json`，原文件保持不变。`controls.json`、`followup-controls.json`、`additional-controls.json` 包含逐项 raw exit 和日志位置。最初两个过早完成控制因 dangling identity 被拒；后续控制保留合法身份，暴露真正时序遗漏，不能用前两项的拒绝代替时序验收。

### R1：Resume 完成没有绑定其奖励子任务完成

位置：`tools/native-run/NativeEventFlowContract.cs:87`、`:88`，与 `NativeRewardContract` 的操作栈协同处。

| 反例目录 | 改动 | 实际结果 |
| --- | --- | --- |
| `follow-00` | 将 ef-resume-return 移至 r15 药水子操作返回之前；身份均已登记 | exit0 |
| `follow-01` | 将 ef-resume-return 移至 r14 奖励集合完成之前 | exit0 |
| `follow-02` | 外层 ef-room-resume-return 移至真实 ef-resume-start 之前，快照使用当时完整 Proceed 状态 | exit0 |

根因：真实完成只要求 start 已存在、事实一致及 IsFinished=true；没有要求其拥有的原生奖励会话已返回。外层返回只要求 Proceed 已开始，没有要求具体 Resume 已被调用。

最小修复：把具体 Resume ID、父事件、退出子房间、根动作及其奖励子会话关联起来。真实完成必须发生在其所有必需子任务/奖励集合完成之后，且与最后业务返回的完整状态相符。外层返回必须晚于真实 Resume start；**允许它早于真实任务完成**，保留合法异步情形，不错误强制实际完成必须早于外层返回。故障/预算仍须保留已发生副作用和未确认前缀。

### R2：事件选择的根决定、实际执行和返回事实可互相矛盾

位置：`NativeEventFlowContract.cs:56`、`:62` 及 option boundary。

| 反例目录 | 改动 | 实际结果 |
| --- | --- | --- |
| `case-07` | a47 的 root-request、dispatch、confirmed 一致改为 Setting2；实际 start 仍是 Setting1 | exit0 |
| `case-08` | Hollow 返回和 boundary 都声称 eventFinished=false，但实际完整状态为 true | exit0 |
| `follow-03` | a47 的 room-option-return 改挂 a1/wrong context | exit0 |

根因：候选只与 room-option-start 事实匹配，未完整绑定 dispatch 决定的 session/request/index/key/具体 option；返回检查使用内部 active/ctx，却不验证本条记录的 root/context。两个完成事实只互相比对，未核对实际状态。

最小修复：一个不可变选择事务贯穿候选→根决定→开始→原生返回→确认→boundary，绑定当前请求、实际房间及选项实例，并检查每条记录自己的动作归属。按实际事件转换契约核对完成语义，不能用两份相同错误值自证。固定未支持 Setting2/3 的声明必须与实际根选择一致。

### R3：切换事件检查器后，战斗清理和到达记录的必需性/归属退化

位置：`NativeTraceContract.cs:58` 的分派、`NativeEventFlowContract.cs:36`、`:47`、`:78`、`:90`。

| 反例目录 | 改动 | 实际结果 |
| --- | --- | --- |
| `case-09/10/11` | 单独改 a59 battle-victory-cleanup 的金币、完整 RNG 内的值或新增嵌套字段 | 均 exit0 |
| `case-12` | 删除该战斗清理记录 | exit0 |
| `case-13` | 清理记录改挂错误 root/context | exit0 |
| `victory-callback-wrong-root` | 真实战斗胜利观察记录改挂 a1/wrong room | exit0 |
| `case-18` | 删除 a62 下一房间 room-entered | exit0 |
| `case-19` | 为同一 a62 到达新增另一 session 的重复 room-entered | exit0 |

根因：新 profile 替换原 room contract，prefix 仅在 natural 的 enable 前使用；新合约对胜利只累加计数，对 cleanup 未执行等价约束；arrival 仅保证 session 字符串不重复，未保证“每次实际到达必需且唯一”。既有旧场景的负控仍通过，不代表新 profile 自动继承同一保护。

最小修复：复用或明确迁移当前范围内已经成立的房间/战斗事务约束，绑定具体战斗实例、触发根动作、胜利→确认→清理→奖励顺序，以及每次到达对应的新房间。清理后的静止观察应与本次不可变确认完整状态比较，未知新增字段同样参与；不要只加金币/RNG字段特判。原生清理前 HP71→清理后77 的真实回血必须保留，不能把真实业务区间误判静止。下一房间到达以实际 Travel/房间身份为键而不是任意新 session 字符串，完成目标应要求这条到达证据存在。

### R4：EventRewards 可以遗漏整个原生集合，包装返回还能虚构状态变化

位置：`NativeRewardContract.cs:152` 及 EventRewards 的父子约束。

| 反例目录 | 改动 | 实际结果 |
| --- | --- | --- |
| `case-14` | 删除 a60/r12 整个原生空 reward-set 的所有 n3c 记录，保留外层 EventRewards | exit0 |
| `follow-06` | 外层 r11 返回、a60 confirmed 和 boundary 同时金币+9；实际子集合返回仍是原金币，下一动作又恢复 | exit0 |

根因：EventRewards 被列入合法 root/父类型，但没有继承 RewardOffer 的必需唯一 reward-set 及子返回完整状态约束。泛化的“某处至少出现一个奖励集合”不能证明本次战后原生奖励已执行。

最小修复：对当前实际固定流程，要求本次 EventRewards 有且只有原生返回的集合；**空集合也是完整原生结果，不能省略记录**。集合归属本次子战斗及动作，集合完成后包装没有额外业务变化，完整返回状态必须绑定最后子返回。事件生命周期在进入 Proceed 前应确认该阶段收尾。未来确有原生无集合分支时另设可证明的显式契约，不能用当前漏洞兼容。

## 非阻断说明与保留边界

`case-20` 删除 fixture-setup-declared 仍通过，header/provenance 仍明确 api-fixture，故未造成自然路径误标；`follow-10` 改变自由描述 source 文本仍通过。建议后续给夹具声明必需性和说明字段的非权威性质写清契约，本轮不把这两项算作新的业务支持门。

正常快照全局增加一致的未知结构字段、属性重排、真实清理与获得奖励均被接受。共同篡改某些 Resume 子树后恢复、缺少 Resume start/return、错父奖励集合、缺少 option return、错下一房间引用以及插入故障的成功报告也被正确拒绝。修复必须保留这些已成立的保护，不能通过删除新字段或压缩日志规避反例。

本轮没有启动游戏 UI、真实 Steam/网络或用户存档操作。所有新增文件限独立审查目录和本文；HEAD 保持 `d73039d688eb54c9513f5b9508a03947e689aa18`，无审批或执行阻塞。

## 接续条件

先修复 R1–R4，再以新冻结源码/产物复跑本轮 16 个必修反例、合法等价、已有事件控制及全部适用 1–4D2 回归。保留当前成功原始证据和本轮失败副本，不能把修复后结果写回旧冻结。

4D3 交接文件 `NATIVE_RUN_N3D3_IMPLEMENTATION_HANDOFF.md` 继续可用，但本轮不签发进入实施的验收通过。修复独审通过后依次推进原生 Elite/Boss、实际战后奖励/act-ready，再步骤5正常开局同 Run 跨幕与真正 RunVictory。已披露的累计 BattleVictories 等待信号须在通用多战控制器前改为当前房间/战斗实例边界；尾部夹具及早死都不能代替自然完整流程。步骤6预登记批测和可信数据交付仍待执行。
