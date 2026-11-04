# q705条件分支准入与原生流程验证

2026-11-04，第5步有界批次。**q705已接通；第二幕累计四场原生胜利，当前到第13层。M1尚未完成。** 未提交或推送。

## 判别结论

| 层 | 已核实语义 |
| --- | --- |
| 静态预览 | `IntentForecaster`对遇到的条件分支统一添加近似标记；这些标记必须保留 |
| 上游分支执行 | `BranchMonsterAi`已有显式动态处理，实际回合推进使用分支状态和分支RNG |
| 未验证回退 | 原实现对未显式处理的条件可返回捕获时的根选择，不能据此证明未来状态正确 |
| Lab原准入 | 将静态预览近似直接作为拒绝原因，尚未区分上述两种执行路径 |
| 本轮处理 | 复用上游显式处理；独立依赖禁止通用根选择回退，再以能力锁、风险检查和逐动作差分约束采用 |

上游`CombatPlan.ConfidenceText`本就区分未建模与中等置信度的静态估算。本轮没有将`forecastExact=false`改为true，没有清除`ApproximationDetails`，也没有为某只怪物增加Lab分支表或调整评分。

## 通用实现边界

新本地独立依赖：`.build/pinned-inputs/combatsolver-0.50.1-strict-branch01/`，派生标识`strict-branch-runtime.1`。

- 复用已审计`terminal-hooks.1`的安全构建工程、资源和826源文件；相对该基线只修改`BranchMonsterAi.cs`的通用末端回退。
- 原有显式条件实现原样保留。没有显式处理的条件抛`PredictionUnsupportedException`，即使根快照保存了一个选择也不能继续使用它。
- 新增`branch-runtime-admission/v1`能力声明，绑定实际Solver DLL哈希、派生标识和已编译的AssemblyMetadata。
- 仅允许带此声明、静态近似项全为固定版本“条件分支”类别的查询进入影子验证；缺声明、错声明、非条件近似仍拒绝。
- 风险、unmirrored、unsupported intent、时间/取消、无候选、预算、当前致死保护及严格补丁审计保持。
- 每个采用动作仍先做分支执行，再执行原生命令、比较完整continuation/RNG并重新求解。查询、准入记录和步骤显式保留静态近似与已认证范围：**实际已执行前缀，不是完整未来预测**。

Solver DLL SHA256：

`CB2C7A22F99F0BA28D38854747867904212C099E1DBD5CA4AB17874B520D3598`

原`gate01`及`terminal-hooks01`输入都保持。依赖构建0 warning/0 error，保留MIT归属；独立工程没有部署或外部辅助程序目标。

## 控制与实际行为

### 有界API/分支控制

- 最终生产准入API：2正控、11负控。原始旧q705无能力声明时仍拒绝，原始旧q660仍通过；声明能力的合成副本只算元数据API夹具，不追认旧自然结果。
- 负控包括错误DLL哈希、未知策略、额外字段、错误精度范围、非条件近似、模型风险、unmirrored、unsupported forecast及TimeLimit。
- 真实q705根上，两个条件的当前选择都与直接调用原生条件方法一致。
- A/B/A fork隔离：只在B中清除分支能力，睡眠条件由SNORE变为ROLL_OUT，A和原生状态/RNG保持。
- 通用未知回退在“有根答案”和“无根答案”两类API夹具中均拒绝，共4次。该夹具故意使用不匹配来源，不是实际原生遭遇。

首版控制函数要求第二个不同来源，可能错误阻挡未来单怪场景。最终构建改为仅在通用夹具前提成立时运行控制；功能准入不依赖有无第二只怪物。当前q705满足前提并实际运行两项控制，单怪Boss未因这项修正而新增原生认证。

### 自然执行

原先停在q705的战斗，最终版本实际执行16个动作至Victory。原生历史记录了SlumberingBeetle：

`SNORE → SNORE → STUNNED → ROLL_OUT → ROLL_OUT`

BowlbugRock也实际执行了HEADBUTT与STUNNED。每一步的完整状态/RNG均与分支一致。q705至q716共12步保留静态近似标记；后续4步静态预览本身转为精确。没有把这些步统一重标成“未来精确”。

整条当前策略路径累计：**12场实际战斗胜利，其中第二幕4场；175个已执行求解步全部差分一致**。第四场第二幕Victory在a233，原生奖励a234、休息选项a236完成。

## 当前实际停止边界

最后确认：a236。a237 Travel已派发，原生开始进入`EVENT.SYMBIOTE`，Lab入口审计拒绝；保留 **236确认／237派发，Unsupported/exit2、DataComplete=false**。

实际部分执行后的最终状态位于第二幕`MapCoord (1,12)`、第13层，HP84/MaxHP92。它与a236确认快照不同，两者分别保留。未调用未知事件选项，未伪造到达确认或胜利。

最终重放使用236个真实确认记录，加原始trace提取的未确认a237请求；该请求`after=null`、`confirmed=null`。重放复现相同部分执行与Unsupported，未补造确认结果。

## 冻结与证据

最终producer：

`4CFD0B8B6F63D75CF082DD98F84105E74DD05E00BB60F5882381A2C793F4B45C`

- `.build/native-build/solver-branch-runtime-final02/build-manifest.json`。
- `.build/runs/solver-branch-runtime-work01/plan.json`、`ledger.json`、`facts.json`、`closeout.json`。
- exec01/02属于初构建`41B8A1E8…3A46800B`；exec03/04是最终构建的live与重放。验收结论使用最终两次，旧对照不混充同构建结果。
- 关键文件：`solver-query-705.json`、`solver-admission-705.json`、`solver-branch-controls.json`、`solver-online-step-705.json`至`720.json`、`trace.jsonl`、`result.json`。
- 最终元数据API证据：work目录`admission-api-final02.json`；未确认请求来源：`failed-prefix-final02/provenance.json`。
- work目录`collect-facts.py`重读后63项证据断言成立，含完整封签、状态/RNG、原生动作和重放。它不把Unsupported run验收为完整通关。

本批4/4次原生执行，最后进程在20分钟门前结束；执行窗口1036.99秒，单次约42–44秒，实际运行合计约172秒。其余窗口包含夹具前提修正和构建等待；收尾仅进行证据核对。保守存储计量含新依赖/构建及共享旧缓存约1.15 GiB，小于2 GiB。最终宿主4个既有warning、0error。

15个Mod输入、52兼容源码、旧两套826源/6 DLL及此前5003份原始执行文件保持；RF旧7项和当前9项输入也不变。MemorySaveStore、保存/replay拦截、无UI/真实Steam/遥测边界保持，无权限或未确认命令阻塞。

## 尚需按完整run收口

此轮没有扩展独立退出阶段采集，`MajorTravel`的这项缺口继续明确未完成。当前128尾部根动作预算是早期实验配置，接近上限；`ContinueActTwo`到第三幕MapRoom后返回，也不是完整run控制器。应把这些结构性工作与后续原生节点推进合并规划，避免继续形成逐节点的小循环。

详细交接见[下一步](COMBAT_SOLVER_BRANCH_RUNTIME_NEXT_HANDOFF.md)。最终M1同构建全量验收仍待完成。
