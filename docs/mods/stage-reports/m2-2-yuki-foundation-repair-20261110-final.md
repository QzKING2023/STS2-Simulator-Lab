# M2-2 YukiMakoto foundation/content BLOCKER 结案报告

日期：2026-11-10  
范围：YukiMakoto foundation/content，M2-2 运行时基础设施  
状态：**BLOCKER 已解决**

## 结论

此前唯一阻断项是 collectible `AssemblyLoadContext` 在真实 mod 初始化和 teardown 后仍然存活。根因不是普通 managed static 单独泄漏，而是 RitsuLib 的 `Runtime/Shared/Settings/Ui` 依赖程序集包含 Godot 生成的 `ScriptPath` 类型；这些程序集加载进 collectible ALC 后会登记进 Godot/native 运行时表，形成无法由 managed 静态扫描枚举的 native/runtime 根。

修复后的装载边界是：目标 mod entry assembly 单独进入 collectible ALC；依赖闭包（包括 RitsuLib 及其四个运行时依赖程序集）绑定到 `AssemblyLoadContext.Default`。teardown 仍清除目标程序集和默认上下文依赖程序集中的普通 managed registry，并解除 Harmony、事件、ScriptManagerBridge、运行队列和 action executor 的引用。这样目标程序集可以完整卸载，同时避免重复触碰 Godot 的 native 注册表。

另外保留 headless 的 `ScriptManagerBridge.LookupScriptsInAssembly` 跳过补丁，避免未初始化的 Godot bridge 在无 Godot display-server 的 watcher profile 中被直接调用。

## 代码变更

- [NativeFoundationModLoop.cs](D:/STS2-Simulator-Lab/tools/native-run/NativeFoundationModLoop.cs)：按目标程序集/默认依赖上下文分离加载；增加 headless script lookup guard；统一清理目标与默认依赖程序集的普通 managed 状态；完成 collectible ALC teardown。
- [Program.cs](D:/STS2-Simulator-Lab/tools/native-run/Program.cs)：teardown 前后取消 action executor、等待执行队列结束、清理 run graph 与 host 引用，避免 action task 保留目标类型。
- [NativePerformancePilot.cs](D:/STS2-Simulator-Lab/tools/native-run/NativePerformancePilot.cs)：移除临时诊断尾部空白，保留正式性能路径。
- watcher pinned host 的 `MainLoopContext.ReleaseForTeardown()`：清空并 dispose 主循环队列，随后完成 adding，防止队列线程根继续保留运行时对象。

## 固定输入与构建

- 游戏版本：`v0.111.0`
- 游戏 commit：`41cef1ea`
- 游戏程序集 SHA256：`0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9`
- workshop snapshot：`workshop-snapshot-20261106-163352`
- snapshot content SHA256：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`
- manifest SHA256：`09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`
- 构建产物：`.build/native-build/m2-2-yuki-foundation-repair-20261110-r260`
- build id：`70967F718AE6DC1564B824E9C2E642CA865D1F2607C49ABC2CF5B96B01361A24`

## 运行时证据

两条真实 foundation fixture 都通过 foundation gate，并在 teardown 后报告 `runtimeAlcTeardown=true`、`alcAlive=false`、`assemblyAlive=false`。

### Acheron + RitsuLib probe

- target：`Acheron lib/0.4.11`
- dependency closure：`STS2-RitsuLib compat/0.111.0` → `Acheron`
- gate：`PASS`
- loaded contexts：6（5 个默认依赖程序集上下文项 + 1 个目标 collectible ALC）
- run result：`Terminal=Ongoing`、`Stop=SliceBoundary`、`rawExit=0`
- evidence：`.build/runs/m2-2-yuki-foundation-repair-20261110-r260-acheron/`

### Remilia callback failure fixture

- target：`Remilia lib/0.111.0`
- dependency closure：`STS2-RitsuLib compat/0.111.0` → `Remilia`
- gate：`PASS`
- callback fixture 已观察到受控 callback exception；这条用例故意在 `OfferForRoomEnd` 注入 `NativeRuntimeFailureException`，用于验证失败路径清理。
- run result：`Terminal=Ongoing`、`Stop=RuntimeFailure`、`rawExit=1`；这是 fixture 设计的受控失败，不是 teardown blocker。
- teardown：`runtimeAlcTeardown=true`、`alcAlive=false`、`assemblyAlive=false`
- evidence：`.build/runs/m2-2-yuki-foundation-repair-20261110-r260-remilia/`

## 回归检查

- `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore`
- 结果：`PASS all 137 contract checks`
- stale temporary diagnostic scan：无残留 `SIMULATORLAB_FOUNDATION_SKIP/SCAN/CLEAR/FORCE/UNPATCH/TEARDOWN_WAIT`、`FoundationStopDiagnostic`、`StopAfter` 或 `[foundation-teardown]` 输出。
- `git diff --check`：无新增 whitespace error；仅保留仓库既有的 LF/CRLF 转换提示。

## 覆盖盘点

本次修复针对运行时 foundation blocker，不改变内容覆盖统计。当前 Acheron inventory 仍为 586 项：`Covered=0`、`Partial=8`、`Unrun=578`、`Unsupported=0`、`RuntimeFailure=0`。foundation blocker 已解除，后续可继续进行 content coverage 批次。

