# Final Gap Register — 2026-10-04

این فایل جایگزین تصمیم‌گیری‌های قدیمی نیست؛ وضعیت واقعی با Completion Control و Integration Contract سنجیده می‌شود.

## بسته‌شده در مسیر فعلی
- Stage 12 vertical slice hardening.
- Stage 13 Reporting/Audit server truth.
- Stage 14 Operations/Reservation/Waitlist/Event/Network health.
- Stage 15 Backup/Restore/Scheduler/Recovery preparation.
- OperationsPage mock path حذف شد.
- Dashboard ReportsPage mock report path حذف شد.

## باقی‌مانده واقعی برای Field-ready شدن
1. Exact-SHA CI + migration/startup/Dashboard interaction evidence برای آخرین head.
2. Restore Drill واقعی: Backup → stop Server → restore → startup → verify login/DataProtection/domain data.
3. Installer/Setup finalization و installation on separate Server/Client PCs.
4. Client Process Detection و process telemetry واقعی برای بازی‌ها.
5. Client kiosk/shell field gate روی چند PC واقعی.
6. Agent command recovery/lockout/health drill با قطع شبکه و restart.
7. Canary update/rollback روی یک Agent واقعی.
8. Multi-cashier conflict drill و operator-away acceptance test.
9. Notification center/event delivery persistence.
10. Internal Notes domain/UI.
11. Account Pool health/lease history UI and operational alarms.
12. Final responsive/operator keyboard UX pass.
13. Controlled 2–3 PC rollout، سپس 40+ rollout evidence.

## قانون
هیچ‌کدام با checkbox یا mock اثبات نمی‌شوند. هر مورد باید owner، contract، persistence, permission, recovery و runtime evidence خودش را داشته باشد.
