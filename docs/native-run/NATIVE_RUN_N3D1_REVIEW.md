# M1 步骤 4D1 独立验收

日期：2026-11-02。结论：**未通过，生产证据检查器有两项必修。** 原生商店交易夹具的实际执行已核实；两项问题均位于证据的关联与完整状态校验，未发现当前原始轨迹中的购买或移除效果错误。此次仅审查并生成独立证据，没有修改运行实现，也没有重新运行原生游戏流程。

审查基线为 `62D4B09AC4FED8407EC8B7944DA4D54552C2244EB9F5D5C49C9001C7438C6816`，Native DLL SHA256 为 `1BE38606C947C4D47A63906DC6D2B1F71FC77DA054124BEE241BB672CA220820`。独立重算构建 ID、实际源码/编译副本/输入/产物哈希均匹配。

## R1：移除子选择没有成为必需且绑定结果的生命周期

位置：`tools/native-run/NativeMerchantContract.cs` 的 `Accept`、`ValidateResult` 和 `Finish`；通用 `NativeTraceContract` 只验证已经出现的选择，不知道本次商店移除必须出现哪个选择。

`MerchantRemove` 的结果目前验证删除了原牌组第二个实例、扣款和 Used，却不消费本次 `FromDeckGeneric` 请求/答复/返回。因而选择记录可以全部消失、重复或与实际效果矛盾。独立调用同一个最终 DLL 的生产 `--review` 后，下面六份已重新封装校验链的错误证据全部退出 0：

| 独立副本名称（均位于审查目录） | 矛盾 |
| --- | --- |
| `independent-missing-entire-removal-choice` | 成功移除的全部三条选择记录缺失 |
| `independent-missing-entire-cancel-choice` | 原生取消的全部三条选择记录缺失 |
| `independent-selected-i1-but-removed-i2` | 合法答复及返回一致选择 i1，实际删除/结果仍为 i2 |
| `independent-nonempty-selection-but-cancelled` | 非空合法选择，结果却为无效果取消 |
| `independent-choice-after-mark-and-hook` | 选择整体移到 SetUsed 与购买 Hook 之后 |
| `independent-extra-complete-removal-choice` | 同一次根动作多出第二组完整且不同 ID 的选择 |

最小修复：将本次移除的选择生命周期纳入生产商店契约，绑定当前 operation/root/context、真实选择 source、请求 ID 和永久牌实例。首次未使用且可负担的服务在本夹具内必须恰好有一次请求、一次答复和一次返回；成功时选择的实际实例必须等于 `removed` 和牌组差分；取消必须返回空集合且不收费/不标记；已使用服务的重复拒绝不得产生新选择。成功选择必须先于 mark，再到 Hook，再到原生交易返回，不能借用其他动作的选择。不得用固定 `old[1]` 的结果检查代替选择关联；指定非零实例仍保留为独立夹具断言。

修复时保留通用选择合法性门，覆盖这六个反例以及正常成功、取消、第二次拒绝。若未来增加缺钱移除或其他原生提前拒绝分支，按实际 native 分支声明是否应出现选择，不要求未执行的选择被伪造。

## R2：两个移除中间快照形成未绑定的独立状态岛

位置：`NativeMerchantContract.Accept` 的 `n3d-removal-marked` / `n3d-removal-hook` 分支与 `ValidateResult` 的成功移除分支。

生产检查器验证 marked 与 Hook 快照相等，且 Used=true，但没有把这两个快照与本次原生效果完成后的返回状态绑定。独立同时更改两份中间快照、保持其他证据不变，以下四份错误副本也全部退出 0：

| 独立副本名称 | 矛盾 |
| --- | --- |
| `independent-both-removal-phases-drift-gold` | 中间金币改成未付款的 1249，返回仍为付款后的 1174 |
| `independent-both-removal-phases-drift-hp` | 中间 HP=0，返回仍为 HP=80 |
| `independent-both-removal-phases-drift-rng` | 中间 Shops RNG counter 增加一次，返回恢复原值 |
| `independent-both-removal-phases-drift-futureReviewField` | 仅中间状态新增结构字段，其他静止边界不存在该字段 |

最小修复：在当前已声明的无购买 Hook 业务变更夹具协议内，保存不可变阶段状态，将 marked → Hook 进入 → 原生 return 完整结构绑定；本轮全部真实成功移除记录在这些位置确实完整相等。结合 R1，先确认选择返回和实际删除/付款/计数已完成，再接受 mark/Hook 的事实。不要继续仅增加金币、HP、RNG 特判。未来支持确有 `AfterItemPurchased` 业务效果的遗物时，应显式建立 Hook 进入/完成的变更区间及结果契约，保留其真实变化，不能把本轮静止要求泛化为禁止所有购买 Hook。

## 已核实的原生执行与无 UI 收尾

固定程序集的真实调用链为 `MerchantCardRemovalEntry.OnTryPurchaseWrapper` → 私有 `OnTryPurchase` → `OneOffSynchronizer.DoLocalMerchantCardRemoval`。OneOff 原生执行选择、扣款、`RemoveFromDeck` 和移除计数增加；随后游戏 UI 的 `NMerchantInventory.OnCardRemovalUsed` 转调 `NMerchantCardRemoval.OnCardRemovalUsed`，业务部分只有 `entry.SetUsed()`，其他为视觉更新。外层随后执行原生 `Hook.AfterItemPurchased`。

Lab 的 OneOff Task 后置包装等待原生任务成功，调用一次原生 SetUsed；Hook 前缀只观测并核验顺序，不跳过 Hook。原始 trace 中成功移除为：选择返回 i2 → marked → Hook（Used=true）→ return；两阶段与 return 的完整快照实际相同。取消及第二次失败没有 mark/Hook。没有理由撤销已实现的收尾位置；R2 需要加强证据验证。

原生库存为 7 卡 / 3 遗物 / 3 药水 / 1 移除，来自原生 MerchantRoom 和库存工厂。原始夹具实际支付 51 金币取得 offered 卡 i21、50 金币取得 offered 药水、75 金币删除重复 Strike 中指定的第二个实例 i2；其他永久牌内容、升级和顺序保持。缺钱、满槽、售罄分别实际返回 FailureGold、FailureSpace、FailureOutOfStock；取消与二次移除拒绝不额外收费。预派发旧票据/错误库存拒绝保留 3/3 前缀；交易后故障控制保留真实付款和新卡、3/4 前缀，终态 Unknown，不伪造回滚或成功确认。

这些是从正常 Neow 开局后使用原生 API 进入商店的**夹具**。额外金币、满槽和空钱包均为声明的准备操作。当前没有自然地图商店覆盖，不可称整个 4D 或完整 run 完成。

## 验收证据与计数

- 实现证据中的 204 个原生子进程、1910 项套件检查、Core98 已逐文件复核构建/原始退出/来源/隔离；此次没有重跑这 204 个流程或 Core。退出分布为 144×0、55×1、2×2、3×3，包含预期故障、Unsupported 和预算停止。
- 独立重放生产 reader **231 次**：71 个原商店控制、25 个原商店结果包（22 个成功夹具与 3 个预期失败）、115 个历史包、20 个新增副本。原有 211 包均符合预期；新增 10 个合法等价副本通过，以上 10 个错误副本被误接受。
- 269 项独立来源/产物/退出/重放核验和 47 项实际交易/前缀核验通过；实现者原 512 项核验的数量、内容及通过结果亦已核对。这些不是新增原生对局。
- 52 个冻结源码、33 个编译副本、3 个游戏依赖、7 个产物、15 个 Mod 输入和 52 个兼容源码哈希匹配。跟踪的 939 个原实现/证据文件未变。
- 自然 skip24 / take25、原奖励自然24动作、DEV01 的71/71片段、DEV02 的138/138真实 Death、DEV03 的77/78 WhisperingHollow Unsupported 与完整记录 RNG 重放仍成立。没有用成功种子替换失败。
- MemorySaveStore、shouldSave=false、Steam/UI 未初始化、NetSingleplayerGameService 和 no Mod/CombatSolver 均在过程证据中成立；商店原生过程各有2次 SaveProgress 被现有拦截，ReplayWrites/TelemetryCalls为0。不声称做过操作系统全文件写入监控。

独立证据目录：`.build/runs/n3d1-independent-review01/`。入口 `review.py`、结果 `summary.json`、交易事实 `facts.json`、冻结复算 `build-id-check.json`。原始证据和实现未改。示例重现：

```powershell
dotnet .build/native-build/n3d1-final02/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d1-independent-review01/independent-selected-i1-but-removed-i2
dotnet .build/native-build/n3d1-final02/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d1-independent-review01/independent-both-removal-phases-drift-gold
```

当前两条均错误退出0；修复后应退出1并给出语义拒绝原因。审查脚本外层退出0只表示完成检查，`summary.status=must-repair`、`readerMismatches=10` 才是此次验收结论。

## 后续衔接

先修复 R1/R2 并重新冻结适用统一回归，再独立复验；全 M1 授权已存在，无需再次向用户逐步请批。修复应在当前商店有限契约内完成，不要求提前支持其他内容。

通过后下一最小子阶段建议为**商店经济/历史语义闭合及自然进出**：先检查并恢复固定原生 Potion `CalcCost` 的价格抖动和 Shops RNG 分支、MerchantRoom.Exit 的未购商品历史，再用正常开局的同 Run 合法地图路径进入商店、实际交易/取消、合法离开。应复用原始算法和历史入口，保持现有保存/Steam隔离，不能全局关闭 TestMode 或重写价格公式。

这两项 TestMode 差异会影响 M1：三次药水价格抖动被跳过会改变本店价格及后续 Shops RNG；退出缺少未购商品历史会影响可信选择数据，历史是否被其他业务消费仍需审计。它们不阻断本次明确限定的夹具范围，但必须在自然完整流程的验收前闭合，不能仅披露后算 M1 通过。

随后仍按 [4D 后续交接](NATIVE_RUN_N3D1_NEXT_HANDOFF.md) 继续原生遗物购买/OnPickup 子选择、TheCourier 补货和动态价格、药水槽位/丢弃重试、下一商店移除费用、Driftwood reroll、实际卡替换/拒收、Sozu、Neow 新会话迁移和宝箱来源账本；WhisperingHollow/事件战斗恢复、Elite/Boss、步骤5完整跨幕 run 和步骤6批次数据均未完成。此次不实施这些后续功能，也不修改角色/安装/真实存档，不启动游戏 UI，不调用真实 Steam/网络，不提交推送。
