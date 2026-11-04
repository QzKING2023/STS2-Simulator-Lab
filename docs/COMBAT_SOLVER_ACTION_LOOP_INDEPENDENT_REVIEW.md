# CombatSolver 修复与普通动作闭环独立审查

2026-11-04。请求冻结：`35C6C189D99FCA0D9ABC162A6BE9EA8DF156170EC49791A58E170F090F91796D`。

## 结论与阶段判断

**前次三项必修已关闭；五步计划第 1 步的固定依赖、隔离宿主及最小捕获/执行验收通过。第 2 步只有普通动作片段成立，尚不能宣布完成。** 本轮发现一组新的、范围明确的动作证据绑定缺口，修复后再认证扩展动作协议。没有发现实际原生动作错误或父状态/RNG 污染。

已证实真实 Defend、两次目标 Strike、EndTurn、敌方动作、下一玩家回合及再次求解。未证实必要子选择、药水、同模型非零 occurrence 实际选用、多目标、自动出牌、复活和战斗终态。没有整场胜利、BossVictory、RunVictory 或 M1 完成。本审查没有新增原生游戏执行。

## 前次三项修复

| 原问题 | 独立核验 | 结论 |
| --- | --- | --- |
| 用已消费复制品冒充未消费旧票据 | `NativeSolverGate.cs:93`、`NativeSolverLoop.cs:94` 都在消费前复制票据；检查 `Used=false`，正常原生执行后旧票据拒绝且仍未消费，派发计数不增加。单步 sequence 3→4；循环逐步 3→4、4→5、5→6、6→7，与实际确认动作一致。重复消费单列。 | 已关闭 |
| 顶层 Oracle 标签及文件契约 | header/profile/capture 均为 Oracle；文件必需、版本和初始化标志检查保留。原缺文件/伪标签反例拒绝，新控制同时改写所有模式标签也拒绝。 | 已关闭 |
| capture 归属、必需记录和完成摘要 | 单步三个角色；循环九个 query/shadow 捕获点，绑定当前最后确认 root/context、完整静止状态和 continuation hash。缺失、重复、错角色/hash/归属及矛盾完成摘要拒绝。 | 已关闭 |

原审五个误接受模式在新 v2 正常数据上的对应副本全部 exit 1；共重验实现轮 32 个控制，8 个正常/等价接受、24 个错误拒绝。这里复验的是相同反例变换，不靠旧 v1 文件缺字段触发一个无关错误。旧记录未改写，也未升级成 v2 认证。

## 实际闭环及重放

`solver-adapter-exec-03` 为正常开局的 live 片段，`04` 为同构建 frozen replay。两者完整原生根动作共 7 个，包含以下 4 个求解器战斗动作：

| 原生动作 | 独立读取的真实变化 |
| --- | --- |
| Defend，i15 | 能量 3→2，格挡 0→5 |
| Strike，i17，目标 i26 | 能量 2→1，敌人 HP 43→37 |
| Strike，i19，目标 i26 | 能量 1→0，敌人 HP 37→31 |
| EndTurn | 原生敌方攻击 12，实际格挡 5/未格挡 7，玩家 HP 80→73；进入第 2 回合 Play/Player、3 能量、实际抽 5 张牌 |

已交叉检查手牌实例、升级、真实派发、伤害与抽牌历史。普通 Defend 的 1 费/5 格挡和 Strike 的 1 费/6 伤害是独立常量断言，不由求解器估值产生。战斗历史中 EndTurn 的 `DamageReceivedEntry`、`CreatureAttackedEntry`、`MonsterPerformedMoveEntry` 和五个 `CardDrawnEntry` 均属于 a7。

四步完整 continuation 均相等，阴影执行未改变另一父 fork 或原生根；每次捕获/求解前后原生投影和 RNG 不变。这里的完整状态包含序列化 run/player/event RNG，战斗 continuation 另含战斗 RNG 内部状态。捕获纯度依赖可信采集代码中的实际比较，不代表所有未观测宿主内存均已证明无扰动。

五次查询展开 96/85/77/61/57 节点，均未耗尽预算。第五次从真实第二回合状态重求解，但未执行其返回动作。live/replay 的 route、choices、query、step、final-state 及全部派发/确认 payload 结构相同。重放仍重新求解，并受已冻结原生动作/状态检查约束，不是脱离求解器的纯 PlanAction 播放器。

## 第 2 步必修 R1：求解动作与实际派发未完整绑定

位置：`tools/native-run/NativeSolverLoopContract.cs:40`、`:43`、`:47`。

检查器要求 `step.action == query.actions[0]`，然后只核对 Kind、CardId/model 及另外保存的 Lab card/target。它没有把所采用 PlanAction 的 occurrence、StateKey 和目标描述绑定到实际选中的原生对象，也没有执行与运行时相同的动作字段门。

本轮五份有限反例只同步改写 query 与 step 的重复动作字段，保留原始 trace、route、choices、完整状态、continuation、身份表及结果。它们均被请求冻结的生产 `--review` **误接受，exit 0**：

| 控制名 | 修改 | 与原生证据的矛盾 |
| --- | --- | --- |
| `ind-q2-target-combatid` | q2/step2 TargetCombatId=99999 | 原生目标为 i26；原 root 敌人 CombatId=1 |
| `ind-q2-target-index` | TargetIndex=99999 | 原生只有一个敌人，实际 index=0 |
| `ind-q2-card-occurrence` | CardOccurrence=1 | 当前同模型 occurrence 1 是 i19，实际派发 i17 |
| `ind-q2-card-statekey` | CardStateKey=`IMPOSSIBLE_STATE_KEY` | 不对应真实原生手牌；其余原始状态和 continuation 未变 |
| `ind-q4-unsupported-card-field` | EndTurn 附加 CardId=STRIKE_IRONCLAD | 运行时 `ValidateLoopFields` 会拒绝该执行字段 |

这不是要求识破任意共同重写整套游戏证据，而是已声明的“实际采用求解器第一动作”的有限跨文件一致性。原生运行时在 `NativeSolverGate.cs:57,63` 与 `NativeSolverLoop.cs:20,26` 有更严格的字段/对象解析门，原始样本中的实际对象也正确，故上述问题不推翻四步业务实证或第 1 步隔离验收。

**最小修复范围：**

1. 复用一个不需要原生初始化的执行字段契约，供运行派发与证据读取共同检查；对当前不支持的非空字段明确拒绝。不要让两个检查器继续复制不等价规则。
2. 把采用动作的牌/目标选择绑定当前确认根及实际派发。模型、升级、手牌顺序/occurrence、目标索引应直接与现有状态交叉核对。StateKey 与 CombatId 使用已有可靠原生投影；若当前证据缺少可读取投影，增补最小原生映射观察并绑定同一 root，而不是只重复保存 PlanAction 自报值。不要为这个修复设计完整游戏语义解释器。
3. 五个反例全部拒绝，正常/等价和现有控制继续通过。仅检查器变化可重验冻结原始数据；若新增采集字段，只需一组受影响的普通 live/replay 小样本及明确新旧证据兼容边界，不需重跑旧完整原生矩阵。之后再推进必要子选择。

当前没有要求把未实现的子选择/药水改成成功；它们仍须在依赖派发前显式 Unsupported。实际预判到战斗终态也主动停止，目前这项限制会阻止整场完成，须在后续终态步骤解决。

## 依赖、隔离、版本与证据来源

- 请求冻结的 945 个 source、71 个 compiled、15 个 artifact 及 3 个外部输入哈希一致，并重算 manifest 得到相同完整 build ID。编译产物与执行 `process.json` 的身份一致。
- 单步修复的独立构建为 `A549BBFCEFF0464625DF0B6B704930F70F93B25A8E4E6B0B73BA294E053C920E`，对应 exec01/02。后续五个 live 源文件变化有显式记录，旧编译快照和产物保持，未把两阶段说成同一构建。
- 默认原生构建为 `8AD4C519CA298703FD0B22092CA314E5E284927FD18C2B85106F057947ACE4D8`；112 source/71 compiled/7 artifact 匹配。实际托管 API 控制证明默认配置拒绝 CombatSolver/MyMod/BaseLib/Ritsu，求解器配置拒绝 MyMod/BaseLib、拒绝错误 DLL 哈希。默认构建本轮没有新原生游戏运行，历史 24 动作证据仍按旧构建列明。
- 固定游戏 v0.111.0/41cef1ea，游戏 DLL SHA256 `0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9`。
- 固定 CombatSolver 0.50.1 **Lab 衍生版本**，DLL SHA256 `4503D5234915B4B0C163556C6FDB4FA0C38F45796F656AB4B2C7E76A1BF7D98B`。826 源快照、6 DLL、原生产构建 manifest、许可和 Mod manifest 哈希均核对。MIT/Torch1230/hotwords123 归属保留；游戏/Godot/Ritsu 没有因此获得新的分发许可。
- 默认宿主的隔离拒绝没有删除；求解器采用独立编译配置和精确依赖锁。没有调用 Entry.Initialize/配置 Load-Save、没有新增搜索补丁。宿主完整补丁清单不变门和求解器 root 审计继续工作。实际四条原生记录均为 MemorySaveStore、ShouldSave=false、Steam 未初始化、NetSingleplayerGameService、无 UI 实例/遥测/捕获故障，未加载 MyMod/BaseLib。
- 94 个原始执行文件、804 个实现工作证据文件哈希匹配；15 个锁定输入和 52 个兼容源码保持。哈希证明当前文件与固定来源一致，不单独证明执行真实性；真实性结论另基于受控 process、真实业务历史和采集/派发调用链。

这足以完成**当前第一步的依赖与隔离范围**，不意味着所有卡牌根捕获、任意补丁组合或默认构建的所有游戏流程都已验证。

## 审查成本与交接

独立证据：`.build/runs/solver-action-loop-independent-review01/`。

- `facts.json`：59 项核对通过；其中包含原验证器调用结果，不与其 113 项相加宣传独立覆盖。
- `existing-verification.json`：113 项原验证器复用通过，仅输出重定向，不覆盖原文件。
- `historical-controls.json`：32 次实际生产读取，全部符合预期。
- `independent-controls.json`：11 次实际生产读取；2 正常/等价接受，4 个模式/票据/capture/RNG 错误拒绝，5 个上述动作绑定错误误接受。
- `profile-api-controls.json`：8 项真实托管方法控制通过；不运行原生 bootstrap。
- `build-id-controls.json`：三个构建 ID 重算一致。
- `counterexample-bindings.json`：五个反例具体文件差异及未变原始 trace/状态证据。
- `closeout.json`：分阶段结论、计数与限制。

本轮新增原生执行 **0**。前实现批次仍为 4/4，28.123 秒原生累计、末次在登记约 378 秒后结束，低于 20 分钟/2 GiB；本次没有证明查询耗尽、合作取消或新进程终止注入。120 秒 helper 沿用既有验证，不能把快速结束算超时测试。

审查初次有一份派生副本未带重封工具要求的 `.copy-for-control` 标记，被工具本地守卫拒绝，未改原始文件、未运行 reader/游戏。该不完整副本保留；后续重新建立明确标记的副本完成控制。它是审查脚本的本地前置条件错误，不是权限审批拒绝或生产缺陷。

下一实施回合先闭合上述一组有限 R1，接着复用现有 NativeChoices 和 PlanAction 执行顺序接口做必要子选择，再推进真实终态。保留原生状态、RNG、确认前缀和 budget/unsupported 分类，不恢复单 Boss 评分微调。未修改运行代码、固定输入、角色、安装目录或用户存档；无 UI/Steam/网络调用，无提交推送。
