# 默认遗物原生拾取与重放验证

2026-11-04。本轮实现待独立审查；原版 M1 尚未完成。

## 实际推进

- 使用正确 `solver-native-darv-act2-replay` 入口补齐上一轮冻结 `C31B3A2D3C8DF2A0D455A03148CC6862822E73A5219C4BAED638CF9F64D03FC2` 的重放：407 确认 /408 派发、319 个战斗差分，325 个路径/选择/完整状态文件比较无差异。原来的零派发 InvalidAction 文件原样保留。
- 新在线执行实际完成 SoulNexus 精英奖励：原生领取金币39、ExplosiveAmpoule 和 MummifiedHand（实例 i982），卡牌奖励合法跳过；随后进入休息点并回血至105/105。
- 最远位置第三幕第11层 `MapCoord (4, 10)`，SlipperyBridge 入口尚未审计，410 确认 /411 派发，原始 `Unsupported/exit2`。没有跳过该事件或把部分 Travel 记为完成。
- 战斗仍为此前19胜、319个原生/分支差分步。取得遗物后没有执行下一场战斗；不能称 MummifiedHand 战斗效果已在本路径差分验证。

## 通用实现与复用依据

`NativeRelicPickup` 使用固定原版程序集的实际类型、ModelId、原生方法声明及 Harmony 所有者检查，替代奖励领取的两个型号白名单。只准入默认 `RelicModel.AfterObtained`、不声明即时拾取效果的原版持久遗物；仍调用原生奖励 `SelectLocalReward`，要求实际 ClaimedRelic 与新入库引用一致。完整状态只允许追加真实遗物实例，其他资源/RNG变化明确 Unsupported，未支持自定义拾取、拒收、替换或未知副作用。

战斗模型独立由现有 CombatSolver 根审计准入。固定 `combatsolver-0.50.1-ending-powers02` 已在 `AfterCardPlayedMirrors.cs` 注册 MummifiedHand handler，继续复用其机制；本轮未改搜索评分或上游镜像。

生产 `NativeRelicPickupEvidence` 校验奖励集合/选择/原生开始/准入/完成/返回/根确认关联、实例与来源、完整状态及顺序，并保留旧证据格式。`NativeRewardContract` 仅在新 header profile 下采用通用类型解析，历史白名单证据仍可重读。

## 修复过程中保留的失败

初次来源负控尝试给继承 MethodInfo 打补丁，被 Harmony 拒绝；不能算审计通过。独立小型 API 探针随后对真实基类声明方法注入，发现继承句柄的 GetPatchInfo 漏检。已统一将方法解析为声明类型中相同 Module/MetadataToken 的真实方法；修复后实际注入被拒绝。6项来源控制通过，其中实际 Harmony 控制是元数据/API 测试，业务执行为0。

第二个在线构建 `41DBAA4F4986F77959C8BC779F8CA559D5862B0D2708DF4D4E3DAE497938380F` 保留这次发现，不称其未知补丁审计已通过。最终冻结 `6C02AFDC464AA71611330AAA56ACD74AA4795CFE94FB58BBDD3432D6220FB721` 仅修改 `NativeRelicPickup.cs` 的声明方法归一化与负控入口。最终回放与在线来源不同构建，不能写成最终构建 live/replay 双跑。

第三次运行的首次包装器尝试因外层重定向占用包装器自己的日志而失败，尚未 Popen、无 wrapperPid、无输出目录。原日志和账本快照保留；确认零启动后恢复原第三个名额，未新增额度或重置账本。

## 验证与证据

主目录：`.build/runs/solver-relic-pickup-work01/`；原始执行分别为 `solver-relic-pickup-exec-01/02/03`。

- `old-replay-check.json`：旧冻结同构建重放325文件一致。
- `source-controls.log`、`source-audit-declared.log`：失败和真实审计漏检证据；`source-controls-fixed.log`：6项修复控制。
- `relic-control-summary.json`：18项生产控制（2合法、16拒绝），覆盖缺失/重复/顺序、动作/上下文、旧实例、型号、方法、额外字段及完整状态/返回。
- `live-review.log`、`legacy-review-final.log`：最终检查器重读当前与历史原始证据，不替代原始 exit2。
- `binding-check.json`：最终1138个源码/输入/产物哈希一致，15输入+52兼容源码共67项不变；哈希仅证明绑定一致。
- 最终构建回放实际达到相同410/411 Unsupported；325文件（含319个完整战斗差分文件）与在线来源完全一致。这是跨构建语义重放，不是最终构建live/replay双跑。预算账本已关闭，3次实际原生执行，1513.52秒/1800秒，1,093,401,445字节/2GiB；4623份原始文件已哈希固定。具体比较与关闭结果见 `replay-check.json`、`raw-evidence-lock.json`、`closeout.json`。执行上限3次/30分钟/2GiB，单进程480秒、run450秒；不会为本轮有限修复重跑无关全量矩阵。

## 未完成

SlipperyBridge 原生入口与选择、取得遗物后的战斗差分、第三幕 Boss、RunVictory、最终 M1 同构建全量验收均待完成。特定拾取效果不在本默认契约内。既有预持折扣原价和其他商店限制不变。未修改角色/安装/真实存档，未启动UI或提交推送。
