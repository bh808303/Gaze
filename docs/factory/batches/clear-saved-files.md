# Saved-folder Clear follow-up

Human response on 2026-10-25: "Include PNG and ZIP alongside MP4, with confirmation". This resolves the optional choice raised at the end of the initial live-batch build. Build as Factory task B05 on the existing clean feature branch. Mandatory checks remain the same; B01-B04 completion is preserved.

Scope: existing Clear action, configured-folder top level only, MP4/PNG/ZIP exact extensions case-insensitively, explicit confirmation with count/types/folder defaulting No, existing Recycle Bin handling. Other files/subdirectories untouched. No new automatic saved-file deletion, gallery change, video behavior change, or temp cleanup change. Isolate file selection and confirmation/callback invocation sufficiently for meaningful tests without recycling fixture/user files. Source remains in target repository.

Proceed under the human's direct authorization; no additional product decision needed. B05 fresh worker and independent reviewer, then local commit and updated handoff.

B05 accepted by independent reviewer in run `20261025-134958-805`; all mandatory checks passed. Parent confirmation-predicate mutation failed tests, restoring exact source passed core suite. Completion recorded in tasks.json. No user files recycled during testing; native dialog remains untested interactively.
