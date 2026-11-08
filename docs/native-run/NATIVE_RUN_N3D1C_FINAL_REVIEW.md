# 4D1C R1/R2 修复最终独立验收

结论：**通过当前 4D1C 最小范围验收，原 R1/R2 必修项已闭合。** 可接续[奖励语义闭环交接](NATIVE_RUN_N3D_REWARD_CLOSURE_HANDOFF.md)。这不是完整 4D 或 M1 完成结论。

本次只读运行实现及原证据，在独立副本上执行生产检查器；没有重新运行原生游戏场景，没有修改原始结果或运行代码。原失败报告 [NATIVE_RUN_N3D1C_REVIEW.md](NATIVE_RUN_N3D1C_REVIEW.md) 保留，修复范围见 [REVIEW_REPAIRS](NATIVE_RUN_N3D1C_REVIEW_REPAIRS.md)。

## 冻结与来源核对

- Build ID：`1348A7FAD6C9BDA35E91EC469911FD57CC1800BB8B76CD3B19B30F9872D3163B`，对 manifest 重新计算一致。
- Native DLL SHA256：`58446B36A4AA5E08316BF0FD3A8A88B496649E7E75EB558C0ED6294B939BFCEE`。
- Manifest：`.build/native-build/n3d1c-repair-final01/build-manifest.json`；61 源文件、40 编译输入、3 游戏输入、7 产物与当前文件和保存的 dirty 源码快照一致。
- 15 个固定 Mod 输入、52 个兼容源码及 2622+993 个历史证据文件哈希保持。它们仅作为冻结资产检查，本次没有新增 Mod 适配。
- 独立证据：`.build/runs/n3d1c-repair-independent-review01/`。`provenance.json` 为 475 项元数据、实际退出码、隔离记录、结构化状态/选择/RNG 重放核对；`build-id-check.json` 为重新计算结果。

## R1：补货状态链已闭合

`NativeMerchantExpansionContract.cs:50` 到 `:76` 将本次 stock-start 完整状态绑定到 price-start。仅按原生 FillSlot 顺序允许当前商品 content/model/source 更换和 Shops 两次推进，旧 cost 保留至 CalcCost。price-return 仅允许同 entry 的 cost 及一次 Shops 推进；`:156` 要求 stock-return 完整等于本次 priceTail。

这使共同篡改两个价格快照、然后在父操作恢复原值的反例失效，而不是只补金币或特定 RNG 字段。第一、第二次补货均覆盖；相邻 stock-return/Hook-start 共同添加嵌套字段也被拒绝。真实原生商品更换、价格变化和完整 RNG 推进仍通过。

## R2：身份、代次与真实取得已闭合

`NativeMerchantExpansion.cs:22` 至 `:32` 从实际对象读取 reference、具体 type、model 和 relicState；额外来源读取前先执行相同的必需状态投影，以保持身份登记顺序。新增观察不执行选择、扣款、取得或 RNG 调用。

生产契约的关键绑定为：

1. identity 登记、库存 content/model/source、effect-start offeredSource 对应同一实际对象；类别和具体类型交叉检查。
2. 同 entry 代次严格递增，生成对象不能重复；补货对象须在当前 stock-start 后登记，也不能已经存在于补货前完整投影中。
3. 默认 AfterObtained 完成后，真实新增遗物实例的完整 transactionRelics 记录等于取得前的 offered relicState；已有实例保留，支付与原生取得历史独立核验。
4. MawBank 的真实 Hook 变化在其单独业务区间检查，不能借“允许 Hook”放宽其他完整状态；返回、receipt、确认、退出链继续保持绑定。

对应代码：`NativeMerchantExpansionContract.cs:53`、`:83`、`:135`、`:172` 至 `:198`。这些保证限于目前采集的状态投影和声明的交易 profile，不等于验证整个运行时堆。

## 实际生产检查器复验

本次累计 **452 次生产 reader 调用**，全部符合预期；它们不是 452 次原生游戏执行。

| 独立复验集合 | 数量 | 结果 |
| --- | ---: | --- |
| 全部现有商店生产控制 | 368 | 362 损坏拒绝、6 等价通过 |
| 正常交易/自然/观察模式副本 | 14 | 全部通过 |
| 历史证据抽样 | 28 | 损坏与等价均符合预期 |
| 历史 v1 退出反例 | 1 | 拒绝；不以旧 schema 拒绝作为 R1/R2 语义证据 |
| 新 v2 重建原八例及修复补充 | 15 | 14 拒绝、1 等价通过 |
| 本次另建独立副本 | 26 | 23 拒绝、3 等价通过 |

`reader-results.json` 保存前 426 次实际退出与拒绝原因，`independent-controls.json` 保存另 26 次。副本重新计算 seal，故拒绝由生产语义门产生，并非仅由封装校验失败产生。被读取的原包保持逐字节不变。

原八例中的金币/嵌套字段共同篡改现由 `refill price start full generation state binding` 拒绝；取得状态由 `relic state acquisition` 或完整来源结构门拒绝。旧对象复用还做了 type/model 一并保持一致的专门副本，明确在 `generated object unique generation` 门拒绝，不能只靠相互矛盾的模型字段拒绝冒充代次验证。

本次新副本覆盖两轮补货各自的非 Shops RNG 篡改、相邻库存/Hook 共同篡改、来源与库存一起改指另一商品、登记提前/迟到/缺失/重复、整段定价删除/重复/倒序，以及来源与取得状态一起改变、取得子阶段共同改变和退出尾部共同改变。属性顺序变更、深层属性顺序变更、所有状态中一致的静态扩展均通过，证明没有把合法等价一并误拒。

既有 830 次历史 reader 调用的完整清单和原始退出码已核对；本轮实际抽样重读其中 28 次，未声称独立重跑全部 830 次。7 个真实 Harmony 审计 API 探针/14 次拒绝/目标业务调用 0 的记录保持，仅证明补丁审计，不属于原生交易覆盖。

## 原生执行与结果范围

同一最终构建的 **245 个原生进程 /2924 项套件检查**逐包核对一致；Core98 核对其已有通过日志及同构建 Core 产物身份，本轮未重新执行 Core。分组为交易 18/471、经济 23/361、旧商店 25/371、4C 55/561、4B 34/307、4A 16/273、N2 74/580（进程/检查）。

原始退出码为 178×0、60×1、4×2、3×3；受控失败、Unsupported 和预算停止均按各自契约保留，没有以外层成功遮盖。

- OddlySmoothStone 原生购买、默认 AfterObtained、MawBank 真实 Hook 和 Courier 连续两次药水购买/补货成立。生产包装等待原始 Task，未替代原生业务。
- DEV_01 是正常 Neow 开局的同 Run 71/71 动作：购买 WingCharm 实付 177、合法离店并在下一战实际打出 DefendIronclad。
- DEV_02/03 仍为 Unsupported，确认/派发前缀 37/37、79/79；原样本未替换。它们不能计作成功自然购买路径。
- stale 为 InvalidAction 4/4；fault 为 EngineError 3/4，保留真实已发生副作用及未确认根动作。
- 旧自然商店 72/41/83、自然 skip/take、真实 Death、尾部夹具、完整状态/选择/RNG 重放及观察模式对照保持。后者不能转换成零售演出或完整堆等价声明。
- 所有原生记录仍使用 MemorySaveStore、shouldSave=false、Steam 未初始化、无 UI/Mod/CombatSolver。预期故障场景的被捕获 Faults 和遥测拦截计数单独按失败契约检查，没有误要求它们为零。

## 后续与限制

当前仍是无特殊拾取效果的遗物购买、MawBank Hook、Courier 药水补货和单商店切片。特殊拾取/选择、卡与遗物补货、其他购买 Hook、替换/拒收及多商店 session 仍需后续原生证据；未知分支不能静默跳过。自然策略应支持合法不购买离店，不能把购买目标不可达变成整个 run 的永久停点。

接续奖励的 reroll、真实 card-add 结果/替换、Sozu 拒收、Neow 统一会话和宝箱来源账本，再推进事件战斗恢复、Elite/Boss、跨幕完整 run 与预登记批测。详见[下一交接](NATIVE_RUN_N3D_REWARD_CLOSURE_HANDOFF.md)。当前无必修项或权限阻塞。

执行期间发生一次工具 transport disconnected；返回状态一度不确定。恢复后核实 26 个独立结果及日志已完整落盘，未重复运行该批次。没有绕过权限拒绝、改动角色/安装/真实存档、启动 UI、调用真实 Steam/网络或提交推送。
