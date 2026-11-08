# 新会话启动提示词：继续解决 Watcher 阶段 6 阻塞

你在 `D:\STS2-Simulator-Lab` 以独立 Agent 工作。

先读取并遵守：

- `AGENTS.md`
- `docs/agents/domain.md`
- `docs/mods/MOD_INTEGRATION_EXECUTION_PLAN.md`
- `docs/mods/stage-reports/06-watcher-first-battle.md`
- `docs/mods/stage-reports/06-watcher-new-session-handoff.md`

本任务继续阶段 6，只处理 Watcher。不要接入 Aemeath、RitsuLib 角色、MinionLib、Shadowverse 或其他内容。不要提交、推送、删除文件、重写历史或覆盖已有未提交修改。

固定输入：

- Workshop 快照：`local-mods/workshop-snapshot-20261106-163352`
- 快照摘要：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`
- 游戏：`0.111.0`，commit `41cef1ea`
- BaseLib：`3.4.7`
- seed：`LAB_V0111_WATCHER_STAGE6_01`
- 场景：`watcher-stage6`

用户已在测试版游戏中将全部 Mod 同时加载并运行，已排除安装环境层面的版本不兼容和 Mod 组合拒载。仍必须在仓库固定快照、`.build/pinned-inputs/sts2-0.111.0` 和新 NativeRun 进程中产生可审计证据。

当前红灯反馈回路：

```powershell
$env:SIMULATORLAB_WATCHER_STAGE6='1'
$env:SIMULATORLAB_WATCHER_SNAPSHOT_ROOT=(Resolve-Path 'local-mods/workshop-snapshot-20261106-163352').Path
pwsh -NoProfile -ExecutionPolicy Bypass -File tools/native-run/run-native-slice.ps1 `
  -BuildManifest .build/native-build/watcher-stage6-final/build-manifest.json `
  -OutputDirectory .build/runs/watcher-stage6-debug-<fresh-id> `
  -Scenario watcher-stage6 `
  -Seed LAB_V0111_WATCHER_STAGE6_01 `
  -ProcessTimeoutSeconds 120
```

已知结果：`process.json.rawExit=-1073741819`（`0xC0000005`）。ModManager 事件已看到 `BaseLib:Loaded:1` 和 `Watcher:Loaded:1`；崩溃发生在 Watcher 初始化或 Watcher 模型进入 `ModelDb` 阶段。原版 `slice-skip` 在同类 build 上退出码 `0`。

按 `/diagnosing-bugs` 工作：

1. 先重跑红灯命令，确认同一失败。
2. 最小化为原版、BaseLib、Watcher 程序集、Watcher Character、Watcher 卡牌/遗物/Power 分组对照；每次只改变一个变量。
3. 列出 3–5 个可证伪假设，再逐一做定向 instrumentation。日志使用 `[DEBUG-WATCHER-...]` 前缀，修复后全部移除。
4. 优先查 `Watcher.Code.WatcherMainFile.Initialize`、Harmony/资源初始化、Watcher 类型静态构造、`ModelDb.Init` 类型集合和 BaseLib 内容发现顺序。
5. 不用跳过初始化、空实现、静默忽略异常或“加载成功”作为兼容结论。
6. 修复后重新跑原版回归、Watcher 真实战斗、奖励边界、清理和两个新进程重放。

如果仍无法在不改变玩法语义的前提下越过 Godot 原生边界，输出精确 `RuntimeFailure`/`Unsupported`、保留失败证据并更新阶段报告；不要进入阶段 7。

完成后写交接报告，包含阶段状态、分支/status、修改文件、命令结果、逐项验收、已知缺口、下一唯一入口、是否提交/推送。不得自行进入下一阶段。
