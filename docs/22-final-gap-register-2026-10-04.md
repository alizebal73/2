# Final Gap Register — 2026-10-04

این فایل وضعیت واقعی head فعلی را ثبت می‌کند. معیار تصمیم‌گیری:
`Server Truth → Contract → Identity → Permission/Ownership → Validation → Transaction → Persistence → Audit → Agent/External Action → Result → UI Event`

## اصلاحات قطعی انجام‌شده در این دور

- Session نرخ شروع خود را با `HourlyRateSnapshot` نگه می‌دارد و Settlement از همان snapshot استفاده می‌کند؛ تغییر تعرفه بعد از شروع Session دیگر مبلغ قبلی را بازنویسی نمی‌کند.
- `CustomerLogin → Session` مالکیت persisted دارد؛ uniqueness فعال Session برای Login و Station در DB enforce شده است.
- EndSession و AgentLogoutAndLock فقط Login متعلق به همان Session را می‌بندند.
- Session End و Lease Release در یک transaction انجام می‌شوند.
- Game Sync تا `AgentCommand = Succeeded` و state محلی Agent اثبات می‌شود؛ Sent به معنی اجرا نیست.
- Buffet Sale قبلاً atomic بود و حفظ شده؛ موجودی منفی نباید رخ دهد.
- Inventory transaction list دیگر Take-before-Order ندارد.
- Buffet profit report، Finance/Reporting و چند گزارش دیگر با بازه زمانی در SQL محدود شده‌اند و کل ledger را بی‌دلیل load نمی‌کنند.
- VIP usage فقط Sessionهای مرتبط با بازه را می‌خواند.
- Client identity دیگر بر اساس IP حدس زده نمی‌شود؛ با DeviceId + Bearer Token احراز می‌شود.
- Session transfer مقصد را atomic claim می‌کند.
- Reservation/Event listها قبل از pagination فیلتر می‌شوند.
- Reservation و Event participant checks برای SQLite writer transaction تقویت شده‌اند.
- Recent migrations در `MigrationMetadata.cs` ثبت شده‌اند.
- `Program.cs` دیگر helper رمز عبور تکراری ندارد و duplicate partial declaration حذف شده است.
- مسیرهای Dashboard که قبلاً mock بودند با سرویس‌های Server-backed جایگزین شده‌اند و `mockService.ts` دیگر در tree فعلی وجود ندارد.
- Restart/Shutdown command contract و `client.power` در مسیر Dashboard → Server → Agent وجود دارد.
- Production seeder اکنون Demo data را پشت `includeDemoData` نگه می‌دارد و password واقعی را از `GAMENET_ADMIN_PASSWORD` می‌گیرد.

## گپ‌های باقی‌مانده واقعی

1. Exact-SHA CI برای latest head: Build/Test/EF/Startup/Dashboard/Agent smoke.
2. Event creation: جلوگیری از هم‌زمانی دو Tournament overlapping در چند writer هنوز نیاز به یک lock row/domain gate صریح دارد.
3. Audit text-search pagination: SQL filtering ساختاری است ولی search متن در memory انجام می‌شود؛ باید در نهایت API pagination/search کامل‌تر شود.
4. `Program.cs` هنوز بزرگ است؛ extraction باید مرحله‌ای باشد و فقط بعد از تثبیت correctness انجام شود.
5. Installer/Separate Server-Client deployment هنوز field gate است.
6. Backup → stop → restore → startup → DataProtection/domain verification هنوز field gate است.
7. Process detection/real game process telemetry و کنترل واقعی بازی هنوز field gate است.
8. Kiosk/shell واقعی روی 2–3 PC و سپس rollout کنترل‌شده 40+ PC هنوز field gate است.
9. Canary update/rollback و network-disconnect/restart recovery روی Agent واقعی هنوز field gate است.
10. Notification queue سراسری، Internal Notes و final responsive/keyboard pass هنوز product-closure items هستند.

## قواعد

- CI سبز به‌تنهایی Field-ready نیست.
- Mock یا marker موفقیت واقعی محسوب نمی‌شود.
- هر زنجیره جدید باید قبل از implementation، Integration Contract و state ownerهای زنجیره‌های قبلی را audit کند.
