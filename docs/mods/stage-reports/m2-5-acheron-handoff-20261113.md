# M2-5 Acheron complex-content handoff — 2026-11-13

## 状态

**BLOCKED / PARTIAL，GitHub #14 保持 OPEN。**

本轮严格绑定 M2 固定输入：

- 游戏：`v0.111.0` / commit `41cef1ea`
- 游戏程序集 SHA-256：`0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9`
- Workshop snapshot：`workshop-snapshot-20261106-163352`
- snapshot content SHA-256：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`
- snapshot manifest SHA-256：`09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`
- Mod：Acheron `lib/0.4.11`，Workshop `3747588205`
- dependency closure：`STS2-RitsuLib compat/0.111.0`
- registration identity：`REG.Acheron.lib/0.111.0#1@0.111.0`

未使用当前 Steam `v0.113.0`，未修改 Mod、固定 snapshot、DLL、PCK 或既有 evidence。

## 已有可复用事实

最新 generic foundation evidence：

- `.build/runs/m2-2-yuki-foundation-repair-20261110-r260-acheron/`
- foundation gate：`PASS`
- `runtimeAlcTeardown=true`
- `alcAlive=false`
- `assemblyAlive=false`
- `observerDetached=true`
- `registryCleared=true`
- Acheron 运行终态：`Ongoing / SliceBoundary`，31/31 confirmed
- Acheron inventory：586 units，`Covered=0`、`Partial=8`、`Unrun=578`、`Unsupported=0`、`RuntimeFailure=0`

这证明了 Acheron + RitsuLib 的 shared foundation seam 和清理边界，但没有证明 M2-5 要求的复杂内容生命周期或完整 flow gate。

## 本轮验证

为当前源码重新构建了一个只用于本轮审计的 native host：

- build：`.build/native-build/m2-5-acheron-20261113-host/`
- `build-native-run.ps1`：成功，0 error，5 个既有 warning

使用该 host、固定输入和 Acheron foundation config 执行了新进程探针：

- 输出：`.build/runs/m2-5-acheron-20261113-foundation-primary/`
- run kind：`primary`
- scenario：`foundation-fixture`
- seed：`LAB_M2_5_ACHERON_20261113`
- 进程：退出码 `1`
- 终态：`Ongoing / EngineError`
- 已确认：31/31 dispatch/confirm，Acheron 与 RitsuLib 程序集真实加载，trace 和 cleanup 文件均生成
- 错误：`Expected Play, actual EventDecision`，发生在当前 `Program.Ready()` 的自然流程推进

该失败保留为真实 `EngineError`，不能提升为 `Death`、`RunVictory` 或 content gate 通过。它说明当前源码的自然流程协调器在这条 foundation 路径上出现了新的选择状态不匹配；它本身也不是 Acheron 复杂内容的生命周期证据。

## 阻塞原因

当前 [Program.cs](/D:/STS2-Simulator-Lab/tools/native-run/Program.cs) 只为 Acheron 提供 generic `foundation-fixture` 加载路径；没有 Acheron-specific content probe 或完整 flow scenario。现有分派分支覆盖 foundation、Watcher、Kaguya、Minion 等场景，但没有 Acheron 的角色选择、复杂内容进入、Primary/Repeat/Replay/Failure 和真实 native terminal 闭环。

因此当前不能满足 #14 的四项 acceptance criteria：

- 尚无 Acheron 复杂 content unit 的真实生命周期证据；
- 尚无 Acheron 内容参与后真实 `RunVictory` 或 `Death`；
- 尚无 Acheron 的四路稳定性闭环；
- 现有 586-unit inventory 仍以 `Unrun` 为主。

## 下一唯一入口

主会话需要在不修改 Mod 或固定输入的前提下，先建立可复用的 Acheron content probe / full-run seam：

1. 解决当前 `Ready()` 对 `EventDecision` 的流程处理，或明确将其分类为环境/协调器阻塞；
2. 在 worker/adapter 边界加入一个实际 Acheron 内容表面（例如 Card、Power、Relic 或 Encounter）的真实注册、构造、激活/效果、退出和 cleanup probe；
3. 使用同一 canonical input 生成新的 `primary`、`repeat`、`replay`、`failure` 目录；
4. 重新聚合 Acheron lifecycle matrix、interaction risk table 和 flow/content 状态。

在此之前，#14 应保持 OPEN，M2-6 只能将 Acheron 记为 `PARTIAL` 或 `BLOCKED`，不能宣称固定 roster PASS。

## 未做的操作

- 未提交、未推送、未修改 GitHub Issue；
- 未关闭 #14；
- 未删除旧证据或规划文档；
- 未修改共享源码、Mod、游戏快照、DLL、PCK 或 manifest。
