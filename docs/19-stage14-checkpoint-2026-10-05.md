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

## Slice 14.2 — NEXT

**Dashboard PC grouping by Internet 1/2 + dense responsive hardening**

### Required scope

- Verify the existing PC/station grouping behavior against actual station/network data.
- Make Internet 1 / Internet 2 grouping explicit and reliable in the Dashboard UI.
- Preserve status, VIP/normal and remaining-time filters.
- Ensure grouping and filtering do not alter server truth or session state.
- Harden dense dashboard layouts for the real operator viewport.
- Add Browser Smoke for grouping/filter combinations and responsive critical paths.

### 14.2 implementation status — IN PROGRESS

- ✅ `Station.Network` is now persisted server-side with EF migration and model snapshot.
- ✅ Dashboard `GET /api/dashboard` returns the authoritative Station.Network value.
- ✅ Demo seed distributes PC-01..20 to Internet 1 and PC-21..40 to Internet 2.
- ✅ Dashboard grouping renders explicit Persian labels for Internet 1 / Internet 2.
- ✅ Browser Smoke covers grouping/filter switching and a 520px dense viewport.
- ✅ Responsive hardening collapses the dashboard workspace and protects dense station cards on narrow viewports.

### Not Done

- Exact-head CI certification for 14.2.
- Server-backed Settings
- Physical 2–3 PC validation
- Real Backup/Restore
- Remaining Stage 14.3 accessibility/operational polish

## Recovery Rule

Resume Stage 14.2 from branch `stage14-2-dashboard-network` and use the exact HEAD reported by CI. Do not infer completion from chat history.

## Next

Start **Stage 14.2** only from the certified main baseline above.
