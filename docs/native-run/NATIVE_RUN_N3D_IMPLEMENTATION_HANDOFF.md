# M1 步骤4D：商店、嵌套事件与精英／Boss

日期：2026-11-02。承接[4C最终独立验收](NATIVE_RUN_N3C_FINAL_REVIEW.md)，进入条件已满足，可继续实施。全六步已有授权；本交接不代表已执行4D，也不替代步骤5的自然跨幕完整run与步骤6交付。

后续状态：首个商店交易子阶段已实现，见[4D1验证](NATIVE_RUN_N3D1_VALIDATION.md)与[接续清单](NATIVE_RUN_N3D1_NEXT_HANDOFF.md)。当前仅Card/Potion/移除/退出的显式夹具，尚待独立验收；不是本交接全部完成。其余自然商店、遗物/补货、奖励闭环、事件与Boss仍按下述范围推进。

实现建议继续Sol xhigh / Standard，独立验收Astra xhigh / Standard。固定v0.111.0 / 41cef1ea、Ironclad/A0、独立Lab档案、一run一进程、native-only/NotCompared。4C验收基线为 `458B3547C7B2E9A979EF7CC299C024E9BFA9704861D734FD370133EE3EE16652`，manifest为 `.build/native-build/n3c-boundary-final03/build-manifest.json`；实际原生矩阵为 `n3c-boundary-final03b-*`，独立证据为 `.build/runs/n3c-boundary-independent-review01/`。保留当前dirty源码和已通过的17类生命周期契约，不回退到早期构建。

## 4C独立审查后的进入条件与新增依赖

[早期4C独立审查](NATIVE_RUN_N3C_REVIEW.md)及其后续复审发现的操作树、子会话静止状态、取得身份／结果内容问题，已在最终独立验收闭合：历史21反例在当前证据上拒绝，全部292控制和独立补充均符合预期。4D可以开始；以下未覆盖原版路径仍是后续明确依赖，不因4C通过而自动视为支持。新增操作应沿用[完整生命周期契约](NATIVE_RUN_N3C_LIFECYCLE_CONTRACT.md)的归属、必需/唯一结果、静止完整状态和真实业务变化区间。

在4D商店／事件与奖励桥整合时，增加以下已核实的原版闭环；最迟在步骤5正常跨幕验收前完成。范围和真实代码依据详见上述审查，未完成不能只写Unsupported后关闭M1：

- 同一CardReward.OnSelect中的Driftwood原生REROLL→第二次菜单→实际取得，保留每次请求、choice ID、RNG、历史及失败前缀；无需引入Mod制造额外多选机制。
- 复用真实CardPileAddResult建立所选卡→实际取得卡／拒绝结果／modifyingModels关系。原生FresnelLens存在实例替换，也可能在候选生成时已完成修改，夹具须证明具体发生路径，不能猜测或手工添加。
- 原生Sozu阻止药水取得时，PotionReward仍可标完成；报告应区分奖励已处理与真正入槽，保留NotAllowed及TooFull的不同语义。
- 新session整合到Opening后，使用旧已知seed／决策验证Neow内部连续CardReward。4C自然Golden Pearl样本与旧4Bprofile回归各自保留，不能代替迁移后的证明。
- 完成宝箱金币修改／SpoilsMap的原生来源账本。4C修复仅认证native-unmodified；原生返回金额与实际增量不同必须有具体Hook／任务来源及真实结果，不可强行相等或用Unsupported关闭M1必需路径。

这些是原版主线的内部接口完善，不开展公开SDK或Mod适配。依赖若留到步骤5，应在4D交付清单明确为未完成前置门；不得以选择成功种子、替换奖励或跳过已触发语义取消失败。

## 工作顺序

### 4D1：原生商店与交易

1. 从真实`MerchantRoom.EnterInternal`复用`MerchantInventory.CreateForNormalMerchant`，取得本地原生库存，不手工填卡／遗物／药水。先核对并处理`PreloadManager.LoadRoomMerchantAssets`纯展示边界，保留价格、库存、购买Hook。记录entry实例、商品实例、价格、stock状态、完整库存及player金币；策略只接收公开字段。
2. 交易调用原生`MerchantEntry.OnTryPurchaseWrapper(inventory, ignoreCost:false)`，等待其返回和`AfterItemPurchased`完成；不直接扣金币或append商品。原生方法处理缺货、金币不足、清空／补货。丢弃或满槽后重试药水使用4C的真实槽位与实例规则。
3. 卡牌移除走`MerchantCardRemovalEntry`和`OneOffSynchronizer.DoLocalMerchantCardRemoval`，复用原生候选／可取消选择。检查当前库存实例与指定永久牌实例。成功的扣款、移除、次数和后续价格必须实际执行；取消保持牌组／金币。
4. 特别审查UI承载的非视觉收尾：当前`MerchantCardRemovalEntry`通过可空`NRun.Instance?.MerchantRoom?.Inventory.OnCardRemovalUsed()`更新商店。固定DLL中该回调属于`NMerchantInventory`，转发`NMerchantCardRemoval`，业务为原生`entry.SetUsed()`；扣款/移除/次数增加在OneOff任务。4D1已在OneOff成功返回后、`AfterItemPurchased`之前补调原生SetUsed并实际观察Hook时Used=true，取消/失败不补调。不能用`Used=false`反复移除，也不能重复增加次数或收费；跨店价格实际行进仍须后续证明。

固定A0夹具的无修饰首次移除75金币、后续基础增价25可作为经SDK核验后的独立规则常量；购买时仍以原生动态价格／Hook为准，不将独立常量套给所有遗物组合。

### 4D2：事件选择、事件内战斗与返回

先处理已有DEV_03实际停点WhisperingHollow，保留原4B的77/78 Unsupported证据和旧profile结果；新profile可扩展同种子路径继续执行，不替换成功种子。核对该Model实际初始化、layout中的业务责任与`CurrentOptions`，仅隔离纯展示，不绕过原生选项生成／执行。

随后选一个本版本真实的“事件选项→事件内战斗→奖励→原事件恢复／继续选择”垂直场景。具体`RunManager`/事件命令、room stack和Resume入口须从固定DLL核验，不猜方法名。一次根任务持有其原生任务及嵌套选择／奖励；不能在父根未确认时开启平级根，也不能因await队列空就跳地图。原生内部吞错继续由已有task-fault账本传播。

4C已在Neow EventOption根内接入奖励提供器，可复用这条结构。需要新候选类型时增加内部、类型明确的请求和生产契约；不把非卡对象塞进ICardSelector，不建立公开SDK。

### 4D3：精英／Boss与幕末准备

先用明确标注的原生Debug房间夹具验证各一个精英和第一幕Boss，实际进入、打完并收取原生奖励。复用4C的Gold/Card/Potion/Relic集合，而不是按节点写奖励效果镜像。困难战斗可使用声明的固定动作策略／可选solver，但每步原生执行；不能修改HP／资源以取得胜利。

随后在新自然多节点profile中进入并执行本轮节点。商店／事件／精英无法处理时明确停Unsupported，不按类型跳过以满足表面覆盖。弱策略真实死亡保留。至少给出自然进入一个新类型节点且完成其真实业务、合法后续行进的证据；各类别fixture与自然路径分列。

Boss战斗Victory仅是战斗终态。记录原生幕末奖励与act-ready/同步器边界，保持4A的终态判定；自然跨幕与全默认Acts的RunVictory在步骤5关闭。最终Boss可能合法没有普通奖励，不能恢复“单offer且正金币”的旧假设。

## 每组验收与数据门

| 范围 | 实际正例 | 必需拒绝／失败证据 |
| --- | --- | --- |
| 交易 | 原生Card/Relic/Potion取得与扣款、补货／售罄、合法离店 | 旧entry、错误库存、重复购买、金币不足、满槽、扣款后故障的真实前缀 |
| 移除 | 指定非0永久牌移除、实际收费与Used／价格更新、取消 | 旧／不可移除牌、重复移除、取消仍扣款、仅改数量或标志、缺业务收尾 |
| 事件 | 原生不同选项、分阶段／继续选择、实际选项效果 | 旧请求、非法／缺失／多余答复、内部task异常、未完成就离房 |
| 嵌套事件战斗 | 同一run真实房间栈、胜利／死亡、奖励和原事件恢复 | 假恢复、原事件身份丢失、跳过奖励、父子根重叠、重复结算 |
| 精英／Boss | 实际战斗终态及原生奖励、幕末准备 | 未实际胜利发奖励、把HP清场当Death、fixture当自然通关 |
| 数据 | 原生对象／模型／来源、完整RNG、前后HP/金币/牌组/库存、任务与房间生命周期 | 缺失／重复／错配／顺序错误、静止完整快照漂移、不同类型/实例混用、新增字段的局部矛盾 |

原生任务开始的静止边界绑定不可变dispatch或其正确子请求快照；实际业务中间状态按阶段记录，完成后的静止观察点绑定对应不可变返回／确认。不要把所有相邻事件一律当静止。新场景一次定义所需请求／返回／确认／边界的必需、唯一、归属和闭合条件，随实现加入生产reader正负控，保留合法取消／补货／清场变化。

每个正场景新进程冻结重放；每组至少一个详细／minimal对照，比较完整RNG及实际状态。沿用完整状态字段与实例验证，不只看最后金币或进程exit0。budget、Unsupported、InvalidAction、EngineError和真实Death继续分开；故障后的原生前缀和未确认根保留，不以回滚或补写完成消除。

## 集成与交付

先共享库存／请求协议，再交易／移除，再事件恢复，再精英／Boss与自然片段；共享构建缓存串行使用。原4C、4B、4A、N2 profile均作为独立回归，不改旧样本或数量要求来接纳新场景。最终冻结实际dirty源码与编译输入，在同产物运行适用1–4D矩阵和Core，复核15个Mod输入／52个兼容源码未变；保留历史独立损坏包。

只新增必要原生内部适配。遇纯视觉依赖可在精确来源／方法／版本边界处理；需要新宿主权限、真实存储／Steam／网络或弱化审计时停止该依赖并报告具体接口，继续独立安全工作。保留MIT与上游归属，不发布游戏DLL、反编译源码、Agent资料和原始运行目录，不提交推送、不改角色或安装、不启动真实UI。

交付支持矩阵、真实自然／fixture／control分类、最终源／输出hash、原始退出及失败前缀、可重现入口、数据样例、未覆盖清单，并给步骤5的自然第一幕／跨幕／完整run交接。4D结束仍需独立验收；用户已授权总体推进，无需逐步再次请批。
