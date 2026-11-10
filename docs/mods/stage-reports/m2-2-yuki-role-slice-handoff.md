# M2-2 YukiMakoto 新会话交接

本文件用于在新会话继续 M2-2。当前阶段没有完成双门验收，下一 Agent 必须把这里的状态当作事实输入，不能把已有 flow 证据升级为 content PASS。

## 当前结论

- 阶段状态：`PARTIAL`。
- 子票：GitHub #4，当前已评论证据但仍 `OPEN`、`ready-for-agent`；不要在 content gate 和 foundation gate 完成前关闭。
- 分支：`main`；HEAD `7718d76399da8b515794fe5d68c2bfdaba59ebbd`。
- 固定输入：游戏 `0.111.0` / commit `41cef1ea`；快照 `workshop-snapshot-20261106-163352`；内容 SHA-256 `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`。
- 角色：YukiMakoto `v0.0.7-hot`，Workshop ID `3829924471`，精确 `lib/0.111.0`；依赖是 RitsuLib `compat/0.111.0`。
- 不要改选 Aemeath。已有静态审计和原生 loader 日志证明 Yuki 的精确依赖闭包可解析并初始化成功。

## 已通过的事实

- 真实角色身份：`CHARACTER.YUKI_MAKOTO_CHARACTER_YUKI_MAKOTO`。
- 起始状态：4×Strike、5×Defend、Dark Hour；Wild Card、Velvet Room、Meaning of Seeking；HP 70。
- 真实路径：Neow → `MapCoord (3, 0)` Monster → 首战胜利 → GoldReward/CardReward → 第二战合法结束回合 → HP 0 → native `Death` → cleanup。
- Core 检查：`PASS all 137 contract checks`。
- `primary`、`repeat`、`replay` 使用相同 canonical input digest：
  `73ecc49093727f9036a390326fcae06742e2c29a9ed638d1fdaf147da49c1f8a`。
- 三条成功路径共同 digest：
  - state `79ca17c18aadeac4b979bca04eceb87f4711a5f6a7500941ee8f63ba89e3d8e2`
  - RNG `620a655bd2e4574686aa860b3b01af30ebc16020f4e9b556db479b1d3e1a4f09`
  - trace `dd8513bec6fa299b3f474a04c9b8a63bcebeb7762755b5a957443e0c3fe26f5b`
  - route `b6a490a8c33fbd471f9457bc284c63dd0b7a4d4c83c20ad37e1fa4b1184070e7`
- `failure` 为 `Unknown / BudgetExceeded`，没有发出 Death；其 trace digest 是 `b079bbeaf580370e99d734f950aabd476277cdb3e7430f116d921560e0f7c9b8`。

## 当前阻塞面

- Runtime inventory 共 586 个身份：Card 270、Character 1、Power 281、Relic 4、Other 30。
- Content unit 分母：`Covered=0`、`Partial=8`、`Unrun=578`、最终 accepted cohort 的 `RuntimeFailure=0`。
- 8 个 Partial 是角色、3 个起始卡 identity、3 个起始遗物 identity、1 个奖励卡 identity；它们参与了本片，但没有完成所有适用 lifecycle checkpoint。
- 578 个身份尚未有真实 lifecycle evidence，尤其是 Power 281、Other 30、未触发卡牌/遗物及复杂生成语义。
- Foundation gate 仍是 M2-0 的 `PARTIAL`：共享 RitsuLib 的跨房间、完整 callback、cleanup 和跨 Mod 语义没有闭合。
- visible-policy 不在本阶段被宣称；采集器使用 TestMode/internal state，只证明 execution compatibility。
- 平台缺口：正式 UI、正式 save、Steam/cloud、network/replay network；post-reward 新 map point 的跨房间证据仍未闭合。

## 证据入口

- 阶段报告：`docs/mods/stage-reports/m2-2-yuki-role-slice-report.md`
- 证据索引：`.build/runs/m2-2-yuki-20261108/contract-summary.json`
- 四路摘要：`.build/runs/m2-2-yuki-20261108/runs.json`
- 内容矩阵：`.build/runs/m2-2-yuki-20261108/content-matrix.json`
- 生命周期矩阵：`.build/runs/m2-2-yuki-20261108/lifecycle-matrix.json`
- 交互风险表：`.build/runs/m2-2-yuki-20261108/interaction-risk.json`
- 代表性 trace：`.build/runs/m2-2-yuki-20261108-primary-10/game-output/trace.jsonl`
- 代表性 manifest：`.build/runs/m2-2-yuki-20261108-primary-10/run-manifest.json`
- repeat：`.build/runs/m2-2-yuki-20261108-repeat-02/`
- replay：`.build/runs/m2-2-yuki-20261108-replay-02/`
- failure：`.build/runs/m2-2-yuki-20261108-failure-02/`

早期探索失败目录也保留在 `.build/runs/`，包括 card selector 未安装、headless screen-shake/FTUE、未观察 Death 和 map transition 中止；不要删除或覆盖，它们是 RuntimeFailure/环境证据。

## 新会话的工作边界

1. 先重新读取仓库规约、M2 ADR、M2-1 报告、本报告和上面的 M2-2 阶段报告。
2. 重新核对 #4 的 state/label/blocker、分支、HEAD、git status、固定快照和 manifest；保护当前 dirty 工作区。
3. 不要重跑已经接受的四路作为“新证据”；所有新运行必须使用新的 `.build/runs` 子目录和完整 canonical input。
4. 只继续 Yuki 的 foundation/content 闭环；不要接入第二个角色，不要改 Mod、不改快照、不改核心外部类型边界。
5. 先判断共享 foundation 缺口是否阻塞下一步。若阻塞，保留证据并报告 `BLOCKED`；不要用 Yuki 专用 shim 绕过。
6. 若 foundation 足够，按内容族逐步建立真实注册、构造、激活/效果、持久化/退出、cleanup 和 interaction evidence；不可达内容才使用真实回调 probe。
7. 未完成的身份必须保留 `Partial`、`Unsupported`、`Unrun` 或 `RuntimeFailure`，不能为了关闭票据把它们改成 `Covered`。
8. 只有 flow gate、content gate、foundation gate 和稳定性都满足，才可在 #4 评论证据并关闭；否则更新报告并保持票据 OPEN。

下一唯一建议入口：先闭合 **RitsuLib foundation 中 Yuki 依赖的注册、callback、跨房间和 cleanup 语义**，再按风险顺序补齐 Yuki 的 Card/Power/Relic content unit。不要开始 M2-3。

