# 第二幕自然推进与生命周期：分层验证

日期：2026-11-03。状态：**本轮实现和分层检查收尾，待独立审查。完整 M1 尚未完成，第二幕 Boss 仍未胜利。**

本轮从已独审通过的206动作自然切片继续，见 [实施范围](NATIVE_RUN_ACT2_IMPLEMENTATION_SCOPE.md)。当前自然路径最远达到第二幕第6层，真实死亡；没有把死亡或专用夹具当作完整通关。

## 实际新增能力与结果

- `NativeRewardContract` 按 TreasureRoom 实例保留宝箱阶段和取得次数，后续房间不能复用旧箱的选择/退出。历史单箱正常、跳过和 SpoilsMap 夹具继续通过。**同一自然 run 连续两个宝箱尚未实际覆盖。**
- `NativeMajorContract` 以 `ActScope` 保存每幕目标、奖励与 ready/跨幕阶段，全 run 的原生计数保持累计。当前实际证据验证第一幕完成、第二幕作用域建立和持续战斗；**第二幕 Boss 后的第二次跨幕分支尚未实际覆盖。**
- 新增内部自然 profile `major-natural-act2`，当前仍是“尝试第二幕至第三幕地图或真实失败”的有界控制器，不是已完成的任意幕控制器。
- 补齐原生 `SpecialCardReward`。它在固定版本中用于返还被盗牌等指定卡奖励，原生 `OnSelect` 调用 `CardPileCmd.Add`。Lab 提交真实奖励选择，记录原始卡、实际取得实例、升级、原生序列化及入牌组楼层；没有手动 append 卡牌。

最终保留策略为 `oracle-legal-natural-act2/v2`。第一幕策略沿用旧切片；第二幕在已覆盖的 FirePotion 合法目标满足固定条件时通过原生动作使用药水。策略是有界启发式，没有最优性或公平信息胜率承诺。

| 预登记种子 | 实际终局 | 确认/派发 | 最远位置 |
| --- | --- | --- | --- |
| DEV01 | Death | 93/93 | 第一幕第8层 |
| DEV02 | Death | 156/156 | 第一幕第7层 |
| DEV03 | Death | 277/277 | 第二幕第6层，MapCoord (0, 5) |

DEV03 共14场实际战斗胜利，其中第二幕4场，均有原生奖励记录。第二幕序列为 BOWLBUGS_WEAK → THIEVING_HOPPER_WEAK → CHOMPERS_NORMAL → BOWLBUGS_NORMAL；随后在 THE_OBSCURA_NORMAL 死亡，HP0、Gold403，最后确认边界 `EndTurn:277`。

`a206` 建立第二幕作用域，记录实际地图 generation 2、目标 KAISER_CRAB_BOSS；这不是 Boss 已被执行的证据。`a216` 实际领取返还牌奖励，取得 CARD.INFLAME 的原对象 i448，永久牌组序列化记录入组楼层20。来源、具体对象和升级/内容均有生产校验。

## 构建身份与分层范围

| 用途 | 冻结与目录 |
| --- | --- |
| 11次最终原生执行的生产构建 | `6FF6F3528D673DEC235644E08AC7FA35F91E7BD20302CA937263CBC791182994`，`.build/native-build/native-act2-final02/` |
| 最终生产检查器及当前源码 | `EE0A62AF261D0F9A9786338F130BBCC98ED293F0E434DAFFE16D16DE41C3490F`，`.build/native-build/native-act2-final03/` |
| 最终检查器 DLL SHA256 | `D2922F8AABDA74721C1F61A24FE7BBA88DD4C2AE5E4B2CA936ED68AD12AD460F` |

final02 → final03 的53个编译输入中，仅 `NativeRewardContract.cs` 不同；它只属于证据检查路径，原生生成链、采集器和策略源未改变。最后修复是把特殊牌奖励的 `card.reference` 与实际 offered content 绑定。按 [分层验收](../architecture/NATIVE_RUN_EVIDENCE_TRUST_BOUNDARY.md)，复用了已完成的原生执行，用最终检查器重验其原始证据并执行正负控，没有再重复11次原生运行。**不能把这些原生执行改称 final03 生产，也不能把本轮分层门称作最终 M1 同构建全量门。**

当前冻结有89个源码输入、53个编译输入、3个游戏输入、7个产物；构建零警告、零错误。dirty 源码副本在 `native-act2-work01/source-final03/`，313项冻结/保留检查通过。15个 pinned 输入与52个兼容源码保持。Core 源码及 DLL 与先前 Core98 证据相同，本轮复核哈希并复用该证据，没有宣称重新执行 Core98。

## 验证矩阵

证据根目录：`.build/runs/native-act2-work01/`。

| 检查 | 结果 | 范围 |
| --- | --- | --- |
| `.build/runs/native-act2-final02/summary.json` | 11原生执行、91检查符合预期 | 三种子 live/replay；DEV03 minimal/off；旧206动作；原版 skip24/take25 |
| `full-frozen-evidence-review-final03/summary.json` | 346/346符合预期 | 323旧基线 + 上轮12 + 本轮11原始证据；不新增原生执行 |
| `controls-final03/summary.json` | 18/18符合预期 | 15损坏拒绝 + 3正常/等价通过 |
| `previous-controls-final03/summary.json` | 24/24符合预期 | 上轮独审21损坏 + 3正常/等价控制重验 |

346份原始证据中，262份完整切片/合法终态通过，84份原错误或未完成证据按预期拒绝。后者为53 InvalidAction、15 EngineError、8 Unsupported、5 BudgetExceeded、3 DataError，均保留原始退出和生产构建身份，不计作成功路径。

本轮11次原生进程 raw exit 均为0，因为 Death 是合法完成的终局。三种子都没有完成两幕胜利，不能以外层检查通过推导游戏通关。三个 live 各自独立进程重放；DEV03 的 minimal/off 也重放同一真实轨迹，完整比较 route、choices、reward-choices、final-state 以及554个 dispatch/confirmed 前后完整状态，含完整 RNG，未只比较 RNG 计数。

新增控制针对作用域必需/唯一/当前动作及完整快照、实际特殊奖励源/取得实例和升级/序列化、宝箱房间归属与退出/通知、Death 事实。控制由生产检查器读取重新封签的副本；一次特殊奖励来源引用反例在 final02 被误接受，final03 修复后拒绝。正常旧宝箱缺少后期 `rooms` 投影的问题，也按旧格式实际记录边界修复：旧夹具仍须登记 TreasureRoom 类型并与动作/子操作绑定；当前 Major profile 必须具有具体房间投影，未以默认空对象绕过。

18项新控制使用保留的dev04/v2真实Death轨迹与旧宝箱API夹具的副本，保留各自producerBuildId；它们不是新的原生执行。原始文件和失败记录保留。本轮没有放宽补丁审计、删减状态差分或将不完整 Mod 事务接入主线。

## 保留的开发失败与策略试验

- dev01 编译失败，未执行游戏；后续修复局部变量赋值和计数后零警告构建。
- dev02 在220个确认动作后遇到 SpecialCardReward，221次派发、raw exit2、Unsupported；保留真实失败前缀。
- dev03 在264动作后真实 Death，暴露读侧在新幕仍要求旧局部 Ancient 标志的问题；后续作用域归属修复。
- v2、v3、v4 策略开发结果均保留；v3/v4 的自然路径更早死亡，未作为最终选定策略。final01 的v4矩阵、旧检查器12份宝箱兼容失败、final02 的单个误接受负控仍存在，未被覆盖成通过。

这些是开发与策略选择证据，不是独立样本胜率评估。未替换预登记DEV01/02/03来制造全胜；未运行的第二幕 Boss、第二次跨幕和重复自然宝箱也未记为通过。

## 断电恢复

用户确认意外断电后重新连接。恢复检查没有发现遗留 python/dotnet 任务；当前冻结全部源码/输入/产物哈希匹配。346份重验、18项控制及11次原生矩阵均已完整落盘，没有重跑它们。

历史24项控制目录不存在，断连前 CreateProcess 报 transport disconnected 的那次动作没有可见已启动任务或产物；恢复后仅补该项，24/24符合预期。恢复核验与最后原始文件保留检查记录在 `power-recovery.json` 和 `closeout.json`。断电没有被解释为新审批拒绝，也没有改变安全或连接配置。

## 下一验收门

本轮应先独审，见 [下一交接](NATIVE_RUN_ACT2_NEXT_HANDOFF.md)。第四步剩余覆盖包括当前主控制器的多宝箱、后续事件/商店组合和第二幕 Boss；第五步仍需真实多幕集成和正常开局 RunVictory。Architect 旧尾部夹具不能关闭自然通关门。必要数据中的 unavailable 字段、最终同构建全量回归和预登记评估仍待完成。

复现当前源码对应的原生矩阵（会执行11个原生进程，须使用新目录）：

```powershell
python -P tools/native-run/run-native-act2.py --manifest .build/native-build/native-act2-final03/build-manifest.json --output .build/runs/<fresh-act2-dir>
```

如只核验已有原始证据，使用冻结 DLL 的 `--review <副本目录>`，不必重新执行原生流程。哈希证明一致性，不单独证明执行真实性；本轮仍是 native-only、非零售UI等价、非全角色/平衡/完整因果图验收。
