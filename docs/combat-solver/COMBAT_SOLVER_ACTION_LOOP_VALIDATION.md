# CombatSolver 三项修复与普通动作在线闭环

2026-11-04。本轮完成三项定向修复，并将普通攻击和 EndTurn 接入“求解一步、原生执行、核对、再求解”。没有调整 Boss 评分。

## 实际结果

同一正常开局、固定种子 `LAB_V0111_NATIVE_DEV_01` 的首战中，求解器在每个新边界重新计算，实际依次执行：

| 步骤 | 原生动作 | 实际状态变化 |
| --- | --- | --- |
| 1 | Defend，实例 i15 | 能量 3→2，格挡 0→5 |
| 2 | Strike，目标 i26 | 能量 2→1，敌人 HP 43→37 |
| 3 | Strike，目标 i26 | 能量 1→0，敌人 HP 37→31 |
| 4 | EndTurn | 原生敌方行动和下次抽牌；玩家 HP 80→73，进入回合 2 的 Play，能量 3、手牌 5 |

四步的完整预测/原生 continuation 均相等，父 fork 保持不变。每步实际执行后原票据不可重复使用，另一张未消费旧票据因原生边界改变被拒绝，零额外派发。随后从第二回合真实状态做第 5 次求解；没有执行该查询给出的下一动作。

五次查询展开节点分别为 96、85、77、61、57。独立进程的冻结重放得到相同动作、选择、各步完整状态/RNG、五个查询结果和终点。开局至终点共 7 个已确认根动作，其中 4 个为求解器战斗动作。

两条路径都是原生正常开局片段。结果是 `Ongoing / SliceBoundary`，总体 `Comparison=NotCompared`；逐步文件提供四个战斗动作的精确差分认证。**没有完成整场、BossVictory、RunVictory 或 M1。**

## 三项修复与生产契约

1. **未消费旧票据。** 在消费前复制另一张票据，明确确认其 `Used=false`。真实原生动作后验证该旧票据拒绝、仍未消费、序号已改变和零额外派发；重复消费单列。普通修复场景实际为 sequence 3→4，不再依赖已消费 record 的事后复制品。
2. **Oracle 信息模式。** 求解器编译配置的 trace header、运行 profile 与所有捕获记录统一声明 Oracle。生产检查器要求 profile 文件、固定版本/隔离配置身份和一致信息模式，缺失或伪报 PlayerVisible 均拒绝。
3. **捕获与摘要。** 单步场景明确 query1、shadow1、query2 三个捕获角色，要求唯一、有序，绑定各自上一确认动作的 root/context、完整静止状态和对应 continuation hash。完成摘要的 query/执行计数和单步范围与实际记录一致。循环场景复用同样原则：每步 query/shadow 两个捕获，最后重新求解一个捕获，共 `2×动作数+1`。

旧 v1 记录和上一轮误接受证据原样保留；它们不被自动标为新 v2 契约通过。当前契约信任锁定原生执行器及采集器，不承诺从任意协同改写的全部日志重新证明游戏发生过。

## 动作桥与复用范围

沿用锁定的 CombatSolver 0.50.1 Lab 衍生 DLL、现有 `CombatRootSnapshot`、`ForkSimulator`、`CombatBeamSolver`、`ContinuationStamp` 和原生 run 宿主。

目标攻击同时检查 CardId occurrence、完整 StateKey occurrence、升级和原生实例引用；目标 CombatId 与敌方 index 同时给出时必须指向同一对象，并检查存在、存活和原生 `CanPlayTargeting`。当前真实样本覆盖一个敌人和两个普通 Strike，尚未用多敌人实证认证两个不同有效目标的错配、非零重复牌 occurrence 或生成对象。

EndTurn 只接受本轮已支持字段，拒绝卡牌、目标、药水和未实现选择等额外执行字段。预测复用固定版本已有的 `CombatBeamSolver.AdvanceRound` 内部方法和稳定边界收尾；原生端使用 `EndPlayerTurnAction` 与 ActionExecutor，随后核对敌方行动及下一玩家回合。这个内部方法接口与 DLL 版本耦合，不是公开稳定 SDK。

循环上限为 8 个实际动作、9 次查询，并在完成目标攻击、EndTurn 和新边界重新求解后停止。查询维持 beam 8、1000 节点、5 秒软门、10 秒协作取消、单线程配置；进程硬门 120 秒。节点/时间耗尽不采用候选。

没有空执行未知牌效。未支持的 primary/nested/turn-start 选择、药水、自动重放、结束回合型出牌和附魔字段在采用前拒绝；当前预测到战斗终局也在派发前显式 Unsupported，因为尚未将终局/清理差分契约接入该循环。真实动作后的差分错误会保留已确认原生前缀，不伪造模拟器成功。

## 构建与证据身份

| 用途 | 构建 ID | 目录 |
| --- | --- | --- |
| 三项修复的单步 live/replay | `A549BBFCEFF0464625DF0B6B704930F70F93B25A8E4E6B0B73BA294E053C920E` | `.build/native-build/solver-repair-final01/` |
| 普通攻击/EndTurn live/replay | `35C6C189D99FCA0D9ABC162A6BE9EA8DF156170EC49791A58E170F090F91796D` | `.build/native-build/solver-loop-final02/` |
| 最新默认宿主编译/API 控制 | `8AD4C519CA298703FD0B22092CA314E5E284927FD18C2B85106F057947ACE4D8` | `.build/native-build/solver-loop-native-default01/` |

前两次原生执行属于修复构建，后两次属于循环构建；不混称四次都来自最终循环构建。修复构建的复制编译源码和产物保留，执行包装器当时核验实际源/输入前后一致。阶段二推进了五个 live 源文件，具体差异路径写入 `repair-source-drift.json`；不能仅用 HEAD 或当前 live 源代替阶段一的身份。

依赖快照保持 `.build/pinned-inputs/combatsolver-0.50.1-gate01/`，DLL SHA256 `4503D5234915B4B0C163556C6FDB4FA0C38F45796F656AB4B2C7E76A1BF7D98B`。MIT 和上游归属保留；游戏/Ritsu 仍是本机外部依赖，未发布其二进制。15 个旧锁定输入、52 个兼容源码及旧原始执行/工作证据哈希保持。

原始执行：`.build/runs/solver-adapter-exec-01/` 至 `04/`。`01/02` 为修复的 live/replay，`03/04` 为四步循环的 live/replay。收口目录 `.build/runs/solver-adapter-repair-work01/` 保存 ledger、构建日志、控制、验证及 closeout。

## 验证范围和预算

- 原生执行 4/4，均 raw exit 0。累计进程运行 28.123 秒；最后一次在预算登记后约 378 秒完成，未达到 20 分钟或 2 GiB 门。完整字节统计见 closeout。第 5 次请求在创建进程前拒绝，ledger 未改变、没有第五输出目录。
- `verification.json`：113 项原始证据、独立普通 Defend/Strike 规则、真实下一回合、状态/RNG 重放、构建/历史保留检查通过。新验证器先修正了对原目录已有 reader 输出的假设，随后使用与原 trace 哈希相同的正常副本检查结果；没有覆盖原始执行文件。
- `production-controls.json`：14 次生产检查器调用，4 正常/等价接受、10 反例拒绝。五个上一轮误接受反例全部关闭。
- `extra-production-controls.json`：18 次最终循环构建的生产检查器调用，4 正常/等价接受、14 反例拒绝，覆盖旧票据声明、缺文件、捕获重复/角色/hash/模式、循环静止状态、动作、目标、完成范围及缺少最后查询。名为 `loop-step-rng` 的副本实际改动金币，报告已明确标注；真正 RNG 的覆盖来自第一组 RNG 反例及完整 RNG 重放。
- 每条循环路径额外执行 11 项真实映射拒绝控制，保持完整源状态和零派发；另有一个 EndTurn 字段正例。它们是 API 负控，不算额外游戏动作或自然策略样本。
- 默认原生新配置编译 0 warning/0 error，8 项实际依赖配置 API 控制通过。旧 24 动作默认宿主原始回归保留并重验其证据，不冒称在最新构建重新执行；共享的禁依赖逻辑未弱化。本轮影响范围为独立求解配置和新动作桥，因此没有重跑旧全原生矩阵。
- 求解器循环构建 1 项既有 Publicizer CS0436 warning、0 error；首个循环编译因缺少 GameActions using 失败，未启动原生进程，修正后使用新的 final02 目录。修复构建的 nullable warning 在后续构建消除。

本轮没有强制触发 5 秒求解耗尽或 10 秒取消；进程终止 helper 继续复用已测试版本，不将自然快速完成当超时注入通过。所有失败构建和控制结果保留。

复核入口（只检查证据，不执行游戏）：

```powershell
python tools/native-run/verify-solver-adapter.py
dotnet .build/native-build/solver-loop-final02/bin/Release/net9.0/OfflineSearchHarness.dll --review .build/runs/solver-adapter-repair-work01/extra-controls/loop-live
```

## 下一步交接

五步接入路线中的修复门已完成；普通动作/EndTurn 已得到这个自然片段的真实差分与重放。下一项是必要的真实子选择：先复用 `GetActionChoicesInExecutionOrder`、PlanCardChoice token 和 NativeChoices，验证一次非首项 Exhaust 选择，按来源/实例绑定并拒绝缺失、多余、过期答复。随后补单场真实终局，再回到 M1 自然路径。多敌人目标、非零重复实例、药水、自动出牌/复活、终局清理仍是独立待验项目，不能因为求解器已有相应预测代码就称执行桥支持。

当前批次已耗尽原生次数；不重置 ledger、不扩大本批预算。后续由新批次登记明确上限，最终 M1 同构建全量验收仍保留。未操作游戏 UI、Steam/在线服务、真实存档或其他项目，没有提交推送。
