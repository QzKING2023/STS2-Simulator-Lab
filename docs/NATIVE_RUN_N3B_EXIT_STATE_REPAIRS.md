# M1 步骤 4B：退出开始状态绑定修复

日期：2026-11-02。针对[独立复审](NATIVE_RUN_N3B_REPAIR_REVIEW.md)剩余 R2 完成最小修复和最终同构建回归，实施验收通过，交独立复验。4C–4D、步骤5–6继续按既有授权推进。

## 修复及边界

`NativeRoomContract.cs` 在 `room-exit-start` 的原归属、顺序和选择闭合检查后，将必需的对象快照与当前动作保存的不可变 `dispatch` 快照做完整结构比较，成功后才推进退出状态。比较复用现有结构比较器，覆盖嵌套资源、实例、RNG、字段缺失与额外字段；未逐字段限定金币或 RNG。

两个调用点均先记录此开始事件，随后才执行原生 `Exit` 或 `EnterMapCoord`，因此此处保持派发状态。原生退出/Travel 期间的换房、清场和资源变化仍保留在执行区间，返回状态继续绑定实际 confirmation。首次入房没有旧房间退出事件的 Travel 保持合法。实际房间执行、选择和原生牌效代码未改。

生产回归新增13个控制：Travel退出开始和相邻选择开始各覆盖金币、永久牌升级、run RNG、player RNG、新增字段、缺失扩展字段，另覆盖夹具 RoomExit 的金币矛盾。共79个 room 控制，其中76个损坏副本拒绝、3个正常/等价副本接受。每个新增副本另按完整 payload 核对实际变更，确认测试工具修改了指定快照。

## 冻结与复现

- Build ID：`C632744968AA67F2BF4E472681E2B0F5B285C49F748B9F33A3420AF3D78AD438`。
- Native DLL SHA256：`9992F72852960676F2CCA92E89EA5A61FA6834493FD8514E74DDA49F05A15325`。
- 最终 manifest：`.build/native-build/n3b-exit-state-final02/build-manifest.json`。
- 汇总、检查脚本和日志：`.build/runs/n3b-exit-state-work01/`，以 `summary.json`、`inspection.json`、`closeout.json` 为准。
- 42份实际源码输入另存该目录的 `source-snapshot-final02/`，逐文件 hash 与 manifest 一致；编译源码、原生输入、产物及 Build ID 均再次核对。
- 固定游戏 v0.111.0 / 41cef1ea，原版 Ironclad/A0，native-only/NotCompared，MemorySaveStore，一 Run 一进程。

```powershell
pwsh -NoProfile -File tools/native-run/run-native-n3b.ps1 -BuildManifest .build/native-build/n3b-exit-state-final02/build-manifest.json -OutputDirectory .build/runs/local-n3b-exit-state-check
```

输出目录必须尚不存在且位于 Lab `.build/runs`。`run-native-n3a.ps1` 和 `run-native-n2.ps1` 接受相同参数，统一验收时均使用上述最终 manifest。

四个原独立反例已在最终 DLL 的生产检查器上全部返回 exit1，诊断为 `Room exit start dispatch state mismatch`。复核副本位于工作目录 `review-regression/control-new-*`，包括自然金币、夹具金币、额外状态字段和玩家 RNG 漂移。直接复现之一：

```powershell
dotnet .build/native-build/n3b-exit-state-final02/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3b-exit-state-work01/review-regression/control-new-exit-start-player-rng-drift
```

## 同构建结果

| 范围 | 实际结果 |
| --- | --- |
| 4B | 34个原生进程、307项检查，0失败；79份房间控制全部符合预期 |
| 4A | 16个原生进程、273项检查，0失败；80损坏/8等价终态控制保持 |
| 步骤1–3/N2 | 74个原生进程、580项检查，0失败；自然skip24/take25及完整RNG、失败传播和快照负控保持 |
| 同构建合计 | 124个原生进程、1160项suite检查；逐个核对原始退出、result、trace header、构建与来源 |
| Core | 现有检查驱动实际执行98项通过，所用Core DLL与最终冻结产物一致 |
| 补充核对 | 694项，0失败；含原生结果、实例、RNG、完整快照、实际控制变更及文件hash |
| 旧审查包 | 最终生产reader读取394份旧包，全部符合预期；原九项及本轮四项反例均在语义门拒绝 |

原生进程的原始退出分布仍为80×0、39×1、2×2、3×3，分别按预登记结果核对。故障、Unsupported和预算停止没有用外层成功退出掩盖。394次reader读取采用最终DLL，但输入包保留原冻结身份；它们不计为当前构建新执行的原生场景。1,576个被复核的源证据文件前后hash不变。

DEV_01保持同一自然开局Run的71/71动作、7房间、3次实际战斗胜利、事件、指定实例休息升级及下战Play。DEV_02保持138/138实际原生Death。DEV_03保持WhisperingHollow的77确认/78派发、未确认Travel和明确Unsupported，数据不标完整。三条路径的route、choices、final-state与原AF641业务结果一致；DEV_01/02冻结重放及代表性minimal/off观察对照保持完整RNG状态一致。

相邻静止边界核对覆盖选择开始→dispatch、退出开始→dispatch，以及选择结果、leave-ready、exited、complete→对应confirmation。实际Travel返回与派发状态的合法差异也明确保留；4A原生业务清场及Closed例外由原矩阵继续验证。旧R1请求必需、唯一、当前选择绑定，以及R2返回/清理/奖励归属没有退化。

## 失败记录及文件范围

首个 `final01` 尝试在完成原生路径后，因自然和夹具金币控制共用目录名而以exit1停止，尚未运行后两套矩阵。输出目录改为“控制名＋来源场景”后重新冻结为final02，并从该最终产物完整重跑；未合并首轮局部成功为最终证据。失败日志和目录保留。

汇总脚本的一次历史目录重命名误改导致只读核对报FileNotFoundError，已恢复原AF641引用；`inspection-01-failed.log`保留，修正后只重做证据核对，原生结果未改。

本轮修改三个既有项目文件：`NativeRoomContract.cs`、`NativeRoomControls.cs`、`run-native-n3b.ps1`，并新增本报告。其余已有源码/文档与用户dirty工作保留。15个锁定Mod输入、52个兼容源码不变；未改角色、安装、真实存档，未启动UI或调用真实Steam/网络，未提交推送。

本轮完成4B剩余最小修复，待独立复验后接续[4C交接](NATIVE_RUN_N3C_IMPLEMENTATION_HANDOFF.md)。当前自然多节点片段和4A尾部夹具的证据范围保持；完整原版run及数据交付仍需完成后续步骤。没有权限、环境或执行阻塞。
