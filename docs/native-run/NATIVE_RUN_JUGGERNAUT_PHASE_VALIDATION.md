# Juggernaut 第三形态收窄与 a595 在线尾部

本轮实现和有界验证已收口，待独立验收。**三次原生执行均为 Death610，Boss 剩 94HP；没有 BossVictory、RunVictory 或 M1 完成。** 本批最多三次执行的额度已经用完，未追加候选或扩大搜索。

冻结：`3DB954E49C1282F26D41555155C7E547922B91C85AA36481F78B35251A7F19FC`，清单 `.build/native-build/juggernaut-phase-final01/build-manifest.json`。证据汇总：`.build/runs/juggernaut-phase-work01/closeout.json`；原始三份输出为 `.build/runs/juggernaut-phase-exec-minimal/`、`…-detailed/`、`…-replay/`。

## 变更与边界

相对已独审的 `6D1CC702…574CE`，只修改两个生产源码：

- `NativeThirdPolicy.cs`：800 分提升额外要求实际 TestSubject `IsAlive` 且有 `POWER.NEMESIS_POWER`，将其限定到第三形态。此前自伤存活、迫近伤害、Intangible、复活与已有非负评分保持原顺序。没有新增原生补丁或改变牌效、资源。
- `NativePreparation.cs`：生成标签改为 `v41-third-form-juggernaut`，旧 v40 加入明确的读取兼容列表；完整描述其他字段仍严格比较。

本次没有另加“保留 Impervious”策略，也没有改变升级 True Grit 的子选择排序。旧 a595 前缀中的 Impervious 已耗尽，因此这不是检验保留该牌的场景。

构建为 0 警告 / 0 错误。源码/编译副本/宿主输入/产物分别核对 103/66/3/7 项，Core 产物不变；15 个锁定输入和 52 个兼容源码、上一批 60 个原始执行文件匹配原锁。没有改动角色、安装目录、真实用户存档或其他项目，没有 UI、真实 Steam/网络访问、提交或推送。

## 实际执行来源

每次均在独立隔离进程正常创建 Run，以 `LAB_V0111_NATIVE_DEV_03` 执行旧 `juggernaut-baseline` 的前 595 个实际动作。分支计划固定 PrefixCount=595、零强制动作、Mode=tail，并记录完整路线/普通选择/奖励选择哈希；切换时要求其选择均已消费。

前 595 个动作的全部派发/确认完整 payload 与源轨迹相等，包含完整 RNG。实际切换点为玩家 Play、turn5、67HP、Boss 第三形态299HP且有 Intangible。随后按原生合法候选在线执行。没有复制全局状态、改 HP 或补造资源。

minimal 与 detailed 都标为 `natural-run / prefix-branch`；最后一份是整条已执行路线的 `frozen-replay`。**这是自然开局合法前缀加在线尾部实验，不能称为 v41 从开局全程在线通关。** 状态、RNG与来源标签保持独立可核查。

| 执行 | 确认/派发 | 原生终态 | 原始退出 |
| --- | ---: | --- | ---: |
| minimal 前缀+在线尾部 | 610/610 | Death；玩家0HP，Boss94HP | 0 |
| 同构建 detailed 重复 | 610/610 | 同上 | 0 |
| 独立进程完整冻结重放 | 610/610 | 同上 | 0 |

三个结果均为 `Stop=Completed / Comparison=NotCompared / DataComplete=true`。退出0表示合法终态和数据收口，不表示获胜或仿真差分。minimal/detailed、detailed/replay 的完整派发/确认 payload 各1220条相同；后两份 route、choices、reward-choices、final-state 全部相等。

## 有用的新事实与不能作出的归因

源参考与新实验到 a596 的完整状态相同。**第一个差异发生在 a597 的子选择，而非根动作**：两边均打 True Grit；旧冻结答复 q8 选择 `o0 / i1218 Defend`，新在线分支选择 `o1 / i1220 Stampede`。请求上下文、候选、选择前 stamp 相同，实际耗尽实例不同。旧参考要到 a617 才退出冻结决策，而新实验在 a595 就切为在线，比较范围不同。

当时 Boss 有 Intangible，Juggernaut 的新增评分提升没有生效。因此不能把后续牌序、失血和 Death610 全部归因于这次评分收窄。`first-live-choice-divergence.json`、`reference-comparison.json` 和原始普通选择文件保存了这个差别。没有为了取得旧结果而在本轮改选。

新尾部 a607 实际打 Defend，获得5格挡并产生12点对 Boss 的 Unpowered 实际伤害；有原生 BlockGainedEntry / DamageReceivedEntry 及当前 Juggernaut12状态。这支持局部机制确实执行，不代表整个排序策略有效。历史没有通用 PowerSource 字段，不能把这一观察扩张成全面触发来源系统。

失败的主要可观察边界：turn6 无常规格挡手牌，EndTurn 前66HP/4Block，下一回合25HP；最终 a610 为 turn8 EndTurn，24HP/8Block，Boss94HP。三次原生敌方命中依次实际扣4、12、8HP，最后一击记录4 overkill并死亡。它不是仅缺94点伤害的静态问题；本轮没有证明任一单项改动能补足存活与输出。

## 验证、预算与剩余限制

15 个编译后评分 API 控制通过：11 个明确 API 边界夹具，以及4个取自真实历史派发状态的输入门验证，覆盖第一/第二形态、第三形态开放/Intangible窗口、正评分、迫近伤害、缺 Block/能力、攻击牌和复活门。API 输入控制不算新原生牌效执行。

生产检查器共10次调用：旧 v39 源、旧 v40详细轨迹、本轮三份结果共5份接受；原 R1三反例及未知策略/改变描述预算两份共5份拒绝。读取旧证据只在隔离副本上进行，失败按实际退出和错误日志判断，避免沿用上一次成功的派生报告。

50项来源、产物、前缀、合法实例、终态、状态/RNG、隔离与预算核对通过。MemorySaveStore、shouldSave/UI false、Steam未初始化、Telemetry0、host faults为空；没有把保存/replay拦截命中当作真实存档写入。

执行前登记上限为3次 / 10分钟 / 1GiB，每次120秒。登记 `00:05:40.047 UTC`，三份执行及重放核对于 `00:09:39.358 UTC` 完成，约3分59秒；每个原生进程均低于120秒，未超时。原始证据与构建、分析、临时副本成本一并核对，最终字节数及包含单份 reader scratch 的保守上界见 closeout；没有删除原始失败来节省额度。审查与文档收尾另有时间戳，不再启动游戏尝试。

本轮没有全量重跑无关原生矩阵，依据仅策略排序和描述版本变更运行受影响的原生尾部、重放、API及读取回归。最终 M1 同构建全套门保留；旧缺锚点证据仍未认证，预持折扣遗物原价采集等未覆盖项保持。

本批不再继续尝试。下一次判断应优先解决控制变量：若要评估这条评分的收益，需要固定相同前缀和 True Grit 子选择策略；若要追求可达胜利，应先分析实际耗尽 Stampede 后的手牌/防御链，不能把旧33HP参照当同策略目标。两者都属于后续有界实验，尚未执行。

第三幕 Boss 实际胜利后的 Architect/RunVictory 仍需接通同 Run 原生生命周期；当前驱动在第三幕 Boss奖励后返回。收窄规则通过和一次失败重放通过，都不能关闭完整M1。
