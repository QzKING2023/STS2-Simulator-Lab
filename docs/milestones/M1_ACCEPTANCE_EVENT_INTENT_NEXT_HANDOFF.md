# M1 第二批验收后的交接

状态：M1 未完成，现有授权持续有效，无外部权限/连接阻塞。第二批 18 次原生预算已用完，全部退出；后续须重新登记有界批次，不能继续往已关闭预算中塞执行。

基线与结果见 [本批验证](M1_ACCEPTANCE_EVENT_INTENT_VALIDATION.md)。保留所有前代 InvalidAction、DataError、BudgetExceeded、Unsupported。旧目录不修改，不替换种子；没有提交推送。

## 实施顺序

1. **先补当前新增生命周期可信数据。** `NativePersistentAncient.cs` 的实际 options、before/after、offered instance 已采集，复用 `NativeAncientChoice.CheckPersistentPickup`；生产 reader 尚无该分支的独立契约。绑定当前选择/原生回调/实际取得对象/完整静止状态，沿用有限信任边界，配缺失、错归属、旧实例和合法变更控制。`full-event-child` 同样绑定实际父 EventRoom 和原生子 CombatRoom；不能将这两个增量当成已经独立验收。不要无界解释任意协同伪造。
2. **复用上游阶段选择。** EVAL05 final04 的查询 44 已产生合法 `STRATAGEM_POWER` / `PlayerTurnStart` / `MoveToHand` / Draw 的 BLUDGEON token。原生来源是 StratagemPower.AfterShuffle。当前 `NativeSolverPhaseProtocol` / `NativeSolverPhaseRuntime` 仅接 KnowledgeDemon EnemyTurn。扩展必须验证实际原生 hook/阶段/来源和完整 StateKey，再消费上游计划；不能改成 Action 时序、默认第一张或吞未消费选择。
3. **特殊先古拾取采用原生事务。** EVAL02 Pael 提供 Horn/Tooth/Eye；EVAL03 Orobas 提供 SeaGlass/SandCastle/TouchOfOrobas。先读真实锁定实现及已有 RelicCmd/选择/拾取观测，复用可用能力。`HasUponPickupEffect=false` 仅展示提示，不能作为无副作用证明。TouchOfOrobas 已实证 BurningBlood 被移除、BlackBlood 和实际 offered 遗物取得；不要手写替换结果。为实际命令记录增删、资源、选择、顺序和返回边界；未知预测保持未知，勿放宽精确战斗差分。
4. **宝箱金币账本。** EVAL04 final06 350/351 在实际宝箱处理中触发旧 `goldBefore + statedGold` 假设。先核查原生 ModifyGoldGained / 获取遗物等钩子，复用已有商店分离支付与业务副作用的观测方式；记录原始奖励与实际增量，不根据最终金币倒造输入或逐遗物特判。
5. **两个原生事件入口。** EVAL01 WarHistorianRepy、EVAL06 CrystalSphere。先调查原版与 RandomForeseer 已有来源/入口，原版效果和选择由真实任务执行。入口审计不等于所有选择、显示依赖、异步结果都已支持。保留正常队列固定决策；不存在的预测不得推断。

修复共享边界后按依赖跑定向控制和受影响样本；冻结后六个固定样本及重放均须同构建达到真实终态。还需原计划 Death、各节点/失败分类、独立 Mod 冻结配置及公共数据查询验收。当前两份完整路线回归能证明已知路径未退化，不能替代这些门。

## 可复用证据与注意事项

- `.build/runs/m1-acceptance-work02/plan.json`、`closeout.json`：18 原生 / 19 包装器调用（1 次启动前参数拒绝），45 分钟 / 4 GiB，全部收尾。
- `targeted-runtime-evidence.json`：VineShambler 67 个执行步及 NeowsFury 3 个显式空选择；未减差分字段。
- `final-comparison.json`：21 项通过；`terminal-unit-results.json`：同一生产终态方法 1 正 / 4 负；旧依赖正常副本拒绝记录留在 production-review-summary，不能冒称终态控制通过。
- `telemetry-summary.json`：现有运行观测，包含 overlapping timing 明示；不增加性能测试，不用在线/重放墙钟相减。
- `changed-source-scope.json`：相对前批的 13 个项目源文件；包含新依赖构建工具。构建工具顶部 docstring 和两个错误消息仍沿用旧衍生名称，实际 ID、scope、锁与变更源正确；下次修改时更新文字并记录新工具哈希，勿悄悄覆盖已锁定工具来源。
- 原先 15 输入 / 52 兼容源码仍匹配；禁止 Mod 专项、真实保存/UI/Steam/网络、其他项目写入。M1 未整体通过前不提交推送。

发布前还需从整体 dirty diff 区分项目实现、必要工具/文档与 `.build` 原始证据/游戏内容/研究/Agent 文件；当前尚未启动该发布步骤。
