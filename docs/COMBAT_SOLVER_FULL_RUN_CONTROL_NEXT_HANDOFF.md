# 下一回合：先复验逐幕生成链，再接回合边界选择

用户原M1持续授权不变，不需逐步请批；本批执行额度关闭后不得重置旧账本继续跑。

1. 读取本轮[验证报告](COMBAT_SOLVER_FULL_RUN_CONTROL_VALIDATION.md)、`full-run-work01/facts.json`和当前`solver-full-run-final06`manifest。实际native producer是final04；final06修正逐幕调用/必需观察后**尚未原生执行**。先用新的有界批次跑原110步前缀→当前自然尾部，并同producer重放，要求`full-act-enter 0`→首Boss/reward完成→`full-act-enter 1`真实发出且与实际动作和完整快照一致。不要给旧raw补事件，旧pair仅该项未认证。
2. 现有全局预算/退出31条观察已验证；保持上游custom搜索与dop1不变。已读四档/并行的具体值见报告：它们不是整局预算解决方案；不要因字段Unsupported盲目提高搜索档位。后续需要强度或性能实验时另立基准并复用上游配置。
3. q836卡在实际EndTurn之前，268确认=268派发。先核对上游`src/Prediction/KnowledgeDemonChoiceSupport.cs`、`Search/CombatBeamSolver.ExecutionChoiceContinuation.cs`、`Runtime/NativeChoiceRuntime.cs`及`Runtime/SolverController.cs`。上游已能搜索敌方回合的选择；NativeChoiceSession.SetPlanAndStartDriving要求NGame，不得启用UI驱动或全部Ritsu补丁。优先复用纯匹配/次序/SourceId/Timing消费契约接现有无UIICardSelector，支持完整计划消费与未知来源拒绝，禁止忽略TurnStartChoices/IsExecutable或逐怪硬编码效果。
4. 生成链改动先定向真实EndTurn与敌方选择、父fork/影子/native完整状态/RNG、全部计划恰好消费、错来源/时序/缺失/多余/过期拒绝；然后自然路径继续。不是针对单场调评分。任一差分/未建模/预算停止应保留前缀；复现自然起点，不从部分状态拼接。
5. 第二幕Boss、第三幕、Architect/WinRun组合代码虽已接线，均须自然实际到达与原生数据验证，不能按夹具结果宣称已完成。最终M1同构建全量门保留。控制器/观察未认证先关门，不重复整套旧矩阵；无影响的原始证据重读即可。

建议下一有界批次最多3次原生执行/20分钟/2GiB、120秒进程树门（整局110秒协作门）；若先复验控制器占2次，选择桥实现应另有明确批次而非悄悄追加本批。保持15输入52兼容及所有历史raw，不UI/真实Steam网络/用户存档/其他项目/提交推送。
