# 4D1 之后实施交接

4D1 R1/R2 已通过 [最终独立验收](NATIVE_RUN_N3D1_FINAL_REVIEW.md)：558 次生产检查器执行全部符合预期，原十个反例闭合；最终原生证据为 204 进程 / 2092 检查及 Core98。当前仍只覆盖 Card/Potion/移除/退出 API 夹具，不代表完整 4D 或 M1。

下一轮直接执行 [商店价格、RNG、历史与自然进出最小交接](NATIVE_RUN_N3D1B_IMPLEMENTATION_HANDOFF.md)。先闭合固定原生经济/历史语义，再完成同 Run 正常开局的合法商店进出；后续其余任务保留在下列清单。用户全 M1 持续授权有效，不再逐步请批。历史问题与修复分别见 [首次审查](NATIVE_RUN_N3D1_REVIEW.md)、[修复报告](NATIVE_RUN_N3D1_REVIEW_REPAIRS.md)。

## 接续顺序

1. 4D1 最终独立审查已通过，保留冻结和历史证据。下一阶段从下项商店经济/历史及自然进出开始；不重跑已完成审查来替代新功能推进。
2. 下一最小子阶段优先闭合商店经济/历史语义及自然进出：审计固定原生 Potion CalcCost 的价格抖动/Shops RNG 分支、MerchantRoom.Exit 的未购商品历史与实际消费者；保留原始算法和历史入口、宿主隔离，然后给正常开局同 Run 合法地图入店→原生交易/取消→合法离开。不能全局关闭 TestMode 或重写价格公式。之后补原生 Relic 购买及真实 OnPickup 子选择/奖励、TheCourier 原生补货、动态价格/拒收、实际下一店移除价格。复用原生 MerchantInventory；当前卡/药水取得替换与补货仍为显式停点，需要真实结果协议及 Hook 业务变化区间，不能直接删除有限契约的完整状态检查。
3. 在共享奖励桥闭合 Driftwood 原生 reroll 第二菜单、CardPileAddResult 实际替换/拒收/modifyingModels、Sozu NotAllowed/TooFull、旧已知seed的Neow连续CardReward迁移、SpoilsMap修改金币来源账本。每条各有原生实际证据与完整父子生命周期；不能以修改奖励或换成功种子规避。
4. 原 DEV_03 WhisperingHollow 同种子继续推进；事件内战斗/奖励/恢复必须持有原生父任务与房间栈，不能拆成平级根或猜 Resume。原旧77/78 Unsupported证据保留。
5. 精英/第一幕Boss真实战斗及原生幕末奖励/act-ready，fixture与自然分列；随后步骤5自然跨幕完整run、步骤6预登记批次和可信数据交付。死亡不是完整胜利路径的替代。

## 复用与具体注意事项

- 当前 `NativeMerchantContract` 为有限无修饰交易协议，成功后要求原商品同实例入库且售罄。新Hook会有合法修改/拒收/补货时，应建立明确业务区间及原生结果记录，扩展生产契约和正负控，不直接删除完整性比较。
- 当前药水商店夹具覆盖空槽购买和满槽拒收；有空洞的槽位、指定槽丢弃后重试尚未验证，后续复用4C已有完整capacity/slot/实例投影和原生命令，不能只以紧凑列表数量替代槽位语义。
- `FromDeckForRemoval` 固定版本只是调用 `FromDeckGeneric` 并提供原生 IsRemovable/filter/sort。现有叶请求拥有真实选择；不要再添加第二个虚假包装选择。
- 原生 `NMerchantInventory.OnCardRemovalUsed` 仅转发 UI 节点；`NMerchantCardRemoval.OnCardRemovalUsed` 业务为原生 SetUsed，非视觉职责已由本轮原生OneOff返回后、AfterItemPurchased之前补调。次数和扣款由 OneOffSynchronizer 完成。未来不要重复增加次数或收费；跨店费用需在新原生库存实际证明。
- TestMode 药水价格不取浮动 RNG、商店 Exit 省略零售历史；当前只提供真实TestMode记录。这是自然完整run前必须核验的经济/RNG及历史消费边界，不能只写限制后宣称零售语义。4D1后续应验证固定原生方法内启用真实价格分支的窄路径（保留原生算法与Shops RNG），以及游戏逻辑是否消费退出历史；需要时补原生非视觉责任。不得全局关闭TestMode、放宽保存/Steam隔离或用独立重写价格替代原生逻辑。
- `merchant.closed` 是 Lab 会话守卫，不意味着当前模型已被从 run 栈移除；真正 Travel 仍须复用原生 Hook/房间结束/地图合法边。
- 延续 request/result 必需唯一、父子归属与顺序、不可变静止完整快照、真实业务区间、实际新增/替换/拒收实例、完整记录 RNG、失败前缀。不同状态字段新增后不能绕过检查。
- 固定 v0.111.0、Ironclad/A0、无UI、MemorySaveStore、Steam/真实保存隔离、一run一进程。15 Mod输入/52兼容源码冻结，不扩Mod专项或公开SDK，不提交推送。

这些是后续实施任务，本文没有声明已执行。后续允许再按清晰有界子阶段串行交付，但最终必须关闭总M1验收队列。
