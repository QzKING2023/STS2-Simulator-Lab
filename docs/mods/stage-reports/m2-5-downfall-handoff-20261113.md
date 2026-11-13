# M2-5 Downfall 交接报告 — 2026-11-13

## 结论

本轮没有把 Downfall 提升为 M2-5 `PASS`。固定快照和依赖闭包的静态发现通过；原生适配尝试在 Mod 初始化阶段发生 Godot native access violation，未产生真实 `RunVictory`/`Death`，因此当前状态为 `BLOCKED`：foundation、flow、stability gate 均未闭合，content gate 只保留静态发现和待运行分母。

## 固定输入

- 游戏：`v0.111.0 / 41cef1ea`
- Workshop 快照：`workshop-snapshot-20261106-163352`
- 快照摘要：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`
- Mod：Downfall `0.1.24`
- 依赖闭包：`BaseLib@3.4.7 -> Downfall@0.1.24`
- 选定复杂内容身份：Automaton 程序集（`REG.DOWNFALL.Automaton@0.111.0`）
- 配置：`.build/runs/m2-5-downfall-20261113/foundation-config.json`

未读取或使用当前 Steam `v0.113.0` 输入；没有修改 Mod、固定快照、manifest 或共享源码。

## 已完成的静态证据

使用 `SimulatorLab.ModWorker` 对固定快照的 Downfall 进行了新进程发现：

- 输出：`.build/runs/m2-5-downfall-20261113/static-worker/`
- `run-manifest.json`：`status=succeeded`、`exitCode=0`
- `dependencyClosure`：`BaseLib`, `Downfall`
- 发现文件：22
- 发现程序集：10（BaseLib、Downfall 以及八个角色程序集）
- discovery digest：`48c22ba9479f51476a8bc0817c932aeead880c053e86a62252f92fd040294f52`
- 固定 Downfall DLL/PCK 和各角色程序集的长度、SHA-256 已记录在 worker manifest

该结果只证明快照、依赖闭包和程序集元数据可审计，不能计入 complete content compatibility。

## 原生尝试

使用已有 `kaguya-full-20261113-r9` NativeRun build 的 `foundation-fixture` 运行了两个独立新进程：

1. `primary-direct`
   - BaseLib 正常初始化路径
   - 原生退出码：`-1073741819`
   - 失败位置：BaseLib initializer / Godot Logger 的 native access violation
2. `no-baselib-direct`
   - 禁用 BaseLib initializer，仅保留 Downfall initializer
   - 原生退出码：`-1073741819`
   - 失败位置：Downfall initializer / `Godot.OS` 的 native access violation

两次都在首个可观察战斗前崩溃，没有 `result.json`、native terminal 或可用于 Primary/Repeat/Replay 的完整 route。原始 trace 和控制摘要保留在：

- `.build/runs/m2-5-downfall-20261113/primary-direct/`
- `.build/runs/m2-5-downfall-20261113/no-baselib-direct/`
- `.build/runs/m2-5-downfall-20261113/attempt-summary.json`

## Gate 状态

| Gate | 状态 | 说明 |
|---|---|---|
| Foundation | BLOCKED | Downfall/BaseLib 初始化不能在当前 headless NativeRun seam 中安全完成 |
| Flow | BLOCKED | 未到真实 RunVictory 或 Death |
| Content | PARTIAL | 只完成静态注册候选和程序集发现；真实生命周期未运行 |
| Stability | BLOCKED | 没有可比较的 Primary/Repeat/Replay/Failure 四路结果 |
| 总体 | BLOCKED | 不满足 #12 关闭条件 |

## 下一步

需要新增一个可复用的 Downfall/多程序集角色加载与 headless Godot 初始化 seam，至少解决：

1. BaseLib 与 Downfall initializer 对 Godot Logger/OS 的 native 依赖；
2. 多角色程序集在同一 Mod 闭包中的初始化顺序和 `ModelDb` 登记；
3. 以 Automaton 为目标角色的真实起始牌组、首战和清理观察；
4. 再建立 Primary、Repeat、Replay、Failure 四路证据和内容矩阵。

在这些问题解决前，不应关闭 #12，也不应把 Downfall 纳入 M2-6 的 PASS 分母。
