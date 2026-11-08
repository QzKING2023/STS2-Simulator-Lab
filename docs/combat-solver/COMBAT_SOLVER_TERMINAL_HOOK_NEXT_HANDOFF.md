# 第5步交接：已修复常规Hook，下一实际入口是ThisOrThat

2026-11-04。M1持续授权及安全边界保持，无需逐项再请用户批准。本轮预算已关闭，后续实验应另行预登记。

## 可使用的基线

- [验证报告](COMBAT_SOLVER_TERMINAL_HOOK_REPAIRS.md)。最终 producer `B6B0952C3BED91D14CB200B8748870D4D3CC728F8D22278703B5BAEAF5FC932A`。
- 依赖是独立`combatsolver-0.50.1-terminal-hooks01`，派生标识`terminal-hooks.1`；不要悄悄换回旧pin或覆盖原快照。
- 最终exec03/04均126求解步差分一致；原生第二幕首战、奖励和Stone Lift完成。
- 当前未确认Travel a169的目标为`MapCoord (0, 3)`，原生已开始进入`EVENT.THIS_OR_THAT`，审计拒绝。保留168确认/169派发及部分最终状态，不把它假装成安全续跑根。

## 下一最小工作包

1. 只读审查固定程序集ThisOrThat的原始入口/选项/任务、选择与奖励依赖；优先使用既有原生事件桥。先查可获得的RandomForeseer版本与已实现预测；缺覆盖就明确native-only，不另写一套推测器或声称隔离多步精确。
2. 按实际代码决定可审计的选项范围。必须处理真实状态变化、卡/物品具体实例、随机流和完成任务；不能简单把事件模型加入白名单后任意选项全放行。未审计分支继续明确Unsupported。
3. 继续使用已认证正常起点和合法前缀进行重放，不从本轮部分失败状态直接拼接。新生成链变更只重跑受影响自然路径和同构建重放，保留原Unsupported；预设有限执行数、总时间、存储和120秒单进程门。
4. 后续按自然流程至第二幕Boss、第三幕及RunVictory；Coordinator评分与一般死亡保护保持，不回到单Boss手调。最终M1仍需同构建全量门。

## 非阻断但未覆盖的边界

本轮只针对回合开始常规Hook入口的通用结束语义，不是整个生命周期引擎重写。初始化中间点、复活/多玩家未新增认证；Stone Push/Vigorous及其选牌未支持；RF拾取/Stone预测未知。原固定输入与旧DataError证据应继续保留。

本轮成功前缀重放带一个明确未确认的实际请求，`failed-prefix-input/provenance.json`已解释null after/confirmation；审查时不能把该输入末项当成历史已确认动作。后续若修复ThisOrThat，不应把此失败票据直接当作已认证的新正常路径结果。
