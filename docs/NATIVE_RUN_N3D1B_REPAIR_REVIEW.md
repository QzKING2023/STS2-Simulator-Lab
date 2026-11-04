# 4D1B R1–R3 独立复审

日期：2026-11-02（UTC）。结论：**仍有两项数据契约必修，暂不进入 4D1C。** 原审查的九个反例全部闭合，148 个生产控制符合预期；独立审查另发现三个误接受副本，归属两个根因：库存生成与旧节点统计的先后关系未绑定，以及 Attach/库存投影的房间身份未与当前真实房间交叉绑定。没有发现本轮原生执行结果被伪造，也没有把坏副本当作实际游戏故障。

被审查冻结为 `20EB7F9259E9EC2806AB9D91F2AFAA9EA73ED70DD0FCE8DC7FA21D816BD0D276`；Native DLL SHA256 为 `D251B8F0A6EF23959AE13599FB278AAB88B5DCF6201FF1481D0F733A81BAF4AC`。构建 ID 独立重算一致，57 源文件、37 编译输入、3 游戏输入、7 产物及 dirty 源码快照与当前文件一致。

## 已通过的修复及实证

- 价格 start/return 完整状态按连续链连接，最终绑定生成父动作确认；共同篡改整个价格链的金币、HP、牌组和新增结构字段被拒绝。精确 Shops RNG 转移和原生定价断言保留。
- 每次自然 Travel 的统计、旧房完成、新房到达已经成为必需阶段。原九个反例、整段缺失、重复、错 context、先到达后完成、推迟到下一派发等控制均被拒绝。
- MerchantTravel 返回 from/to/closed 绑定真实动作及派发/返回状态；反转坐标仍被拒绝。合法等价字段重排、全程一致扩展字段被接受。
- 227 次完整原生执行、2436 项套件检查及 Core98 的证据身份已复核；原始退出分布为 164×0、58×1、2×2、3×3，故障、Unsupported、BudgetExceeded 没有被外层退出 0 改写。此轮没有重跑 227 次原生流程或 Core，复核的是这些最终构建实证及其原始退出、来源和哈希。
- 三个既定自然种子实际为 72/72、41/41、83/83 确认/派发动作。正常 Neow 开始、原生战斗/奖励、合法商店购买、退出及下一战真实 Play 的同 Run 证据成立。买到的具体卡实例保留在永久牌组，下一战 DeckVersion 对应；终态是 Ongoing/SliceBoundary。
- 三路径完整 route/choices/final-state 与各自重放一致，含全部 RNG 状态；DEV01 minimal/off 观察对照一致。三路径这三个文件与上一冻结也一致，仅证明已记录投影稳定。
- 2622 个历史证据文件保持哈希，包含中断恢复记录；15 个锁定输入、52 个兼容源码不变。前 19 个已完成场景及 header-only 中断尝试没有被改写或计为新完整执行。

## 必修 A：统计与库存生成阶段仍可倒序

位置：`tools/native-run/NativeMerchantEconomyContract.cs` 的 `merchant-stats-start`（约第 51 行）、`merchant-price-start`（约第 76 行）及生成确认分支。

独立副本 `independent-stats-after-inventory-prices` 仅把进入商店动作 a67 的完整 stats start/return 从原位置移到全部三个价格 return 之后，仍在 room-exit-return 之前。其他事件、快照、动作和结果不变，重新计算有效 seal。最终生产 reader **退出 0**。

实际正常记录在统计阶段是 floor=5、CombatRoom；随后的价格阶段是 floor=6、MerchantRoom。坏副本因此先记录新商店价格，再回到旧房间统计，最后又确认新商店，属于不可能的生命周期顺序。原生固定版本 `RunManager.EnterMapCoordInternal` 先调用 `UpdatePlayerStatsInMapPointHistory`，再推进楼层、退出旧房间、创建/进入新房间；本地只读证据位置为 `.build/native-reference/RunManager.cs` 的对应方法。Lab 的自然入口在 `NativeMerchantNatural.cs` 真实调用 `EnterMapCoord`，没有另一路允许上述倒序。

根因是统计对和价格链各自有顺序约束，却没有共享的父 Travel 阶段约束：价格开始不要求本次统计已完成；统计开始也不拒绝本次库存生成已经开始。先出现价格链后补统计仍能同时满足最终 flags。

最小修复：在当前有限自然 profile 内，把生成绑定到同一个 Travel 已完成的统计阶段及其合法退出/生成区间，记录不可逆阶段推进；不能在生成开始后重新进入统计。保留 MerchantSetup API 夹具与自然 Travel 的差异，不给夹具强加不存在的统计阶段，也不要把生成首快照直接等同原 Travel dispatch。回归应包括该整对倒序、统计跨价格对穿插、阶段重复及正常完整序列。

## 必修 B：Attach 与库存所属房间未交叉绑定

位置：`NativeMerchantEconomyContract.cs` 的库存绑定分支（约第 97 行），以及 `NativeMerchantContract.ValidateResult` 的 Setup/Attach 分支（约第 113 行）。真实动作生产位置为 `NativeMerchantNatural.cs` 第 30–32 行。

| 独立副本 | 改动 | 最终 reader |
| --- | --- | ---: |
| `independent-attach-wrong-room-all-action-copies` | 将 a68 Attach 的 root-request、dispatch、open、confirmed 四处动作副本的 decision.room 一致改为第一战 CombatRoom i32；真实当前 MerchantRoom 仍为 i74 | 0 |
| `independent-merchant-room-projection-mismatch` | 保留真实 Attach 动作及 rooms.reference=i74，将绑定后所有 merchant.room 和两个 merchant-exit facts.room 一致改成旧 CombatRoom i32 | 0 |

这些 `$ref` 均已在身份表声明，因而不是悬空引用。第一种是内部一致但指向错误房间的动作；第二种是内部一致但归属错误的库存投影。两者都违反已存在的真实对象关联，不需要新增节点或遗物效果才能判定。

根因是 Attach 允许引入整个 merchant 投影，当前只验证其中三件药水的 entry/model/cost、库存引用和移除价格；动作副本彼此相等，以及后续库存投影与自己相等，都不能证明其来自当前房间。`room == "MerchantRoom"` 的字符串判断也不能绑定具体实例。

最小修复：自然 Attach 的 decision.room、派发时当前 rooms.reference、已确认的生成/到达房间、返回时 merchant.room 和返回当前房间必须指向同一具体对象；nativeInventory 应继续对应所绑定库存引用。记录该父房间身份供后续退出使用。合法 MerchantTravel 返回后，当前房间已经变成下一战，旧 merchant.room 应仍指原商店，不能在所有后续状态无条件要求 merchant.room 等于当前房间。Setup 夹具没有自然 Attach decision.room，应按其实际生成返回建立对应关系。

补入两个反例、缺失/null/已声明错误对象及合法离开后旧商店投影保留的控制即可；不要求提前支持 4D1C 补货或新增业务效果。

## 独立执行记录

目录：`.build/runs/n3d1b-repair-independent-review01/`。

- `review.py` / `summary.json`：201 次生产 reader 调用，分别为 148 个已有控制、23 个本轮原生证据包、9 个原误接受反例、21 个独立变体；291 项来源、完整原生结果、哈希和复现核对无失败。这里的 23 包包含预期故障，reader 预期拒绝并不等于原生执行成功。
- `followup.py` / `followup.json`：再执行一次库存所属房间共同篡改控制；共 **202 次 reader，3 个坏副本误接受**。只在新目录复制、修改和重封副本；原实现、原证据均未修改。
- `build-id-check.json`：本轮独立构建 ID 重算。
- `logs/`：每次实际 reader 原始输出。坏副本退出 0，不能用外围脚本退出成功替代审查结论。
- `closeout.json`：最终汇总及原文件保留核对。

可直接复现（当前均错误退出 0，修复后应退出 1）：

```powershell
dotnet .build/native-build/n3d1b-repair-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d1b-repair-independent-review01/independent-stats-after-inventory-prices
dotnet .build/native-build/n3d1b-repair-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d1b-repair-independent-review01/independent-attach-wrong-room-all-action-copies
dotnet .build/native-build/n3d1b-repair-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d1b-repair-independent-review01/independent-merchant-room-projection-mismatch
```

三个真实 Harmony 未知补丁探针的同构建证据已核对，六次拒绝成立。其范围继续限定为审计 API 夹具：没有初始化游戏 run，目标业务调用为 0，Ensure 基线及恢复均停在预期隔离不变量边界。不能作为实际游戏业务方法执行通过的证据。本轮没有重新执行这些探针。

## 后续交接

由实施回合修复上述两个有限契约缺口，再重新冻结、复跑适用统一矩阵并保留本次副本。原 R1 完整价格链绑定、R2 阶段必需性、R3 Travel 返回坐标均应保持；不扩商店效果或 Mod 范围。

[4D1C 交接](NATIVE_RUN_N3D1C_IMPLEMENTATION_HANDOFF.md) 的遗物购买、真实 Hook 业务区间和 Courier 补货顺序仍可执行，但先完成本轮必修与复验。4D 余项、步骤 5–6 和完整 M1 尚未完成。无需用户逐步批准；当前没有权限或环境阻塞，依赖阻塞是已证实的生产证据验收缺口。

本轮只写独立证据和审查文档；未改运行实现、角色、安装或真实存档，未启动游戏 UI、调用真实 Steam/网络，未提交或推送。
