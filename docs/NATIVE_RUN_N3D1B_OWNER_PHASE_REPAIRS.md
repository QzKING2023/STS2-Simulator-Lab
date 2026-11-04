# 4D1B：库存生成阶段与房间归属修复

状态：**两项修复及最终同构建统一回归通过，待独立复审。** 对应 [独立复审的两项必修](NATIVE_RUN_N3D1B_REPAIR_REVIEW.md)。只修改生产证据检查器、控制生成器及矩阵入口；原生商店业务、宿主补丁未变。

项目代码变化为 `NativeMerchantEconomyContract.cs`、`NativeMerchantEconomyControls.cs`、`run-native-n3d1b.py` 三个文件；最小差异保存在 `.build/runs/n3d1b-owner-work01/implementation.diff`。

## 修复契约

自然 Travel 使用单向阶段：派发 → 旧房退出开始（存在旧 session 时）→ 统计开始 → 统计完成 → 可选库存生成 → 退出返回 → 确认 → 新房到达。首次 Neow 保留没有 Lab 旧 session 的真实差异。生成必须在本次统计完成后；统计不能后补或穿插价格，退出返回必须等生成完成，之后不能继续生成。原阶段必需性、唯一性、当前 root/context、完整静止状态和真实业务变化约束保留。

自然商店第一次价格观察绑定当前 MerchantRoom，生成确认、实际到达及 Attach 保持同一具体引用。Attach 的 decision.room、派发房间、已到达 session、返回当前房间、merchant.room 和 nativeInventory 必须相互对应。身份声明必须证明房间为 MerchantRoom、库存为 MerchantInventory，不能把已声明旧 CombatRoom 同时填到多个字段通过。

已绑定的库存及所属商店独立保留。离店后当前房间变成下一战 CombatRoom，库存投影仍保留旧商店归属；退出事件必须属于绑定对象。

API Setup 夹具没有自然控制器的 `rooms` 投影。在原生 Setup 返回首次出现具体库存和所属房间时绑定类型与引用，继续完整比较价格尾状态与返回/确认状态；不把此夹具称为自然到达，也不要求其产生不存在的统计/到达事件。

## 控制及失败保留

原 148 个控制保留，新增 17 个：5 个阶段倒序/穿插/重复，8 个 Attach 或库存归属的旧房间/缺失/null/错误类型，2 个将整个生成房间或库存引用改指已声明旧 CombatRoom，2 个合法等价控制（房间引用全程一致重命名、字段重排）。合计 165 个，预期 159 个坏副本拒绝、6 个合法副本接受。原三个误接受副本直接重读应退出 1，原九个反例继续保持闭合。

首构建 `owner-final01` 有一个 nullable warning，未验收；`owner-final02` 编译无 warning，但过早要求房间投影，误拒正常 Setup。该经济矩阵和历史重读的失败证据保留。按实际夹具观察边界修复后重新冻结为 `owner-final03`，未删除失败样本或改变原生效果。最终证据仅使用该最后构建。

## 最终冻结与证据入口

- Build ID：`3B0C3069A9199A13E6DC202961E9A7D233FB39B2AEF1EA53357624F2A502F3D0`。
- Native DLL SHA256：`BCDD9D5FED0555FC0A52C118764823CD4D03BDC4DCDFE2184DAB6FA358A04B56`。
- Manifest：`.build/native-build/n3d1b-owner-final03/build-manifest.json`，0 warning／0 error。
- 统一入口：`.build/runs/n3d1b-owner-work01/run-final03.ps1`；子矩阵 `.build/runs/n3d1b-owner-final03-*`。
- 历史重读：同工作目录 `recheck-history-final03.py`，167＋231＋202＝600 次 reader，不能算新游戏执行或独立胜率样本。
- 来源/冻结核对：`inspect-final03.py`、`summary.json`、`source-snapshot-final03.json`。
- 最终结果及历史保留核对：同目录 `closeout.json`。

以下控制预期拒绝，退出 1 才是检出成功：

```powershell
dotnet .build/native-build/n3d1b-owner-final03/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d1b-repair-independent-review01/independent-stats-after-inventory-prices
dotnet .build/native-build/n3d1b-owner-final03/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d1b-repair-independent-review01/independent-attach-wrong-room-all-action-copies
dotnet .build/native-build/n3d1b-owner-final03/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d1b-repair-independent-review01/independent-merchant-room-projection-mismatch
```

完整矩阵由实际退出、生产检查器、重放/完整 RNG、状态/来源和冻结核对共同判定。三个真实 Harmony 探针仅证明生产 Patch/Ensure 审计 API 拒绝未知 patch，未初始化 run，目标业务调用计数为 0。

## 最终结果

统一执行为 **227 个完整原生进程 /2453 项套件检查，另 Core98**；经济矩阵 23/361、旧商店25/371、4C55/561、4B34/307、4A16/273、N2 74/580。全部绑定上述最终构建；164×0、58×1、2×2、3×3 的实际退出分布保留。套件整体成功不改变内部预期失败、Unsupported 或预算停止分类。

165 个生产控制全部符合预期，三个新误接受和原九个反例均拒绝；600 个历史包重读无预期差异。来源/实际退出/存档隔离/源码产物身份等 248 项补充核对通过。历史重读、控制副本和重复运行不能作为独立策略样本。

三个既定自然路径仍为 72/72、41/41、83/83 确认/派发动作，HP/金币为58/365、64/87、56/104；实际购买花费38/24/38，具体新卡保留至下一战。各自冻结重放、DEV01 minimal/off 对照及相对上一构建的 route/choices/final-state（含完整 RNG）均保持。终局 Ongoing/SliceBoundary，不是全 run 胜利。

原 2622 文件和额外 993 文件哈希保持；15 输入、52 兼容源码固定。当前 DLL 的三个真实 Harmony 审计 API 探针重新运行，六次拒绝通过，未初始化 run，目标业务调用仍为0。

## 范围与后续

本轮保留 2622 个历史文件，另锁定上一修复及独立复审的 993 个文件；15 输入、52 兼容源码继续固定。三个自然商店片段和 API 夹具不等于完整 M1。未修改角色、安装或真实存档，未启动游戏 UI、调用真实 Steam/网络，未提交推送。

最终回归及独立复审通过后按 [4D1C 交接](NATIVE_RUN_N3D1C_IMPLEMENTATION_HANDOFF.md) 继续遗物/Hook/补货；4D 余项及步骤 5–6 仍在既有 M1 授权内。
