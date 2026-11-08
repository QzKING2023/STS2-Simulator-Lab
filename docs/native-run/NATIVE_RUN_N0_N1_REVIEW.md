# M1-NativeRun：N0／N1 独立验收

日期：2026-11-02。审查对象为 [N0／N1 实现报告](NATIVE_RUN_N0_N1_VALIDATION.md) 声明的原版首个纵向切片。

**结论：该限定范围通过，未发现必须先修复才能继续 N2 的阻断项。** 这表示独立原版宿主实际完成了正常开局、首战 Victory、原生奖励和下一战 Play 边界，不表示新 M1 已完成。N1-T 尾部探针、完整选择／数据门、所有房间、跨幕和完整 run 仍待实施。下一轮可直接按 [N2 实施交接](NATIVE_RUN_N2_IMPLEMENTATION_HANDOFF.md) 继续，六步总体授权已经生效。

## 1. 审查方法与冻结身份

检查实际 Core 协议、原生驱动、host、构建／运行／证据脚本，以及固定程序集的有关选择、保存、战斗结束和历史入口。没有修改运行实现、旧证据或已冻结输入。

- 构建：`.build/native-build/n0-n1-final-03/build-manifest.json`。
- Build ID：`913DEAA5D674366A36B2256E9658F46380B4F9C66C43EE9BD8707FDE9FEEAFD6`。
- NativeRun DLL：`A921016C57E77FC6056CCB06549B74408D7D415AE68324CF44BCA0A8F2A43769`。
- Core DLL：`32B54F0DFA32AD3A3607F16B666B869E41A6C17C3AB44035E0A12B1B7B622DF3`。
- 游戏固定 v0.111.0／41cef1ea，输入 hash 与 manifest 一致。
- 独立审查产物：`.build/runs/native-n0-n1-independent-review01/`；`checks.json` 中 24 项复核全部通过。这是审查检查数，不是新增 24 个游戏场景。

重新计算 manifest 标识，逐项核验 23 个 source、3 个 input、9 个 compiled、7 个 artifact，零不匹配。六个既有子进程的 manifest／process／result 绑定一致，实际退出与报告一致。

对原始 natural／replay 的生产检查器复跑使用新目录中的副本，原文件没有重写；两次均为 22 项通过。已有 Core 检查程序重跑 98 项通过，没有重新构建；DLL hash 对应原 `core-check-binding.json`。额外执行一次现有原生重放入口的字段负控：首动作加入未知字段，实际得到 `InvalidAction`／exit 1／0 dispatched／0 confirmed。证据为审查目录下 `extra-field-replay/`。

## 2. 实际流程与数据核对

| 验收项 | 独立核对与结论 |
| --- | --- |
| 无 Mod／无 solver 前置 | 独立项目只引用原版依赖和 Core；运行解析器拒绝指定 Mod 依赖；实际 loadedAssemblies 不含 MyMod、BaseLib、Ritsu 或 CombatSolver。未改旧差分协议来制造 native-only 成功 |
| 正常开局 | 原生新 Ironclad／A0／Standard、同一实验 Progress 生成 UnlockState；80 HP／99 gold／5 Strike＋4 Defend＋1 Bash／Burning Blood 符合独立起始契约；原生进入 Neow 并选择实际选项 |
| 同 run 首战 | 轨迹从 Neow `(3,0)` 到真实 child `(1,1)` 的 Nibbits Weak；14 次 PlayCard、4 次 EndTurn，实际敌方阶段发生；原生 `EndCombatInternal(CombatTurnState)` 入口观测先于清理 |
| Victory／清理 | events seq 165 清理前 HP64；seq 167 清理后 HP70，原生 Burning Blood 效果和历史清空可见；不是把敌人列表清空或模拟预测当作胜利 |
| 原生奖励 | GoldReward 实际 Amount17，原生同步器领取后 gold249→266；skip 指剩余可选奖励，包含 PotionReward／CardReward，并非“领取金币后只跳卡” |
| 下一节点 | 调用生产地图 Hook，选择当前节点真实 child `(0,2)`，原生 EnterMapCoord，seq182 到下一战 Play；HP70／gold266／永久牌组和遗物延续 |
| 动作计数 | 24 个根动作＝EnterAct1＋EventOption1＋Travel2＋PlayCard14＋EndTurn4＋TakeGold1＋SkipRewards1；逐个 route 与 dispatch／confirmed 一一对应 |
| 同构建重放 | natural 与 replay 的 route.json、events.json、profile.json **逐字节相同**；各 182 个事件，其中 105 个即时 CombatHistory 观察；每步前后投影包含 run/player RNG 状态 |
| 结果分层 | 原生 Ongoing／SliceBoundary，Comparison=NotCompared；没有给模拟器捏造终态，也没有宣称完整 run Victory |

主要证据：`.build/runs/native-n0-n1-final03/natural/`、`replay/`；本轮重算见审查目录 `checks.json`。这次重放属于“重新运行固定简单策略并核对冻结动作／边界”，不是接受任意动作计划的通用执行器，也不是任意中间快照恢复。

## 3. 失败、隔离与旧回归

| 项目 | 证据与准确范围 |
| --- | --- |
| 异步异常 | 实际 TaskHelper fault signal 注入；EngineError／Unknown／exit1，0派发，fault1、遥测拦截1。该控制验证故障门，尚未覆盖每一种真实事件内部任务 |
| 旧票据 | 真实 Neow 的两个已确认根动作之后，对 production ticket 注入错误 sequence；InvalidAction／exit1，计数保留2／2。这不是跨房间外部请求全链路证明 |
| 预算 | 显式 TimeoutException 注入，BudgetExceeded／exit3／0派发；不是实际运行到墙钟耗尽或 watchdog 触发 |
| 未支持子选择 | Bundle 调用在 provider 未实现时明确 Unsupported／exit2；0派发，没有用 TestMode 默认首项冒充选择支持 |
| JSON 字段 | Core 检查格式变化可接受、额外字段不相等；本轮再用实际 replay 入口证明额外字段在派发前拒绝 |
| 证据负控 | 已有 empty-route 与 wrong-exit 对实际检查器均 exit1；本轮保留原负控，未覆盖写回 |
| 保存／网络／UI | 实际 TestMode=true、Steam=false、shouldSave=false、DailyTime=null、MemorySaveStore、NetSingleplayerGameService、NGame.Instance=null；SaveProgress5／WriteReplay1拦截。静态核对 SaveManager 附带 progress 调用、RunSaveManager shouldSave 守卫及其第二重载使用同一内存后端 |
| 原版旧回归 | 旧兼容 harness 的 battle×2、RNG×2 原始退出0；独立构建身份，不计入新 native-run 的同构建任务数 |
| Eleanor 旧回归 | 进一步检查 reverse 实际报告：16 steps／16 dispatches／native和sim Victory；122检查0失败且报告hash一致。顶层 compat-summary 的 cases 为空是旧脚本该分支不填数组；通过结论依据实际报告和检查器，不依赖该空数组 |
| 输入保留 | 15 个 v0.4.1 输入重新核验，零不匹配。旧 manifest 中共享 Core 测试输出的两项已在上轮重建，其余旧 harness 产物未变；不声称所有旧 live 输出均冻结 |

隔离结论限已审计路径和配置：不是系统级文件／网络拦截证明。全量 Godot 静态构造抑制、纹理／preload／hover seam 在这条切片中有实际依据，不能自动推广到新内容。新房间仍需逐项审查。Steam／网络没有真实连接测试；其初始化关闭，单人服务和遥测拦截是当前防线。

## 4. 交给 N2 的非阻断缺口

1. 现在 `Program.cs:42` 反射读取 history 属性，复杂对象只写类型名；`DamageResult`、CardPlay、MoveState 等缺少可查询字段。105条记录证明及时采集发生，不证明完整效果数据。部分原生描述文本自身也可能误导，应使用类型化事实而非解析描述。
2. `Program.cs:53` 的实例编号按观测首次遇到顺序产生，`State()` 会分配编号；详细观察开关可能改变编号顺序。N2 必须先把身份分配与详细日志分离，再做不扰动对照。
3. 目前没有根 action／嵌套 choice／异步 task 的完整关联，只有边界序号；事件日志每次重写整个数组。N2 增加追加记录、失败前缀、缺数据检查和独立汇总。
4. 当前 root ticket 在驱动内部即时生成消费；Core 原语正确，但外部选择生命周期、跨请求／跨房间旧票据仍未覆盖。N2 必须在实际 broker 消费路径验证。
5. `NativeHost.Ensure` 对 Steam、TestMode、shouldSave、内存后端主动断言；网络服务类型和 UI 单例目前仅记录。N2 可把本次已成立的两项条件纳入运行守卫；不能把当前记录表述为已有主动拒绝测试。
6. run/player RNG 投影已重现；事件模型／同步器流及完整 powers／卡牌状态仍未全部盘点。未验证 observer 开关不扰动、真实预算耗尽／取消和新原生死亡。相应门保留，不要求 N0/N1 先实现全部后续功能。

本轮仅新增本审查与 N2 交接文档及隔离审查产物；没有运行实现修改、提交、推送、游戏 UI、角色／安装／用户存档改动。原始 N0/N1 报告保持历史原貌，以上结论为其后续独立验收记录。
