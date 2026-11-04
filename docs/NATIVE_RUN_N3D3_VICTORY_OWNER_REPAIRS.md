# 4D3 胜利条件动作归属修复与统一验证

状态：实施和冻结回归完成，待独立复审。原反例、旧失败、API 夹具边界保留；M1 仍在进行。

冻结 `6C39707A0FF4DB94DB7E645BD2014952845193A17B55364DAC96030206D3E61D`，Native DLL SHA256 `5D3A923281BADAE4E9D18FB8AF72FEB1A786EAE703C6A2FDAD41A0A741876ECC`；实际 dirty 源码82文件、49编译输入、3游戏输入、7产物。相对前冻结仅改 NativeMajorContract.cs 与 preparation_repair_controls.py，原生执行器、策略、角色代码未改。

## 修复与契约

每次合法 dispatch 后清除前一动作的完整条件锚点、root/context、游标与清理标志。major-victory-condition 保存当前动作 root/context 和完整快照，允许同动作多次 true 观察更新；胜利回调要求完整状态等于本次条件观察且归属一致；history-clearing 和清场核对该归属、游标和已确认动作。原物理事实、条件来源、不可变战斗上下文、清场/奖励/跨幕框架继续生效。没有要求最后一条 combat-history 与胜利快照相等，也没有要求开启 detailed 才认证。

先在旧冻结 B1FB0F…917DDD 复现四个原坏副本，实际均退出0。修后用新冻结重读六个原归属副本（四坏及两个 detailed 拒绝对照）均退出1；六个正常样本均退出0。副本和日志在 work01/before 与 targeted，原审查不改。

## 验证

- 同最终冻结重跑323个原生执行、4074项suite检查及实际Core98；13个顶层任务均完成。原始退出码240×0、70×1、8×2、5×3分别核对，Death/Unsupported/预算/执行错误/数据错误前缀保留。
- 当前生产控制工具202次调用：174拒绝、28正常/等价通过，0意外。包括原34反例、原148矩阵及新增54项归属回归：6原审查副本、2目标×3观察模式×6种缺失/跨动作/上下文/回调/清理错配、6实际重复条件正例、6同动作追加等价条件正例。
- 467个上一独审副本重读全符预期；原四个误接受现在均拒绝。其7个旧读取器正例单列为格式对照，其他用当前DLL；这不是467条新原生运行。
- 历史247控制条目及47实际原生边界核对符合预期；七旧格式有旧读取器0 / 新读取器1 / 新格式替代0的明确迁移。新增必需条件门未弱化。
- 1684项源码/产物/原生结果与隔离核对通过；15锁定输入、52兼容源码、历史证据未改。完整路线、选择、奖励选择、final-state（含全部RNG）在同构建详细/重放/minimal/off一致，五个基础样本与前冻结的原生结果一致。

准备目标仍为明确 API 夹具：精英DEV03实际113动作、5准备战斗后取得BYGONE_EFFIGY_ELITE胜利与Gold35/PommelStrike/MealTicket，HP12→18；BossDEV03实际184动作、9准备战斗后取得CEREMONIAL_BEAST_BOSS胜利与Gold100/DarkEmbrace，HP11→17，并真实ready/EnterNextAct至act1/floor0/node null/新MapRoom/mapGeneration2。BossDEV01/02途中Death（93/156动作），自然DEV01为Elite Death（77动作）。未重标为自然胜利。

Major遗物取得仍仅审计MealTicket的无拾取效果契约；未知Major遗物在原生领取前Unsupported。未覆盖自然Boss胜利、第二幕首节点/Ancient、通用多幕组合、默认Acts完整RunVictory及非空次要敌人终局。旧Harmony API审计是未改宿主方法的历史证据，本轮没有新增patch或重做该API探针。

## 证据与复现

统一构建 `.build/native-build/n3d3-victory-owner-final01/build-manifest.json`；原生矩阵 `.build/runs/n3d3-victory-owner-final01-*/`；新增控制 `...-review-controls/reader-summary.json`。work01下有源码快照、driver、原始日志、verification/facts与closeout；review-regression保存467份副本和生产reader输出。复制来源均有hash锁；无修改原证据。旧四例与新格式同根因控制分别存在，避免只因格式缺项拒绝而假通过。

```powershell
python -P .build/runs/n3d3-victory-owner-work01/run-final01.py
python -P tools/native-run/preparation_repair_controls.py --manifest .build/native-build/n3d3-victory-owner-final01/build-manifest.json --source .build/runs/n3d3-victory-owner-final01-prepared --output .build/runs/<fresh-control-dir>
```

完整runner输出目录必须全新；已落盘结果勿重复覆盖。已有driver固定输出用于记录确切命令，复跑需复制并替换全部输出根。先独立验收本修复，再接既有PREPARATION_NEXT_HANDOFF的自然Boss、下幕首节点与通用run组合，持续M1步骤5–6。

本轮未启动真实UI、Steam/网络或用户存档，未改Mod/安装，未提交推送。没有环境或授权阻塞。完成声明仅为实施验证，待独立复审。
