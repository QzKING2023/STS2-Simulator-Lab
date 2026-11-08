# CombatSolver 最小接入独立审查

2026-11-04。审查对象：`50F8D1D03E1F7D3FB44DB8A58CAA08F9C690DB5DF553BEC702873ABF7BBECEEC`。

## 结论

**单步接入的业务实证成立；完整验收仍有三项小范围必修。** 可以继续沿 CombatSolver 复用方向推进，但先修复下述票据负控、信息标签和采集证据契约，再扩大动作覆盖。无需重新否定已取得的 Defend 结果，也无需为检查器修复重跑全部原生矩阵。

这不是整场战斗、Boss 胜利、RunVictory 或 M1 完成。第二次求解只提出 Strike，没有实际执行。现有许可、依赖和隔离没有发现本轮所需的新授权阻塞。

## 已独立核实的事实

- 最终 live `solver-gate-exec-02` 与 frozen replay `solver-gate-exec-03` 使用相同请求冻结。每条路径正常开局、Neow、原生 Travel 至首战，然后执行一个求解器动作，共 4 个已确认根动作；不是从手改战斗资源开始。
- 捕获调用实际 `CombatRootSnapshot.Capture`，其中比较原生与根投影 continuation，并检查捕获前后状态。搜索使用 `CombatBeamSolver.Solve` 和 `ForkSimulator`，没有把原生执行当作搜索 fork。
- 两次查询分别展开 96、85 节点。原生完整 Lab 状态（包括完整序列化 run/player/event RNG）在捕获/搜索前后相等；战斗 continuation 额外覆盖九条战斗 RNG 的 counter 和内部状态。阴影动作前后的另一父 fork continuation 不变。
- 首动作实际是 Defend `i15`：原生 `TryManualPlay` 和 ActionExecutor 完成后，能量减 1、格挡加 5、HP 不变；同一实例从手牌进入弃牌堆，永久牌组、遗物、药水和 RNG 未改变。这些是固定普通 Defend 的独立常量断言，不从求解器估值推导。
- 阴影与原生动作后的非空完整 `ContinuationStamp` 字符串相等。随后从新原生状态再次捕获和求解。
- 两次最终路径的 route、choices、两个 query、step、final-state 全结构相等。这里的“冻结重放”仍会重新执行两次搜索，并由既有原生 route 的动作/前后状态检查约束执行；不是一个不调用搜索的 PlanAction 文件播放器。
- 默认原生独立构建 `61FC506F367FF328289F67DE7183BD9B686DF7E8D2B6C19B7BA5DE06A4BDDA02` 的 24 动作回归通过，未加载 CombatSolver、Ritsu、MyMod 或 BaseLib。它与求解器构建身份分列，没有伪称所有四次执行都是同一构建。
- 求解配置在单独编译配置中，仅接受锁定依赖；MyMod/BaseLib 仍拒绝。未调用 Entry.Initialize、配置 Load/Save、UI 或在线服务，没有新增搜索补丁。宿主补丁清单、MemorySaveStore、SaveProgress/replay 拦截、单机网络服务和 Steam 未初始化的约束保留。

以上状态无扰动结论限当前投影和 continuation 覆盖的语义，以及这一个普通战斗样本；不是对所有对象、隐藏宿主状态或全部牌效的证明。父 fork/捕获纯度依赖可信采集器中的实际检查，原始 JSON 无法独立重演未记录的每个瞬间。

## 必修 R1：未消费的过期票据尚无跨原生动作实证

位置：`tools/native-run/NativeSolverGate.cs:36`、`:92`、`:101`。

`SolverTicket` 的 record 复制构造会复制私有 `used` 字段。现有 `ticket with {}` 在原票据消费及动作执行之后创建，故复制品也是 `used=true`；`Consume` 在第一个条件便拒绝。当前 `staleRejected=true` 证明的是已消费复制品被拒绝，不能单独认证未消费票据在状态改变后过期。

本审查直接调用冻结 DLL 的真实方法，完成 5 个纯托管 API 控制：新票据未消费、未消费但 sequence 不符被拒绝且不消费、实际 clone 继承已消费标记、复制品拒绝。没有初始化游戏，没有构造原生战斗状态。这个控制确认实现含过期序号门，并复现负控歧义；它仍不是一张真实旧票据跨原生动作的验证。

**最小修复：** 在实际出牌前保留另一张明确 `used=false` 的票据；正常票据执行后，用该未消费旧票据验证拒绝和零额外派发。重复消费与过期拒绝分别记录，不能用一个 boolean 代表两者。应保留一个当前有效票据成功的正例，以及现有无效字段控制。

## 必修 R2：Oracle 元数据须一致且必需

位置：`tools/native-run/Program.cs:251`、`NativeSolverGate.cs:25`、`NativeSolverContract.cs:12`。

实际 trace header 为 `player-visible-policy/oracle-trace`；solver profile 和 capture 事件声明 `Oracle`。整个运行含读取真实抽牌顺序和 RNG 的求解器，不能让顶层使用旧开局策略标签。原始文档已披露，故不属于隐瞒，但不能据此进入玩家可见策略评估。

有限生产负控还证明：删除 `solver-profile.json`，或者把 profile 与 capture 的 information 改成 `PlayerVisible`，检查器仍 exit 0。

**最小修复：** 为求解器场景明确顶层 Oracle 信息模式，并要求 profile 存在、配置身份匹配、header/profile/求解观察的一致标签。保留开局简单策略与战斗策略的区别，不把原生可见候选接口等同公平信息搜索。旧记录保留并标明旧标签，不回写历史证据。

## 必修 R3：捕获与完成记录没有完整纳入生产契约

位置：`tools/native-run/NativeSolverContract.cs:16`–`:40`（当前只约束 query、play、confirmation 和最终 state）。

本审查用冻结生产程序 `--review` 复现以下误接受，均只修改明确标注的派生证据副本；没有篡改真实原始结果：

| 副本 | 变更 | 实际结果 |
| --- | --- | --- |
| `missing-root-observations` | 删除全部三个 `solver-root-pure` 记录并重封副本 | exit 0 |
| `root-owner` | 首次 capture 的 root/context 改为旧动作/boot | exit 0 |
| `gate-counts` | 完成记录改成 1 次 query、0 个原生动作、wholeBattle=true | exit 0 |

这是已声明单步闭环的必需记录/归属/结果一致性遗漏，不要求检查器重新实现游戏规则，也不要求对全部观察共同伪造的证据作无限推断。

**最小修复：** 将当前三个捕获点作为明确角色（query1 前、动作阴影前、query2 前）绑定其 query/确认边界、完整静止状态与 continuation hash，检查必需性、唯一性和顺序。前两个属于先前 Travel 确认边界，后一个属于本次 PlayCard 确认边界；不要按当前房间重新猜旧动作 context。完成事件的计数/单步范围须从真实 query 和 dispatch/confirmation 推导，拒绝矛盾摘要。缺失、重复、错归属和合法等价即可，不扩大为通用日志解释器。

## 审查证据与成本

审查目录：`.build/runs/solver-gate-independent-review01/`。

- `facts.json`：45 项独立核对通过；其中 1 项是复用现有验证器的返回结果，不与下面 56 项相加宣传独立覆盖。
- `existing-verifier-results.json`：原 56 项验证器复用通过，仅输出重定向到本审查目录，没有覆盖原验证结果。
- `production-controls.json`：14 次实际生产检查器调用。4 个正常/等价副本接受；5 个 RNG、父 fork 标志、query root、实例或文件缺失反例正确拒绝；上述 R2/R3 的 5 个反例误接受。
- `ticket-api-controls.json`：5 个实际冻结票据方法的纯托管 API 控制；不冒充原生跨动作测试。
- `profile-api-controls.json`：复用并重新运行 8 个真实配置方法控制，输出与原证据分离。
- `build-id-controls.json`：从 manifest 重算两种构建 ID 均一致。
- 请求构建 941 source、3 inputs、69 compiled、15 artifacts；默认构建 108 source、3 inputs、69 compiled、7 artifacts 均匹配。826 个求解器源快照文件、6 个依赖 DLL、77 个原始执行文件和 271 个实现轮工作证据文件无哈希不符。15 个旧输入、52 个兼容源码未变。

本审查新增原生游戏执行 **0 次**。`--review` 在 Program 入口、宿主初始化前返回；只在派生副本落盘检查结果。未运行 UI、Steam、真实用户存档或在线服务，未修改生产实现，也未提交推送。

原实现批次仍为 4/4 原生执行，总原生计时 9.533 秒，最后一次在登记后 324.065 秒结束，均在 20 分钟门内；收口字节数约 145.2 MB（保守记录 150.4 MB），低于 2 GiB。实际查询未耗尽预算，也未触发取消。120 秒进程树停止门沿用已有测试过的 helper，本批没有新的强制超时实证，不能将自然快速结束当作超时测试。

## 依赖与复用判断

实际使用的是历史认证的 Lab CombatSolver 0.50.1 衍生 DLL，SHA256 `4503D5234915B4B0C163556C6FDB4FA0C38F45796F656AB4B2C7E76A1BF7D98B`；来源构建 `D5FF494B75BF670D2EFD0A335A6E87290F6C5A2C7B3C8511839CD68ACB0DFF1D` 的 manifest 和源码、二进制均可核对。它不是临时重建的未经固定的上游最新版，也不是无 Lab overlay 的纯上游版本。

MIT 文件及 Torch1230、hotwords123 归属已保留。游戏程序集和 Ritsu 仍是本机外部依赖；本审查不授予它们新的分发许可。RitsuUi 被加载不等于创建游戏界面。现有内部 friend assembly 接口可复用，但有版本耦合；暂不把它包装为公开稳定 SDK。

## 后续最小路线与验收门

1. **先闭合 R1–R3。** 检查器改动重验冻结原始证据和本轮有限控制；票据/标签的采集改动只需同新构建的一次普通单步 live 加一次 replay。先登记这个小批次的预算，不增加 Boss 搜索。不要求重跑全部旧原生矩阵。
2. **普通目标动作和 EndTurn。** 从实际 `PlanAction` 出发，交叉验证 TargetCombatId/TargetIndex 和活目标，验证同模型不同 occurrence；当前代码两种目标同时给出时优先 CombatId，尚无二者一致性的实测。EndTurn 使用原生调度，覆盖敌方动作、下一回合、死亡/胜利终态；不把 PlanAction.IsExecutable（只含 PlayCard/UsePotion）当作完整动作集。
3. **必要子选择桥。** 复用已有 `PlanAction.GetActionChoicesInExecutionOrder`、PlanCardChoice token 和 NativeChoices；每个字段支持或在派发前明确拒绝，候选按 StateKey/occurrence/来源绑定。先一个真实 Exhaust 非首项选择，再连续/隐式单候选/缺失多余过期选择。无法安全续算时用已验证完整动作重放，不让默认策略替求解器作选择。
4. **单场在线循环。** 每次捕获 → 有预算求解 → 验证票据 → 原生执行一步 → 比较 → 新状态重求解；真实终态由原生宿主决定。节点/时间耗尽、unsupported、InvalidAction、EngineError、差分不一致均保留确认前缀。必要药水、自动出牌、生成实例、复活只按实际遇到的依赖增量验证。
5. **回到 M1 自然路径。** 再将可替换战斗策略接到已有 run 生命周期，保留跨战奖励/地图控制器，验证真实 Boss 与原生 RunVictory；最终 M1 同构建全量门保留。不要恢复针对单个 Boss 的无界评分微调。

路线 2–5 是后续工作建议，不是本轮已实现或已认证的范围。当前较大的成本在动作/选择和生命周期映射与定向验证；真实捕获、fork 搜索和普通一步执行已证明可以复用，没有证据支持另起一套求解器。
