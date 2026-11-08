# 第二幕胜利之后：第三幕与完整run交接

基线冻结`D59D778F3360FBBFF7B65A419725D321F20A7B541FBC2E91CDAF45FDCFC45910`，见[本轮验证](NATIVE_RUN_ACT2_VICTORY_VALIDATION.md)。先独立审查真实Boss/精英胜利、奖励、第二次跨幕和音频双方法适配；之后接续已获授权M1，无需逐轮请批。

## 独立审查的有限范围

1. 主路径正常开局367动作，a365Boss胜利、a366原生100Gold/IMPERVIOUS新实例、a367原生ready/EnterNextAct到act2 MapRoom；不能将中间SliceBoundary记为RunVictory。
2. 独立精英路径318动作、a317胜利、a318金币及实际MealTicket；不与主路径拼接。原DEV01/02死亡、360/306死亡和本轮开发失败继续保留。
3. 新策略只选择合法候选/原生Skip及药水；原生效果未镜像或手动合成。检查两个真实音频callback各一处替换及实际Harmony前后未知patch拒绝API夹具；不将API夹具计为业务执行。
4. 同一最终构建15次原生、389原始记录+122历史控制重验、21新控制、15输入/52兼容源码和完整RNG重放。按信任边界做有限反例，无需重复全量原生矩阵来审检查器。

## 接下来的有序工作

- 先把自然控制器的act scope从限定第二幕推广到实际当前幕。重用原生地图/ActReady任务和不可变确认状态，按幕归档target、奖励、宝箱和room ledger；不清空全局计数冒充新生命周期。保留本轮两条有限场景作为回归，另注册持续run场景及版本化策略。
- 第三幕StartingMapPoint要走原生EnterMapCoord，实际Ancient选择与真实初始化。当前Tezcatara Soup有限契约不能默认推广到所有Ancient；读取锁定原版真实模型，根据实际候选建立选择、效果/任务和完整数据观察范围。未知分支明确Unsupported，不静默跳过。
- 组合已实现商店和嵌套事件入口，保持原生选择/购买/奖励/EventModel.Resume真实Task完成。现有`PreparedNonCombatRoom`仍会对商店和BattlewornDummy停止，不能因单模块夹具通过就当持续run已支持。先真实最小跨模块路径，再扩自然样本。
- 正常流程进入后续Boss、其奖励与最终Architect至WinRun；保留真实Death、预算、异常和Unsupported前缀。已有尾部Architect夹具可供接口参考，不能替代同run自然胜利证据。
- 同时从具体实验问题检查必要数据：当前日志记录派发、选择、实例、资源、历史和RNG，但powers目前主要是身份/类型/amount。诸如Surrounded.Facing等有业务意义的状态须列入数据缺口清单并按实际用途投影，不以“完整JSON比较”宣称所有内部状态已记录。增加观察前先评估 getter 是否纯，配实际不扰动对照。
- 生命周期稳定后完成同一个最终构建全部适用原生/共享回归、完整RunVictory独立进程重放、必要数据缺失门和既定EVAL01–06。当前成功种子是开发样本；预登记样本不能换掉失败，策略表现与执行可靠性分开记录，不据重复次数推胜率。

下一子回合可先完成“实际第三幕Ancient→首战与奖励”作为明确收口，再连续多节点至Boss/终局；这些仍在全六步既有授权内。原有无UI、无真实Steam/网络/存档、无Mod专项及无提交推送限制不变。检查器修改按有限生产正负控与冻结原证据重读；生成链改动按受影响场景实际执行。最终M1全量门保留。
