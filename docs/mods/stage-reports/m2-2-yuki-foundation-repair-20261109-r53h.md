# M2-2 Yuki foundation repair — 2026-11-09 r53h

## 结论

**BLOCKED**。本阶段继续只修共享 foundation，没有启动 Yuki 内容 probe，没有修改既有 586-unit content matrix，没有接入第二角色，没有开始 M2-3，也没有关闭 issue #4。

本阶段把 host-owned 的胜利房间引用和 lifecycle pending task 从 teardown 路径中释放，并把 cleanup 证据扩展为 host reference、pending task、worker/static 和 singleton-instance root 检查。Acheron 正常路径与 Remilia controlled callback-failure 路径都能写出完整 cleanup，且都报告 observer detached、registry cleared、pending task cleared、host references cleared；两者的 collectible runtime ALC 仍然存活。共享 foundation 的 runtime unload 语义仍未闭合，因此 gate 继续阻塞。

## 分支、票据、工作区

- 分支：`main`
- HEAD：`7718d76399da8b515794fe5d68c2bfdaba59ebbd`
- #4：`OPEN`、`ready-for-agent`
- #3：`CLOSED`
- 工作区原有 dirty 修改保留；没有执行 reset、clean、checkout、覆盖复制或删除。
- 本阶段没有提交、推送或关闭 issue。

## 固定输入

- 游戏：`v0.111.0` / commit `41cef1ea`
- snapshot：`workshop-snapshot-20261106-163352`
- snapshot content SHA-256：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`
- snapshot manifest SHA-256：`09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`
- RitsuLib exact runtime SHA-256：`3df9987d226b6d88b09727d19e99e51d3cf76c90d819d1cd04cf191d8b87fcf8`
- RitsuLib loader SHA-256：`6cc6b9df7b892ed18480ad528168a804af46e24e376f0a77cfa26b57900c1b11`
- Yuki exact `lib/0.111.0` SHA-256：`442113614bbaf43cc988cf5f917f82be3a41dd57ee0a7d19b1e6b5ccb360caa2`
- Yuki identity：`REG.YUKI.lib/0.111.0#1@0.111.0`
- canonical seed：`LAB_M2_2_YUKI_01`

## 实际修改

本阶段只修改 generic native host / foundation fixture 接入点：

- [NativeHost.cs](/D:/STS2-Simulator-Lab/tools/native-run/NativeHost.cs)：新增 `ReleaseForTeardown()`，清除 host-owned `LastVictoriousRoom` 和 `Observe`，并暴露 teardown 断言。
- [NativeLifecycle.cs](/D:/STS2-Simulator-Lab/tools/native-run/NativeLifecycle.cs)：新增 `ReleaseForTeardown()`，清除 `ActTask` 和 lifecycle active/pending 状态。
- [Program.cs](/D:/STS2-Simulator-Lab/tools/native-run/Program.cs)：把上述 host/lifecycle release 接入统一 teardown。
- [NativeFoundationModLoop.cs](/D:/STS2-Simulator-Lab/tools/native-run/NativeFoundationModLoop.cs)：记录新的 cleanup flags，扫描 worker/static 与 `RunManager`/`CombatManager` instance roots；把 Mod static 诊断限制为 collectible field types，避免在 RitsuLib 未初始化的 Godot static constructor 上触发 native crash。

没有修改 Mod、snapshot、DLL、PCK、游戏二进制、Yuki inventory 或核心模拟器外部类型边界。

## 新证据与命令

新的 aggregate 目录为 [m2-2-yuki-foundation-repair-20261109-r53h](/D:/STS2-Simulator-Lab/.build/runs/m2-2-yuki-foundation-repair-20261109-r53h/)，包含 canonical input、foundation audit 和 evidence index。

### Acheron normal

目录：[r53h-acheron](/D:/STS2-Simulator-Lab/.build/runs/m2-2-yuki-foundation-repair-20261109-r53h-acheron/)

- seed：`LAB_M2_2_FOUNDATION_ACHERON_R52`
- identity：`REG.Acheron.lib/0.111.0#1@0.111.0`
- units：Character 1 / Card 98 / Relic 14 / Power 35 / Other 4
- 29/29 confirmed，`Ongoing / SliceBoundary`，到达 `Travel:29`
- 28 ordered callback events；真实 reward→next-room boundary
- cleanup：`observerDetached=true`、`registryCleared=true`、`pendingTaskStateCleared=true`、`hostReferencesCleared=true`
- runtime：`runtimeAlcTeardown=false`、`alcAlive=true`、`assemblyAlive=true`

### Remilia controlled callback failure

目录：[r53h-remilia-callback](/D:/STS2-Simulator-Lab/.build/runs/m2-2-yuki-foundation-repair-20261109-r53h-remilia-callback/)

- seed：`LAB_M2_2_FOUNDATION_REMILIA_R53_CALLBACK`
- identity：`REG.Remilia.lib/0.111.0#1@0.111.0`
- dependency closure：`STS2-RitsuLib compat/0.111.0` → `Remilia lib/0.111.0`
- units：Character 1 / Card 88 / Relic 8 / Power 24 / Other 3
- 37/37 confirmed，`Ongoing / RuntimeFailure`，错误为 controlled foundation callback interruption
- 33 ordered callback events，failure 在真实 reward callback 前缀处中断
- cleanup：`observerDetached=true`、`registryCleared=true`、`pendingTaskStateCleared=true`、`hostReferencesCleared=true`
- runtime：`runtimeAlcTeardown=false`、`alcAlive=true`、`assemblyAlive=true`

既有 Remilia normal raw crash 证据 `[r52-remilia2](/D:/STS2-Simulator-Lab/.build/runs/m2-2-yuki-foundation-repair-20261109-r52-remilia2/)` 保留，没有覆盖或重分类；其 raw exit 仍为 `-1073741819`。

### 验证命令

- `build-native-run.ps1 ... -OutputRoot .build/native-build/m2-2-yuki-foundation-repair-20261109-r53h`：0；3 个既有 warning，0 errors。
- Acheron `run-native-slice.ps1`：0。
- Remilia controlled callback `run-native-slice.ps1`：1；子进程结果一致，cleanup JSON 存在。
- `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore`：0，137/137 checks passed。
- `git diff --check`：0，只有既有 LF/CRLF warnings。
- generic fixture source Yuki/Workshop scan：无匹配。

## Foundation、flow、content、stability

- foundation：**BLOCKED**。registration、callback ownership/order、Acheron reward→next-room 边界和 controlled failure cleanup 已有新证据；normal/failure runtime collectible ALC teardown 仍失败。
- flow：**PARTIAL**。Acheron 到达下一房间；Remilia controlled failure 在 reward callback 中止，独立族的正常 reward continuity 仍未证实。
- content：**PARTIAL（generic fixture only）**。两族 inventory 已记录；Yuki content probe 未启动。
- stability：**BLOCKED**。Acheron 与 controlled Remilia 都保持 `alcAlive=true`；既有 Remilia normal raw crash 仍存在。

## Lifecycle matrix

| 语义 | 状态 | 当前证据与缺口 |
|---|---|---|
| registration identity / duplicate / rollback / reload | PARTIAL | Acheron identity、duplicate rollback 和两族 loading 已记录；runtime registry unload 后 ALC 仍存活，reload/failure 旧证据仍需与真实 runtime teardown 统一。 |
| callback/hook order / exception / ownership / detach | PARTIAL | Acheron 28 ordered events；Remilia 33 events 的 controlled reward interruption；两条 current path 都 detach；normal Remilia reward completion 未证明。 |
| reward 后跨房间 secondary resource / Power / Relic / identity | PARTIAL | Acheron 到达真实跨房间边界；Remilia 在 reward callback failure 停止，跨族连续性未闭合。 |
| normal/failure cleanup / registry unload / runtime ALC teardown | BLOCKED | 两条 current path 的 observer/registry/task/host flags 均通过，但 `runtimeAlcTeardown=false`、`alcAlive=true`、`assemblyAlive=true`；存在 Mod/依赖 static-clear failures。 |

## Interaction risk 与平台能力

| 风险 | 状态 | 说明 |
|---|---|---|
| secondary resource 跨房间连续性 | PARTIAL | Acheron 有真实边界；Remilia failure 在 reward 处中止。 |
| Power/Relic 状态连续性 | PARTIAL | content inventory 与 native boundary 有记录，跨族完整 continuity 未闭合。 |
| registration identity continuity | PARTIAL | identity 和 ModManager association 已记录；runtime unload 未闭合。 |
| callback ownership / detach | PARTIAL | current Acheron normal 与 Remilia controlled failure 都 detach；normal Remilia crash 仍未证明。 |
| UI、正式 save、map presentation、network、Steam/cloud | PLATFORM GAP | headless TestMode、memory save、offline native worker 已覆盖；visible-policy 单独记录。 |

## Yuki 状态与分母

既有 Yuki primary/repeat/replay/failure evidence 冻结保留：primary/repeat/replay 为真实 `Death / Completed` 且 digest 一致；failure 为 `Unknown / BudgetExceeded`，没有重跑或重分类。

既有 Yuki runtime inventory 仍为 `586` units：`Covered=0`、`Partial=8`、`Unrun=578`、`Unsupported=0`、`RuntimeFailure=0`。本阶段没有增加 Yuki coverage。

## 下一唯一入口

先找到安全、generic、可跨族复核的 runtime Mod teardown 机制，让 normal 与 failure path 都清除剩余 Mod/依赖/engine references 并使 collectible ALC 真正不可达；再用至少两个独立内容族重复四类 foundation 检查。只有 foundation 四项全部 PASS 后，才允许从既有 586 identities 选择最小 Yuki 内容族开始 probe。
