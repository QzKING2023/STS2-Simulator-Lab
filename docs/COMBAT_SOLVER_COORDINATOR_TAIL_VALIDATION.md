# CombatSearchCoordinator 有界在线尾部验证

2026-11-04。本轮复用固定上游 Coordinator，在已验证的自然开局110根动作之后完成8个在线原生动作、两次 EndTurn、逐步差分和再次求解。总确认118根动作，原生玩家HP80、第三回合、Vantom HP170。按8步尾部额度结束，原始结果3/BudgetExceeded/Ongoing/DataComplete=false。没有新增BossVictory、RunVictory，第5步和M1仍未完成。

## 输入、复用和采用规则

固定v0.111.0/41cef1ea、Ironclad/A0、原种子；输入来自 `.build/runs/solver-shop-exec-01/` 的route/choices/reward-choices哈希。前110动作完整执行原生命令，并逐根比较前后状态哈希；没有恢复伪造内存状态。新profile明确标注 `prefix-replay/live-tail`，第三条为 `frozen-prefix-and-tail-replay`；它是受控冻结前缀实验，不是全程在线策略自然通关。新增配置不包含Boss或卡牌特判。

复用 `CombatSearchCoordinator.Solve`、Smart药水层、既有原生派发/选择桥、父fork隔离、完整continuation/RNG和旧票据审计。未改固定上游源码/DLL、评分或牌效。`NativeSolverCoordinator` 单独登记采用规则，旧 `node-first-action/v1` 和严格模式保留。

每次查询采集StartTurnNumber、DeathTurn、OnlyDeathRoutesFound、CombatEndedTurn、预测结果/HP、搜索边界、采用决定；当前动作另采集影子终态、完整原生前状态、拥有者和continuation。当前动作先影子验证，派发后用真实回调/状态差分，再从新根重新查询。

未来死亡仅在DeathTurn严格晚于当前回合且其余门通过时可进入影子验证；当回合预测死亡拒绝采用。所有动作影子致死均在派发前停止；EndTurn额外调用上游 `LiveEndTurnRiskEvaluator.Evaluate`，致死或未解决选择不派发。TimeLimit、取消、无候选、未建模/近似、非法/过期动作、差分失败显式停止。搜索预算、预测结果、采用决定和原生终态分别保存；None不代表穷尽搜索，OnlyDeathRoutesFound只描述保留候选。

## 原生执行与预算

预登记最多3次原生执行、20分钟、2GiB、单进程120秒。`plan.json`、各输入plan及ledger保留。

| 执行 | 实际结果 |
| --- | --- |
| 01 | 110动作冻结前缀，Coordinator首次查询，未派发尾部动作；按probe结束2/Unsupported |
| 02 | 前缀后在线8动作，两次EndTurn，实际重求解；118/118确认，按尾部8步门结束3/BudgetExceeded |
| 03 | 同一冻结动作/选择输入真实重放，118/118确认，全部状态/RNG/continuation一致；仍3/BudgetExceeded |

首查询q449不再是旧Beam末端死亡路线：Coordinator返回NodeLimit、选择成员展开256、累计819节点、非死亡未终局候选，首动作PommelStrike。八次查询均NodeLimit、预测未死亡，没有把不完整搜索转换成胜利。q449–455累计819，q456累计1843。最终状态HP80/Turn3/Vantom170。原110前缀的七次胜利、四次商店购买和休息升级保留。

**实际自然未来死亡候选的采用尚未执行**：本轮该分支仅有生产规则API夹具正负控。实际当回合/结束回合致死停止、TimeLimit/取消、复活等分支也未新增原生实证；两次非致死EndTurn的上游保护和完整差分已实际执行。不将这些未覆盖项称为完成。

请求使用既有上游派生设置：配置beam8、成员节点256、请求10秒、取消15秒、DOP1、FixedBudget，禁用novelty/普通宽度portfolio、无强制用药、PredictPotionReward=false。上游自身策略在该根状态选择有效beam45及能力成员；`work.portfolio.Members`保存实际宽度/节点预算/展开/边界/选择/耗时，`work.attributions`保存主搜索、精炼与Smart药水层累计及次数。**配置beam8不等于所有成员实际beam8**。q449为PrimaryBeam256、RefinementBeam51、SmartPotionGradient512（两次搜索），累计819；详细成员可见重放q449。4096是返回采用门，不是第4096节点的精确熔断。

## 构建和验证范围

- 初次probe/live：`D0A6A630DE57FE3A1780346C8CC54559B301FA19494BF260C6FC7D72F0F14190`，`solver-coordinator-final02`。
- 重放生成器：`A4E5C3D8B0B32F47157438F87F3DC6F1D982F7AA93081AC838C53995001A5C68`，`final03`。
- 最终检查器：`232FD9346F944665FADA813C2CE5DAD9F904D2FECAE8714C1A7B1EB8F84DD65A`，`reader04`。

live→replay编译源仅三处变化：查询增加上游PortfolioTelemetry、检查器登记switch事件、收紧前缀收尾检查。派发/搜索/评分/设置未改；逐步全部状态/RNG、选择、动作、预测与工作量次数一致。**这不是同构建live/replay**，不替代最终M1同构建全量门。reader04相对final03仅检查器的影子归属/结果绑定变更。构建ID重算、compiled/artifact/input哈希均一致，当前源绑定reader04。

`facts.json`339项核对通过，包含每步完整状态/RNG/动作/旧票据/父fork和输入锁。17项生产采用规则API控制在最终检查器通过；14次最终生产reader调用全部符合预期，覆盖三条正常原始来源副本、致死/终态矛盾/拥有者/牌实例/采用决定/死亡锚点/工作量/未知规则损坏，以及旧预测死亡拒绝、商店Unsupported和严格预算前缀。控制仅作用于标记副本，原始证据未更改。检查数量不表示对局数。

早期reader遗漏新switch事件分类，第一次正常probe读取被拒绝；保留 `preflight-reader.json`。检查器登记后重验成功，未为此追加原生执行。final01构建在环境断连时未生成manifest，未算成功构建或原生执行；恢复先核对进程/文件，未重复不确定任务。

MemorySaveStore、ShouldSave=false、Steam未初始化、离线单人服务、无UI/遥测/异步故障保留。三次各SaveProgress/replay拦截25/7次。15个锁定输入、52兼容源码、826个solver源和6程序集不变；MIT及归属保留。编译既有4warning/0error，git diff --check通过。未执行无关全量原生矩阵；未新增RandomForeseer运行或预测器。预算、锁和最终字节见closeout。

## 复现及后续

已有原始执行 `.build/runs/solver-coordinator-exec-01/02/03/`，集中材料 `.build/runs/solver-coordinator-work01/`。在副本上调用最终检查器 `dotnet <reader04>/bin/Release/net9.0/OfflineSearchHarness.dll --review-solver-coordinator-prefix <副本目录>`；事实入口 `python -X utf8 .build/runs/solver-coordinator-work01/facts.py`。原始失败结果必须保留，不能作为完整成功读取。

本批三次额度已关闭，不追加原生执行。下一有界批次可沿用这一通用配置扩展至真实战斗终态及奖励/跨幕，不针对单场调整评分；先补同构建的在线与重放，并明确未来死亡候选仍缺真实派发实证。战斗外预测仍优先复用RandomForeseer单节点入口及其隔离门，不假定live Player/RunState可多步精确克隆。独立审查和最终M1全量门待完成。
