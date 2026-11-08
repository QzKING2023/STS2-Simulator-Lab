# M1 步骤 4B：请求生命周期与完整状态绑定修复

日期：2026-11-02。针对 [4B 独立审查](NATIVE_RUN_N3B_REVIEW.md) 的 R1/R2。**两项修复及最终同构建回归已通过，待独立复审。** 本文与原报告分别保留构建身份。

## 实现范围

运行代码仅修改生产检查器 `tools/native-run/NativeRoomContract.cs`；负控修改 `NativeRoomControls.cs` 和 `run-native-n3b.ps1`。原生房间、卡牌、奖励、选择执行器未改。既有 dirty 工作、原始开发/验收证据保留。

R1 现在为每次已派发选择保存不可变 request/session/room/root/context，以及 start、return、confirmation、boundary 的完成状态和返回/确认完整快照。开始和返回必须匹配当前请求；结果边界只可闭合该请求刚确认的同一根动作，恰好一次，结果与完整状态一致。新根动作、新候选请求、离开房间及成功 Finish 前必须检查结果边界闭合。旧取消结果不能满足新升级结果；非法证据不会被补写成完成。

R2 把战斗胜利前回调绑定到当前房间的实际 PlayCard/EndTurn 活动动作；只有该动作确认后才可记录清理完成，且清理完成的全结构快照必须等于对应 confirmed。Travel 若承担旧房间退出，原生 exit-return 与该 Travel 的 confirmed 也必须完整相等；第一次进入房间没有旧退出，保留其正常语义。

奖励在当前房间的胜利清理完成后建立 collection，记录原生 offer 快照和候选，领取/跳过动作必须属于同一集合，不能重复领取。完成必须绑定最后一次已确认领取/跳过、使用完整确认快照，且已领取所有候选或已明确跳过剩余候选。完成事实里的金币前后值也与 offer/确认快照交叉核对。奖励生成本身可合法消耗 RNG，故不把生成后的 offer 状态强行等同到生成前的战斗清理状态。

完整状态比较复用既有结构比较，不枚举 gold 等特定字段；新增、缺失的未来字段也参与相等判定。原生业务执行中的治疗、升级、事件伤害、清场、奖励生成变化仍保留在真实执行区间，原有 4A Closed 特例没有扩张。

## 冻结与复现

- Build ID：`830880E39BB8D6F59E503B87BF798EF0ABFFF912FB821F033400650E06F33B9E`。
- NativeRun DLL SHA256：`3702EF7BEF0A7E5D59B6D949568849EA6A9F915FDA074C5CD712B4922ABC31E8`。
- Manifest：`.build/native-build/n3b-repair-final01/build-manifest.json`。
- 工作与汇总目录：`.build/runs/n3b-repair-work01/`；同构建运行目录：`n3b-repair-final01-suite/`、`n3b-repair-final01-n3a/`、`n3b-repair-final01-n2/`。
- 固定游戏 v0.111.0 / 41cef1ea，Ironclad/A0，MemorySaveStore，一 Run 一进程，无 Mod/CombatSolver 原生主路径。

```powershell
pwsh -NoProfile -File tools/native-run/run-native-n3b.ps1 -BuildManifest .build/native-build/n3b-repair-final01/build-manifest.json -OutputDirectory .build/runs/local-n3b-repair-check
```

目标目录必须尚不存在。4A/N2 使用同一 manifest，入口分别为 `run-native-n3a.ps1`、`run-native-n2.ps1`。历史坏副本可直接由新 DLL 的 `--review` 读取，无需游戏场景初始化；例如旧审查目录中的 `control-independent-stale-cancel-boundary-attached-to-upgrade`。

## 正负控和旧证据维护

开发构建 dev01 定向读取27个旧包全部符合预期（含九个原误接受案例）；dev01 的4个可空性编译警告在 dev02 之前通过条件注解修正。dev02 编译0 warning/0 error，34进程/279检查通过。随后增加“真实金币已领取但剩余奖励尚未跳过，便提前声称集合完成”负控，再冻结 final01；开发结果不合并为最终同构建证据。

最终 room 控制集合为66个副本：63个损坏、3个等价，均调用实际生产 `--mutate` 与 `--review`。保留原23个控制，新增43个，覆盖缺失/重复/错配/过期的选择边界和返回、边界提前、session错误、Travel退出返回缺失/重复、战斗回调/清理缺失重复和错误归属、奖励 offer/完成缺失重复及跨集合/提前完成。

其中20个结构负控分别在选择结果、Travel原生返回、战斗清理完成、奖励完成的快照改变金币、deck升级、RNG、添加字段或删除统一扩展字段。统一添加扩展字段的等价副本继续合法，通过完整结构契约区分一致扩展与局部漂移。

旧独立审查的306个原封装包由最终新检查器读取，全部符合预期：九个原误接受案例在对应语义门拒绝，非通用 hash/seal 错误。包含163个历史包，保留原E261（151）与30C9（12）身份；其余143个包保留AF641身份。这是新读取器的兼容/拒绝证据，不是历史游戏轨迹在新构建重新执行。见 `review-regression/summary.json`；1224个源证据文件复制前后hash不变。

## 完成条件及下一步

| 最终范围 | 实际结果 |
| --- | --- |
| 4B原生矩阵 | 34进程、281检查、0失败 |
| 4A原生矩阵 | 16进程、273检查、0失败，80损坏/8等价控制保留 |
| N2及步骤1–3 | 74进程、580检查、0失败，原自然skip24/take25与全部生产负控保留 |
| 同构建总计 | 124原生进程、1134 suite检查；raw exit为80×0、39×1、2×2、3×3，逐一核对预期及process/result/原始退出，未用外层0覆盖非零 |
| Core | 实际运行98项通过，所加载Core DLL与冻结artifact一致 |
| 补充检查 | 582项通过，含源码/输入/产物hash、来源、原始退出、重放/观察对照、实例与失败前缀 |
| 旧独立审查 | 新检查器306次读取全部符合预期，九项修复均命中语义门 |

DEV_01实际保持71/71、7房间、3次原生胜利、Rest指定永久实例升级及下一战Play；DEV_02保持138/138真实Death；DEV_03保持WhisperingHollow Unsupported、77/78未确认Travel前缀。三个种子的route、choices、final-state与修复前AF641原始文件分别全hash一致。DEV_01/02共24个对应静止边界完整状态一致，重放及minimal/off对照完整RNG无差异。旧证据没有覆盖或重新标记。

42 source、25 compiled、3原版inputs、7 artifacts与新manifest全部hash一致，Build ID独立重算一致；15 Mod inputs与52兼容源码固定。新构建0 warning/0 error。构建期间曾遇执行服务断连，随后从原会话确认原进程成功退出，未重复启动构建。

最终原生矩阵、负控、自然路径、RNG/重放、24个正常静止边界和冻结源/输入核对以本目录的最终 `summary.json`、`inspection.json`、`closeout.json` 为准。后续独立验收通过后，按 [4C交接](NATIVE_RUN_N3C_IMPLEMENTATION_HANDOFF.md) 从本次新冻结接续；该交接里AF641是修复前历史基线。

本轮仍只验收4B。DEV_02死亡与DEV_03未知事件Unsupported继续作为真实结果，不换种子或绕过未知事件。4C–4D、步骤5–6、正常开局完整爬塔均未在本轮实现；公开Mod API仍后置。未改变角色、安装、真实存档，未启动游戏UI、真实Steam/游戏网络，未提交推送。
