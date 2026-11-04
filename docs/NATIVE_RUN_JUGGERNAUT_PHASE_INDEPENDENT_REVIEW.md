# Juggernaut 第三形态门控与 a595 尾部实验独立审查

## 结论与范围

**通过本轮声明的实现及证据范围；没有发现必须修复的生产缺口。没有证明策略收益，M1 尚未完成。**

审查冻结：`3DB954E49C1282F26D41555155C7E547922B91C85AA36481F78B35251A7F19FC`。
构建清单：`.build/native-build/juggernaut-phase-final01/build-manifest.json`。
独立证据：`.build/runs/juggernaut-phase-independent-review01/closeout.json`；原实验：`.build/runs/juggernaut-phase-work01/closeout.json`。

本审查没有修改生产实现，没有启动新原生对局，也没有全量重跑历史原生矩阵。使用冻结程序集的生产 `--review` 入口读取隔离副本，执行编译后评分函数的边界控制，独立核对原始轨迹、构建和输入。派生坏副本重新封印后送入生产检查器，防止仅因旧封印失配而被拒；原始证据不改写。

## 实现及冻结核验

与上一构建相比，103 项生产源码清单仅两项改变：

- `tools/native-run/NativeThirdPolicy.cs:75`：新增提升条件要求实际 TestSubject 存活且带 `POWER.NEMESIS_POWER`。调用仍受第三幕路线/TestSubject 限定，已有自伤生存、正防御缺口、Intangible、复活及正分数规则保留。没有修改原生牌效或资源。
- `tools/native-run/NativePreparation.cs:14`：策略标签升为 v41，并把精确 v40 标签加入历史读取兼容列表；其他能力字段仍完整比较。

源码、冻结编译输入副本、宿主输入、产物分别为 103 / 66 / 3 / 7 项，哈希均匹配；清单规范化计算得到上述冻结 ID。15 个锁定 Mod 输入、52 个兼容源码、上一轮 60 个原始文件、本轮 47 个锁定原始文件均未变。哈希只证明文件一致性；执行真实性另由原生派发、选择、历史、结果及进程记录相互核验。

编译后 `NativeJuggernautRanking.Adjust` 的 10 项独立控制通过：早期形态不提升，第三形态可提升，Intangible/复活/无能力/无 Block/攻击牌/正防御缺口不触发，既有正分和零分保留。这是参数级 API 控制，不能称为新的原生 Creature 或牌效执行。实现者的 15 项控制同样应限定为 11 项参数夹具和 4 项历史形态投影。

`pureBlock` 参数实际表示有 Block 变量的非攻击牌，并不证明该牌没有其他效果。当前牌范围内未见安全门退化，不能把此命名扩大为通用精确牌效分类。

## 原生结果、前缀和重放

| 原始执行 | 来源 | 确认/派发 | 实际终态 | 原始退出 |
| --- | --- | ---: | --- | ---: |
| `juggernaut-phase-exec-minimal` | 正常开局，冻结前 595 步，随后在线 | 610/610 | Death，玩家 0HP、Boss 94HP | 0 |
| `juggernaut-phase-exec-detailed` | 同构建、同前缀及在线策略 | 610/610 | 同上 | 0 |
| `juggernaut-phase-exec-replay` | 新路径完整冻结重放 | 610/610 | 同上 | 0 |

三个执行都来自同一冻结，`Stop=Completed / DataComplete=true / Comparison=NotCompared`。退出 0 表示正确记录了合法死亡，不表示通关或 native/simulator 差分通过。

独立比较得到：

- 三份轨迹的前 595 步完整派发/确认 payload 与 `juggernaut-baseline` 相等，包含全部已采集 run/player/event RNG 字段。a595 后真实切换在 turn5、Player/Play，玩家 67HP，Boss 299HP、Nemesis + Intangible。
- minimal/detailed、detailed/replay 两对各 1220 个完整派发/确认 payload 相等；后者的 route、普通选择、奖励选择、final-state 也相等。不是仅比较省略 RNG 字段的旧投影。
- 每次出牌的指定实例存在于当时原生手牌；实际派发仍走原生合法候选与执行队列。全程无资源补写或全局状态复制。
- a610 是 turn8 EndTurn，执行前玩家 24HP/8Block、Boss 94HP。真实敌方三段伤害造成未格挡 4、12、8HP，最后一段记录 4 overkill 和死亡。死亡记录与唯一终态 stamp 属于 a610，原生 loss 计数为 1、Won=false。
- 隔离报告为 MemorySaveStore、shouldSave/UI=false、Steam 未初始化、Telemetry=0、无 host fault，无 MyMod/BaseLib/CombatSolver/RitsuLib 加载。没有把保存/replay 拦截计数解释为真实存档写入。

这是自然开局合法前缀上的分支实验，不能标成 v41 从开局全部在线的自然通关；也不能把三次重复作为三个独立胜率样本。

## 归因：首次分歧是子选择

旧参考与新路径到 a596 的完整边界相同，a597 派发的根动作、完整状态、RNG 仍相同。双方实际打出 True Grit `i1219`，在同一 q8 请求、同一 stamp 下，候选为：

| 候选 | 实例 | 牌 | 选择 |
| --- | --- | --- | --- |
| o0 | i1218 | Defend | 旧冻结参考 |
| o1 | i1220 | Stampede | 新在线分支 |
| o2 | i1221 | Defend | 未选 |

a597 确认边界才首次不同。原生耗尽的具体实例对应各自答复，非首项选择被真实消费；没有偷偷恢复旧选择。此时 Boss 有 Intangible，新增 800 分提升不会触发。旧参考保持冻结至 a617，新实验在 a595 后在线，两个尾部的决策来源不同。

因此 **不能把 Death610、Boss 94HP 与旧参考剩余 HP 的差异归因于第三形态门控，不能据此宣称此改动改善或恶化策略**。独立事实及两个请求保存在 `facts.json`，关键原生事件保存在 `key-events.json`。

a607 Defend 的原生 +5Block 和随后对 Boss 的 12 点 Unpowered 伤害成立。但评分调用对正防御缺口提前返回，运行证据没有记录该次具体采用了哪条评分分支；这一局部伤害事实不能证明新增 800 分规则促成了出牌。历史也没有通用 PowerSource 字段，不能由此宣称完整触发来源系统已完成。

当前最明显的生存差异观察点是 turn6：EndTurn 前 66HP/4Block，下一回合 25HP。这个 41HP 损失值得比较，但尚未证明由某张牌选择独立造成。a595 前 Impervious 已耗尽，从这里无法验证“早先保留 Impervious”。

## 有限对抗检查与预算

独立核对 49 项事实全部通过；生产检查器实际调用 13 次：5 份合法证据通过，8 份坏证据拒绝。

| 控制 | 实际生产拒绝依据 |
| --- | --- |
| 原 R1：强制耗尽另一实例 | `Branch forced Exhaust actual instance` |
| 原 R1：缺失强制选择 | `Branch missing/duplicate child decision or result owner` |
| 原 R1：错误选择上下文 | `Branch actual choice owner/order` |
| a595 切换快照 HP 矛盾 | `Branch static full confirmed state/owner` |
| a597 报告索引与实际实例矛盾 | `Branch actual request instance/index` |
| a597 错误上下文 | `Branch actual choice owner/order` |
| 删除 a597 在线选择记录 | `Branch missing/duplicate child decision or result owner` |
| 死亡终态 HP 改为存活 | `Major complete immutable confirmed state` |

原三条新执行、旧来源及 JSON 属性重排等价副本均通过。所有拒绝实际 exit=1，不依赖旧成功报告文件；接受 exit=0。控制验证的是有限可信采集契约，未声称可识别任意整包协同伪造。

原实验遵守预登记的 3 次 / 10 分钟 / 1GiB：执行数 3/3；执行与重放核对约 239.31 秒，收尾约 452.39 秒；原生进程总计 81.81 秒，各自小于 120 秒且无超时。独立重新计算包含当前收尾文件的持久数据 897,276,673 bytes，加最大单次 reader 临时副本 116,818,553 bytes 及 1MB 余量，保守上界 1,015,095,226 bytes，低于 1GiB。此为文件规模保守上界，不是实时采样峰值。审查自身新增原生执行为 0，不占用或扩张原实验的三个名额。

## 建议下一步：只改变 a597 耗尽对象

这是建议，**本审查未执行**。暂不继续调整评分常数或更换种子，保留 v41 作为范围明确的实验策略，不给其胜率收益背书。

建议复用当前冻结和已经认证的 detailed 路径作对照，采用现有 Branch API，不新增运行实现：

1. 锁定 `.build/runs/juggernaut-phase-exec-detailed/` 的 route/choices/reward-choices 哈希。PrefixCount=596，精确执行到 a597 前的相同状态/RNG。
2. 唯一强制 Move 为实际 True Grit `i1219`、model `CARD.TRUE_GRIT`、target=null；`Exhaust="i1218"`，即把本次耗尽改回同一请求中的 o0/Defend。之后保持 v41 根动作与默认子选择策略不变。
3. 最多 **2 次原生执行、8 分钟、1GiB、单次 120 秒**，任一门限到达即收尾。第一次直接 detailed 反事实尾部；仅在结果及完整性成立时，第二次完整冻结重放新路径。当前同构建对照与观测不扰动证据可复用，无需再启动 minimal 对照。
4. 必须核验共同前缀、a597 真实请求/指定实例、后续抽牌与 RNG、turn6 失血、turn8 到达状态及真实终态。非法动作、隔离、数据或重放错误立即停止依赖实验；Death/Unsupported/Budget 保留，不替换样本、不加预算。

这一对照可判断“固定其余策略时，该次耗尽选择对实际路径有什么影响”，包括后续 RNG 消费变化带来的影响；仍不能证明第三形态评分的独立收益或整局胜率。若该对照仍未带来 BossVictory，应停止此评分微调轴，基于已知防御缺口重新规划牌组、资源或更早路线的有界实验。

第三幕 Boss 真实胜利仍未获得；`NativeThirdAct.cs:75` 当前在 Boss 奖励后返回，后续还须接入同 Run 的 Architect/RunVictory 流程及最终 M1 同构建全套验收。预持折扣遗物原价采集、旧缺锚点证据未认证等既有限制保持。本轮无执行或权限阻塞，无提交推送。
