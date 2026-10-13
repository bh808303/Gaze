# Optional factual timeline metadata

## Outcome and scope
Implement the user-approved optional metadata bundle using one Settings toggle named exactly `Record timeline metadata`, off by default for new and existing settings, applied to the next recording. Existing basic timeline remains active and keyboard input retains its separate opt-in. No Windows logs, diagnostics product, semantic action interpretation, dialog detection, cursor shapes or visual-change analysis.

## Acceptance criteria
- Toggle persists and reaches recording startup; metadata declares enabled collection options so readers distinguish disabled collection from no activity.
- Enabled recordings add mouse button releases, vertical/horizontal wheel events with original raw delta and pointer coordinates, bounded changed-position pointer samples (provisional max 10 Hz normally / 30 Hz while a button is held), and capture geometry/mapping changes.
- Observe only within existing capture boundaries, including window relationship/hit-test restrictions. Explicit pointer boundary transitions contain no foreign identity or outside coordinates. Pause/stop drain accepted discrete events and ignore paused activity. Resume samples follow existing timing rules.
- Preserve original input timestamps, local/original frame association, and factual fields; do not label gestures or intent. Low-level hooks remain enqueue-only with bounded pointer collection. Any known event loss or hook installation failure is explicit in collection status where embedding is possible; do not claim undetectable hook failures are detected.
- Record actual sample timing in a compact representation suitable for recognizing capture gaps, and actual geometry changes; avoid redundant per-frame geometry events.
- Extend embedded MP4 schema compatibly. Existing recordings and legacy cues remain readable. Reader supports filtering event kinds and time/frame ranges and returns relevant collection context. Dense events respect timed-text packet limits through batching rather than silent truncation; preserve safe plain-video fallback.
- Meaningful TDD covers toggle/default migration and recording plumbing, rate limiting, boundary transitions, input event ordering/pause-resume, geometry/timing metadata, serialization/reader compatibility and filters. Run available build/core/reader tests. Native desktop validations may be limited by environment and must be reported accurately.
- Update README, CHANGELOG and docs/event-timeline.md with behavior, exact fields, defaults and limitations.

## Plan and authorization
One bounded factory task implementing the bundle with regression tests. User explicitly requested implementation with the factory after agreeing scope; this authorizes spec/plan preparation, local branch and commits of planning files, factory workers and checks. No merge, deployment, publishing or real user recording. Target repository and evidence remain here.
