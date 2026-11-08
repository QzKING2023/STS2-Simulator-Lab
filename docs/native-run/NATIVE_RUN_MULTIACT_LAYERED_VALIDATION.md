# 自然 Boss 与第二幕首战：分层验证

状态：本轮分层实施验证通过，交独立审查；**完整 M1 仍在进行**。已从旧 API 目标夹具推进为同一正常开局 run 的自然地图 Boss、奖励、跨幕、Ancient 领取和第二幕首战/奖励。

## 当前实质覆盖

固定游戏 v0.111.0 / 41cef1ea、Ironclad/A0、独立离线档案。DEV03 实际完成 206 个确认动作：

`正常 Neow → 第一幕真实地图/战斗/事件/休息/宝箱 → THE_KIN_BOSS Victory → 原生奖励 → ready/EnterNextAct → 第二幕 Tezcatara → 原生营养汤领取/附魔 → BOWLBUGS_WEAK Victory → 原生奖励`

最远为 act index 1、floor 2、`MapCoord (2, 1)`，HP75、金币361。该条路径没有 `EnterRoomDebug` / `MajorFixture`，没有手动改 HP、资源、牌组或奖励。DEV01/02 仍分别在 93/156 个确认动作后真实死亡，未换样本；重复执行不计独立胜率样本。

详细数据可重算第二幕首战：入战 HP80、胜利清理前69、原生清理后75；五条 DamageReceivedEntry 合计格挡24、未格挡11、过量0。使用 DamageReceivedEntry 单一来源，不与 CreatureAttackedEntry 双计。`.build/runs/native-multiact-work01/data-sample.json` 还关联了附魔 Strike 的永久实例、战斗副本、动作/root/context 和实际 DamageResult。例如 a192 的 Strike 副本 i409 来源于永久 i3，序列化包含 TezcatarasEmber amount1，该次实际未格挡伤害9；该数值没有被当作独立平衡公式。

## 冻结和证据

- 当前冻结：`02479D358FE8B4311A0B164D02A62BE8546C3DD9BDEF86A07B399A427311FD42`。
- Native DLL SHA256：`13B5ADA8EC5EEB85E55CEB91BAD794570898C52FA372782735506415385FE1DB`。
- 清单：`.build/native-build/native-multiact-final01/build-manifest.json`；88 实际源码、53 编译输入、3 游戏输入、7 产物，0 warning / 0 error。
- 运行：`.build/runs/native-multiact-final01-layered/`；收尾：`.build/runs/native-multiact-work01/closeout.json`。
- 222 项冻结/来源/输入检查，15 锁定输入和52兼容源码保持；Core98 的实际检查及 Core DLL 身份一致。

本构建的分层队列是 **11 次实际原生执行、104项检查、18个生产副本控制**，无失败：DEV01/02/03 各执行/新进程冻结重放，DEV03 再作 minimal/off 对照，另有第一幕 Boss 至第二幕首决策烟测及原版 skip24 / take25 烟测。四份完整 JSON（动作、选择、奖励选择、final-state 全 RNG）精确比较。

另有同构建一次损坏路线真实重放：185/185 前缀保留，错误起点在派发前 InvalidAction，退出1，来源明确 `fault-control / invalid-route`；并有一个联合修改 Soup 任务返回、确认和后缀的副本，生产检查器确实在独立 Soup 规则拒绝。两项均符合预期。因此本轮同构建共12次原生进程，副本控制合计19个，不能把预期拒绝计成成功通关。

当前检查器对78份历史副本重读均符合预期：原34/6反例、已通过的首节点33控制和5个正常历史证据。全部副本独立复制，来源哈希保持；这是当前检查器的兼容性/负控证据，**不是本构建新执行的78条游戏路径**。旧34例缺后加的必需条件观察，单凭它们的拒绝不代表各项语义被重新验证；当前格式控制及自然烟测另列。

按用户补充的默认规则，公共归属检查器另对既有冻结的323份实际原生原始证据，以及本轮12份原始证据作完整重验，**335/335符合预期且来源保持**，输出在 work01/full-frozen-evidence-review；它们保留各自 producerBuildId。该层不重启游戏执行，也不把历史记录改标为当前构建的原生执行。源码、输入、构建、采集器及环境核对位置为 work01/freeze-verification.json、fingerprints.json 和 environment.json；这些哈希一致不单独证明执行真实性。

PowerModel.Icon 新展示适配的实际 Harmony API 夹具验证了“已有未知补丁拒绝、正常注册、后加补丁导致清单变化拒绝”，未调用目标 getter 或游戏业务。初始化不完整的早期探针失败也保留，修正为调用真实 bootstrap bypass 初始化后通过；不算零售 UI 等价或玩法执行证明。

## 具体改动与边界

`NativeBoundaryContract.cs` 统一动作/context 所有者、具体房间和不可变完整快照校验；`NativeStageLedger` 提供按所有者的必需/唯一阶段；`NativeInstanceLedger` 保存不可变的操作前实例集合。Major 和新自然入口已复用这些基础操作，尚未声称所有历史控制器已迁移。

自然地图使用实际生成的完整有向图及 generation/地图身份；策略只按公开节点和边排序，采用有界 DAG 记忆化，未测量加速倍率。新的 `major-natural-second` 配置执行原生 NutritiousSoup 选项，再沿合法边进入下一战，未找到该选项时明确 Unsupported。

为记录原生附魔，第二幕配置加入永久牌、战斗副本和奖励来源的原生序列化。数据结构必须存在且绑定模型/升级/永久来源；奖励卡入牌组允许原生写入 `floor_added_to_deck`，绑定同动作记录的实际 run floor，其他内容保留比较。原生 Soup 对未附魔 Ironclad Strike 的 amount1 变化另有固定版本独立断言。其余复杂 native props 仍以实际原生业务和重放为来源，没有复制整套牌效解释器。

无 UI 依赖方面，Tezcatara 选项构造曾在 PowerModel.Icon 的 ResourceLoader 调用 native crash；原始退出码 -1073741819 和185步前缀保留。适配只对该纯纹理 getter 返回 null，原生选项生成和三次 RNG 消耗保留。NCardEnchantVfx.Create 的原生 TestMode 分支直接返回 null，无需替换 Soup 的领取或附魔逻辑。

信任和退出标准见 [证据边界](../architecture/NATIVE_RUN_EVIDENCE_TRUST_BOUNDARY.md)。目标是发现缺失、重复、归属、顺序和静止状态错误；对整份日志的任意一致伪造不作不可实现的证明承诺。

## 断连恢复、失败和全量门

恢复时已核查：`natural-boss-final02` 没有输出目录/进度和残留 Python/dotnet 执行进程，确认未启动后才启动定向队列；没有重复运行未确认批次。此前 final01 大批次因已证实身份缺口被标为 superseded，后来出现的 source-binding 失败及断连留下的“running”进度不能当作完整通过。

旧 final02 的自然切片9次/126检查/33控制已完成并保留，但与当前冻结分列；早期非法重放夹具缺少选择文件、错误来源标签和缺失实例检查均已修正，没有拿错误的零步失败替代185步拒绝证据。

本轮按用户批准的方式完成针对性验证、历史重读和原生烟测，**没有声称当前构建已完成旧323场全量队列或整个M1验收**。最终同构建完整回归、默认 Acts 自然 RunVictory、评估 EVAL01–06 以及必要数据门仍保留，待多幕生命周期稳定后执行。

重跑范围的依据：归属/快照基础检查器复用变更采用完整冻结原始证据检查；自然第二幕调用和卡序列化生成链采用新12进程定向执行及原版24/25烟测；采集器 NativeObservation 的实现、Core 和工具链未作全局更换。新卡序列化仅由独立 continuation 配置启用，旧配置分支保持；源码/输入/构建/采集器指纹及环境另存 work01，局部范围仍有最终完整 run、完整数据与全量回归风险未关闭。若后续共享采集器全局变化或这些假设不成立，则扩大原生重跑。

当前仍待组合：按 room/session 所有的多次宝箱/奖励、每幕 Boss/act transition 状态、其他 Ancient/选择、商店与事件战斗的通用流程，以及最终 Architect/RunVictory。详细历史中的 Player、ResourceInfo、部分意图/MoveState 仍明确标 unavailable；实际伤害/格挡、资源前后、永久牌及已盘点 RNG 可查询。不能声称必要数据全集、全原版内容、公平信息策略或可靠胜率已完成。

```powershell
python -P tools/native-run/run-native-multiact.py --manifest .build/native-build/native-multiact-final01/build-manifest.json --output .build/runs/<fresh-layered-dir>
```

复现需当前源码/输入仍匹配清单；后续代码变化后使用新构建，不覆盖本轮证据。历史重读和额外故障入口脚本在 work01 中保留。下一包见 [继续多幕交接](NATIVE_RUN_MULTIACT_NEXT_HANDOFF.md)，无需重新索取已批准的M1授权。

无环境或权限阻塞；无 UI、真实 Steam/网络/存档、角色/安装改动或提交推送。
