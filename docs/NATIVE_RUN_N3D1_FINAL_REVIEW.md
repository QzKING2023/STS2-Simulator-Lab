# M1 步骤 4D1：修复后的独立验收

日期：2026-11-02。结论：**通过当前有限商店夹具范围的独立验收，R1/R2 已闭合。没有发现本轮必须继续修复的问题。** 本轮只读实现和原始证据，执行生产证据检查器；没有重新运行原生游戏流程或修改运行代码。

这是 4D1 Card/Potion/移除/退出 API 夹具的验收，不代表自然商店、完整 4D 或完整 M1 已完成。下一步直接执行 [商店语义与自然进出交接](NATIVE_RUN_N3D1B_IMPLEMENTATION_HANDOFF.md)，不需要再次请求用户逐步授权。

## 冻结与证据身份

- Build ID：`7C4D2069013543D1930EFD98BFD14C95C246E9384B527C32A733E43BAC5E03D3`。
- Native DLL SHA256：`D4BD95107225AC4C0EDB6556485BC4A9EDEFF4FD99589B18B03BB4036077DF42`。
- 构建清单：`.build/native-build/n3d1-repair-final01/build-manifest.json`。本次独立重算 ID 一致；52 项源码、33 项编译副本、3 项游戏输入、7 项产物匹配，dirty 源码快照匹配。
- 15 项锁定 Eleanor 输入、52 项兼容源码保持不变；本轮没有恢复 Mod 专项。
- 原实现报告及失败历史仍保留：[首次审查](NATIVE_RUN_N3D1_REVIEW.md)、[修复报告](NATIVE_RUN_N3D1_REVIEW_REPAIRS.md)、[有限生命周期契约](NATIVE_RUN_N3D1_LIFECYCLE_CONTRACT.md)。

## R1：当前移除选择成为结果的必要证据

`NativeMerchantContract.Accept` / `ValidateChoice` / `ValidateResult` 现在共同要求：可付款且未使用的移除操作，恰好出现一次原生 `FromDeckGeneric` 请求、答复和返回；三者属于当前根动作、context 和请求 ID，必须先请求、后答复、再返回，且发生在 mark/Hook/交易返回之前。候选映射绑定当前永久牌组的实际实例、型号和升级；取消必须返回空集合，成功必须满足 selected = removed = 实际牌组缺失实例。已使用的重复拒绝不能借用旧选择。指定第二个重复 Strike 的独立夹具断言仍然存在。

原六个选择反例均由生产 reader 拒绝。额外独立副本覆盖先答后问、先返回后答复、取消声明与真实结果相反、错误 completion/max/implicitChoice、整组选择挪到先前根动作；均退出 1。合法地统一重命名当前请求的公开候选 handle，并保持其实际实例映射不变，成功/取消两种副本都退出 0。

这不是仅检查 trace seal：本次对独立副本重新生成有效 seal，再调用最终 DLL 的 `--review`。错误在选择、归属、顺序或结果语义门被拒绝。

## R2：SetUsed、Hook 进入和交易返回形成完整状态链

原生执行代码未被本次修复改变：`OneOffSynchronizer` 实际完成选择、付款、删牌和移除计数，`NativeMerchant.FinishRemoval` 等待其成功后调用一次原生 `SetUsed`；随后原生 `Hook.AfterItemPurchased` 仍然执行。Lab 的 Hook 前缀只观察与检查顺序。该位置对应固定版本 UI 的业务委托，原生购买入口没有被替换为手写交易。

reader 保存不可变 marked 状态，要求 marked → Hook 进入 → 原生交易 return 完整结构相等；随后 return → confirmed → boundary 与下一次 dispatch 继续绑定。原四个双快照协调篡改反例均被拒绝；额外同时篡改 marked 和 Hook 的牌升级、库存价格、房间、遗物、缺失字段也被拒绝。Hook 移到 mark 之前、把成功阶段挪给已使用拒绝，以及同时修改 dispatch/open 或 return/confirmed/boundary 的未知字段，均不能通过。

合法购牌、购药、指定实例删牌、取消和拒绝的真实业务变化仍通过。对象属性重排、所有对应状态一致增加扩展字段等合法等价副本不被误拒。本完整相等要求限定于**当前没有购买 Hook 业务变化的原版夹具**；后续真实遗物效果必须定义 Hook 进入/完成之间的业务区间，不能把这一限制推广成禁止所有原生 Hook 效果，也不能直接删除校验。

## 实际检查次数与结果

独立证据：`.build/runs/n3d1-repair-independent-review01/`，入口 `review.py`，结果 `summary.json`；交易事实复核 `facts.py` / `facts.json`，构建 ID 重算 `build-id-check.json`。

| 本次实际生产 reader 调用 | 次数 | 结果 |
| --- | ---: | --- |
| 最终构建的商店控制副本 | 253 | 237 损坏退出 1，16 等价退出 0 |
| 最终商店原生结果包 | 25 | 22 完整夹具接受，3 原始失败包按预期拒绝 |
| 上次独立审查包 | 231 | 全部符合修复后的预期，含原十反例及 115 历史包 |
| 本次独立生成副本 | 49 | 原十坏例重新构造、10 等价及新增 29 个副本全部符合预期 |
| 合计 | **558** | **452 次退出 1、106 次退出 0，0 个预期不符** |

49 个独立副本中，新增 29 个为 27 损坏与 2 等价。原十坏例分别在旧包和独立新副本中复核，并非二十个不同缺陷。历史包保持各自旧构建身份；用新 reader 重读，不冒充新原生执行。

本次另有 269 项来源/产物/退出/重放/隔离检查和 47 项交易事实检查，全部通过。跟踪的 2131 项原始证据/来源文件没有被此次审查改动。实现者的 512 项补充检查已逐项核对为通过，不能将这些检查数相加解释为独立对局数。

最终同构建原生证据的分布如下；本次只读核验原始退出、清单、来源和结果，没有重复运行这 204 个流程或 Core：

| 矩阵 | 原生进程 | 套件检查 |
| --- | ---: | ---: |
| 4D1 merchant | 25 | 371 |
| 4C suite | 55 | 561 |
| 4B | 34 | 307 |
| 4A | 16 | 273 |
| N2 | 74 | 580 |
| 合计 | **204** | **2092** |

Core98 独立列出，其驱动锁和最终 Core 产物身份匹配。原生退出分布为 144×0、55×1、2×2、3×3，包含声明的 InvalidAction、EngineError、Unsupported、BudgetExceeded，外层成功没有将它们改写为原生成功。

## 原始执行事实与保留限制

商店购牌实际支付 51 金取得 offered i21；购药支付 50 金取得 offered 实例；移除支付 75 金删除第二个 Strike i2，第一张 i1 和其余完整牌组内容、升级、顺序保持。取消、满槽、缺钱、售罄和重复移除按真实结果记录。交易后注入异常保留已付钱、已取得卡及 3/4 前缀，终态 Unknown；没有假回滚或确认第四个动作。旧票据/错误库存拒绝为 3/3 前缀。

自然 skip24/take25、奖励自然24动作、DEV01 的71动作片段、DEV02 的138动作真实 Death、DEV03 的77/78 WhisperingHollow Unsupported、完整记录 RNG 重放保持。4A 的正常死亡和 Architect 夹具尾部证据保持；尾部夹具不构成正常开局通关。

原生商店夹具记录 MemorySaveStore、shouldSave=false、Steam/UI 未初始化、NetSingleplayerGameService、未加载 Mod/BaseLib/CombatSolver；每个商店流程有两次已拦截的 SaveProgress，ReplayWrites/TelemetryCalls 为零。这些是宿主入口及结果证据，不是操作系统全文件写监控声明。本轮只执行 reader，没有启动原生游戏场景、真实 Steam/网络或游戏 UI。

以下必须继续完成，不能用披露限制代替 M1 验收：TestMode 跳过药水价格抖动/相应 Shops RNG、商店退出未购买历史、自然地图进入/离开；随后原生遗物购买与补货、奖励 reroll/替换/拒收、Sozu、Neow 迁移、宝箱账本、事件战斗恢复、精英/Boss、步骤5完整跨幕 run 和步骤6批次数据。当前仍是显式 GainGold + EnterRoomDebug 的 API 夹具，未经自然商店覆盖。

复现单个语义反例（预期退出 1）：

```powershell
dotnet .build/native-build/n3d1-repair-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d1-repair-independent-review01/independent-selected-i1-but-removed-i2
dotnet .build/native-build/n3d1-repair-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d1-repair-independent-review01/independent-coherent-pair-deck-upgrade
```

结论：当前 R1/R2 验收结束，无审批或执行阻塞。运行代码、角色、安装与真实存档未修改，未提交或推送。下一轮按已授权 M1 主线继续，不重新打开已通过的有限协议，也不提前宣称整段主线完成。
