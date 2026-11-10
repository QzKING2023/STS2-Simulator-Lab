# M2-2 Yuki foundation 修复交接 — 2026-11-09 r53h

## 接手结论

当前阶段仍为 **BLOCKED**。下一 Agent 只能继续修复共享 foundation，不能进入 Yuki 内容 probe，不能接入第二角色，不能开始 M2-3。

最新阶段报告：

- [m2-2-yuki-foundation-repair-20261109-r53h](/D:/STS2-Simulator-Lab/docs/mods/stage-reports/m2-2-yuki-foundation-repair-20261109-r53h.md)

最新 aggregate evidence：

- [canonical-input.json](/D:/STS2-Simulator-Lab/.build/runs/m2-2-yuki-foundation-repair-20261109-r53h/canonical-input.json)
- [foundation-audit.json](/D:/STS2-Simulator-Lab/.build/runs/m2-2-yuki-foundation-repair-20261109-r53h/foundation-audit.json)
- [evidence-index.json](/D:/STS2-Simulator-Lab/.build/runs/m2-2-yuki-foundation-repair-20261109-r53h/evidence-index.json)

## 仓库与票据

- 工作目录：`D:\STS2-Simulator-Lab`
- 分支：`main`
- HEAD：`7718d76399da8b515794fe5d68c2bfdaba59ebbd`
- #4：`OPEN`，标签 `ready-for-agent`
- #3：`CLOSED`
- 工作区已有 dirty 修改必须继续保护；不要执行 reset、clean、checkout、覆盖复制或删除。
- 默认不提交、不推送、不关闭 #4。

## 固定输入

- 游戏：`v0.111.0` / `41cef1ea`
- snapshot：`workshop-snapshot-20261106-163352`
- content SHA-256：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`
- manifest SHA-256：`09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`
- RitsuLib runtime SHA-256：`3df9987d226b6d88b09727d19e99e51d3cf76c90d819d1cd04cf191d8b87fcf8`
- RitsuLib loader SHA-256：`6cc6b9df7b892ed18480ad528168a804af46e24e376f0a77cfa26b57900c1b11`
- Yuki `lib/0.111.0` SHA-256：`442113614bbaf43cc988cf5f917f82be3a41dd57ee0a7d19b1e6b5ccb360caa2`
- Yuki identity：`REG.YUKI.lib/0.111.0#1@0.111.0`
- canonical seed：`LAB_M2_2_YUKI_01`
- Yuki inventory：586 units；`Covered=0`、`Partial=8`、`Unrun=578`、`Unsupported=0`、`RuntimeFailure=0`

## 本阶段实际改动

- `NativeHost.ReleaseForTeardown()` 清除 host-owned `LastVictoriousRoom` 与 `Observe`。
- `NativeLifecycle.ReleaseForTeardown()` 清除 `ActTask` 与 lifecycle active/pending 状态。
- `Program.ReleaseForTeardown()` 调用两项 release。
- `NativeFoundationModLoop` 写入 pending-task/host-reference flags，并增加 worker/static 与 `RunManager`/`CombatManager` instance root diagnostics；Mod static 诊断只读取 collectible field types，避免 RitsuLib 未初始化的 Godot static constructor 触发 native crash。

不要把这些诊断当成 PASS：它们证明 host/registry/task 清理已发生，不能替代 runtime ALC teardown。

## 当前新证据

1. Acheron normal：`.build/runs/m2-2-yuki-foundation-repair-20261109-r53h-acheron/`
   - `REG.Acheron.lib/0.111.0#1@0.111.0`
   - Character 1 / Card 98 / Relic 14 / Power 35 / Other 4
   - 29 confirmed，28 callbacks，reward→next-room boundary
   - observer/registry/task/host flags 均 true
   - `runtimeAlcTeardown=false`、`alcAlive=true`、`assemblyAlive=true`

2. Remilia controlled callback failure：`.build/runs/m2-2-yuki-foundation-repair-20261109-r53h-remilia-callback/`
   - `REG.Remilia.lib/0.111.0#1@0.111.0`
   - Character 1 / Card 88 / Relic 8 / Power 24 / Other 3
   - 37 confirmed，33 callbacks，reward callback controlled `RuntimeFailure`
   - observer/registry/task/host flags 均 true
   - `runtimeAlcTeardown=false`、`alcAlive=true`、`assemblyAlive=true`

既有 Remilia normal `.build/runs/m2-2-yuki-foundation-repair-20261109-r52-remilia2/` raw exit `-1073741819` 保留不动。

## 当前 foundation 状态

| 语义 | 状态 |
|---|---|
| registration identity / duplicate / rollback / reload | PARTIAL |
| callback/hook order / exception / ownership / detach | PARTIAL |
| reward 后跨房间 secondary resource / Power / Relic continuity | PARTIAL |
| normal/failure cleanup / registry unload / runtime ALC teardown | BLOCKED |

唯一硬阻塞：必须找到安全、generic、跨族可复核的 runtime Mod teardown，使 normal 与 failure path 都能清除剩余 Mod/依赖/engine references，并让 collectible ALC 真正不可达。

## 验证

- r53h native build：0；3 个既有 warning，0 errors。
- Acheron run：0。
- Remilia controlled failure run：1，结果一致且 cleanup JSON 存在。
- Core checks：137/137 passed。
- `git diff --check`：0，只有已有 line-ending warnings。
- generic fixture Yuki/Workshop scan：无匹配。

## 下一唯一入口

继续诊断 runtime ALC root，优先检查 safe 的 Mod/依赖/engine static or native callback references；每次修复都必须在新的 run directory 用 Acheron normal 与至少一个独立族 failure/normal 复核。四项 foundation 全部 PASS 之前，禁止启动 Yuki probe，禁止接入第二角色，禁止 M2-3。
