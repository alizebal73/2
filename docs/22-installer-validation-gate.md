# Installer Validation Gate

این سند قرارداد تست Setup است. ساخت EXE به‌تنهایی Release محسوب نمی‌شود.

## A. Server Setup

1. روی Windows Server نصب شود.
2. مسیر Install روی یک پوشه قابل انتخاب آزمایش شود.
3. Data Root روی یک مسیر جدا از Install آزمایش شود.
4. Setup از admin password و Agent registration token بخواهد.
5. Windows Service با نام `GameNet Manager Server` ساخته شود.
6. Startup Type برابر Automatic باشد.
7. Failure Recovery روی restart تنظیم شده باشد.
8. TCP 5080 روی Private network باز شده باشد.
9. `http://127.0.0.1:5080/api/health` با HTTP 200 پاسخ دهد.
10. Dashboard از LAN روی آدرس Server باز شود.
11. در اولین اجرای Production، admin ساخته و password hash شود.
12. plaintext `GAMENET_ADMIN_PASSWORD` بعد از اولین healthy start در Environment اختصاصی Service باقی نماند.
13. Agent registration token برای Agentهای جدید قابل استفاده باشد.
14. Database، DataProtection و Backup خارج از Install Root قرار داشته باشند.
15. حذف Setup نباید بدون تصمیم مشخص، Data Root و دیتابیس فروشگاه را پاک کند.

## B. Client Setup

1. روی PC مشتری جدا از Server نصب شود.
2. Install Path قابل انتخاب باشد.
3. Server URL قابل تنظیم باشد.
4. Registration Token قابل ورود باشد.
5. Agent Name قابل تنظیم باشد.
6. Agent executable self-contained باشد؛ نصب .NET Runtime جداگانه لازم نباشد.
7. Agent registration موفق شود.
8. AgentDevice در Dashboard ظاهر شود.
9. Station assignment قابل انجام باشد.
10. پس از logon/restart Agent دوباره اجرا شود.
11. Lock/Unlock واقعی روی PC تست شود.
12. Customer login فقط برای همان دستگاه مجاز باشد.

## C. Reboot / Recovery

Server:
- reboot
- service auto-start
- DB recovery
- health

Client:
- reboot
- agent startup
- state restore
- heartbeat

## D. Physical 2–3 PC Gate

از سند `docs/20-physical-validation-2-3pc-2026-10-05.md` استفاده شود.

حداقل:
- registration / identity
- lock/unlock
- customer login / ownership
- session start / timer
- game launch / account lease
- session end / release
- disconnect / reconnect
- two-agent concurrency
- server outage
- agent restart
- backup / restore

## E. Release Evidence

برای Release نهایی باید ثبت شود:
- exact Git SHA
- installer version
- Server Setup SHA-256
- Client Setup SHA-256
- install path
- data root
- server IP
- client IPs
- Windows Service status
- health response
- physical test result
- known residual issues

## Rule

`Build green` و `Installer compiled` کافی نیست.

Release فقط بعد از:
`Installer build` → `Install` → `Production smoke` → `2–3 PC physical validation`
