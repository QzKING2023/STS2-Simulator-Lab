# 自然 Boss 至第二幕首战：独立分层验收

日期：2026-11-03。结论：**本轮有限自然切片通过独立验收，未发现必修缺口；完整 M1 未完成。**

冻结：`02479D358FE8B4311A0B164D02A62BE8546C3DD9BDEF86A07B399A427311FD42`。
Native DLL：`13B5ADA8EC5EEB85E55CEB91BAD794570898C52FA372782735506415385FE1DB`。

## 本次审查范围与执行方式

遵循 [证据信任边界](NATIVE_RUN_EVIDENCE_TRUST_BOUNDARY.md)，只读审查生产实现和已冻结原始执行结果，在独立副本上实际调用冻结 DLL 的生产 `--review` 入口。该入口在原生宿主初始化之前返回。本轮 **没有重新运行原生游戏场景、没有编译或修改生产实现**，没有 UI、真实 Steam/网络/用户存档操作及提交推送。

独立证据位于 `.build/runs/native-multiact-independent-review01/`：

- `closeout.json`：最终结论及证据索引。
- `review.py`、`review-checks.json`：411 项指纹、来源、轨迹和重放核验。
- `frozen-evidence-summary.json`：335 次完整原始证据集重验。
- `controls.py`、`controls-summary.json`：24 个有边界的生产检查器正负控。
- `native-source-review.json`：12 个当前冻结原始进程的退出、来源、隔离和关键生命周期摘录。
- `generation-diff.txt`、`build-id-review.json`、`environment-current.json`：生成链变化、构建 ID 重算及当前环境复核。

## 确认的真实进度

DEV03 来自正常 `CreateForNewRun` / Ironclad / A0 / Neow，使用独立实验档案 `vanilla-all-epochs-zero-runs/v1`，不是用户存档。原始轨迹属于同一个进程、同一条正常开局 run，206 次派发和确认连续；无 `MajorFixture` 或 `EnterRoomDebug`，无手动设置 HP、资源或奖励来拼接胜利。

| 实际根动作 | 原始事实 |
| --- | --- |
| a151 | 原生地图到达 `THE_KIN_BOSS`，具体 CombatRoom i371 |
| a183 | 当前原生胜利条件、清理前回调、历史清理和确认后战斗结果；玩家存活，敌人已终结 |
| a184 | 该 Boss 的原生房间奖励完成；金币 244 → 344 |
| a185 | 真实 ready → `EnterNextAct` Task 完成；新 MapRoom / 新地图，act index 1、floor 0、node null |
| a186 | 原生 `EnterMapCoord` 到达第二幕 Tezcatara；A0 入场 HP32 → 80，新的房间、事件及三个选项实例 |
| a187 | 实际选择 NutritiousSoup，取得原生遗物，永久 Strike 获得 TezcatarasEmber amount1 |
| a188 | 沿真实地图子边到达第二幕 `BOWLBUGS_WEAK`，具体 CombatRoom i431 |
| a205 | 第二幕首战原生 Victory；战后 HP75 |
| a206 | 该战原生奖励完成；金币 344 → 361 |

最终 `Ongoing / SliceBoundary / NotCompared`，act index 1、floor 2、`MapCoord (2, 1)`、HP75、Gold361。最终导出状态与最后确认完整状态一致。两幕共 11 场实际战斗胜利；这不代表第二幕完成或完整 RunVictory。

DEV01 / DEV02 分别保留真实 Death（93 / 156 次确认），各自冻结重放一致。另一个实际非法路线重放保留 `InvalidAction / raw exit 1`、185/185 已执行前缀、`ActReady:185` 最后边界，没有把拒绝后的动作算作派发成功。

## 重放、指纹与来源

独立读取了原始 process/result/trace/route/choices/reward-choices/final-state，核对 raw exit、watchdog、派发/确认数和 producerBuildId。DEV03 live、独立进程冻结重放、minimal、off 的四类导出 JSON 结构完全相等；另外直接比较各模式 **412 个 dispatch/confirmed 完整快照**，包含完整 run/player/event RNG 状态，而非只比较末尾计数。

重新计算 manifest ID 与冻结完全相同。88 个源码、53 个编译输入、3 个游戏输入、7 个产物、88 份 dirty 源码副本均匹配。15 个 pinned Mod 输入和52个冻结兼容源码未变。采集器指纹及当前 SDK/环境与记录一致；环境记录原本是在执行后采集，不能把它当作进程启动前的环境证明，PowerShell 的 framework 字段也不是原生子进程运行时证明。

从旧审查冻结到当前构建的生成链变化已检查：自然场景增加地图/跨幕控制器、选择后续和 native card 序列化；PowerModel.Icon 适配、Ancient 入口及新卡序列化按自然 profile 启用；原有 NativeObservation 未改变。共享检查器原始证据全量重验，受影响自然生成链已有本冻结的12个执行结果及原版24/25动作烟测，分层范围成立。最终 M1 同构建全量原生回归仍是独立待办。

原始 DEV03 隔离记录为 MemorySaveStore、NetSingleplayerGameService、Steam 未初始化、ShouldSave=false、无 NGame；未加载 MyMod/BaseLib/CombatSolver。ProgressWrites=32、ReplayWrites=11 是返回 false 的拦截计数，不能解释为32/11次持久化写入。代码中的隔离断言和未知/后加补丁拒绝保持。

独立从锁定程序集静态核对 PowerModel.Icon 为纹理加载 getter、Tezcatara 原生三次选择 RNG、Soup 原生 AfterObtained 调用 CardCmd.Enchant。新增适配仅返回空纹理，Soup 效果仍由原生调用产生。既有 actual Harmony API 探针只证明注册前/后未知补丁拒绝，targetBusinessCalls=0；不将它算作游戏业务或零售展示等价证据。静态参考只保留于忽略的本地审查产物，不纳入公开项目源码。

## 生产检查器验证结果

**完整冻结原始证据重验：335/335 符合预期。** 从原始各组 producer summary 重建323个旧冻结原生证据目录，加当前12个，而非复述上一轮审查结论。1340个源文件与原封存哈希一致，审查后原始文件不变；旧证据保留原 producerBuildId。

- 251 份可完整验证的成功切片或合法终态，reader exit0。
- 84 份原错误/未完成证据，reader exit1：53 InvalidAction、15 EngineError、8 Unsupported、5 BudgetExceeded、3 DataError。这里只证明检查器拒绝将其当作完整成功；不把拒绝计作成功游戏路径，也不把所有84份都说成完成了细粒度前缀语义复算。当前 Death 和185步非法路线前缀另有直接检查。

**独立控制：24/24 符合预期。** 21份损坏副本在生产语义门拒绝；3份正常或结构等价副本通过。修改后重新封签，排除了单纯哈希/封签失败：

- 当前 Boss 条件或回调缺失、胜利时玩家死亡或仍有敌人、目标模型错配；
- Boss 奖励返回缺失，跨幕真实 Task 完成缺失、重复、借用旧动作、位置矛盾；
- 返回快照完整 RNG word 修改、首节点复用旧房间、子边候选不属于真实地图；
- 三份动作记录共同改成非法目标，Ancient 完成记录移到首决策之前；
- 第二幕战斗结果缺失、奖励全部子操作缺失、重复返回、最终静止卡数据嵌套扩展；
- Death 回调伪报活人、最后确认状态前缀漂移。

正常 detailed 自然路径、真实 Death、递归调整 JSON 字段顺序全部通过；其中原生奖励、抽牌、资源、回血及 RNG 变化保留。单个坏跨幕坐标的诊断为 `Sequence contains no matching element`，已经 fail closed；错误信息可读性可以后续改善，不阻塞本轮。

## 信任边界和下一最小工作

本结论针对可信冻结宿主产生的有限切片、重放和有限完整性契约。哈希不是执行真实性证明；本轮不承诺识别任意协同重写全部业务事实的日志，不继续把检查器扩成第二份游戏规则引擎。

最小后续按 [现有交接](NATIVE_RUN_MULTIACT_NEXT_HANDOFF.md) 推进：将奖励/宝箱状态按实际 room/session 保存，将 Boss/跨幕状态按 act/map generation 保存，再从已经验证的第二幕首战继续通用自然流程，组合既有商店和事件真实任务。其他 Ancient 选项仍需实际原生覆盖；不能把只支持所提供 Soup 的当前有限策略说成完整内容支持。

最终仍需正常开局默认全部 Acts 的真实 RunVictory及重放、完整 M1 同构建回归和预登记评估。当前是 native-only，未认证仿真差分、最优策略、可靠胜率、零售 UI/RNG 等价或完整触发因果图。Player/ResourceInfo等仍有明确 unavailable 字段，必要数据门需后续补齐。

本审查只新增本报告和独立本地证据；没有更改既有验收报告、实现、原始证据或下一阶段功能。无权限/执行阻塞。
