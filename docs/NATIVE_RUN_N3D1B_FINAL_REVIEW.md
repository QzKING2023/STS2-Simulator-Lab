# 4D1B 房间归属与阶段顺序最终独立验收

日期：2026-11-02（UTC）。结论：**通过，可继续 4D1C。** [上一轮复审](NATIVE_RUN_N3D1B_REPAIR_REVIEW.md) 的两个必修根因已闭合；本轮没有发现当前契约范围内的新阻断项。这是原生商店经济语义与自然商店片段验收，不是完整爬塔验收。

## 冻结及审查方式

- Build ID：`3B0C3069A9199A13E6DC202961E9A7D233FB39B2AEF1EA53357624F2A502F3D0`。
- Native DLL SHA256：`BCDD9D5FED0555FC0A52C118764823CD4D03BDC4DCDFE2184DAB6FA358A04B56`。
- Manifest：`.build/native-build/n3d1b-owner-final03/build-manifest.json`。
- 独立证据：`.build/runs/n3d1b-owner-independent-review01/`，包括 `review.py`、`build-check.ps1`、`summary.json`、`supplemental.json`、`closeout.json` 和每次 reader 原始日志。

本轮实际调用最终 DLL 的生产 `--review` 入口 230 次。独立损坏副本重新计算了合法 seal；拒绝来自生产语义契约，不是故意破坏文件哈希。没有重新运行 227 个原生场景或 Core98，也没有修改运行实现、已有结果和旧失败证据。

## 两项根因复核

### 1. 统计与库存生成的生命周期顺序

`NativeMerchantEconomyContract` 的 Travel 阶段单向推进：派发、旧房退出开始、统计开始/返回、可选库存生成、退出返回、确认、实际到达。首个无旧 Lab session 的 Travel 保留其真实差异。每个价格调用仍与相同 root/context、前一个价格完整状态及最后生成确认绑定。

本机原生参考 `RunManager.EnterMapPointInternal` 的顺序为记录旧节点统计、推进楼层/退出旧房间、创建并进入新房间；`NativeMerchantNatural` 实际调用 `EnterMapCoord`。本次修改没有调整这些业务调用来迁就检查器。

原 `independent-stats-after-inventory-prices` 现在退出 1，原因是 `inventory generation outside post-stats native Travel phase`。独立追加了三种子上的统计返回插进价格对、整个统计对后移、全部价格移至退出返回后、退出返回提前到最后价格返回前；也测试了重复统计、到达先于确认、缺失/重复商店到达。全部被拒绝。

### 2. 生成、到达、Attach、库存及退出的具体房间身份

自然路径从首个价格观察保存生成房间引用，交叉绑定确认、到达、当前 room-controller、Attach 动作、实际库存投影与返回对象。引用类型也必须是 MerchantRoom/MerchantInventory。后续库存和退出使用不可变的原商店归属；合法离开后当前房间已是 CombatRoom，不错误要求保留的 merchant.room 指向新战斗房间。

原 Attach 四份动作共同改指旧房间的反例，现因 `attachment action not owned by generated/arrived room` 被拒绝；原库存投影及退出事实共同改指旧房间的反例，现因 `inventory/returned identity not owned by native generated room` 被拒绝。

独立追加生成至返回共同改指旧房间、Attach 动作及当前快照共同改指、退出事实与库存投影共同改指、库存归属整体缺失、库存对象改指旧房间、到达类型不一致，以及完整快照增加未知嵌套字段等控制，全部被拒绝。三条自然路径的全局一致房间引用重命名、完整状态属性重排全部通过，验证器没有依赖特定编号或 JSON 属性顺序。

API Setup 夹具没有自然房间控制器投影，按其真实可观察边界在原生 Setup 返回绑定房间和库存；正常夹具仍通过。此处不声称验证了夹具不存在的自然到达。

## 生产检查器独立执行

| 组别 | 实际调用 | 结果 |
| --- | ---: | --- |
| 本轮生产控制包 | 165 | 159 个损坏拒绝、6 个等价通过 |
| 当前经济场景证据包 | 23 | 正常及预期故障包均符合各自 reader 预期 |
| 第一轮原始反例 | 9 | 全部拒绝 |
| 上一轮新增反例 | 3 | 全部拒绝 |
| 本轮独立构造 | 30 | 24 个损坏拒绝、6 个等价通过 |
| **总计** | **230** | **零意外接受或误拒绝** |

`summary.json` 另有 895 项来源、计数、原始退出、文件保持和自然路径核对，包含 600 个历史包存在性核对；这些不是 895 个新增业务场景。`supplemental.json` 另列 13 项非空完整 RNG、范围标签及源码差异核对。现有 600 次历史重读的同构建记录和原始文件已核验，本轮没有再次执行全部 600 次。

可直接复现原必修负控，预期退出 1：

```powershell
dotnet .build/native-build/n3d1b-owner-final03/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d1b-repair-independent-review01/independent-stats-after-inventory-prices
dotnet .build/native-build/n3d1b-owner-final03/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d1b-repair-independent-review01/independent-attach-wrong-room-all-action-copies
dotnet .build/native-build/n3d1b-owner-final03/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d1b-repair-independent-review01/independent-merchant-room-projection-mismatch
```

## 统一回归与自然执行证据复核

已逐进程核对实现轮的 **227 个完整原生进程、2453 项套件检查及 Core98**。原始退出分布为 164×0、58×1、2×2、3×3，预期失败、Unsupported 和预算停止没有被外层成功覆盖。六组实际计数为经济 23/361、商店 25/371、4C 55/561、4B 34/307、4A 16/273、N2 74/580。

每个进程的 manifest、header、来源标签、原始退出及 MemorySaveStore/不保存/未初始化 Steam/无 UI 标记对应最终冻结；没有加载 MyMod、BaseLib 或 CombatSolver。Core98 日志及其驱动和 Core DLL 身份相符。Build ID 独立重算一致，57 个源文件、37 个编译输入、3 个游戏输入、7 个产物及 dirty 源快照哈希一致。相较上一冻结，项目源码变更仅为经济契约、控制生成器与套件脚本三项。

| 固定自然路径 | 确认/派发 | 本次购卡支出 | 最后 HP/金币 | 实际停止位置 |
| --- | ---: | ---: | --- | --- |
| DEV_01 | 72/72 | 38 | 58/365 | 离店后下一战实际 PlayCard |
| DEV_02 | 41/41 | 24 | 64/87 | 离店后下一战实际 PlayCard |
| DEV_03 | 83/83 | 38 | 56/104 | 离店后下一战实际 PlayCard |

三条路径均由正常 Neow 开局在同 Run 内推进，无 fixture 设值；实际取得卡实例进入永久牌组并对应下一战 DeckVersion。完整 route/choices/final-state 与冻结重放一致，DEV01 minimal/off 对照一致；与上一构建的相同投影也一致。明确检查了 12 条 run RNG 流、3 条 player RNG 流及 event RNG 的 counter/s0–s3 均存在且非空，没有只比遗漏字段后的 JSON。

结果保持 `Ongoing / SliceBoundary / NotCompared`：本轮证明原生执行、数据与重放契约，未宣称模拟器独立差分、可靠胜率或完整 run 胜利。

2622 个原始历史文件及 993 个修复/审查历史文件哈希未变；15 个锁定输入、52 个兼容源码保持。三个真实 Harmony 探针的六项拒绝证据已核对，它们仅调用审计 API、未初始化 run、目标业务调用计数为 0；本轮未重新执行探针，也不把它们当成游戏业务覆盖。

## 接续范围

[4D1C 交接](NATIVE_RUN_N3D1C_IMPLEMENTATION_HANDOFF.md) 可立即执行：先原生遗物交易，再有实际来源的 OnPickup/AfterItemPurchased 效果，再 Courier 补货及新库存版本的第二次合法购买。当前单次生成/不补货的严格 profile 应保留，新业务使用明确的扩展契约，不能删除旧完整状态断言来放行效果。

原生奖励 reroll、实际替换/拒收、Sozu、Neow 迁移、宝箱账本、事件战斗恢复、Elite/Boss，以及步骤 5–6 的跨幕完整 run 与数据批测仍待完成。没有新的权限或环境阻塞，无需用户逐步请批。本轮仅写审查文档和隔离证据；未改角色、安装或真实存档，未启动 UI、调用真实 Steam/网络，未提交或推送。
