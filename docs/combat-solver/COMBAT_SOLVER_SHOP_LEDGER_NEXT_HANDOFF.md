# 商店账本后的最小交接

2026-11-04。M1 持续授权范围内，当前批次已关闭；不重新打开或重置其 3 次执行额度。

## 先补一次准确重放

保持生产构建 `.build/native-build/solver-shop-ledger-final06/`，ID `C31B3A2D3C8DF2A0D455A03148CC6862822E73A5219C4BAED638CF9F64D03FC2`。最新真实结果为第三幕 SoulNexus 精英胜利，407 确认 /408 派发、319 精确战斗差分，领奖中途 Unsupported；不是 RunVictory。

原重放失败是执行命令误用了 live 场景，并非输入内容被证实有误。下一有界批次使用：

- 场景 **`solver-native-darv-act2-replay`**，不要再用无 `-replay` 后缀的 live 场景。
- manifest：上述目录 `build-manifest.json`。
- route：`.build/runs/solver-shop-ledger-work01/replay-input/route.json`。
- plan：`.build/runs/solver-shop-ledger-work01/replay-plan.json`。
- route 的末尾是实际派发 a408，`after/confirmed=null`，没有捏造完成。choices/reward-choices 包含已发生的领奖前缀。
- 单次原生重放，显式 full-run v2 的 450 秒 run /480 秒进程、4 GiB 内存；沿用输入哈希及独立新输出。逐步比较实际动作、完整状态、RNG、子选择和失败前缀。失败照录，不默认再次启动。

exec-03 的 0 确认/0 派发 InvalidAction 保留；不要更改其调用参数或结果文件。补齐后再独立审查本轮有限声明范围。读取[本轮验证](COMBAT_SOLVER_SHOP_LEDGER_VALIDATION.md)及生产正负控，避免无必要全量重跑。

## 下一实际准入停点：MummifiedHand

`NativeRewards.CheckMajorRelic` 仍限定 MealTicket / Orichalcum 两个型号，因此 MummifiedHand 在实际 `OnSelect` 前被拒绝。错误文本说“acquisition hook outside finite audited contract”，但不能据此断言该遗物有特殊拾取 Hook。

已只读核查：固定游戏 MummifiedHand 覆盖的是 AfterCardPlayed；未在该类发现专用 AfterObtained。固定 CombatSolver 0.50.1 的 `source/src/Engine/InCombat/Mirrors/Hooks/Card/AfterCardPlayedMirrors.cs` 已注册 `HandleMummifiedHand`。这证明存在可复用实现，尚不证明本 Lab 路径已完成拾取或后续差分。

后续应先复核实际默认继承拾取、真实来源/方法/补丁审计和现有原生奖励生命周期，推广已存在的默认持久遗物取得契约，避免逐个型号加白名单；不移除审计、不手动添加遗物。新拾取实例、原生奖励完成、所有合法资源/RNG 变化及后续求解适配需各自验证。战斗效果优先复用上游已注册镜像，未经验证的部分保持明确 Unsupported。

a408 已取得 39 金币和 ExplosiveAmpoule，MummifiedHand 尚未取得，CardReward 尚未处理。保留这一真实部分完成前缀，不能回滚、跳奖或拼接整局成功。

## 持续边界

不改游戏/角色/用户存档或安装目录，不启动 UI，不调用真实 Steam/网络，不提交推送；15 输入与52兼容源码固定。原生宿主、可选预测与策略能力分层声明，第三幕 Boss /RunVictory 与最终 M1 同构建全量验收仍待完成。资源账本保留完整状态检查，未知补货/替换和预持折扣原价边界不悄悄放宽。
