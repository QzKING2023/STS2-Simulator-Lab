# M2-2 Yuki foundation repair handoff — 2026-11-09

## 结论

**BLOCKED**。本工作单元只修复并审计共享 foundation，没有启动 Yuki 内容 probe，没有修改既有 586-unit content matrix，没有接入第二角色，也没有关闭 issue #4。

同一个 generic native worker 已在 Acheron 与 Remilia 两个独立内容族边界加载真实 snapshot assembly，并记录真实 registration、callback、reward 与跨房间边界。正常 Acheron teardown 能 detach observer 并清空 ModManager registry，但运行时 collectible ALC 仍存活；Remilia 在真实 reward pool 处以原生进程崩溃退出，failure cleanup 无法闭合。按阻塞规则，foundation gate 仍为 BLOCKED。

## 分支、输入与工作区

- 分支：`main`
- HEAD：`7718d76399da8b515794fe5d68c2bfdaba59ebbd`
- #4：`OPEN`、`ready-for-agent`
- #3：`CLOSED`
- 固定游戏：`v0.111.0` / commit `41cef1ea`
- snapshot：`workshop-snapshot-20261106-163352`
  - content SHA-256：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`
  - manifest SHA-256：`09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`
- exact RitsuLib `compat/0.111.0`
  - runtime：`3df9987d226b6d88b09727d19e99e51d3cf76c90d819d1cd04cf191d8b87fcf8`
  - loader：`6cc6b9df7b892ed18480ad528168a804af46e24e376f0a77cfa26b57900c1b11`
- exact Yuki `lib/0.111.0)：`442113614bbaf43cc988cf5f917f82be3a41dd57ee0a7d19b1e6b5ccb360caa2`

工作区原有 dirty 修改全部保留。当前工作单元实际新增或修改的是 generic foundation fixture 及其接入点、r52 build/run evidence、repair aggregate evidence 和本报告：

- 新增 [NativeFoundationModLoop.cs](/D:/STS2-Simulator-Lab/tools/native-run/NativeFoundationModLoop.cs)
- 接入 [AssemblyResolver.cs](/D:/STS2-Simulator-Lab/tools/native-run/AssemblyResolver.cs)、[NativeLifecycle.cs](/D:/STS2-Simulator-Lab/tools/native-run/NativeLifecycle.cs)、[NativePerformancePilot.cs](/D:/STS2-Simulator-Lab/tools/native-run/NativePerformancePilot.cs)、[NativeScenario.cs](/D:/STS2-Simulator-Lab/tools/native-run/NativeScenario.cs)、[NativeSolverProfile.cs](/D:/STS2-Simulator-Lab/tools/native-run/NativeSolverProfile.cs)、[Program.cs](/D:/STS2-Simulator-Lab/tools/native-run/Program.cs)
- 新增 build：`.build/native-build/m2-2-yuki-foundation-repair-20261109-r52/`
- 新增 run evidence：`.build/runs/m2-2-yuki-foundation-repair-20261109-r52-primary2/`、`.build/runs/m2-2-yuki-foundation-repair-20261109-r52-remilia2/`
- 新增 aggregate evidence：`.build/runs/m2-2-yuki-foundation-repair-20261109/`
- [.build/native-build/m2-1-protocol-v5/MainLoopContext.cs](/D:/STS2-Simulator-Lab/.build/native-build/m2-1-protocol-v5/MainLoopContext.cs) 已恢复到 manifest 记录的 SHA-256 `408239fd...`，未保留诊断性队列改动。

未修改输入包括既有 Yuki primary/repeat/replay/failure JSON、既有 `586`-unit content matrix、`.build/runs/m2-2-yuki-foundation-audit-20261108-r2/`、固定 snapshot/manifest、游戏二进制、Mod DLL/PCK，以及核心模拟器的外部类型边界。generic fixture source 不含 Yuki 名称或 Workshop ID。

## 命令与退出码

- `. .\tools\maintenance\Set-GitHubSession.ps1; gh issue view 4 ...`：0，#4 仍 OPEN/ready-for-agent
- `. .\tools\maintenance\Set-GitHubSession.ps1; gh issue view 3 ...`：0，#3 CLOSED
- 固定游戏、snapshot、manifest、RitsuLib 与 Yuki exact variant 存在性/SHA-256：0
- `build-native-run.ps1 ... -OutputRoot .build/native-build/m2-2-yuki-foundation-repair-20261109-r52`：0
- Acheron `run-native-slice.ps1`：0
- Remilia `run-native-slice.ps1`：1；子进程 raw exit `-1073741819`，没有伪造 result
- `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore`：0，137/137 checks passed
- `git diff --check`：0，只有既有 LF/CRLF warning
- generic fixture Yuki/Workshop scan：无匹配

新的 canonical input、audit、evidence index 在 [.build/runs/m2-2-yuki-foundation-repair-20261109/](/D:/STS2-Simulator-Lab/.build/runs/m2-2-yuki-foundation-repair-20261109/)。

## Foundation、flow、content、stability

- foundation：**BLOCKED**。registration、callback ownership/order、reward→next-room 边界已有真实证据；正常运行时 ALC teardown 未闭合，failure cleanup 也未跨族闭合。
- flow：**PARTIAL**。Acheron 到达真实 reward 后下一房间；Remilia 到达真实 reward callback 后遇到原生 reward-pool failure。
- content：**PARTIAL（generic fixture only）**。只记录 generic family inventory；Yuki content probe 未启动。
- stability：**BLOCKED**。Acheron r52 进程完整退出但 `runtimeAlcTeardown=false`；Remilia raw crash `-1073741819`。

## Generic fixture identity 与 content units

| 内容族 | registration identity | content units | r52 终态 |
|---|---|---:|---|
| Acheron | `REG.Acheron.lib/0.111.0#1@0.111.0` | Character 1 / Card 98 / Relic 14 / Power 35 / Other 4 | `Ongoing / SliceBoundary`, digest `6b7b966e962f99d45d9b99cd29c17ddac4996619a1f3b01aceb6f1aa2436e828` |
| Remilia | `REG.Remilia.lib/0.111.0#1@0.111.0` | Character 1 / Card 88 / Relic 8 / Power 24 / Other 3 | fixture recorded `Ongoing / EngineError`, digest `e4e46891271f1ad933e611f6905fb4c13edbbd9839d053412e9643e058cded94`; process later crashed at raw `-1073741819` |

Worker：`SimulatorLab.NativeFoundationWorker`。Adapter：`native-foundation/v1`。Collector：`isolated NativeRun/GameProbe; foundation-trace/v1`。没有添加 Yuki-specific adapter、没有注入结果、没有跳过真实回调。

## Lifecycle matrix

| 语义 | 证据 | 状态 |
|---|---|---|
| registration identity / duplicate / rollback | Acheron loaded=true，duplicateAttempted=true，duplicateRolledBack=true；Remilia 以同一 generic worker 加载 RitsuLib dependency closure；既有 generic reload/failure cases 保留 | PARTIAL |
| callback/hook ownership、顺序、异常中断、detach | Acheron 28 个 callback events，含 room-enter、turn、reward；patch ownership 记录 91 rows，正常 cleanup `observerDetached=true`；Remilia 26 个 callback events，真实 reward failure 后进程崩溃 | PARTIAL |
| reward 后跨房间 secondary resource、Power/Relic、identity、ownership | Acheron 真实 reward→`EnterMapCoord` 边界；shared state boundary 已记录，但跨族 secondary-resource/Power/Relic 连续性仍没有完整闭环；Remilia 在 reward pool 截断 | PARTIAL |
| normal/failure cleanup、registry unload、runtime ALC teardown、重复启动 | Acheron `registryCleared=true`、`observerDetached=true`，但 `runtimeAlcTeardown=false`、`alcAlive=true`、`assemblyAlive=true`；post-cleanup roots 仍包含 Ritsu `VariableMethod/VariableProperty` 与 Acheron `SpireField`/generated static roots。Remilia 没有最终 cleanup 证明 | BLOCKED |

Acheron cleanup 的 `postCleanupGameStaticAlcRoots=[]`，说明已扫描到的游戏静态根不是当前阻塞源；阻塞证据来自 Mod/依赖静态根和运行时 collectible ALC 仍存活。此前 load-only diagnostic 可卸载 ALC，但它没有真实 run teardown 语义，不能替代本 gate。

## Interaction risk 与平台能力

| 风险 | 状态 | 说明 |
|---|---|---|
| secondary resource 跨房间连续性 | PARTIAL | 真实 reward→next-room 边界可达；族专属 secondary resource 仍需完整 continuity evidence |
| Power/Relic 状态连续性 | PARTIAL | native state boundary 有记录，跨族完整保真未闭合 |
| registration identity continuity | PARTIAL | identity 与 ModManager association 有记录；registry unload 未闭合 |
| callback ownership / detach | PARTIAL | Acheron 正常 detach 通过；Remilia failure detach 未证明 |
| UI、正式 save、map presentation、network、Steam/cloud | PLATFORM GAP | TestMode、memory save、offline native worker 已覆盖；visible-policy 单独记录 |

## 既有 Yuki primary/repeat/replay/failure 终态

以下全部是既有输入，未重跑、未覆盖、未重分类：

- primary：`Death / Completed`；state `79ca17c18aadeac4b979bca04eceb87f4711a5f6a7500941ee8f63ba89e3d8e2`；RNG `620a655bd2e4574686aa860b3b01af30ebc16020f4e9b556db479b1d3e1a4f09`；trace `dd8513bec6fa299b3f474a04c9b8a63bcebeb7762755b5a957443e0c3fe26f5b`；route `b6a490a8c33fbd471f9457bc284c63dd0b7a4d4c83c20ad37e1fa4b1184070e7`。
- repeat：同上全部 digest。
- replay：同上全部 digest。
- failure：`Unknown / BudgetExceeded`；state `a54b5f510189b75e2e579087059defd4d52c19288fd421616e227542e193d5ba`；RNG `dbcbe2b7e6f06c4ed23e2b24cf9212c373cf8059f2cbfe6b55ed782706893b30`；trace `b079bbeaf580370e99d734f950aabd476277cdb3e7430f116d921560e0f7c9b8`；route/choice digest 为 null。

## Coverage 分母与未覆盖语义

既有 Yuki content matrix 保持 append-only 原状：总数 `586`；`Covered=0`、`Partial=8`、`Unrun=578`、`Unsupported=0`、`RuntimeFailure=0`。本单元没有增加 Yuki content coverage。

Foundation 仍有 Partial/Blocked：跨族 reload/failure registration teardown、reward 后 secondary resource/Power/Relic continuity、failure callback detach、运行时 Mod registry unload 和 collectible ALC teardown。平台缺口保留为 Unsupported/Platform Gap：UI、正式 save、map presentation、network、Steam/cloud、visible-policy。Yuki 内容 probe 与第二角色仍为 Unrun。

## 下一唯一入口

先为共享运行时 Mod teardown 找到安全、generic、可跨族复核的机制，清除正常与 failure path 的静态根并证明 collectible ALC teardown；再用至少两个独立内容族重复 foundation audit。只有 foundation 四项全部 PASS 后，才允许从既有 586 identities 中选择一个最小 Yuki 内容族开始 probe。

未提交、未推送、未关闭 issue #4。

