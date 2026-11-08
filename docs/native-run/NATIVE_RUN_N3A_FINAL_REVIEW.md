# M1 步骤 4A 最终独立验收：通过

日期：2026-11-02。审查对象为[完整状态绑定修复](NATIVE_RUN_N3A_STATE_BINDING_REPAIRS.md)，冻结ID：`223DB2B31B0E199B694CFF24DBF08600F0A104AFCC095493EB907FD71538AA1F`。

**结论：4A在已约定范围内通过，无剩余必修项，可以进入4B。** R1完整状态绑定及此前R2归属／顺序／唯一性成立。该结论覆盖真实死亡和明确标注的尾部夹具，不代表正常开局完整通关或M1完成。

## 独立执行证据

本轮没有修改运行实现、重新编译或新增原生游戏执行。只将证据复制到新的审查目录，并实际调用最终DLL的生产`--review`。新负控由独立脚本修改副本、重建链和seal，没有调用实施者的`--mutate`。

证据根目录：`.build/runs/n3a-state-independent-review01/`。入口为`review.py`，结果为`summary.json`、逐例`reader.log`和`trace-review.json`。

| 独立检查组 | 实际结果 |
| --- | --- |
| 历史163副本，含原始四反例 | 全部得到预期退出；四反例均在完整状态绑定门拒绝 |
| 本轮80损坏／8等价副本 | 80个exit1，8个exit0，实际诊断匹配 |
| 当前Death、next-act、Architect、skip、take五个正常包 | 全部exit0 |
| 新增15负控／2正控 | 15个exit1，2个exit0 |
| 合计 | **273次生产读取器调用，0不符预期；170项独立核对，0失败** |

新增负控分别为七个静止边界的maxHp改动、三个场景的全部后续终态快照一起增加gold、三个尾部的遗物数组顺序变化，以及Closed提前／重新打开。即使多个后续快照互相一致，也会因不符合最后确认状态被拒绝。独立无改动重封装、完整状态递归反转对象属性顺序均通过。数组顺序仍具有语义，不被当作对象属性序处理。

1024个原始证据源文件hash保持，已有实现及文档未改。历史包的旧Build ID与本轮reader身份分别记录，不混入同构建原生执行计数。旧辅助脚本失败和final01的两项诊断预期失败仍保留，不作为通过证据。

## 源码与状态绑定核验

`NativeTerminalContract`在实际`confirmed`时克隆完整state，后续构造期望值不会修改该锚点。比较沿用Core的结构化JSON深比较，不列字段白名单，也不忽略整个lifecycle。七个边界分别为：

- Death：death、terminal-stamp、terminal-contract。
- Architect：terminal-stamp、terminal-contract、tail-contract。
- next-act：OldActVoteControl确认后的tail-contract。

仅terminal-contract及随后的Architect tail允许`lifecycle.native.Closed`从false变true，且先确认原锚点为false。全部其它已采集内容必须相同。缺失／null／非对象快照继续由生产必需state契约拒绝。

归属检查在相应边界确认active为空并匹配最后root/context；原有native counter、start/completion顺序、唯一性及终态后拒绝派发仍有效。没有把真实业务清场阶段强制相等：原生OnEnded返回HP80，WinRun完成HP0，选项返回后才确认root并认证RunVictory。正常包和原R2负控均验证了这一区别。

## 同构建与运行事实

重新计算Build ID一致；38个source、22个compiled、3个原版inputs、7个artifacts逐项hash一致。NativeRun DLL SHA256为`A02AEBF7A6791DC22E396F32D40202B778B222105C5054E9AC8BCCF8EDBFEFE6`。

逐个核验4A的16进程／273检查及N2的74进程／580检查，合计90进程／853检查，process、result、trace header和复制的manifest身份一致。实际raw exit分布为56×0、31×1、1×2、2×3，全部符合对应预期，不由外层exit0代替判断。Core98执行记录及Core DLL身份、347项补充记录均与冻结对应。

真实自然Death保留13/13动作及80点敌方实际伤害；next-act夹具5/5、Act0→1、旧投票无效；Architect夹具7/7、原生选项至RunVictory。自然skip24／take25的HP70、金币266、永久deck10／11及下一战Play保持。route、choices及完整final-state与原final05基线一致；三个4A场景的重放／off／minimal对照一致，包含完整已盘点RNG。

预算4/4、回合故障3/4、Architect两个故障6/7保留实际前缀和明确stop；失败／预算数据不认证为完整成功。15个Mod输入和52个兼容源码hash未变。MemorySaveStore、shouldSave=false、Steam未初始化、单人服务、无UI和错误账本为空等原隔离证据成立。

## 后续边界

一run一进程；TestMode死亡不承诺零售loss收尾或worker复用安全；尾部夹具不补足前幕Boss／自然完整run；Architect展示桥不代表零售演出或其RNG等价；原生结果仍为NotCompared。本次检查没有发现4A范围内的新阻断，不把未来全部节点／全部schema要求前移到此验收。

下一轮按[4B具体交接](NATIVE_RUN_N3B_IMPLEMENTATION_HANDOFF.md)推进内部房间驱动、休息和简单事件。4C–4D、5–6继续待办，六步授权有效。本轮仅新增两份审查／交接文档和隔离审查证据，没有提交推送、角色／安装／用户存档修改或真实UI／Steam／游戏网络调用。
