# 第三幕商店修复：独立复审

2026-11-03。结论：**当前有界范围通过独立复审，原 R1/R2 已闭合，未发现新的必修问题。** 此结论只认证本次商店检查器修复及适用证据；第三幕 Boss、正常开局 RunVictory 和最终 M1 全量门仍未完成。

入口：`.build/runs/act3-shop-repair-independent-review01/closeout.json`。修复说明见 [NATIVE_RUN_ACT3_SHOP_REVIEW_REPAIRS.md](NATIVE_RUN_ACT3_SHOP_REVIEW_REPAIRS.md)，原问题见 [此前独审](NATIVE_RUN_ACT3_SUPPLIES_INDEPENDENT_REVIEW.md)。本轮仅新增独立审查副本、脚本和本报告，未改生产实现、原始记录或已有报告，未启动新原生游戏流程、UI、真实 Steam/网络/用户存档操作，未提交推送。

## 冻结、生成链与复用边界

| 角色 | 完整冻结 ID |
| --- | --- |
| 原始执行与采集 | `2EB24CD78644588B03618F313FF3842166AB850858CBEB09BE9C937A6E7F9F53` |
| 上轮检查器 | `F28EA015BBC3D32D3919E732209C11F7ABE7FD5CE8CE026BC50E4CEFFE127777` |
| 本轮实际调用检查器 | `A3A35E442200B934240DA32F8ED0FC3B394C9E00D193031CF794F1A7B47B7F48` |

独立重新计算三份 manifest ID，核对当前 100 份源码、64 份编译输入、3 个游戏输入和 7 个产物，以及各历史构建的输入/产物。SDK 指纹均为 10.0.201。相对上一检查器仅 `NativeThirdShopContract.cs` 变化；相对原生生产构建仅 `NativeMajorContract.cs`、`NativeThirdShopContract.cs` 两个检查器编译输入不同，执行/采集链未变。15 个固定输入、52 份兼容源码保持。

本轮因此按[分层验收及信任边界](../architecture/NATIVE_RUN_EVIDENCE_TRUST_BOUNDARY.md)重验完整受影响冻结证据，**没有重跑无关原生矩阵**。不同年代证据保留自己的 producerBuildId，不把本次检查器 ID 写成原始执行 ID。哈希证明一致性；实际执行结论同时依据原始受控进程退出、原生调用链、完整记录和既有独立进程重放。

## 实际检查结果

- 生产 `SimulatorLab.NativeRun.dll --review` 共 **269 次**：253 份既有原始/历史/控制输入重读，另加 16 个独立控制（2 合法/等价、14 错误）。44 接受、225 拒绝，全部符合预期，原始输入未变。每个错误副本先合法重封，拒绝来自生产语义检查，不依靠损坏 seal。
- **475 项**独立指纹、来源、原始退出/失败前缀、状态、RNG、实例、付款、合法离店和重放核对通过。见 `facts.json`；实际读器结果见 `summary.json`。这两个计数分别是断言数与检查器进程数，不是新增原生对局数。
- 原 6 个反例重验均退出 1：`reread/204`、`205`、`207` 在 `exit popped-room complete dispatch frame` 拒绝；`209`、`210`、`211` 在 `registered concrete type/model/entry category` 拒绝。
- 旧 11 份缺实际玩家 Creature 锚点的记录仍拒绝；旧 v2 两份缺 maxHP 中间观察的记录仍拒绝。未回填、未误认证。原 EngineError 保留 3 确认/4 派发及原退出 1；BudgetExceeded 保留 4/4 及原退出 3，二者均不能认证完成。

## R1：当前 Travel 与完整退出状态

`NativeThirdShopContract.cs` 的 `ExitInput` 从当前不可变 dispatch 构造预期快照，先要求它等于本次商店完成状态并绑定实际房间/库存。只应用固定版原生出栈变化：实际 Travel 目标、floor 加一、room/rooms 清空，以及每个原库存 entry 的 Cost 恢复已保存的原价。退出返回再只允许 `merchant.closed` 变为 true。

独立核对实际记录：节点 `(2,5)`→`(2,6)`、floor 6→7、金币始终 85；退出开始/返回的完整结构分别等于上述预期。锁定程序集 `MerchantEntry.Cost` 在 MerchantRoom 外返回原始 `_cost`，所以不能简单要求退出快照与 dispatch 全等，也不能用折扣价乘二恢复奇数原价。实际 185→92→185、37→18→37 等合法变化保留。

独立负控覆盖两退出快照共同修改嵌套 inventory 字段、交换原价但保持总和、修改 RNG 内部字、改牌升级、同时修改 dispatch 和退出金币、错 root/context。均被生产契约拒绝；递归反转 JSON 对象成员顺序的等价副本通过。当前完整状态绑定不依赖仅检查金币、节点等少数字段。

**既有折扣遗物的原价采集仍未覆盖。** 当前样本入店前没有价格修改遗物，入店后实际购买 MembershipCard，故初始 entry Cost 可作为原价锚点。检查器会拒绝缺少独立原价来源、且入店前已有 `ModifyMerchantPrice` 覆盖实现的情况；本轮未声称这类自然入店路径通过，也未新增采集器或测试去补它。后续若需要该路径，应实际采集原价并按生成链影响范围重跑。

## R2：取得对象、登记模型与商品类别

`ModelType`/`CheckEntry` 验证真实库存 entry 类别、所指内容身份、具体 CLR 类型与对应原生 CardModel/RelicModel/PotionModel 基类，再调用固定程序集的 `ModelDb.GetId(Type)` 核对模型 ID。该 API 仅通过类型元数据生成 ID，不调用 ModelDb.Init、构造模型或初始化游戏内容。

这些检查覆盖入店库存、购买开始和实际取得返回，并保留 offered→acquired→实际牌组/遗物/药水槽位的同一对象关联。独立核对五笔真实付款和新增对象：MembershipCard 185、Pantograph 112、StoneCracker 114、ExplosiveAmpoule 25、Inflame 36，共 472。对应实际原生 `OnTryPurchaseWrapper(ignoreCost:false)` 任务、一次成功 callback、库存清空和新实例均有记录。

独立负控还覆盖同类别其他具体模型、未购买库存对象的错误类型、entry 类别矛盾、抽象/错误限定类型、登记 model 与 offered 不同，以及 acquired 错指已持有遗物。全部拒绝。三类合法商品和完整原始记录通过，没有靠放开类型或删除状态字段取得通过。

## 保留的流程事实与下一步

原始同一自然 Run 的第三幕五场胜利、五笔购买、两个事件、后续实际离店出牌及 **Death545** 保持。live 与 detailed replay、minimal、off 的 **1090 个派发/确认完整 payload（含完整状态、RNG、root/context）**、商店记录及动作路线相等。本轮复核的是原始 9 个原生进程结果，新增原生进程为零。MemorySaveStore、禁保存、Steam 未初始化、无 UI 的原始隔离记录保持；保存/replay 拦截次数不是持久化写入次数。

可以接续[第三幕既有交接](NATIVE_RUN_ACT3_SUPPLIES_NEXT_HANDOFF.md)，使用本报告的新检查器冻结，继续合法策略、第三幕 Boss 及正常开局 RunVictory。不要把检查器通过当作策略存活改善或完整通关。自然集成商店移除/补货/特殊拾取、预先持有折扣遗物的原价采集仍未覆盖；复活等死亡机制也未在本轮扩展。最后的 M1 同构建全量验收仍须执行。

复现：针对 `summary.json` 所列副本运行本轮冻结 DLL 的 `--review <case>`；`review.py` 为本轮完整独立读取和控制脚本，要求新的输出目录以免覆盖证据。`fingerprints.ps1` 与 `facts.py` 提供指纹和事实复核。无权限或环境阻塞；本轮止于独立验收，由主线程接续后续实现。
