# M2-6 St2bs 固定 roster 诊断报告（2026-11-13）

## 结论

本轮没有闭合 St2bs 的 M2-6 接受条件。固定输入下已经取得可审计的加载和 ModelDb 失败证据，但 foundation、flow、content、stability、platform 均保持 `BLOCKED`。没有把静态类型、部分 ModelDb 计数或通用 fixture 的 loader 结果提升为真实兼容性结论；没有修改公共分派文件、St2bs/BaseLib、固定快照或游戏二进制。

## 固定输入与配置

| 项目 | 值 |
| --- | --- |
| 游戏 | `0.111.0` / `41cef1ea` |
| Workshop 快照 | `workshop-snapshot-20261106-163352` |
| 快照摘要 | `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73` |
| manifest 摘要 | `09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3` |
| Mod | `St2bs@v2.3.0` / Workshop `3749061519` |
| 依赖闭包 | `BaseLib@3.4.7 -> St2bs@v2.3.0` |
| adapter | `native-foundation/v1`，目标数据在 `.build/diag/st2bs-foundation-load-only-20261113.json` |
| collector | `isolated NativeRun/GameProbe; foundation-trace/v1` |

配置只引用冻结快照中的 `BaseLib.dll`、`BaseLib.pck`、`St2bs.dll`、`St2bs.pck`。第二个诊断运行把两个 Mod 的 `Initialize` 设为 `false`，仅观察 assembly association 和 ModelDb augmentation；这不是完整 Mod 初始化，也不能作为通过证据。

## 试跑与证据

### 1. 完整 initializer 路径

输出目录：`.build/runs/m2-6-st2bs-foundation-20261113/`

BaseLib initializer 在 `BaseLib.Utils.FmodAudio+Methods..cctor()` 触发 Godot `StringName` native interop，进程退出 `-1073741819`（`0xC0000005`）。原始堆栈保留在 [stderr.log](../../../.build/runs/m2-6-st2bs-foundation-20261113/stderr.log)，运行清单记录 `stop=ProcessCrashed` 且没有完整 `result.json`。因此当前普通 NativeRun 不能把 BaseLib/St2bs initializer 当成可用 foundation 前置。

### 2. load-only ModelDb 诊断

输出目录：`.build/runs/m2-6-st2bs-foundation-load-only-20261113/`

通用 foundation fixture 完成了 assembly association，并在 `FinishModelDb()` 处暴露出确定的内容冲突：

```text
DuplicateModelException:
ModelDb already contains ID POWER.GUARDED_POWER mapped to
MegaCrit.Sts2.Core.Models.Powers.GuardedPower,
but St2bs.St2bsCode.Powers.GuardedPower attempted to map the same ID.
```

机器可读证据：[foundation-fixture.json](../../../.build/runs/m2-6-st2bs-foundation-load-only-20261113/foundation-fixture.json)、[foundation-lifecycle.json](../../../.build/runs/m2-6-st2bs-foundation-load-only-20261113/foundation-lifecycle.json)、[foundation-cleanup.json](../../../.build/runs/m2-6-st2bs-foundation-load-only-20261113/foundation-cleanup.json)。fixture 在失败前捕获到目标程序集的候选内容计数：`Power=87`、`Other=1`、`Relic=75`；这些只是扫描/构造前的诊断计数，不能替代完整 content inventory。

注册边界显示 `loaded=true`、`duplicateAttempted=true`、`duplicateRolledBack=true`。但 engine error 后 cleanup 未闭合（`observerDetached=false`、`registryCleared=false`、`runtimeAlcTeardown=false`），随后静态 root 诊断再次触发 Godot 崩溃；`run-manifest.json` 因此保留 `ProcessCrashed`。这条证据只能证明阻塞点和部分注册边界，不能证明清理成功。

## 固定 roster 状态

机器可读汇总：[st2bs-m26-status.json](../../../.build/runs/m2-6-st2bs-foundation-load-only-20261113/st2bs-m26-status.json)。

| 门/路径 | 状态 | 依据 |
| --- | --- | --- |
| Foundation | `BLOCKED` | BaseLib initializer 的 Godot native crash；ModelDb 冲突；cleanup 未闭合 |
| Content | `BLOCKED` | `POWER.GUARDED_POWER` 重复 ID；87 Power/75 Relic 仅为诊断计数 |
| Flow | `BLOCKED` | 没有合法的 St2bs `RunVictory` 或 native `Death` |
| Stability | `BLOCKED` | 没有可比较的 Primary/Repeat/Replay/Failure cohort |
| Platform | `BLOCKED` | 进程 raw exit `-1073741819`；没有稳定 native result |
| Primary | `BLOCKED` | 诊断启动到 ModelDb/engine error |
| Repeat | `Unrun` | Primary 未建立 |
| Replay | `Unrun` | 没有可重放的 primary route |
| Failure | `Unrun` | 尚无合法初始化路径可定义 failure route |
| Native Death / Victory | `Unrun` | 未进入真实完整流程 |

## 下一步最小 seam

本轮不修改公共 `Program.cs` 或其他共享分派文件。要继续 #13，至少需要在 worker/adapter 边界提供可复用的两项能力：

1. **ModelDb 冲突诊断与声明式 admission policy**：把重复模型 ID、来源程序集和回滚结果记录为结构化 evidence；任何允许/排除都必须由版本锁定的内容配置显式声明，不能静默丢弃 `GuardedPower` 来伪造完整内容覆盖。
2. **安全的 initializer/teardown profile**：在固定 `0.111.0` native host 中隔离 BaseLib 的 Godot logger/Fmod 初始化和静态 root 扫描，保证失败也能写出完整 cleanup；随后再为 St2bs 选择一个真实角色（`St2bs` 或 `Leike`）及一个复杂内容单元，生成真实 native `Death`，并用同一 canonical input 完成四路 cohort。

这些是通用 seam 的要求；本轮没有提交 St2bs 专用 shim，也没有绕过重复 ID。Issue #13 应继续保持 `BLOCKED`，直到真实内容生命周期、native terminal 和 teardown 证据全部补齐。
