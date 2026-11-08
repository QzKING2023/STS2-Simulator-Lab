# 第三幕原生商店收支语义：下一实施交接

2026-11-04。M1 既有范围内，承接[本轮证据](COMBAT_SOLVER_THROUGHPUT_THIRD_ANCIENT_VALIDATION.md)，不要求新增角色或单 Boss 策略。当前批次已经关闭，不复用其剩余为零的执行额度。

## 固定起点

- 生产/检查器 `solver-third-ancient-final02`：`A3C548AF316FFF6538430769864CD4BBF22A698F2F0BB2C9958CD1139446E613`。
- `.build/runs/solver-third-ancient-exec-02/03`：380 确认/381 派发、297 精确步骤，同构建重放到真实 DataError；不得用中间构建替代。
- 同 Run 第三幕四胜，到 MerchantRoom。未到第三幕 Boss / RunVictory。
- 保留 15 输入、52 兼容源码；不改游戏/角色/存档/UI，不提交推送。原有 MemorySaveStore、平台隔离与 Harmony 审计不弱化。

## 根因及优先复用

`NativeThirdShop.cs` 已调用真实 `OnTryPurchaseWrapper`，但将成功交易的净金币损失等同售价；`NativeThirdShopContract` 同样要求该关系并限制 HP/maxHP 不变。a381 的 Uppercut 购买触发 LuckyFysh +15 金币及 DragonFruit 最大生命变化，原生交易成功后被 Lab 错误拒绝。

先复查固定游戏的 MerchantCardEntry / MerchantEntry 与上述真实 Hook，不把本地反编译源码复制到项目文档或发布。复用 `NativeMerchant` 既有 `Hook.AfterItemPurchased` 观察入口，其原生 `goldSpent` 参数及完成边界；按需要观察原生资源命令的实际前后变化。记录必须区分原生扣款、入牌组引起的收入/生命变化及最终净额，不能仅把净差改成某个常量或按遗物名补偿。

## 最小实施与验收

1. 定义一次交易的请求、实际原生扣款/Hook、返回契约：当前 root/room、商品/取得实例、售价、原生报告实扣、回调次序、完整 before/after 与 RNG。真正业务变化按观察链解释；未知效果显式停止。
2. 修改运行断言与生产检查器为同一有界契约，仍要求成功/失败回调、库存、实例及完整状态匹配；不删除状态字段或单靠多个自报数字相等证明真实发生。
3. 有限正负控：普通无 Hook 购买、实际带拾取收入购买；错误售价/实扣、错 root/实例、缺失或重复记录、伪造收入/最大生命变化及合法资源变化。优先复用现有原始失败证据，涉及新增采集则做最少受影响原生运行和同构建重放。
4. 先登记新的执行次数、总时间、存储与单进程停止门，使用独立 ledger。保留旧 DataError 和未确认 a381，不重置证据；成功后继续自然合法节点至实际终态或明确停止，不专门调整战斗评分。

既有折扣遗物的原始价格采集仍是独立未覆盖项，应按实际影响验证，不能用本次返金样本代替折扣认证。RandomForeseer 继续仅在已核实的单节点能力范围使用；预测未知不阻断可安全原生执行，但必须标明无预测差分。最终 M1 同构建全量验收仍保留。
