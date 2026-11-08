> 2026-11-04增量：已固定0.13.19来源和MIT许可，选定源码以窄标量接口复用；下一幕已生成Darv/KnowledgeDemonBoss内容读取与原生跨幕结果一致，纯度检查通过。CallingBell预测方法本次未被实际调用，三项实际Darv拾取预测仍未覆盖，未验证隔离多步预测。详见[Darv验证](../combat-solver/COMBAT_SOLVER_DARV_VALIDATION.md)。以下为此前只读研究记录。

# RandomForeseer 后续复用入口（本机源码初查）

2026-11-04。当前优先完成原版原生流程，战斗复用CombatSolver；战斗外预测在确有需要时先核查并复用RandomForeseer。本次只读本机已有0.13.19源码，没有加载、编译或执行RandomForeseer，也没有将预测接入第4步。第4步奖励/地图继续由原生游戏执行。

已确认的源码事实：

- `RandomForeseer.json`：id=RandomForeseer，version=0.13.19，min_game_version=0.111.0，RitsuLib最低0.5.14。`LICENSE` 为MIT，版权hotwords123（2026），复用须保留许可和归属；游戏/Godot及其他依赖不因而成为MIT内容。这只是本机版本声明，不代表已完成Lab运行兼容验证或当前最新版查询。
- `RandomForeseerCode/OutOfCombat/CardRewardPrediction.cs:23` 提供PredictCards，另有RunPredictionContext重载。上下文使用预测RNG、稀有度状态和已知Hook镜像；CloneOptions复制可变选项及RngOverride。适合先做原生奖励前的小型预测→原生生成对照。该文件明确尚未镜像AfterModifyingCardRewardOptions，SilkenTress/SilverCrucible等一次性状态不能直接当完整支持。
- `OutOfCombat/EventOptionPrediction.cs` 有按实际EventModel类型登记的TryPredict registry，现有输出主要为IHoverTip，部分事件依赖原有选项到EventModel的映射。须复用计算逻辑并提取可记录结果，不能把UI提示解析或未知事件的空列表当作完整事件执行器。
- `OutOfCombat/NextActPrediction.cs:45` 读取 `runState.Acts[current+1]`；UI图标读取下一Act已有的古人/Boss。此处主要展示已生成内容，并非重新执行完整跨幕生成。离线host已有合法预生成状态时应优先复用状态与读取逻辑；没有据此声称预测了任意先古选择或Boss业务。
- `OutOfCombat/OutOfCombatPredictionUtils.cs` 复用RNG.Clone、RelicGrabBag.Clone及原生工厂，已有牌/遗物/药水计算入口；不能仅因出现Clone就宣称整个真实状态不扰动，须结合实际调用链和原生对照验证。

适配约束：这些计算类主要是internal，很多对外样式方法仍返回Godot HoverTip/Control；当前没有发现可直接作为Lab稳定公开SDK的统一数据端口。`RandomForeseer.csproj` 依赖Godot.NET.Sdk4.5.1/net9.0、RitsuLib0.5.14、Harmony及游戏程序集，并有发布部署目标。`Entry.Initialize` 注册遥测、设置和广泛Harmony补丁。未来应优先在Lab锁定源码/程序集和依赖，接窄计算入口或保留许可的源码复用，避免调用完整Mod初始化及部署目标。是否直接调用内部类、抽出少量计算层，须在后续小验证中按实际依赖决定；本轮没有作这项架构实施。

最小后续建议：先选本批已取得真实证据的普通卡奖励，固定输入/选项/预测上下文，核对预测调用前后真实完整状态/RNG不变，再比较原生候选的模型/升级/顺序及RNG变化。未知Hook、缺状态或UI依赖明确Unsupported；预测作为附加观察，不能替代原生领奖/地图/事件生命周期。事件、先古、休息、商店及Boss相关预测逐类验证后再声明覆盖。该小验证不应阻断M1原生流程推进，也不扩Mod/BaseLib/RitsuLib/MinionLib兼容专项。

本次初查来源及文件SHA256记录在 `.build/runs/solver-run-work01/random-foreseer-source-review.json`，保持本机参考源码只读。此文是复用入口说明，不是已经通过的预测验证报告。

2026-11-04 后续设计约束：RunPredictionContext 复制部分 RNG、奖励稀有度和一次性状态，同时仍持有 live Player/RunState。它不构成完整可独立fork的原生run快照，也不能因RNG已Clone就推断任意遗物/监听器或连续多个节点完全隔离。尤其 AfterModifyingCardRewardOptions 未完整镜像时，单步候选恰好一致不能证明多步奖励状态和顺序一致。

后续按复用优先推进：原生节点驱动继续提供真实事件、奖励、先古、Boss与终局；CombatSolver负责已认证范围内的战斗决策。需要预测时先核查RandomForeseer已有类型注册和计算入口，再确定窄Lab适配，能力不足才补实现。第一门仅普通单节点卡奖励：调用前后核对定义内完整live状态/RNG与实例，预测候选模型/升级/顺序与紧随其后的原生生成比对。检测到未支持Hook、状态缺失或副作用即显式Unsupported；保持预测观察和原生执行的结果分类，不默认回退成空预测或“精确多步”。

单节点通过后才扩展有已知处理器的事件，再逐类核查先古和Boss相关的已生成内容读取/预测。结果优先提取已有计算层，避免另建同类效果引擎；不加载完整Entry初始化或部署目标，不新增Mod/BaseLib/RitsuLib/MinionLib兼容专项。首次预测对照须另预登记小预算及输入锁；本轮未新增RandomForeseer运行实证，也不以其研究阻断原版原生流程。
