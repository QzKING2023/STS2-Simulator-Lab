# M1 步骤 4D1：商店交易子阶段

本页保留首次实现及其冻结结果。独立审查发现的 R1/R2 已在后续修复，当前最终构建与统一回归见 [4D1 修复报告](NATIVE_RUN_N3D1_REVIEW_REPAIRS.md)，待独立复验；原始证据不覆盖。

日期：2026-11-02。状态：**商店交易子阶段及适用1–4C同构建回归通过，待独立验收。** 这是4D的第一个有界子阶段，不是4D或M1完成。

## 范围与实现

从正常 Neow 开局后，通过原生 EnterRoomDebug(Shop) 进入 MerchantRoom，使用原生 EnterInternal/CreateForNormalMerchant 生成库存。金币 +1000、满药水槽和清空金币均是显式声明的原生 API 夹具。所有本轮商店场景为 api-fixture；没有把 Debug 入房算成自然地图行进。

实现覆盖 Card/Potion 购买、失败及售罄、指定永久牌移除/取消/重复尝试、原生退出与 Lab 会话关闭。交易调用原生 OnTryPurchaseWrapper(ignoreCost:false)，等待付款、取得、库存更新和 AfterItemPurchased，记录真实 PurchaseCompleted/PurchaseFailed。没有直接添加购买商品或手工扣款。实际卡替换与补货尚未接入本子阶段，触发时保留真实前缀并显式停止。

原生移除链为 MerchantCardRemovalEntry → OneOffSynchronizer.DoLocalMerchantCardRemoval → FromDeckForRemoval → FromDeckGeneric → LoseGold/RemoveFromDeck/次数增加。原来的 FromDeckForRemoval 拒绝点改为允许这个已核验的纯委托包装器，由既有 FromDeckGeneric 完成合法候选、取消与顺序选择协议，没有添加默认第一项捷径。

固定 DLL 中，OnCardRemovalUsed 位于 **NMerchantInventory**；它转发 NMerchantCardRemoval 的同名方法，其业务仅为原生 `entry.SetUsed()`，其余为视觉更新。无 UI 路径在原生 OneOff 同步任务返回后、AfterItemPurchased 之前补调用 SetUsed 一次；扣款、牌组移除与次数增加都由原生同步器完成。取消、重复失败不补调用。当前店保持 Used/售罄，新建下次原生移除 entry 的费用独立断言为100。

新增 `NativeMerchant.cs`、`NativeMerchantContract.cs`、`NativeMerchantControls.cs`、`run-native-n3d1.ps1`。共享接入限于场景注册、原生宿主预加载边界、状态/事件/生产 reader、原有选择包装器允许集；未改 Core、角色或冻结兼容源码。[完整契约](NATIVE_RUN_N3D1_LIFECYCLE_CONTRACT.md)先于场景验证定义。

## 安全与真实语义边界

唯一新增展示替换为固定版本 PreloadManager.LoadRoomMerchantAssets；静态核验其仅调用 LoadRoomAssets("Merchant Room", NMerchantRoom.AssetPaths)。库存与原生 AfterRoomEntered 保留。无 UI、Steam 未初始化、shouldSave=false、MemorySaveStore、原生单人网络服务及无 Mod/CombatSolver 的原有门继续生效。商店场景每进程观察到两次 SaveProgress 尝试被现有拦截拒写，ReplayWrites/TelemetryCalls 为0；这不是操作系统级全文件写入监控。

TestMode 的原生 MerchantPotionEntry 不执行零售浮动价格 RNG，MerchantRoom.Exit 不写零售未购商品历史。当前记录这些真实 TestMode 行为及 Lab 自身账本，不声称零售价格/RNG/商店历史等价。`merchant.closed` 是原生 Exit 返回后的 Lab 会话守卫，不是伪造原生房间字段。

## 冻结与入口

- Build ID：`62D4B09AC4FED8407EC8B7944DA4D54552C2244EB9F5D5C49C9001C7438C6816`。
- Native DLL SHA256：`1BE38606C947C4D47A63906DC6D2B1F71FC77DA054124BEE241BB672CA220820`。
- 构建：`.build/native-build/n3d1-final02/build-manifest.json`；0 warning / 0 error。
- 实际 dirty 源码快照：`.build/runs/n3d1-work01/source-snapshot-final02/` 和对应 JSON；52 个源码文件。
- 构建标识重新计算：`.build/runs/n3d1-work01/build-id-final02-check.json`。
- 商店矩阵：`.build/runs/n3d1-final02-merchant/summary.json`、`merchant-controls.json`。
- 统一回归驱动/进度：`.build/runs/n3d1-work01/run-final02.ps1`、`final02-progress.json`。
- 汇总及512项来源/流程核对：`.build/runs/n3d1-work01/summary.json`、`inspect-final02.py`。
- 固定DLL静态调用链/展示边界证据：`.build/review/n3d-merchant/provenance.json`及对应本地摘录，不随仓库发布游戏源码。

```powershell
pwsh -NoProfile -File tools/native-run/run-native-n3d1.ps1 -BuildManifest .build/native-build/n3d1-final02/build-manifest.json -OutputDirectory .build/runs/local-merchant-check
dotnet .build/native-build/n3d1-final02/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d1-final02-merchant/merchant-remove-1
```

输出必须为新的 Lab runs 目录；不覆盖旧证据。

## 已通过的商店矩阵

八个场景各执行一次并在新进程冻结重放：买卡、买药水、满槽、金币不足、售罄后再次购买、指定非0牌移除、取消、第二次移除。另有三个场景各 minimal/off 观察对照，以及三个真实执行故障控制，共 **25 个原生进程 / 189 项检查**。重放与观察对照比较完整 route、choices、final-state，包含记录的完整 RNG。

| 实际场景 | 结果 |
| --- | --- |
| 原生库存 | 7卡/3遗物/3药水/1移除，真实条目及商品实例；本轮没有购买遗物 |
| Card | 实付51，取得本次 offered 实例 `i21`，牌组10→11，原生售罄及完成回调一次 |
| Potion | 实付50，取得本次 offered 实例，0→1药水 |
| 满槽 | 原生 FailureSpace，无扣款/取得，库存保持 |
| 零金币 | 原生 FailureGold，无扣款/取得 |
| 再买售罄条目 | 第一次成功，第二次原生 FailureOutOfStock，不重复扣款 |
| 指定移除 | 实付75，删除原永久牌组第二个实例 `i2`，其他牌完整内容/升级/顺序保持，次数0→1，Used=true |
| 取消 | 空选择经原生取消返回，不扣款、不移除、不置 Used |
| 第二次移除 | 原生 false，次数仍1，无第二次选择/扣款 |
| 过期/错误库存 | InvalidAction，确认/派发均3/3，交易零派发 |
| 购买后任务故障 | EngineError，3/4未确认前缀；真实扣51和新卡保留，不回滚或补确认 |

新增标记与原生Hook前缀两类必需事件，实际记录 SetUsed→AfterItemPurchased 的顺序、完整静止状态及唯一性。取消/失败不产生成功阶段事件。

71 个生产 reader 控制：61 个坏副本拒绝、10 个结构等价副本接受。覆盖4类必需新事件的缺失/重复/快照缺失-null-类型错误/新增字段漂移/脱离根、操作ID与顺序、假成功、费用/付款/回调、旧取得对象、新对象遗漏、移除与SetUsed来源。额外把所有后续快照一起改成同一假售价或未知条目字段，仍在完整条目契约拒绝；并非只依赖相邻快照不一致。哈希链重新封装后实际调用生产检查器。

## 同一最终构建的统一回归

| 套件 | 原生进程 | 套件检查 |
| --- | ---: | ---: |
| 4D1商店 | 25 | 189 |
| 4C奖励/药水/宝箱 | 55 | 561 |
| 4B房间 | 34 | 307 |
| 4A真实终态/尾部夹具 | 16 | 273 |
| N2/步骤1–3 | 74 | 580 |
| 合计 | **204** | **1910** |

所有矩阵均使用上述final02产物，实际子退出和报告退出逐个相符。分布144×0、55×1、2×2、3×3符合预先声明的正常/故障/Unsupported/预算预期，不能读成204条自然成功路径。Core98实际执行通过，7个Core驱动文件及加载Core DLL哈希匹配。另有512项源码/产物/输入/流程检查，0失败；不另算原生执行。

115个旧独立审查包使用当前生产reader复验，全部符合原预期；保留其三个原构建身份，不冒充当前原生执行。460个原始包文件未变，见 `.build/runs/n3d1-work01/review-regression/summary.json`。当前4C的292控制也已完整重跑，不只是复制旧绿结果。

原自然24动作、skip24/take25重放和记录的完整RNG保持；DEV01为71/71片段，DEV02为138/138真实Death，DEV03为WhisperingHollow Unsupported77/78，原失败前缀和故障传播保持。4A的正常开局真实Death与Architect夹具RunVictory均保留，不将夹具计作正常开局通关。

52个本次构建来源文件/实际快照、33个编译输入、3个游戏输入、7个产物哈希匹配，构建标识重算一致。另行冻结的15个Mod输入、52个兼容源码也全部匹配；本轮未新增Mod运行/适配。HEAD保持 `d73039d688eb54c9513f5b9508a03947e689aa18`，仅新增/修改11个native-run实现/驱动文件及相应项目文档，无提交推送。

## 尚待后续 4D 完成

本子阶段不覆盖自然地图商店、遗物购买/取得 Hook、TheCourier 补货和动态价格组合、跨店实际行进。也未完成 WhisperingHollow/事件内战斗返回、精英/Boss 及主交接中的五项奖励闭环。后续任务与依赖见[4D1之后交接](NATIVE_RUN_N3D1_NEXT_HANDOFF.md)。旧自然24动作仍是战斗/奖励片段，不能当作自然商店或完整 run。

实现过程保留候选证据：candidate01基础买卡/移除通过但含两条nullable警告；一次启动在manifest生成前被脚本拒绝，未启动原生进程。candidate02因购买回调枚举命名空间错误编译失败；candidate03修复后25进程/173检查通过。随后补强售出条目完整约束与两个一致改写控制，final01商店25/175及旧4C55/561通过。复核发现SetUsed晚于购买Hook，正常取消本会话自己的旧回归，保留未完成的4B自然片段批次及各子进程实际结果；修正为原生OneOff任务返回处调用SetUsed并实际观察后续Hook，重新冻结final02后重跑，不复用final01作为最终通过证据。所有失败/候选产物保留，不混入最终同构建计数。
