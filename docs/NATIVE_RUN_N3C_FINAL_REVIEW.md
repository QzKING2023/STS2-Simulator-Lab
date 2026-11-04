# M1 步骤 4C 最终独立验收

日期：2026-11-02。结论：**通过当前 4C 有界验收；历史 21 个反例已闭合，可进入 4D。** 本轮只读审查实现及原始证据，执行生产证据检查器并保存独立审查结果，没有修改运行实现，也没有重新启动原生游戏场景。

依据：[修复报告](NATIVE_RUN_N3C_BOUNDARY_REPAIRS.md)、[17 类操作生命周期契约](NATIVE_RUN_N3C_LIFECYCLE_CONTRACT.md)、[上一轮独立复审](NATIVE_RUN_N3C_REPAIR_REVIEW.md)。本结论不表示原版完整 run 或 M1 已完成。

## 冻结身份与证据

- Build ID：`458B3547C7B2E9A979EF7CC299C024E9BFA9704861D734FD370133EE3EE16652`。
- Native DLL SHA256：`96B2A0A1B82E7272509551426FDEA9E414BC05A449BC8800D7A4EBC403CC6682`。
- 构建：`.build/native-build/n3c-boundary-final03/build-manifest.json`。
- 被审原生矩阵：`.build/runs/n3c-boundary-final03b-{suite,n3b,n3a,n2}/`；`final03b` 是运行批次，使用的是 `final03` 冻结产物。
- 独立证据：`.build/runs/n3c-boundary-independent-review01/` 下的 `summary.json`、`facts.json`、`contract-audit.json`、`closeout.json`。

独立重算构建标识并核对 48 个实际源码及快照、30 个编译输入、3 个游戏输入、7 个产物，均匹配。15 个锁定 Mod 输入和 52 个冻结兼容源码不变。HEAD 仍为 `d73039d688eb54c9513f5b9508a03947e689aa18`；工作区原有修改保留。本轮审查结束时再次核对冻结文件，未发现变动。

## 实际生产检查器复验

所有案例均调用冻结 DLL 的生产 `--review` 入口，读取当前证据或独立修改并重新封装的副本。没有用独立测试替身代替生产逻辑，也没有仅依赖封印不匹配拒绝坏包。

| 组别 | 实际 reader 次数 | 结果 |
| --- | ---: | --- |
| 实现提交的完整控制集 | 292 | 278 个坏包拒绝、14 个等价包接受 |
| 当前正常、重放及观察对照 | 42 | 全部接受 |
| 保留的历史包 | 115 | 均符合各自原预期，保留原构建身份 |
| 本轮独立构造 | 53 | 48 个坏包拒绝、5 个等价包接受 |
| 合计 | **502** | **0 个预期不符** |

本轮另完成 195 项来源核验、229 项实际流程核验和 324 项契约核验，均无失败。这些是证据断言，不是新增原生游戏执行；本轮新增原生执行数为 0。1884 个原始证据文件在审查前后未变。

### 历史反例与同类覆盖

21 个历史反例全部在当前正常证据上重新构造，并被具体语义门拒绝。`closeout.json.fixedHistoricalCounterexamples` 列出每例、原始退出码和对应诊断：集合返回与完成状态、子选择必需及顺序、卡来源升级、宝箱金额与新遗物、药水/宝箱静止入口与尾部，以及外层卡取得指向旧牌。不是因为旧 4C 的 `acquired=null` 布局不兼容才拒绝。

独立补充检查了 UsePotion、TreasureOpen/Pick/Exit 的入口与尾部完整 HP 漂移；既有控制同时覆盖金币、RNG 和新增嵌套字段。还将卡奖励父子两层 acquired 一起改为同一旧牌：父子表面一致仍被真实新增实例与牌组关联门拒绝。合法属性重排与一致扩展副本仍被接受。

## 按 17 类操作契约审查

实际审阅 `NativeRewardContract.cs` 的有限操作树、不可变快照和结果规则；核对 `NativeRewards.cs` 真实原生任务与卡取得记录。42 份正常证据中观察到全部 17 类操作，逐包独立核对 LIFO、父动作归属、请求/答复/开始/返回闭合和结果。292 控制中每类都有 7 种生命周期破坏，坏包载荷确实发生对应变化。

| 契约 | 独立结论 |
| --- | --- |
| 请求与结果完整唯一 | operation ID、当前根/context/父操作、start/return、候选答复、所需子类型/次数均受生产检查；缺失、重复、错序和错配拒绝 |
| 静止入口与尾部 | UsePotion、TreasureOpen/Pick/Exit 统一通过 `StaticWrapper` 使用已复制的 Open/LastChild 状态；完整结构比较，不是金币/RNG 字段特判 |
| 集合与取得 | reward-set→item 的当前状态、集合完成→返回、CardReward 子菜单→外层返回均有对应约束；确认边界继续绑定实际根返回 |
| 真实业务区间 | 原生 Offer/Populate、选择效果、取得 Hook、Travel、药水使用和宝箱业务允许真实状态变化，不一律要求前后相等 |
| 外层 acquired | 以实际 CardReward 对象为键保存并消费一次原生完成结果，外层 acquired/success/后态与该次 card-menu 对应；子菜单再验证新卡、来源、升级和实际牌组 |

独立统计到静止入口：reward-set→item 80 次、药水 10 次、宝箱 Open 12/Pick 6/Exit 6 次；静止尾部：集合 36 次、药水 10 次、宝箱 Open/Pick/Exit 各 6 次，均合法。八次 CardReward 尝试中六次取得新卡、两次选择替代选项；外层结果与真实子结果一致。宝箱六次金币结果、四次具体新遗物取得成立。

观察到的十类父子边包括真实 Cauldron 取得后的嵌套 reward-set。全部 17 类操作已出现，不等于每一条允许的 Hook 拓扑都已执行：EventOption→新 reward-set 的 Neow 迁移、card-menu 内额外奖励 Hook、treasure-extra/obtain 内额外奖励等仍须按后续具体路径举证。不得据此宣称全原版内容或任意监听器已支持。

## 原生回归事实核对

| 范围 | 原生进程 | suite 检查 |
| --- | ---: | ---: |
| 4C | 55 | 561 |
| 4B | 34 | 307 |
| 4A | 16 | 273 |
| N2 / 步骤 1–3 | 74 | 580 |
| 合计 | **179** | **1721** |

每个进程的实际结果、原始退出、header、构建 manifest 和来源均核对。退出分布为 122×0、52×1、2×2、3×3，各自符合预先声明的正常/负控/Unsupported/预算预期；不是 179 条自然胜利。Core98 的实际日志、驱动文件和 DLL 与冻结对应。648、371、561 项补充核验是不同审查层的断言，不重复计作原生进程。

- 正常开局同 Run 的 24/24 根动作保留：Golden Pearl Neow、真实首战 Victory、原生金币/药水/卡奖励、下一战 Play、永久牌与战斗 DeckVersion 关联，以及实际使用 BlockPotion。最终 HP70、Gold266、牌组11、药水0，结论仍为 native-only/NotCompared。
- 16 份正向重放和 5 组完整/minimal 观察对照的 route、choices、reward-choices、final-state（含完整记录 RNG）相等；旧 skip24/take25 回归也保留。
- DEV01 为 71/71 连续片段；DEV02 为 138/138 真实 Death；DEV03 在 WhisperingHollow 保留 77/78 Unsupported 前缀，没有更换种子避开失败。
- 奖励任务故障保留已发生的 +7 Gold 和 2/3 前缀；药水效果故障保留实际消耗/伤害和 4/5 前缀；非法药水控制无 UsePotion 派发。未确认根动作没有被报告成成功。

隔离证据为 MemorySaveStore、shouldSave=false、Steam 未初始化、无 UI、无 MyMod/BaseLib/CombatSolver。该检查不等于操作系统级全文件写入监控。本轮只运行 reader，不调用真实 Steam、网络、游戏 UI 或用户存档。

## 进入 4D 与限制

[4D 实施交接](NATIVE_RUN_N3D_IMPLEMENTATION_HANDOFF.md)现可执行，保留商店交易/移除、WhisperingHollow 与嵌套事件战斗、精英/Boss 的依赖顺序。以下已知原版边界必须在 4D 或步骤 5 的相应前置门闭合，不能只标 Unsupported 后宣称 M1 完成：

1. Driftwood 真实 reroll 的第二菜单及取得。
2. CardPileAddResult 的实际替换/拒收与 modifyingModels，不能手工添加卡。
3. Sozu NotAllowed 与 TooFull，以及“奖励处理完成”和“真正入槽”的区别。
4. 新奖励会话下 Neow 连续 CardReward 的旧已知 seed 迁移。
5. 受修改宝箱金币/SpoilsMap 的真实来源账本。

完整状态比较覆盖当前声明的数据投影，不代表整个 CLR 隐藏状态；记录的 RNG 一致也不等于所有未记录随机源或零售表现等价。原生业务效果尚未逐项独立重算；当前是在明确契约内验证执行来源、生命周期、身份与可信记录。4D、步骤 5 跨幕完整 run、步骤 6 批测交付仍未完成。

## 复现与收尾

```powershell
dotnet .build/native-build/n3c-boundary-final03/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3c-boundary-final03b-suite/rewards-natural-1
```

本轮完整复核驱动保存在独立目录的 `review.py`、`facts.py`、`contract-audit.py` 与 `closeout.py`，仅为本地审查材料。原始证据与实现未改；更新的是本报告、里程碑状态、修复报告验收链接和 4D 交接入口。未提交、未推送，无待定执行、权限拒绝或剩余必修阻塞。根线程可直接继续已授权的 4D，随后推进 5–6，无需再次逐步请批。
