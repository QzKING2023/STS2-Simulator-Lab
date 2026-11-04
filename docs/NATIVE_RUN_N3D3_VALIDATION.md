# 4D3 首批原生精英/Boss驱动与终态验证

本轮完成首批驱动、实际精英/Boss死亡、正常地图精英片段和相应可信数据门，**待独立审查**。精英/Boss Victory及其奖励、Boss后的实际act-ready/跨幕尚未闭合；不能据此宣布整个4D3或M1完成。

最终冻结：`963B389CA6C6E6B353000ED0CD67CD6B3F60EC397FA94135985FCE0CF601F20C`，目录`.build/native-build/n3d3-final02/`。Native DLL SHA256：`30E9E360A41C2C23056ABFFAD53B8F3C406BF1DDAD8B2C5D0B5656D1B13A2FA7`。编译exit0，0 warning/0 error；75源输入、48编译输入、3游戏输入、7产物及75源快照逐项锁定。

## 实际覆盖与结果

固定v0.111.0/41cef1ea、Ironclad/A0、独立内存档案。三类正常执行均由原版方法产生Death，不是胜利预测；没有手工改HP、能量、敌人或永久牌组。第二版策略按当前敌意先格挡到10，再攻击/Bash优先，审慎标为Oracle；不宣称公平可见信息模式或有效胜率。

| 场景 | 实际证据 | 结果与前缀 |
| --- | --- | --- |
| major-elite，DEV03 | 正常开局Monster、原生奖励，再通过明确API夹具进入BygoneEffigyElite | 实际Death，51确认/51派发；live、冻结重放、minimal、off四次 |
| major-boss，DEV03 | 同样原生前序，API夹具进入CeremonialBeastBoss | 实际Death，51/51；四次 |
| major-natural，DEV01 | 正常开局、3场普通战斗及奖励、SunkenStatue/DenseVegetation、合法地图进入BygoneEffigy精英 | 实际Death，93/93；四次。不是完整run胜利 |
| major-natural，DEV02 | 自然路线到MerchantRoom | Unsupported，35/35；现场执行和失败前缀重放，未跳过商店 |
| major-natural，DEV03 | 自然路线到Hollow入口 | Unsupported，99确认/100派发；原生进入已发生、根Travel未确认，两次失败前缀重放一致 |
| major-action-fault | 真实首个卡任务后注入包装异常 | EngineError/exit1，3确认/4派发；保留实际副作用 |
| major-action-budget | 第一个真实战斗动作确认后停止 | BudgetExceeded/exit3，总4/4，其中战斗动作恰1个 |
| major-export-fault | 真实精英Death后注入便捷导出IOException | DataError/exit1，51/51；不是自然磁盘损坏，未认证为成功 |

原固定池Ordinal首项规则在首次尝试前登记，失败后未换Encounter或seed。Overgrowth实际精英池为BygoneEffigy、Byrdonis、PhrogParasite；Boss池为CeremonialBeast、TheKin、Vantom。第一版attack-first的死亡和第二版策略结果分列保留，不当作相同策略重复。最初误将Canonical Encounter直接传入原生房间入口产生的两个EngineError也保留；已改用原生ToMutable实例。

自然片段和夹具各有独立来源标签；replay与来源分开。DEV02/03的停止是本新驱动尚未组合既有商店/Hollow能力，不是宣称这些原生内容不可离线运行。失败重放保留真实未确认根动作，使用明确派生的`failure-replay-route.json`，不补造after确认边界。

## 统一同构建验收

最终构建完整执行10套原生组，另实际执行Core98：

| 组 | 原生进程 | 检查 |
| --- | ---: | ---: |
| major | 19 | 261 |
| events | 10 | 304 |
| reward closure | 35 | 419 |
| merchant transactions | 18 | 471 |
| merchant economy | 23 | 361 |
| merchant | 25 | 371 |
| 4C rewards | 55 | 561 |
| 4B rooms | 34 | 307 |
| 4A terminals | 16 | 273 |
| N2 | 74 | 580 |
| **合计** | **309** | **3908** |

所有套件检查符合预期，另Core98通过。原始子进程退出分布为**226×0、70×1、8×2、5×3**，保留预期非法动作、故障、Unsupported和预算，并非全部胜利。19个major执行中只有3个不同的正常场景配置；同种子重复/观察对照不是独立样本。

603项来源/执行/重放核对无失败：每个原生进程的实际manifest、header、process/result、退出码和隔离证据匹配最终冻结。75源快照及全部编译输入/产物核对，15锁定输入、52兼容源码不变。旧4D2自然/夹具的8个完整sidecar与此前已审查冻结相同；本轮没有重跑Mod runtime或将仅输入保留说成Mod验收。

新组95个生产检查器控制全部符合预期：93个损坏包拒绝，2个结构等价包接受。覆盖缺失/重复/归属、完整快照、选择结果、错误到达房型、原生生命周期计数、缺失整个奖励集合和整子树篡改等；均在重新生成合法seal后调用真实reader。原4D2独审79包也由本次最终reader重读，原25反例拒绝、正常/等价与不完整控制的预期保持，316个原文件哈希不变。没有把历史reader包计成新原生进程。

## 本轮自查与修复证据

首个冻结`DE432BB3D7A58CBA0998D5B537BD256A4AE2738395E75EED927EEB48F13E298E`未通过：两个回调新增状态字段负控被误接受；另自查证明删除所有leave-ready、错误到达房型及虚构Win计数也被误接受。原矩阵和所有坏副本保留，未将外层失败写成成功。

随后补齐离开就绪前置、具体session/房间/房型/来源、所有选择请求唯一性、完整观察投影结构及原生生命周期事件计数。8包定向修复gate（5坏/3正常）符合预期，再以最终冻结完整重跑上述矩阵。首批源码快照和最终源码分开，只有最终构建用于本次通过口径。

详尽责任表见[生命周期契约](NATIVE_RUN_N3D3_LIFECYCLE_CONTRACT.md)。静止边界完整比较，真实业务区间保留合法变化；当前奖励生成只独立验证非Rewards-RNG状态及状态字/counter步进一致，不声称独立重算所有池抽取次数/候选算法或全部Hook语义。

测量还发现重复写便捷`events.json`是主要开销。新profile执行时继续追加规范JSONL，便捷导出结束时写一次。相同自然片段该类写入1044次/13061.9ms→193次/231.7ms，完整route、choices、reward-choices、final-state（含RNG）保持一致。两轮并发负载不同、阶段计时重叠，不推导通用加速倍数。导出异常有专门DataError控制；旧profile写入方式保持其既有回归。

## 证据、复现和下一步

最终汇总：`.build/runs/n3d3-work01/closeout.json` / `summary.json`；来源核对`facts-final02.json`、`outcomes-final02.json`、`source-final02.json`；新组`.build/runs/n3d3-final02-major/summary.json`与`controls.json`；定向修复`repair-gate01/summary.json`；历史独审重读`prior-review-gate02.json`；优化对照`observer-export-comparison.json`。

```powershell
python -P tools/native-run/run-native-major.py --manifest .build/native-build/n3d3-final02/build-manifest.json --output .build/runs/n3d3-reproduce-01
```

输出目录必须是新的；原始证据和历史失败不可覆盖。每Run独立进程，MemorySaveStore、ShouldSave=false、未初始化Steam、单人网络替身和UI为空的守卫均通过。代表精英/Boss包分别记录拦截SaveProgress4次、replay1次；这些是被拦截的调用次数，不是持久化写入次数。没有新增Harmony目标或放宽未知patch拒绝，没有启动UI、改角色/安装/用户存档、提交或推送。

本轮交独立审查后按[后续交接](NATIVE_RUN_N3D3_NEXT_HANDOFF.md)继续组合既有节点能力、改善合法资源与策略可达性，完成精英/Boss真实Victory→原生奖励→实际ready→自然跨幕，再步骤5–6完整原版run与数据交付。没有外部权限或环境阻塞；当前剩余的是上述业务验收门。
