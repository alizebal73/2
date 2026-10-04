# ممیزی عمیق Stage 1 تا 12 — وضعیت فعلی

## مرجع
`Server Truth → Contract → Identity → Permission/Ownership → Validation → Transaction → Persistence → Audit → Agent/External Action → Result → UI Event`

## وضعیت واقعی گپ‌های قبلی

موارد زیر که در نسخه‌های قدیمی این سند GAP بودند، در کد فعلی بررسی و بسته شده‌اند:
- Tariff Management: مسیر Server-backed و service واقعی دارد.
- Production Seeder: demo data فقط با includeDemoData؛ password production از secret تنظیم‌شده می‌آید و fail-fast است.
- CustomerLogin concurrency: transaction + customer writer lock + retry + concurrency tests.
- Buffet sale / stock mutation: مسیرهای حساس transaction/atomic writer protected هستند.
- Global Command Center: mockService حذف شده و از serviceهای واقعی استفاده می‌کند.
- ClientShell fake-success: عملیات real command path یا وضعیت غیرقابل‌پشتیبانی را success جعلی نشان نمی‌دهد.
- Dashboard DEV mock fallback: mockService از tree production path حذف شده است.
- Stage9 Restart/Shutdown contract: command type/persistence/ack/health lifecycle همسان شده‌اند.
- Session CustomerLogin ownership و Agent Lease release: persisted ownership + transactional release در Stage12 تثبیت شده است.

## گپ واقعی باقیمانده در Stage 1 تا 12

### ST2-EVIDENCE — Prototype/Behavior Parity Matrix
هسته‌ی implementation مراحل 1 تا 12 Server-backed و verified است، اما یک ماتریس رسمی و قابل‌اجرا برای اثبات «رفتار مورد انتظار محصول در برابر رفتار فعلی» وجود ندارد.

این گپ نباید با mock یا حدس بسته شود. باید:
- رفتارهای بحرانی Stage1-12 به assertionهای قابل‌اجرا تبدیل شوند.
- owner/state/permission/result/recovery برای هر رفتار مشخص باشد.
- regression matrix در CI اجرا شود.
- مواردی که عمداً out-of-scope هستند explicit ثبت شوند.

### ST1-12-VERIFY — Exact latest head
تا زمانی که Build/Test/EF/Startup/Dashboard/Agent/Session/Game/Lease smoke روی exact latest SHA سبز نباشد، Stage1-12 را Verified نهایی اعلام نمی‌کنیم.

## خارج از Stage1-12 software
- installer و deployment جداگانه Server/Client
- physical kiosk/shell روی 2–3 PC
- real process telemetry مصرفی بازی
- backup/restore field drill نهایی
- canary rollout و 40+ PC rollout
- notification queue جامع
- responsive/keyboard final pass
این‌ها Release/Field gates هستند، نه بهانه‌ای برای بستن Stage1-12 با checkbox.
