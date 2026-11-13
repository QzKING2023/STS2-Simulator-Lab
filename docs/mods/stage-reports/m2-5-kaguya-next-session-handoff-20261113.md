# M2-5 Kaguya 新会话交接

日期：2026-11-13

## 当前结论

当前子票 #11 保持 `OPEN`，标签为 `ready-for-agent`。阻塞票 #9 和 #10 已关闭。

全量矩阵已覆盖 640 个冻结内容单元：

| 状态 | 数量 |
| --- | ---: |
| Covered | 634 |
| Partial | 0 |
| Unsupported | 0 |
| Unrun | 0 |
| RuntimeFailure | 6 |

阶段仍为 `BLOCKED`。RuntimeFailure 不能升级为 Covered。

## 固定输入

- 游戏 `0.111.0`，commit `41cef1ea`
- 快照 `local-mods/workshop-snapshot-20261106-163352`
- 快照内容 SHA-256 `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`
- manifest SHA-256 `09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`
- Kaguya Workshop `3747548629`，版本 `3.2.4`
- BaseLib `3.4.7`
- Kaguya DLL SHA-256 `f565b7232975212e59088bebc2e2ef169c78f0c5ef36e0f698807f9c9942abce`
- Kaguya PCK SHA-256 `f60ba6e6975245de6c8ed98aa0c69a92864fe608f15329b7c8f17d246ffdcb8a`

固定快照、Mod DLL、PCK、manifest、正式游戏输入全部保持只读。

## 主要证据入口

- [全量矩阵](D:\STS2-Simulator-Lab\.build\runs\m2-5-kaguya-20261112-exhaustive-final6\content-matrix.json)
- [生命周期矩阵](D:\STS2-Simulator-Lab\.build\runs\m2-5-kaguya-20261112-exhaustive-final6\lifecycle-matrix.json)
- [交互风险矩阵](D:\STS2-Simulator-Lab\.build\runs\m2-5-kaguya-20261112-exhaustive-final6\interaction-risk.json)
- [全量证据目录](D:\STS2-Simulator-Lab\.build\runs\m2-5-kaguya-20261112-exhaustive)
- [阶段报告](D:\STS2-Simulator-Lab\docs\mods\stage-reports\m2-5-kaguya-exhaustive-coverage-handoff-20261112.md)
- [Issue #11 最新评论](https://github.com/QzKING2023/STS2-Simulator-Lab/issues/11#issuecomment-6826263898)

## 六个待解决单元

| 单元 | 首个失败边界 | 失败分类 |
| --- | --- | --- |
| `Kaguya.Encounters.TsukitoBosatsuBossEncounter` | `TsukitoBosatsu.AfterAddedToRoom` 创建 `Godot.Timer` | headless Godot node capability |
| `Kaguya.Monsters.TsukitoBosatsu` | `AfterAddedToRoom` 的 `Timer` 和 Godot 运行时 | headless Godot node capability |
| `Kaguya.Events.EightThousandYearsMemory` | `Reject` 停止音乐时进入 `NDebugAudioManager.Stop` | event audio/runtime state |
| `Kaguya.Events.KaguyaEnding` | `Proceed` 调用正式 `RunManager.OnEnded` 和 game-over UI | ending state/UI capability |
| `Kaguya.Events.MoonNews` | `Reject` 停止音乐时进入 `NDebugAudioManager.Stop` | event audio/runtime state |
| `Kaguya.Events.YachiyoCupEvent` | `GiveUp` 停止音乐时进入 `NDebugAudioManager.Stop` | event audio/runtime state |

每个失败目录保留 `result.json`、`stderr.log`、`trace.jsonl`、`process.json` 和 canonical input。失败证据路径位于 `.build/runs/m2-5-kaguya-20261112-exhaustive` 下对应的单元目录。

## 已完成的适配工作

- 真实调用快照内 `KaguyaModInitializer`。
- 运行时登记 638 个 Kaguya 模型，声明分母为 639，另有 1 个事件奖励身份。
- 334 张 Card、73 个 Relic、159 个 Power、10 个 Potion、1 个 Enchantment 已经逐项运行。
- 14 个 Encounter、23 个 Enemy、16 个 Event、2 个 Character 已进入逐项矩阵。
- `Kaguya.Cards.Clash` 与原版 `CARD.CLASH` 的身份冲突已建立独立 collision-isolated probe，原始 `DuplicateModelException` 仍保留。
- headless audio、video、hover-tip、asset cache 等旁路只用于平台能力隔离，不能证明 visible-policy compatibility。

## 新会话必须遵守的工作边界

- 首次 GitHub 命令前执行 `. .\tools\maintenance\Set-GitHubSession.ps1`。
- 开工前再次读取 `AGENTS.md`、`GLOSSARY.md`、domain、issue tracker、triage labels、ADR、full-run plan 和本交接文档。
- 先用 `gh issue view 11 --json number,title,body,labels,state` 验证票据仍为 open、标签仍为 `ready-for-agent`、阻塞票仍关闭。
- 先读取分支、HEAD、git status、固定快照和 manifest。保护已有未提交修改。
- 每次运行使用新 `.build/runs` 子目录并写入 canonical input。
- 不修改 Kaguya Mod、BaseLib、游戏快照、DLL、PCK、资源和 manifest。
- 不用空实现、静默跳过、原版替代行为、预测死亡或假 DTO 伪造生命周期。
- 不把 Godot、Mod 类型或外部 DTO 引入核心模拟器。
- 不提交、推送、重写历史或创建远程 PR。

## 完成条件

新会话只有在以下条件同时满足时才能关闭 #11：

1. 六个 RuntimeFailure 均在真实回调和真实清理路径中重新运行。
2. 全量矩阵保持总数 640。
3. `RuntimeFailure=0`、`Partial=0`、`Unsupported=0`、`Unrun=0`。
4. 原有 primary、repeat、replay、failure cohort 重新验证稳定。
5. 阶段报告、矩阵、Issue 评论同步更新。

如果任一失败需要静态替换、跳过回调、注入结果或修改 Mod/快照，保留证据并将阶段状态设为 `BLOCKED`。

## 当前唯一建议入口

先建立 `TsukitoBosatsu` 的真实 Godot Timer 生命周期 seam，再处理 KaguyaEnding 的真实终局状态。随后重跑六个失败单元和全量矩阵。

提交或推送：**否**。
