# 4D3 跨房历史上下文绑定修复

本轮修复[动作阶段独审](NATIVE_RUN_N3D3_ACTION_PHASE_REVIEW.md)指出的8个误接受副本。本轮同构建本地验证已完成，**待独立复审**；最终入口为`.build/runs/n3d3-transition-work01/closeout.json`。本轮不代表完整4D3或M1完成。

## 生产契约

问题在于：Travel / MajorFixture已经处于合法阶段，仍可以在内部combat-history中记录错误act/node/旧房间，随后在根确认处恢复。原有限动作门继续保留，跨房豁免改为明确的本次到达契约。

生产修改限定`tools/native-run/NativeMajorContract.cs`：

1. 跨房dispatch登记当前根和空历史集合；内部历史继续由共享契约核对根、上下文信封、epoch和index。
2. 每条历史立即核对本次dispatch幕号。Travel节点必须是本次合法目的地；MajorFixture节点必须保持原API的dispatch节点。历史须处于CombatRoom，rooms.reference与majorCombat.room完整相等，并指向新房，不能借用退出的旧房。
3. 暂存位置与不可变战斗身份投影，在根确认时与实际到达投影逐条完整结构比较。majorCombat仅去掉固定版本中可变的inProgress和intentAttackers；其余字段及未来嵌套扩展均进入比较，不逐字段放过新增身份内容。
4. 跨房确认本身也核对幕号/目的节点，随后必须保留既有唯一room-entered及完整确认状态绑定。历史集合在确认后清空；成功收尾不允许遗留未确认集合。

这不会把资源、牌堆、HP、RNG或历史整份state锁成dispatch/确认值。原生进入新战斗时的抽牌、力量及资源变化保留，比较的范围是位置与战斗身份。当前有限Major流程的155条跨房历史均在新战斗内产生；若以后增加退出阶段的旧房历史，应另行声明阶段，不能使用整段无约束豁免。

## 修复及证据范围

旧冻结`17C6C990BAD45E733D0210C373BAE162BE319B709E4FE391700808C3CF922247`上，8个副本全部重新复现exit0 / passed，证据位于`.build/runs/n3d3-transition-work01/before.json`。新reader的初始13项定向检查中，8个坏副本语义拒绝，5个正常包接受；包含详细、minimal及off，合法跨房和真实死亡保留。见`gate.json`。

新增`tools/native-run/major_transition_controls.py`直接调用生产reader：重验前轮152份审查副本（原29反例、82阶段负控、21独立联动控制、8个本轮反例及正常/不完整/等价包），并对Travel/夹具的首条、末条、整组历史，覆盖act/node/旧房/Encounter/type/奖励属性/嵌套身份或引用扩展的损坏。另有缺失/null/错误类型、历史及确认后缀联动改写、完整属性重排和一致身份扩展。

当前正常包作为原生见证：逐条比较历史与到达的位置/战斗身份，同时确认至少部分历史完整state真实不同于最终确认，避免通过锁死资源或RNG获得全绿。所有损坏只写显式副本并重新封装，历史来源构建身份保持。历史reader调用不算新原生执行。

冻结`53186436B11E78EED9FFFAF8214EF68A960D28B513EE04151BFD70557E392188`，位置`.build/native-build/n3d3-transition-final01/`，实际编译0 warning / 0 error。最终dirty源码、来源和产物快照保存在`.build/runs/n3d3-transition-work01/`，源码差异仅检查器和新增控制脚本，未删除用户文件。

## 最终结果

十组原生套件完成 **309次实际执行 / 3908项套件检查，另有实际Core98**。原始退出保留226×0、70×1、8×2、5×3，未将Unsupported、预算、执行/导出故障当作胜利。12个最终任务全部完成后才收尾。

最终追加 **240次生产reader调用**全部符合预期，包括原29反例拒绝、82阶段负控拒绝、8个本轮反例拒绝及21个独立联动控制保持；另有76个新的跨房历史控制和正常/不完整/等价副本。47项原生位置/业务变化见证通过；5个当前正常包中，详细模式真实历史state可不同于最终确认，而其位置与战斗身份对应实际到达。所有损坏副本有效重新封装，不依赖损坏哈希拒绝。

732项来源、退出、回放、历史保持及反例核对通过。79份dirty源码快照、48份编译输入、3份游戏输入和7份产物绑定同一冻结；15锁定输入与52兼容源码保持。源差异仅本轮检查器和新增控制脚本，没有删除文件。

原生行为保持：3513条普通战斗位置历史、155条合法跨房历史及13条真实Death转换仍成立；精英/Boss Death51/51、自然精英Death93/93、Unsupported35/35与99/100、EngineError3/4、BudgetExceeded4/4、DataError51/51保持。live/replay/minimal/off完整route/choices/reward-choices/final-state（含RNG）相等，旧原生行为也保持。

冻结`53186436B11E78EED9FFFAF8214EF68A960D28B513EE04151BFD70557E392188`；Native DLL SHA256 `CAD4C5DBCB071E1D0BE3237A83ED3F693AF027D66F3F3A26750E1737C4A18261`。证据为本轮`closeout.json`、`facts-final01.json`、`native-context-facts.json`及`.build/runs/n3d3-transition-final01-controls/summary.json`。复现控制入口为`tools/native-run/major_transition_controls.py --manifest <build-manifest> --source <final-major-suite> --output <fresh-Lab-runs-directory>`；`run-final01.py`需改成全新输出目录后才可重新执行，不能覆盖已有证据。

## 剩余限制及后续

精英/Boss实际Victory、目标奖励、act-ready及正常开局完整Run仍未验证。首场Monster胜利、目标战斗真实Death和专用夹具不关闭这些门。修复独审通过后，继续[既有N3D3后续交接](NATIVE_RUN_N3D3_NEXT_HANDOFF.md)及M1步骤5–6。

15锁定输入与52兼容源码继续冻结；不扩Mod适配或公开SDK。本轮未改角色、安装、用户存档或宿主权限，未运行游戏UI、真实Steam/网络或提交推送。保留所有历史失败和原始证据。
