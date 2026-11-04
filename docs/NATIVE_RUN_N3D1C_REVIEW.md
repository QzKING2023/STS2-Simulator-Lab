# 4D1C 最小范围独立验收

结论：**未通过；需要集中修复两项生产证据校验缺口。** 正常原生交易和自然片段的执行证据成立，当前可信数据检查器仍会接受补货中间状态及新对象身份矛盾。此次审查只读取实现，执行现有生产 reader 与独立副本负控；没有修改运行代码或原始证据，没有重跑原生游戏矩阵。

审查对象为冻结 `37375A30D91CA842BB756301A94F344335F75498B4DE759A3BFBBAC2D4A297BD`，Native DLL SHA256 `B476631DFB1ADABE65E4056D6A3D7DF06602A094E6440948865B12F6C3CA7D5D`。Build ID 独立重算一致，61 源文件、40 编译输入、3 游戏输入、7 产物与当前文件及保存的源快照匹配。

## 已确认的执行结果

- 原生 `MerchantEntry.OnTryPurchaseWrapper` 按顺序调用实际购买、Clear/Restock、`Hook.AfterItemPurchased`、完成回调。新包装器等待原任务，未替代扣款、取得或补货业务。对应项目入口：`tools/native-run/NativeMerchantExpansion.cs:47`；本机原生参考在 `.build/runs/n3d1c-work01/` 和 `.build/review/n3d-merchant/`，仅作本地证据引用。
- `relic-1` 的 OddlySmoothStone 原对象 i38 实付 161 后进入持有列表；默认 AfterObtained 两端及清空库存可观察。`hook-1` 实付 161，真实 MawBank 从未购买变为已购买、IsUsedUp、Disabled；不是人为设置 Hook 的结果。
- `courier-1` 同一药水 entry 连续购买两次，实际取得 i44、i48，替换库存为 i48、i49，支付 38、40。两次原生补货各消耗 Shops 流 3 次，当前正常证据的完整 RNG、价格、实例与原生调用一致。
- DEV_01 正常 Neow 开局的同一 Run 中，a69 原生买到 WingCharm（i98、177 金币），a70 从 (2,5) 合法到 (2,6)，a71 真正执行 DefendIronclad；71/71。DEV_02、DEV_03 仍是 Unsupported，分别 37/37、79/79，未换样本。这是采购目标不可达的片段边界，通用 run 以后须允许合法不买离店。
- stale 为 InvalidAction 4/4；fault 为 EngineError 3/4，真实购买已发生、根动作未确认。两者没有被外层成功改记为正常完成。
- 同构建 245 原生进程、2889 套件检查、Core98 的已有结果和实际 raw exit 已核对：178×0、60×1、4×2、3×3。Core98 是核对已有同产物日志与 Core 哈希，本次没有宣称重新运行。旧自然商店 72/41/83 的 route/choices/final-state 与前一基线相等；新正常夹具重放、DEV_01 重放及 minimal/off 对照也保持完整投影（包括 RNG）。
- 15 锁定输入、52 兼容源码、2622+993 历史证据文件未变。隔离记录为 MemorySaveStore、shouldSave=false、Steam 未初始化、无 UI、无 Mod/CombatSolver；进度/重放以及错误遥测计数是拦截次数。五个既有故障控制中的 Faults 和遥测拦截已按故障传播契约核对，不能要求这些预期故障计数为零。

## 实际 reader 检查

独立证据目录：`.build/runs/n3d1c-independent-review01/`。

- `reader-results.json`：实际执行 376 次生产 reader，包含全部 333 个既有控制、14 个正常交易包、自查退出共同篡改包、28 个历史样本；全部符合预期，所有来源文件未变。
- `independent-controls.json`：独立生成并用 .NET 重新封装 15 份副本。13 份为矛盾控制、2 份为等价控制；5 份矛盾正确拒绝，**8 份矛盾被错误接受**，2 份等价正确接受。总计本次 391 次生产 reader 调用；不计为新增原生执行。
- `provenance.json`：475 项冻结/进程/来源/保存隔离/重放/保留性核对。`build-id-check.json` 记录独立重算。
- 已有 830 次历史重读的完整清单和 raw exit 均已核对；此次只从中实际抽取 28 份重读，未冒称重新运行全部 830 次。七个实际 Harmony 审计 API 探针的 14 次拒绝仅为审计证据，目标业务调用为零。

已修复的退出反例仍正确拒绝：生产 reader 对 `n3d1c-work01/control-exit-anchor-before-fix` 和本次新建的 `control-exit-coedit-gold` 都返回 1，错误为 `Transaction exit full non-history state binding`。缺 stock-start、重复 Hook、错误退出房间也被拒绝；换属性顺序和全程一致的静态扩展均可通过。

## R1：补货价格子阶段没有完整绑定原生父阶段

位置：`tools/native-run/NativeMerchantExpansionContract.cs:44`、`:50`、`:134`。

当前 `merchant-price-start` 只把 Shops RNG 与 stock-start 推进两次的结果绑定；price-return 只与本对 price-start 的状态比较。stock-return 则另行对照 stock-start，并仅从 priceTail 取 playerRng。于是两份价格快照可共同偏离父阶段，再在 stock-return 恢复，整个证据仍通过。

以下副本均来自正常 `courier-1`，仅改变第一轮补货 a4 内两份价格快照，其他阶段保持原始值；seal 有效，实际 reader 均 **exit 0**：

| 副本目录（审查目录下） | 违背的数据 |
| --- | --- |
| `control-refill-price-gold` | price-start/price 金币共同增加 7，stock-return 又恢复 |
| `control-refill-price-extension` | 只在这两个快照出现未知嵌套状态，随后无业务原因消失 |
| `control-refill-price-wrong-content` | 同 entry 的价格快照指向另一已在店内的药水 i45，stock-return 却为真实 i48 |

最小根因修复：建立 `stock-start → price-start → price-return → stock-return` 的完整状态关系。原生 `FillSlot` 在 CalcCost 前确实已经替换当前商品并消耗选择 RNG，因此允许**所选 entry 的实际新 content/model 和恰当 Shops 推进**，保留原 cost 到 CalcCost；其他状态须完整相等。CalcCost 只允许约定价格和 RNG 变化，stock-return 应与 price-return 的完整状态绑定。不能继续只补 gold 或单个字段，不能误拒真正的替换和价格变化；需覆盖第一、第二次补货及合法等价。

## R2：生成代次、登记身份和取得遗物状态缺少同一对象绑定

位置：`NativeMerchantExpansionContract.cs:31`、`:118`、`:123`、`:139`；观测投影在 `NativeMerchantExpansion.cs:22`。

检查器只保存 identity 的 type 字符串供房间/库存检查；补货新对象只要求不等于紧邻旧商品，未排除更早的已持有对象，也未将登记 type/model 与实际投影绑定。取得遗物的 transactionRelics 新条目只检查 id、未使用标记及数组长度，其 model/status 等并未绑定来源。因此当前观察数据中的相互矛盾可被整体传播到后续静止点。

以下副本实际 reader 均 **exit 0**：

| 副本目录 | 违背的数据 |
| --- | --- |
| `control-refill-reuses-owned-instance` | 第二次补货将 i49 全部改指已取得且仍持有的第一瓶 i44；i44 同时成为新库存且被写成不同 model |
| `control-refill-identity-wrong-type-model` | i48 的登记记录改成 BurningBlood/RELIC.BURNING_BLOOD，后续仍作为 FlexPotion 使用 |
| `control-acquired-relic-model` | i38 在 relics 是 OddlySmoothStone，但 transactionRelics 从取得起一直写成 BurningBlood |
| `control-acquired-relic-status` | 无相应业务变化，i38 从取得起被持续写为 Disabled |
| `control-acquired-relic-extra-field` | 新取得条目出现未与来源关联的嵌套字段并持续传递；用于同根因结构覆盖，不作为唯一阻断依据 |

最小根因修复：为本次原生生成对象绑定登记的身份、模型/种类、生成阶段和 entry 代次；新补货必须是本次生成的真实对象，不能别名到持有列表、另一库存或更早代次。关联 price facts、价格快照、stock-return、receipt 及后续购买使用的对象。新遗物须把已有 relics 身份、登记信息及 transactionRelics 完整记录绑定到**取得前实际 offered 对象的观测**和默认 AfterObtained 的允许变化；可补最小只读来源观测，避免靠 id 或宽泛字段排除放行，也不要硬编码所有遗物的初始状态。保留真实 MawBank Hook 及 Courier 连续代次的合法变化。

这些是当前最小范围承诺的实例与可信状态缺口，不要求本轮扩展特殊拾取、其他遗物效果或全局 schema 所有可能值。

## 修复验收与后续顺序

先集中修复 R1/R2，把本次 8 个误接受副本及同类完整链/身份控制加入实际生产检查器回归；正常副本、合法业务变化和等价副本继续通过。保留本次所有反例及原证据，重新冻结同一源码/产物，复跑适用原生矩阵和既有控制，再独立验收。修复已获用户当前 M1 授权，无需逐步请批。

通过后沿用 [下一交接](NATIVE_RUN_N3D1C_NEXT_HANDOFF.md)：奖励 reroll、真实取得/替换/拒收、Sozu、Neow 和宝箱来源账本，再推进事件战斗恢复、Elite/Boss、同 Run 跨幕及完整终局与批测。特殊拾取选择/效果、卡和遗物补货、动态价格/替换监听器、同 Run 多商店会话仍未覆盖；这些会限制 M1 原生内容和选择可达性，须在完整 run 内容门及预登记样本中显式解决或精确列为 Unsupported。不能把采购夹具的通过当作所有原版遗物已支持，也不能用静默跳过来完成 M1。

本次没有环境或权限阻塞，没有实现改动、提交或推送。只新增此审查文档和独立审查证据；M1 仍在进行。
