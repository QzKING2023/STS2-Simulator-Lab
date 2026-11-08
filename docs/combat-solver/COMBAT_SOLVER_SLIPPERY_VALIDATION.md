# SlipperyBridge 原生流程与借取属性根捕获

2026-11-04；本批待独立审查，M1 未完成。原410确认/411派发 Unsupported、上轮跨构建重放标识均保留。

## 复用与实际事件

固定 RandomForeseer 0.13.19，MIT / hotwords123。新独立输入快照 `randomforeseer-0.13.19-native04-slippery` 保留旧9项输入并加入原 `SlipperyBridgePrediction.cs`，10项输入均哈希绑定。Lab复用其候选算法、跳过集合和克隆RNG，预览数量固定为1，仅将UI hover tip投影换成牌实例/型号/升级与RNG数据；不初始化Mod设置、UI或遥测。

原生 `SlipperyBridge.OnRoomEnter` 并非继承空入口；它的雨效使用空条件访问。Lab检查原版方法/异步体无未知Harmony补丁和无事件UI，实际调用原入口并核对前后状态及事件RNG不变，没有替换业务。真实未知补丁注入拒绝已运行，仅属审计API控制、业务执行0。

预登记策略为一次存活的HoldOn，随后Overcome。run01实际：

- a411原生进入事件；初始候选为PommelStrike+1实例i902。
- a412原生HoldOn，HP105→102；上游预测与实际均选中FlameBarrier实例i246，完整事件RNG一致，原生跳过集合加入i902。
- a413原生Overcome，移除具体i246，其他永久牌组内容保持，事件完成。第二次HoldOn预览没有执行，明确NotComparedCounterfactual。
- a414真实进入下一战。该路径是同一正常开局run加既有110动作冻结前缀，不是房间夹具或拼接。

事件私有RNG另以不可变快照观察；既有State.eventRng代表多人事件选项同步器RNG，不能将二者混同。

## 新出现的真实战斗差分

run01冻结 `C033300D8EE9DBB4AA50B9CFAFDFC3E0A7182A916E40D2D6ACF04D30A52967AE` 实际422确认/422派发、DataError/exit1；327个差分动作中326一致，q1224不一致。不能将最后一个原生确认动作称为差分认证成功。

q1224为带Swift的Hellraiser。MummifiedHand的降费目标与完整RNG一致；延续状态唯一不同片段为玩家Dexterity：原生+1，分支−1。`q1224-diagnosis.json`保留具体差异。上游 `DeathPowerSupport` 已调用 `RefundPossessedStats`，已有forkable借取账本及fingerprint，但根物化没有装入此前原生已记录的借取数据。

独立Lab依赖 `combatsolver-0.50.1-possessed-root02`（derivative `possessed-root.1`）继承 `ending-powers.2`：仅两个上游源文件变更，将原生已有力量/敏捷借取数据在MaterializeRoot装入已有账本。继续复用原fork、指纹和返还，不改评分、特定卡或怪物判定，不初始化Mod。空原生lazy数据视作无历史借取；非法目标、正值、非整数和溢出明确拒绝。旧输入不覆盖。

生产构建 `64DB6340C5FC4511EC64B79B30E340719B92F66A8EB6400544EA7AA913A12DB8` 加入基于真实非空原生账本的分支API检查：捕获一致、返还一次、父分支/其他fork/原生源不变。这些API夹具不冒充原生死亡返还；实际修复仍以后续原生差分为准。

## 保留的失败与配置影响

- 首次依赖编译错误使用了ForkableDictionary不存在的TryAdd。失败目录 `combatsolver-0.50.1-possessed-root01` 保留；改用其已有ContainsKey与索引写入后新目录编译0警告/0错误。
- run02尝试以422个实际已执行动作作为前缀，在110确认/110派发处被旧Beam死亡候选规则拒绝（Unsupported/exit2）。PrefixCount同时控制求解器切换与回放消费，因此扩大它改变了中段的实际求解配置。此运行未到q1224，不能声称验证了修复。
- 最后名额run03恢复原110切换点和现有通用Coordinator配置。没有放宽死亡保护或为具体战斗调评分。
- 新检查器读取run01完整文件时拒绝旧敌方阶段能力指纹；保留该正确身份门。run01由其原构建检查器通过前缀一致性检查；这不将DataError转为成功。

## 验证范围

`.build/runs/solver-slippery-work01/` 包含计划、不可重置账本、22项事件生产正负控（2合法/20拒绝）、原构建与最终构建控制、来源审计、绑定检查及失败诊断。控制覆盖缺失全部/部分子事件、重复、错序/归属、实际选项/实例型号、RNG、损血、删牌和完整静止边界；既有通用房间契约继续检查真实任务和动作。

最终结果、原生预算和同构建重放完成情况以 `closeout.json` 为准；不得将未运行重放或最终全量验收写成通过。15输入、52兼容源码保持冻结。当前新源码与依赖仅Lab内，未改角色/安装/真实存档，无UI、真实Steam或提交推送。


## 原生整局胜利与断连恢复

最终构建 `64DB6340C5FC4511EC64B79B30E340719B92F66A8EB6400544EA7AA913A12DB8` 在 `.build/runs/solver-slippery-exec-03` 完成 RunVictory：499 确认/499 派发、22 场战斗、393 个当前战斗动作差分 exact，原始退出码 0。同一次正常开局 Run 使用110动作冻结前缀后在线通用 Coordinator；不是全程在线策略、夹具通关或胜率实验。

a494 原生击杀最后 Boss；a495 完成奖励；a499 Architect 原生调用 WinRun。回调前玩家 HP18，原生终局清理后 HP0；Won=true、Losses=0、WinStarts/Returns=1，不能将清理后 HP0 记作 Death。整体 Comparison=NotCompared 保留：战斗逐步差分和战斗外部分预测分开认证。

q1224 修复证明见 `q1224-repaired-proof.json`：旧/新原生动作及前后状态相同，旧差分失败而新差分 exact。属性账本 API 控制另外验证力量/敏捷捕获、只返还一次、fork 隔离，不冒充两个独立原生致死测试。

断连前3次执行均完成，失败输出保留。恢复后1146项源码/输入/产物哈希与67项受保护输入均匹配，1932个胜利原始文件另行封存；未发现遗留原生进程。原始最后3次执行在原30分钟窗内完成，账本关闭元数据因断连延后补写。后续同构建重放使用独立批次，不重置旧预算。

实际拦截 SaveProgress 73次、WriteReplay 22次；这些是拒绝持久化的计数。MemorySaveStore、shouldSave=false、Steam未初始化、无UI，faults为空。未修改角色、安装或用户存档，未提交推送。

事件生产控制最终构建22项（2合法/20拒绝），另2项顺序及实例控制通过；同一控制在两个构建执行不能算双倍覆盖。尚未覆盖多次HoldOn、穷尽候选池、致死HoldOn、完整事件策略优化或整角色全内容。

同构建重放与本轮独立核对结果见 [整局重放验收](COMBAT_SOLVER_RUNVICTORY_REPLAY_VALIDATION.md)。M1统一最终回归与覆盖清单收口仍需单独完成。
