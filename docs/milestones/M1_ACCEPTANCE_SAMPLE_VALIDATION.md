# M1 固定样本验收与重连收尾

2026-11-04。结论：**开发样本完整通关回归成立，M1 整体未验收通过**。不启动性能里程碑，不提交推送。

## 证据与构建

- 历史构建 `64DB6340C5FC4511EC64B79B30E340719B92F66A8EB6400544EA7AA913A12DB8`：正常开局同一 run 的 499 动作、22 战、393 步差分和整局重放成立；7589 次有状态观察一致。前 110 动作使用冻结策略前缀，之后在线 Coordinator；Oracle 模式，不能称纯 live 策略或独立胜率样本。
- 当前构建 `7E14CE7298B7932666B7EAC48CBF723729290DC8BA0770871C8FAB49CD17EDDA`：两个新进程冻结动作执行（detailed/minimal）均 499/499、原生 RunVictory、退出 0。两次均消费相同既有路线，不冒称当前构建重新在线选出了整局路线。
- 393 步完整身份/前后状态/RNG/预测与原状态不变检查一致，499 个确认边界完整状态一致。详细模式 7615、最小模式 4055 个状态观察；差额恰为最小模式不采集的 3560 条 combat-history，其余全部有序完整状态相同。原“所有观察完全相同”比较为 false，原结果保留，并非隐去差异。
- 当前生产 full-run 检查器对两条完整回放通过；5 个终态正负控通过。1150 项源码/输入/输出绑定匹配；15 输入和 52 兼容源码未变。Core 98 项通过，但不将 Core 计数当完整原生回归。

原始资料均在 `.build/runs/m1-acceptance-work01/`：`plan.json`、各 `evaluation-*-ledger.json`、`final-regression-ledger.json`、`final06-binding.json`、`final06-comparison.json`、`observer-mode-comparison.json`、`final06-terminal-controls.json`、`closeout.json`。历史原通关 1932 文件再次核对未变。

## 本轮修复

1. 明确允许 PrefixCount=0，从真正新 run 在线求解；空输入文件有哈希和严格计划验证，不借用开发种子状态。
2. 复用上游 PlanCardChoice/StateKey 支持 Upgrade/Hand、MoveToHand 和 MoveToDrawTop 的原生牌堆选择。非法来源、时机、对象、重复实例序号仍拒绝。
3. 原生空隐式返回没有决策，不要求消费不存在的求解器选择。仅 implicit、候选空、原生结果空、min=0 同时成立才略过 provider；重放匹配和未消费的额外选择检查保留。EVAL05 a24 已实际 request→resolved→returned→confirmed；a17 非空选择 7 负控及 2 正控通过。
4. full-run 复用已有 PotionCmd.TryToProcure 观察，Sozu 处理成功但未入槽记录为未取得。
5. 五个简单事件沿用原生入口/原生效果任务，仅在继承的空 OnRoomEnter、真实方法来源和无未知 Harmony 补丁检查后接通。不是重写事件效果或新预测算法。
6. 新增 full-run 专用终态契约，绑定 Architect 动作、活玩家、真实 WinRun/RunEnd、顺序、唯一性和完整状态链。合法 HP 清零发生在原生胜利清理，不能据此误判 Death。

## 预登记样本：不换种子或策略

六种子为 `LAB_V0111_NATIVE_EVAL_01` 至 `06`，配置在执行前固定。重复是复现而非增加样本数。当前最后结果跨构建，不能合称同构建最终矩阵：

| 样本 | 构建目录后缀 | 确认/派发 | 实际停点 |
|---|---|---:|---|
| 01 | final05 | 434/435 | TheLanternKey 入口 Unsupported |
| 02 | final05 | 74/74 | VineShambler 动态伤害预测近似，候选拒绝 |
| 03 | final05 | 25/26 | BrainLeech 入口 Unsupported |
| 04 | final05 | 46/46 | 同一 VineShambler 近似门 |
| 05 | final06 | 31/32 | 原空选择缺陷已越过，BrainLeech Unsupported |
| 06 | final06 | 122/123 | MorphicGrove 已越过，Wellspring Unsupported |

这些均未达真实终局。之前 InvalidAction、编译失败 final04 及所有 Unsupported 保留；不能把它们计为策略死亡或完成。02/04 的上游 unmirrored 数组为空，但 forecastExact=false、GRASPING_VINES_MOVE 动态伤害 approximations 非空；近似不能静默认证为精确。

## 验收边界与下一批

本批总计 18/18 次原生执行，最后结束于 15:06:40 UTC，40 分钟窗口内；收尾逻辑证据大小约 3.45 GiB（含硬链接重复计数），不包含所有历史目录。全部退出，无重复断连任务。构建 4 个已有警告、0 错误；无 UI、真实保存或 Steam 初始化。

下一批按已有 M1 授权另行登记有界预算，顺序：

1. 审查 BrainLeech、Wellspring、TheLanternKey 的真实入口及选择/异步依赖；RandomForeseer 锁定源已注册前两者，但尚未接入此样本，不把注册当验证。
2. 核对上游 VineShambler 动态伤害解析及 branch runtime 能力，从通用预览/运行时契约处理，不做逐怪评分，不移除精确性门。
3. 同一最终构建重跑原六种子及合法终态重放，保留失败分母；完成原计划的真实 Death、类别回归、生产负控和独立旧 Mod 回归配置及数据查询示例。
4. 当前 `--review-full-run` 的有限契约不等于旧 `--review` 所有场景检查器；旧 Major 检查器对 full-run 初始房间不兼容。需要明确组合与覆盖，不能把旧类别报告拼成最终统一验收。

M1 通过后才核对完整发布范围、许可与敏感内容，整体提交推送。现阶段没有执行提交或推送。
