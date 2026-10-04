# Stage 13 — Checkpoint 2026-10-05

## Branch

stage13-reporting-audit

## Base

main = 9aecaba120a69a9340ff214c2a3f3f7ffc7e104a

## Slice 13.1 — Audit Explorer

- Server `AuditLogService` queries authoritative `AuditLog` records with time/operator/action/entity/search filters and pagination.
- `GET /api/audit` is protected by `audit.view`.
- Dashboard Reports includes a real Audit view with filters, paging and CSV export of the visible page.
- Reports navigation accepts `finance.view` or `audit.view`.
- Finance data loading is skipped when the current user only has `audit.view`.
- Regression coverage added for service filtering/pagination, browser behavior, and CI server API smoke.

## Not Done

- Stage 13.1 has not been certified until the exact branch HEAD is green.
- Sessions/Stations, Customers/VIP, Users/Shift reports are still later Stage 13 slices.
- Fine-grained permission/scope and full reporting export policy remain later in Stage 13.
- Notification/Event Queue remains later in Stage 13.

## Recovery Rule

Resume from this branch and this checkpoint. Do not infer completion from chat history; use the exact HEAD and CI result.

## Next checkpoint

After 13.1 is green: audit the real API/UI behavior, then implement Stage 13.2 Sessions & Stations reports without reintroducing Mock data.