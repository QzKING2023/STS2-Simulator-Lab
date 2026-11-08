# 4D2 生产数据契约修复

本轮按 [独立审查](NATIVE_RUN_N3D2_REVIEW.md) 修复 R1–R4。完成状态及最终计数以 `.build/runs/n3d2-repair-work01/closeout.json` 为准；独立复审尚需另行完成。原审查、旧冻结和所有反例保留。

最终候选冻结：`C4B85E2FDDADA7418E49A1A73A6F91256195707415128D3038461B08CF1A563C`，位于 `.build/native-build/n3d2-repair-final02/`。编译 exit0、0 warning/0 error。源码快照保存于 work01/source-snapshot；当前 dirty 源码按文件哈希记录，不以 HEAD 代替源码版本。

## 最终同构建结果

实现与统一回归通过，**待独立复审**。Native DLL SHA256 `98A95310C940354C369E610F50A20F5B0F60A6C2340CBE187D14334B91A5DAC5`。72 个源输入、46 个编译输入、3 个游戏输入、7 个产物与源码快照对应；相对旧冻结仅 3 个项目源码变化，69 个保持相同。

- 原 16 个反例修改前全部复现误接受，最终 reader 全部按语义门拒绝；10 个正常/等价历史副本全部接受。拒绝不依赖坏 seal 或 dangling identity。
- 新事件套件实际执行 10 个原生进程、243 项检查，152 个生产控制全部符合预期：147 个损坏副本拒绝、5 个合法/等价副本接受，包含允许外层返回早于实际任务完成的契约控制。
- 最终产物完整执行 9 组共 290 个原生进程/3586 项套件检查，另实跑 Core98。各组为 closure 35/419、transactions 18/471、economy 23/361、merchant 25/371、suite 55/561、n3b 34/307、n3a 16/273、n2 74/580、events 10/243（进程/检查）。
- 逐进程退出分布保持 214×0、68×1、4×2、4×3。预期错误、非法动作、不支持、预算停止分别保留；外层 suite exit0 没有把这些记录改成游戏成功。
- 537 项来源/退出/重放核对通过；1026 个历史副本由最终 reader 实际重读，4020 个历史原文件哈希保持。15 个锁定输入、52 个兼容源码不变，Mod runtime 本轮未重跑。
- 自然 80/80 和事件战斗夹具 62/62 的 live/replay/minimal/off 完整 sidecar 比较一致，包括记录 RNG；与旧冻结的 route、choices、reward-choices、final-state 另作 8 项完整 JSON 比较也一致。故障及预算仍保留 60/61 前缀。

完整输出见 work01 的 `closeout.json`、`facts.json`、`final-repair-gate.json`、`execution-continuity.json`、`historical-reader-final02.json`。Core 实际输出保留在 `core-final01.log`（沿用编排中的日志文件名，内容为本轮最终产物实际运行），对应测试与 Core 产物哈希见 `preserved-inputs.json`。没有挂起命令或审批阻塞。

## 生产修复及边界

只修改三个原有项目文件：`NativeEventFlowContract.cs`、`NativeRewardContract.cs`、`event_flow_controls.py`。原生执行器、角色/牌效、输入和宿主安全补丁没有改动。

| 契约 | 生产约束与状态依据 |
| --- | --- |
| R1 真实 Resume | 奖励检查器要求具体任务在当前 EventProceed 的已开始父操作内启动，启动前无子奖励；该任务的奖励子集合必须属于此父操作。完成时栈上只剩该父操作，必需唯一子集合已返回，完整状态等于最后子返回。父返回还要求真实 Resume 完成 |
| R1 外层返回 | 必须晚于实际 Resume start；允许早于真实任务完成。真实完成后的外层返回按完整状态绑定，不把 IsFinished=true 当任务完成 |
| R2 根选择 | 当前请求、session、index、key、具体 option 实例、enabled/supported 与 dispatch 完整状态绑定；实际 start 必须执行同一个根决定。请求不重复，旧请求不能为新动作收尾 |
| R2 结果 | 每条返回自己的 root/context/request/session 均核对；返回→确认→boundary 唯一有序。当前有限事件中 success 应为 null，eventFinished 按实际事件状态核对；Dummy Setting1 在子战斗启动时父事件仍未完成 |
| R3 胜利/清理 | 胜利回调属于当前具体 CombatRoom 及触发根动作且唯一；确认之后必须有清理记录，绑定不可变完整确认状态、动作、房间和空历史。后续根动作和事件完成均要求清理收尾 |
| R3 到达 | Travel/EventSetup 确认产生一个待记录到达，绑定动作及实际新房间；room-entered 消费它且唯一。换一个 session 不能重复同次到达；下一个动作及最终目标都不能漏掉到达 |
| R4 战后奖励 | 当前 EventRewards 必须在本次子战斗清理后；必须包含一个实际原生 reward-set，空集合也须记录。集合返回与包装返回完整状态相同，确认和 boundary 收尾后才能 Proceed |
| 相邻归属 | Proceed 根决定中的父/子房间绑定真实父子对象，EventSetup 的模型绑定固定夹具模型，exit-return 自身的 root/context 必须属于当前退出动作 |

完整结构比较包含未知新增字段及嵌套 RNG。真实业务区间继续允许原生变化：例如清理前 HP71→清理后77、原生奖励改变药水库存；静止观察仅与其实际确认/业务返回锚点比较，没有把清理前后强制设为相同状态。

## 复现与控制

1. 修改前以旧冻结实际重读 16 个反例，16/16 仍误接受，保存在 `before-reproduction.json` 及 before-* 副本。
2. 修复后以最终 DLL 重读相同历史反例副本，要求 16/16 拒绝；同一最终 reader 重读 10 个正常/等价副本，要求全部接受。详见 `final-repair-gate.json`。这里保留旧 packet 的原 build ID，并明确记录新的 reader build ID，不冒充本轮新原生执行。
3. `event_flow_controls.py` 新增 46 项系统控制，覆盖三类选择边界及根决定字段、清理/到达的缺失重复错序归属及完整状态、整个奖励子集合的遗漏/重复、返回快照共同篡改和相邻动作身份。与原 106 项一起在最终构建本轮实际生成的 packet 上运行。
4. 新控制包含一个合法异步顺序等价副本：外层返回位于真实 Resume start 之后、实际完成之前，状态取当时实际开始快照。它验证 reader 允许该契约顺序，属于持久证据等价控制，不声称执行器新增了自然异步延迟业务样本。
5. 完整 live/frozen replay/minimal/off 原生场景、故障/预算前缀、适用旧套件和历史 reader 均使用最终产物回归。原始反例、成功 packet 和中间构建保留。

复现入口使用新输出目录：

```powershell
python -P tools/native-run/run-native-events.py --manifest .build/native-build/n3d2-repair-final02/build-manifest.json --output .build/runs/n3d2-repair-reproduce-01
```

本轮完整回归及证据收集脚本保存在 work01：`run-final.py`、`recheck-history.py`、`inspect.py`。脚本中的原始输出路径已经使用，不得直接覆盖重跑；再次执行应指定新的隔离目录。原始 witness 重读和最终 native 场景分别列出，不把 reader 调用数计入原生执行数。

## 保留的范围与后续

当前仍是固定 v0.111.0/Ironclad/A0 的有限原生样本；event-natural 为自然 80/80 片段，Dummy V1 为 API 夹具 62/62。Hollow Hug、Dummy V2/V3、全部事件、Boss、正常开局完整跨幕通关均不由本轮结果推定。故障/预算依旧是原生 Resume 业务完成后的观察包装层控制。

每 Run 独立进程、MemorySaveStore、保存/replay/Telemetry 拦截和未知 Harmony patch 拒绝继续保留。旧真实 Harmony 探针的 Host/NativeEventFlow 源码哈希可与本轮直接核对；本轮只改变证据 reader，不把旧探针说成本轮重跑。

本轮实现和统一矩阵收口后交独立复审；通过后接续 [4D3 Elite/Boss 交接](NATIVE_RUN_N3D3_IMPLEMENTATION_HANDOFF.md)，随后步骤5自然完整流程与步骤6批测/可信数据。没有启动游戏 UI、真实 Steam/网络、用户存档、Mod 适配或提交推送。
