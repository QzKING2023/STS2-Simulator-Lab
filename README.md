# STS2 Simulator Lab

An experimental offline validation framework for Slay the Spire 2 combat and run-state behavior.

## Current milestone

The implementation targets game **v0.111.0 / 41cef1ea** and a separately locked Eleanor **v0.4.1-forms-cardpool.fix** input.

- Vanilla combat replays compare native actions with simulator branches through Victory, including enemy turns and a two-target RNG fixture.
- Eleanor has bounded card, form, upkeep and Record/Echo API coverage, with strict version/assembly/manifest/patch checks.
- The minimum offline run loop executes **Victory → native rewards → legal next node → next battle's first actionable state**. Gold collection plus card-skip and card-take policies repeat in separate processes with the same fixed battle plan.
- A separate power API fixture verifies temporary Recording expiry and nonzero Record/Echo cleanup across combats.

This is a validation milestone, not complete character support, independent reward prediction, balance analysis or a full-run optimizer. The published minimum-loop snapshot stops at next-combat first action. The local two-battle milestone additionally adapts ManaShield and validates both reward policies through the second Victory; see [two-battle validation](docs/TWO_BATTLE_VALIDATION.md). The six candidate Echo cards remain absent from the pinned input.

See [milestone scope](docs/MILESTONE_STATUS.md), [validation setup](docs/VALIDATION.md), and [implementation provenance](docs/PROBE_PROVENANCE.md).

## Core checks

The core protocol library does not require game or Mod assemblies:

~~~powershell
dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj
~~~

Game-bound validation additionally requires locally supplied licensed game files and pinned dependencies. No dependency is downloaded, deployed or bundled. Configuration uses command parameters or environment variables documented in [VALIDATION.md](docs/VALIDATION.md). Local input locks, binaries, reports and research stay under ignored directories.

## Licensing

Original Lab code is MIT licensed; see [LICENSE](LICENSE). [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) preserves upstream attribution and distinguishes external dependencies from Lab code. The Lab does not distribute or relicense game code, game assets, Mod binaries or private research.

Only project source, necessary validation tools and public technical documentation belong in this repository.
