# 原生 RunVictory：恢复、同构建整局重放与证据核验

2026-11-04。本轮未修改运行代码，没有启动额外搜索试验。**自然整局胜利及同构建冻结重放通过；M1 最终统一验收尚未关闭。**

## 结果

| 项目 | 实际结果 |
| --- | --- |
| 固定构建 | `64DB6340C5FC4511EC64B79B30E340719B92F66A8EB6400544EA7AA913A12DB8` |
| 原运行 | `.build/runs/solver-slippery-exec-03` |
| 同构建重放 | `.build/runs/solver-victory-replay-exec-01` |
| 终态 | 两次均真实 RunVictory / Completed，499确认／499派发，原始exit0 |
| 战斗 | 22场；393步当前动作native/sim exact，parentUnchanged全部为真 |
| 重放比较 | 499动作、全部子选择/奖励选择、最终完整状态一致 |
| 完整观察 | 7589个有序state观察点的kind/root/context及完整状态全部一致，包含RNG状态与计数 |
| 文件保护 | 原始胜利1932文件保持哈希；1146构建清单与15输入+52兼容源码执行前后无差异 |
| 生产核验 | 24项既有事件控制（2合法+22拒绝）和原始整局重读通过；重放整局生产检查器通过 |
| 独立比较 | 16项结构/来源/终态/隔离/状态比较通过；未将哈希一致当作执行真实性的唯一证据 |

## 来源、调用及真实边界

固定游戏v0.111.0、Ironclad A0、开发种子 `LAB_V0111_NATIVE_DEV_01`。原始运行是正常开局、110动作冻结前缀后通用Coordinator在线尾部；本轮用准确的 `solver-native-darv-act2-replay` 场景重放全部499条已执行动作。PrefixCount仍为110，因为该配置还决定搜索引擎切换，不能改成499。没有重新构建、替换输入或修改策略。

SlipperyBridge复用固定RandomForeseer 0.13.19候选算法、克隆事件RNG；原生HoldOn/Overcome实际执行。既有MummifiedHand镜像保持；q1224属性返还差异以通用原生账本根捕获修复。其来源、失败及控制详见[上一批验证](COMBAT_SOLVER_SLIPPERY_VALIDATION.md)。

最终a494击杀Boss、a495奖励、a499 Architect调用WinRun。清理前HP18；Won=true、Losses=0、Win/RunEnd开始与返回各一次，清理后HP0是胜利生命周期。两次完整状态均一致，不借用搜索预测判断原生终态。

整局Comparison保留NotCompared；393战斗动作精确比较不能外推为所有节点独立模拟。预测中静态近似、未执行反事实与Oracle信息模式保留。在线/冻结模式、header输入哈希、时间和搜索工作量本来不同，不删除这些字段，也不宣称整份trace逐字节一致。所有state观察点采用完整结构比较，没有删RNG字段。

## 断连与预算

断连前3次原生执行均已退出；恢复时没有发现遗留dotnet/OfflineSearchHarness/python测试进程。旧账本延后补关闭记录，失败01(DataError422/422)与02(Unsupported110/110)完整保留，不重置预算。

本轮预登记只执行一次重放，实际1次，148.357秒，进程480秒/整局450秒上限未触发；峰值工作集1,792,204,800 bytes（4GiB上限）。封存时包含本轮输入/输出/工作、固定构建和新依赖的逻辑字节486,443,104（2GiB上限）。工具原有窄统计448,470,320，差异来自列明的统计根，均保留。数据输出前后源码、构建和输入未变化。

73次SaveProgress、22次WriteReplay是拦截调用。MemorySaveStore、shouldSave=false、Steam未初始化、无UI、无guard faults；没有据此声称完成全OS文件访问证明。无提交推送。

## 可复核入口

- `.build/runs/solver-victory-replay-work01/closeout.json`：结果、预算、核验矩阵。
- `comparison.json` / `compare.py`：独立于汇总声明的完整结构比较；16项。
- `recovery-binding.json`、`original-raw-lock.json`、`replay-raw-lock.json`：来源封存。
- `production-rechecks.json`、`original-review.log`、`replay-review.log`：实际调用生产检查器与退出状态。
- `input/`、`replay-plan.json`、`plan.json`、`ledger.json`：冻结输入、预登记与真实执行。

重验已有证据：`python -X utf8 .build/runs/solver-victory-replay-work01/compare.py`（不启动游戏原生执行，只重读结果和调用检查器）。请保留原证据，审查者用新目录写新增结果。

当前为实施者在恢复回合所做的独立计算核对，并非另一独立验收责任人签署。下一步按[最终验收交接](COMBAT_SOLVER_RUNVICTORY_NEXT_HANDOFF.md)完成独审、同构建回归和预登记样本覆盖；一条成功开发路径不能替代整个M1验收、总体胜率或全内容支持。
