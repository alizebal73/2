# Setup-First — نقشه رسمی اولین نسخه نصب‌شدنی

تاریخ: 2026-10-06

## هدف اصلی فعلی
هدف فوری پروژه «اولین Setup قابل نصب و تست» است؛ نه تکمیل هم‌زمان تمام Release Readiness و همه findings ممیزی.
نسخه اول باید روی سیستم واقعی نصب شود تا رفتار واقعی Server/Client مشخص شود. بعد از آن، باگ‌های واقعی بر اساس reproduction اصلاح می‌شوند و تا حد امکان فقط component مسئول Update می‌شود.

## معماری عملیاتی مورد انتظار

Server:
- روی PC سرور نصب می‌شود.
- Install Path قابل انتخاب است.
- DataRoot جدا از Install Path است.
- Windows Service با نام GameNet Manager Server اجرا می‌شود.
- پورت پایه فعلی 5080 است.

Client:
- روی PC مشتری جداگانه نصب می‌شود.
- Install Path قابل انتخاب است.
- Server URL و Registration Token و Agent Name دریافت می‌شوند.
- Agent با Server ثبت و در Dashboard قابل مشاهده می‌شود.

Database/DataRoot:
- از پوشه نصب جدا می‌ماند.
- Update نرم‌افزار نباید Database/DataRoot را پاک کند.
- Migration باید قبل از تغییر schema و با امکان backup انجام شود.

## ترتیب کار
1. Build Server Setup + Client Setup.
2. تولید SHA-256 و ثبت نسخه.
3. نصب Server واقعی.
4. بررسی Windows Service و /api/health.
5. باز کردن Dashboard از LAN.
6. نصب Client روی PC جدا.
7. Agent registration و Station assignment.
8. Customer login.
9. Session start/timer/end.
10. حداقل یک جریان مالی واقعی.
11. ثبت اولین باگ‌های واقعی.
12. اصلاح کوچک‌ترین component مسئول.
13. Regression test.
14. ساخت update محلی برای همان component.
15. نصب update روی نسخه قبلی.
16. تکرار چرخه.

## قانون Bug → Patch → Update
برای هر باگ:
- ابتدا reproduce واقعی.
- علت در کوچک‌ترین component ممکن پیدا شود.
- fix فقط همان component تا حد ممکن.
- regression test اضافه شود.
- فقط package لازم build شود.
- local update تولید شود.
- روی نسخه نصب‌شده تست شود.
- version و تغییرات ثبت شوند.

مثال:
اگر فقط Client مشکل دارد، Server و Database بی‌دلیل دوباره نصب یا تغییر داده نشوند.
اگر فقط Dashboard مشکل دارد، Server Domain/Database بی‌دلیل تغییر داده نشود.
اگر فقط Server API مشکل دارد و قرارداد Client حفظ شده، فقط Server update شود.
اگر API/DB contract breaking است، compatibility یا migration اجباری است و Update باید به‌صورت هماهنگ منتشر شود.

## Update فاز اول
Update ابتدا محلی است:
- یک پوشه local update/package روی Server یا USB/شبکه داخلی.
- Cloud URL در فاز بعد.
Update باید نسخه قبلی سالم را قابل برگشت نگه دارد و در صورت failure امکان rollback داشته باشد.

## چیزهایی که فعلاً نباید پروژه را متوقف کنند
Findingهای غیرمرتبط با نصب پایه نباید مانع اولین Setup شوند:
- گزارش‌ها و polish UI
- refactor بزرگ Program.cs
- کامل‌سازی همه security hardening
- physical certification نهایی
- همه جزئیات Finance/Inventory که در مسیر پایه استفاده نمی‌شوند
اما هر باگی که Server نصب، Client registration، Agent ارتباط، Database startup یا جریان پایه Session را متوقف کند، blocker مستقیم Setup محسوب می‌شود.

## نسخه فعلی و نکته مهم
برای First Installable Build نسخه پایه `0.7.0` ثبت شده است. `ProductVersion`، `MinimumClientVersion` و `RecommendedClientVersion` فعلاً با همین نسخه هماهنگ شده‌اند و `Build-Setup.ps1` نیز همین مقدار را به‌عنوان پیش‌فرض استفاده می‌کند. بعد از اولین نصب، Versioning کامل برای Updateهای بعدی جداگانه انجام می‌شود.

## Build command
Build از روی repository root و ترجیحاً روی self-hosted Windows runner کاربر اجرا شود:

powershell -NoProfile -ExecutionPolicy Bypass -File .\build\installer\Build-Setup.ps1 -Version <VERSION>

خروجی مورد انتظار:
- artifacts/installer/out/GameNetManager-Server-Setup-<VERSION>.exe
- artifacts/installer/out/GameNetManager-Client-Setup-<VERSION>.exe
- artifacts/installer/out/SHA256SUMS.txt

## Definition of First Installable Build
وقتی این موارد انجام شد، اولین نسخه نصب‌شدنی داریم:
- هر دو Setup ساخته شده‌اند.
- Server روی یک PC واقعی نصب شده.
- Service بالا آمده.
- /api/health = 200.
- Dashboard از LAN قابل دسترسی است.
- Client روی PC جدا نصب شده.
- Agent registration موفق است.
- Agent/Station در Dashboard دیده می‌شود.

این نسخه «Final Release» نیست؛ فقط اولین نسخه واقعی برای تست و کشف باگ است.

## وضعیت CI
CI در دوره Setup-First نباید با هر commit پروژه را دوباره اجرا کند. اجرای acceptance CI فقط وقتی لازم است انجام شود.

## Checkpoint برای چت بعدی
مخزن: alizebal73/2
هدف: First Installable Build
اولویت: Setup → Install Server → Install Client → Real Test
روش اصلاح بعدی: Bug → Reproduce → Smallest Component Fix → Regression → Local Update → Retest
اصل معماری: component-scoped changes؛ عدم بازسازی بی‌دلیل کل برنامه