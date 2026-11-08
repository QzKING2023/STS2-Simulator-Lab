# Documentation map

The documentation is grouped by the question it answers. Reports keep their original filenames so existing references remain recognizable.

## Start here

- [Validation entry](validation/VALIDATION.md) gives the current validation entry points.
- [Milestone status](milestones/MILESTONE_STATUS.md) records the latest milestone state.
- [M1 final acceptance](milestones/M1_FINAL_ACCEPTANCE.md) is the sealed M1 result and its declared limits.
- [Long-term roadmap](milestones/LONG_TERM_ROADMAP.md) records the current direction.
- [M2 full-run plan](mods/MOD_FULL_RUN_EXECUTION_PLAN.md) defines the role-Mod terminal compatibility target and phase gates.
- [M2 phase prompts](prompts/MOD_FULL_RUN_PHASE_PROMPTS.md) are the handoff prompts for each independent session.
- [Agent operating docs](agents/) describe issue tracking, triage labels, and domain-document rules.

## Topic directories

- `architecture/` contains contracts, trust rules, storage policy, and development principles.
- `milestones/` contains milestone plans, acceptance batches, and current status reports.
- `native-run/` contains native run implementation, lifecycle, evidence, review, and handoff reports.
- `combat-solver/` contains CombatSolver integration and online execution reports.
- `performance/` contains performance and visible-execution pilot reports.
- `validation/` contains cross-cutting validation, compatibility, provenance, and retained foundation reports.
- `prompts/` contains execution prompts intended to be used with maintenance work.
- `history/` contains superseded roadmaps and historical entry pages.

## Report naming

Validation, review, repair, contract, and next-handoff files stay together in the topic directory they describe. A `*_NEXT_HANDOFF.md` file is an execution pointer. It does not replace the corresponding validation or review report.

Historical files remain available for provenance. Current status belongs in the milestone and validation entry points above.
