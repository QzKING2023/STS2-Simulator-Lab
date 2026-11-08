# M1 第三批生命周期验收后交接

M1 尚未通过，不能提交或推送。持续授权有效，下一批先核对本批 closeout 与执行账本，再登记有界预算；不要把未用完的次数等同于解除时间和存储上限。历史失败与前两批 18/18 均须保留。

基线：`.build/native-build/m1-acceptance-b03-final07/build-manifest.json`，冻结 `1C0C5855F33AABBF144A61CFAFD3C1911AD981444FE6939A67DB1A74921F407E`。详情见 [本批验证](M1_ACCEPTANCE_LIFECYCLE_VALIDATION.md)。原始证据 `.build/runs/m1-acceptance-work03/`，其中 final06 原生结果与 final07 检查器重读明确区分。

## 最小依赖顺序

1. **先解决真实模型差分，禁止删字段过关。** EVAL04 final06 的 query327 / action383，IronWave+1 击杀 InfestedPrism 后，分支终态把手牌/抽牌/弃牌中的 TAINTED:4 清除，原生清理前终态仍保留。其他已比较字段和 RNG 相同。`eval04-mismatch.json` 保留原步。上游 `SimulatedCombatState.PowerLifecycle.NormalizePowerAfflictions` 依据有效存活 powers 归零推断 AfterRemoved；原版 `VitalSparkPower.AfterRemoved` 清除附着，但“持有者死亡”是否真正调用该 Hook 必须核实，不可把推断当成事实。应区分实际移除、死亡时机、战后清理；复用上游生命周期，必要时派生新的固定依赖，保留现有 B6A3…87CC，不覆盖旧 pin。覆盖非致死/致死/实际移除的最小原生与分支证据。
2. **药水牌堆选择复用上游 GetSpec。** EVAL06 final06 query282 被本地 GenerateToHand-only 入口拒绝，实际为 LiquidMemories / MoveToHandFreeThisTurn / Discard，原生尚未派发该药水。上游 PotionChoiceSupport.GetSpec、NativeChoiceSession.ResolvePlannedCards、PotionExecutionSupport.Prepare/Complete 已存在；复用其能力表、CardStateKey 与 source/option occurrence，保持真实槽位、原生子选择及完整状态差分。不要单独重写 LiquidMemories 效果，也不要把所有非生成型药水直接放行；生产 evidence reader 同步支持已声明的 pile choice。
3. **事件资源应观察真实命令与修正。** EVAL01 final06 a634 的 ThisOrThat Plain 显示 HP loss6，实际 HP48→43，已持有 TungstenRod；Gold325→372。当前无修正断言因此正确停为 Unsupported，但该断言应由通用原生命令/写入账本替代。可复用 NativeShopLedger/TreasureLedger 的 setter 与 Task 观察，分别记录输入伤害、原生修正、实际 HP/Gold、来源与前后完整状态。不能按特定遗物硬写减一；RF Ornate 单节点预测仍不能代表实际 Plain 效果或多步隔离。
4. **未建模敌人复活保持明确。** EVAL05 final06 query93 报 `EYE_WITH_TEETH.REVIVE_MOVE:Heal` 未镜像，虽预测 Victory 但未派发。先检查上游 Heal/复活通用语义和配置，不逐怪特判，不以候选预测当真实胜利。
5. **有限预算和死亡保护不隐式改变。** EVAL02 final07 在 ColorfulPhilosophers 完成后381动作触发 aggregate return admission，EVAL03 final07 完成 ColossalFlower 后440动作被 predicted-current-turn-death 保护停止。前者不是执行错误，后者不是实际死亡。固定样本原策略/预算不变；若需新的通用候选或终态政策，先记录独立方案与影响，不替换样本、不针对单战调评分。
6. **收口未运行边界。** CrystalSphere 仅展示入口适配，真实 CellClicked/完成任务仍由原生执行。查看本批 supplemental `crystal-prefix` 的实际结果，不以代码已编译宣称验证。若前缀不兼容，保留失败，不手动拼入事件。其独立前缀实验不替换零前缀的 EVAL06。

## 保持的门

- 六固定种子全部保留。final07 并未把六样本全部推进到真实终态；既有固定路线 RunVictory 回归不能代替它们。
- 原生退出分类与生产检查器结果分开。`--review-full-run` 对部分失败前缀退出0仅证明有限结构契约，不会把 DataError/Unsupported 变成完成。超预算查询可能被同一生产预算门再次拒绝；必须保留原始 exit3。
- 定向检查器改动重读原始证据；生成链/模型改动按依赖重跑。最终 M1 同构建完整门仍包括六样本、完整重放、死亡/故障/选择、原版回归、与隔离旧 Mod 构建分列的回归及数据查询验收。
- 检查器有限信任边界不变：原始受控执行与版本来源是基础，哈希只证明一致性；不试图独立解释每个游戏效果或防御无限协同伪造。
- 15 输入与52兼容源码固定；不改角色/安装/真实存档，不 UI/Steam/网络，不开始性能里程碑。继续整理已有日志，在线搜索与重放耗时不能直接相减。

只有 M1 整体验收通过后，才按用户授权检查发布范围并整体提交推送；本批未执行 git 写操作。
