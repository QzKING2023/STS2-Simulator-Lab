# 原生商店收支账本与第三幕精英推进

2026-11-04。本轮实施证据，待独立审查。M1 未完成，最新生产构建的同构建重放未完成。

## 执行结果与版本

固定游戏 v0.111.0 / 41cef1ea、Ironclad / A0、原 DEV_01 种子及 110 动作合法前缀；之后采用现有 CombatSearchCoordinator 通用在线策略。没有 Boss/遗物专项搜索评分，也没有改角色参数。

| `.build/runs/` 目录 | 生产构建 | 真实退出 | 确认/派发 | 精确战斗差分步 |
| --- | --- | --- | --- | --- |
| `solver-shop-ledger-exec-01` | `2A10BA8CCA4FDA79E8AE15183EAB0D77151BAC5578B39067BCE05D0C368AF059` | EngineError / 1：原生 SoulNexus 死亡视觉查询空引用 | 406/407 | 318 |
| `solver-shop-ledger-exec-02` | `C31B3A2D3C8DF2A0D455A03148CC6862822E73A5219C4BAED638CF9F64D03FC2` | Unsupported / 2：精英奖励 MummifiedHand 被旧遗物准入表拒绝 | 407/408 | 319 |
| `solver-shop-ledger-exec-03` | 同 exec-02 | InvalidAction / 1：重放误用了 live 场景名，入口拒绝 | 0/0 | 0 |

第三次调用传入完整 408 动作重放输入，却使用要求恰好 110 动作前缀的 `solver-native-darv-act2`。正确场景为 `solver-native-darv-act2-replay`。这是调用错误，不是游戏动作失败；保留其原始记录，不能计为成功重放或原生路径样本。批次已到 3 次上限，没有启动第四次；后续先补正确的同构建重放。

第二次运行实际完成第三幕第五场胜利：第 9 层 `ENCOUNTER.SOUL_NEXUS_ELITE`。a407 EndTurn 原生触发胜利并清理；a408 RewardOffer 已取得 39 金币与 ExplosiveAmpoule，随后在 MummifiedHand 领取前停止。不能称整个奖励完成，不能把该派发动作标成确认。第三幕 Boss、RunVictory 尚未到达。

## 商店根因修复

旧 `NativeThirdShop` 把净金币损失当作售价。原生卡交易先加入牌组并执行取得 Hook，再扣价，最后执行 AfterItemPurchased；取得效果可能返金和改变生命。

本轮没有按遗物名称补算效果。`NativeShopLedger` 观察原生 GainGold / LoseGold 的参数与 Task 完成、Gold / CurrentHp / MaxHp 的实际同步写入、原生 AfterItemPurchased 的 goldSpent 参数及 Task 完成。MaxHp 复用既有死亡观察器的注册入口，未覆盖原生 setter 或删除审计。所有新注册仍拒绝未知 Harmony 补丁。

新完整交易契约分别核对：

- 当前交易/动作/房间和实际玩家 Creature；连续命令编号、嵌套归属与完成。
- 原生命令的实扣和实际金币写入；原生 Hook 报告实付；最终净变化。
- 生命/最大生命的独立原生写入及完整前后状态；未观测资源变化拒绝。
- 实际取得实例、型号/升级、库存变化、完整返回状态、原 RNG 约束。

旧 v1 商店证据保留原契约。新头部 `shopLedgerProfile=native-shop-ledger/v1` 必须带账本；不通过默认值把旧证据补成新认证。`NativeFullRunContract` 现在调用同一生产商店检查器；审查入口从头部读取 full-run 范围，不依赖检查进程内未初始化的运行配置。

### 实际 a381 交易

RedSkull：售价与实际支付均 154。随后 Uppercut：金币 104→119→41，原生收入 15、实付 78、净支出 63；最大生命 102→103，生命 92→93（还记录了一次原生 92→92 写入）。购买完成、合法离店、进入下一战均有记录。上述数值来自本轮生产构建，不能与旧构建的生命数值拼接作单变量实验。

收支记录的直接来源是原生命令和 setter；LuckyFysh/DragonFruit 的解释另由锁定游戏方法核查支持，不宣称记录了任意 Hook 的完整因果调用图。其他集合变化、拒收/替换、补货、尚未审计的资源链仍由有限契约拒绝。预持折扣遗物的原始价格采集仍未覆盖。

## 无 UI 死亡回调修复

SoulNexus.AfterDeath 原方法先取消 Died 订阅，再通过 NCombatRoom 查找视觉节点。空引用发生在缺失画面房间的查询，非死亡规则本身。

`NativeCreaturePresentation` 复用既有 `NativeBossPresentation` 的接收者处理方式：保留原回调与取消订阅，仅替换一处已核实的视觉节点查找调用。在隔离 full-run / TestMode / 无 NGame 且房间为空时返回空节点；有房间则调用原方法。IL 锚点必须恰好一处，记录方法 token/MVID/IL 哈希，未知补丁拒绝。未声称零售演出等价。

实际 a407 产生一次 `creature-visual-guard`，随后原生胜利、清理及 319 号差分步成立。额外实际 Harmony 注入负控拒绝通过；该负控业务执行次数为 0，只属于审计 API 证据。

## 有界验证与信任边界

- 最终检查器重读 exec-01 和 exec-02 的完整冻结原始证据通过，分别保留 partialRoot a407 / a408。检查器接受真实失败前缀不等于原生运行成功。
- 同一生产商店逻辑的 17 个有限控制全部符合预期：2 个正常/键顺序等价，15 个损坏拒绝。包括净额冒充实付、错误净额、收入/扣款/写入/Hook 记录缺失、重复命令、错误主体/根动作/商品/命令归属、错误 Hook 实付、额外状态字段、未观察治疗、Hook 提前。
- 控制从 exec-01 原始记录提取相关事件，调用生产 `--check-shop-ledger-events`；原始链与完整来源另由 `--review-full-run` 和执行锁验证。未以摘要自报值代替原始执行，也不试图重建任意恶意协同改写后的游戏语义。
- 1134 项最终 manifest 来源/产物/输入哈希匹配；15 输入、52 兼容源码未变；上一轮 4069 份原始文件哈希未变。本轮 3094 份原始文件锁定。哈希只证明一致性，不能替代执行真实性。
- 构建 4 个既有警告、0 错误。共享死亡观察器的无交易分支保留，未额外执行完整死亡矩阵；最终 M1 同构建全量门仍需覆盖。

本批 3/3 次执行（包括零派发的错误重放调用），原生进程耗时合计 463.387 秒；收口逻辑占用约 1.10 GB / 2 GiB。单进程上限 480 秒、run 上限 450 秒、内存上限 4 GiB，未触发 watchdog。无运行中的本批进程、无外部权限阻塞；未提交推送。

## 证据与复验

工作目录 `.build/runs/solver-shop-ledger-work01/`：`facts.json`、`closeout.json`、`execution-lock.json`、`ledger.json`、`ledger-controls.json`、`visual-audit.log`、`review01.log`、`review02.log`。原始失败与中间检查器的失败日志均保留。

```powershell
dotnet .build/native-build/solver-shop-ledger-final06/bin/Release/net9.0/OfflineSearchHarness.dll --review-full-run .build/runs/solver-shop-ledger-exec-02
```

重放及后续奖励范围见[下一步交接](COMBAT_SOLVER_SHOP_LEDGER_NEXT_HANDOFF.md)。不能沿用上轮重放成功结论给本轮新构建背书。
