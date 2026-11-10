# M2 uses library-first compatibility with dual acceptance gates

M2 will require both `flow-compatible` and `complete content compatibility`: a Mod must reach an actual native `RunVictory` or `Death`, and every frozen content unit must have lifecycle evidence without changing the Mod's original code. BaseLib, RitsuLib, and MinionLib are treated as the shared compatibility foundation and are addressed before per-Mod fixes; if a gap remains, the Lab may add only a reusable generic compatibility interface, never a single-Mod adapter or special case. This preserves the broadest reuse boundary while keeping the final denominator and evidence auditable.

## Consequences

- The M2 plan has two independent gates per Mod and cannot promote a flow-only result to full compatibility.
- Dependencies remain outside the role-bearing denominator, but their shared semantics become a prerequisite infrastructure gate.
- Content inventory and lifecycle evidence grow substantially; load-only or construct-only probes remain incomplete.
