# Stage 13 — Checkpoint 2026-10-05

## Base

main = c3f579b38ad1704a62d775c6f45921b24624d43c

## Slice 13.1 — Audit Explorer

- Server `AuditLogService` queries authoritative `AuditLog` records with time/operator/action/entity/search filters and pagination.
- `GET /api/audit` is protected by `audit.view`.
- Dashboard Reports includes a real Audit view with filters, paging and CSV export of the visible page.
- Reports navigation accepts `finance.view` or `audit.view`.
- Finance data loading is skipped when the current user only has `audit.view`.
- Regression coverage added for service filtering/pagination, browser behavior, and CI server API smoke.

## Certified Gate

- Exact-head Stage 13.1 CI: **Run #1380 — success**
- Post-merge main CI: **Run #1383 — success**
- Main merge commit: `24a0654fa6490f415372ab2e7d28f30a0ecfbe89`

## Slice 13.2 — Sessions & Stations — CERTIFIED

- Real server report endpoint: `GET /api/reports/sessions`.
- Dashboard Reports has a real Sessions & Stations panel with date range, station, zone, operator, state and customer filters.
- Summary is built from authoritative Session rows; revenue is `Session.TotalAmount`.
- Unit, browser and CI API smoke passed.
- Exact-head CI: **Run #1391 — success**.
- Post-merge main CI: **Run #1392 — success**.
- Main merge commit: `72045d34a3ba9d561ddca488c61470daad27b3fc`.
- Supporting Stage 10 Agent watchdog hardening is included in the same certified main baseline.

## Slice 13.3 — Customer/VIP + Users/Shift — CERTIFIED

- Real server endpoint: `GET /api/reports/customers`.
- Real server endpoint: `GET /api/reports/users-shifts`.
- Customer/VIP report uses authoritative Customer/VipPackage/Session/Draft Invoice data.
- Users/Shift report uses authoritative AppUser/EmployeeProfile/Payroll/Shift/Expense/InvoicePayment/Session data.
- Server-side permission masking is applied to wallet/debt and payroll fields.
- Dashboard Reports includes real Customer/VIP and Users/Shift panels with shared date range, filters, pagination and CSV export.
- Unit/API/Browser coverage passed.
- Exact-head CI: **Run #1402 — success**.
- Post-merge main CI: **Run #1403 — success**.
- Main merge commit: `2ee64bf42ca1dca0d81f2e051a8fcf0557a5d2a0`.

## Slice 13.4 — Permission/Scope + Export — CERTIFIED

- Server-side report View/Scope All/Export permissions are enforced.
- Report export is server-backed and audit logged.
- Exact-head CI: Run #1411 — success.
- Post-merge main CI: Run #1412 — success.

## Slice 13.5 — Notification/Event Queue — CERTIFIED

- Durable Notification entity + migration are live.
- Domain publishers cover buffet low-stock and Agent offline/command failure events.
- Authenticated notification Inbox, read and read-all APIs are server-side enforced and user-owned.
- Dashboard consumes the server inbox and refreshes from SignalR notification events.
- SQLite-safe notification list/read persistence and correct scoped DI are part of the certified baseline.
- Exact-head CI: Run #1421 — success.
- Post-merge main CI: Run #1422 — success.
- Main merge commit: c3f579b38ad1704a62d775c6f45921b24624d43c.

## Stage 13 — FINAL CERTIFICATION

**Stage 13.1 through 13.5 — COMPLETE from software-engineering perspective.**

## Not Done

- Fine-grained permission/scope and full reporting export policy remain later in Stage 13.
- Notification/Event Queue remains later in Stage 13.
- Backup/Restore real, physical validation on 2–3 PCs and controlled 40+ PC rollout remain release gates outside CI.

## Recovery Rule

Resume from the certified main commit c3f579b38ad1704a62d775c6f45921b24624d43c and this checkpoint. Do not infer completion from chat history; use the exact HEAD and CI result.

## Next checkpoint

Create/resume from certified main c3f579b… for **Stage 14 — UI Hardening / Settings Information Architecture / Dashboard UX**. Do not modify certified Stage 13 slices except for regression fixes.