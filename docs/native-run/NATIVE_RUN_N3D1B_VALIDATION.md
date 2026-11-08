# 4D1B：原生商店经济、历史与自然进出

状态：**实现与同构建统一回归通过，待独立审查。** 这是原版 M1 的有限商店纵向切片，最终停在离开商店后的下一战真实 Play；没有据此宣称完整 4D 或完整 run。

实施依据：[4D1B 交接](NATIVE_RUN_N3D1B_IMPLEMENTATION_HANDOFF.md)。前置 [4D1 最终独立验收](NATIVE_RUN_N3D1_FINAL_REVIEW.md) 与全部历史证据保留。细节契约见 [4D1B 生命周期](NATIVE_RUN_N3D1B_LIFECYCLE_CONTRACT.md)；后续具体工作见 [4D1C 交接](NATIVE_RUN_N3D1C_IMPLEMENTATION_HANDOFF.md)。

## 原生业务与安全边界

全局 TestMode 保持开启，原版 v0.111.0 / Ironclad / A0、独立内存实验档案、一 run 一进程。新 `native-merchant-economy/v1` profile 仅在三个固定原生方法内恢复被 TestMode 跳过的业务分支：

| 方法 | 恢复内容 |
| --- | --- |
| `MerchantPotionEntry.CalcCost` | 原生药水价格抖动、取整和 Shops RNG 消耗 |
| `MerchantRoom.Exit` | 原生未购买卡/遗物/药水历史 |
| `RunManager.UpdatePlayerStatsInMapPointHistory` | 原生当前节点金币、HP、MaxHP 历史更新 |

每处只替换一个已固定的 TestMode getter 判定，原方法其余 IL 保持；未知已有 Harmony patch 拒绝，唯一锚点不匹配拒绝。原始 token/MVID/IL 哈希、期望匹配数和 profile 写入 `result.json.seams`，完整补丁清单继续参与每动作宿主稳定性检查。方法/版本安全检查有实际代码和成功路径证据；本轮没有新加针对这三个方法逐一实际织入未知 patch 的负控，不能将静态门控审查说成已做该类测试。

价格、库存、付款、取得、移除、历史均由原生业务执行；Lab 的独立检查器只验证结果，没有写入游戏历史或替代价格算法。地图历史由原生 `AppendToMapPointHistory` 创建；不凭空补条目。原生历史中的卡序列化可能早于 FloorAddedToDeck 赋值，验证分别绑定原 offered 序列化和最终永久卡实例，不抹平真实差异。

MemorySaveStore、ShouldSave=false、SaveProgress/replay 拦截、Steam/UI 禁用、NetSingleplayerGameService、no Mod/BaseLib/CombatSolver 继续检查。原生保存调用次数按各路径记录，拦截计数不代表实际写入；本轮不承诺操作系统全文件写监控或所有 TestMode 分支等同零售。

## 同一 Run 的自然执行

预登记三个既有 DEV seed，公开地图最短 Shop 距离，平局优先 Monster/列号；实际购买最便宜可负担卡，有足够金币时原生取消移除选择，再沿合法边进入下一 Monster 并实际打出一张合法卡。节点预算12、根动作5000、墙钟600秒；没有替换失败种子。登记位于 `.build/runs/n3d1b-work01/cohort-registration.json`，早期探索/候选证据分开保留。

自然路径从正常 Neow 开始，完成真实战斗与奖励，原生 EnterMapCoord 进入商店并生成库存；Attach 只绑定既有库存。没有 GainGold、EnterRoomDebug 或手工准备库存/牌组。原生 EnterMapCoord 拥有退出，不另调第二次 Exit。Closed 守卫之外，还验证实际地图、楼层、房间栈、旧商店历史、新房间与下一战实际 Play。

| 固定 seed | 已确认/已派发动作 | 商店前 Victory 数 | 原生购牌与实付 | 最终 HP / Gold |
| --- | ---: | ---: | --- | --- |
| DEV_01 | 72 / 72 | 3 | Rage，38 | 58 / 365 |
| DEV_02 | 41 / 41 | 1 | Shrug It Off，24 | 64 / 87 |
| DEV_03 | 83 / 83 | 3 | Whirlwind，38 | 56 / 104 |

每条路径都有实际商品实例进入永久牌组，下一战牌堆中的副本绑定相同 DeckVersion；未强行摆进首手。最终为 CombatRoom / Play，终态 Ongoing，停止原因 SliceBoundary。三种子各有一次 live 和一次冻结重放，重复不是独立胜率样本。自然片段重放保持完整 route/choices/final-state（含完整 RNG），不是完整 run 策略强度评估。

三条自然路径原生药水价格依次为 `[48,51,72]`、`[49,48,48]`、`[48,51,50]`，对应每次 CalcCost 的 Shops counter `25→26→27→28`。检查器独立检查完整 xoshiro 状态转移和价格映射，另有固定输入输出锚点；没有把整个库存生成的 RNG 消耗简化成三次。

## 新矩阵与可信数据

新矩阵 `.build/runs/n3d1b-final05-economy/summary.json`：**23 个完整原生执行，313 项检查，0 失败**。

- 4 个经济 API 夹具（卡/药/移除/取消），各 live + frozen replay，共8；这些夹具明确使用原生命令准备金币/调试进入商店。
- 三个正常开局自然 seed，各 live + frozen replay，共6。
- 卡/移除/自然 DEV_01 的 minimal/off 观测对照，共6。
- stale、wrong-inventory、实际付款取得后注入 task-fault，共3，均按预期失败。

前20个完整场景的原生退出均为0；后3个为1，分别明确 InvalidAction 或 EngineError。旧票据/错误库存保留3/3前缀，交易后异常保留3/4前缀及真实已付51金币/新增卡，终态Unknown，不假回滚或伪确认。

117 个生产检查器控制（115 损坏拒绝、2 等价通过）使用重新封装有效 seal 的副本，验证语义而非仅破坏文件校验：价格/RNG/实例，退出历史的内容/升级/顺序/已购标记，stats、房间到达/准备/退出/完成的必需性、唯一性、root/context、完整静止快照，以及相邻真实业务区间。正常的属性重排和一致扩展字段通过。

final04 曾有六个 room-entered/leave-ready/complete 错 root/context 负控被接受，原始失败结果保留。final05 补上已确认根动作/context绑定；六个历史反例与八个邻近/合法副本的14次重查均符合预期，见 `repair-recheck.json`。另以新 reader 只读重查231个旧证据包，0不符、924原文件哈希不变；旧包保留旧构建身份，不计入新原生执行。

## 冻结与执行中断恢复

- Build ID：`EBC85ADE50B9B504BB8E86FF02D70A633BB08A63B69692F04499C37EAF855E37`。
- Native DLL SHA256：`DAB5D72815FB4F5547D654A791F7B00689E128919C272CAD5049931857E2904C`。
- 构建：`.build/native-build/n3d1b-final05/build-manifest.json`；0 warning / 0 error；57项源码、37项编译副本、3项游戏输入、7项产物。
- 运行与检查：`.build/runs/n3d1b-work01/`，新矩阵与各旧套件分别在 `n3d1b-final05-*`。

本轮检查状态时发现原 batch 已无进程，仅残留 running 标记：19个场景已完成，第20个 natural-1-off 只有 header，无 raw exit 或结果，不能推定其成功或语义失败。该未完成尝试完整归档到 `interrupted-final05-natural-1-off/`；保留原 progress/cohort 和故障记录。恢复只运行4个未完成场景，复用19个已完成原生结果，再串行启动尚未执行的旧回归。`recovery-final05.json` 证明原19个场景与归档 header 内容未变。

恢复首次在进入原生执行前遇到本机 `inspect.py` 遮蔽 Python 标准库的工具错误，记录保留为 `recovery-final05-failed-attempt01.json`；以 Python safe-path 运行恢复驱动后继续，没有重复原生场景。恢复脚本单独记录在 `facts-final05.json.supplementalRecoveryDrivers`，不伪称其已属于原构建清单。冻结的运行代码、产物与原项目 runner 没有因此变化。

最终统一回归为 **227 个完整原生执行 / 2405 项套件检查，另列 Core98**：新经济23/313、旧商店25/371、4C 55/561、4B 34/307、4A 16/273、N2 74/580。原生退出为164×0、58×1、2×2、3×3，包含明确的故障/不支持/预算案例，全部符合各自预期。完整来源核验、dirty源码快照和结果见同目录 `summary.json`、`source-snapshot-final05.json`、`closeout.json`；不能把外层exit0等同于每个原生流程都成功。

## 复现与剩余工作

在全新输出目录执行整个新矩阵：

```powershell
python tools/native-run/run-native-n3d1b.py --manifest .build/native-build/n3d1b-final05/build-manifest.json --output .build/runs/n3d1b-reproduce-new
```

单路径可用 `run-native-slice.ps1 -Scenario merchant-natural -Seed LAB_V0111_NATIVE_DEV_01`，必须提供固定 BuildManifest 和全新 OutputDirectory；重放加源 `route.json` 的 ReplayPath。原始目录不可覆盖。

当前尚未执行遗物购买、真实 OnPickup/AfterItemPurchased 业务变化、TheCourier 补货及动态库存版本；当前有限商店契约会在遇到取得替换/补货时明确 Unsupported。接续4D1C后仍有奖励 reroll、真实替换/拒收、Sozu、Neow会话迁移、宝箱账本、WhisperingHollow/事件战斗恢复、Elite/Boss，以及步骤5完整跨幕 run、步骤6预登记批测和可信数据交付。15项 Mod 输入和52项兼容源码继续冻结，无角色/安装/真实存档修改、游戏UI、真实Steam/网络调用或提交推送。
