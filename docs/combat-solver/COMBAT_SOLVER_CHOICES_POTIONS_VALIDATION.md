# CombatSolver 动作绑定修复与选择、药水首批验证

2026-11-04。本批有界收口；五步计划第 2 步尚未完成。

## 结果

原五个误接受反例已修复。正常旧证据仍通过，新检查器拒绝错误目标 CombatId、目标 index、牌 occurrence、StateKey 和 EndTurn 多余执行字段。

另完成一条**固定原生 API 夹具**：正常开局至首战后，用真实 CardPileCmd.Add、CardCmd.Upgrade 和 PotionCmd.TryToProcure 建立测试输入；实际打出升级 TrueGrit，选择非首项 Defend 耗尽，再实际使用 FirePotion。两步完整预测/原生 continuation 相同，父 fork、原生来源和所记录 RNG 检查通过。

这条路径共 6 个派发/确认根动作，终态是 `Ongoing / SliceBoundary`，顶层 `Comparison=NotCompared`；两份 step 文件分别提供差分证据。它没有改变永久牌组，不代表自然获得这些卡/药水，也不是求解器在线自行选中了这两个动作。没有新的 BossVictory、RunVictory 或 M1 完成。

## 修复及复用

- `NativeSolverActionContract` 供运行时和检查器共用执行字段门。读取证据时还将牌模型、升级、两个 occurrence、StateKey、目标索引/CombatId 绑定实际派发和当前状态。
- 新捕获直接保存原生手牌 StateKey、Creature CombatId 和药水槽实例映射，绑定当前状态和 continuation。历史 v2 的精确映射只适用于已验证的三种未改动初始牌；其余旧格式明确拒绝，不能凭不完整旧 JSON 推导任意新牌语义。
- `NativeSolverChoices` 复用固定 CombatSolver 的 `GetActionChoicesInExecutionOrder`、`PlanCardChoice`、`MatchesToken`、`ChoiceCardKey` 和 occurrence 契约，并接到已有 NativeChoices 请求/结果/重放通道。当前实际能力限 Action/Exhaust/Hand；来源或时机不符、缺失、多余、过期或错误实例均拒绝。
- 预测侧复用 `BeginActionChoices/EndActionChoices` 的完整选择游标。原生侧选择发生在当前根动作内，实际候选、返回句柄和对象均有记录。重放通道也会检查计划选择与冻结答复一致，但本批没有实际运行完整重放。
- 药水使用 fork 自身的槽实例，以及现有 `PotionExecutionSupport.Prepare/Complete`；原生侧调用 `EnqueueManualUse` 并等待真实 ActionExecutor。没有新增药水效果实现或搜索算法。

选择 token 的匹配方式参考了固定 CombatSolver 的 KnownExoskeletonsNativeSelector（MIT）；既有 MIT 和 `THIRD_PARTY_NOTICES.md` 归属保留。游戏和其他本机依赖的分发边界不变。

## 真实动作与独立断言

原始成功目录：`.build/runs/solver-choice-potion-exec-04/`。

| 实际动作 | 状态与身份实证 |
| --- | --- |
| 升级 TrueGrit | 能量 3→2，格挡 0→9；原生候选为 Strike i30、Defend i31、Defend i32；计划选择 o1/i31，真实返回并耗尽 i31，i32 留在手牌 |
| FirePotion，槽 0，目标 i26 | 敌人 HP 43→23，槽中原药水移除，能量保持 2 |

这里的非首项是候选 index=1，**不是**同模型 OptionOccurrence=1；实际使用的是 occurrence=0。未认证非零重复实例部署。

两步的永久 deck 内容和引用连续性在真实运行中检查；金币、HP、maxHP、遗物及完整 run/player/event RNG 没有变化。完整战斗 continuation 另覆盖战斗 RNG。独立 9 格挡/1 能量、20 伤害/移槽断言取固定普通牌/药水规则，不取求解器评价函数。

每个固定动作之后，真实捕获并重新调用 CombatBeamSolver：分别展开 809、119 节点，boundary=None，来源保持不变。两次返回首动作均为 Defend；第二个固定 FirePotion 动作由夹具预登记，不能称为采用了第一次搜索的建议。

15 个真实托管选择 API 负控在该进程内通过：错误 key、源/候选 occurrence、模型/升级、数量、效果、来源 pile、时机、上下文、来源卡，以及缺失答复、未消费多余计划、额外请求。均检查零额外派发和源状态不变。这些是 API 控制；没有把它们说成 15 条自然战斗错误路径。

## 冻结与分层验收

| 用途 | 构建 ID | 目录 |
| --- | --- | --- |
| 原五反例首次修复，无新原生执行 | `551E3390C963BE44CDE38EF3B7879063FC93BE37BE4A950F8E9AFD739915B662` | `.build/native-build/solver-binding-repair01/` |
| 成功原生生成器 | `6000695356411BEEB98FD7A4CB27EF5778A41FBDEAF50FF2C22CBC6165767447` | `.build/native-build/solver-choice-potion-final06/` |
| 最终生产检查器 | `52CBA2FA019E799A89A66E8A07DD94EBE50F2A885AEF48AACB5E331A7CFC7328` | `.build/native-build/solver-choice-potion-checker10/` |
| 默认原生配置编译/API 回归 | `F29C8F9B23E7EDF60D9C02A36E69FD5D70DF8493D680340D7793FE9C1DA28849` | `.build/native-build/solver-choice-potion-native-default11/` |

成功执行只属于 final06。之后修改范围仅为 `NativeSolverFixtureContract.cs` 和 `NativeTraceContract.cs` 的证据检查逻辑；compiled/source 差异和排除这两个文件的生成链指纹已独立计算。没有把 checker10 或默认配置说成重新执行过游戏，也没有为了检查器修复重跑原生矩阵。

检查器修复包括：新多动作夹具不套用旧单夹具全局断言集合；改由其专用契约要求每个动作各有两条真实、唯一、状态绑定的断言。两个动作重复检查永久牌组是合法的，同一动作重复断言仍拒绝。原 NativeChoices 的选择事件是直接 detail 元数据，专用检查器按真实 schema 读取，不把它当 Observe 的 facts/state 包装。早期检查器失败及控制记录均保留，未计入最终通过数字。

最终验证目录：`.build/runs/solver-choice-potion-work01/`。

- `verification.json`：50 项事实、指纹、隔离、规则和分层结果核对通过。它包含控制集合的汇总检查，不与控制次数相加宣传覆盖。
- `reader/historical-controls.json` 与 `reader/independent-controls.json`：生成器 final06 对前次 32 个控制及新增 11 个控制复验全部符合预期，原五反例全部拒绝。
- `checker10-legacy-confirmed/controls.json`：最终检查器重读四条旧原始路径，并拒绝原五个反例，9/9 符合预期。
- `checker10-controls/controls.json`：2 个正常/等价副本接受，18 个选择/药水/标签/捕获/断言错误副本拒绝。正常数据先通过，不能将早期正常数据也失败时的负控算有效认证。
- `profile-api-controls.json`：8 个真实方法控制通过，默认配置继续拒绝 CombatSolver/MyMod/BaseLib/Ritsu，求解器配置拒绝 MyMod/BaseLib 和错误依赖哈希。
- `generation-scope.json`、`build-id-controls.json`、`closeout.json`：生成链差异、重算身份、实际预算及限制。

固定游戏仍是 v0.111.0/41cef1ea；CombatSolver 仍是锁定的 0.50.1 Lab 衍生 DLL，SHA256 `4503D5234915B4B0C163556C6FDB4FA0C38F45796F656AB4B2C7E76A1BF7D98B`。15 个输入、52 个兼容源码和旧原始执行/工作证据锁未变。

成功记录继续使用 MemorySaveStore，ShouldSave=false、Steam 未初始化、无 UI 实例、单机网络服务、零遥测；MyMod/BaseLib 未加载。默认配置没有新的原生运行。本批求解器构建有既有 Publicizer 冲突和受非空前提保护的 nullable 警告；默认构建有仅在求解器配置赋值的 provider 字段警告；均为 0 error。

## 失败与预算

预登记上限为 4 次尝试、20 分钟、2 GiB；每个原生进程硬门 120 秒。

| 尝试 | 实际结果 |
| --- | --- |
| 01 | EngineError，5/5 确认；TrueGrit 已精确执行。错误将 live 药水对象传给 fork 槽，药水尚未派发 |
| 02 | DataError，6/6 确认；原生 FirePotion 实际 20 伤害，低层 ManualUse 未执行对应搜索效果，预测/实际不一致后停止 |
| 03 | 包装器在 manifest 落盘前启动，前置读取失败；没有原生游戏进程或 result.json。该失败仍计入尝试额度 |
| 04 | final06 的 live 成功，6/6 确认，两步精确差分及再次求解 |

共实际启动 3 个原生游戏进程，只有最后一个完整通过；没有执行完整重放。第 5 次调用被预算门在启动前拒绝。预算未重置、未补发额外原生尝试。所有失败和原始文件保留。

只读控制脚本曾因一个相对路径在结果汇总时失败；已核查其实际输出，保留不完整工作包，并在新目录用绝对路径补齐可记录退出码的定向检查。它没有触发原生执行。

## 下一步与未覆盖

本批先关闭了原动作绑定缺口，并完成必要选择和药水的固定 API 垂直验证。现有 `SolverLoop` 仍会主动拒绝子选择/药水；新桥尚未进入该在线部署主循环。因此第 2 步不能结束。

下一有界批次先补当前夹具同版本 live/replay，然后把共享的预测/原生动作执行桥接入 SolverLoop，实际采用求解器返回的首动作及其完整子选择，再从实际新状态重求解。需覆盖缺失/多余/晚到选择的失败前缀、非零重复实例、药水槽替换、必要连续选择、EndTurn 子选择和真实终态。遇到不支持的效果明确停止，保留候选、预算和原生前缀。

不要通过强迫求解器选择某张卡来把夹具说成策略成功。整场、Boss 与 RunVictory 留在后续步骤，最终 M1 同构建全套验收仍保留。本批没有修改角色、安装、用户存档或其他项目，没有游戏 UI/真实 Steam/网络操作，没有提交推送。
