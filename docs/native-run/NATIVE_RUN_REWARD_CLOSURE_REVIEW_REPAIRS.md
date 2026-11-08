# 奖励闭环三项审查修复

当前：**三项修复及同最终构建统一回归通过，待独立复验。** 完整 M1 继续待后续节点与跨幕工作。

基于 [独立审查](NATIVE_RUN_REWARD_CLOSURE_REVIEW.md) 修复生产检查器，范围为三份项目源码：`NativeRewardClosureContract.cs`、`NativeRewardContract.cs`、`reward_closure_controls.py`。原生任务、卡牌效果、观察投影、比较字段和 Host 审计不改。完整分支契约见[修复契约](NATIVE_RUN_REWARD_CLOSURE_REPAIR_CONTRACT.md)。

## 三项修复

1. **Sozu 结果与实际取得**：当前 closure 采购只声明 Sozu 空槽/满槽拒收，独立要求原生适用持有者、NotAllowed、success=false、actual=null 和完整状态不变。外层可完成、claimed 保留，但不能将 claimed 充当 actual。未声明的采购分支拒绝，旧 4C 的普通成功、TooFull、丢弃重试继续原契约回归。
2. **父子静止状态**：首次/后续 card-menu 与父 reward-item 最新状态、直接 card-add 与父 RewardSetup 状态完整绑定；RewardOffer 及本轮带直接 Add 子项的 RewardSetup 返回绑定最后子返回。真实 Populate、reroll、Add、Neow/跨幕 Setup 的业务变化仍按各区间处理，未将所有业务区间一律判静止。
3. **实例、类型与升级**：所有永久牌组 id 唯一，actual 必须不在旧永久牌组，保留旧内容/顺序；selected 与 actual、修改者和 native 内容交叉绑定。登记类型须是固定原生程序集内对应类别的真实类型并匹配 model。卡牌 Fact/永久牌组升级与原生 `current_upgrade_level` 一致，省略默认零按零处理。reader 只查类型元数据，不初始化 ModelDb。

## 已完成的实证

最终冻结为 `AFB0E42BE7173FFDF371CE7E0DDBEC605430151608F171177F9436C12C289A52`，位于 `.build/native-build/reward-closure-repair-final01/build-manifest.json`；Native DLL SHA256 为 `B8ACA53758BB161E3D8A432FB4C14C0F55BF8904DA02F317F914A3091B15872E`，编译 0 warning /0 error。68 源输入及保存副本见 `.build/runs/reward-closure-repair-work01/source-snapshot-final01.json`。

- 新奖励组：35 次原生进程、419 项检查、171 个实际生产 reader 控制，全通过。其中 161 个损坏拒绝、10 个等价通过。新增39个同根因控制覆盖 Sozu 联动伪报、各菜单/直接 Add 的金币/HP/RNG/嵌套字段共同篡改、Neow 第二菜单、同模型旧对象、重复 id、类型和升级矛盾。
- 原独立审查13个误接受在最终产物中全部语义拒绝，见 `required-review-counterexamples.json`；保留原包。拒绝来自 Sozu、父状态、升级、类型、唯一性对应生产门，非无效 seal。
- 最终产物重读993个历史副本均符合预期，包含原830包及独立审查163包；原始文件不改。四个方法实际 Harmony 审计 API 再执行，8个未知补丁拒绝、目标业务调用0；不计作游戏业务覆盖。
- 开发构建先在原31份审查副本上确认全部预期，再加入控制与类别限定后冻结最终构建。没有失败样本被替换，没有换 seed、奖励索引或预算。

## 同最终构建统一回归

全部完成，8个原生组加 Core 的外层任务均退出0；逐进程退出和内部断言另行核查，没有仅凭外层0认定成功。

| 组 | 原生进程 | 套件检查 |
| --- | ---: | ---: |
| 新奖励闭环 | 35 | 419 |
| 遗物交易/Hook/补货 | 18 | 471 |
| 商店经济/自然路径 | 23 | 361 |
| 商店基础 | 25 | 371 |
| 原奖励/药水/宝箱 | 55 | 561 |
| 房间/休息/事件/自然多节点 | 34 | 307 |
| 终态/尾部 | 16 | 273 |
| 选择/数据完整性 N2 | 74 | 580 |
| **合计** | **280** | **3343** |

Core98 独立执行通过，执行文件与 Core 依赖哈希单列在 `core-artifact-lock.json`；依赖与冻结 Native 产物中的 Core 一致。原始业务退出为 206×0、67×1、4×2、3×3，保留预期故障、Unsupported、预算停止。DEV_01/02/03、自然商店三种子、旧24/25动作、真实死亡、原尾部夹具、重放/完整 RNG/观察对照和失败前缀均保留。

`provenance.json` 的530项核对全部通过：重算 Build ID、68源输入/副本、44编译输入、3游戏输入、7产物、各真实子进程、15锁定输入、52兼容源码以及历史文件锁一致。993份历史副本实际调用最终 reader，无预期偏差，原始3888个被读文件不变；它们与新原生执行、171控制、审计 API 和来源核对分别计数。

总入口和原始日志保存在 `.build/runs/reward-closure-repair-work01/`，汇总为 `summary.json`、`closeout.json`；八组结果为 `.build/runs/reward-closure-repair-final01-*`。开发构建和全部原失败/误接受证据保留。一次只读进度命令遇服务短断后核查原批次继续运行，没有重复启动批次。

复现新奖励组时使用新鲜输出目录，且源文件须保持冻结哈希：

```powershell
python -P tools/native-run/run-native-reward-closure.py --manifest .build/native-build/reward-closure-repair-final01/build-manifest.json --output .build/runs/reward-closure-repair-reproduce-01
```

完整统一入口为工作目录中的 `run-final01.py`，来源核对 `inspect-final01.py`，历史重读 `recheck-history-final01.py`。独立复验可优先重读13个原反例、171控制与明确父子契约，不必为复述计数重复启动全部批次。

## 范围与后续

本轮 native-only/NotCompared，未新增 Mod 适配。固定 SDK 的 ShouldAddToDeck 拒收 Hook 无原版覆盖，健康奖励拒收保持不适用/not run；死亡持有者等 Add 前置失败不被扩大成“Add 永远成功”。SpoilsMap 仍是明示 API 夹具，不能作为自然跨幕证据。

先对本轮最终证据独立复审，通过后接 [事件战斗与 Boss 交接](NATIVE_RUN_REWARD_CLOSURE_NEXT_HANDOFF.md)，再继续步骤5–6。未修改角色、安装、真实存档，未启动游戏 UI 或真实 Steam/网络，未提交推送。
