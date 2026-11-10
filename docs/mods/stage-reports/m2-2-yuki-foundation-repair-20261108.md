# M2-2 foundation repair handoff — 2026-11-08

## 结论

**BLOCKED**。共享 foundation 仍未全部闭合，因此没有启动 Yuki 内容 probe，没有修改既有 586-unit content matrix，也没有关闭 issue #4。

## 工作区与输入

- 分支：`main`
- HEAD：`7718d76399da8b515794fe5d68c2bfdaba59ebbd`
- `git status`：保留用户进入前的 dirty 修改；本单元新增/修改仅为 generic native foundation seam、其 build/run evidence 和本报告。
- 固定游戏：`v0.111.0` / commit `41cef1ea`。
- 固定 snapshot：`workshop-snapshot-20261106-163352`，content SHA-256 `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`，manifest SHA-256 `09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`。
- 精确 Ritsu compatibility variant 存在且匹配：Runtime `3df9987d226b6d88b09727d19e99e51d3cf76c90d819d1cd04cf191d8b87fcf8`，loader `6cc6b9df7b892ed18480ad528168a804af46e24e376f0a77cfa26b57900c1b11`。
- Remilia 与 LexNinja2 config-driven cases 均把该 exact compatibility variant 放入真实 ModManager dependency closure；LexNinja2 的 Ritsu data startup 在 native host 暴露了实际前置缺口，未被伪装成成功。
- #4：`OPEN` / `ready-for-agent`；#3：`CLOSED`。两个查询均在 dot-source `tools/maintenance/Set-GitHubSession.ps1` 后完成，均 exit 0。
- 既有输入未覆盖或重分类；当前 SHA-256 记录在 `prior-evidence-sha256.json`。

## 实际修改

- 新增 `tools/native-run/NativeFoundationModLoop.cs`：配置驱动、无角色专用分支的 generic worker；加载真实 snapshot assemblies，接入 ModManager、重复注册/回滚、reload、Harmony ownership、room/turn/reward/before-death/combat-end callbacks、failure injection、registry teardown 和 evidence collector。
- 接入 `AssemblyResolver.cs`、`NativeLifecycle.cs`、`NativePerformancePilot.cs`、`NativeScenario.cs`、`NativeSolverProfile.cs`、`Program.cs`。
- 未修改 Mod、snapshot、DLL、PCK、游戏二进制或核心模拟器外部类型边界；generic source 不包含 Yuki 名称或 Workshop ID。
- 新 build：`.build/native-build/m2-2-yuki-foundation-repair-20261108-r6/`。build 命令 exit 0，保留 4 个既有 warning、0 errors。

## 命令与 evidence

- `gh issue view 4 ...`：exit 0。
- `gh issue view 3 ...`：exit 0。
- 固定 snapshot/manifest/游戏/variant 存在性与 SHA-256：exit 0。
- `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore`：exit 0，137/137 checks passed。
- `git diff --check`：exit 0。
- 新 evidence 根目录：`.build/runs/m2-2-yuki-foundation-repair-20261108/`。
- aggregate files：`canonical-input.json`、`foundation-audit.json`、`evidence-index.json`、`prior-evidence-sha256.json`。
- 运行目录：`acheron-primary-r2`、`acheron-repeat`、`acheron-replay`、`acheron-failure`、`acheron-reload`、`ryoshu-primary`、`remilia-primary-r8`、`lexninja2-primary-r3`。

## Foundation / flow / content / stability

- foundation：**BLOCKED**。真实注册、重复注册回滚和 Acheron reload 成功；跨房间 reward/card-pool 连续性和默认 ALC teardown 未闭合。
- flow：**PARTIAL**。Acheron、Ryoshu、Remilia 通过同一个 generic worker 进入真实 native run seam，并记录了 callback 顺序；各族在真实内容执行边界保留实际错误。
- content：**PARTIAL（generic fixture only）**。只记录共享 fixture 的内容 inventory；Yuki content probe **NOT STARTED**。
- stability：**PARTIAL**。Acheron primary/repeat digest 一致；replay case 保留 `InvalidAction`；failure case 保留 `RuntimeFailure`；observer detach 与 ModManager registry clear 成功；默认 ALC unload 仍不可证明。

## Generic fixture identity 与 content units

| 内容族 | registration identity | content units | 终态 |
|---|---|---:|---|
| Acheron | `REG.Acheron.lib/0.111.0#1@0.111.0` | Character 1 / Card 98 / Relic 14 / Power 35 / Other 4 | `Ongoing / EngineError`，merchant card pool failure |
| Ryoshu | `REG.Ryoshu.lib/0.111.0#1@0.111.0` | Character 1 / Card 107 / Relic 13 / Power 35 / Other 4 | `Unknown / EngineError`，真实 card callback `MissingMethodException` |
| Remilia | `REG.Remilia.lib/0.111.0#1@0.111.0` | Character 1 / Card 88 / Relic 8 / Power 24 / Other 3 | `Ongoing / EngineError`，真实 reward pool failure |
| LexNinja2 | `REG.LexNinja2.lib/0.111.0#1@0.111.0` | Character 1 / Relic 9 / Power 37 / Other 12 | `Unknown / EngineError`，Ritsu data registration prerequisite未在 native host闭合 |

## Lifecycle matrix and interaction risks

| 语义 | 真实证据 | 状态 | 风险/缺口 |
|---|---|---|---|
| registration identity | ModManager inventory、assembly association、4 个 config-driven identities | PARTIAL | Acheron duplicate association observed and rolled back；依赖 startup 仍受 Godot boundary 影响 |
| duplicate strategy | `before/after` assembly count，duplicate observed + rollback true | PASS for seam | 需要在可初始化的 shared dependency path 再复核 |
| reload | Acheron `reloadAttempted=true`、`reloadSucceeded=true` | PARTIAL | 只证明 ModManager registry reload；不证明完整 content registry reset |
| callback/hook order | room-enter、turn、reward、before-death、combat-end observers；Acheron 44、Ryoshu 8、Remilia 26 events | PARTIAL | 真实 callback exception 已触发并分类为 `RuntimeFailure`；跨族 reward continuation 未完成 |
| cross-room secondary/Power/Relic continuity | Acheron/Remilia 到达真实 reward/next-room boundary 前被内容池错误截断 | BLOCKED | 没有完整跨房间连续性、丢失与重复注册闭环 |
| cleanup | `observerDetached=true`、`registryCleared=true`；正常/failure case 均写入 cleanup evidence | PARTIAL | default `AssemblyLoadContext` 无法卸载；残余 Mod patch owner 不作通过证据 |
| platform capability | headless TestMode、memory save、offline Steam/network guard | PLATFORM GAP | UI、正式 save、map presentation、network、Steam/cloud 未覆盖；visible-policy 另行记录 |

## New run terminal and digest summary

Generic repair runs保留实际终态：Acheron primary/repeat 为 `Unknown / EngineError` 且 fixture digest 一致；Acheron replay 为 `Ongoing / InvalidAction`；Acheron failure 为 `Ongoing / RuntimeFailure`，callback interruption 被实际观察；Acheron reload 为 `Unknown / EngineError` 但 reload 成功证据已写入；Ryoshu、Remilia、LexNinja2 均保留各自 `EngineError` 原因。没有把任何失败提升为 Death 或 Completed。

既有 Yuki runs 未重跑、未覆盖、未重分类：

- primary：`Death / Completed`；state `79ca17c18aadeac4b979bca04eceb87f4711a5f6a7500941ee8f63ba89e3d8e2`；RNG `620a655bd2e4574686aa860b3b01af30ebc16020f4e9b556db479b1d3e1a4f09`；trace `dd8513bec6fa299b3f474a04c9b8a63bcebeb7762755b5a957443e0c3fe26f5b`；route `b6a490a8c33fbd471f9457bc284c63dd0b7a4d4c83c20ad37e1fa4b1184070e7`。
- repeat：同上全部 digest。
- replay：同上全部 digest。
- failure：`Unknown / BudgetExceeded`；state `a54b5f510189b75e2e579087059defd4d52c19288fd421616e227542e193d5ba`；RNG `dbcbe2b7e6f06c4ed23e2b24cf9212c373cf8059f2cbfe6b55ed782706893b30`；trace `b079bbeaf580370e99d734f950aabd476277cdb3e7430f116d921560e0f7c9b8`；route/choice digest 为 null。

## Coverage denominator and uncovered semantics

既有 Yuki content matrix 保持 append-only 原状：586 units；`Covered=0`、`Partial=8`、`Unrun=578`、content `RuntimeFailure=0`。本单元没有增加 Yuki content coverage。

Foundation repair 新增的 generic evidence 仍有 `Partial`：registration reset 的完整 shared dependency semantics、reward 后 secondary resource/Power/Relic continuity、failure rollback 的跨族一致性、默认 ALC teardown。`Unsupported`/platform gap：UI、正式 save、map presentation、network、Steam/cloud、visible-policy。`Unrun`：任何 Yuki 内容族 probe及第二角色；`RuntimeFailure`：各 generic case 的实际 runtime errors，均保留原分类。

## 下一唯一入口

先补齐 generic Godot/Ritsu startup boundary 与 default ALC teardown 的真实跨族证据，再在同一 repair evidence 目录下重跑 foundation audit。只有 foundation 全部 PASS 后，才允许从既有 586 identities 中选择一个最小 Yuki 内容族；在此之前不启动任何 Yuki content probe、不接入第二角色、不进入 M2-3。

未提交、未推送、未关闭 issue #4。
