# 4D2 事件与嵌套战斗：实施交接

奖励闭环已通过 [最终独立验收](NATIVE_RUN_REWARD_CLOSURE_FINAL_REVIEW.md)。基线冻结 `AFB0E42BE7173FFDF371CE7E0DDBEC605430151608F171177F9436C12C289A52`；本文件细化原 [4D 交接](NATIVE_RUN_N3D_IMPLEMENTATION_HANDOFF.md)，不增加公开 SDK 或 Mod 工作。用户已授权完整 M1，实施者可连续推进，只有新增权限或实质范围决定才暂停依赖项。

## 先做 4D2A：关闭现有自然事件停点

当前 `NativeRooms.cs:177` 的 `EnterAuditedEvent` 只允许三个已审查简单事件且要求继承无操作 OnRoomEnter。DEV_03 是实际入房后在此停止，77 确认/78 派发、未确认 Travel；这不是“WhisperingHollow 原生已证明无法执行”。

1. 从固定 SHA 的游戏程序集核验 WhisperingHollow 的具体入口、选项生成、layout 责任与任务完成条件，记录确切方法/版本和安全依赖。不要仅删除白名单或调用所有未知 OnRoomEnter。
2. 增加版本化的自然 profile，保留旧 rooms-natural 及其原始 Unsupported 结果。固定 DEV_03、档案和前段策略，核对到旧最后确认边界的动作/选择/完整状态及 RNG；之后合法处理该事件、完成原生离房并到下一真实决策。
3. 原生展示若无业务责任，可做窄适配；布局若负责生成状态/选项，须执行对应原生语义，不能只置 IsFinished。选项 task 异常依然进入 NativeHost.Faults 并保存实际派发前缀。
4. WhisperingHollow 的后续行为以实际实现为准。不要预设它必有“战斗后恢复”分支；若它不提供该机制，4D2B 另选固定 SDK 中确实具备该路径的事件。

## 再做 4D2B：同 Run 的事件战斗与恢复

已读本机参考的可复用链：RunManager.EnterRoomWithoutExitingCurrentRoom 将子房间压栈；终端战斗奖励后的 ProceedFromTerminalRewardsScreen 在符合 ShouldResumeParentEventAfterCombat 时进入 ResumePreviousRoom；其退出子房间后调用同一个父 EventRoom.Resume。EventRoom.Resume 经 EventSynchronizer.ResumeEvents 调用实际 EventModel.Resume。

这些入口的本机证据在 `.build/native-reference/RunManager.cs`（1278、1419、1445）、`EventRoom.cs`（106）及 `EventSynchronizer.cs`（339）。参考只作定位，新增实现前仍须与锁定 DLL/MVID/方法核验；不把反编译源码发布进项目。

**关键依赖：EventRoom.Resume 返回 Task.CompletedTask，但 ResumeEvents 通过 TaskHelper.RunSafely 启动实际 EventModel.Resume。** 不能把外层 Task 完成或 pending-option 队列空当作恢复完成。现有 NativeObservation 只直接跟踪 EventOption.Chosen，下一步需审计并关联真实 Resume Task 的开始、完成、异常，保持原 Task 语义。错误不能被 fire-and-forget 或 UI 缺席吞掉。

实施顺序：

1. 选择本版本可核验的事件→子战斗→原生奖励→原事件继续路径，冻结具体分支及动作策略。可先用声明的原生 API 房间夹具覆盖机制，另外保留自然事件片段；不把夹具拼接成自然流程。
2. 让房间控制器区分父 EventRoom、子 CombatRoom、恢复中的事件和可离开事件。稳定记录两个 room 实例、stack 深度、parentEvent、combat 实例和对应任务，不通过清空 session/计数器模拟恢复。
3. 根据原生 option task 的实际结束时机组织动作：若选项先完成并交出战斗，确认它后再派发战斗根动作；若原生父任务仍待完成，设计内部有界调度与明确父子执行关系，禁止并行平级根或提前标确认。必要共享机制修复在本范围内，重大宿主权限变化另报。
4. 真实执行战斗到 Victory 或 Death。胜利后用统一原生奖励会话领取/合法跳过，随后调用实际生产收尾入口一次；死亡不得领奖/恢复成正常继续。不要直接 PopRoom 或手写 Resume 结果。
5. 等待真正的 Resume 任务和后续原生选项达到可行动/完成边界，完成事件后再合法 Travel。奖励对象、实际新卡/遗物/药水和 HP/金币持续关联。

## 同步定义数据与验收门

| 门 | 正例 | 必需失败控制 |
| --- | --- | --- |
| 来源/自然前缀 | DEV_03 旧确认前缀一致，新 profile 合法继续；API 夹具独立标签 | fixture 冒充 natural、途中换 seed/档案、旧动作重放到新房间 |
| 事件任务 | 实际 option/Resume 任务、子选择与原事件实例关联且完成唯一 | 缺任务结束、重复/早结束、异步异常、旧请求/缺失/多余答复 |
| 房间栈 | 原父房间→真实子战斗→同一父房间恢复，深度和实例对应 | 指向另一事件、重复恢复、未结束子战斗即离开、错层奖励 |
| 奖励收尾 | 实际战斗胜利、原生奖励唯一、收尾一次后恢复 | 未胜利领奖、重复奖励/收尾、拒收伪取得、未完成会话继续 |
| 静止状态 | 请求/派发/子开始、原生返回/确认按实际调用链绑定不可变完整快照 | 共同篡改后恢复、嵌套 RNG/新增字段、父子错归属/顺序；真实业务变化保持合法 |
| 失败前缀 | 预算/非法输入/实际任务异常保留已确认前缀及未确认动作 | 外层 exit0 掩盖内层失败、故障后补写完成或重新执行 |
| 复现/不扰动 | 同 seed 独立进程重复、冻结路线、detailed/minimal 完整状态与记录 RNG 对照 | 缺数据、观察改变业务/RNG、使用旧构建结果混入 |

先冻结有限策略、选项数/动作数/每任务及总时间预算，沿用原 45 秒动作/600 秒房间 profile 作为起点；需要调整则版本化并保留旧预算结果。策略真实死亡可以是正确结果，但事件恢复能力必须另有真实胜利路径证据，不能手动改 HP/资源强求成功。

共享代码修复后冻结实际 dirty 源码/产物，在同最终构建回归适用 1–4D 套件、Core98、奖励171控制及本轮新增独立反例/等价包；保持原自然 skip/take、DEV_01/02/03 旧 profile、商店路径、失败前缀及 15 输入/52兼容来源。新增审计方法需验证已有/后加未知 Harmony patch 拒绝，审计 API 夹具和游戏业务分别记录。

## 4D3 与后续交接

4D2 独审通过后，按原主计划实际执行 Elite 和第一幕 Boss，先声明原生房间夹具与固定策略，再接自然节点。Boss Victory、原生奖励、act-ready 各自观测，最终 Boss 无普通奖励可合法，不套用“必有正金币”的假设。沿已验证原生 ActChangeSynchronizer/EnterNextAct 进入步骤5，取得正常开局同 Run 的跨幕和最终 RunVictory；4A Architect 夹具只提供尾部基准。

步骤6再做预登记样本及可信数据交付，保留失败分母，分开执行可靠性和策略表现。特殊拾取、多取卡、卡/遗物补货、更多 Hook 和连续 session 按实际可达内容列门，不能静默跳过。当前 ShouldAddToDeck 健康卡 Hook 拒收在固定原版不适用，SpoilsMap 自然跨幕尚待后续，保持准确范围。

只在 Lab 写入，独立进程/MemorySaveStore、保存与 replay 拦截、无 UI/真实 Steam/网络继续。角色/安装/真实存档不改，不提交推送，不重置已有 dirty 工作；串行构建。此交接尚未实施新事件或 Boss 代码。
