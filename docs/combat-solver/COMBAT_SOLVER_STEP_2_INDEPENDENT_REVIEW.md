# CombatSolver 第 2 步综合独立验收

2026-11-04。**通过已声明的第 2 步有限动作接入验收，可以进入第 3 步单战终态闭环。本轮没有发现新的必修缺陷。** 此结论覆盖类型化动作部署、必要手牌耗尽子选择、药水主动采用和逐步核对/再求解；不表示全部选择、全部牌效或整场已经支持。M1 仍未完成。

审查冻结：`12F1E1BFF786FBBD5DD29BF882BD1C743949AD82E31A464F7A9AF221F6A6312F`。独立证据：`.build/runs/solver-step2-independent-review01/`。本次新增原生游戏执行 **0**，没有编译或修改生产实现。

## 验收依据和通过范围

按前次普通动作验收后的顺序，必要门是关闭动作/实例绑定缺陷、实际子选择、实际药水部署，并形成在线“求解一步→原生执行→完整核对→重新求解”与重放。终态是下一步，不能把未覆盖的所有原版组合无限追加到本步。

| 能力 | 独立确认的实际证据 | 覆盖边界 |
| --- | --- | --- |
| 普通牌、目标攻击、EndTurn | `solver-adapter-exec-03/04`：自然首战，Defend、两张指定 Strike、敌方动作和下一玩家 Play；完整重放 | 4 个求解器动作，7 个总根动作；一个敌人、非终态 |
| 在线子选择 | `solver-online-exec-01/02`：求解器主动选 TrueGrit+ 和 Wound；原生耗尽 i29、i30 保留；+9 格挡/-1 能量；真实再查询，完整重放 | Action / Exhaust / Hand，单个选择；实际 index=0，两个 occurrence 都为0 |
| 非首项选择部署 | `solver-choice-potion-exec-04`：明确固定 API 动作，真实选择 index=1 的 Defend i31 | 验证同一 token/原生 selector 桥；不能称在线求解器主动选非首项或非零重复 occurrence |
| 药水主动采用 | `solver-smart-exec-04` 与 `solver-smart-replay-exec-01`：Smart 协调器主动选择 EnergyPotion，原生能量0→2、HP80不变、i29离槽；精确差分及再次求解 | 上游测试派生 API 夹具、Oracle；下一候选 Strike 未执行，敌人 HP 仍1，无战斗胜利 |
| 敌方目标药水 | `solver-choice-potion-exec-04`：FirePotion 原生20伤害、实际移槽 | 固定 API 动作；没有在线主动采用及该整条固定路径的成功重放 |
| 失败和预算 | 两个旧 Block 查询 NodeLimit；两个 Smart Block 到8动作上限；旧 Energy 自目标检查错误 | 原退出分别3/3、3/3、1，未改写为成功；原生确认前缀和完整失败重放保留 |

五对 live/replay 的完整 route、choices、最终状态、已执行 step 文件和每次 query 的状态、continuation、原始计划相等，包括已记录 run/player/event RNG。没有将重放同一输入解释成独立策略样本。成功 Energy 路径的两次执行确为相同 final02；其他组合保留各自 producer，不称全部由 final02 重新产生。

## 动作和选择契约

`NativeSolverActionContract.cs` 与运行时复用执行字段门。牌由模型、升级、CardStateKey、模型 occurrence 与状态 occurrence 定位实际手牌；目标由 index 与 CombatId 相互核对；药水由槽、模型与当前持有实例关联真实派发。前五个动作绑定反例与随后四个子选择/药水身份反例，本轮均用最终生产 reader 重新执行并拒绝。

`NativeSolverLoop.cs:62` 明确拒绝不支持字段，包含嵌套集合及非零前置嵌套数量；TurnStartChoices、额外 EndTurn 选择、ReplayCount 等由共享字段门拒绝。原生目标和 CanPlay/IsValidTarget/可用性仍检查。Energy 自目标修复只在传入目标为 null 时用玩家验证；实际入队仍传 null，显式敌人目标不会被替换成玩家。

`NativeSolverChoices.cs:20` 仅接受 Action/Exhaust/Hand 和指定来源；原生候选由真实对象、升级、key、两个 occurrence 解析。每个请求按 root/context 和游标消费；缺少、剩余、多余请求和过期/错误 token 会拒绝。`NativeChoices.cs:64` 先调用此 provider；激活时不会因 solver 答复不合法而退回选第一项，隐式选择/冻结答复也须与 solver 决策一致。

这里的“嵌套”须精确：已支持一张根牌产生一个子选择，**未支持 PlanAction.NestedChoices、连续子选择或回合开始选择**。生产 reader 本轮拒绝嵌套/回合开始/错误来源/错误 context/空或重复 token 等受损副本；旧15项托管 selector 控制证明零额外派发及来源不变。后者不是实际晚到选择失败的游戏任务实证。

真正未消费旧票据在每次原生动作后拒绝，消费前复制、旧/新 sequence、Used 状态和零额外派发均有代码与记录对应。捕获/阴影/实际查询绑定当前确认 root/context、完整状态和 continuation；不依赖单个 `exact=true`。未消费标志错误、缺少再次捕获、完整 RNG 矛盾以及共同改写 Oracle 标签，本次生产 reader 均拒绝。

## 搜索、隔离与失败边界

在线耗尽例使用原直接 beam；成功 Energy 例使用固定上游 `CombatSearchCoordinator.Solve`，Smart、无强制 directives，实际 Profile 为 beam8、每成员256节点、10000ms、15秒取消、DOP1。查询记录的有效 Profile 与初始基础 `solver-profile.json` 是不同层，不能把它们说成完全相同默认值。累计4096节点是**返回结果拒绝采用门**，不是第4096节点精确停止；另有合作取消与120秒拥有进程硬门。

本轮重核执行 ledger 的次数/截止时间/容量和原始退出；没有重新做挂起或进程树超时注入，沿用原有明确版本的证据。快速成功不证明取消成功。原生异常、Unsupported、InvalidAction、BudgetExceeded、DataError 有不同出口；当前真实未完成记录均被成功证据检查器拒绝，不能因验证程序退出0就改称游戏成功。

Energy 的旧 `solver-smart-exec-03` 是 Lab 错误拒绝合法自目标，记录为 InvalidAction、4/4 setup、无 UsePotion 派发。新执行修复了接口；旧失败仍在。NodeLimit 和8动作上限案例是未完成结果，不是死亡，不是证明 BlockPotion 不兼容。

所有12份被读原始记录均保留 MemorySaveStore、ShouldSave=false、Steam未初始化、单机网络服务、无游戏 UI、零遥测/异步故障，未加载 MyMod/BaseLib。默认原生配置仍拒绝求解器依赖；独立 solver 配置精确核验程序集路径/名称/哈希，未调用完整 Entry.Initialize、设置 Load/Save 或额外搜索补丁。宿主 patch inventory 与 solver root 审计未放宽。本轮没有重跑默认宿主游戏流程。

## 冻结和独立检查

- `facts.json`：169 项事实核对，全部通过。直接核对12份原始 process/result/trace、实际效果、失败前缀、五组重放、依赖和旧证据锁；数量不等于169场游戏或独立牌效。
- `controls.json`：34 次最终生产 `--review` 调用，8个正常/等价接受、26个受损或未完成记录拒绝，零意外结果。其中9个为原审反例。只操作明确标记的副本，不改原始文件。
- `build-ids.json`：四个相关 producer ID 按实际 manifest 算法重算一致。最终952个 source、77个 compiled、15个 artifact、3个游戏输入哈希一致；每份被审执行的 producer artifacts 均一致。
- 固定 CombatSolver 0.50.1 Lab 衍生版：826源文件、6程序集、MIT许可/manifest及原producer manifest一致。游戏为v0.111.0，15锁定输入及52兼容源码一致。MIT/Torch1230/hotwords123归属保留，不扩大游戏/Godot/Ritsu的分发许可。
- Smart、在线选择和最新药水重放三批原始/work锁全部保持。哈希证明文件一致；执行结论另基于受控process、真实派发/历史、独立窄规则与重放。

审查工具初次读取旧循环格式时误以为 step 自带 root，随后改用该旧格式的实际派发顺序与 query 关联；仅本轮审查脚本修正。一次 PowerShell stdin 形式没有产生 build-id 输出，检查文件不存在后用独立脚本完成重算。均无原生游戏执行、原始证据改写或权限拒绝，不作为产品缺陷/通过项。

## 明确未覆盖和第 3 步起点

非零重复实例实际采用、同模型不同状态、在线非首项、连续/回合开始/生成卡子选择、药水槽替换/多个药水/药水子选择/自动用药、多个敌人组合、自动出牌及复活均未完成认证。晚到非法选择发生在真实牌效前缀之后的完整错误收尾，仍缺专门原生实证。可以在后续受影响场景首次使用前补最小验证；不应宣称这些全都支持，也无需本轮穷举所有组合。

**第 3 步首要工作是终态契约，而非策略调分。** 当前 `NativeSolverLoop.cs:95,99` 对 EndTurn 非普通边界及预测终态明确 Unsupported，因此执行器尚不能结束任何整场战斗。这是已知后续能力边界，本次没有把它算胜利或隐藏。

建议按下列顺序实施下一有界批次：

1. 复用现有 terminal stamp、原生胜利/死亡观察和不可变上下文。定义“模拟终态→实际原生动作→清理前终态核对→原生清理/奖励”的分界；清理后状态不能冒充同一个战斗 continuation，终态后不再捕获普通可行动 root。
2. 以正常 Ironclad 首战作为第一条完整战斗 live/replay，继续使用通用求解器和明确预算；真实 Death、预算或 Unsupported 都保留。先证明胜利/死亡与奖励生命周期后，再接回 M1 自然多幕入口，不先回到 TestSubject 调评分。
3. 若需额外夹具验证死亡或晚到非法选择，单列 API origin，记录实际派发/确认计数和最后确认前缀；不拼接成自然完整战斗。新使用的子选择或多目标范围在派发前有能力门。
4. 建议一次登记上限4次原生执行（含重放/失败）、20分钟、2GiB及每进程120秒；按生成链影响重跑定向场景，检查器变化复验冻结原始数据。父状态/RNG污染、差分不一致、取消不安全或额度到限立即收口；不自动加预算。最终M1同构建全量门仍保留。

这些是可继续执行的下一阶段入口，不是第3步已经完成。第2步通过可以准确对用户表述为：**求解器的有界行动桥已通过综合验收，普通牌/回合/必要子选择/药水均有真实执行证据，在线选择和成功药水重放成立；开始补整场终态和自然流程接回。** 没有胜率、全局最优、完整Boss或RunVictory声明。

本轮只新增本审查报告和独立审查产物；未改生产实现、游戏/角色/用户存档或其他项目，未启动游戏 UI/真实 Steam 网络，无提交推送。无环境或审批阻塞。
