# 4D3 动作集合与生命周期阶段修复

本轮修复 [上下文独审](NATIVE_RUN_N3D3_CONTEXT_REVIEW.md) 的动作分类绕过。本轮同构建验证已完成，**待独立复审**；最终本地收尾入口为 `.build/runs/n3d3-action-work01/closeout.json`。本轮不代表完整 4D3 或 M1 完成。

## 根因与生产修复

原检查器核对了 root-request、dispatch 和 confirmed 三份动作的一致性，但使用未经阶段校验的 kind 选择位置规则。三份一致改成未知动作或战斗中的 `EnterAct`，即可避开战斗位置约束。六个反例已在旧冻结 `98EE6B390F01A94AFB407F2D1C6576F59AE09A9D853EE1A2161CE5F5EA526FFC` 的 reader 上重新复现，全部 exit0 / passed，保存于 `.build/runs/n3d3-action-work01/before.json`。

生产变更限定于 `tools/native-run/NativeMajorContract.cs`。新增有限动作类型，先在根请求和派发处按不可变已确认状态及已观察的生命周期校验，再设置战斗位置检查标记。未知名称、数字枚举、大小写或组合伪类型均拒绝。此规则没有改变原生命令、卡牌效果、资源、策略、预算或输入。

| 已登记动作 | 必需阶段及依据 |
| --- | --- |
| EnterAct | 唯一 new-run 完整快照、零派发、act0 且尚无地图节点/房间；仅初始进入 act0 |
| EventOption | 初始进入已确认、Neow 尚未选择、原生候选完整且绑定已确认状态；决定必须对应原生未锁选项 |
| Travel | Neow 完成及计划已记录；当前房间允许离开，前战奖励完成；仍检查真实地图边、完整状态和到达 |
| MajorFixture | 同样要求前战闭环；另保留原有声明、目标、具体新房间和奖励前提 |
| PlayCard / EndTurn / UsePotion | 当前具体战斗已开启且未胜利/清理/领奖/死亡；实际 inProgress；全部带状态观察继续绑定不可变 act/node/房间 |
| RoomOption | 当前已到达的事件或休息房间，尚未离开且前战已闭环；保留当前请求、候选实例及结果边界 |
| RewardOffer | 当前战斗真实胜利并清理，已开始且未返回的奖励会话；战斗实际停止 |
| ActReady | Boss 场景的真实目标胜利与奖励之后，尚未进入下一幕；原有真实任务、快照及持久状态检查保留 |

同时约束开局候选、开局完成及计划观察的唯一性与顺序，避免只限制 EnterAct 参数形状。实际 Neow 候选逐项对应原生 rooms.eventOptions，确认后观察必须保持完整状态。

## 验证与证据类别

定向新 reader 已拒绝全部六个旧反例；五个正常当前证据（精英、Boss、自然路径和 minimal/off 观察模式）通过，详见 `.build/runs/n3d3-action-work01/gate.json`。拒绝原因是动作集合或生命周期阶段，未依赖损坏 seal。

新增 `tools/native-run/major_action_controls.py`：实际调用生产 reader 重验上轮 277 个副本，包括原 23 个位置/死亡/身份反例、六个分类反例及历史正常/故障控制；再对实际存在的每一动作阶段实施其它已登记阶段替换，使用相应真实决策形状，三份动作共同改写；加入未知类型、开局阶段记录缺失/重复及当前正常和失败前缀见证。PlayCard/EndTurn/UsePotion 共享战斗阶段，本轮不将互换种类称为牌效语义验证。

所有篡改限于显式副本并重封装。旧证据保留原构建身份，仅用于新版生产 reader 的兼容与拒绝验证，不计为新的当前原生执行。自然跨房 Travel/MajorFixture 历史可以属于新房间，不能误套固定战斗位置规则。正常、等价和真实业务变化继续由原适用套件验证。

统一构建：`17C6C990BAD45E733D0210C373BAE162BE319B709E4FE391700808C3CF922247`，位置 `.build/native-build/n3d3-action-final01/`。0 warning / 0 error。保存实际 dirty 源码及哈希，不以 HEAD 代替源码；完整来源快照和差异清单在 `.build/runs/n3d3-action-work01/`。15 个锁定输入和 52 个兼容源码需在收尾再次核验。

## 最终结果

最终十组原生套件完成 **309 次实际执行 / 3908 项套件检查，另有实际 Core98**。原始退出保持 226×0、70×1、8×2、5×3，不支持、预算、执行故障及导出故障保持各自分类和确认前缀。12 个最终任务（十组原生、Core、动作阶段控制）均完成，未用外层 exit0 代替内层结果。

最终追加 **369 次生产 reader 调用 + 5 项原生位置见证**全部符合预期。前轮277个调用在新 reader 上全量复验，原23及本轮6个反例全部拒绝；另有82个动作集合/阶段负控、5个新正常包和5个新失败前缀包。所有副本有效重封装，未依赖损坏 seal 拒绝。历史副本保留自身构建，不混算原生执行。

730 项来源、产物、实际退出、回放、历史保持及目标反例核对通过。最终78份dirty源码快照、48份编译输入、3份游戏输入和7份产物逐一绑定；15锁定输入和52兼容源码不变。源差异仅生产检查器及新增动作阶段控制脚本，未删除文件。

详细轨迹保留3513条固定战斗位置历史、155条合法跨房历史和13条真实死亡转换。精英/Boss Death51/51、自然精英Death93/93、Unsupported35/35与99/100、EngineError3/4、BudgetExceeded4/4、DataError51/51保持。live/replay/minimal/off完整route、choices、reward-choices、final-state（含RNG）相等，旧原生行为副本也保持。实际目标胜利与奖励仍未验证。

冻结 `17C6C990BAD45E733D0210C373BAE162BE319B709E4FE391700808C3CF922247`；Native DLL SHA256 `D18E68C9143188FC00BFF205CF7C585DD76FE879898544B1A521B7A52B58E49C`。复现入口：`run-final01.py`（必须修改为全新输出目录再运行）、`inspect-final01.py`（读取已保存矩阵），以及 `tools/native-run/major_action_controls.py --manifest <build-manifest> --source <final-major-suite> --output <fresh-Lab-runs-directory>`。所有脚本及原始失败证据保留在本轮目录。

## 剩余范围

精英/Boss 胜利、目标奖励、实际 act-ready 及正常开局完整 run 仍未验证，不能由真实死亡或专用夹具替代。修复独审通过后，继续 [4D3 后续交接](NATIVE_RUN_N3D3_NEXT_HANDOFF.md) 的合法资源准备、目标胜利与奖励/跨幕，再进入 M1 步骤 5–6。

未修改 Mod、游戏安装或真实用户存档，未运行游戏 UI、真实 Steam/网络或提交推送。保留所有历史证据和现有 dirty 工作。
