# PhotonCut 与战斗结束钩子复验

状态：目标修复已获得原失败种子的实际终局证据；本批最终封存见 `closeout.json`。性能/策略里程碑仍未整体完成，玩家可见策略整局隔离尚未认证。

## 最终构建及实际进展

最终生成器/检查器位于 `.build/native-build/performance-choice-doom-04`，冻结为 `77F4962F55AD85A8D5C1A11395179B2F5AFBC84D4086B7874E900E666A53AD7A`。下文第一、二构建是保留的中间证据，不能替代最终构建。

| 职业 | 原种子自然结果 | 确认/派发 | 战斗差分与父隔离 | 战斗数 | 在线墙钟 / 搜索墙钟 |
|---|---|---:|---:|---:|---:|
| Regent | 第二幕 KnowledgeDemon Boss 真实 Death | 442/442 | 374/374 | 20 | 115.14 s / 65.21 s |
| Necrobinder | 第二幕 Boss 胜利、奖励、第三幕 OwlMagistrate 真实 Death | 459/459 | 380/380 | 24 | 262.24 s / 187.78 s |

两条均为 Completed / Death / rawExit 0，数据完整。死亡是策略实际终局，不是预测错误或胜利。战斗差分不意味着所有战外效果另有独立模拟器。Regent 同构建重放已匹配 442 动作、374 步差分及 6055 个完整状态/RNG观察；其余重放及回归的最终范围见封存汇总。

两个新增流程入口均保留原生业务：Amalgamator 实际选择 CombineDefends，移除两张基础牌并取得 UltimateDefend，只隔离两个 null 屏幕震动调用，原延迟保留；FieldOfManSizedHoles 复用带方法/来源/patch审计的空 OnRoomEnter，VFX 由游戏自己的 TestMode 分支处理，实际 EnterYourHole 后捕获到 PerfectFit。没有新增事件效果/附魔镜像，也没有宣称 RandomForeseer 对这两项预测成功。CombineStrikes 和 Resist 未实际覆盖。

生产检查器控制分别为 PhotonCut 12 项、Amalgamator 10 项（含 5 个方法的真实 Harmony 注入拒绝，但业务执行为 0）；原始机制断言 23 项全部通过。Doom 的终态捕获保留 Vulnerable 2，战后清理再清空能力，两种边界不能混淆。BoneShards 原生终态与原失败记录完全一致，修正的是模拟侧末端 Hook 分发。

证据根目录为 `.build/runs/performance-choice-doom-work01/`：`final-full-*-live/replay`、`final-*-review.json`、`*-replay-exact.json`、`mechanism-assertions.json`、`topdeck-final04-controls`、`amalgamator-repaired-controls`、`source-changes.json`、`incidents.json`、`preservation-audit.json`、`native-evidence-seal.json`、`measurements.json`、`closeout.json`。15 个 Lab 源文件/脚本变化及上游 3 文件变化独立列在 source-changes；文档、分析工具另行保留。复现使用 `compat_runner.py` 的全新目录和已登记预算；重验证据使用 `review_study.py full` 与上述生产控制脚本。

时间为实际记录，不是控制变量 A/B；部分运行与只读检查并行。各计时范围有嵌套，不可求和，live−replay 不是搜索耗时。未改评分、种子或既有搜索预算，不推断可靠胜率或真人水平。

下一步具体边界见 [玩家可见策略整局隔离工作包](PERFORMANCE_VISIBLE_RUN_NEXT.md)。仍只认证先前首战的受限 DTO 部署，没有把 Oracle 路线改标签冒称整局可见策略。M1 封存保持，不提交、不推送。

## 以下为保留的中间诊断记录

下列“未完成”是相应旧构建状态；当前结果以上表及最终封存为准。

## 固定范围

- 游戏 v0.111.0 / 41cef1ea，原种子 `LAB_V0111_PERF_PILOT_01`，原搜索配置和 RunToTerminal 执行策略。
- 本批登记：`.build/runs/performance-choice-doom-work01/registration.json`。16 次原生执行、45 分钟运行窗口、4 GiB 证据；每完整运行 465 秒外部停止门。
- 全信息 Oracle 在线求解；冻结重放仍调用求解器。耗时差不能直接作为搜索成本。
- 不修改角色、安装、实际存档，不启动 UI，不提交或推送。M1 原始封存不变。

## 通用修复

1. `NativeSolverChoices` 复用上游 MoveToDrawTop/Hand 与原生 FromHand，校验真实类型、StateKey、重复实例位置及当前请求。业务效果全部由原始 OnPlay 执行。
2. 出牌中的抽牌使候选不同于起始手牌。因此记录选择时的完整状态、牌区和响应 stamp；生产检查器将候选、选择、返回对象及实际牌堆顶部交叉绑定，不能用起始手牌冒充选择时手牌。
3. 新派生依赖 `duration-ending-boundary.1` 保留原生 TickDownDuration 的 skip 标记处理，并在结束时停止实际减层。未改搜索评分、删差分字段或强置期望数值。
4. 随后原种子暴露 BoneShards 全体击杀后 Flatten 降费差异。固定游戏的 Hook.IterateCombatHookListeners 在派发开始时检查结束状态；上游 AfterAttack 镜像缺此门。第二派生 `combat-hook-ending.1` 修复 AfterAttack 和敌方 AfterSideTurnEnd 入口。已开始的监听器序列不逐个截断；配对攻击的分支临时状态通过现有 AbortAttack 清理，死亡业务钩子保留。

精确原生调用链从本机固定程序集只读确认；反编译材料仅保留在忽略的本地证据目录，不属于仓库发布内容。

## 构建与来源

- 第一构建 `9B60CC2E1196CFDB9F9773F5EED0051B2751D08412BE426289EFD4A3C2657365`：`.build/native-build/performance-choice-doom-01`。
- 最终修复构建 `355E11E41E51B0BED03EE934E6915B45144E5A5DDFDC64ACD386172B1C875A00`：`.build/native-build/performance-choice-doom-02`。
- 最终 Solver SHA256 `BBAD4C281E026348AC0D298209F18F8397AEC2A34704D7DCFFBE9C039B805551`。
- 派生链：orb-terminal-boundary.1 → duration-ending-boundary.1 → combat-hook-ending.1；MIT 与上游归属保留。两个新准备脚本按源及依赖锁校验并只写新快照，旧快照未覆盖。
- 两个构建均 5 个既存警告、0 编译错误。最终验收按实际构建分别记录。

## 已确认的定向结果

- Regent 第一构建 a240/q201 PhotonCut 原生置顶成功，完整差分及父状态隔离通过；后续到 357 确认/358 派发、300/300 战斗差分，在第二幕 Amalgamator 的未审计入口 Unsupported。未改路线避开该事件。
- 生产 MoveToDrawTop 检查器 12 控制（1 正/11 负）通过；最终检查器重读相同原生样本也通过。来源与构建分列，后者不是新构建原生运行。
- Regent 第一构建的冻结路线只含确认动作。重放到 a357 一致，随后缺 a358 输入被拒，保留 InvalidAction；不称完整失败尾部重放。两条轨迹到实际 confirmed a357 共 4998 个状态/RNG 观察逐项相等，300 个差分步骤相等。
- 早先按 root 数值筛选的辅助比较错误包含退出后的观察，失败报告保留；改为截至实际 confirmed 边界重新验证，未修改原始轨迹。
- Necrobinder 第一构建原 a88/q77 Doom 差分已关闭，但在 a174/q146 发现 AfterAttack 差异并记录 DataError。最终构建两个停点均 exact、父状态不变；终局结果见收口证据。
- Core 98 检查通过，Core DLL 与原生构建相同。

## 未覆盖及下一依赖

- Regent Amalgamator 没有免费离开分支；原生两条分支均移除两张基本牌并生成新牌，包含非空实例的屏幕震动调用。需要复用现有原生牌组选择/生成账本并审计无 UI 展示依赖；不能只放行类型，也不能绕过事件。
- 玩家可见策略目前仍仅有首战部署及 DTO 纯函数门，整局信息隔离尚未验收。整局工作须在已有原生控制器上增加统一决策边界，分别覆盖地图、事件、奖励、休息、商店及子选择；策略只收序列化公开字段，主机保留 Oracle 诊断但不得把排序后的隐藏候选、种子或 RNG 传给策略。RandomForeseer 的 Oracle 预测也不能流入该策略。
- 首批性能数字是兼容性运行实测，部分时段存在本机只读检查/反编译任务；不能当作受控性能 A/B，不能从单种子推胜率或真人表现。
## Follow-up build 07 (bounded, 2026-11-05)

Build `79A563588511931910E3809AD95E39230E261C8DC68F2DE2A04F70E7D4AD860B` was compiled from the same pinned game and solver inputs. The generic choice bridge now accepts native `FromCombatPile` for logical pile effects and the native `ApplyEthereal` hand selection used by SculptingStrike; source, timing, context, and instance-token checks remain enforced.

The earlier CombineStrikes run on build `6CBE4EF29FACEA770AF05B4041F6DCD07D23CCB0245C1207F0563B0D4CF39367` completed a 447-action Regent run with exact state/RNG and parent isolation checks; the trace records the native `COMBINE_STRIKES` option. Its review passed.

Necrobinder on build 07 passed the Cleanse and SculptingStrike choice surfaces and reached 458 confirmed actions across 22 battles. It then ended with a real native Defeat, while the solver/native differential failed at action 381 (`DataError`); this is retained as a failed bounded strategy sample, not an unsupported or success result. The run is recorded at `.build/runs/performance-choice-doom-work04/repair2-full-necrobinder-live`.

Defect on build 07 completed 236/236 actions across 12 battles with a classified real Death, exact state/RNG and parent isolation (202 steps). The production review passed at `.build/runs/performance-choice-doom-work04/regression-defect-review.json`.

`FieldOfManSizedHoles` Resist was not reached by the Necrobinder route before the recorded failure, so Resist remains unverified. The visible-policy work remains bounded to the certified Ironclad first-battle probe; whole-run visible short-flow certification is still open.
