# CombatSolver 第3步修复与第4步两战综合独审

2026-11-04。**第3步原终态 R1 的四个反例已闭合；第3、4步声明的真实执行和重放成立。综合可信数据验收还剩一项共享检查器小修：step 报告的 card/target 重复字段未绑定实际派发。** 原始正常证据没有该矛盾，不撤销真实胜利、领奖、Rage 出牌的事实。本轮没有新原生游戏执行、编译或生产代码修改。

独立证据：`.build/runs/solver-run-independent-review01/`。`facts.json` 有173项事实/一致性核对，全部通过；`controls.json` 和 `followup-controls.json` 合计30次实际生产 reader 调用：10个原始正常/结构等价副本接受，18个坏副本拒绝，2个下述重复字段坏副本误接受。计数是检查及读取次数，不是新游戏次数。所有控制只操作明确标记的派生副本，保留原结果和退出码；没有机械重跑原生矩阵。

## 已确认的范围

第3步：`solver-battle-exec-01/02` 正常开局首战13次求解/执行至原生 Victory，16个总根动作；`03/04` 明示 HP1、空手牌的 API 夹具中，求解器实际 EndTurn、敌方致死并完成原生 Loss 回调。两组完整重放一致。修复后的 `NativeSolverBattleContract.BindNativeTerminal` 要求正确回调唯一、同 root/context、采集→回调→确认顺序和完整结构状态相等；Death 另要求 LossAfter 顺序。四份原误接受副本均由最终 reader 实际拒绝。

胜利清理前HP73，原生 BurningBlood 后79、phase=None、历史清空；死亡HP0，原生 Losses 增加，保留 PlayerCombatState 和历史。没有把胜利治疗与清理后的状态当清理前差分，也没有宣称死亡路径执行 AfterCombatEnd、零售 RunLoss 最终收尾或同进程 worker 复用。

第4步的四份原始执行均为同一生产构建、同一固定种子 `LAB_V0111_NATIVE_DEV_01`、正常 Neow 开局，没有 FixtureSetup 或人工改资源：

| 原始目录（`.build/runs/`） | 实际范围 | 完整重放 |
| --- | --- | --- |
| `solver-run-exec-01/02` | 同 Run 首战 Victory→领取17金币→跳过剩余奖励→合法下一地图节点→第二战 Victory；23个求解动作，29个确认根动作 | route/choices/reward-choices/final-state、全部step、query状态/动作/binding/continuation/Profile一致 |
| `solver-run-exec-03/04` | 同 Run 首战 Victory→17金币及原生首张卡奖励 Rage→合法下一节点→第二战实际使用 Rage 并 Victory；25个求解动作，31个确认根动作 | 同上 |

skip 指领取金币后跳过余下奖励，不是跳过金币。每条路径只有一次 RewardOffer、一次 ProceedRewards、一次跨战 NativeTravel；两场各一次终态采集、原生胜利回调和清理。奖励来自 `RewardsCmd.OfferForRoomEnd`、原生集合/卡选择和领取任务；永久牌组没有由 Lab 手动追加。

take 的永久 `CARD.RAGE` 为 i37、升级0；第二战实际副本 i44 的 DeckVersion 指向 i37。query71/a26 的实际原生 PlayCard 派发、求解 PlanAction、真实手牌及状态共同证明该牌被打出，随后继续重新求解。不是仅有预测动作或只看最后牌数。

金币249→266、永久牌组10→11（skip保持10）；HP、金币、完整牌组/升级/引用、遗物、药水继承至下一战。第二战由真实新 CombatState 初始化为 turn1/Play、energy3、block0、powers空、HP79。最终两路HP80。旧未消费票据跨战拒绝、零额外派发、父 fork 和原生来源/RNG不扰动有记录。

全部动作的局部 continuation、定义内完整快照和 run/player/event RNG 与 replay一致；每步确认后实际重新求解。顶层仍为 `Ongoing / SliceBoundary / NotCompared`：奖励、地图及战后清理由原生业务与数据契约验证，没有冒称独立仿真。没有第二战奖励、第三节点、Boss 或 RunVictory 的本批覆盖。

## 剩余必修 R1：步骤报告的实例/目标重复字段

位置：`tools/native-run/NativeSolverBattleContract.cs:57`。这里调用 `NativeSolverActionContract.Bind`，正确核对了 `step.action`、query、真实 dispatch.decision；随后比较完整前后状态，却未核对同一 step 中另行输出的 `card`、`target`。生产输出来自 `NativeSolverLoop.cs:126`，这些字段也被验收脚本用于关联取得卡与实际使用，不能在通过报告内互相矛盾。

两个有限反例都只改一个 step JSON 字段，不改动作、状态、RNG、trace 或整个执行：

| 派生副本名 | 唯一改动 | 实际reader结果 |
| --- | --- | --- |
| `rage-step-wrong-real-instance` | `solver-online-step-71.json.card`：真实 Rage i44→另一张 Strike i45 | 错误exit0；query71与原生a26仍指i44 |
| `attack-step-wrong-target` | `solver-online-step-65.json.target`：真实敌人i52→手牌i44 | 错误exit0；PlanAction与实际a20仍指i52 |

定位用的对照 `rage-actual-dispatch-wrong-instance` 把实际a26的dispatch.card改为i45，正确exit1。这表明真实动作绑定有效，缺口限重复步骤元数据，不能据此说原生曾打出错误卡或攻击手牌。

最小修复：在共享步骤验证中，把 `step.card/target` 绑定已经通过身份验证的实际 dispatch.decision。PlayCard要求确切card和nullable target；UsePotion的card应为null、target与实际派发一致；EndTurn两者应为null。保留JSON类型及null语义，避免空串默认值掩盖缺字段。可提取窄共享助手供相关step reader使用；不改搜索、游戏业务或重新设计动作协议。

修后对这两个反例、实际dispatch错配对照、8份正常原始副本及合法null/等价字段顺序做定向检查即可。只改reader时，冻结新reader、重读现有证据，保留原producer；不需要重跑四次原生游戏或完整矩阵。第3步原终态R1无需重做实现；综合验收待这项数据一致性修复后关闭。

其他本轮控制已拒绝：第二战回调借用首战root/context、缺回调、重复清理、重复金币返回、奖励错父动作、整个卡选择子会话缺失、旧卡冒充取得实例、错误永久卡连续性、下一战新增状态字段/旧序列、错误地图目标、实际步骤RNG变化。正常治疗、抽牌/RNG变化、原生清场及结构等价副本通过。发现该单一根因后停止扩充控制。

## 构建、来源和边界

- 第3步原生产构建：`AAACB551E949A74ED8DBA202B8C91DB71AA8CB3535C6F0A48A811570BA0CA17A`；修复reader：`AD0506AE6E0E76F47DC5A7915B4D0C6C1229720A6E18B6F19601561B1E33B0E0`，编译源码仅 `NativeSolverBattleContract.cs` 改动。
- 第4步四次原生producer：`6D57D5954F46BC3E728D43B66C7C660D16C4A54D2F0D1F72CE3557C2DECDA984`。
- 本次实际reader：`FE6232717EFE8C7F5E0B2B41DBE5C5FEEF94CE2623C5BA7FA534CAF25A97365F`。相对第4步producer仅 `NativeRewardContract.cs`、`NativeSolverRunContract.cs` 两份编译源不同；执行/采集链相同。修复分别允许已声明Closed转换及正确理解Travel源context，未减少完整状态字段。

四个构建ID独立重算相符；当前reader source、各构建compiled/input/artifacts、15输入/52兼容源码、solver826源码/6程序集和MIT许可/manifest均核对。原始及历史证据锁保持。哈希证明一致性，实际执行另由原始process、动作、生命周期、历史与完整重放支持。

第4步预登记4次/20分钟/2GiB，单进程120秒、每战64动作/查询、总160根动作；4次原生用时合计40.84秒，均exit0，在门限内，没有第五次。原两次reader对合法数据的误拒保留在原工作包，不混为原生失败。此次没有新增超时、64步耗尽或Unsupported实测，旧失败和预算边界继续保留。

原始记录确认 MemorySaveStore、ShouldSave=false、Steam未初始化、单机服务、无UI/遥测/异步故障，未加载MyMod/BaseLib；第4步SaveProgress/replay拦截每次6/2。没有修改用户存档、安装、其他项目或提交推送。来源/信息模式为明确的natural或API fixture与Oracle；不是玩家可见信息策略强度证明。

第2步子选择/药水限制仍在：仅已声明的Action/Exhaust/Hand主选择及有限药水证据，未认证任意嵌套/连续/回合开始选择、复活、所有牌/遗物或多敌人组合。最终M1仍需自然完整RunVictory和最终同构建全量门，不能由本批两战代替。

RandomForeseer只完成本机0.13.19源码、MIT许可、接口和依赖初查，未编译、加载、预测调用或原生对照。`RANDOM_FORESEER_REUSE_NOTE.md`的已知Hook/UI依赖和“下一幕读取已生成状态”界限准确；版本声明匹配不等于兼容通过。关闭本小修后，可将完整battle单元接入既有原生节点控制器，按预登记样本/预算推进；若需要战斗外预测，先复用窄奖励预测入口做不扰动及原生候选对照，不能让它代替原生执行或阻断M1。Mod专项继续后置。
