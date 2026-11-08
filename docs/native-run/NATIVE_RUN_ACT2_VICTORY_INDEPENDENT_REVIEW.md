# 第二幕胜利与再次跨幕：独立验收

日期：2026-11-03。结论：**当前第二幕范围通过，未发现必修项；完整 M1 未完成。**

核验冻结：`D59D778F3360FBBFF7B65A419725D321F20A7B541FBC2E91CDAF45FDCFC45910`。依据实际源码、构建清单、原始轨迹、退出记录和生产检查器重新执行；未以实现报告的完成声明代替证据。本轮仅增加独立审查文档与隔离证据，没有修改生产实现、重跑原生游戏矩阵或提交推送。

## 实际流程

| 路径 | 独立核实的实际边界 |
| --- | --- |
| DEV03 主路径 | 正常 Neow 开局，同一 run 的 a365 原生 KAISER_CRAB_BOSS 胜利，玩家 HP12；a366 原生领取 100 金币，411→511，并取得 IMPERVIOUS 新实例 i777，升级0、入组楼层33；a367 等待真实 EnterNextAct 任务完成，到 act=2 的第三幕 MapRoom，HP18，node=null。367/367动作，SliceBoundary/Ongoing。 |
| DEV03 独立精英路径 | 正常开局的另一条合法地图路径，a317 原生 INFESTED_PRISMS_ELITE 胜利，HP32；a318 原生领取41金币及 MealTicket 新实例 i689，最终HP38、金币397。318/318动作。未与主路径拼接。 |
| 失败与回归 | 最终固定矩阵仍保留 DEV01 Death93、DEV02 Death156及重放。此前策略开发的 Death199、Death357，以及 Crusher 空音频异常355派发/354确认全部保留。异常终态是 Unknown/EngineError，最后确认 PlayCard:354，没有推测胜利或存活终态。 |

核查 `Program.cs` 的 CanPlayTargeting/TryManualPlay、`NativeMajor.cs` 的真实地图边与 RewardsCmd.OfferForRoomEnd、`NativeRewards.cs` 的原生奖励同步器，以及 `NativeNaturalBoss.cs:96` 的准备投票和真实异步任务等待。当前策略只从合法动作、原生奖励候选和 Skip 中作选择。自然路径没有 MajorFixture、Debug 跳房或资源设置。

最终15个原生执行均绑定该冻结，实际为4次 Death、11次 SliceBoundary；这些是本轮实现已有执行，本次审查没有重新启动。主路径 live/replay/minimal/off 和另一份 live 的734个派发/确认完整投影一致；精英 live/replay 的636个完整投影一致。另核对两条死亡重放。比较包括全部记录的 run/player RNG 的 counter、s0–s3，以及动作、子选择、奖励选择和最终状态。

## 生产检查器与来源

证据根目录：`.build/runs/act2-victory-independent-review01/`。

- `reread-summary.json`：重新读取532份既有证据，全部符合预期。其中389份原始执行记录、143份既有控制；原始执行的失败、未完成和退出语义保留。2599项来源/指纹核对无失败。
- `controls02/summary.json`：独立新增23份副本，20份损坏证据被生产检查器拒绝，3份正常/等价证据通过。副本重新封签后检验，拒绝原因来自语义门而非仅哈希失配。
- 新控制覆盖当前胜利玩家/敌人事实、旧 root/context 胜利条件借用、战斗位置、跨幕任务缺失/重复、旧 MapRoom 实例、跨幕返回共同篡改金币与完整 RNG、领奖和 ActReady 顺序、实际卡实例/升级/金币、精英遗物与奖励动作归属、精英完成状态、Crusher 音频上下文/接收者、策略版本及幕作用域。合法重复的原生胜利条件查询、实际奖励和跨幕变化均通过。
- 合计555次生产检查器调用，全部符合预期。`facts-summary02.json` 另有202项实际流程/隔离/重放核对，无失败。检查次数表示证据规模，不表示独立胜率样本。
- 92项实际 dirty 源码、56项编译输入、3项游戏输入、7项产物及92份源码快照均匹配。当前 dotnet SDK 为清单中的10.0.201。15项锁定 Mod 输入与52项兼容源码保持。原始证据与生产源文件的前后哈希未变化。

第一次独立负控构造因原生已经移除死敌、数组为空，在调用检查器之前停止；该副本留在 `controls/`。随后从同一动作的实际派发敌人构造明确“仍有活着的主要敌人”的坏副本，输出到新的 `controls02/`。初次事实脚本将开发异常终态预期写为 Ongoing；读取原始记录后纠正为实际的 Unknown，保留初次报告并输出 `facts-summary02.json`。两项均为审查夹具问题，没有修改或覆盖原始执行结果。

## Rocket / Crusher 审计与隔离

检查 `NativeBossPresentation.cs`：先审计两个具体 BeforeDeath 方法，分别只替换唯一 PlayOneShot(string,float) 调用；原生 IsOverOrEnding 和其他回调流程保留。锁定原生音频方法在 TestMode 内本就不执行声音业务。空接收者仅允许在指定无UI场景处理，具体来源写入轨迹。

本次分别在独立进程运行现有实际 Harmony 注入夹具，证据为 `rocket-audit.json` 和 `crusher-audit.json`，两次均 exit0：外来 prefix 真实织入后，Install 拒绝；移除后生产适配安装成功；晚加外来 prefix 后 Ensure 拒绝；移除后回到预期的未初始化宿主隔离门。两个目标业务调用次数均为0。该证据是审计 API 检验，不是战斗效果测试。

主路径实际业务轨迹另记录 a355 Crusher 与 a365 Rocket 各一次音频守卫，并继续完成原生伤害历史、胜利回调与清理。未以替换整个死亡回调实现成功。隔离记录保持 MemorySaveStore、shouldSave=false、Steam未初始化、无UI，SaveProgress/replay调用受现有拦截；没有新的真实存档、Steam或网络执行。本次没有新增宿主权限或批准绕行。

## 后续范围

继续按 [第三幕交接](NATIVE_RUN_ACT3_IMPLEMENTATION_HANDOFF.md) 推进：第三幕实际 Ancient 与首战、自然房间能力组合、后续 Boss 和同run Architect/RunVictory。商店/嵌套事件能力已有局部证据，但自然控制器中的组合缺口仍须补足。不得用尾部夹具或当前 MapRoom 代替完整通关。

本次是 native-only 执行与可信轨迹验收，没有新增仿真差分认证。完整状态比较指声明的 JSON 投影，尚未包括所有原生内部状态，例如 Surrounded.Facing；该项进入最终必要数据盘点与不扰动验证。该边界不否定本次真实胜利及重放，但不能据此关闭完整 M1 数据门。Oracle 策略开发样本不代表公平信息胜率或留出集表现。

最终 M1 仍需完整正常开局 RunVictory、完整重放、必要数据验收及同一最终构建的全量原生/共享回归与既定批测。当前无执行环境阻塞，无需重新请用户逐步批准。
