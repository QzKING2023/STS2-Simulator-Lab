# 4D2 剩余状态绑定修复

本轮依据 [第二次独立审查](NATIVE_RUN_N3D2_REPAIR_REVIEW.md) 修复两类状态绑定遗漏。最终结果以 `.build/runs/n3d2-state-work01/closeout.json` 为准；统一矩阵已完成并符合预期，待独立复审。旧报告、冻结及全部反例保留。

冻结 ID：`5B062F0D8E5AF9C6CB6498C5F472EA626C000E83F1A970B568CB88A8EE2B0758`，目录 `.build/native-build/n3d2-state-final01/`。构建 exit0，0 warning/0 error；源码输入和快照按实际 dirty 内容锁定。

## 最终同构建结果

Native DLL SHA256：`394EA58C0470DE2D530E390BF37F68FDC04C72B21F13F8507E9A0D3E775DEAA8`。72源输入、46编译输入、3游戏输入、7产物及72份源码快照均核对。

- 统一9组实际290原生进程/3647套件检查，另实跑Core98。事件10/304；其余 closure35/419、transactions18/471、economy23/361、merchant25/371、suite55/561、n3b34/307、n3a16/273、n2 74/580。
- 原始进程退出仍为214×0、68×1、4×2、4×3；预期错误、Unsupported和预算均保留。
- 213个事件生产控制：203个损坏拒绝、10个合法/等价接受。新增61项也在候选阶段先作定向检查，未替代最终原生生成证据上的正式控制。
- 最终85次历史gate全符合预期，含原16拒绝、原10保留、上次59副本按预期。1026历史副本由最终reader实际重读；4020原始文件哈希保持。
- 537来源/退出/重放核对通过，15输入52兼容源码不变。前一冻结与当前自然/夹具8个完整JSON sidecar比较一致；当前live/replay/minimal/off亦全字段一致，包含记录RNG。
- 原生执行器/宿主未改。旧实际Harmony审计API探针保留历史边界，未计作本轮新审计或业务执行。

证据入口：work01的 closeout.json、facts.json、final-gate.json、execution-continuity.json、historical-reader-final01.json、preserved-inputs.json、source-snapshot.json。Core日志为core-final01.log，相关产物实际哈希单列。原生套件最多2个独立编排；历史reader单worker读新副本，无并行构建或共享证据写入。

## 状态依据和修复

只修改原有 `NativeRewardContract.cs`、`NativeEventFlowContract.cs`、`event_flow_controls.py`。没有修改原生执行器、卡牌/角色、宿主安全补丁或游戏程序集。

| 边界 | 当前约束 |
| --- | --- |
| 实际 Resume 开始 | 任务/父操作归属和时序通过后，保存不可变任务输入及观察阶段锚点 |
| 外层 Resume 返回 | 无论任务是否已完成，都必须与当前已验证阶段的完整状态相同；外层返回和元数据不能重写锚点 |
| 原生空 EventRewards | 子集合入口绑定包装父状态；集合必须确为空，随后已有集合完成、子返回、父返回、确认链继续绑定 |
| V1 Resume 奖励生成 | 从不可变实际任务输入构造完整预期转换：完成事件、清空选项、玩家 Rewards RNG 按一次原生 NextItem 所需的 xoshiro256** 步进。其余完整结构保留 |
| V1 药水取得 | 当前固定分支为一个原生药水奖励；按真实候选、具体新实例、首个空槽和原生清单变化验证完整取得状态，保留其它状态及未知字段 |
| 相邻根动作 | enabled 后 dispatch 的完整状态绑定上次不可变确认状态，拒绝错误子树在下一动作恢复原值 |

RNG 校验从任务输入计算四个状态字和 counter 的下一步，不使用本种子的期待药水、金币或输出状态反推允许字段。锁定 SDK 的 `BattlewornDummy.Resume`、`Rng.NextItem/NextInt`、`MegaRandom` 调用关系在本机研究副本核实；反编译资料只放在 `.build`，不作为项目代码或公开文档内容。

这仍是固定 V1 脚本的有限数据契约。未知额外奖励/取得 Hook、其它奖励分支如改变本契约外状态，会被拒绝，不由本轮宣称支持。当前实际药水取得和原生生成继续执行游戏代码；上述计算仅用于独立证据检查。

锚点仅由成功校验过的业务记录推进。保留外层先返回、实际任务稍后完成的合法契约顺序；新增正控在实际任务开始、集合入口、药水开始、药水返回、集合返回五处分别安排外层返回。它们是持久证据的顺序等价控制，不是新增自然异步延迟的游戏执行证据。

## 验证与复现

修复前以旧 reader 实际重读9个独立反例，9/9误接受，见 work01/before-reproduction.json。候选和最终 reader 均重读原16+10基准及上次全部59个独立副本，共85次，输出分别为 candidate-gate.json、final-gate.json。原9个新反例不再通过，正常和合法等价保留。

新增61项生产控制：5个合法阶段返回，56个破坏控制。覆盖金币、HP、Run RNG、玩家 Rewards RNG/counter、事件 RNG、未知嵌套字段，外层单点篡改、完整集合到父返回共同篡改、药水取得后共同篡改及错误完成事实。与原152项共同执行；每个副本重新生成合法seal，再调用实际生产 reader。

统一原生矩阵、Core98、历史 reader、完整 sidecar/RNG、源/输入锁及退出分类以最终 closeout 为准。原生进程、持久证据副本 reader 与元数据/时序控制分别计数；不把 reader 调用当作新游戏执行。

新目录复现入口：

```powershell
python -P tools/native-run/run-native-events.py --manifest .build/native-build/n3d2-state-final01/build-manifest.json --output .build/runs/n3d2-state-reproduce-01
```

统一编排、原始反例 gate、历史重读和来源收集脚本保存在 work01。已使用的输出目录保持不可变；重跑须使用新目录。

## 剩余范围

自然80动作片段、Dummy V1 API夹具、观察包装层的故障/预算控制仍分别列。未新增 Boss、全事件、正常开局完整跨幕/RunVictory、策略强度或 Mod 支持。原15锁定输入、52兼容源码继续冻结；Mod runtime 本轮不重跑。

统一回归完成后交独立复审，再接续 [4D3精英/Boss交接](NATIVE_RUN_N3D3_IMPLEMENTATION_HANDOFF.md) 和 M1 步骤5–6。每Run独立进程、MemorySaveStore及保存/replay/Telemetry和Steam隔离继续保留。没有启动游戏UI、改角色/安装/用户存档、提交或推送。
