# 第二幕跨幕及第三幕先古验证

2026-11-04。实施证据收口，待独立审查；第 5 步与 M1 尚未完成。

## 真实进展与构建边界

固定游戏 v0.111.0 / 41cef1ea、Ironclad / A0、`LAB_V0111_NATIVE_DEV_01`。继续正常开局形成的同 Run 动作序列，冻结前缀与在线求解阶段分别记录，没有拼接战斗状态。使用 CombatSolver 的通用 Coordinator，不新增单 Boss 评分。

| 证据 | 生产构建 | 实际结果 |
| --- | --- | --- |
| `solver-throughput-exec-01/02` | `948F980B0DB28393C173202A3C9D032439A2AB2AF7FE849B5900C69680241372` | 第二幕 KnowledgeDemon 胜利、原生奖励、第三幕 MapRoom；292 确认动作、222 差分步，原第三幕先古入口 Unsupported / exit 2；同构建重放一致 |
| `solver-third-ancient-exec-01` | `D1944708182D7FB0CA36172E283F2ACBCAF0C397FB4B9B2FF04AD6F2B2E954CE` | 336 确认/336 派发、257 差分步、第三幕两胜；q1031 药水主选择游标错误，EngineError / exit 1，尚未派发该药水动作 |
| `solver-third-ancient-exec-02/03` | `A3C548AF316FFF6538430769864CD4BBF22A698F2F0BB2C9958CD1139446E613` | 380 确认/381 派发、297 差分步、第三幕四胜；a381 实际商店购买后 DataError / exit 1；同构建完整失败前缀重放一致 |

以上目录均位于 `.build/runs/`。不同生产构建不混作同构建证据。最终生产与检查器均为 `solver-third-ancient-final02`。本轮构建 4 个既有警告、0 错误。

第三幕实际胜利并领奖的 Encounter 为 DEVOTED_SCULPTOR_WEAK、SCROLLS_OF_BITING_WEAK、OWL_MAGISTRATE_NORMAL、SLIMED_BERSERKER_NORMAL。当前到第三幕第 7 层 MerchantRoom；全 Run 已记录 18 次战斗胜利及清理。第三幕 Boss 与 RunVictory 未验证。

## 复用、实现与实际覆盖

- 先古：复用原生事件选择与 Darv 的完整拾取状态校验，独立桥接 Nonupeipe。读取原生三个选项、稳定实例、锁定状态和拾取标志，实际选择并取得 DelicateFrond；原对象与取得对象引用一致，事件结束及完整状态/RNG 受检。另两个选项没有实际拾取证据；`HasUponPickupEffect=false` 本身不保证其他任意 Hook 无效果，仍由完整状态门拒绝未覆盖变化。
- 药水：修正 `NativeSolverLoop` 使用上游 `Prepare/Complete` 的主选择参数与 `NestedChoices` 游标协议；通过既有 `NativeChoiceSession.ResolvePlannedCards` 解析生成候选，不另造搜索或默认首项选择器。q1031 / a337 是求解器实际采用 AttackPotion → Mangle，原生候选另有 Breakthrough、Anger；派发、生成对象、药水消耗、完整 continuation/RNG 与 fork 隔离一致。
- 通用能力准入复用上游生成卡药水识别；本轮仅实证 AttackPotion 单主选择。其他生成卡药水、空选择跳过、嵌套 Hook 选择不宣称已覆盖。
- RandomForeseer 的现有 Nonupeipe/BeautifulBracelet 预测入口已核查，本次没有执行该选项或预测；不将真实原生选择称为预测认证。
- CombatSolver / RandomForeseer 既有固定 MIT 依赖及归属保持。未修改 Solver DLL、搜索评分或角色代码。

## 搜索档位、并行与全局预算

核查了上游纯设置接口及预置：Low 45/60000/60 秒、Medium 60/120000/120 秒、High 90/250000/180 秒、VeryHigh 135/500000/300 秒（beam/nodes/time）。此前 Lab 实际使用 Custom 与 Coordinator 覆盖参数，不等于 UI 预置已生效。本轮保持 Coordinator 的 8 beam / 256 nodes / 10 秒配置，仅显式接通 DOP 2；15 秒取消与返回工作量门继续存在，返回门不是精确节点熔断。

单根 DOP 1/2 探针各展开 110819，约 316/284 ms，实测最大并行 2；父状态/RNG 不变，探针结果不采用。这是单根有顺序样本，不能推算整体加速。

单场搜索、整局动作/查询、run 时间及宿主进程预算分别记录。原 120 秒默认入口保持；显式 full-run v2 允许 run 450 秒、进程 480 秒，进程内存上限 4 GiB。当前批次 3/3 次执行、原生耗时合计 320.501 秒、逻辑证据占用 1422466530 / 2147483648 字节（含控制副本逻辑大小），无 watchdog 触发，已关闭。不得将第三次 121.759 秒重放误记为旧 120 秒入口失效。

## 检验与证据

- 最终 live/replay 的 route、choices、reward-choices、final-state、先古两份记录以及全部 297 个步骤，共 303 个 JSON 结构一致；包括完整状态/RNG、父 fork 不变及真实失败根动作。a381 没有确认后状态，未伪造补齐。
- 原生生成选择回调中 10 个拒绝控制、2 个合法重复控制通过；控制未增加派发，原状态不变。该回调出现时 a337 根动作已经派发，不能称零根动作派发。
- 实际生产 `--review-full-run`：先古与生成药水各 9 个有限控制（各 2 个正常/等价、7 个损坏）。覆盖实例、型号/升级、来源、请求归属、缺失记录、选择 key 与完整状态矛盾。正常副本通过、损坏副本拒绝；这是证据检查控制，不是新增原生样本。
- 1126 项最终构建 manifest 路径/哈希匹配；15 输入、52 兼容源码未变；旧 throughput 的 2064 份原始文件未变；本轮 4069 份原始文件锁定。哈希证明来源一致，不替代执行真实性审查。
- q857 终态 Hook 原生回归已通过；旧失败参考有弃牌顺序差异，不能称严格单变量对照，未删除这些差分字段。

主要文件：`.build/runs/solver-third-ancient-work01/{facts.json,closeout.json,execution-lock.json,ledger.json,ancient-controls.json,generated-controls.json}`；旧阶段 `.build/runs/solver-throughput-work01/`。复验入口：

```powershell
dotnet .build/native-build/solver-third-ancient-final02/bin/Release/net9.0/OfflineSearchHarness.dll --review-full-run .build/runs/solver-third-ancient-exec-02
```

生产读器接受已正确记录的失败前缀，不会将原生 exit 1 变成业务成功。

## 当前失败与下一步

a381 中先购买 RedSkull，随后 Uppercut 原价 78，金币 104→41，净支出 63。原生 LuckyFysh 在卡入牌组时返还 15 金币；DragonFruit 的得金 Hook 又改变最大生命。原生商店流程先入牌组及 Hook，再扣售价。因此 `净金币减少 == 售价` 是 Lab 的错误假设。原始代码调用及状态提供解释，但尚无专门逐 Hook 收支账本认证。

此动作已发生真实业务变化，仍保留 380 确认/381 派发及 DataError，不能回滚或假装未派发。下一步应复用原生扣款与 `AfterItemPurchased(...,goldSpent)` 观察，分别核对售价/实扣、取得 Hook 收入、净变化与实例，见[商店交接](COMBAT_SOLVER_THIRD_ACT_SHOP_NEXT_HANDOFF.md)。没有外部权限阻塞；本批资源门已收口。原生全流程、第三幕终态及最终 M1 同构建全量门仍待完成。
