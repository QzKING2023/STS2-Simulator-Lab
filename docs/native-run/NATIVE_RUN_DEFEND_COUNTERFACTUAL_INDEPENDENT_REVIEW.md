# a597 Defend counterfactual: independent review

Date: 2026-11-04. Scope: existing evidence and production evidence-reader calls only.

## Conclusion

**Pass for the declared controlled-prefix experiment. No BossVictory, RunVictory, or completed M1.** The later instruction to stop single-battle tuning has been applied. This review launches no native gameplay and recommends no further a597/a617 tuning batch.

The producer and checker remain frozen at `3DB954E49C1282F26D41555155C7E547922B91C85AA36481F78B35251A7F19FC`. Independent evidence is in `.build/runs/defend-counterfactual-independent-review01/`:

- `facts.json`: 47 checks, all passed. Source 103, compiled sources 66, host inputs 3 and artifacts 7 match the manifest; the 15 locked inputs and 52 compatibility sources match. Original baseline/output/work inventories remain intact.
- `manifest-identity.json`: independently recomputed manifest identity matches.
- `readers.json` / `reader-summary.json`: eight actual production-reader calls; four valid copies accepted and four invalid copies rejected. These include wrong forced instance, missing forced choice, wrong a597 context and an alive-player terminal contradiction. No native gameplay executions.
- `tail-evidence.json`: independently extracted original state/history/decision evidence. Local SDK inspection files in this directory are local research only and must not be published as project source.

## Intervention and causal limits

The first 596 root dispatch/confirmation payloads, including complete state and RNG, are identical to the same-build baseline. The a597 dispatch is also identical. The actual TrueGrit request offers Defend `i1218`, Stampede `i1220`, and another Defend `i1221`. The sole forced intervention selects `i1218` instead of `i1220`. Native execution exhausts that exact Defend and retains Stampede; the first differing confirmation is a597.

The subsequent unchanged v41 policy reaches **Death at action 624, Boss HP 33**, compared with the baseline's Death610/Boss94. A complete native frozen replay matches all 1,248 ordered dispatch/confirmation payloads, final state, choices and RNG. This establishes the effect of this intervention on this deterministic fixed-policy trajectory, including downstream draw/RNG/decision changes. It does not establish that Defend is generally the best exhaust choice, that the ranking patch is stronger, or that the remaining combat is winnable.

Both outputs classify `Terminal=Death`, `Stop=Completed`, `Comparison=NotCompared`, with 624 confirmed/dispatched actions and raw exit 0. A successfully recorded native death is not a simulated win. The experiment uses a normal-start native prefix followed by a controlled intervention, not an entirely online natural policy.

## Actual survival gap

Raw history locates the major losses before the last 33 HP:

| Boundary | Observed facts |
| --- | --- |
| End turn 6 | Player 66 HP / 23 Block; native enemy sequence costs 22 HP |
| End turn 8 | Player 44 HP / 13 Block; three 12-damage attacks cost 23 HP |
| End turn 9 | Player 21 HP / 29 Block; a 47-damage attack costs 18 HP |
| Start turn 10 | Player 3 HP; three block cards cause three real Juggernaut hits of 12; this round survives |
| Final EndTurn624 | Player 3 HP / 3 Block; Boss33 with Intangible; hand Burn and Breakthrough; no potion |

At the final boundary Burn consumes two Block, then the first enemy strike is lethal. The record does not execute every remaining hit after death. The available final hand has no direct block/heal/draw; Breakthrough itself pays HP. There is no demonstrated local rescue in that hand, but this is **not a proof that all earlier legal strategies lose**. A better decision method should consider earlier resource use and future effects, rather than optimize the last damage number alone.

## Budget and preservation

The implementation used two native executions, 57.697 seconds of recorded native runtime. The registered experiment/replay checks finished in 79.660 seconds; closeout in 325.069 seconds, within eight minutes. Persistent batch bytes were 832,636,766; the documented conservative transient-inclusive bound was 951,998,769, below 1 GiB. Each process was below the 120-second limit. This is a disk-evidence limit, not a RAM cap.

No production code, input, game installation, real save, UI, or other project was modified by this review. No commit or push occurred. The next deliverable is the separate read-only [CombatSolver feasibility and cost review](../combat-solver/COMBAT_SOLVER_POLICY_FEASIBILITY_REVIEW.md), not another local strategy experiment.
