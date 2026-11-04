# 4D1C 原生遗物、购买 Hook 与 Courier 药水补货

状态：**实现及最终同构建回归通过，待独立验收。** 固定 v0.111.0 / 41cef1ea、Ironclad/A0。当前子阶段是单商店的原生交易与可信采集切片，完整 M1 继续推进。

## 实际实现

新增 `NativeMerchantExpansion.cs`、`NativeMerchantExpansionContract.cs`、`NativeMerchantExpansionControls.cs` 和 `run-native-n3d1c.py`。既有驱动只增加显式场景路由、观察包装与 profile 选择；旧 4D1/4D1B 契约继续使用原路径。原生购买始终调用 `OnTryPurchaseWrapper(ignoreCost:false)`，没有手动扣款、附加库存或直接调用补货来制造结果。

新 profile `native-merchant-transactions/v1` 记录原生 OnTryPurchase 的取得区间、基类 AfterObtained、Clear/Restock、AfterItemPurchased 的真实进入/完成以及 PurchaseCompleted 回调。顺序是取得/付款 → 清空或补货 → Hook → 回调 → 返回 → 确认。TheCourier 和 MawBank 的准备均通过原生 RelicCmd.Obtain，标记为 API 夹具；库存由原生商店生成。

[完整操作契约](NATIVE_RUN_N3D1C_LIFECYCLE_CONTRACT.md) 覆盖请求/结果的必需、唯一、root/context 归属与顺序，静止点完整采集投影比较，实际新旧实例、商品/价格版本、生成与退出归属，及真正业务变化。补货 RNG 从原始 Shops 状态独立推进两次选取和一次定价，并绑定完整 counter/s0–s3；价格按固定原版规则独立核对 Courier 折扣。完整比较的范围是当前状态投影，未声称采集所有遗物的隐藏字段。

## 真实业务证据

| 场景 | 实际结果 |
| --- | --- |
| 原生遗物购买 | 正常原生库存中的 OddlySmoothStone，实付 161；offered 与玩家新增 relic 为同一对象；真实默认 AfterObtained 成对记录；5/5 动作 |
| MawBank Hook | 同样实付 161；真实 Hook 前 HasItemBeenBought=false，之后 true、IsUsedUp=true、Status=Disabled；夹具原生入店加 12 金币单独归因 |
| Courier 连续补货 | 同一原生 potion entry，两次实付 38、40；i44 实际取得后生成 i48，第二次实际取得 i48 后生成 i49；两次均由购买触发补货；6/6 动作 |
| 缺钱 | 原生 FailureGold，0 付款、无取得/补货/Hook，静止完整投影不变；5/5 动作 |
| 旧票据 | 保留第一次购买前真实未消费票据，实际补货后消费被拒绝；InvalidAction，4/4 前缀 |
| 途中故障 | 标注故障发生在实际购买后、根动作确认前；EngineError，3/4 前缀，保留实际取得结果 |
| 自然 DEV_01 | 正常 Neow 开局同 Run，经真实战斗/奖励/地图到达商店，原生买入 WingCharm 实付 177，合法离店并在下一战实际 PlayCard；71/71 动作 |
| 自然 DEV_02/03 | 原样保留预登记样本，在无可负担受支持遗物的采购目标停止；Unsupported，37/37、79/79 前缀；未换 seed 或注入金币 |

自然 DEV_01 的停止为 Ongoing/SliceBoundary/NotCompared。DEV_02/03 是本采购切片的目标可达性停止；通用 run 策略仍需允许合法无购买离店。所有正向夹具均冻结重放；遗物和 Courier 另有 minimal/off 观察对照，完整 route/choices/final-state（含 RNG）一致。没有把 native-only 结果称作独立模拟差分或策略胜率。

## 修复与失败证据

开发中保留了编译失败、历史契约遗漏、默认 Hook 基类误判、驱动失败及补货价格投影合法变化的诊断结果。`final03`/`final04` 的既有通过结果保留，最终结论只绑定下述 `final05`。

自查构造了在退出返回及全部后续快照共同增加 7 金币的有效 seal 副本：`control-exit-anchor-before-fix` 在 final04 reader 误通过。修复后绑定退出开始、原生退出返回、外层返回、确认及最终边界的完整状态，并独立验证真实退出历史。final05 实际拒绝该副本，理由 `exit full non-history state binding`；没有把发现的数据校验缺口当作游戏业务错误。

同轮补齐库存生成尾部至 Setup 返回的完整绑定、具体生成 entry/model/价格到库存的绑定，以及所有退出阶段的必需唯一/归属/时序。MawBank 入店加金币、贫穷夹具原生扣款和真实退出历史变化均按明确契约保留。

## 最终冻结与统一回归

- Build ID：`37375A30D91CA842BB756301A94F344335F75498B4DE759A3BFBBAC2D4A297BD`。
- Native DLL SHA256：`B476631DFB1ADABE65E4056D6A3D7DF06602A094E6440948865B12F6C3CA7D5D`。
- Manifest：`.build/native-build/n3d1c-final05/build-manifest.json`，零 warning/error；Build ID 独立重算一致。
- dirty 源、编译输入、游戏输入及产物分别为 61/40/3/7 项，最终哈希和源码快照均核对。
- 统一入口：`.build/runs/n3d1c-work01/run-final05.py`；最终源快照、来源核查和总汇：同目录 `source-snapshot-final05.json`、`summary.json`、`closeout.json`。

同一最终构建共 **245 个完整原生进程 / 2889 项套件检查，另 Core98**：新交易 18/436，旧经济 23/361、商店 25/371、4C 55/561、4B 34/307、4A 16/273、N2 74/580。原始退出为 178×0、60×1、4×2、3×3；预期故障、Unsupported 和预算停止分别核对，没有由外层 exit0 覆盖。

新交易 **333 个生产控制**（327 个损坏拒绝、6 个等价通过），覆盖整体缺失、重复、错序、归属、null/缺状态、新增嵌套字段、共同篡改、实例与生成价格绑定、RNG、Setup/退出全链及合法等价。`controls.json` 保留每个实际 raw exit。另有 **830 次历史生产 reader 调用**，均符合各自预期，不计为新增游戏执行或胜率样本。

旧自然商店 72/41/83 动作的完整 route/choices/final-state 与 4D1B 冻结基准保持；旧自然 skip/take、真实 Death、尾部夹具、选择/快照/失败传播控制均在本构建回归。2622+993 历史文件哈希保持，15 输入和 52 兼容源码固定。7 个实际 Harmony 审计 API 探针、14 项未知 patch 拒绝通过，目标业务调用为 0；它们没有初始化游戏 run，不能当业务覆盖。

## 复现

以下输出必须选新目录：

```powershell
python -P tools/native-run/run-native-n3d1c.py --manifest .build/native-build/n3d1c-final05/build-manifest.json --output .build/runs/my-fresh-transactions
```

复核已生成正向或负向包：

```powershell
dotnet .build/native-build/n3d1c-final05/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d1c-final05-transactions/courier-1
dotnet .build/native-build/n3d1c-final05/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d1c-work01/control-exit-anchor-before-fix
```

前者应退出 0，后者应退出 1。重新执行原生套件须保持当前冻结源/输入，否则 wrapper 拒绝旧构建。开发冻结 final04 的源快照也已保留，重建的历史驱动逐字节匹配其原 manifest SHA。

## 真实范围与接续

当前仅默认基类拾取、MawBank 购买 Hook、Courier 药水补货及单店自然切片。特殊拾取选择/效果、卡牌/遗物补货、其他动态价格/拒收/替换组合尚未验收；执行器对非默认 AfterObtained 与既有未知购买监听器前置拒绝，生产检查器拒绝未声明状态变化。这些边界不代表原版内容已经全部支持。

下一轮按 [后续交接](NATIVE_RUN_N3D1C_NEXT_HANDOFF.md) 完成奖励 reroll/真实取得替换/Sozu/Neow/宝箱来源账本，再接原 4D2 事件战斗恢复、4D3 Elite/Boss，以及步骤 5–6 的跨幕完整 run 和批测。完整 M1 未完成，当前没有权限或环境阻塞。

本轮未修改角色、游戏安装、真实存档或 Mod 专项；无 UI、真实 Steam/网络调用，未提交推送。Progress/Replay 数值为拦截计数，不是实际持久化写入。中途一次 exec transport 断连已恢复，原批次持续运行，无重复启动。
