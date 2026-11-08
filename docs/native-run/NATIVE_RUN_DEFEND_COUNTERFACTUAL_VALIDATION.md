# a597 Defend 耗尽单变量对照

本轮按已批准的 2 次执行 / 8 分钟 / 1GiB 上限完成。**在线尾部及完整重放均为真实 Death624，第三幕 TestSubject Boss 剩 33HP；没有 BossVictory、RunVictory 或 M1 完成。**

保持构建 `3DB954E49C1282F26D41555155C7E547922B91C85AA36481F78B35251A7F19FC`，没有修改生产实现或重新构建。证据汇总为 `.build/runs/defend-counterfactual-work01/closeout.json`，原始执行分别为 `.build/runs/defend-counterfactual-exec-detailed/` 与 `.build/runs/defend-counterfactual-exec-replay/`。

## 唯一干预与合法性

对照来源为已认证的 `.build/runs/juggernaut-phase-exec-detailed/`。用其原始 route、choices、reward-choices 的固定哈希，按原生动作重新执行前 596 步。分支计划只含一个强制 Move：a597 打出当前手牌 True Grit `i1219`，从真实 FromHand 请求选择耗尽 `i1218 / Defend`，代替对照路径的 `i1220 / Stampede`。之后仍使用同一 v41 策略，没有再强制其他选择。

前 596 步全部派发/确认 payload 与对照一致，包含完整状态及已采集的 run/player/event RNG；a597 派发也完全一致。同一 q8 请求、同一 stamp、同一候选中，原生实际返回 o0/Defend。确认后的 exhaust 包含 `i1218`，hand 保留 `i1220 / Stampede`；第一次差异出现在 a597 确认边界。分支 frontier 在该动作确认后出现，生产检查器验证唯一强制动作及子选择已消费。

本实验是正常开局合法前缀上的受控在线尾部，并非从开局全部在线的自然通关；没有复制全局状态、调整资源或拼接终态。强制选择通过已有原生合法候选和精确实例检查。

## 原生结果及重放

| 路径 | 确认/派发 | 实际终态 | Boss HP | 原始退出 |
| --- | ---: | --- | ---: | ---: |
| 已认证同构建对照：耗尽 Stampede | 610/610 | Death，turn8 | 94 | 0 |
| 本轮在线尾部：耗尽 Defend | 624/624 | Death，turn11 | 33 | 0 |
| 本轮完整冻结重放 | 624/624 | 同上 | 33 | 0 |

两次本轮执行全部 1248 个派发/确认完整 payload 相等，route、choices、reward-choices、final-state 也相等。结果均为 `Terminal=Death / Stop=Completed / Comparison=NotCompared / DataComplete=true`。退出 0 表示合法死亡及证据完整，不代表胜利或 native/simulator 差分通过。

a624 EndTurn 前玩家 3HP/3Block。原生 Burn 先造成 2 点全格挡伤害，随后 Boss 攻击记录 1 点格挡、3 点实际 HP 损失、10 overkill、WasTargetKilled=true。玩家实际 0HP，唯一死亡 stamp 属于 a624，原生 Losses=1、Won=false。

所有本轮 PlayCard 指定实例存在于当时手牌；MemorySaveStore、shouldSave/UI=false、Steam 未初始化、Telemetry=0、host faults 为空，无 Mod 或 CombatSolver 加载。没有实际游戏 UI、真实用户存档或其他项目操作。

## 可归因结论

这是同构建、同完整前缀、同 a597 派发状态下的单次子选择干预，后续策略保持一致，因此能够比较该次选择在固定后续策略下产生的实际路径差异，包括后续 RNG 消费、抽牌与决策的变化。

| 同一回合边界 | 原对照 | 本轮 |
| --- | --- | --- |
| turn6 EndTurn | 66HP/4Block，Boss182HP | 66HP/23Block，Boss191HP |
| turn7 起始后的可行动状态 | 25HP | 44HP |
| turn8 EndTurn | 24HP/8Block，Boss94HP，随后死亡 | 44HP/13Block，Boss99HP，随后存活 |
| 最终 | turn8 Death610 | turn11 Death624 |

本轮后续实际抽到并打出 Shrug It Off 和 True Grit，turn6 的格挡多 19，进入 turn7 的 HP 也多 19；完整动作和状态见 `comparison.json`。这是该固定样本的可观察结果，不能解释成保留 Stampede 必然更好、第三形态 800 分规则的独立收益，或胜率提升。旧参考也曾出现 Boss 剩 33HP，不可仅因最终数字相同就视为相同策略或相同路径。

## 检查、范围及后续

40 项事实核对通过，覆盖输入/生产构建、唯一干预、真实实例耗尽、前缀与重放完整状态/RNG、确认计数、实际死亡、原生退出、隔离和进程上限。两次原生结果均另由当前生产检查器读取隔离副本通过。

追加三个不启动游戏的生产检查器控制：JSON 属性重排等价副本通过；将计划耗尽对象改报为保留的 Stampede，被 `Branch forced Exhaust actual instance` 拒绝；删除真实强制选择记录，被 `Branch missing/duplicate child decision or result owner` 拒绝。坏副本在重新封印后实际 exit=1，不依赖失配封印或旧报告。派生原始控制副本均无损 gzip、核对解压哈希后保留，未删除或修改原始执行证据。

本轮登记时间为 2026-11-04 00:25:31.385 UTC，截止 00:33:31.385 UTC；两次执行和完整重放核对在 00:26:51.045 UTC 完成。原生进程耗时分别 28.990008 和 28.7068061 秒，无超时。数据量的最终值及包含单次临时控制副本的保守上界见 closeout；均低于 1GiB。该上限指新增证据文件，不是进程工作内存。达到 2/2 原生执行上限后未启动额外对局。

103/66/3/7 项生产源码、编译输入、宿主输入、产物均绑定原冻结；15 个锁定输入、52 个兼容源码、上一轮 60 个及前次实验 47 个原始文件不变。没有自动提交或推送。

本轮已排除“旧结果与新结果的 a597 子选择不同”这一比较混杂，留下可复现的较长生存尾部。按照上一轮审查的停止条件，此处收口，不再扩展评分常数微调；下一轮应先独立验收该单变量结果，再围绕较早的牌组/防御资源或明确的动作组合提出有预算方案。当前仍没有第三幕 Boss 胜利，同 Run Architect/RunVictory 连接与最终 M1 同构建全套验收均待完成。预持折扣遗物原价采集、旧缺锚点记录未认证等边界保持。

复现入口：`experiment.py` 为原登记的一次性执行脚本，已有登记和输出时拒绝重启，禁止覆盖证据。`verify.py` 可只读复核已有执行；`controls.py` 只生成独立标记的检查器控制副本，不执行对局。需要复跑原生实验时，应另建登记和新输出目录并计入新预算，不能直接重复本批。
