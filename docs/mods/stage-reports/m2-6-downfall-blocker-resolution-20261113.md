# M2-6 Downfall 阻塞诊断 — 2026-11-13

## 结论

本轮没有关闭 Downfall（Issue #12）。固定的 `0.111.0/41cef1ea` 输入已完成静态闭包审计，并保留了一次带诊断适配器的独立原生尝试；该进程仍在 Mod 初始化阶段以 Windows access violation 退出。因此 Downfall 在 M2-6 的状态是 `BLOCKED`，不能计入四路真实内容或 native Death 的 PASS 分母。

## 固定输入与证据

- 游戏：`v0.111.0 / 41cef1ea`
- Workshop：`workshop-snapshot-20261106-163352`
- 快照 SHA-256：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`
- manifest SHA-256：`09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`
- Downfall：`0.1.24`
- 依赖闭包：`BaseLib@3.4.7 -> Downfall@0.1.24 -> Automaton@0.1.24`
- 注册身份：`REG.DOWNFALL.Automaton@0.111.0`

已有固定输入和静态 worker 证据位于：

- `.build/runs/m2-5-downfall-20261113/foundation-config.json`
- `.build/runs/m2-5-downfall-20261113/static-worker/run-manifest.json`
- `.build/runs/m2-5-downfall-20261113/attempt-summary.json`

静态 worker `status=succeeded`，发现 22 个文件、10 个程序集（BaseLib、Downfall 和 8 个角色程序集），discovery digest 为 `48c22ba9479f51476a8bc0817c932aeead880c053e86a62252f92fd040294f52`。这只证明固定包和程序集闭包可审计，不证明运行时内容兼容。

## 阻塞定位

原有两个原生尝试（`primary-direct`、`no-baselib-direct`）都以 `-1073741819` 退出，没有 `result.json` 或 terminal。为确认第一处 native 调用，另建了诊断适配器实验：

- 运行目录：`.build/runs/m2-6-downfall-adapter-diag-20261113/`
- adapter：`downfall-foundation-adapter-diag/v1`
- seed：`LAB_M26_DOWNFALL_ADAPTER_20261113`
- build：`.build/native-build/downfall-adapter-diag-20261113/`
- adapter 安装记录：`adapter.log`

该实验仅在隔离 build 中通过 ModuleInitializer 对 `Godot.OS.AddLogger` 加了跳过补丁，没有修改 `Program.cs`、`NativeFoundationModLoop.cs`、Mod DLL、snapshot 或游戏文件。运行仍然在 `BaseLib.BaseLibMain.Initialize()` 中崩溃，stderr 栈明确落在：

```text
Godot.NativeInterop.NativeFuncs.godotsharp_string_new_with_utf16_chars
Godot.StringName..ctor
Godot.Logger..cctor
Godot.Logger..ctor
BaseLib.Patches.Utils.LogListener..ctor
BaseLib.BaseLibMain.Initialize
```

所以第一处可复现阻塞不是 `OS.AddLogger` 本身，而是构造 `LogListener` 时触发的 `Godot.Logger` 静态初始化。Downfall 自身也声明了同类静态 `Logger`，即使跳过 BaseLib initializer，仍需同一类 headless Godot Logger/OS seam；当前通用 foundation fixture 没有这个 seam。

## Gate 矩阵

| Gate | 状态 | 可审计依据 |
|---|---|---|
| Foundation | `BLOCKED` | BaseLib/Downfall initializer 在 Godot Logger native 初始化前退出；诊断 adapter 只越过 `OS.AddLogger` 仍失败 |
| Flow | `BLOCKED` | 未进入首战，也没有真实 `RunVictory` 或 `Death` |
| Content | `PARTIAL` | 静态闭包和 10 个程序集可审计；Automaton 内容注册、构造、效果和退出未运行 |
| Stability | `BLOCKED` | 没有可比较的 Primary/Repeat/Replay/Failure 四路结果 |
| Platform | `BLOCKED` | 固定输入绑定、raw exit、stderr、trace 和 build manifest 可审计，但 headless Godot Logger 初始化仍发生 native access violation，尚无稳定 native result |
| Overall | `BLOCKED` | 不满足 #12 的关闭条件 |

## 最小通用 seam 建议（未并入公共宿主）

下一次公共宿主整合前，需要一个可复用的 headless Godot bridge，至少覆盖：

1. 在任何 Mod initializer 运行前安全处理 `Godot.Logger` 静态初始化及 `OS` logger 注册；必须记录调用顺序和禁用范围，不能只跳过一个后置 `OS.AddLogger`。
2. 保持 BaseLib、Downfall 和多角色程序集的真实初始化顺序，继续让 `ModelDb` 从真实程序集收集模型；不能用静态清单代替注册。
3. 资源包加载、音频 bank 注册和角色程序集加载需要显式 headless 能力或可审计拒绝结果；不能在 adapter 中直接伪造内容。
4. seam 通过后，使用同一 `REG.DOWNFALL.Automaton@0.111.0` 重新跑 Primary、Repeat、Replay、Failure，并分别观察真实 Automaton 首战和 native Death，再更新固定 roster matrix。

本报告只提出 seam，未修改公共 dispatcher，也未关闭 Issue #12。
