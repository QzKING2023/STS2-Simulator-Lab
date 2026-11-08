# 4D3 三项独审缺口修复

本轮修复 [独立审查](NATIVE_RUN_N3D3_REVIEW.md) 的 R1–R3。目标是可信数据检查器的完整状态、动作归属与夹具来源；精英/Boss胜利、其奖励及实际act-ready仍待后续真实执行。本文件不关闭整个4D3或M1。

最终冻结：`004B16F2E83E94C9CD546E0A4082B412259209CE53849C01548E01AC35AA8ECC`，目录`.build/native-build/n3d3-repair-final01/`。原冻结及审查副本保持；本次只修改两个生产检查器，新增一个reader控制脚本，没有改变原生执行器、宿主补丁、策略、seed或Encounter选择。

## 修复契约

| 审查项 | 生产修复 |
| --- | --- |
| R1 死亡完整状态 | 保存不可变ProcessPendingLoss输入和完成状态；详细模式的输入绑定当前动作最新原生history完整状态，输入要求当前房间/Encounter一致且HP0、战斗尚在进行。固定版本该阶段仅停止战斗、增加Losses；完整输入推导完成快照，包含未知嵌套字段。随后根confirmed必须等于完成快照。 |
| R1 终态事实 | terminal-stamp.native完整等于其快照中的native投影，scope等于注册来源，retailLossFinalization保持false；terminal-contract明确Death并维持关闭后零派发。最终报告继续绑定Death/Completed。 |
| R2 历史归属 | 共享检查器将combat-history及history-clearing绑定当前已派发root/context；history-checkpoint绑定最后确认动作且不得处于活跃动作内。原epoch/索引/数量门保留。Major还将战斗动作context绑定实际act/node/房间引用，历史中的当前combat身份和开场身份一致。 |
| R3 夹具来源 | 预登记目标→唯一声明→MajorFixture动作→实际新房间→到达→战斗开场完整关联。声明必须在首场真实战斗、奖励及leave-ready后出现，来源和名称一致；派发完整状态等于声明状态，动作Encounter/RoomType等于目标，实际进入房间须为新实例且目标一致。自然场景仍不允许夹具声明。 |

R1没有从损坏输出反推允许字段。锁定原生ProcessPendingLoss清除PendingLoss、将turnState.IsInProgress置false，然后触发CombatEnded；当前无UI宿主/场景中，投影只改变inProgress和观察Losses计数。全部三个正常详细样本的真实前后差异符合该固定转换。战斗执行中的HP/格挡/RNG变化仍由原生业务产生，不将整个动作冻结。非详细模式保留死亡输入、完整完成→确认及终态契约，不宣称存在未输出的详细history；live/minimal/off完整结果对照单列。

R2实施前读取原保存矩阵：12,194条combat-history全部位于活跃动作内，root/context匹配；history-clearing及checkpoint也符合各自边界。没有将所有元数据或所有观察一律要求为活跃动作。未来增加合法异步阶段时需明确相应契约。

## 修复前后证据

证据根目录：`.build/runs/n3d3-repair-work01/`。

- `before.json`：在原最终DLL实际复现19/19误接受，副本单独保留。
- 首个候选构建编译通过，但正控暴露provenance字段大小写错误，导致正常包也被拒绝；`candidate-gate.json`及候选产物保留，不计为修复通过。修正为注册协议的`ScenarioKind`后重新构建。
- `.build/runs/n3d3-repair-candidate-controls01/summary.json`：153次定向reader调用符合预期，作为候选证据。
- 新项目脚本`tools/native-run/major_repair_controls.py`直接调用最终DLL，所有修改副本重新seal；哈希链错误不计语义拒绝。55个系统性控制包含死亡单边/双边/整尾部状态篡改、真实前后转换、终态facts、历史归属及延后、夹具缺失/重复/错序/联合改写、属性重排与完整一致嵌套扩展、正常及失败前缀。
- 另将原19反例与原4D2独审79包用同一新reader重读。旧包保留原build身份；这些是兼容reader控制，不冒充新冻结的原生执行。

源码快照`source-final01.json`包含76个输入；`source-delta.json`记录相对原冻结只有NativeMajorContract.cs、NativeTraceContract.cs改变，新增major_repair_controls.py。没有更改15锁定输入、52兼容源码或Core契约实现。

## 同构建回归

最终同构建矩阵已通过本轮实现验收，**待独立复审**。10套原生矩阵共309次执行/3908项套件检查，另实跑Core98，全部符合预期。原始退出226×0、70×1、8×2、5×3；没有把死亡、Unsupported、故障或预算转换为全胜。每个进程的manifest、header、process/result、隔离、原始exit及来源标签匹配新冻结。Native DLL SHA256为`A50034525C00DC4AA75341F5A823F264B0A727ACFF552822B7EFC299BA367499`；最终编译0 warning/0 error。

新major矩阵仍为19次原生执行/261检查，原95生产控制全部符合预期。追加153次最终reader调用全部符合预期：55系统性控制、原19反例拒绝、原事件79包（含原25反例）重读。55控制中43坏包拒绝、7正常/等价包接受、5不完整包拒绝认证。候选153次与最终153次分列，不相加冒充本次最终证据。

`native-loss-transition.json`按独立构造的固定原生阶段转换核对最终13条实际死亡路径，完整输入→完成及完成→确认全部一致；详细模式保留真实history锚点，非详细模式不虚构详细记录。精英51/51、Boss51/51、自然精英93/93及所有live/replay/minimal/off完整JSON（含RNG）对照保持。Unsupported35/35与99/100、包装故障3/4、预算4/4、导出DataError51/51及失败重放也保持。

604项来源/执行/重放/保存证据核对通过；76源输入及快照、48编译输入、3游戏输入、7产物、15锁定输入和52兼容源码匹配。对原独审文件锁核对时只允许本次两个生产源文件的预期修改，其余原证据/旧冻结/输入保持不变。最终证据入口为`.build/runs/n3d3-repair-work01/closeout.json`与`summary.json`；逐项见`facts-final01.json`、`outcomes-final01.json`、`source-final01.json`。全部同构建任务及额外reader门完成后才关闭最终矩阵，不以中间套件阶段的成功状态替代完整收尾。

## 复现

最终reader控制（使用全新输出目录）：

```powershell
python -P tools/native-run/major_repair_controls.py --manifest .build/native-build/n3d3-repair-final01/build-manifest.json --source .build/runs/n3d3-repair-final01-major --output .build/runs/n3d3-repair-reproduce-controls01
```

原生major矩阵（另使用全新目录）：

```powershell
python -P tools/native-run/run-native-major.py --manifest .build/native-build/n3d3-repair-final01/build-manifest.json --output .build/runs/n3d3-repair-reproduce-major01
```

## 保留限制与下一步

当前策略仍为oracle-current-intent/defend10/attackfirst/v2，三个不同正常配置不是独立胜率样本。Death是合法终态证据，API夹具不是自然Boss路径。Merchant/Hollow在新驱动的能力组合尚未完成，真实失败前缀继续保留。奖励生成RNG仍为有限契约，不独立重算整个候选池；同房间多阶段Boss也未由当前LastVictoriousRoom证明完整支持。

通过本轮独立复审后，接续[4D3后续交接](NATIVE_RUN_N3D3_NEXT_HANDOFF.md)，先组合既有节点能力和合法资源准备，实际完成精英/Boss Victory→奖励→act-ready，再步骤5–6。持续授权有效，无需逐步请批。

仅Lab内实现及隔离实验；无游戏UI、真实Steam/网络、用户存档、Mod专项、提交或推送。原始执行、审查反例和候选失败证据均保留。
