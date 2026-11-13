# M2-5 Kaguya RuntimeFailure Resume Prompt

将下面整段作为新会话启动提示词。

```text
你在 D:\STS2-Simulator-Lab 以独立 Agent 工作。

执行 M2-5 Kaguya RuntimeFailure 修复续作，只处理子票 #11。不要重新运行 triage，不创建重复票据，不扩展到第二个 Mod。

开工前读取并遵守：
- AGENTS.md
- GLOSSARY.md（如果存在）
- docs/agents/domain.md
- docs/agents/issue-tracker.md
- docs/agents/triage-labels.md
- docs/adr/README.md（如果存在）及涉及 M2 的 ADR
- docs/mods/MOD_FULL_RUN_EXECUTION_PLAN.md
- docs/mods/stage-reports/m2-5-kaguya-exhaustive-coverage-handoff-20261112.md

固定输入必须保持不变：
- 游戏 0.111.0，commit 41cef1ea
- 快照 local-mods/workshop-snapshot-20261106-163352
- 快照内容 SHA-256 7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73
- manifest SHA-256 09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3
- Kaguya Workshop 3747548629，版本 3.2.4
- BaseLib 3.4.7

首个 GitHub 命令前执行：
. .\tools\maintenance\Set-GitHubSession.ps1

然后执行：
gh issue view 11 --json number,title,body,labels,state

确认 #11 仍为 OPEN、标签为 ready-for-agent，#9 和 #10 已关闭。读取当前分支、HEAD、git status、固定快照和 manifest。保护所有已有未提交修改。

当前全量证据入口：
- .build/runs/m2-5-kaguya-20261112-exhaustive-final6/content-matrix.json
- .build/runs/m2-5-kaguya-20261112-exhaustive-final6/lifecycle-matrix.json
- .build/runs/m2-5-kaguya-20261112-exhaustive-final6/interaction-risk.json
- .build/runs/m2-5-kaguya-20261112-exhaustive/
- docs/mods/stage-reports/m2-5-kaguya-exhaustive-coverage-handoff-20261112.md

当前矩阵是 640 个单元，Covered=634，RuntimeFailure=6，Partial=0，Unsupported=0，Unrun=0。

只处理以下六个单元：
1. Kaguya.Encounters.TsukitoBosatsuBossEncounter
2. Kaguya.Monsters.TsukitoBosatsu
3. Kaguya.Events.EightThousandYearsMemory
4. Kaguya.Events.KaguyaEnding
5. Kaguya.Events.MoonNews
6. Kaguya.Events.YachiyoCupEvent

当前首个失败证据：
- TsukitoBosatsu 的 AfterAddedToRoom 创建 Godot.Timer，并调用 Godot 原生随机。
- KaguyaEnding 的 Proceed 调用正式 RunManager.OnEnded 和 game-over UI 状态。
- 其它三个事件在真实回调中停止音乐时进入 NDebugAudioManager.Stop，离线状态缺少对应运行时对象。

目标是建立真实、可复用的 native lifecycle seam。适配器可以隔离平台能力，但必须继续执行原始 Mod 回调和真实清理。不可使用空实现、静态结果、假 DTO、静默跳过回调、原版替代行为、预测死亡、运行时补丁、修改 Mod 或修改快照。

所有新运行必须使用新的 .build/runs 子目录，并记录 seed、快照摘要、游戏/Mod/依赖版本、worker、配置、adapter、collector 和输入摘要。

执行顺序：
1. 为每个失败单元建立最小复现目录并读取 stderr、trace、result、canonical input。
2. 找到第一个原生依赖缺口，区分 Godot 节点、音频状态、终局状态和 Mod 自身语义。
3. 优先复用已有 GameBootstrap、NativeHost、MainLoopContext 和平台旁路。
4. 只在能保留原始回调、原始状态变更和真实清理时实现 seam。
5. 先重跑对应单元，再重跑受影响类别的全量 probe。
6. 最后重跑 primary、repeat、replay、failure，并重建全量矩阵。

完成条件：
- 矩阵总数保持 640。
- RuntimeFailure、Partial、Unsupported、Unrun 全部为 0。
- 原有 full-run primary/repeat/replay/failure digest 保持稳定。
- 所有失败单元的 lifecycle 和 interaction 证据落入新报告。

如果任何单元仍需要跳过真实回调、替换 Mod 逻辑、修改快照、伪造结果或依赖未授权的正式 UI/云端状态，保留失败证据，将报告状态设为 BLOCKED，不关闭 #11。

完成后：
- 更新 docs/mods/stage-reports/m2-5-kaguya-exhaustive-coverage-handoff-20261113.md 或新版本报告
- 在 #11 留下证据评论
- 只有在 RuntimeFailure=0 且所有验收条件通过时才关闭 #11
- 默认不提交、不推送、不创建远程 PR
```
