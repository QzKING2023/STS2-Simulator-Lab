# M2 验收报告与下一路线（2026-11-13）

## 验收结论

当前 M2 的固定分母验收状态为 **PARTIAL**。固定 12 条角色分母已经逐条汇总，所有已知的 `PASS`、`PARTIAL`、`BLOCKED`、`Unsupported`、`RuntimeFailure` 和 `Unrun` 状态均保留在 [M2-6 固定角色矩阵](m2-6-fixed-roster-matrix-20261113.md) 及其机器入口中；没有把静态发现、预测终态或平台能力缺口升级为 gameplay 通过。

机器入口：[fixed-roster-matrix.json](../../../.build/runs/m2-6-fixed-roster-20261113/fixed-roster-matrix.json)。矩阵验证输出为 `PASS M2-6 fixed roster matrix rows=12 aggregate=PARTIAL`，核心契约检查为 `PASS all 137 contract checks`。

## 固定输入与受影响范围

- M2 固定输入：游戏 `0.111.0 / 41cef1ea`，快照 `workshop-snapshot-20261106-163352`。
- 当前测试版已经迁移到 `0.113.0`；它与旧快照不是同一 canonical input。旧证据继续作为 append-only 历史证据保留，不能直接充当 0.113.0 兼容性证明。
- 固定角色分母仍为 ADR-0005 定义的 12 条 role-bearing entries；依赖、functional、resource-only 和 Eleanor baseline 不被重复计数。

## 固定分母结果

- `Remilia`、`LexNinja2`：固定输入下 foundation、flow、content、stability 通过；平台能力仍单独记录。
- `figure_Saya`、`Aemeath`、`Watcher`、`YukiMakoto`、`Kaguya`、`shadowversebydmod`：保留真实终态和/或内容证据，但仍有共享 foundation、内容分母或未支持能力，状态为 `PARTIAL`。
- `Downfall`、`Ryoshu`、`Acheron`、`St2bs`：保留真实初始化、API、ModelDb 或状态机阻塞证据，不能声称 flow/content 通过，状态为 `BLOCKED`。
- Shadowverse/Minion 本轮关闭的是固定 Minion/组件真实战斗、native `Death`、稳定四路证据和明确 capability 边界，不是完整 Shadowverse 内容包兼容。

## M2-7 范围决定

M2-7 的三个新 Workshop 样本不再属于当前版本的验收范围。由于测试版从 `0.111.0` 迁移到 `0.113.0`，Issue #17–#20 已标记 `wontfix` 关闭，理由是取消当前 M2 的样本准入与样本验收；这不是样本通过，也不会生成新的 Workshop 分母。后续如需继续 Workshop 试点，必须在 0.113.0 新快照上重新建立独立入口和 canonical input。

## 0.113.0 下一路线

1. 固定并哈希 0.113.0 游戏快照、Mod 快照和依赖闭包，单独生成新的 manifest；不改写 0.111.0 证据。
2. 先复核 BaseLib、RitsuLib、MinionLib 的 exact compatibility variant、加载顺序、ModelDb 注册、跨房间状态、cleanup 和失败隔离。
3. 重跑核心契约、native host 和库级 foundation fixtures，确认 collector、adapter 与游戏版本一致。
4. 按固定 12 条角色分母重新执行四路 primary/repeat/replay/failure，优先处理 Downfall 的 headless Godot Logger/OS seam、St2bs 的 initializer 与 `POWER.GUARDED_POWER` 冲突、Acheron 的 flow 状态机边界和 Ryoshu 的 `AttackCommand` API seam。
5. 在固定角色重新建立稳定分母后，再决定是否创建新的 Workshop sample issue；不得把 M2-7 的旧票据重新解释为 0.113.0 通过。

## 证据保留与清理

当前证据在验收报告发布前不删除。Issue #22 负责后续 retention policy、dry-run 清单、大小配额和旧 evidence 清理；清理必须保留 canonical inputs、失败/Partial/Blocked/Unsupported/Unrun 证据及本报告引用的 manifest，不得用删除来降低分母或掩盖失败。
