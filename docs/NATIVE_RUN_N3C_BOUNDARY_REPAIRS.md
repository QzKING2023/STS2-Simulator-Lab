# 4C 静止边界与卡取得结果修复

日期：2026-11-02。依据[独立复审](NATIVE_RUN_N3C_REPAIR_REVIEW.md)的两项必修实施。当前状态：**修复及同构建1–4C统一回归已通过，最终独立验收已通过。** 见[最终独立审查](NATIVE_RUN_N3C_FINAL_REVIEW.md)：502次生产reader执行符合预期，历史21反例闭合。下文原始实现汇总中的 `passed-pending-independent-review` 保留为历史记录，不改原证据。本报告不代表M1完整run已经完成。

## 实际改动

- `NativeRewardContract.cs`：以有限 `StaticWrapper` 操作集合统一约束 UsePotion/TreasureOpen/TreasurePick/TreasureExit 的直接子入口及最后子返回到根返回的完整状态。每个锚点均为已复制的 Open/LastChild/ReturnedState，比较整个结构；没有增加金币/RNG字段特判。根 open 同时明确 subject=null 的真实记录结构。
- `NativeRewards.cs`：原生 CardReward.OnSelect 及 card-menu 完成后，以实际奖励对象为键记录真正取得的 CardModel，再由 Collect 消费一次并写到外层 acquired。检查器把外层 acquired、success、完整后态与该次子菜单绑定；旧牌、null、缺字段和错误来源不能替代本次新卡。跳过未取卡仍是null；GoldReward不得声明对象取得。
- `NativeRewardControls.cs`、`run-native-n3c.ps1`：保留原117控制，新增175控制，系统覆盖17种操作和本次静止边界、取得身份与合法等价，不只登记十一份样例。

完整边界列表、归属、结果结构和真实业务变化区间见[4C生命周期契约](NATIVE_RUN_N3C_LIFECYCLE_CONTRACT.md)。没有改变原生效果；Offer/Populate、事件、卡/遗物取得Hook、同步器业务及Travel等真实变化区间保留。外层卡取得字段的结构变化明确限于当前内部profile，新reader不提供旧4C结果布局兼容承诺；旧原始证据不改。

## 冻结与实证入口

- Build ID：`458B3547C7B2E9A979EF7CC299C024E9BFA9704861D734FD370133EE3EE16652`。
- Native DLL SHA256：`96B2A0A1B82E7272509551426FDEA9E414BC05A449BC8800D7A4EBC403CC6682`。
- Manifest：`.build/native-build/n3c-boundary-final03/build-manifest.json`；编译0 warning / 0 error。
- 实际源码快照：`.build/runs/n3c-boundary-work01/source-snapshot-final03/` 和 `source-snapshot-final03.json`；重新计算标识为 `build-id-final03-check.json`。
- 统一调度：同目录 `run-final03b.ps1`、`final03b-progress.json`。结果目录前缀为 `.build/runs/n3c-boundary-final03b-`，执行产物均为上述 final03。
- 生产控制：`n3c-boundary-final03b-suite/reward-controls.json`。4C为55原生进程、561项suite检查、292生产控制，均符合预期；292中278损坏拒绝、14等价接受。
- 补充独立核对：工作目录 `boundary-inspection.json` 为561项、0失败，逐份对比175新增控制的实际完整payload；`repair-inspection.json`保留原十反例修复的371项检查、0失败。
- 重新构造审查反例：`fresh-review-final/summary.json` 42次生产reader、0不符；`fresh-adjacent/adjacent-summary.json`另外4次、全符预期。合计39损坏拒绝、7等价接受。全部从本次正常证据构造，不以旧布局不兼容导致的拒绝冒充修复。

## 正负控制范围

| 新控制类别 | 数量 | 实际作用 |
| --- | --- | --- |
| 17种operation × 7生命周期错误 | 119 | 整棵操作缺失、start缺失/重复、return重复、return错误root/context、parent错配、return提前；调用生产reader，拒绝后保留实际日志 |
| 六组子类型/领取或跳过 × entry/return × 三类漂移 | 36 | potion-use；treasure-normal/extra/choice/exit；treasure-skip的choice，分别改变金币、完整RNG一项或未知嵌套字段 |
| 外层acquired | 8 | 卡奖励指旧牌/null/缺失/错误来源；卡跳过和金币奖励假取得 |
| 六组正常路径的两类等价 | 12 | 宝箱领取/跳过、卡领取/跳过、药水和真实嵌套Offer；属性重排与所有快照一致扩展 |

`boundary-inspection.json`还逐项核对实际正例：静止entry包括集合80、药水10、宝箱Open12/Pick6/Exit6；静止tail包括集合36、药水10、宝箱Open/Pick/Exit各6。8个CardReward尝试的外层/子结果/完整后态一致，实际取得时来源是本次新永久卡。正常观察、业务清场、嵌套Offer和合法skip仍通过。

原十一误接受反例与新控制的对应、实际rawExit和具体语义诊断记录在 `boundary-inspection.json.fixedEleven`。原十个更早反例对应在 `repair-inspection.json.fixedIndependentCases`。另有上述独立fresh脚本以原始审查变异逻辑重新构造，不只依赖控制名称。

## 最终统一回归

最终汇总为 `.build/runs/n3c-boundary-work01/summary.json`，状态 `passed-pending-independent-review`；`closeout.json`再次核验源码/输入/编译输入/产物和实际Core驱动，并记录补充检查脚本hash。最终调度五项原始退出均为0，包含预定错误结果的矩阵按各自原始退出另行核对。

| 范围 | 同一最终构建结果 |
| --- | --- |
| 4C | 55原生进程、561 suite检查，0不符 |
| 4B | 34原生进程、307 suite检查，0不符 |
| 4A | 16原生进程、273 suite检查，0不符 |
| N2/步骤1–3 | 74原生进程、580 suite检查，0不符 |
| 合计 | **179原生进程、1721 suite检查**，全部符合其预定结果 |
| Core | 98实际执行检查通过；7个驱动文件和实际Core DLL与冻结产物核对一致 |
| 补充核对 | 648原矩阵/完整载荷/重放核对 + 371原修复核对 + 561本次边界/取得核对，均0失败；数量分列，不冒充额外原生执行 |
| 历史reader回归 | 115旧包符合原预期，460原文件hash未变；保留各包原构建身份 |
| 固定输入与源码 | 15锁定输入、52兼容源码不变；48实际源码及快照、30编译输入、3游戏输入、7产物全匹配 |

179进程的真实退出分布为122×0、52×1、2×2、3×3。EngineError负控、Unsupported、预算停止均保留，不把外层成功当成所有路径自然完成。自然24/24动作、冻结重放及完整RNG/观察对照保持；旧DEV01为71/71片段，DEV02为138/138真实Death，DEV03为77/78的WhisperingHollow Unsupported，均与旧完整业务文件对照一致。奖励/药水故障仍保留2/3、4/5未确认前缀和已发生效果，旧自然skip24/take25也保持。

## 复现与后续

```powershell
pwsh -NoProfile -File tools/native-run/run-native-n3c.ps1 -BuildManifest .build/native-build/n3c-boundary-final03/build-manifest.json -OutputDirectory .build/runs/local-n3c-boundary-check
dotnet .build/native-build/n3c-boundary-final03/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3c-boundary-final03b-suite/rewards-natural-1
```

复跑目录需为Lab中新目录。其他profile分别使用 `run-native-n3b.ps1`、`run-native-n3a.ps1`、`run-native-n2.ps1`及相同manifest。

开发候选保留：final01曾因控制工具局部变量名冲突编译失败；final02在新增控制遍历非操作事件时触发JSON类型异常，该批用原会话正常取消。final03的预检第一次使用不符合既有control目录命名约束的目录，被工具自身拒绝并由超时收尾；未修改该保护，改用合法新control目录后两项预检通过。随之正常取消了误启动的早期final03批次，避免混入统一结果。早期补充fresh脚本缺少write辅助函数而中止，保留旧目录，修正后以fresh-review-final新目录完整执行42份，再执行另外4份。完整验收使用唯一 final03b 批次；源/产物不再变化。相关失败目录及诊断保留，不计为通过执行。

4D的reroll、实际替换/拒收、Sozu采购拒收、新会话Neow奖励迁移和修改后宝箱金币账本仍按[4D交接](NATIVE_RUN_N3D_IMPLEMENTATION_HANDOFF.md)落实；本次不伪造取得成功。自然24动作仍只是一战到下一战片段，native-only/NotCompared；完整原版run及后续步骤尚未完成。本轮不提交推送，保留15个输入、52兼容源码及全部历史证据。

当前无执行/审批阻塞或结果不确定的动作。交根线程独立复验后接续已授权4D、5–6，无需逐步请批；保持原有无UI、不调用真实Steam/网络、不改角色/安装/用户存档的范围。
