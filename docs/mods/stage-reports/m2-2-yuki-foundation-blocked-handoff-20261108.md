# M2-2 YukiMakoto foundation/content 续作交接

## 当前结论

阶段状态：**BLOCKED**。新会话必须先处理共享 RitsuLib foundation，不能直接进入 Yuki 内容 probe。

- 票据：#4，保持 `OPEN`、`ready-for-agent`；阻塞票 #3 已关闭。
- 分支：`main`。
- HEAD：`7718d76399da8b515794fe5d68c2bfdaba59ebbd`。
- 固定游戏：`v0.111.0` / commit `41cef1ea`。
- 固定快照：`local-mods/workshop-snapshot-20261106-163352`，内容 SHA-256 `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`；manifest SHA-256 `09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`。
- Mod：YukiMakoto `v0.0.7-hot`，Workshop ID `3829924471`，精确 `lib/0.111.0`。
- 依赖：`STS2-RitsuLib compat/0.111.0 -> YukiMakoto lib/0.111.0`。
- seed：`LAB_M2_2_YUKI_01`。

## 已接受证据

不要重跑或重写以下四路：

- primary/repeat/replay：`Death / Completed`，state/RNG/trace/route digest 一致；
- failure：`Unknown / BudgetExceeded`，不能提升为 Death；
- 原始索引目录：`.build/runs/m2-2-yuki-20261108/`；
- 新审计目录：`.build/runs/m2-2-yuki-foundation-audit-20261108/`。

Yuki append-only inventory 保持 586 个身份：`Covered=0`、`Partial=8`、`Unrun=578`、`RuntimeFailure=0`；没有删除、折叠或升级任何单元。

## 共享 foundation 阻塞点

1. 注册：exact variant 和 Yuki registration identity 已观察，但共享重复注册、reload、注册失败和 teardown 语义未闭合。
2. callback/hook：turn/combat/reward/before-death 的顺序、异常中断和解绑未闭合；LexNinja2 仅为窄片。
3. 跨房间：奖励后的 secondary resource、Power/Relic 状态、注册身份和 callback ownership 仍 `Unrun`。
4. cleanup/teardown：共享 hook 移除、状态清理、callback detach、注册表卸载和 ALC teardown 未证明。

禁止用 Yuki 专用 adapter 绕过这些缺口。若 foundation 仍缺任一项，继续报告 `BLOCKED` 并保留原始证据。

## 现有文件

- 完整阶段报告：[m2-2-yuki-foundation-content-blocked-20261108.md](../../../docs/mods/stage-reports/m2-2-yuki-foundation-content-blocked-20261108.md)
- foundation 审计：[foundation-audit.json](../../../.build/runs/m2-2-yuki-foundation-audit-20261108/foundation-audit.json)
- 审计 canonical input：[canonical-input.json](../../../.build/runs/m2-2-yuki-foundation-audit-20261108/canonical-input.json)
- 原始 flow/content/lifecycle/interaction 索引：`.build/runs/m2-2-yuki-20261108/`

## 下一唯一入口

建立一个不含 Yuki 名称、Workshop ID 或单包分支的共享 RitsuLib foundation fixture，在独立 native worker 中真实闭合注册/重复注册与失败、callback/hook 顺序与异常中断、奖励后跨房间状态、hook/state/ALC cleanup 及 failure cleanup。foundation 闭合后，才回到 Yuki 选择最小内容族切片；不要开始 M2-3。
