# M1-NativeRun：N0／N1 实现与本地验收

日期：2026-11-02。对应新 [M1 实施方案](../milestones/MILESTONE_1_NATIVE_RUN_PLAN.md)的安全宿主／执行契约和首个原生纵向切片。**本地验收通过，待独立审查；整个 M1 尚未完成。** 历史 STAGE_1 与既有 Mod 矩阵继续保留。

## 实际执行结果

固定游戏 v0.111.0 / 41cef1ea，Ironclad、Standard、A0，独立内存档案。原生档案把全部 epoch 设为 Revealed，未虚构胜负／Ancient 访问；日期归零、实验档案 ID 固定，不读取用户进度。原生 UnlockState 从同一 Progress 派生。Obtain 与 Revealed 不同；最初用 Obtain 导致 MapRoom 开局的失败报告保留。

同一 run 使用原生命令实际完成：

1. 正常新 run、默认 Acts、原生起始牌组／Burning Blood。
2. Neow 原生选项生成与选择；该 seed 取得 Golden Pearl，金币 99 → 249。
3. 普通 EnterMapCoord 进入生成地图的首战 Nibbits Weak；真实出牌、结束回合及敌方动作，至原生 EndCombatInternal(CombatTurnState)。
4. 采集清理前胜利与历史，继续原生战后清理。
5. RewardsCmd.OfferForRoomEnd 真实生成奖励，原生命令领取 17 金币，249 → 266；合法跳过其余可跳奖励。本策略的 skip 包含未领取的卡牌／药水奖励，不是只跳卡牌。
6. 经奖励完成检查与生产 Hook，进入当前节点真实 child：首战 (1,1) → 下一战 (0,2)，抵达原生玩家 Play 状态。HP 70、金币 266、永久牌组与遗物延续。

自然执行和新进程冻结重放各 **24 个确认根动作、182 个观察事件、105 条即时历史观察**；各通过 **22 项独立证据检查**，raw exit 均为 0。profile.json 两次 SHA-256 相同：
`1846F7D99F855D7E313F7940044B0E4EE2E14E7BCA6AC3CD01D7E01301DA053C`。

这是 **native-only / NotCompared** 的实际执行，不依赖 CombatSolver 或逐效果镜像，不声称新的 native/sim 差分或完整 run 胜利。第一战结束时的战后 HP 包含真实原生 Hook；没有为胜利修改 HP、金币、牌组、奖励或 RNG。

## 构建与证据身份

最终构建：`.build/native-build/n0-n1-final-03/build-manifest.json`。

- Build ID：`913DEAA5D674366A36B2256E9658F46380B4F9C66C43EE9BD8707FDE9FEEAFD6`。
- NativeRun DLL：`A921016C57E77FC6056CCB06549B74408D7D415AE68324CF44BCA0A8F2A43769`。
- Core DLL：`32B54F0DFA32AD3A3607F16B666B869E41A6C17C3AB44035E0A12B1B7B622DF3`。
- 23 个来源记录、9 个生成源码／项目／许可记录、3 个原版本机程序集输入、7 个输出文件；最终绑定核对零不匹配。
- 构建 raw exit 0，零 warning／error；本机 .NET 工具链版本记录在 manifest。早期构建的在线 NuGet 审计警告另留日志；最终构建关闭在线审计查询，使用已有包缓存。

统一证据根：`.build/runs/native-n0-n1-final03/`。

| 文件／目录 | 作用 |
| --- | --- |
| `summary.json`、`process-summary.json` | 验收范围、真实退出码与证据身份；明确 fullM1Complete=false |
| `natural/`、`replay/` | result、process、events、route、profile、manifest 和 22 项 evidence-review |
| `negative-controls.json`、四个 control 子目录 | 实际运行时失败传播与拒绝结果 |
| `evidence-controls.json`、`evidence-control-*` | 对真实检查器施加的空证据／退出矛盾负控 |
| `final-binding.json`、`preservation.json` | 最终构建绑定、旧来源／产物与 15 个 Mod 输入的保留情况 |

本地 HEAD 仍为 `d73039d688eb54c9513f5b9508a03947e689aa18`。实际源码包含已有 dirty 工作和本次新增文件，manifest 的逐文件 hash 才是构建身份，不能仅用 HEAD 复现。

## 实现与隔离

新增 `tools/native-run/` 独立入口和构建／运行／检查工具。只在忽略的构建目录复制上游 MIT host 的 GameBootstrap、MainLoopContext、MemorySaveStore、OfflineLocalization 及 LICENSE，以有守卫的转换移除 Mod／solver 初始化引用。原复用目录和既有 Mod 驱动未改。运行时解析器拒绝 MyMod、CombatSolver、BaseLib、Ritsu；实际 loadedAssemblies 亦无这些程序集。

新增 `NativeRunResultV1`，允许实际原生结果 NotCompared；保留旧 ValidationOutcomeV1 的严格差分完成条件。原生票据绑定来源对象、sequence、边界投影与完整动作，仅消费一次。冻结动作比较完整 JSON 结构，允许缩进变化，拒绝附加字段；修复前第 0 步误拒绝的证据保留在 final02，不计最终通过。

实际隔离证据：TestMode=true、SteamInitialized=false、ShouldSave=false、DailyTime=null、MemorySaveStore、NetSingleplayerGameService、NGame.Instance=null。自然路径 SaveProgress 拦截 5 次，WriteReplay 拦截 1 次，无 telemetry Capture 调用。报告写入 Lab runs 目录；子进程有 5 分钟 watchdog。没有启动真实 UI、部署、改角色／游戏安装或用户存档，没有提交／推送。

新 host 继续沿用已验证的无 UI bootstrap；新增展示／IO seam 有方法 token／MVID 和 handler 记录，完整 Harmony inventory 在每次确认边界检查稳定：

- Neow 的候选构造会即时取 enchantment／energy 悬浮提示图标；仅 texture getter 返回 null，保留原生选项、RNG 和效果。
- NHoverTipSet.Clear 在无节点树时跳过，保留其余战后逻辑。
- Godot.Time 用托管时钟；MegaInput 仅跳过静态热键名称初始化，原生 CardRewardAlternative.Generate、Hook 和选择效果仍执行。
- 事件资源 preload 变为已完成任务；继承 bootstrap 的 run／act／combat preload、空本地化表、节点静态构造等范围记录在报告。
- TaskHelper.UnobservedFault 原生信号进入失败门，防止 EventSynchronizer 吞异常。Sentry Capture 仅被阻断并计数，不用于忽略该错误。
- N1 尚未实现 CardSelectCmd.From* 子选择，统一明确 Unsupported，先于 TestMode 自动首项执行。没有把 Bundle 选择伪称已支持。

这不是 OS 级系统调用隔离证明，也没有证明所有后期类型均可沿用现有节点初始化替代。新增内容必须继续审计，遇到新的宿主依赖明确停止。

## 正确性与负控

| 案例 | 实际结果 | 已确认／派发 | 原始退出 |
| --- | --- | --- | --- |
| 自然流程 | SliceBoundary、Ongoing、NotCompared | 24 / 24 | 0 |
| 冻结自然重放 | 相同每步前后投影（含 run/player RNG 序列化状态）、档案相同 | 24 / 24 | 0 |
| 标注的 TaskHelper 内层故障 | EngineError、Unknown；捕获 1 个 fault，阻断 1 次 telemetry | 0 / 0 | 1 |
| 标注的旧票据 | InvalidAction；Neow 后保留两个实际动作，没有额外派发 | 2 / 2 | 1 |
| 标注的预算停止 | BudgetExceeded、Unknown；控制注入，不冒充真实耗尽预算 | 0 / 0 | 3 |
| 未实现 Bundle | Unsupported；没有默认选首项、没有根动作派发 | 0 / 0 | 2 |
| 空 route／矛盾 raw exit | 复制证据上运行实际 review-native-slice 检查器，均拒绝 | 非游戏执行负控 | 各 1 |

Core 共 **98 项检查**通过，包含原 81 项和新增 17 项：原生完成／非完成／未知值、缺数据／未确认派发拒绝、原生死亡与差分解耦、票据作用域和一次性、完整动作 JSON 比较等。日志：`.build/native-core-checks.log`。期待的初始 Ironclad 80 HP／99 gold／10 张牌（5 Strike、4 Defend、1 Bash）写为独立契约，未从被测运行时变量求出。

已有回归另外使用保留的旧 harness SHA `9EBF76E26E467D763545F374E0AACE73D3F70104BD59BE8EC8AAE568D7951452`：

- `.build/runs/native-n01-legacy-vanilla/`：两次原版 battle／有限奖励流程、两次双目标 RNG；四子进程均 exit 0。
- `.build/runs/native-n01-legacy-eleanor/`：原 16 步／122 检查 Victory，exit 0。

这是旧产物兼容回归，不冒充新原生构建的差分认证；本轮没有重跑整个 36-task 两卡矩阵。旧 69 项来源中仅 `tests/SimulatorLab.Core.Checks/Program.cs` 为本次增加检查而变化；原兼容实现全部保持字节一致，15 个锁定输入零变化。旧 manifest 的 9 个产物中，两个共享 `tests/.../bin/Release` 检查输出被本次授权构建更新，故其旧 hash 不再匹配；其余 7 个（包括实际回归的旧 harness 与依赖）未变。旧报告、manifest 与源码快照保留，不声称旧 manifest 的所有 live 路径仍绑定。

## 可复现入口

本机合法游戏目录与已审查的本地 host 源码由调用者提供。所有输出使用全新的 Lab `.build` 子目录，不下载、分发游戏代码或部署 Mod。

~~~powershell
pwsh -NoProfile -File tools/native-run/build-native-run.ps1 `
  -GamePath <local-game-directory> -HostSource <reviewed-host-source-directory> `
  -OutputRoot <fresh-Lab-build-directory>
pwsh -NoProfile -File tools/native-run/run-native-slice.ps1 `
  -BuildManifest <build-manifest> -OutputDirectory <fresh-Lab-runs-directory> `
  -Seed LAB_V0111_NATIVE_DEV_01
pwsh -NoProfile -File tools/native-run/review-native-slice.ps1 `
  -RunDirectory <that-run-directory>
~~~

重放在 run-native-slice 中加 `-ReplayPath <natural/route.json>`；控制分别用 `-Control async-fault|stale|budget|subchoice`，预期原始 exit 依表解释。不能将全部子进程要求为 0。新构建若源码／输入／输出与 manifest 不符，会拒绝派发；运行器不覆盖旧证据。

## 已知限制与下一回合交接

N0／N1 的 DataComplete 仅表示上述切片所声明的必要投影齐全。历史记录包含即时原生条目及可读原始属性；部分复杂属性目前只记录类型名，**不代表完整伤害／触发因果图**。run/player RNG 状态已比较，事件／同步器专有 RNG 尚未全部盘点；没有任意中途恢复承诺。

冻结重放现在验证同输入策略产生的每个动作及原生投影，不是独立策略寻优。没有验证观察器开／关不扰动、所有 power／特殊状态的完整投影、自然死亡、实际墙钟耗尽、取消／watchdog 杀进程或多种子可靠性。后期幕转换／Architect 风险探针 N1-T 本轮未运行。

下一步先由独立审查核对本次源码、最终构建和真实证据；通过后进入 N2：合法非首项／嵌套／隐式选择 provider，尤其 Bundle 的窄接入；完整数据契约、来源／实例／事件 RNG、追加日志、观察不扰动、更多失败控制。N1-T 应在扩大房间类别前安排。随后按 N3–N6 处理节点、跨幕、完整终局与批测，不回到 Mod 专项扩展。
