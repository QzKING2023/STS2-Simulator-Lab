# 4D1 商店交易子阶段契约

## 移除选择与阶段状态补充（4D1 R1/R2）

下表是生产 reader 对本轮有限商店协议的边界要求；执行效果继续来自原生方法。已有商店原始成功/取消/失败证据作为合法对照，新增错误副本调用同一生产 reader。

| 边界 | 请求/归属/唯一性 | 状态及业务结果 |
| --- | --- | --- |
| Setup/Buy/Remove/Exit dispatch → open | 当前根动作/context，操作 id 唯一，decision 完整一致 | open 完整等于不可变 dispatch；setup 后的 dispatch 完整等于上一确认 |
| Setup/Buy/Exit 子选择 | 当前有限夹具不应产生子选择，出现即拒绝 | 未来真实遗物购买效果需扩展明确协议 |
| Remove 请求 | 未使用且费用可负担时恰好一次 FromDeckGeneric，父为当前根，min=0/max=1，显式 native-selector | 每个候选对应当前永久牌实例及真实型号/升级；不能只凭卡名识别重复牌 |
| Remove 答复 → 返回 | 同一个请求 id，恰好一次，先答复后返回 | 合法候选映射的实际实例一致；取消为空，成功为一个实例 |
| Remove 提前拒绝 | 已使用或费用不可负担时不要求并且不允许子选择 | 不付款、不删除、不计数、不 SetUsed；当前实际覆盖已使用拒绝，缺钱移除仍未运行 |
| 原生 OneOff 成功 → marked → Hook 进入 | 选择已经返回，成功 mark/Hook 各一次，绑定当前 entry/root/context | 当前无购买 Hook 业务变更协议下，marked、Hook 进入和交易 return 完整结构相等；仅相互相等不足以通过 |
| Remove return | 成功/取消与本次选择结果绑定 | selected = removed = 永久牌组实际缺失实例；剩余牌完整内容、升级、顺序保持；指定第二个重复实例仍是独立夹具断言 |
| Buy/Remove 真实业务区间 | open 后、return 前发生原生效果；不能伪造未发生的选择/结果 | 金币实际差额、原价、原生成功或失败、库存和具体对象连续性按分支校验；取消/失败完整状态保持 |
| return → confirmed → boundary → 最终 contract | 各操作必需唯一、顺序和 root/context/id 对应 | 所有静止边界完整结构绑定；新增未知字段的单边变化也被拒绝，跨所有状态一致的扩展可通过 |

取消、重复拒绝不会产生 marked/Hook 事件；选择不得晚于 mark/Hook 或交易返回，也不能把其他动作的旧选择当本次选择。对未来真实 `AfterItemPurchased` 业务效果，应建立 Hook 进入与完成之间的明确变化契约，不能删除完整比较或禁止真实业务变化来应付。

状态：实现及同构建回归通过，待独立验收，见[验证报告](NATIVE_RUN_N3D1_VALIDATION.md)。固定 v0.111.0；本轮仅原生商店库存、Card/Potion 购买与移除、退出。遗物取得 Hook、补货、自然地图商店、嵌套事件与精英/Boss 留在 4D 后续，不能因此关闭 M1。

所有本轮场景从正常 Neow 后通过 EnterRoomDebug 进入真实 MerchantRoom，标为 API fixture；金币/满槽等准备通过原生命令，单独声明。库存必须由原生 EnterInternal/CreateForNormalMerchant 创建。资产预加载可在固定方法处隔离；价格、随机库存、交易效果和 Hook 不替换。TestMode 的药水价格不消耗零售浮动价格 RNG、Exit 不写零售商店历史，须明确限制。

每个 MerchantSetup/Buy/Remove/Exit 根动作，必须唯一 open→原生任务及子选择→return→confirmed→boundary；成功移除另必需且唯一 marked→hook，二者位于open/return内，完整快照相等且Used=true。ID/根/context/决定绑定当前 dispatch；open 完整状态等于 dispatch，return 完整状态等于 confirmed，boundary 完整状态等于对应不可变 confirmed。根任务真实变更区间不强制全状态相等；结束后不得无新动作改变确认状态。开局和准备前的状态不假设相同。

库存投影包含 room、inventory、全部 entry 的身份/种类/价格/stock/商品身份/型号/升级、移除 Used 与总次数。策略只接收公开型号/价格/库存状态，oracle 身份用于校验。购买答复须绑定当前库存和 entry、状态票据，ignoreCost=false。原生缺钱、缺货、满槽返回 false，不得记成功；交易成功对应实际扣款和具体新卡/药水，售罄由原生 wrapper 完成。原生 Hook 替换/拒收尚无本轮明确规则时保留实际前缀并 Unsupported，不能伪造同实例成功。

移除通过原生 MerchantCardRemovalEntry/OneOffSynchronizer 和现有卡选择器。原生OneOff任务成功返回后、AfterItemPurchased之前调用原生 SetUsed 一次：固定 DLL 中 NMerchantInventory.OnCardRemovalUsed 仅转发 NMerchantCardRemoval.OnCardRemovalUsed，后者业务为 SetUsed，剩余为 UpdateVisual。额外移除次数及扣款由 OneOff 原生任务完成；不手改次数/金币/牌组。取消不收费、不置 Used；第二次移除拒绝。A0 无修饰首次费用75，下一间商店100，是独立常量断言，不代替交易时真实价格。

生产 reader 检查具体实例内容，不能只对比数量。每个已完成操作必需唯一、归属和顺序明确；缺失/重复/错配/错序、静止状态新增字段漂移、假成功/假扣款/旧商品、遗漏 Used 或取消扣款均需负控。合法取消、缺钱/满槽不成功及真实退出允许。故障发生后保留已发生原生变化及最后确认前缀，未确认根不能通过正常证据门。
