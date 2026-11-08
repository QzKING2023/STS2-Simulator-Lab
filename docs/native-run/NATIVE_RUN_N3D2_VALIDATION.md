# 4D2 原生事件与事件战斗验证

状态：**实现及最终同构建统一验证通过，待独立验收。** 汇总在 `.build/runs/n3d2-work01/closeout.json`。4D3 Elite/Boss 与 M1 步骤5–6仍待后续执行。

固定游戏 v0.111.0 /41cef1ea；最终冻结 `12F0036561A3AF6AA801BB2D894EE5B3FFBED04D4246E8DECC1DFB54CD545004`，Native DLL SHA256 `65F71CE91ABBE29B24E0C4EC23A51F535EB943982B6CAA05A1B37D53A109F99F`。构建位置 `.build/native-build/n3d2-final04/`，72项源输入、46项编译输入、3项游戏输入、7项产物。实际编译退出0，0 warning/0 error。当前 HEAD 保持 d73039d688eb54c9513f5b9508a03947e689aa18，真正 dirty 源码和输出另有哈希/副本，未用 HEAD 冒充当前代码版本。

## 当前真实能力

| 场景 | 实际执行与范围 |
| --- | --- |
| event-natural，固定 DEV_03 | 正常 Neow，同一 Run 延续旧77确认动作；真实进入 WhisperingHollow，原生付费领取两瓶药水，合法下一节点至新战 Play，80/80根动作。Gold分支费用从原生RNG独立核对；不是跳过原 Unknown 事件 |
| event-combat，固定 DEV_03 | 正常开局、真实沿途三战与攻击卡奖励、真实 Hollow 药水；随后**明确 API 夹具**进入 BattlewornDummy V1。真实选项压入子战斗，实际击杀、原生战后奖励入口、生产 Proceed、同一父事件的真实 Resume Task、其原生额外药水奖励，最后合法地图离开到下一战 Play，62/62根动作 |
| event-resume-fault | 原生 Resume 业务完成后，在观察包装任务中显式注入错误；保留60确认/61派发、已发生奖励和未确认 Proceed，EngineError/exit1，不能称为游戏缺陷复现 |
| event-resume-budget | 原生业务结束后受控挂起观察包装任务，以50ms边界等待验证 BudgetExceeded/exit3；保留60/61和真实副作用。它验证完成门，不证明此次游戏业务自然发生晚到 |

正常场景不是整局终态，均 Ongoing/SliceBoundary/native-only/NotCompared。fixture 的前段策略为可见攻击优先、Bash优先、原生奖励首个攻击卡（无攻击则首个合法卡），不修改 HP/能量/敌人/牌组；得到的 InfernalBlade、Anger、PerfectedStrike及药水由实际原生执行取得。其策略不同于旧自然前缀，不能拿 fixture 前段冒称旧77动作；只有 event-natural 的原77动作逐项完整比较。

新增支持的 Hollow 仅 Gold；Hug 的变牌/失血未覆盖，Dummy Setting2/3未覆盖，在候选协议中明确不支持。未引入对应卡牌效果镜像或 CombatSolver 依赖。

## 真实调用与数据契约

实现位置：`NativeEventFlow.cs`、`NativeEventFlowContract.cs`，共有7个既有 native-run 文件必要接入修改、4个新增项目源文件；Core及52个旧兼容源码不变。完整输入/返回/状态契约见 [生命周期契约](NATIVE_RUN_N3D2_LIFECYCLE_CONTRACT.md)。

WhisperingHollow 没有事件战斗；已读固定DLL实际实现，其两分支是金币换药水与变牌失血。实际 Resume override 在固定 Events 命名空间中仅 BattlewornDummy 一项，元数据检查保存在 work01/resume-types.json。本轮没有因此开放所有未知事件入口。

原生 EnterCombatWithoutExitingEvent 的同步器压栈；战后 ProceedFromTerminalRewardsScreen 经原生子房间 Exit/EventRoom.Resume，后者通过 ResumeEvents/TaskHelper 启动 EventModel.Resume。外层 Task 完成不代表实际 Override 完成。本轮包裹并等待原生真实 Task，分别记其开始、完成、异常与外层返回，禁止提前确认。

状态检查保留原生退出后的玩家 turn/energy/phase/piles，仅允许当前 CombatManager 的 round/side/enemies按真实退出清理。父事件快照、实际新旧房间实例、父子时序及各静止点全结构绑定，未知嵌套字段同样参与。实际奖励、事件完成和房间转换按业务区间处理；新战完整永久牌组与全部战斗 DeckVersion/模型/升级关联。所有 ef-* 记录须属于注册事件场景，不能插入旧奖励场景冒充覆盖。

Hollow费用独立从其一次已推进的xoshiro状态反推上一输出，再按固定原版有界整数算法计算，精确核对金币，而不是从被测费用重复推导期望。药水取得由统一原生奖励会话/槽位与实例结果验证；claimed、处理完成与实际取得保持原有区别。

## 最终验收与复现

最终正式新矩阵为10次原生进程、197检查、106个生产 reader 损坏/等价控制；另有独立跨场景插入控制与实际 Harmony Resume 方法事先/事后未知patch拒绝探针。观察对照及重放比较完整 route、choices、reward-choices和final-state，含完整记录RNG。off/minimal仍保留必需的身份和边界投影；对照不证明完全移除所有观测后的运行等价。

全部适用旧1–4D矩阵以同一最终产物重跑。9组原生矩阵合计 **290次原生进程/3540套件检查，另有实际 Core98**，各组汇总及子退出均核对。原始退出为214×0、68×1、4×2、4×3，含预期故障、Unsupported和预算停止，没有把外层成功当作全部自然完成。

552项来源核对与40项补充核对全通过；历史1026份控制由最终reader实际重读，4020个历史原文件保持哈希。15锁定输入/52兼容源码不变。新增106控制为102损坏拒绝/4等价接受；另一个跨场景伪记录反例在旧候选被接受，在最终生产reader中由场景语义门明确拒绝。Resume方法实际Harmony探针验证事先/后加未知补丁两项拒绝，目标游戏业务调用为0，仅是审计API证据。

```powershell
python -P tools/native-run/run-native-events.py --manifest .build/native-build/n3d2-final04/build-manifest.json --output .build/runs/n3d2-reproduce-01
```

复现需全新输出目录和同冻结源/输入；不能覆盖旧证据。全回归、来源和历史 reader 入口在 work01 的 run-regressions-final04.py、inspect-final04.py、recheck-history-final04.py。

## 保留的失败与限制

work01 保留初始牌组和初次策略的原生三回合超时、错误药水目标、重放答复未装载、原生清场投影误假设、切换scope错误归属和跨场景伪记录等中间证据。修复后使用新的不可变构建/输出目录复跑，没有替换seed隐藏失败。部分已开始的旧冻结矩阵用工具正常Ctrl-C取消，保留未完成文件，不计入最终通过项；旧原始历史不删除。

只有原版无UI执行，不宣称整角色/全事件/平衡/胜率、零售演出或retail RNG等价。没有新增Mod适配；ShouldAddToDeck健康卡拒收Hook在本固定原版仍不适用，SpoilsMap仍API夹具而非自然跨幕。真实死亡覆盖继续由适用旧自然死亡基准回归，本轮没有从不会攻击的木偶制造新死亡证据。

后续 [Boss与步骤5交接](NATIVE_RUN_N3D3_IMPLEMENTATION_HANDOFF.md) 已准备。当前仍用有限场景控制器，完整Run控制器、精英/Boss、幕转换、最终自然胜利和预登记批测没有因本轮夹具通过而完成。每Run独立进程、MemorySaveStore、保存/replay/Telemetry隔离及严格补丁审计保持；未改角色、安装、真实存档，未启动游戏UI/真实Steam网络，未提交推送。
