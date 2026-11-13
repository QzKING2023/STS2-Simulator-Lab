# M2-5 Kaguya exhaustive coverage handoff — 2026-11-13

本续作只处理 GitHub #11 的六个 RuntimeFailure 单元，固定输入未变：游戏 `0.111.0` / commit `41cef1ea`，快照 `workshop-snapshot-20261106-163352`，内容 SHA-256 `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`，manifest SHA-256 `09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`，Kaguya Workshop `3747548629` / `3.2.4`，BaseLib `3.4.7`。未修改 Mod、快照或正式输入。

实现位于 `tools/native-run/KaguyaNativeModLoop.cs`，复用了现有 GameBootstrap/NativeHost/MainLoopContext：

- `Godot.Timer` 使用 managed lifecycle seam：替换目标 async state machine 的 Timer 构造、属性设置、Timeout 绑定，并对缺失 `NCombatRoom.Instance` 使用静态 attach bridge；原始 `AfterAddedToRoom`、Timeout delegate 和 QueueFree 清理仍被执行或记录。
- `Godot.Engine.GetMainLoop` 只返回 headless null，使 Kaguya 原始视频回调继续运行到其自身的平台边界。
- `NDebugAudioManager` 在内容 probe 中使用有句柄的 managed Play/Stop/StopAll 状态机，保留原始事件回调和停止清理；full-run 保持既有旁路以维持历史 digest。
- `GD.RandRange` 使用当前 native run 的 `Niche` RNG，不使用静态或预测结果。
- `KaguyaEnding.Proceed` 只在其 async state machine 内为缺失 `NRun.Instance` 提供 managed UI receiver；`RunManager.OnEnded(true)` 原始调用保留，game-over UI 状态写入 lifecycle evidence。

验证结果：

- 最终矩阵：`.build/runs/m2-5-kaguya-20261113-exhaustive-final-r9/content-matrix.json`，总数 `640`，`Covered=640`，`RuntimeFailure=0`，`Partial=0`，`Unsupported=0`，`Unrun=0`。
- lifecycle 与 interaction 证据：同目录的 `lifecycle-matrix.json`、`interaction-risk.json`；14 encounters、23 enemies、16 events 全量 category probe 均 `exit=0`，`dataComplete=true`。
- 六个修复单元的独立 evidence 位于 `.build/runs/m2-5-kaguya-20261113-final-unit-*-r8/`；受影响类别汇总位于 `.build/runs/m2-5-kaguya-20261113-category-{encounter,enemy,event}-r8/`。
- full-run primary/repeat/replay/failure：`.build/runs/m2-5-kaguya-20261113-{primary,repeat,replay,failure}-r9/`。primary/repeat/replay 均为 `Death/Completed`，failure 为 `Unknown/BudgetExceeded`；与 2026-11-12 原有四路的 input/state/RNG/trace/route/choice digest 完全一致。
- 合约检查：`dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore`，`PASS all 137 contract checks`；`git diff --check` 通过。

状态：PASS。此报告和机器证据可供 #11 审核；未提交、未推送、未创建远程 PR。
