# Print Screen implementation plan

1. PS01: testable exclusive trigger policy and native session-scoped adapter/controller gesture integration. Introduce shared enum (Middle mouse=0, Print Screen=1) and optional default parameters so current callers compile. Reuse snapshot/gesture/overlay capture flow; key down/up behave like MMB; cancel/repeat/modifier/teardown ownership correct. Test before behavior. Scope core trigger model, TranscribeInput, narrow controller integration and tests. No settings UI yet.
2. PS02: settings persistence/validation and both UI choices, host mappings and dynamic session instructions. Settings tests before production wiring; actual save/cancel/render checks when feasible. Document trigger semantics, exclusivity and limits; run complete checks and independent review. Scope settings, PanelOptions/TrayPanel/TrayContext, WPF settings/session text and tests/docs.

Each task goes through fresh Software Factory worker -> mandatory checks -> fresh read-only reviewer -> local commit; record exact command/exit/evidence file paths. Native OS interception remains separate from injectable behavioral evidence. No edits to prior live-batch manifests or incomplete historical plans. Current tasks: docs/factory/printscreen/tasks.json. No factory launcher changes.

User clarifications settle trigger behavior and exclusive selection; no additional permission gate needed for implementing this approved feature. Runtime choices are documented and verified, not silently inferred. Main/video behavior and previous manual test results remain baseline.
