# Stage 14 — Checkpoint 2026-10-05

## Main certified baseline

- ✅ Stage 14.1 — Settings Information Architecture / UI Hardening
- ✅ Exact-head CI: Run #1429
- ✅ All CI gates green, including Dashboard Browser Smoke
- ✅ PR #36 merged into main
- ✅ Certified merge commit: `4bc6cd4484647eb6eb029aef53c947149f9e1da9`

## Slice 14.1 — COMPLETE

### Scope delivered

- Settings category navigation and searchable IA
- Stable anchors for settings sections
- Explicit local-only settings notice; no fake Server-backed Settings capability
- Browser Smoke for search, category navigation and unique anchors
- Notification/settings E2E selectors hardened against strict-mode ambiguity

### Gates

- ✅ Build
- ✅ .NET Tests
- ✅ EF validation
- ✅ Server/Migration/Health Smoke
- ✅ Dashboard Lint/Build
- ✅ Dashboard Preview
- ✅ Dashboard Browser Smoke

## Slice 14.2 — COMPLETE

**Dashboard PC grouping by Internet 1/2 + dense responsive hardening**

### Delivered

- ✅ `Station.Network` persisted server-side with EF migration and model snapshot.
- ✅ Dashboard `GET /api/dashboard` returns the authoritative Station.Network value.
- ✅ Demo seed distributes PC-01..20 to Internet 1 and PC-21..40 to Internet 2.
- ✅ Dashboard grouping renders explicit Persian labels for Internet 1 / Internet 2.
- ✅ Existing Status / VIP-Normal / Remaining Time grouping options preserved.
- ✅ Grouping/filtering remains presentation-only and does not alter server truth or session state.
- ✅ Responsive hardening covers workspace, toolbar and dense station cards down to the tested 520px viewport.
- ✅ Browser Smoke covers Internet grouping, grouping/filter switching and responsive critical path.
- ✅ Exact-head CI: Run #1435.
- ✅ Main certification: Run #1436, Attempt 2.
- ✅ Certified merge commit: `63ac3f67357a0970df504a62c9ab16cc1ba4f53a`.

### Not Done

- Server-backed Settings
- Physical 2–3 PC validation
- Real Backup/Restore
- Stage 14.3 accessibility / operational polish

## Recovery Rule

Resume from certified main commit `63ac3f67357a0970df504a62c9ab16cc1ba4f53a`. Do not infer completion from chat history.

## Next

Start **Stage 14.3 — Accessibility / UI / operational polish** from the certified main baseline above.


## Slice 14.3 — COMPLETE

**Accessibility / UI / operational polish**

### Delivered

- ✅ Skip link and keyboard entry to main content
- ✅ Active navigation semantics via aria-current
- ✅ Accessible API / SignalR status announcements
- ✅ Notification expanded state and labelled region semantics
- ✅ Reduced-motion support
- ✅ Last-valid-snapshot messaging when live API data is unavailable
- ✅ Browser Smoke for the new accessibility and stale-data paths

### Gates

- ✅ Exact-head CI: Run #1439
- ✅ Main certification: Run #1440
- ✅ Certified merge commit: b69fa15c7ea166a65c5932f8ceb11fb34429eb4e

## Slice 14.4 — IN PROGRESS

**Server-backed Settings**

### Delivered in current branch

- ✅ Server-persistent AppSetting global scope with EF migration + model snapshot.
- ✅ Independent settings.view / settings.manage permissions.
- ✅ GET/PUT /api/settings with type/range validation and Persian errors.
- ✅ Atomic save path with AuditLog for changed settings.
- ✅ Dashboard server synchronization and explicit server-save action.
- ✅ Hotkeys and UX section locks remain explicitly local rather than being falsely presented as global server state.
- ✅ Server API Smoke and Browser Smoke added for the new path.

### Current gates

- ⬜ Exact-head CI for Stage 14.4
- ⬜ Main certification for Stage 14.4

## Next

Complete Stage 14.4 through exact-head CI and Main certification, then advance to real Backup/Restore and physical 2–3 PC validation gates.
