# 4D1C R1/R2 集中修复与复验

本轮只修复 [独立审查](NATIVE_RUN_N3D1C_REVIEW.md) 的两项可信数据缺口。实现、定向正负控和最终同构建统一矩阵已通过，待独立验收。整个 M1 继续按 [下一交接](NATIVE_RUN_N3D1C_NEXT_HANDOFF.md) 推进。

## 修复方式与版本

交易采集契约升级为 `native-merchant-transactions/v2`。v1 原始证据和原冻结产物均保留；旧 v1 包缺少 v2 的必需来源字段，不能拿新 reader 拒绝旧包当作原反例语义已修复。本轮在新 v2 实际执行结果上重建八个原反例及同类变更，由同一生产 reader 验证。

R1：补货前状态、原生生成后/定价前状态、定价后状态、上架返回状态形成完整链。生成阶段只允许所选 entry 的真实 content/model/source 替换及 Shops 流两次推进；保留旧 cost。定价阶段只允许该 entry 的 cost 变化及 Shops 流一次推进。补货返回必须完整等于定价后状态。金币、其他资源、另一 entry、嵌套新增字段、其他 RNG 和房间/库存身份没有宽泛豁免。

R2：读取原生对象的 reference、具体 type、model 和完整 relicState；同时在库存快照、取得前 effect-start、identity 登记、价格事实和后续状态中绑定。每个 entry 独立递增生成代次，每次补货的新对象必须在当前 stock-start 后登记，且不出现在补货前完整状态或既有生成集合中。持有列表、其他库存和以前代次都不能冒充本次生成对象。取得遗物的完整状态必须等于取得前真实 offered 对象的状态；默认 AfterObtained 不允许无依据改变它。MawBank 之后的真实购买 Hook 单独应用其既定状态契约。

来源观测只调用已有无副作用的身份和状态投影 getter，不执行选取、扣款、取得或补货。为保持同模式身份分配稳定，额外来源读取前先完成同一必需状态投影。旧非交易 profile 不加入 source 字段，旧业务分支和隔离审计继续原路径。

## 操作边界契约

| 边界 | 必需关联与允许变化 |
| --- | --- |
| 初始生成/定价 | 每个实际 potion entry 的代次 1、具体来源身份、配对价格事实和完整 RNG；三份记录逐一绑定上架后的实例和价格 |
| Setup/Attach | 原库存/房间身份，生成后的完整状态锚点；保留夹具声明的 GainGold/Obtain/LoseGold 及 MawBank 原生进房收益，不伪称自然来源 |
| request/dispatch/open | 当前 inventory/entry/offered/cost、root/context 及完整确认状态；过期动作拒绝，操作必需唯一有序 |
| effect-start | 完整等于派发状态；来源完整等于该库存对象，尚未持有；登记 type/model 与来源一致 |
| 默认 AfterObtained/取得返回 | 真实扣款和准确新实例、原生取得历史；新遗物完整状态等于取得前来源，其余完整状态按独立契约比较 |
| stock-start | 完整等于取得返回；Clear/Restock 依据真实 Courier 状态，只有当前原生 entry 可变化 |
| 生成→price-start | 当前 stock 子操作中新登记对象、严格下一代次；只替换所选 entry 的真实来源并推进两次 Shops，其他完整状态保持 |
| price-start→price | 来源/代次和 entry 不变；独立计算价格、折扣和一次完整 RNG 推进；只允许相应 cost 变化 |
| price→stock-return | 全部状态相等；同代次的实际新对象上架；Clear 路径 source/content/model 归空且 RNG 不变 |
| Hook start/return | start 完整等于 stock-return；只允许真实 MawBank 未购买→已购买、IsUsedUp、Disabled；其他完整状态保持 |
| callback/return/confirmed/boundary | 必需唯一、归属当前动作、严格顺序；完整绑定 Hook 尾部，receipt 指向本次真实取得/替换对象 |
| 失败 | 缺钱原生 FailureGold、不扣款、不取得、无补货/Hook；保留真实失败。未实现的拒收/替换仍显式 Unsupported，不改写成成功 |
| 原生退出/尾部 | 保留此前完整状态、房间/库存与原生退出历史校验；共同修改退出快照及后续状态仍拒绝 |

新遗物及持有列表的 id/model 必须相互一致并与登记信息一致；transactionRelics 的完整记录结构明确。来源字段缺失/null/额外字段、错误具体类型、模型、代次或对象都不使用默认值放行。

## 定向实证与开发记录

生产套件保留原 333 控制并新增 35 控制，覆盖第一/第二次补货的共同篡改、其他 entry、来源字段、代次、登记类型/模型、已持有对象复用、取得状态及 effect 来源。所有控制通过实际 `NativeEvidence.Review` 执行，损坏封装由 .NET 重新计算完整 seal，目标是语义拒绝。

此外 `.build/runs/n3d1c-repair-work01/review-repairs.json` 在新运行结果上独立重建原八例，并增加来源与取得一起篡改、第二次价格阶段、退出共同篡改及等价副本，共 14 份：13 份矛盾应拒绝、1 份等价应通过。原八例不依赖旧的硬编码对象编号。源包哈希保持。

开发中 `repair-dev01` 的原生执行成功，但新增 JSON 节点序列化未使用包含字段的 Program.Json，导致价格 after RNG 被写为 `{}`；生产 reader 正确拒绝。已修正为同一序列化配置，失败证据保留；`repair-dev02` 的遗物和连续补货读写通过。最终矩阵重新使用完整库存来源绑定后的同一最终产物，不混合开发构建结果。

特殊拾取/选择效果、卡与遗物补货、其他动态价格或替换监听器、Sozu 拒收等仍按原范围分列未覆盖。当前修复不把拒收/替换当成功，不扩展 Mod，不将有限自然采购片段称为完整 run 或胜率证据。


## 最终冻结与同构建结果

- Build ID：`1348A7FAD6C9BDA35E91EC469911FD57CC1800BB8B76CD3B19B30F9872D3163B`；独立重算一致。
- Native DLL SHA256：`58446B36A4AA5E08316BF0FD3A8A88B496649E7E75EB558C0ED6294B939BFCEE`。
- Manifest：`.build/native-build/n3d1c-repair-final01/build-manifest.json`；0 warning /0 error；61 源文件、40 编译输入、3 游戏输入、7 产物。实际 dirty 源快照：`.build/runs/n3d1c-repair-work01/source-snapshot-final01.json`。
- 同一最终产物实际完成245个原生进程/2924套件检查，另Core98。原始退出分布保持178×0、60×1、4×2、3×3；预期失败、Unsupported、预算停止单独核对，没有由外层exit0覆盖。
- 分组：交易18/471、经济23/361、旧商店25/371、4C55/561、4B34/307、4A16/273、N2 74/580（进程/检查）。368交易控制为原333+新增35；其中362拒绝、6等价通过。补充15语义副本为前述14份及1份完整类型/model一致的已持有对象复用：后者明确被 `generated object unique generation` 门拒绝；共14拒绝、1等价通过。
- 本轮真实重读830份历史证据、7个实际Harmony审计API探针/14次拒绝，目标业务调用0；都不计入245个原生执行。
- DEV_01新自然采购仍71/71，WingCharm实际支付177、合法离店、下一战实际PlayCard；DEV_02/03继续Unsupported 37/37及79/79。旧自然商店72/41/83及旧自然skip/take、真实Death、尾部夹具、重放/完整RNG和故障前缀全部同构建回归。
- 15锁定输入、52兼容源码、2622+993历史文件保留；原审查来源包及旧v1冻结产物哈希保持。无角色/安装/真实存档改动，无真实UI/Steam/网络调用，无提交推送。

汇总和收尾：`.build/runs/n3d1c-repair-work01/summary.json`、`closeout.json`；原八例及同类拒绝具体原因见 `review-repairs.json`、`coherent-alias.json`；全部原生和控制包在 `.build/runs/n3d1c-repair-final01-*`。最终校验入口 `run-final01.py`，来源/哈希检查 `inspect-final01.py`，补充驱动哈希在 `supplemental-driver-lock.json`。复跑应选全新目录以保留原结果。

本轮相对上一冻结只改六个项目源文件：NativeMerchant、NativeMerchantEconomy、NativeMerchantExpansion、NativeMerchantExpansionContract、NativeMerchantExpansionControls和run-native-n3d1c.py。新增来源观测、生产完整性门及其控制，不增加已声明范围之外的牌/遗物支持。

独立验收通过后立即按原下一交接处理奖励语义闭环和其余4D、5–6。M1尚未完成；当前没有权限或环境阻塞。
