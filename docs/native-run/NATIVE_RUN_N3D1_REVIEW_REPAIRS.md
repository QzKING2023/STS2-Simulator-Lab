# M1 步骤 4D1：移除选择与完整阶段状态修复

日期：2026-11-02。状态：**两项生产修复及最终同构建统一回归通过，待独立复验。** 本轮处理 [独立审查](NATIVE_RUN_N3D1_REVIEW.md) 的 R1/R2，范围仍为商店 API 夹具，不代表完整 4D 或 M1。

## 修复与原生行为

`NativeMerchantContract` 现在逐操作消费子选择，要求首次可负担且未使用的移除服务恰好出现一次原生 FromDeckGeneric 请求、答复、返回。请求绑定本次 operation/root/context，候选映射绑定当前永久牌实例、型号和升级。结果要求本次 selected、removed 与实际永久牌组差分一致；取消要求空返回和无效果；已使用服务的重复拒绝不得出现选择。缺整组、重复、借用旧选择或晚到选择均不能通过。指定第二个重复实例的夹具断言继续独立保留。

成功移除必须按选择返回 → 原生 OneOff 完成/SetUsed → AfterItemPurchased 进入 → 交易返回排序。marked 状态保留为不可变副本，与 Hook 进入、交易返回做完整结构比较，覆盖未来新增字段；同时篡改两个中间状态仍会因返回状态不符被拒绝。当前裸角色夹具没有购买 Hook 的业务变更；未来真实遗物效果应建立明确 Hook 进入/完成业务区间，不能把本轮静止要求泛化为禁止所有购买效果。

本轮只修改三个运行代码/工具文件：`NativeMerchantContract.cs`、`NativeMerchantControls.cs`、`run-native-n3d1.ps1`。原生购买、付款、移除、Used 收尾位置和 Hook 执行没有改动；静止状态比较与旧差分字段均保留。Exit 的 decision 库存身份也纳入当前会话校验。所有商店操作的边界见 [生命周期契约](NATIVE_RUN_N3D1_LIFECYCLE_CONTRACT.md) 中的有限操作表。

## 定向验证

最终商店矩阵已完成 **25 个原生子进程 / 371 项检查**。253 个实际生产 reader 控制中，237 个损坏副本退出1，16 个结构等价副本退出0；控制都重新封装校验链后进入实际生产语义逻辑。

| 覆盖 | 验证方式 |
| --- | --- |
| 上轮十个误接受反例 | 最终 DLL 只读复查原始审查副本，十个均在 Merchant 语义门退出1 |
| 选择缺失、重复、身份与顺序 | 成功/取消缺整组，缺单个请求/答复/返回，重复各阶段，错误候选、源、父、context、ID、min、升级，移到 open 前/Hook 后/return 后，向重复拒绝借用完整选择 |
| 全部商店根操作边界 | Setup、Buy（卡/药水/满槽/缺钱）、Remove（成功/取消）、Exit 的 open/return/boundary 缺失、重复、错配、context、未知状态字段、ID；不允许选择的操作插入完整合法形状的选择组 |
| 同时修改中间状态 | marked 与 Hook 的金币、HP、完整 RNG、牌组、移除次数、未知结构字段共同漂移，仍被完整返回绑定拒绝 |
| 合法业务与等价 | 原生付款/取得、指定实例删除、取消和拒绝；所有八种正常场景的属性顺序与一致状态扩展；冻结重放、minimal/off 对照 |

商店原实际动作计数和错误前缀保持：正常5/5，售罄/重复移除6/6，旧票据/错误库存3/3，真实交易后故障控制3/4且保留已付款和新卡，不伪造回滚或确认。

## 冻结与证据

- Build ID：`7C4D2069013543D1930EFD98BFD14C95C246E9384B527C32A733E43BAC5E03D3`。
- Native DLL SHA256：`D4BD95107225AC4C0EDB6556485BC4A9EDEFF4FD99589B18B03BB4036077DF42`。
- 冻结：`.build/native-build/n3d1-repair-final01/build-manifest.json`；0 warning / 0 error。
- 真实 dirty 源码快照：`.build/runs/n3d1-repair-work01/source-snapshot-final01/` 及对应 JSON；构建 ID 独立重算在 `build-id-final01-check.json`。
- 商店：`.build/runs/n3d1-repair-final01-merchant/summary.json`、`merchant-controls.json`。
- 231 个上轮证据包只读复验：`.build/runs/n3d1-repair-work01/final01-old-review.json`，包含原71商店控制、25结果包、115历史包及20独立副本；0失败，924个原文件不变。它们保留历史构建身份，不冒充新原生执行。
- 统一回归入口/进度：`.build/runs/n3d1-repair-work01/run-final01.ps1`、`final01-progress.json`；最终结果以同目录 `summary.json`、`facts.json`、`closeout.json` 为准。

候选过程也保留：candidate01 因新增控制夹具的一处括号错误编译失败，没有运行原生流程；candidate02 0警告/0错误，231旧证据全部符合预期。随后重新冻结 final01，候选证据不并入最终原生计数。未覆盖后续价格/历史语义、自然商店、遗物补货或其他新功能。

```powershell
pwsh -NoProfile -File tools/native-run/run-native-n3d1.ps1 -BuildManifest .build/native-build/n3d1-repair-final01/build-manifest.json -OutputDirectory .build/runs/local-merchant-repair-check
dotnet .build/native-build/n3d1-repair-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d1-independent-review01/independent-selected-i1-but-removed-i2
```

第一条要求全新输出目录；第二条应以 Merchant 的选择/实际移除不一致原因退出1。

## 同一最终产物统一回归

| 套件 | 原生子进程 | 检查 |
| --- | ---: | ---: |
| 4D1 商店 | 25 | 371 |
| 4C 奖励/药水/宝箱 | 55 | 561 |
| 4B 原生房间 | 34 | 307 |
| 4A 真实终态/尾部夹具 | 16 | 273 |
| N2 / 步骤1–3 | 74 | 580 |
| 合计 | **204** | **2092** |

Core98 实际执行通过，512 项来源/状态/流程核对及47项独立交易事实核对均0失败。所有原生子进程使用同一 final01 构建及对应真实源码快照；原始退出分布仍为144×0、55×1、2×2、3×3，包括预期 InvalidAction/EngineError/Unsupported/BudgetExceeded，并非204条自然成功路径。外层完成标记之外，同时核验各原始退出、确认/派发前缀、来源、完整记录 RNG 和数据完整性。

原生实际购买、指定实例移除、取消和重复拒绝继续通过；marked/Hook/return 在当前有限协议内完整相等。自然 skip24/take25 和奖励自然24动作保持；DEV01 为71/71片段，DEV02 为138/138真实 Death，DEV03 为原种子 WhisperingHollow Unsupported77/78。真实死亡与 Architect RunVictory 夹具保留，夹具胜利不代表正常开局完整通关。

52个冻结源码/33个编译副本/3个游戏输入/7个产物匹配，15个 Mod 输入和52个兼容源码不变。MemorySaveStore、shouldSave=false、Steam/UI未初始化、原生单机网络服务及 no Mod/CombatSolver 检查保持。HEAD仍为 `d73039d688eb54c9513f5b9508a03947e689aa18`；原始证据、既有工作和历史失败保留。

## 接续范围

本轮交独立复验后，继续 [4D1 后续交接](NATIVE_RUN_N3D1_NEXT_HANDOFF.md) 的商店价格/RNG/退出历史语义及自然地图进出，再闭合遗物/补货、奖励、事件与 Boss，随后步骤5–6。TestMode 跳过药水抖动会影响价格和后续 Shops RNG；省略未购历史会影响可信数据，均需在完整原生 run 前闭合。15 个 Mod 输入和52个兼容源码冻结继续有效。

本轮无提交推送，不改角色、游戏安装或真实存档，不启动 UI，不调用真实 Steam/网络。没有权限拒绝或待定动作。
