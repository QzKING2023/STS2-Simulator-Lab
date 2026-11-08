# 4D1B 商店经济与自然进出契约

## 库存生成阶段与具体房间归属

自然 Travel 统计必须先于库存价格生成；阶段单向推进，禁止统计与价格穿插、生成后补统计、退出返回后继续生成。首次 Neow 与有旧 session 的 Travel 分别按真实调用要求阶段。

自然生成的当前 MerchantRoom 身份从第一次价格观察保留至生成确认、实际到达和 Attach。Attach 决策、当前 rooms.reference、session、merchant.room、nativeInventory 必须绑定同一实际房间及库存；身份声明类型也必须相符。绑定后保留原商店归属，合法进入下一战后不把旧 merchant.room 等同新当前房间。Setup API 夹具按实际返回绑定房间/库存，不伪称自然到达。见 [本轮修复与证据](NATIVE_RUN_N3D1B_OWNER_PHASE_REPAIRS.md)。

## R1–R3完整绑定补充

价格start/return按所属生成根动作/context形成唯一三段完整状态链，最后状态绑定原生父动作confirmed；自然Attach仅引入同一实际库存投影，不改变其余主状态。价格阶段不再构成两个彼此自证的快照。每次适用Travel必须完整完成本次stats对、旧会话完成及新房到达，下一根请求/最终收尾不允许遗留阶段欠账。MerchantTravel结果的closed/from/to必须匹配实际动作和派发/返回状态；有限结果结构缺失/额外字段拒绝。详见 [修复实现与证据](NATIVE_RUN_N3D1B_REVIEW_REPAIRS.md)。

本契约属于固定原版 v0.111.0 的内部验证协议。原生执行与证据检查分开：检查器不执行交易、不生成库存或历史。旧 4D1 TestMode 夹具继续按旧 profile 回归，新 profile 显式恢复下列三个固定方法中的局部 TestMode 业务分支，全局 TestMode、MemorySaveStore、保存/Steam/网络隔离保持。

| 原生方法 | 局部分支 | 原生效果与边界 |
| --- | --- | --- |
| MerchantPotionEntry.CalcCost | IsOff 视为 true | 原生价格抖动、取整与 Shops RNG，保留其他 IL |
| MerchantRoom.Exit | IsOn 视为 false | 原生记录未购买的卡、遗物、药水选择 |
| RunManager.UpdatePlayerStatsInMapPointHistory | IsOn 视为 false | 原生保存当前金币、HP、MaxHP 到本节点历史 |

每个目标先核验固定游戏程序集、无已有补丁和唯一 TestMode 调用锚点；记录方法 token/MVID/原始 IL 哈希、配置和匹配数，注册纳入宿主稳定性检查。不全局替换 TestMode getter，不调用真实零售展示或平台路径，不在 Lab 重写业务效果。历史条目仍由正常 RunState.AppendToMapPointHistory 生成。

## 操作与完整状态边界

| 边界 | 归属/必需/顺序 | 状态与业务契约 |
| --- | --- | --- |
| 所有 root dispatch | 绑定当前动作/context；已确认状态不可变 | 与最近确认完整比较；原生战后奖励生成另有明确区间 |
| 原生 reward-offer | 必须有此前真实 Victory cleanup，恰好消费一次生成区间 | 允许原生奖励生成消耗 player RNG；其余完整状态保持，后续动作绑定 offer 状态 |
| 每次价格 start → return | 同一 entry、root/context，唯一且顺序固定 | start.before 绑定 Shops；完整状态只改变该 RNG；检查所有 xoshiro 状态字、counter 与独立价格计算；价格结果再绑定实际库存 |
| 自然 room-entered / leave-ready | 每个会话唯一、当前实际房间，完成后才可 Travel | 静止完整快照；缺失、重复、借用旧会话拒绝 |
| Travel dispatch → room-exit-start | 当前会话/动作唯一 | 完整静止比较；目标必须来自公开合法地图候选 |
| 原生 stats start → return | 当前 Travel；先 start 后 return，唯一 | 节点坐标已由原生 AddVisitedMapCoord 更新；只更新本历史的 current_gold/current_hp/max_hp，其他完整状态保持 |
| 原生 Merchant Exit start | 绑定旧库存/商店与外层 Travel | 从 stats return 仅允许原生楼层推进与房间出栈；专用 fixture Exit 则直接绑定 dispatch |
| 原生 Merchant Exit return | 同一 root/context/库存/房间，恰好一次 | 商店会话关闭；历史精确追加剩余库存，保留全部旧历史及其余完整状态 |
| Travel return → confirmed → room-complete | 同一请求/会话，完整且唯一 | 实际目标、楼层、房间栈；静止记录绑定完整返回状态；不能只设置 Closed 冒充离开 |
| 下一 room-entered → PlayCard | 旧商店已退出，新房间是实际战斗 | 原生合法动作执行并确认；不是只记录“可行动”或搜索预测 |
| 子阶段 contract | 原生实际前缀全部确认且未留下待完成子操作 | 绑定最近完整确认状态；夹具/自然来源与 live/replay 模式分别记录 |

商店 Setup/Attach、Buy/Remove/Exit/Travel 的 open/return/confirmed/boundary、具体移除选择和 SetUsed/AfterItemPurchased 顺序继续使用已验收的 4D1 契约。Attach 只接管正常原生进入时已生成的库存，不创建或填充库存。当前自然策略采用最短公开地图距离、最便宜可负担卡，必要时实际取消移除；不保证最优或完整 run。

## 历史内容与实例

新 profile 的永久牌组和商店卡记录原生 SerializableCard 完整内容，历史仍保留其原生较弱身份。Lab 用当前库存实例关联原生型号/序列化内容，不能声称游戏历史本身含 Lab 实例 ID。

- 购牌的原生取得历史在设置 FloorAddedToDeck 前序列化，因此与交易前 offered 卡内容对应；最终永久卡另有实际入牌楼层。不能把两者不同当作差分错误，也不能只比较型号/数量。
- 购药验证原生被选 potion history 与 bought_potions、付款和实际新药实例；移除验证原有永久卡的完整序列化内容；取消的完整历史保持。
- Exit 按原生顺序追加尚有库存的 character/colorless 卡、遗物和药水，was_picked=false。已购内容不能混入未购列表，原有历史不能被修改或清空。
- 自然 Travel 的旧商店历史在 Exit 完成前保留，下一节点的原生新条目在之后产生；会话 Closed 只负责拒绝旧操作。

## 正负控与范围

控制副本均重新封装有效 seal，再使用同一生产 reader 验证语义；不能以校验链损坏代替语义拒绝。覆盖缺失、重复、乱序、错 root/context/session、缺/null/错误类型 state、协调篡改完整快照、错误价格/RNG/历史型号/升级/付款/顺序。属性重排及所有对应状态一致增加扩展字段作为合法等价控制。

四个经济夹具明确使用 GainGold/EnterRoomDebug；三个预登记自然种子没有这些准备，保留正常 Neow、实际战斗/奖励、合法 Shop 和下一战动作。固定 seed 重放是复现证据，不是独立样本；观察不扰动比较完整状态和 RNG。新 profile 只恢复这里列出的三个业务分支，不能据此宣称所有 TestMode/零售差异已消除。

遗物购买、真实 Hook 业务变化、补货/替换/拒收、奖励闭环和完整跨幕流程仍属后续 4D/5–6。出现当前未声明效果时应保留真实前缀并 Unsupported，不得跳过效果或删除完整状态比较。
