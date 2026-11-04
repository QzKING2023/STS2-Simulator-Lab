# 4D1B 独立验收：原生路径成立，数据门仍需修复

日期：2026-11-02（UTC）。结论：**未通过最终验收。原生商店纵向路径和恢复证据成立，但生产证据检查器仍接受9个错误副本，须完成下述R1–R3后再进入4D1C。** 本轮没有修改实现或原始结果，没有重跑完整原生流程。

冻结 `EBC85ADE50B9B504BB8E86FF02D70A633BB08A63B69692F04499C37EAF855E37`；Native DLL SHA256 `DAB5D72815FB4F5547D654A791F7B00689E128919C272CAD5049931857E2904C`。独立重算构建ID一致，57源码、37编译副本、3游戏输入、7产物及dirty源码快照一致。15锁定输入、52兼容源码保持。

## R1：价格前后快照仍是未绑定的独立片段

位置：`NativeMerchantEconomyContract.Accept` 的 `merchant-price-start` / `merchant-price` 和生成库存绑定分支。

目前开始快照由该记录自己提供，再由其派生价格结束快照，验证完整Shops RNG转移与价格。最后仅将记录中的entry/model/cost与生成库存对照。这没有将整个价格快照绑定至所属原生库存创建/房间进入区间。两个快照一起错，仍可互相满足检查。

独立副本只改变第一个价格start/return的同一字段，其他所有证据保持，重新生成有效seal：

| 副本名称（均位于独立审查目录） | 矛盾 | 实际reader退出 |
| --- | --- | ---: |
| `independent-price-pair-gold` | 两个价格快照Gold=9999，真实该处为403 | 0 |
| `independent-price-pair-hp` | 两个价格快照HP=0，实际玩家HP=58 | 0 |
| `independent-price-pair-deck` | 两个价格快照牌组清空，真实牌组存在 | 0 |
| `independent-price-pair-futureReviewField` | 只在这两个快照新增未知结构字段 | 0 |
| `independent-price-pair-other-rng` | 两个快照Rewards RNG counter凭空增加，外部状态不变 | 0 |

最小根因修复：将价格子操作纳入一个真实的原生库存生成生命周期，保存不可变的父状态及其已确认结果，完整连接价格start/return、相邻价格和生成完成。为原生房间/库存生成期间确实发生的变化定义有限业务区间；如现有观测不足，可增加最少的原生开始/完成观察，不能靠一组彼此自证的新记录再次形成独立片段。不要仅校验Gold/HP等列举字段，也不能将整个RNG对象或所有状态笼统忽略。

不得简单令价格快照等于Travel dispatch：原生已在此前更新坐标/历史、退出旧房间、推进楼层、创建新房间和抽取库存。固定实现先生成卡、遗物，再生成药水及其价格；完整绑定须保留这些真实变化。当前有限无额外购买Hook的profile可以定义精确边界，不要求提前支持4D1C全部遗物。

## R2：自然Travel的必要子阶段可整组消失

位置：`NativeMerchantEconomyContract.Accept` 的stats、confirmed、room-entered和 `Finish`。

| 副本 | 改动 | 实际reader退出 |
| --- | --- | ---: |
| `independent-missing-initial-stats-pair` | 删除第一次Travel的完整stats start/return | 0 |
| `independent-missing-approach-stats-pairs` | 删除商店前所有Travel的stats对，仅留下MerchantTravel所需的一对 | 0 |
| `independent-missing-next-room-arrival` | 删除离开商店后下一CombatRoom的room-entered，后续Play仍被认可 | 0 |

现有检查能拒绝已出现但错配/乱序的单条记录，却未对每次适用Travel建立必须完成的阶段账本。普通Travel只检查 `!statsPending`，start与return全缺就可通过；`Finish` 的sessions数量下限不能保证最后一个新房间实际有到达记录。

最小修复：按当前自然profile的每次Travel/ MerchantTravel创建必需、唯一、按序且属于当前动作的阶段要求。实际原生 `EnterMapCoord` 调用节点统计的路径，必须完成恰好一对stats，再完成对应旧房间退出、新房间确认和到达；进入下一根决策前以及最终收尾都不能留下到达欠账。初次从Neow出发没有Lab旧session时，按实际原生调用要求stats及新房间到达，不能因为没有旧session而整体免验。保留真实原生跳过分支与专用API夹具差别，不给本来没有的阶段捏造记录。

这项仍属于已声明4D1B数据完整性，不是要求新增节点类型。新控制须包括整组缺失、单条缺失、重复、借用旧动作、调序及正确原始/等价副本。

## R3：Travel结果字段未绑定实际动作与状态

位置：`NativeMerchantContract.ValidateResult` 的 `MerchantTravel` 分支。

`independent-contradictory-travel-result` 将本次返回的 `result.from` 和 `result.to` 都改成 `wrong`，其余真实根动作、状态、合法边保持不变，reader仍退出0。目前只验证decision和实际快照，消费了result.closed，却未校验其余已声明业务结果字段。

最小修复：完整校验当前操作结果的必要字段，from/to绑定当前decision以及派发/返回快照的真实节点，closed绑定退出完成及对应会话。检查同一操作结果中其他已声明的归属字段，保留合法原生状态变化；不需要扩大到无关未来schema。不能以结果字段未被当前策略使用为由允许矛盾的对外数据。

## 三个新增方法的未知补丁门：已补充真实织入证据

本轮在独立进程中针对 `MerchantPotionEntry.CalcCost`、`MerchantRoom.Exit`、`RunManager.UpdatePlayerStatsInMapPointHistory` 分别安装真实Harmony prefix。探针直接加载相同最终Native DLL与哈希锁定游戏程序集，反射调用生产 `NativeMerchantEconomy.Patch` 和 `NativeHost.Ensure`，没有复制审计逻辑或伪造PatchInfo。

每个方法都得到：

- 预置真实未知patch后，生产注册入口抛 `Unknown merchant business patch`。
- 先通过真实Ensure的补丁清单段记录实际清单，再新增真实patch，Ensure抛 `Native host patch inventory changed`。
- 移除该patch后回到同一个预启动隔离检查边界，没有误报清单仍变化。

这是一种明确标注的**审计API夹具**：Ensure基线及恢复后均故意停在 `Offline host isolation invariant failed`，因为没有初始化游戏宿主；它们不算完整宿主运行通过。三个目标业务方法调用计数都是0。这样能够验证实际织入下的生产拒绝逻辑，同时避免为审查启动新的游戏生命周期。正常宿主成功安装与逐动作Ensure仍由已有自然执行证明。

证据为独立目录 `patch-price.json`、`patch-exit.json`、`patch-stats.json`，探针源码在 `patch-probe/`；隔离工程0warning/0error。三进程均退出0，含六项实际未知补丁拒绝。该未覆盖项已在本轮补足其审计门范围，不再单列为当前必修；未来新方法/新业务区间仍需相应门控，不将此扩大成所有patch兼容保证。

## 已独立核实的原生事实与计数

三种子同一Run正常Neow→实际战斗/奖励→合法商店→原生购买→合法下一节点→实际Play的证据成立：DEV01为72/72、Rage实付38、最终HP58/Gold365；DEV02为41/41、ShrugItOff实付24、HP64/Gold87；DEV03为83/83、Whirlwind实付38、HP56/Gold104。实际商品实例进入永久牌组，下一战副本绑定对应DeckVersion，最终为Ongoing/SliceBoundary，而非完整run胜利。无自然路径金币/库存注入。

原生三局药水价格分别为48/51/72、49/48/48、48/51/50；每次记录的Shops完整状态转移和固定原生CalcCost算法一致。局部TestMode分支转接只改三个固定getter判定，原生其余业务保留；`MerchantRoom.Exit`写原生未购历史，RunManager原生统计写当前Gold/HP/MaxHP，原生AppendToMapPointHistory生成条目。全局TestMode与现有平台/保存隔离保持。原生卡历史在FloorAddedToDeck设置之前序列化的区别有保留。

本轮没有发现原始购买、付款、价格或历史数值错误；阻断是完整性门不能拒绝矛盾或缺失的记录。原生调用成功与数据审查通过分别判断。

独立核对了全部227个完整原生结果的原始退出、构建、provenance、隔离和2405项套件检查；退出分布164×0、58×1、2×2、3×3保留。Core98产物及驱动锁匹配。完整route/choices/final-state及RNG的三种子重放、DEV01 minimal/off对照相等。本轮未重跑这227个流程或Core。

恢复记录的前19个原生场景逐文件哈希不变，后补列表确为 natural-1-off/stale/wrong-inventory/task-fault；原残留header的两文件哈希不变，没有process/result，单列为中断未知结果，没有计入227个完整执行。补充恢复驱动身份与记录一致。原始中断原因不能由这些文件进一步确定，未将其标成游戏逻辑缺陷或成功。

## 独立执行与复现

目录：`.build/runs/n3d1b-independent-review01/`。`review.py`实际运行生产reader **167次**：117个原控制、23个原结果包、14个修复回查包、13个独立副本。前154次均符合预期，独立13个中9个错误被接受、2个错误正确拒绝、2个合法等价接受。全部副本重新封装有效seal，拒绝/接受均经过实际生产语义门。

`summary.json` 为 `must-repair`，266项来源/实际结果/恢复检查无失败，9项reader预期不符。跟踪的720项原始来源/证据文件内容未变。构建重算见 `build-id-check.json`。旧231包与924原文件保持的记录已核对，本轮未将其重复计作新游戏执行。

可直接复现（当前错误退出0；修复后应退出1）：

```powershell
dotnet .build/native-build/n3d1b-final05/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d1b-independent-review01/independent-price-pair-gold
dotnet .build/native-build/n3d1b-final05/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d1b-independent-review01/independent-missing-next-room-arrival
dotnet .build/native-build/n3d1b-final05/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d1b-independent-review01/independent-contradictory-travel-result
```

## 继续顺序

由实施回合先完成R1–R3和对应生产负控，冻结真正最终源码/产物，复跑适用矩阵并保留旧失败证据，再独立复审。当前不必扩大到新的商店效果或整幕；也不要通过删除字段、减少数据承诺或只修9份样例关闭问题。

[4D1C交接](NATIVE_RUN_N3D1C_IMPLEMENTATION_HANDOFF.md) 的原生遗物购买→真实Hook业务区间→TheCourier补货/二次购买/旧库存票据拒绝顺序仍可执行，但应等待4D1B数据门闭合。其后奖励闭环、事件/Boss、步骤5–6仍在M1授权范围；无需用户逐步批准。

本轮只新增独立审查证据及文档，未改运行实现、原结果、角色、安装或真实存档，未启动游戏UI、真实Steam/网络，没有提交或推送。
