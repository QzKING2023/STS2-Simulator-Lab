# 新 M1 步骤3：独立审查 R1/R2 修复与复跑

日期：2026-11-02。本文保留针对[N2 独立审查](NATIVE_RUN_N2_REVIEW.md)的C166修复证据。其后[复审](NATIVE_RUN_N2_REPAIR_REVIEW.md)确认原六类修复及R2成立，但要求补充必要快照存在性门；现已完成该项本地修复与新冻结复跑，见[快照修复验证](NATIVE_RUN_N2_SNAPSHOT_REPAIRS.md)，待独立复核。M1全六步尚未完成，本文旧构建的74/379/114不替代新结果。

## 最终冻结与复现

- Build ID：`C16648CAF5F9B11E96472FB7C46F0549F802CCCC6A1AD44126AF5EA699C58C63`。
- Manifest：`.build/native-build/n2-repair-final01/build-manifest.json`；实际 dirty 源码 32 条、compiled 副本 17 条、原版 inputs 3 个、outputs 7 个，全部 hash 核对一致，重新计算 Build ID 一致。
- NativeRun DLL：`4031C3BE651D2364DAFD9CB02481C7BF785D85FEAD40359BD6A29983529C6D0E`。
- Core DLL：`32B54F0DFA32AD3A3607F16B666B869E41A6C17C3AB44035E0A12B1B7B622DF3`；Core 源码和输出未改。
- 固定游戏仍为 v0.111.0 / 41cef1ea；HEAD 仍为 `d73039d688eb54c9513f5b9508a03947e689aa18`，没有提交推送。
- 完整矩阵：`.build/runs/n2-repair-final01-suite/summary.json`。
- 补充核对：`.build/runs/n2-repair-work01/inspection.json`；修复证据索引：同目录 `repair-evidence.json`；构建日志：`final01-build.log`。

~~~powershell
pwsh -NoProfile -File tools/native-run/run-native-n2.ps1 `
  -BuildManifest .build/native-build/n2-repair-final01/build-manifest.json `
  -OutputDirectory .build/runs/<fresh-output-directory>
dotnet .build/native-build/n2-repair-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll `
  --review .build/runs/<fresh-output-directory>/slice-take-1
~~~

该入口先核对冻结源码、输入和输出，不覆盖旧证据。构建方法仍见 [N2 原验证报告](NATIVE_RUN_N2_VALIDATION.md)。源码再变更需要新冻结，不把本结果挪给其他二进制。

## R1：生产检查器改为校验必要事实和关联

新增内部 `NativeTraceContract`，由实际 `NativeEvidence.Review` 对每条记录调用；原来的 hash 链、schema、原始退出、结果契约、引用和汇总检查继续执行。没有另建只供测试的检查器。

1. **RNG 与必要状态。** 运行期 run/player RNG 必须是对象，本固定版本的流名称集合必须完整；event synchronizer RNG 也必须存在。counter/s0–s3 由原检查继续验证。HP/金币/格挡/幕/层与运行期集合也验证类型，不使用默认值补 null。header/seal/process 的观察模式需一致。
2. **历史分段。** collector 为每条即时原生历史写入 epoch 和连续 index；Clear 前读取实际原生 Entries.Count，与已观察条数核对；最终安全边界再读取一次。reader 按 epoch 核对详细记录的序号和条数、Clear、最后 checkpoint。自然切片为第一战100条，两个 Clear 后下一战5条，总105条。off/minimal 没有详细历史，仍需相同原生计数检查点，效果统计不标完整。
3. **夹具契约。** 由登记场景决定是否允许 fixture 数据，校验 name、valid=true、必要 assertion 名称集合、唯一性和完成顺序。自然路径不能因混入 fixture-contract 而绕过生命周期门。
4. **动作、任务、选择。** reader 逐条核对 root request → dispatch → 子请求/任务 → confirmation；已结束 root 不再接收子请求。普通选择必须 requested → resolved → returned，且 root/context/父请求一致。原生卡奖励使用单独的 `choice-reward-completed`，在领取实际完成且永久对象身份核对后、根动作确认前记录；没有假装奖励回调是普通 From* 方法返回。
5. **实例对应。** choice request 的 oracleOptions 记录 handle 对应的代表对象及完整组引用；resolved 要对应代表对象，returned 要对应所选完整组/顺序。Bundle 共享首对象时按组保留，不能只凭“引用已定义”通过。此字段只在 oracle 重放资料，公开根 DTO 仍是 Handle/Kind/Model。

所有新语义腐坏控制重新计算 hash 链/footer/seal，并同步 result 内嵌 seal，再交给同一生产 reader。故意构造的错误依然完整落盘，不能只靠文件损坏或重复 seal 不一致碰巧拒绝。

| 审查负控 | 新构建实际拒绝原因 |
| --- | --- |
| 缺全部 combat-history | Missing/unexpected detailed history |
| runtime runRng=null | Null/nonobject runtime RNG runRng |
| fixture assertion/contract=false | Failed/duplicate fixture assertion |
| 缺普通 choice-returned | Confirmation before child choice/task completion |
| choice-resolved 归给旧 root | Choice owner/order mismatch |
| 根动作确认早于选择请求/返回 | Choice root/context mismatch |

此外覆盖 player/event RNG null、缺夹具断言、错误夹具名、选中错误同名对象/返回错误同名对象、相同总数的 history 重复/乱序、缺最终 checkpoint、缺 root-request、任务归属错误。每个控制都核对预期的**具体语义错误文本及 reader exit1**，不只检查任意非零退出。原始成功副本仍需正常通过 reader。

具体日志在最终 suite 的 `<control>-rejection.log`，隔离副本在 `control-<control>/`；20项具体语义门结果统一收录于 `repair-evidence.json`。

## R2：来源与执行方式独立登记

新增内部 `NativeScenario` 作为有限场景登记表。未知场景/控制/观察模式拒绝；不再以 ReplayPath 或名称启发式推导自然来源。header、result 和 process 保存同一元数据，reader 使用登记规则重新核验，suite 另按预定队列核对每个实际进程。

- `ScenarioKind` / process.scenarioKind：natural-run、api-fixture、fault-control。
- `ExecutionKind` / process.executionKind：live、frozen-replay、observation-control。
- `Replay`：是否实际传入冻结重放；观察对照也单独保留此值。
- `HostControl`、`InputControl`：宿主故障和损坏输入控制分别明确记录；root-extra、unknown-choice-field、missing-choice-file 不会被标为自然样本。

旧22条错误标签对应的新结果：18个fixture重复仍属 api-fixture/frozen-replay；Armaments off/minimal 属 api-fixture/observation-control；两个 grid 损坏输入属 fault-control/frozen-replay。softlock 明确属 fault-control。完整对应表在 `inspection.json` / `repair-evidence.json` 的 correctedReplayLabels。

新增 process 来源、执行方式和观察模式错配控制；实际 reader 均拒绝。这里保留了两条自然 skip/take 的 original/replay 和观察对照，不能把这些复跑当独立 seed。

## 同构建结果与计数

最终构建0 warnings / 0 errors，完整 suite **74个原生子进程、379项检查、0失败**；补充实际文件/进程核对 **114项、0失败**。这些是不同计数，不相加当独立场景数。

74进程的队列仍为20组正例各执行/重放、23个运行负控、4个旧宿主控制、4个观察对照、3个损坏输入重放。子进程退出为 **44×0、28×1、1×2、1×3**；负控失败被正确保留，不能写成74次普通完成。

379 = 原队列248检查 + 每进程来源/执行方式交叉核验74项 + 新语义控制57检查。证据腐坏控制由原17类扩为37类，属于检查器调用，不算原生run；其中新增20类校验具体语义原因，部分控制只修改process文件，不会冒算一次 mutation 调用。

所有正常正例均通过实际生产 reader，负控按预期原始退出和失败分类收口。关键结果：

- 自然skip仍24/24，take仍25/25；Neow→首战实际Victory→金币/奖励→下一战Play。二者HP70、Gold266，永久deck分别10/11。
- take仍原生领取奖励index1的Pommel Strike；永久`i31`与下一战副本`i35.DeckVersion=i31`。领奖实际生效后才记奖励子请求完成，未强制放入首手或宣称已打出。
- 与旧final06相比，两条自然路径的 **route.json 和 final-state.json 均逐字节相同**。新 choices 增加了私有候选映射，格式变化不用于冒充旧记录。
- skip和Armaments的详细/关闭/最小模式及冻结重放继续逐边界等价；实际counter/s0–s3存在。旧N0/N1遗漏RNG字段的文件只保留动作证据，不重新赋予完整RNG结论。
- 非法Armaments仍4 confirmed /5 dispatched、0合法子选择提交、Block5/Energy2；真实晚到选择前缀未回滚。事件故障、sink/配额故障、预算控制和Unsupported仍各自保留失败/停止类型。
- MemorySaveStore、无UI、无Mod/CombatSolver、单人离线service和Steam未初始化守卫保留；自然路径SaveProgress5/WriteReplay1各自拦截。未新增原生玩法补丁或改牌效。
- Core98项实际重跑通过，检查程序的Core依赖hash与最终产物一致。旧兼容清单52个验证源文件与15个Mod pin均未变；未重复启动旧Mod回归，其既有分离构建身份和历史结果保留，不混为新NativeRun测试。

## 文件范围与仍保留的边界

运行改动：`NativeChoices.cs`、`NativeEvidence.cs`、`NativeObservation.cs`、`Program.cs`、`run-native-n2.ps1`、`run-native-slice.ps1`；新增 `NativeTraceContract.cs`、`NativeScenario.cs`，均在 tools/native-run。更新数据契约/验证文档并新增本文。其他既有项目文件保持原样，收尾核对见 inspection/preservation。

开发证据保留在 `.build/native-build/n2-repair-dev01` 与 `.build/runs/n2-repair-dev01`：第一次定向构建有1个nullable警告，四个定向原生案例及其reader均通过；添加正式flow注解后最终构建0警告并完整复跑。final06和审查的错误通过副本保留，不改历史标签或原结果。

目前仍是内部单run单进程协议、固定开发seed、有限原版选择/首战切片、native-only/NotCompared。未提供全部触发因果图、全部后续RNG、完整run/Death、任意checkpoint恢复、公平通用策略或公开Mod SDK。JSONL配额仍不能代表全部兼容镜像文件的总配额。此轮不扩这些边界。

下一门为Astra独立复审本次R1/R2与最终证据；通过后继续已授权的N1-T/N3及后续步骤，无需追加用户批准。没有权限/进程/环境阻塞，没有启动真实游戏UI、改角色/游戏安装/用户存档、部署、提交或推送。
