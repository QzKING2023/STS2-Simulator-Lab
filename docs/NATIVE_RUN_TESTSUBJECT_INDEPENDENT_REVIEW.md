# TestSubject 策略与冻结重放独立复审

2026-11-03。**当前声明范围通过，未发现必须先修复的生产缺陷。TestSubject 胜利、RunVictory 和 M1 均未完成。** 当前 live 的非首项耗尽排序未覆盖，不包含在通过口径内。

复审对象：`187D81167676164F126029FDA951C3D6A20D49E6BDE05317F886EBA081E21D49`。报告配套原始证据在 `.build/runs/testsubject-independent-review01/`；生成构建仍为 `.build/native-build/testsubject-final02/`。本轮只新增独立审查材料和下一轮设计，没有修改生产源码、原始证据、角色、安装目录或存档，没有启动原生游戏流程、UI、提交或推送。

## 复审结果与证据

| 项目 | 独立结果 | 证据 |
| --- | --- | --- |
| 冻结标识及生成链 | manifest 标识重算相等；245 项来源/产物/固定输入核对通过 | `manifest-identity.json`、`generation.json`、`fingerprint.json` |
| 15 输入与 52 兼容源码 | 67 个既有固定文件哈希不变 | `generation.json` |
| 当前生产变化 | 对 D704… 基线仅 Program、NativePreparation、NativeThirdPolicy 三文件；采集器和生产检查器未改 | `generation.json` |
| 冻结原始证据重读 | 144 次生产检查器调用：50 接受、94 预期拒绝，零预期外结果 | `reread-summary.json` |
| 新独立控制 | 1 个合法重排/重封签副本通过，13 个损坏副本按具体语义门拒绝 | `controls-plan.json`、`controls-summary.json` |
| 轨迹、前缀与事实 | 89 项核对通过 | `facts.json` |
| 原始证据及生产文件 | 复审前后未改写 | `closeout.json` |

144 次重读包括上一轮的 130 份完整受影响输入与最终 14 个原生进程证据；拒绝部分含历史坏副本、真实未完成/错误前缀、缺锚点旧记录，不能描述成 144 次成功游戏执行。本轮总计 158 次检查器调用，**新增原生游戏执行为 0**。按[分层与信任边界](NATIVE_RUN_EVIDENCE_TRUST_BOUNDARY.md)，已有同构建真实重放足以支撑此次只读审查，不重复全量原生矩阵。

独立事实脚本初版误把正常初始化视为 `NewRun` 根动作，导致单项检查失败；实际源码先 `CreateForNewRun/SetUpNewSingleplayer`，发出 `new-run` 观察，再执行首个 `EnterAct`。初版结果保存在 `facts-first.json`，修正审查脚本的事件名后重新读取原始文件，89 项通过。这是审查工具的假设错误，不是运行代码故障，也没有改写生产结果。

## 两处修复的实际行为

`Program.cs:171` 识别下一冻结根动作是否为 EndTurn；即使当前存在合法卡，也进入原来的 `OnEndedTurnLocally` / `EndPlayerTurnAction` 路径。`Action` 仍在派发前比较整个动作，执行后比较 before/after 状态哈希。`NativePreparation.cs:119` 使冻结决策面对全部原生合法候选，不受当前启发式负分排除。真实卡合法性仍由 `CanPlayTargeting` 和原生执行决定。

同构建的旧 610、旧 618、当前 618、burst 612 路线均实际重放。当前 detailed/minimal/off 的 1236 个完整 dispatch/confirmed payload 及 route、choices、reward-choices、final-state 一致；完整状态中包括 RNG 各流的 counter 和全部状态字，并非仅计数相等。旧 610 重放确实存在“仍有合法卡而执行 EndTurn”的边界。

既有三次真实非法输入执行保留：错误 turn、额外字段、未知 kind 都是 InvalidAction，12 确认/12 派发，在第 13 次派发前拒绝，不能称总派发为零。它们的完整 12 步前缀也与原始路线相同。修复前 final01 五份 InvalidAction12 失败继续保存并被检查器拒绝，未改成预期成功。

`NativeThirdPolicy.cs:80` 附近的 `PrepareTestSubjectChoice` 只针对实际 TestSubject、升级 True Grit、live 模式；使用原生手牌对象枚举生成已有选择桥的计划。真实 `TrueGrit.OnPlay` 先 GainBlock，再 `CardSelectCmd.FromHand`，最后 `CardCmd.Exhaust`。`NativeChoices.Resolve/Validate` 对真实请求、上下文、状态 stamp、候选 handle、数量和重复消费继续校验；请求、返回对象与实际耗尽历史能够关联。

burst612 的 a597/q8 选择 o1，真实对象 i1220/STAMPEDE 出现在同根原生 CardExhaustedEntry 和耗尽堆。它证明最终构建能执行非首项冻结选择；**当前 v39 live 的选择全部为 o0，不能证明 live 排序的非首项分支。** 上一轮保留的覆盖失败不应删掉或转绿。

新独立控制分别检验非法 handle、返回错实例、缺返回、重复解析、错根、候选映射错配、返回早于解析；另检验 EndTurn 字段/根动作额外字段、死亡后存活 HP、敌人冒充玩家死亡主体、终态 RNG 状态字改变和确认计数减少。它们在生产语义校验处退出 1，而不是因封签损坏被拒绝。合法重新序列化副本退出 0。

## 实际进度及风险

16 个开发样本均是真实 Death，均从正常开局开始，前 574 动作的 1148 个完整边界与 D704… 原始路径一致。当前 live 为 Death618，第三形态敌人剩 119HP；一次实际死亡回调，玩家 HP0。没有 TestSubject 胜利回调，没有把 phase 转换或死亡当成胜利。

v35/burst612 的 a595 原生第二次 respawn 后玩家 67HP；第二阶段敌方未格挡 HP 损失 0、自付 Hemokinesis 2HP，最终仍 Death612。这是局部改善。当前 v39 在 a594 转入第三形态时为 55HP，也不能把旧策略的 67HP 归给当前 live。16 次同种子调参不构成独立胜率样本。

目前没有需要回退此冻结的证据。以下是明确的非阻断限制和下一轮前置门：

- live 非首项耗尽排序需要针对性原生证据。计划在打牌前按手牌索引生成，而 GainBlock/Hook 在真实选择前执行；当前轨迹未见候选变化，不能据此推广到所有能改手牌的 Hook。下一轮组合决策应在真实请求到达时选择具体候选对象，或验证计划的候选身份/顺序仍相同，不能只假定索引稳定。
- 当前是有限启发式，不是完整伤害模型或最优规划；不能据所有失败断言固定入口无解。Stampede 原生自动打牌仍可自伤，手动守卫不能代表完全避免自伤。
- 预持折扣遗物的 rawCost 定向覆盖、默认遗物拾取新增方法的实际 Harmony 注入负控、复活及其他未覆盖机制继续保留。当前 Boss 样本未触发新的折扣商店，不用无关全量执行补假覆盖。
- 旧 11 份缺玩家锚点及 2 份缺 maxHP 观察的证据继续未认证；整体 M1 最终同构建全套验收仍未执行。

下一轮可按[有预算组合决策交接](NATIVE_RUN_TESTSUBJECT_BOUNDED_SEARCH_HANDOFF.md)实施；本轮没有执行该搜索，也不把设计参数称为已验证性能或胜率。
