# 4D1C 原生交易生命周期契约

实现及同构建回归通过，待独立验收。原有 4D1/4D1B profile 不变；新增 `native-merchant-transactions/v1`，当前限定无特殊拾取效果的原生商店遗物、MawBank 购买 Hook、TheCourier 药水补货。非默认 AfterObtained 与既有未知购买监听器在执行前明确 Unsupported；未声明的其他业务变化由生产检查器拒绝认证，尚未覆盖组合不得默认获得认证。

每个交易使用当前 run/房间/库存/根动作/context，决策包括 entry、当前 offered 实例与价格。合法阶段为 root-request → dispatch → n3d-open → effect-start → 可选真实 AfterObtained start/return → effect-return → stock-start → 原生 Clear/Restock 与内部价格记录 → stock-return → Hook start/return → PurchaseCompleted callback → n3d-return → confirmed → boundary。缺失、重复、错序和错误归属均拒绝。

静止点用完整结构比较。取得阶段验证扣款、确切新实例及原生历史；默认 AfterObtained 需有真实进入/返回且状态不变。清空仅允许本 entry 内容清除，补货仅允许本 entry 的新实例/价格及原生 RNG 消耗，其他库存不变。Hook 单独允许 MawBank 从未购买到已购买并禁用的原生变化，其他状态保持。回调、返回及确认必须绑定 Hook 后完整状态。失败付款路径无取得/补货/Hook，保留原状态及明确失败回调。

Setup 使用声明夹具，经原生 GainGold/Obtain/EnterRoomDebug，禁止修改库存。自然路径经原生地图到达 Attach，保留 4D1B 阶段顺序与具体房间归属。自然路径与夹具的来源、重放模式独立标注。补货后的相同 entry 不代表相同商品版本：旧 offered/价格/状态票据必须在再次派发前拒绝。

验收采用实际业务执行、冻结重放、观察对照、独立支付/Hook/实例断言及生产检查器正负控。测试预设种子和场景保留，包括失败、预算停止及 Unsupported；同一最终冻结构建执行适用旧矩阵。完整 M1、其他遗物拾取效果、卡牌补货和全部奖励替换不由本子阶段推定通过。
