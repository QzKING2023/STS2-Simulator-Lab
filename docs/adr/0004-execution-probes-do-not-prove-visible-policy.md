# Execution probes remain separate from visible-policy compatibility

M2 content probes may use native or Oracle internal state to drive controlled lifecycle scenarios, because their purpose is to prove execution compatibility. Those probes must not be counted as evidence that a player-facing strategy obeys the visible information boundary. Host instrumentation may observe and correlate native behavior, but it may not mutate Mod semantics, inject results, skip callbacks, rewrite binaries, or substitute synthetic DTOs for a real lifecycle.

## Consequences

- Content compatibility and visible-policy compatibility are reported as separate dimensions.
- Hidden-state probes can close execution gaps without overstating player-visible strategy support.
- Any foundation or environment failure blocks dependent Mod work instead of being hidden behind a one-off adapter.
