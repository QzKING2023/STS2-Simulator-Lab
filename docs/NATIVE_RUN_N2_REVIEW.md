# 新 M1 步骤3独立验收：N2 尚有两项必修

日期：2026-11-02。依据 [N2 实施交接](NATIVE_RUN_N2_IMPLEMENTATION_HANDOFF.md)、[本地验证报告](NATIVE_RUN_N2_VALIDATION.md)与[数据契约](NATIVE_RUN_DATA_CONTRACT.md)，只读核对实际代码及最终证据，并在独立副本上调用同一个生产检查器。**结论：N2 本轮未通过独立验收；原生执行与重放的已有正例成立，数据可信门需完成下述两个有限修复后复审。** 不要求提前完成后续房间、终局或策略指标。

## 1. 构建和计数已核实

完整 Build ID：

`FC9ECD0B89FE31A7FD3635DEAAF1369D835BD77A050FE206169672B376D8E9CD`

构建记录：`.build/native-build/n2-final06/build-manifest.json`。

- NativeRun DLL：`25DC2234C1F77B2F131A726D1DE910EB28B860FD2B657242997CAAD7FD913B68`。
- Core DLL：`32B54F0DFA32AD3A3607F16B666B869E41A6C17C3AB44035E0A12B1B7B622DF3`。
- 重新计算 Build ID 一致；30 条 source、3 个原版 inputs、15 条 compiled、7 个 artifacts 的实际文件 hash 全部匹配。source 包含真实 dirty 文件，compiled 绑定构建目录中的输入副本，不以 HEAD 替代源码。
- 15 个 Eleanor v0.4.1 pin 均匹配原锁；旧冻结清单中的 52 个 tools/validation 源文件实际 hash 也全部相同。本轮无 Mod 运行，旧兼容代码与旧构建身份另行保存，不与 NativeRun 混算。
- `n2-final06-suite/summary.json` 的 **74 条实际子进程记录、248 条检查**均能核对。74 = 40 正例执行/重放 + 23 运行负控 + 4 旧宿主控制 + 4 观察模式重放 + 3 损坏输入重放。17 种日志/结果腐坏控制属于检查器调用，未冒算为 17 次原生游戏执行。
- 子进程退出分布为 **44 个 0、28 个 1、1 个 2、1 个 3**；不能称 74 次全部正常完成。非零退出与预期负控吻合。
- `n2-final06-inspection/inspection.json` 确有 92 条补充检查，全部通过；它们包含构建/进程/对象关联检查，不是 92 个独立游戏场景。
- 本次新增独立核对为 **174 条、0 失败**，见 `.build/runs/n2-independent-review01/binding-and-matrix.json`。这是来源、原始退出、隔离字段和原始文件比对，不代替下面未通过的语义负控。

Core 98 项与旧原版 battle/RNG 四进程、旧 Eleanor 16 步/122 检查按现有日志核对，本次没有重复执行这些游戏回归。旧 harness 身份仍是 `D5FF494B75BF670D2EFD0A335A6E87290F6C5A2C7B3C8511839CD68ACB0DFF1D`，不能称其验证了新的 NativeRun DLL。

## 2. 可以保留的事实

### 原生选择、奖励与执行前缀

实际 `grid` 选择 `o1` 并返回同名牌中的指定引用；Bundle 选择 index 1。`bundle-shared` 两组共享首对象，实际返回第二组完整对象 `i28/i30`，不是从首对象倒推组索引。固定方法的窄 transpiler 仅替换 TestMode 的默认取组位置，保留原生守卫、ReserveChoiceId 和后续逻辑；记录了目标 token/MVID/IL hash，未知已有补丁及运行期 inventory 变化拒绝。未发现改写牌效或削弱旧仿真差分的做法。

DEV01 自然链均是正常开局 → Neow → 实际首战 Victory → 原生金币/奖励 → 合法下一战 Play：

| 路径 | 实际根动作 | 下一战 HP/金币 | 永久牌组 | 奖励身份 |
| --- | --- | --- | --- | --- |
| skip | 24 dispatched / 24 confirmed | 70 / 266 | 10 | 领取金币，跳过剩余奖励 |
| take | 25 / 25 | 70 / 266 | 11 | 卡选项 index 1 的 Pommel Strike；永久 `i31`，下一战副本 `i35` 的 DeckVersion=`i31` |

两路径各有 SaveProgress 5 次、WriteReplay 1 次拦截。没有宣称奖励牌必在首手、已实际打出或第二战已结束。全部是 native-only / NotCompared / Ongoing / SliceBoundary。

非法 Armaments 子选择发生于真实支付和获得格挡之后：4 confirmed / 5 dispatched、0 合法 choice 提交、Block 5、Energy 2；原始已执行前缀保留。事件故障是标注的注入控制，真实效果已把金币变为 249，仍以 EngineError 退出，保留 1 confirmed / 2 dispatched。不是自然 Neow 失败，也不能把这些负控写成零根派发。

### RNG、不扰动及信息模式

新 `IncludeFields=true` 的实际文件含 run/player/event synchronizer 的 counter 与 s0–s3。独立扫描自然 skip/take 分别核对 2,730 / 2,775 个 run/player 流记录；其中不是独立样本，表示多个观察边界上的流结构检查。详细、off、minimal 以及冻结重放的 route/choices/final-state 文件相同；route 的逐动作状态 hash 包含这些真实字段。旧 N0/N1 只能保留动作复现证据，其遗漏 RNG 字段的旧结论不能沿用。

公开根策略 DTO 是 Handle/Kind/Model；当前是固定 first-legal baseline 及预先指定的选择，不能称为已评估的公平通用策略。choices/route/trace 的 context、stamp、隐藏状态属于 oracle 重放资料。off/minimal 仍执行必要身份注册与状态投影，当前二者行为相同。不扰动证据限定于这些实际路径及已声明投影，不能推出所有 RNG、getter 或隐藏状态都已覆盖。

### 隔离与有限覆盖

MemorySaveStore、shouldSave=false、无 Steam 初始化、无 NGame、单人本地 service、未加载 Mod/CombatSolver、SaveProgress/replay 拦截及补丁稳定性均保留。这里是实现与触达路径的证据，不是所有 OS 写入/全部未来内容的证明。普通选择、Armaments/Headbutt、永久牌组升级等仍为标注 API 夹具；没有自然获得这些牌或完整休息室的证明。

## 3. 必修 R1：完整性检查器未验证声明的必要内容与因果关联

目标主要为 `tools/native-run/NativeEvidence.cs`；必要时小幅补充 `NativeObservation.cs` 和 `NativeChoices.cs` 的审计字段。现有 `Review` 验证了 hash 链、全局集合与部分字段，尚未构成所声明的语义完整性门。

本次只在新建副本中修改记录，重新计算链/footer/seal，再调用**相同冻结 DLL 的实际 `--review`**。因此测试的是记录生产/汇总可能产生的语义错误；不是要求防御恶意重写并签署整套证据。未改原始结果、游戏状态或生产代码。未修改副本正常返回 0；以下六个错误副本也全部返回 **0**，应当拒绝：

| 隔离副本 | 实际缺陷 | 生产检查器结果 |
| --- | --- | --- |
| `control-null-run-rng` | 将所有已有状态的 runRng 置 null；该 run 的运行期状态本应有 RNG | passed；`NativeEvidence.cs:105` 遇 null 直接跳过 |
| `control-false-fixture-contract` | grid 的 fixture-assertion 与 fixture-contract 的 valid 改成 false | passed；仅用 fixture-contract 的存在绕过自然生命周期检查，未验证断言/契约的内容 |
| `control-missing-history` | 删除全部 105 条 combat-history，但保留原生清理报告 entries=100 与完整详细模式 | passed，并写出 damage/block/energy 全 0、`effects.complete=true` |
| `control-missing-choice-return` | 删除 grid 的原生 method return 记录 | passed；choice-returned 仅作为允许的 kind，未验证要求/终结 |
| `control-wrong-choice-root` | q1 的 choice-resolved 归给已结束的 a1，而原 request 属 a5 | passed；只校验 context 和请求集合，未绑定相同 root/父请求 |
| `control-early-root-confirm` | 将 a5 confirmed 移到 dispatch 后、其选择请求及返回之前 | passed；只比较最终集合，未检查未决子请求/任务与时间顺序 |

证据：`.build/runs/n2-independent-review01/integrity-controls.json`；各 `control-*/review.log` 和 `trace-review.json` 保留实际 exit 与错误通过结果。另做 `*-consistent` 六个副本，将 result.trace 内嵌 seal 同步为新 seal，仍全部错误返回0，见 `consistent-seal-controls.json`；这排除了仅补一个冗余 hash 比较就掩盖语义缺口的解释。`actual-trace-observations.json` 也保存了真实历史/清理计数与缺历史副本被标完整的对照。

**有限修复要求：**

1. 由可信 schema/scenario/mode 定义本轮 required fields；运行期状态的 run/player RNG 不能 null，流集合与必需字段完整。允许空值的初始化阶段必须明确，不能用统一 null 放行。校验 header/seal/process 的观察模式一致。
2. 详细模式对每个 history epoch/clear 段维护可核对序号和实际原生条数，在清理前、最终切片边界校验已采集条数；原始记录缺失、重复或乱序不能只靠 writer 自报 counts。当前真实首战100条、下一战5条需按生命周期分段，不能简单要求全文件数等于一次 Clear 的值。off/minimal 明确不提供完整效果统计。
3. 对已声明 fixture 验证必要 assertion/contract 的合法结构、valid=true、与当前 scenario 一致；不能仅凭任何一条 fixture-contract 的存在跳过自然场景门。
4. root request→dispatch→子请求/任务→确认建立顺序与上下文校验；确认后不得出现归属于该 root 的未决选择/任务。ordinary method 的 requested→resolved→returned 必须闭合。CardReward 原生回调的终结方式不同，使用明确的 reward 完成记录，不盲目要求所有来源都有同一种 `choice-returned`。
5. 保持同名实例可审计：当前 choice-requested 只记录公开 handle/model/upgrade，缺少 handle→真实对象的审计映射（`NativeChoices.cs:56`）。在 oracle 记录中增加私有映射/Bundle整组引用，验证 resolved/returned 对应；公开 DTO 不增加 oracle ID。不要从“引用曾定义”推导“选中了正确候选”。已有实际夹具正确选择的事实保留。

以上属于 N2 原要求中的必要数据/身份/关联门；不要求全触发因果图、完整 run 状态或公开协议。修复后将上述负控纳入生产矩阵；还应加同名对象返回错配、同数量 history 重复/乱序控制，避免仅加非空判断。保留原正例与失败证据，不以改称“不完整”取消 N2 所需门。

## 4. 必修 R2：fixture 与 fault replay 来源标签错误

`tools/native-run/run-native-slice.ps1:36` 先判断 `ReplayPath`，将所有重放统一写成 `frozen-natural-replay`。实际证据中：

- 18 个 API fixture 的第二次执行被标成 frozen-natural-replay。
- Armaments 的 off/minimal 两个对照同样被标成 frozen-natural-replay。
- `unknown-choice-field`、`missing-choice-file` 两个 grid 损坏输入控制也被标成 frozen-natural-replay。

完整 22 条列表见 `actual-trace-observations.json` 的 `mislabelledFixtureReplays`。文档正确提醒它们是夹具，但机器可读数据会把夹具混入自然路线，违背后续样本/完成率契约。`softlock` 也应按其明确的注入/错误控制身份标注，不能仅依赖冒号或几个 scenario 名的启发式判断。

**有限修复要求：**把来源种类与执行方式拆开，例如 `scenarioKind=natural-run|api-fixture|fault-control`，`executionKind=original|frozen-replay|observation-control`；也可使用明确的 fixture-replay 类型。由预登记场景元数据决定并核对 header/process/result，损坏输入控制显式标 fault-control。不能仅凭有 ReplayPath 决定自然来源，也不能修改历史原始文件以掩盖问题。新增统计负控，确认夹具/故障不进入自然样本计数。

## 5. 修复、复审与步骤4的门

下一轮先由实现者完成 **R1 → R2 → 定向负控 → 新冻结下 N2 必需统一矩阵**。新源码必须建立新 Build ID；不同产物的旧 74/248 不能拼成新冻结的完整通过结果。Core/旧兼容仅按实际共享改动范围回归，继续独立标识旧 harness，不无故扩大 Mod 验证。

步骤4的实施交接暂缓，原因是 N2 的上述门尚未闭合，**不是缺少用户授权**；六步总授权继续有效。修复后通过复审即可继续，无需再请用户批准。

步骤4交接在 N2 修复后细化；依赖顺序保持 [M1 方案](MILESTONE_1_NATIVE_RUN_PLAN.md)：

| 后继工作 | 确切含义与进入条件 |
| --- | --- |
| N1-T | 在现有安全 host 下独立标注 lifecycle fixture，探测实际 ActChangeSynchronizer/EnterNextAct 与 TheArchitect 原生选项/回调/WinRun 路径；先审计保存与清场顺序。不能直接调用 WinRun 造胜利。精确阻塞也可作为风险探针交付，但会阻止依赖它的后续能力声称完成。 |
| N3 基础房间 | 复用 RestSiteRoom/RestSiteSynchronizer 的实际 Rest/Smith；EventSynchronizer 的普通/阶段/嵌套选项；MerchantEntry.OnTryPurchaseWrapper 的付款、库存、指定永久卡移除。各入口需先明确合法候选、原生任务完成与离开条件，不能手工付款/Upgrade 代替整个房间。 |
| N3 取得与使用 | TreasureRoom 原生 relic picking、RewardsSet 的药水/遗物奖励、PotionModel/UsePotionAction 的合法使用与容量/目标。通过原生 Hook 执行效果，不逐效果写镜像；新增特殊选择与宿主依赖逐类显式处理或 Unsupported。 |
| N3 战斗/多节点 | 实际精英/Boss窄案例，然后同run连续若干真实节点，跨节点核对金币/HP/永久deck/relic/potion身份及临时状态。简单策略死亡是合法结果，但不能代替精英/Boss和房间覆盖证据。 |

这些是既定主线的待进入工作，**本轮未作节点新测试，也未宣称可直接关闭 N3/N4/N5**。完整幕转换的自然路径属于 N4；自然完整胜利与真实死亡属于 N5。N1-T 只提前暴露尾部宿主风险，fixture 不混入自然胜率。N2 修复后的独立复审应再按实际可复用入口完成详细步骤4交接。

## 6. 本轮操作范围

新增本审查文档与 `.build/runs/n2-independent-review01/` 本地审查材料；104 个既有项目文件相对本轮开始 hash 全部相同，见 `preservation.json`。没有改运行代码、原始 final06 证据、正式路线图或锁定输入。仅运行证据读取/哈希核对和冻结 DLL 的 `--review`，未初始化新游戏 run、未重跑74进程、未启动UI、未操作角色/安装/真实存档、未提交推送。HEAD仍为 `d73039d688eb54c9513f5b9508a03947e689aa18`。

当前没有审批、网络或挂起进程阻塞。停止点是独立审查发现的两个实现验收缺口；按委派，本轮不自行改实现。调用同一检查器可复现：

~~~powershell
dotnet .build/native-build/n2-final06/bin/Release/net9.0/SimulatorLab.NativeRun.dll `
  --review .build/runs/n2-independent-review01/control-missing-history
~~~

该命令只重写独立控制副本的 trace-review.json；当前错误返回0并标 effects.complete=true，修复后应返回非零。所有原始正例仍留在 n2-final06-suite。
