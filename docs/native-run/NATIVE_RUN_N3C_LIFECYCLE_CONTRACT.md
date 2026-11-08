# 4C 有限父子生命周期与结果契约

日期：2026-11-02。用于[剩余修复复审](NATIVE_RUN_N3C_REPAIR_REVIEW.md)的根因收口。范围是当前原版原生奖励、宝箱和药水 profile，全部为内部协议。本文枚举当前操作，不提供公开 SDK，也不声明后续节点已经实现。

## 所有操作共用的边界

| 边界 | 归属、顺序、实例与快照 |
| --- | --- |
| 根 dispatch → 根 open | ID 唯一，parent 为当前根；kind 与派发相同，subject=null；完整 state 与 dispatch 相等 |
| 父 → 子 open | 父已 start 且未 return，父必须是当前 LIFO 栈顶；子 kind 在有限拓扑中；业务实例 subject 按下面的来源绑定 |
| open → answer → start | 有候选的操作必须唯一答复后 start；handle 与当前候选实例相同；无选择操作 options=null 且不得有 answer；两边完整 state 等于不可变 Open |
| start → 子任务/真实业务 → return | return 必需、唯一、栈顶归属相同；子操作全部先闭合。业务执行区间允许真实状态变化；每个结果按下面的有限结构校验 |
| return → confirmed → boundary | 根已返回且栈空；confirmed 完整绑定根 ReturnedState，boundary 必需唯一且绑定当前根/context/操作/confirmed。下一根不能跨过未闭合 boundary |
| fixture 声明/断言、card-continuity、最终 contract | 位于确认后的静止观察点，绑定不可变 confirmed；fixture 来源、必要独立断言、真实永久卡/战斗 DeckVersion、场景闭合要求分别验证 |

完整 state 使用结构比较，包括未知新增嵌套字段；属性顺序不影响相等，数组顺序仍有意义。身份必须先登记且按引用区分实例。缺失、null、非 object state 由共同的生产快照门拒绝。结果结构中的必需字段读取失败同样使检查失败，不补默认值。

## 当前全部父子区间

下表中“静止”表示从父开始或最后子返回到下一子开始完整相等；“业务”表示保留原生执行区间，不强制状态相等。每行的子 open 都同时应用上表的 LIFO、父 start、唯一身份约束。

| 父 kind | 合法子 kind / 来源 | 进入子前的状态 | 最后子返回 → 父返回 | 结果/完成约束 |
| --- | --- | --- | --- | --- |
| RewardOffer | 恰好一个 reward-set / 原生 RewardsSet 实例 | 业务：Offer/Populate 可生成候选和改变 RNG | 业务：等待完整原生 Offer 返回 | completed=true；集合必需完整返回 |
| EventOption | 可有 reward-set / 原生事件实际触发的集合 | 业务：事件可先改变资源再 Offer | 业务：事件仍可继续执行 | completed=true，普通事件选择/房间契约另行校验 |
| NativeTravel | 无 4C 子操作 | 原生 Travel/入房业务 | 业务 | completed=true；房间/战斗观察另行校验 |
| RewardSetup | 无 4C 子操作 | 明确标记的原生 API 夹具建立业务 | 业务 | completed=true，不能冒充自然路径 |
| UsePotion | 恰好一个 potion-use / 派发指定药水实例与槽 | **静止** | **静止** | completed=true；不能省略真实使用子任务 |
| TreasureOpen | 顺序唯一 treasure-normal、treasure-extra / 派发 room | **静止**；normal 返回绑定 extra 进入 | **静止** | completed=true，normal/extra 均已完成 |
| TreasurePick | 恰好一个 treasure-choice / 派发 room | **静止** | **静止** | completed=true |
| TreasureExit | 恰好一个 treasure-exit / 派发 room | **静止** | **静止** | completed=true，原生 picking 已关闭 |
| reward-set | 零个或多个 reward-item / 同一个集合 subject | **静止**：Open/最后 item ReturnedState | set-completed → return **静止** | 原生集合 ID 唯一；set 声明必需唯一；已全部领取或合法 skip；complete=true |
| reward-item，选 CardReward | 恰好一个 card-menu / 当前选中奖励实例 | 业务：Populate/候选生成 | **静止**：原生 OnSelect 结束后回到 Collect | success 与子菜单一致；acquired 与子结果完全相同 |
| reward-item，选 RelicReward | 可有 reward-set / OnPickup 的原生嵌套 Offer | 业务：实际取得及 Hook | 业务：Hook/取得后续可继续 | 真正取得的实例与 offer 内容/新库存对应 |
| reward-item，选 Gold/Potion/Skip/Discard | 无 4C 子操作 | 原生选取、支付、采购或丢弃 | 业务 | 具体结果见下一节 |
| card-menu | 可有 reward-set / 原生取得 Hook 的嵌套 Offer | 业务：选卡/alternative/取得 | 业务：等待完整 OnSelect | 真实候选来源、AfterSelected、实际取得对象与 deck 对应 |
| potion-use | 无 4C 子操作 | 原生 EnqueueManualUse/ActionExecutor/OnUseWrapper | 业务 | 确切 consumed/target 对应派发，槽中原对象消失；效果独立契约按场景核验 |
| treasure-normal | 无 4C 子操作 | DoNormalRewards 的原生金额与 RNG | 业务 | gold、实际金币 delta 与前后 state 对应；当前 native-unmodified |
| treasure-extra | 可有 reward-set / 原生额外奖励 | 业务：原生任务/Hook | 业务 | completed=true |
| treasure-choice | 领取时恰好一次 treasure-obtain，skip 时零次 / 选中 canonical | 原生同步器投票/通知和队列处理 | 原生 FinishedExecutingActions/obtain 等待区间 | skip、notifications、实际取得次数一致；不把通知当取得 |
| treasure-obtain | 可有 reward-set / RelicCmd.Obtain 的原生 Hook | 业务：ToMutable/原生取得/Hook | 业务 | canonical → created → acquired 的同实例模式，取得前不存在、后库存存在，模型匹配 |
| treasure-exit | 无 4C 子操作 | 原生 room.Exit | 业务 | pickingClosed=true |

普通选择/Bundle、任务 fault、战斗历史和房间生命周期由既有契约同时校验。表中允许业务变化的区间并不保证每个中间字段的全部原生因果都可重建；它们提供实际状态与执行来源，不把全父子状态强行相等当作完整语义验证。

## reward-item 取得字段

| 选中种类 | 外层 acquired | 独立对应 |
| --- | --- | --- |
| CardReward | 实际原生 OnSelect 完成后，由该奖励对象对应的 card-menu 返回本次实际取得卡；alternative 未取卡为 null | 严格等于子菜单 acquired；子菜单要求 selected/sourceCards/acquiredCard/deck 的实例/模型/升级一致，原 deck 中不存在该新对象 |
| PotionReward 成功 | ClaimedPotion | 对应 offered content，取得前不在槽内，后在真实槽内 |
| RelicReward 成功 | ClaimedRelic | 对应 offered content，取得前不在库存、后在库存 |
| GoldReward、SkipRewards、DiscardPotion | null | Gold 不报告对象；skip 合法性；discard 确切原对象从槽中消失 |
| PotionReward 满槽失败 | 原生结果，当前夹具未取得对象 | 满槽、槽结构无变；可继续原生 discard/retry，不能记成功采购 |

本次 CardReward 的外层字段由运行记录直接变为实际卡引用，取代旧 4C 外层 null 布局；通过实际奖励对象键控的完成记录传回 Collect，嵌套 Offer 不覆盖其他奖励结果。最后仍有未消费完成记录时场景不能声明闭合。新 reader 不承诺旧 4C 包布局兼容，历史原证据保持不变，N2/4A/4B 分别回归。

## 验收与后续边界

17 种 operation kind 各自覆盖整棵子树缺失、start缺失/重复、return重复、归属错误、parent错误、return提前。现有 state 存在性控制保留；六组具体子操作/领取或跳过路径分别验证 entry/return 的金币、RNG及未知字段漂移。每种受测业务都保留正常、重放和结构等价控制。卡取得加入旧对象、null、缺字段、错误来源和跳过假取得控制。所有损坏都必须由生产 reader 拒绝，不能仅靠测试脚本判断。

reroll/多菜单、实际 CardPileAddResult 替换或拒收、Sozu采购拒收、Neow新会话迁移和修改后宝箱金币账本仍按[4D交接](NATIVE_RUN_N3D_IMPLEMENTATION_HANDOFF.md)关闭。当前同实例取得保证不覆盖这些情况，不能把未支持的取得记成成功新增。当前自然24动作仅为跨战片段；M1步骤4D、5、6继续待执行。
