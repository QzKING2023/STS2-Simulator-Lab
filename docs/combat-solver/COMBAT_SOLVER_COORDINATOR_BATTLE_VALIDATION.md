# Coordinator 原生 Boss 胜利、奖励与跨幕验证

2026-11-04。第 5 步的新进展：复用固定上游 CombatSearchCoordinator，在原 110 动作认证前缀后完成 43 个在线战斗动作、13 次 EndTurn，真实击败第一幕 Vantom，领取原生奖励并实际跨入第二幕 MapRoom。第 155 个根动作确认后，因现有入口只接受 Tezcatara、实际生成 Darv，明确以 Unsupported 停止。相同产物完成一次完整重放，155 个根动作、119 个求解动作、状态/RNG/continuation 相同。不是完整 RunVictory，第 5 步和 M1 尚未完成，独立审查待进行。

## 输入、范围和预算

固定 v0.111.0/41cef1ea、Ironclad/A0、种子 LAB_V0111_NATIVE_DEV_01。实际从正常开局运行原生命令；前 110 根动作输入来自 `.build/runs/solver-shop-exec-01/` 并核对三份输入哈希，之后切换 Coordinator。live 标注 `prefix-replay/live-tail`，重放标注 `frozen-prefix-and-tail-replay`。没有使用资源、HP、卡牌或房间初始化夹具，也没有恢复伪造内存状态。这是固定自然来源前缀后的在线整战实验，不表示全程重新在线决策。

本轮生产执行改动只有把可配置尾部上限的合法范围从 8 扩为 64；本批实际预登记切换后最多 60 个根动作，现有每战 64 次查询上限保持。搜索配置、评分、原生派发、补丁审计、选择和死亡保护保持。未加入 Boss 特例。

批次最多 3 次原生执行、20 分钟、2 GiB，单进程 120 秒；计划、输入和账本在 `.build/runs/solver-coordinator-battle-work01/`。实际执行 2 次，累计 62.7069894 秒，均低于 120 秒，无 watchdog 触发；第三次额度未使用，账本关闭。最终保守存储计量见 closeout，包含构建、派生控制及运行数据，小于 2 GiB。4096 仍为 Coordinator 返回结果的采用上限，不是精确节点熔断。

## 原生结果与数据

| 边界 | 真实结果 |
| --- | --- |
| a110 | 认证前缀进入 Vantom，HP80、金币141，永久牌组18张 |
| a111–a153 | 43 次在线动作，每步重新求解、独立影子执行、原生派发和差分；13 次 EndTurn 包含真实敌方行动 |
| a153 / q491 | 第14回合 Strike 原生击杀，玩家清理前HP24，真实 Victory 回调与预测终态一致；清理后HP30，战斗历史清空 |
| a154 | 原生 RewardsCmd.OfferForRoomEnd：领取100金币、SpeedPotion及 Hellraiser；实际新卡实例 i329 进入永久牌组，18→19张，金币141→241 |
| a155 | 原生 ActReady→EnterNextAct 任务完成；act=1、MapRoom、node=null，实际新地图与先古 Darv |
| 停止 | 原始退出码2 / Unsupported / Ongoing / DataComplete=false；155/155 已确认，没有伪报完整流程成功 |

live/replay 为同一 producer：`BF68707C690173B50B1DDA833694877C73508B31B0EFB5B2B8EA0F1259B1C109`，目录 `.build/native-build/solver-coordinator-battle-final01/`。原始执行在 `solver-coordinator-battle-exec-01/02/`。119 份完整 solver step 一致，所有 dispatch/confirmed 动作、归属、前后完整状态和 RNG 一致；普通和奖励选择、最终完整状态、查询候选及工作量一致。前8个在线步骤还与上一轮认证结果相同。

43 个新查询中，38 个 NodeLimit、5 个 None，最大累计1843节点；上游成员工作量与累计计数一致。所有新查询均未预测玩家死亡，未来死亡派发仍未实证；本批未额外覆盖真实 TimeLimit、取消、当回合致死停止或复活。不把有限搜索、候选预测或当前动作影子结果当作原生胜利，原生胜利以实际终态回调和生命周期为据。

## 有依据的检查器扩展

producer 完成后，只扩展证据读取契约，未再改变执行产物或重跑原生流程。reader02 正常副本首次拒绝 `Major reward generation complete parent frame`，原始日志保留。进一步核对固定本机程序集发现：WingCharm.TryModifyCardRewardOptionsLate 会用 RunState.Rng.Niche.NextItem 从可附魔奖励中挑一张牌，并生成 Swift 修改后的候选；本次原生奖励恰好推进 Niche 一次。旧检查器只允许玩家 Rewards RNG 变化，遗漏了已持有 WingCharm 的真实奖励钩子。

最终 reader03 对该变化限定为：必须在原生 RewardOffer 生成阶段、此前确有 WingCharm、Niche counter恰好+1且四个内部字独立满足一次 xoshiro 状态推进；完整父状态的其余字段照常比较。Niche 多次推进或内部字矛盾都会拒绝。这只是该固定钩子的生成状态契约，不代表已独立核验所有候选附魔分布，也不是 RandomForeseer 的预测接入。

新增“Boss已完成、下一先古未接入”的失败前缀认证，复用全部整战回调、清理、奖励会话及跨幕校验；额外要求真实胜利/奖励/act任务完成、静止完整确认状态等于 MapRoom 返回状态、节点为空且实际先古非 Tezcatara。不跳过任何下一先古操作来假称完成，也不修改原始 Unsupported。

最终检查器冻结：`966A1555623A4F073A83E935A2C97F81A5A969CE60AC9DA8ADAEAE5966D9EBDF`（`solver-coordinator-battle-reader03`）。相对 producer 的6个编译源差异均为证据读取：NativeEvidence、NativeMajorContract、NativeRewardContract、NativeSolverCoordinatorEvidence、NativeSolverJourneyContract、NativeTraceContract。producer 同构建重放成立；当前源与最新 reader 绑定，不能把 reader ID 写成原始 producer ID。M1 最终同构建全套验收仍须另行完成。

## 验证与限制

`facts.json` 731项通过，包含两次原生完整前缀、Boss终态、奖励实例/金额、跨幕、全状态/RNG、父fork/票据、119步重放与旧证据固定。17个生产候选规则控制通过。最终生产检查器读取两份正常副本通过；另7次定向调用全部符合预期：重封等价副本通过、Niche多推进/错误内部字/最终新增字段/错误采用查询归属/致死影子均拒绝、旧8步预算停止仍通过。控制仅是派生副本，不冒充实际业务执行。

3个构建ID重新计算匹配；编译源、产物、输入哈希匹配。15锁定输入、52兼容源码、826个上游Solver源码和6个程序集保持；824份上一批原始证据哈希匹配。MemorySaveStore、离线单人服务、保存/replay拦截和补丁审计保持，无遥测及异步故障。构建4个既有warning/0error，git diff --check通过。未全量重跑无关原生矩阵，未提交推送。

## 复现与下一依赖

本批账本已关闭，不追加执行。审查时在原始执行的副本上调用 `dotnet <reader03>/bin/Release/net9.0/OfflineSearchHarness.dll --review-solver-coordinator-prefix <副本>`；`facts.py`与`controls.py`及全部计划/退出/锁文件位于 work01，control脚本要求新输出目录，不覆盖历史副本。反编译参考仅保留在忽略的本地证据目录，不进入公开源码。

下一阶段是正常进入实际 Darv 并处理其原生选择，再接下一战和后续幕控制器。具体停点在 NativeNaturalBoss.NaturalBoss 的 Tezcatara 门，发生于已完成 ActReady 之后、NextActStart 之前。已有 NextActStart 状态框架和 EnterAuditedEvent 的 AncientEventModel 入口可复用，但必须核对 Darv.OnRoomEnter 的真实继承与效果、类型化选项/子选择及完整状态契约，再扩展受控支持；不能直接删除先古门或默认选未知选项。Hellraiser 已真实取得，但尚未在下一战被 Solver 搜索或实际打出。自然第二幕、Boss及完整 RunVictory仍待推进；无需为此回到单场评分微调。
