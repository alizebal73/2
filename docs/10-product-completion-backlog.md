# Product Completion Backlog — تکمیل جامع GameNet Manager

این فایل فهرست مرجع مواردی است که در مرور جامع محصول شناسایی شده‌اند. ترتیب اجرا وابستگی‌ها را رعایت می‌کند و هر مورد فقط پس از پیاده‌سازی + تست به وضعیت Done می‌رسد.

## قوانین اجرا
- همه متن‌های قابل مشاهده کاربر فارسی و قابل‌فهم باشند.
- Server تنها منبع حقیقت است.
- Mock نباید در نسخه نهایی به‌عنوان قابلیت واقعی نمایش داده شود.
- هر عملیات حساس: Permission + Persistence + Audit + خطای فارسی + Reverse/Cancel در صورت نیاز.
- هر مرحله قبل از رفتن به مرحله بعد باید Build/Test/CI و در صورت UI، تست تعاملی لازم را پاس کند.
- وابستگی‌ها زودتر از مصرف‌کننده پیاده‌سازی می‌شوند.




## Stage 13 — برش‌های گزارش و ممیزی — مرجع اجرایی فعلی

## Stage 13.1 — برش اول واقعی: Audit Explorer — 2026-10-05

### وضعیت

- ✅ **Stage 13.1 — Audit Explorer** — روی `main` ادغام و با Run #1380 روی exact-head و سپس Run #1383 روی `main` تأیید شد.
- Server دارای `AuditLogService` و endpoint واقعی `GET /api/audit` با Permission سروری `audit.view` است.
- فیلترهای فعلی: بازهٔ زمانی، کاربر، عملیات، دامنه، جست‌وجوی آزاد و صفحه‌بندی.
- Dashboard بخش Audit را از Mock خارج کرده و از Server دادهٔ واقعی می‌گیرد.
- کاربری که فقط `audit.view` دارد نیز می‌تواند وارد Reports شود؛ Finance endpointها برای او به‌صورت بی‌دلیل فراخوانی نمی‌شوند.
- تست واحد Query/Filter/Pagination، Browser Smoke و Server API Smoke برای این برش اضافه شده‌اند.

### Gate این برش

- ✅ Build
- ✅ .NET Tests
- ✅ EF validation
- ✅ Server/Migration/Health Smoke + Stage 13 Audit API Smoke — Run #1380 / Main Run #1383
- ✅ Dashboard Build/Lint — Run #1380 / Main Run #1383
- ✅ Dashboard Browser Smoke — Run #1380 / Main Run #1383

### مرز بعدی

- ⏭️ Stage 13.2 — گزارش واقعی جلسات و ایستگاه‌ها
- ⏭️ Stage 13.3 — گزارش مشتری/VIP و Users/Shift
- ⏭️ Stage 13.4 — Fine-Grained Permission/Scope + Export
- ⏭️ Stage 13.5 — Notification/Event Queue

> Stage 13.1 پس از سبز شدن exact-head و عبور Main از CI Done محسوب می‌شود؛ این قانون برای هر برش بعدی نیز برقرار است.

## Stage 13.2 — برش دوم واقعی: Sessions & Stations — 2026-10-05

### وضعیت
- ✅ **Stage 13.2 — Sessions & Stations** — روی `main` ادغام و با Run #1391 روی exact-head و Run #1392 روی `main` تأیید شد.
- Server دارای `SessionReportService` و endpoint واقعی `GET /api/reports/sessions` است؛ منبع حقیقت `Session + Station + Customer + AppUser` است.
- فیلترها: بازهٔ زمانی، ایستگاه، منطقه، اپراتور، وضعیت جلسه و جست‌وجوی مشتری.
- Summary واقعی: تعداد جلسات، زمان قابل‌صورتحساب، درآمد، میانگین مدت و تفکیک ایستگاه‌ها.
- Dashboard پنل واقعی Sessions & Stations دارد؛ pagination و خروجی CSV صفحهٔ قابل مشاهده را ارائه می‌کند.
- Unit Test، Server API Smoke و Browser Smoke برای این برش سبز شده‌اند.
- در همان برش، Restart/Rollback Agent نیز harden شد: Server URL در چرخهٔ Watchdog به‌صورت صریح به child Agent منتقل می‌شود و Stage 10 Update/Rollback Smoke روی exact-head و main سبز شد.

### Gate این برش
- ✅ Build
- ✅ .NET Tests
- ✅ EF validation
- ✅ Server/Migration/Health Smoke + Stage 13.2 Sessions API Smoke — Run #1391 / Main Run #1392
- ✅ Dashboard Build/Lint — Run #1391 / Main Run #1392
- ✅ Dashboard Browser Smoke — Run #1391 / Main Run #1392

### مرز بعدی
- ⏭️ Stage 13.3 — گزارش مشتری/VIP و Users/Shift
- ⏭️ Stage 13.4 — Fine-Grained Permission/Scope + Export
- ⏭️ Stage 13.5 — Notification/Event Queue

> Stage 13.2 پس از سبز شدن exact-head و عبور Main از CI Done محسوب می‌شود.

