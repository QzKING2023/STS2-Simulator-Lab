# Ryoshu M2-3 阻塞恢复启动提示词

你在 `D:\STS2-Simulator-Lab` 以独立 Agent 工作。目标是继续处理 M2-3 的唯一子票：Ryoshu（Workshop ID `3747528329`）。不要接入其他角色，不要运行 `/triage`，不要创建重复票据。

先读取并遵守：

- `AGENTS.md`
- `GLOSSARY.md`
- `docs/agents/domain.md`
- `docs/agents/issue-tracker.md`
- `docs/agents/triage-labels.md`
- `docs/adr/README.md` 及涉及 M2 的 ADR
- `docs/mods/MOD_FULL_RUN_EXECUTION_PLAN.md`
- `docs/mods/stage-reports/m2-3-ryoshu-blocker-handoff-20261110.md`
- `docs/mods/stage-reports/m2-3-ryoshu-full-run-report-20261110.md`

开始前：

1. 在首次使用 `gh` 或 GitHub-backed `git` 命令前执行：

   ```powershell
   . .\tools\maintenance\Set-GitHubSession.ps1
   ```

2. 用以下命令读取 #8，确认它仍为 OPEN、标签为 `ready-for-agent`、#4 已 CLOSED：

   ```powershell
   gh issue view 8 --json number,title,body,labels,state,comments
   gh issue view 4 --json number,title,body,labels,state
   ```

3. 读取 branch、HEAD、git status；保护所有已有未提交修改。不要 reset、clean、checkout、覆盖复制或删除用户文件。

4. 确认以下固定输入存在且只读：

   ```text
   local-mods/workshop-snapshot-20261106-163352
   local-mods/workshop-snapshot-20261106-163352.manifest.json
   .build/runs/m2-3-ryoshu-20261110/
   ```

固定 canonical input：

```text
Game: 0.111.0
Game commit: 41cef1ea
Snapshot: workshop-snapshot-20261106-163352
Snapshot SHA: 7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73
Manifest SHA: 09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3
Mod: Ryoshu 0.3.6 / 3747528329/Ryoshu.dll
Ryoshu DLL SHA: 0ff08a3187df4969a6d4ecc9a3c79e28bbb6ab51ba6feb7bc0cc6ffb2605de9a
Ryoshu PCK SHA: 91c699a8d79d0b3d17c5b01470165d12370ab2ac96144d0428afbb0770205210
Seed: LAB_M2_3_RYOSHU_01
Registration: REG.RYOSHU.assembly#1@0.111.0
Worker: SimulatorLab.NativeLifecycleProbe/Ryoshu-M2-3
Adapter: ryoshu-m2-3-probe/v1-no-runtime-patch
Collector: real Godot .NET process + SimulatorLab.NativeLifecycleProbe; native-trace/v1; sentry-disabled-overlay/v1
```

当前 blocker：

- 真实 primary/repeat/replay 在 `CARD.RYOSHU_STRIKE` 的原始 `OnPlay` 处得到：

  ```text
  MissingMethodException:
  MegaCrit.Sts2.Core.Commands.Builders.AttackCommand
    .FromCard(MegaCrit.Sts2.Core.Models.CardModel)
  ```

- 固定 `0.111.0` 的全部 31 份本地 `sts2.dll` 都是同一 SHA-256：

  ```text
  0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9
  ```

- 反射证据见：

  ```text
  .build/runs/m2-3-ryoshu-20261110/attack-command-api.json
  ```

- 当前游戏只有：

  ```text
  FromCard(CardModel, CardPlay)
  ```

  不存在单参数 `FromCard(CardModel)`。

- 固定快照没有第二个精确 `0.111.0` Ryoshu 变体。

必须遵守：

- 不修改、替换、重打包、重写或反编译 DLL/PCK/manifest/资源；
- 不修改 Ryoshu 原始代码；
- 不运行时 Harmony/MonoMod/JIT patch，不改写调用点；
- 不使用原版卡、直接伤害、假 DTO、预测死亡、结果注入或跳过回调；
- 不创建包含 Ryoshu 名称、Workshop ID 或单包分支的 adapter；
- 所有新运行必须使用新的 `.build/runs` 子目录，并记录 seed、快照摘要、游戏/Mod/依赖版本、worker、配置、adapter、collector 和 input digest；
- 如果没有合法兼容输入，必须保留 RuntimeFailure/Unrun 证据并报告 BLOCKED，不能为了关闭票据重分类。

调查目标：

1. 只用反射或合法元数据检查确认是否存在精确、已批准、跨 Mod 可复用的兼容输入或通用基础层 seam。
2. 如果存在合法 seam，先建立最小 generic fixture 和 regression，再用全新进程重跑 Ryoshu primary/repeat/replay/failure。
3. 只有在真实首战可完成后，才继续 reward、跨房间、terminal、cleanup 和 156 个内容单元的逐项生命周期矩阵。
4. 不要直接把 154 个 Unrun 单元改成 Unsupported 或 Covered。

完全通过的验收条件：

- 角色选择和初始状态是真实 native lifecycle；
- 首战、奖励、推进、清理和真实 Victory/Death 均观察到；
- primary/repeat/replay/failure 有完整 state/RNG/trace/route 证据；
- 156 个标准 content unit 全部有适用 lifecycle checkpoint 和 interaction contract 证据；
- foundation、flow、content、stability、platform capability 独立可审计；
- 没有 hidden stop、原版替代、注入结果或静默跳过；
- 最终在 #8 评论证据并仅在所有验收通过后关闭 #8。

完成后写新的阶段交接报告，包含 PASS/PARTIAL/BLOCKED、branch/HEAD/status、实际修改、固定输入、命令与退出码、primary/repeat/replay/failure 终态和 digest、foundation/flow/content gate、content lifecycle matrix、interaction risk table、平台能力缺口、状态分母和下一唯一入口。默认不提交、不推送。
