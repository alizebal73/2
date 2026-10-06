# CURRENT RELEASE GATE — 2026-10-05

> **این بخش مرجع اجرایی فعلی است و بر متن‌های تاریخی پایین فایل غالب است.**

- **Main checked:** `f848c792c8bb628b9c2bebefa2caf66a323089cd`
- **Latest CI at audit time:** Run #1532 — ✅ success
- **Stage 14.6 Software Pre-flight:** ✅ complete
- **Stage 14.6 Physical Validation:** 🟡 pending
- **Installer:** ⛔ blocked until the Release Pre-flight blockers are resolved
- **Detailed audit:** `docs/21-release-preflight-2026-10-05.md`

### Installer blockers identified

1. **Production Station provisioning:** fresh Production DB does not seed real Stations, and the current Dashboard has no server-backed Station create/edit path.
2. **Server LAN hosting:** installed Server must explicitly bind to the LAN on the release port (target 5080).
3. **Secret provisioning:** Installer/first-run setup must safely provide `GAMENET_ADMIN_PASSWORD` and `Agent:RegistrationToken`; neither may be hard-coded.
4. **Writable data root:** SQLite/DataProtection/backup data currently live under `ContentRootPath/App_Data`; release packaging must avoid protected install-directory write failures.
5. **Server service hosting:** define reliable auto-start/recovery hosting for the Server (preferred: native Windows Service).
6. **Client runtime strategy:** current Client publish is framework-dependent; release must either install .NET 10 runtime or switch to self-contained Client packaging.
7. **Production CI gate:** current Server smoke uses `ASPNETCORE_ENVIRONMENT=Development`; a true Production startup/provisioning smoke is still needed.

**Rule:** Do not build the final Installer until these blockers are resolved and the Release Pre-flight is green.

---

# Product Completion Backlog — تکمیل جامع GameNet Manager

این فایل فهرست مرجع مواردی است که در مرور جامع محصول شناسایی شده‌اند. ترتیب اجرا وابستگی‌ها را رعایت می‌کند و هر مورد فقط پس از پیاده‌سازی + تست به وضعیت Done می‌رسد.

## قوانین اجرا
- همه متن‌های قابل مشاهده کاربر فارسی و قابل‌فهم باشند.
- Server تنها منبع حقیقت است.
- Mock نباید در نسخه نهایی به‌عنوان قابلیت واقعی نمایش داده شود.
- هر عملیات حساس: Permission + Persistence + Audit + خطای فارسی + Reverse/Cancel در صورت نیاز.
- هر مرحله قبل از رفتن به مرحله بعد باید Build/Test/CI و در صورت UI، تست تعاملی لازم را پاس کند.
- وابستگی‌ها زودتر از مصرف‌کننده پیاده‌سازی می‌شوند.




## Stage 13 — برش اول واقعی: Audit Explorer — 2026-10-05

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

- ✅ Stage 13.2 — گزارش واقعی جلسات و ایستگاه‌ها
- ✅ Stage 13.3 — گزارش مشتری/VIP و Users/Shift
- ✅ Stage 13.4 — Fine-Grained Permission/Scope + Export
- ✅ Stage 13.5 — Notification/Event Queue

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
- ✅ Stage 13.4 — Fine-Grained Permission/Scope + Export
- ✅ Stage 13.5 — Notification/Event Queue

> Stage 13.2 پس از سبز شدن exact-head و عبور Main از CI Done محسوب می‌شود.

## Stage 13.3 — برش سوم واقعی: Customer/VIP + Users/Shift — 2026-10-05

### وضعیت
- ✅ **Stage 13.3 — Customer/VIP + Users/Shift** — روی `main` ادغام و با Run #1402 روی exact-head و Run #1403 روی `main` تأیید شد.
- Customer/VIP report: endpoint واقعی `GET /api/reports/customers` با منبع حقیقت Customer/VipPackage/Session/Draft Invoice.
- Users/Shift report: endpoint واقعی `GET /api/reports/users-shifts` با منبع حقیقت AppUser/EmployeeProfile/Payroll/Shift/Expense/InvoicePayment/Session.
- Permission masking سروری برای کیف پول/بدهی مشتری و داده‌های حقوقی اجرا شده است؛ Dashboard فقط داده‌ای را می‌گیرد که کاربر مجاز به دیدن آن است.
- Dashboard Reports دارای پنل‌های واقعی Customer/VIP و Users/Shift با فیلتر، summary، pagination و CSV export صفحهٔ جاری است.
- Unit Test، Server API Smoke و Browser Smoke برای هر دو پنل سبز شده‌اند.
- در CI محدودیت ترجمهٔ DateTimeOffset در SQLite شناسایی و گزارش Users/Shift به الگوی materialize-then-filter هم‌راستا با SessionReportService اصلاح شد.

### Gate این برش
- ✅ Build
- ✅ .NET Tests
- ✅ EF validation
- ✅ Server/Migration/Health Smoke + Stage 13.3 Customer/VIP + Users/Shift API Smoke — Run #1402 / Main Run #1403
- ✅ Dashboard Build/Lint — Run #1402 / Main Run #1403
- ✅ Dashboard Browser Smoke — Run #1402 / Main Run #1403

### مرز بعدی
- ✅ Stage 13.4 — Fine-Grained Permission/Scope + Export
- ✅ Stage 13.5 — Notification/Event Queue

> Stage 13.3 پس از سبز شدن exact-head و عبور Main از CI Done محسوب می‌شود.


## Stage 13.4 — برش چهارم واقعی: Fine-Grained Permission/Scope + Export — 2026-10-05

### وضعیت
- ✅ Stage 13.4 روی main ادغام و با Run #1411 روی exact-head و Run #1412 روی main تأیید شد.
- Scope گزارش‌ها در Server-side enforce شده و Export گزارش‌ها Server-backed و Audit شده است.
- Unit Test، API Smoke و Browser Smoke سبز شده‌اند.

### Gate این برش
- ✅ Build
- ✅ .NET Tests
- ✅ EF validation
- ✅ Server/Migration/Health Smoke + Stage 13.4 API Smoke — Run #1411 / Main Run #1412
- ✅ Dashboard Build/Lint — Run #1411 / Main Run #1412
- ✅ Dashboard Browser Smoke — Run #1411 / Main Run #1412

## Stage 13.5 — برش پنجم واقعی: Notification/Event Queue — 2026-10-05

### وضعیت
- ✅ Stage 13.5 روی main ادغام و با Run #1421 روی exact-head و Run #1422 روی main تأیید شد.
- Notificationها Server-backed و durable هستند و رویدادهای مهم مانند low-stock و Agent offline/command-failure را ثبت می‌کنند.
- Inbox کاربر authenticated فقط اعلان‌های متعلق به همان کاربر را می‌خواند و read/read-all نیز Server-side enforce می‌شود.
- Dashboard اعلان‌ها را از Server می‌گیرد و با SignalR در صورت ایجاد رویداد refresh می‌کند.
- Queue، list و read-state برای SQLite provider-safe شده و lifetime سرویس در AgentPresenceMonitor صحیح است.

### Gate این برش
- ✅ Build
- ✅ .NET Tests
- ✅ EF validation
- ✅ Server/Migration/Health Smoke + Stage 13.5 Notification API Smoke — Run #1421 / Main Run #1422
- ✅ Dashboard Build/Lint — Run #1421 / Main Run #1422
- ✅ Dashboard Browser Smoke — Run #1421 / Main Run #1422

### نتیجه
**✅ Stage 13 — COMPLETE از نظر مهندسی نرم‌افزار (13.1 تا 13.5).**

## آخرین وضعیت تأییدشده — Stage 12 / 2026-10-05

این بخش مرجع اجرایی فعلی است و بر وضعیت واقعی HEAD شاخه غلبه دارد.

- ✅ Stage 1 تا 10: هسته‌های نرم‌افزاری قبلاً در CI تثبیت شده‌اند.
- ✅ Stage 11: Game Catalog + Account Pool/Lease از Mock خارج شده و مسیر واقعی آن در Server/Agent فعال است.
- ✅ Stage 12: زنجیرهٔ واقعی Session → Game → Agent → Account Lease → Credential → Session End → Release روی همان HEAD نهایی تست شد.
- ✅ Real Game Apply/Sync و persistence مانیفست Agent در CI تأیید شد.
- ✅ authoritative activeUsers هنگام Session فعال و بازگشت آن به صفر پس از پایان Session تأیید شد.
- ✅ Account Pool از Free → InUse → Free در همان چرخهٔ واقعی تأیید شد.
- ✅ Build، Test، EF Model Validation، Server/Migration/Health Smoke، Agent/Session/Lease E2E و Dashboard Browser Smoke همگی روی HEAD یکسان سبز شدند.
- ✅ CI نهایی کد: Run #1369 روی commit ae18da81761b82dcea5d11d69934fd47426c7f3d.
- ⬜ Gateهای بیرون از CI همچنان باقی‌اند: Backup/Restore واقعی، Validation فیزیکی روی 2–3 PC و سپس rollout کنترل‌شدهٔ 40+ PC.
- ⏭️ مرحلهٔ نرم‌افزاری بعدی پس از این checkpoint: Stage 13 — Reporting & Audit / Notification Queue / Fine-Grained Scope.

> قانون: Stage 12 از نظر مهندسی نرم‌افزار Done است؛ Gateهای فیزیکی و Release تا زمان اجرای واقعی همچنان Done محسوب نمی‌شوند.

## Stage 14 — UI Hardening / Settings IA / Dashboard UX — 2026-10-05

### وضعیت
- ✅ **Stage 14.1 — Settings Information Architecture** — روی `main` ادغام و با exact-head Run #1429 تأیید شد.
- Settings دارای جست‌وجوی دسته‌ها، navigation پایدار، anchorهای یکتا و اطلاع‌رسانی شفاف local-only است.
- Browser Smoke برای Settings و سناریوهای مرتبط با Notification نیز سبز شده است.
- ✅ Merge commit: `4bc6cd4484647eb6eb029aef53c947149f9e1da9`

### Gate Stage 14.1
- ✅ Build
- ✅ .NET Tests
- ✅ EF validation
- ✅ Server/Migration/Health Smoke
- ✅ Dashboard Lint/Build/Preview
- ✅ Dashboard Browser Smoke — Run #1429

### Stage 14.2 — Dashboard PC grouping / Internet 1-2 + dense responsive hardening

**✅ COMPLETE**

- ✅ ممیزی ثابت کرد UI از `station.network` پشتیبانی داشت اما Server مقدار Network را authoritative برنمی‌گرداند.
- ✅ `Station.Network` به مدل Server اضافه و با EF migration + model snapshot ثبت شد.
- ✅ `GET /api/dashboard` مقدار `Station.Network` را برمی‌گرداند.
- ✅ Demo Seed: PC-01..20 → Internet 1 و PC-21..40 → Internet 2.
- ✅ grouping صریح و پایدار Internet 1 / Internet 2 با برچسب فارسی.
- ✅ گزینه‌های grouping موجود Status / VIP-Normal / Remaining Time حفظ شدند؛ grouping/filtering فقط presentation است.
- ✅ فیلتر/گروه‌بندی فقط presentation است و Session / Server Truth را تغییر نمی‌دهد.
- ✅ responsive hardening برای workspace، toolbar و کارت‌های متراکم در viewport کوچک.
- ✅ Browser Smoke برای grouping، جابه‌جایی فیلتر grouping و viewport متراکم 520px سبز شد.
- ✅ Exact-head CI: Run #1435.
- ✅ Main certification: Run #1436 (Attempt 2) روی merge commit `63ac3f67357a0970df504a62c9ab16cc1ba4f53a`.

### Stage 14.3 — Accessibility / UI / operational polish

**✅ COMPLETE**

- ✅ Keyboard-accessible skip link to the primary content area.
- ✅ Current navigation item exposes aria-current="page".
- ✅ API / SignalR connection states expose semantic status text to assistive technology.
- ✅ Notification control exposes expanded state and a labelled notification region.
- ✅ Reduced-motion preference is respected for transitions and animations.
- ✅ Dashboard distinguishes the last valid snapshot from live data when API connectivity is lost.
- ✅ Browser Smoke added for keyboard navigation, notification semantics and stale-state messaging.
- ✅ Exact-head CI: Run #1439.
- ✅ Main certification: Run #1440 on merge commit b69fa15c7ea166a65c5932f8ceb11fb34429eb4e.

### مرز بعدی
- ⏭️ Stage 14.4 — Server-backed Settings
- ⬜ Backup/Restore واقعی
- ⬜ Validation فیزیکی 2–3 PC

> Stage 14.3 only becomes releasable after both exact-head CI and Main certification are green; both gates are now green.

### Stage 14.4 — Server-backed Settings

**✅ COMPLETE**

- ✅ Server-persistent AppSetting global scope with EF migration + snapshot.
- ✅ Independent settings.view / settings.manage permissions.
- ✅ GET/PUT /api/settings with typed validation and Persian errors.
- ✅ Atomic persistence and AuditLog for changed settings.
- ✅ Dashboard loads server settings and provides explicit server-save action.
- ✅ Hotkeys and UX section locks remain explicitly local; they are not falsely presented as server-authoritative.
- ✅ Server API Smoke added: read → update → validation reject → restore.
- ✅ Browser Smoke added for server-backed settings synchronization/save.
- ✅ Exact-head CI: Run #1445 on exact Stage 14.4 head `5c6b4575ec46f387e70e715d87ff79e340cf7c14`.
- ✅ Main certification: Run #1446 on merge commit `b3a749db8974a7f75cedc0e9168bff94fc38cad9`.

### مرز بعدی
- ⬜ Backup/Restore واقعی
- ⬜ Validation فیزیکی 2–3 PC

> Stage 14.4 is **DONE**: exact-head CI and Main certification are both green.

### Stage 14.5 — واقعی‌سازی Backup / Restore

**✅ COMPLETE**

- ✅ موتور Backup واقعی برای SQLite با Online Backup API پیاده شده است.
- ✅ نسخهٔ پشتیبان شامل دیتابیس و DataProtection Keys است.
- ✅ SHA-256 و SQLite integrity_check هنگام Verify/Restore بررسی می‌شوند.
- ✅ Retention بر اساس backupKeep اجرا می‌شود.
- ✅ بکاپ خودکار روزانه بر اساس backupAuto و backupHour به‌صورت Server-side اجرا می‌شود.
- ✅ Permissionهای جداگانهٔ backup.view / backup.manage / backup.restore و Audit برای عملیات ایجاد/Verify/Restore اضافه شده‌اند.
- ✅ Restore روی دیتابیس زنده انجام نمی‌شود؛ Pending Restore در startup قبل از Migration اعمال می‌شود و Pre-Restore safety backup ساخته می‌شود.
- ✅ Settings UI از کنترل‌های غیرفعال به Backup/Verify/Restore واقعی Server متصل شده است.
- ✅ تست واحد مسیر Create → Verify → Prepare Restore → Apply Restore اضافه شده است.
- ✅ Exact-head CI: Run #1461 روی exact-head d9304b271321f22d9323553fbb017eb3effcdf6f.
- ✅ Main certification: Run #1462 روی merge commit 7dc7d4b8b7cff2e02c4eb302f024161d5ed05049.
- ✅ همهٔ Gateهای CI روی Main سبز شدند.

### مرز بعدی
- 🟡 Stage 14.6 — Validation فیزیکی 2–3 PC
- ⬜ rollout کنترل‌شدهٔ 40+ PC

> Stage 14.5 از نظر مهندسی نرم‌افزار **DONE** است؛ Gate بعدی Stage 14.6، اجرای واقعی روی 2–3 PC است.

### Stage 14.6 — Validation فیزیکی 2–3 PC

**🟡 IN PROGRESS — پروتکل آماده، اجرای فیزیکی هنوز انجام نشده**

- ✅ پروتکل رسمی تست در `docs/20-physical-validation-2-3pc-2026-10-05.md` ثبت شد.
- ✅ مسیر حداقل شامل Registration → Online → Lock/Unlock → Customer Login → Session Start → Timer → Disconnect → Recovery → Reconnect → Session End → Release است.
- ✅ هم‌زمانی حداقل دو PC و جلوگیری از cross-device ownership در Gate تعریف شد.
- ✅ Agent identity persistence بعد از restart و Safe Offline/Recovery در Gate تعریف شد.
- ✅ شواهد لازم برای Client / Server / Dashboard / Audit / Notification مشخص شد.
- ⬜ اجرای واقعی روی PC-01 و PC-02
- ⬜ اجرای اختیاری PC-03
- ⬜ PASS نهایی Stage 14.6

> Stage 14.6 فقط با شواهد واقعی محیط فروشگاه PASS می‌شود؛ CI جای این Gate را نمی‌گیرد.


### وضعیت ممیزی نرم‌افزاری قبل از PASS فیزیکی

- ✅ **Client Experience از نظر مسیر عملیاتی Server/Agent-backed شد.**
- ✅ Catalog بازی/بوفه از `/api/client/catalog` می‌آید و دادهٔ GameNet credential به Client برگردانده نمی‌شود.
- ✅ Login/Identity/Customer State از Server خوانده می‌شوند و Lock state authoritative روی Server باقی می‌ماند.
- ✅ درخواست‌های message/charge/move/unlock/buffet از endpoint واقعی `/api/client/request` ثبت و Audit/Permission مسیر سروری را طی می‌کنند.
- ✅ Lock و Logout-Lock از مسیر واقعی Agent command اجرا می‌شوند.
- ✅ Launch/Stop بازی از مسیر `Client → Server → Agent → Process` عبور می‌کند؛ Server مالکیت Session/CustomerLogin/Station/Game را enforce می‌کند و Agent درست قبل از Launch دوباره Session زنده را validate می‌کند.
- ✅ Process روی Client با `SessionId + GameId` مالکیت می‌شود و Stop مربوط به Session دیگر پذیرفته نمی‌شود.
- ✅ Fakeهای billing/approval در Dashboard حذف شدند و محدودیت تخفیف اپراتور Server-side enforce می‌شود.
- ✅ Exact-head CI: Run #1528.
- ✅ Main certification: Run #1529 روی merge commit `bf88dd1d91875d12d33c58607776d5e928279bfc`.
- 🟡 فقط **Physical Validation 2–3 PC** باقی مانده است؛ این Gate با CI جایگزین نمی‌شود.
- Agent Test Session Flow برای تست فیزیکی instrumented همچنان در دسترس است.

## آخرین وضعیت اجرایی — Stage 13.5 / 2026-10-05

این بخش مرجع اجرایی فعلی و بر وضعیت تاریخی مراحل قبلی غالب است.

- ✅ Stage 13.1 Audit Explorer — Run #1380 / Main #1383.
- ✅ Stage 13.2 Sessions & Stations — Run #1391 / Main #1392.
- ✅ Stage 13.3 Customer/VIP + Users/Shift — Run #1402 / Main #1403.
- ✅ Stage 13.4 Permission/Scope + Export — Run #1411 / Main #1412.
- ✅ Stage 13.5 Notification/Event Queue — Run #1421 / Main #1422.
- ✅ Main certified Stage 13 merge commit: `c3f579b38ad1704a62d775c6f45921b24624d43c`.
- ✅ Stage 13 — COMPLETE از نظر مهندسی نرم‌افزار.
- ⏭️ مرحلهٔ نرم‌افزاری بعدی: Stage 14 — UI Hardening / Settings Information Architecture / Dashboard UX.
- ⬜ Gateهای بیرون از CI همچنان باقی‌اند: Backup/Restore واقعی، Validation فیزیکی روی 2–3 PC و سپس rollout کنترل‌شدهٔ 40+ PC.

> قانون: Stage 13 از نظر مهندسی نرم‌افزار Done است؛ Gateهای فیزیکی و Release همچنان Done محسوب نمی‌شوند.

## تصمیم‌های تأییدشده از بازبینی دوم محصول

این موارد از پیشنهادهای بازبینی خارجی بررسی و تأیید شدند و از این به بعد بخشی از نقشهٔ رسمی پروژه‌اند:

### 1) Vertical Slice واقعی PC — اولویت بالای فنی
- قبل از صیقل‌دادن قابلیت‌های فرعی، یک برش عمودی واقعی از PC ساخته و روی ۲ تا ۳ رایانهٔ واقعی آزمایش شود.
- حداقل سناریو: اتصال کلاینت به سرور، Lock/Unlock، شروع جلسه، پایان جلسه، تایمر زنده، قطع/وصل سرور و رفتار امن در قطع ارتباط.
- این کار بعد از رسیدن به مرحلهٔ ۸ (PC Agent Foundation) وارد اجرای واقعی می‌شود و دروازهٔ مهم قبل از گسترش فرمان‌های کلاینت است.

### 2) احراز هویت واقعی داشبورد
- حالت «نقش Demo» فقط ابزار توسعه است و نباید در محصول نهایی بماند.
- ورود داشبورد باید با حساب واقعی و Permission سمت سرور انجام شود.
- نقش‌های عملیاتی از Authentication/Authorization واقعی خوانده شوند.

### 3) امنیت کلاینت مشتری
- هر مسیر ورود اپراتور، دسترسی نامحدود، ورود مستقیم به Windows یا آزادسازی Shell در UI مشتری باید حذف یا فقط پشت احراز هویت مدیریتی معتبر و Permission سروری باشد.
- UI مشتری منبع مجوز نیست؛ Agent و Server تصمیم نهایی را می‌گیرند.

### 4) شکستن Program.cs — به‌عنوان Hardening معماری
- Program.cs با رشد دامنه‌ها باید به Startup/Endpoint/Infrastructure/Domainهای جدا شکسته شود.
- این کار باید مرحله‌ای و با حفظ رفتار/تست انجام شود؛ بازنویسی زودهنگام بدون نیاز معماری انجام نشود.

### 5) CI برای Pull Request
- علاوه بر push به main، CI باید روی Pull Request نیز اجرا شود.
- Runner همچنان self-hosted ویندوزی خود پروژه باقی می‌ماند.
- هدف: جلوگیری از ورود Build/Test شکست‌خورده به main.

### 6) Backup خودکار دیتابیس
- Backup خودکار SQLite/داده‌های مالی از قبل از ورود به استفادهٔ واقعی اجباری است.
- Backup باید قابل‌بازیابی و قابل‌بررسی باشد؛ فقط «فایل کپی شد» کافی نیست.
- Retention و محل Backup باید در Settings قابل کنترل باشد.

### 7) تست E2E «یک روز کاری کامل»
سناریوی مرجع:
شروع شیفت → شروع جلسه → شارژ → بوفه → تمدید/توقف → تسویه → Refund/Reverse → بستن شیفت.
- این تست باید بعد از تثبیت مسیرهای واقعی سرور مرحله‌به‌مرحله فعال شود و در Release Gate استفاده شود.

### 8) Hardening ظاهری و عملیاتی داشبورد
- هدر در مانیتور 1366×768 نباید به‌خاطر وضعیت‌های فرعی چند ردیف اشغال کند.
- وضعیت اتصال فقط در حالت لازم برجسته شود و «تلاش مجدد» در حالت قطع نمایش داده شود.
- Footer نباید روی محتوای آخرین ردیف بیفتد.
- Dense Operator Mode و کارت‌های جمع‌وجور برای ۴۰ PC حفظ/تقویت شوند؛ الزام «همه ۴۰ کارت در یک صفحه» وجود ندارد.
- فونت و کنتراست در اندازهٔ مناسب اپراتوری کنترل شوند و گزینهٔ فونت بزرگ‌تر واقعاً کاربردی باشد.
- برای صفحات بزرگ، عرض محتوا و تراکم اطلاعات متعادل شود.
- Responsive/Tablet برای استفادهٔ اپراتور در سالن تکمیل شود.

### 9) آیکون‌ها و کلاینت واقعی
- Emojiهای وابسته به فونت سیستم در مسیرهای مهم عملیاتی مرجع نهایی نیستند؛ برای کنترل‌های حساس از SVG/Iconها استفاده شود.
- صفحهٔ بعد از Login مشتری باید وضعیت جلسه، تایمر، مبلغ/اعتبار، بوفه، درخواست کمک، بازی‌ها و پایان جلسه را پوشش دهد.
- Kiosk/Fullscreen برای کلاینت واقعی بخشی از امنیت/UX مرحلهٔ کلاینت است.

### 10) وضعیت قطع ارتباط و دادهٔ آخر
- در قطع سرور/شبکه، برنامه باید آخرین وضعیت معتبر را از وضعیت «دادهٔ زنده» متمایز نشان دهد.
- صفحه نباید فقط با یک پیام خطا عملاً غیرقابل‌استفاده شود.
- Retry/Recovery باید قابل‌فهم و فارسی باشد.

### 11) پاک‌سازی Mock فقط بعد از جایگزینی واقعی
- Mock هر دامنه فقط پس از اتصال نسخهٔ واقعی Server/Agent همان دامنه و عبور تست حذف شود.
- هیچ قابلیت Mock نباید به‌عنوان قابلیت واقعی به اپراتور ارائه شود.

### 12) دسترس‌پذیری و یکنواختی اعداد
- Contrast متن‌های کم‌رنگ روی زمینهٔ تیره بررسی و تثبیت شود.
- قاعدهٔ نمایش اعداد فارسی/لاتین برای دادهٔ محلی، شناسه‌ها و کلیدهای فنی ثابت باشد.
- کنترل‌ها باید بدون اتکا به Emoji یا رفتار پیش‌فرض مرورگر قابل فهم باشند.

### 13) نمای چیدمان فیزیکی ایستگاه‌ها
- یک View اختیاری برای چیدمان فیزیکی PC/PS/میزها ارزش عملیاتی دارد، اما جای Dashboard Card/List را نمی‌گیرد.
- این قابلیت به مرحلهٔ ۱۳/۱۴ منتقل می‌شود و تا قبل از نیاز عملیاتی واقعی ساخته نمی‌شود.

### 14) اعلان قابل‌اعتماد
- رویدادهای مهم مانند پایان اعتبار، پرداخت معوق، قطع سرور/کلاینت و خطای فرمان باید شمارنده/صف اعلان واقعی داشته باشند.
- Sound/notification فقط برای رویدادهای مهم و قابل‌اقدام فعال شود، نه برای هر تغییر لحظه‌ای.

## تصمیمات جدید محصول — Dashboard / Buffet / Reports / Users & Shift

این بخش نیازهای جدید بررسی‌شده را ثبت می‌کند. هیچ موردی در این بخش تا وقتی پیاده‌سازی + تست واقعی نشده، Done محسوب نمی‌شود.

### Dashboard — کارت ایستگاه و گروه‌بندی
- کارت ایستگاه نباید قیمت ساعتی را در نمای عادی نشان دهد؛ قیمت/تعرفه فقط در شروع جلسه، Session Center، تغییر تعرفه و تسویه نمایش داده شود.
- اطلاعات اصلی کارت:
  1. نام/شماره دستگاه
  2. username/شناسه مشتری متصل
  3. نام و نام خانوادگی مشتری
  4. زمان باقی‌مانده جلسه، فقط وقتی Session واقعاً زمان پایان دارد؛ برای Postpaid به‌جای عدد ساختگی وضعیت «بدون زمان پایان»/«باز» نمایش داده شود.
  5. بدهی مشتری
  6. یادداشت کوتاه پروفایل مشتری، حداکثر ۲–۳ کلمه/عبارت کوتاه
  7. وضعیت قابل‌فهم دستگاه: آماده استفاده، در حال استفاده، متوقف، رزرو، خاموش/آفلاین، خارج از سرویس/در تعمیر.
- برای اطلاعات مشتریِ کارت، Server منبع حقیقت است؛ Dashboard نباید با state محلی نام/بدهی/یادداشت را مستقل نگه دارد.
- گروه‌بندی رایانه‌ها باید قابل انتخاب باشد: وضعیت، VIP/عادی، اینترنت ۱/۲، و بازهٔ زمان باقی‌مانده.
- الگوی مناسب: یک «گروه‌بندی اصلی» + فیلترهای مکمل، تا ۴۰ PC به دیوار فیلتر و کارت تبدیل نشوند. بازه‌های زمان باقی‌مانده: تمام‌شده/کمتر از ۱۵ دقیقه/۱۵–۳۰/۳۰–۶۰/بیشتر از ۶۰ دقیقه.
- اینترنت ۱/۲ باید فیلتر و گروه‌بندی باشد، ولی منبع وضعیت شبکه و تغییر شبکه در Client/Server باقی می‌ماند.
- وضعیت «خاموش» فقط وقتی باید نمایش داده شود که Agent/Telemetry واقعاً power/online state را گزارش کند؛ «خارج از سرویس» معادل «خاموش» نیست.

### Buffet — فروش + انبار واقعی
- صفحهٔ بوفه باید دو مسئولیت روشن داشته باشد: «فروش سریع» و «کالا/انبار».
- کاتالوگ کالا: نام، دسته، واحد شمارش، قیمت فروش، قیمت خرید/هزینه، فعال/غیرفعال، کد کالا/بارکد در صورت نیاز، حداقل موجودی، موجودی فعلی و حداکثر/سطح سفارش.
- موجودی فقط یک عدد قابل ویرایش نیست؛ تمام ورود/خروج باید Inventory Transaction داشته باشد: خرید/ورود، فروش، اصلاح موجودی، ضایعات، برگشت و در آینده انتقال بین انبارها.
- هشدارها: کمبود موجودی، صفر شدن موجودی و فهرست «نیازمند خرید». حداقل موجودی برای هر کالا قابل تنظیم باشد.
- فروش بوفه باید موجودی را سروری و اتمیک کم کند؛ Refund/Cancel باید در صورت نیاز موجودی را برگرداند.
- قیمت خرید همراه با موجودی/خرید ثبت شود تا سود واقعی کالا قابل محاسبه باشد؛ صرفاً فروش ناخالص کافی نیست.
- برای نیاز فعلی GameNet-98 یک انبار اصلی کافی است، ولی مدل داده از ابتدا طوری باشد که بعداً Warehouse/Stock Location اضافه شود بدون بازطراحی فروش.
- صفحهٔ بوفه نباید برای «+ محصول جدید» یا تغییر موجودی به Mock تکیه کند.

### Reports — مرکز گزارش، نه یک صفحهٔ شلوغ
- Reports به یک Report Center با دسته‌بندی و پنجره/زیرصفحهٔ مشخص تبدیل شود؛ همهٔ نمودارها، جدول‌ها و گزارش‌ها در یک صفحه ریخته نشوند.
- دسته‌های اصلی:
  - مالی: درآمد، هزینه، سود، پرداخت‌ها، تخفیف، Refund/Reverse، بدهی
  - جلسات و ایستگاه‌ها: کارکرد PC/Console، اشغال، درآمد هر ایستگاه، زمان استفاده
  - مشتری و VIP: مشتری جدید، فعالیت، شارژ، مصرف VIP/بسته، بدهی
  - بوفه و کالا: فروش کالا، پرفروش، سود کالا، موجودی، کمبود، ضایعات و برگشت
  - کاربران و شیفت: فروش هر اپراتور، شیفت، صندوق، اختلاف، هزینه و فعالیت
  - Audit: عملیات حساس و مسئول هر عملیات
- انتخاب بازهٔ زمانی یک کنترل مشترک در بالای Report Center باشد؛ Presetهای امروز/دیروز/۷ روز/۳۰ روز/۶ ماه/سال و بازهٔ دلخواه با تاریخ و ساعت.
- بازهٔ دلخواه در پنل جمع‌شونده/Modal/Drawer باز شود، نه اینکه با چند ردیف کنترل صفحه را هل دهد.
- فیلترهای ایستگاه/اپراتور/روش پرداخت/نوع تراکنش در همان نوار فیلتر و متناسب با گزارش انتخابی نمایش داده شوند؛ فیلتر نامرتبط پنهان شود.
- هر گزارش یک صفحهٔ خلاصهٔ کوچک + Detail Table داشته باشد؛ نمودارها دادهٔ واقعی همان بازه را نشان دهند.
- در 1366×768 هیچ گزارش نباید horizontal overflow ایجاد کند؛ جدول‌های عریض داخل اسکرول افقی کنترل‌شدهٔ همان پنل باشند، نه کل صفحه.
- «ماهانه (۶ ماه)» فعلی باید اصلاح شود؛ منطق فعلی ماه = حدود ۳۰ روز است، پس برچسب و رفتار باید یکی شوند.

### Users & Shift — پرسنل، شیفت، حقوق و حساب پرسنلی
- صفحه به دو حوزهٔ واضح تقسیم شود: «کاربران/دسترسی» و «پرسنل/حقوق/شیفت».
- پروفایل پرسنل:
  - اطلاعات هویتی و تماس
  - نقش و Permission
  - نوع حقوق: ساعتی/ماهانه
  - نرخ ساعتی، حقوق پایه ماهانه، نرخ اضافه‌کاری، پاداش
  - تاریخ شروع همکاری، برنامهٔ کاری و وضعیت فعال
- حساب حقوقی هر پرسنل باید Ledger مستقل داشته باشد:
  - حقوق محاسبه‌شده
  - پرداخت‌های انجام‌شده
  - ماندهٔ قابل پرداخت
  - پیش‌پرداخت حقوق
  - پاداش
  - کسری/کسر مصوب
  - مساعده/وام در صورت نیاز
  - اصلاحات دستی با دلیل
- «مالک چقدر به کارمند بدهکار است» و «کارمند چقدر به مالک بدهکار است» دو ماندهٔ جدا هستند و نباید در یک عدد مبهم ادغام شوند.
- خسارت/کسری ناشی از دستگاه، صندوق یا تخلف باید یک رکورد مستقل با مبلغ، دلیل، شواهد/یادداشت، ثبت‌کننده، وضعیت تأیید و وضعیت تسویه داشته باشد؛ ثبت خسارت نباید خودکار به معنی کسر حقوق باشد.
- پرداخت حقوق باید Receipt/Payment واقعی داشته باشد؛ با هر پرداخت، مبلغ پرداخت‌شده و مانده به‌روز شود و تاریخ/روش پرداخت ثبت شود.
- شیفت باید به شخص، زمان حضور، صندوق، فروش‌ها، Refund، تخفیف، هزینه، موجودی/تحویل و اختلاف صندوق متصل باشد.
- بستن شیفت باید Summary و Handover داشته باشد؛ وضعیت اختلاف صندوق جدا از بدهی/طلب حقوقی پرسنل نگه داشته شود.
- Permission و Approval برای پرداخت حقوق، پاداش، کسری، خسارت، اصلاح حقوق و مشاهده اطلاعات حقوقی حساس الزامی است.

## نقشهٔ اجرایی یکپارچهٔ ۱۵ مرحله‌ای — مرجع فعلی

ممیزی سراسری 2026-10-03 مشخص کرد که Stage 12 واقعی پروژه «Session → Game → Agent → Account Lease → Credential lifecycle» است، نه Reporting. بنابراین نقشهٔ قبلی ۱۴ مرحله‌ای تا این بخش تاریخی است و از اینجا شماره‌گذاری اجرایی به شکل زیر تثبیت می‌شود:

1. **Foundation** — ریپو، Runner، CI، اسکلت Server/Dashboard
2. **Prototype & Behavior Transfer** — انتقال Prototype و رفتارهای پایه به React
3. **Operational Completion** — تکمیل عملیات روزانه اپراتور و UX
4. **Finance & Session Core** — Ledger، Settlement، Reverse، Split Payment و شیفت
5. **Customer & VIP Domain** — مشتری، کیف پول، بدهی، Free Time/Money و محدودیت ورود
6. **Buffet & Inventory Domain** — فروش، انبار، موجودی، ضایعات، مرجوعی و سود
7. **Users & Permissions** — Authentication، Permission، Approval، Payroll و Concurrency
8. **PC Agent Foundation** — Identity، Heartbeat، Health، Telemetry و Command Transport
9. **Real Client Commands & Kiosk** — Lock/Unlock، Kiosk و Agent-driven Session
10. **Client Lifecycle** — Update/Rollback، Recovery، Watchdog و Health
11. **Games & Accounts Data Foundation** — Game Catalog، Account Pool/Lease، atomic allocation؛ Process Detection به carry-forward وابسته به Client telemetry
12. **Session/Game/Agent/Lease/Credential Integration** — اتصال عملیاتی Session↔Game↔Agent↔Lease↔Credential و real Game Apply/Sync
13. **Reporting & Audit** — گزارش‌های مالی/جلسه/ایستگاه/بوفه، Heatmap و Audit Explorer
14. **Reservations & Operations Scale** — Reservation/Waitlist، Event/Tournament، Network State و Multi-cashier
15. **UI/Deployment Hardening** — Primitiveهای UI، DataTable، Keyboard-first، Desktop Shell، Backup/Recovery، Installer، Canary و انتشار نهایی

**جایگاه فعلی:** Stage 12 از نظر مهندسی نرم‌افزار بسته است. Stage 13.1 — Audit Explorer نیز تکمیل و در `main` ادغام شده و Main Run #1383 روی merge commit `24a0654fa6490f415372ab2e7d28f30a0ecfbe89` سبز شده است. مرحلهٔ نرم‌افزاری فعال بعدی **Stage 13.2 — گزارش واقعی جلسات و ایستگاه‌ها** است. Gateهای فیزیکی و Production جدا هستند.

این نقشه مرجع اجرایی فعلی است؛ اسناد تاریخی قبلی شماره‌گذاری قدیمی خود را حفظ می‌کنند، اما برای ادامهٔ توسعه از این جدول استفاده می‌شود.
## وضعیت نهایی مرحلهٔ ۷ — Users & Permissions

### برش تأییدشده تا CI #527
- ✅ احراز هویت واقعی اپراتور با Session سروری، Cookie امن و PBKDF2؛ `/api/auth/login`، `/api/auth/me` و `/api/auth/logout`.
- ✅ مدل AppUser / Permission / AppUserSession / ApprovalRequest و Migration/Seeder واقعی سرور.
- ✅ کاتالوگ Permissionهای رسمی پروژه و تخصیص دسترسی به کاربر.
- ✅ Approval سروری برای ایجاد/تصمیم‌گیری درخواست‌های حساس با Audit.
- ✅ Dashboard Login Gate و Users/Permissions UI از دادهٔ واقعی Server.
- ✅ Permission Enforcement روی endpointهای حساس موجود: Customer، VIP، Buffet/Inventory، Debt/Wallet/Benefits، Finance، Shift، Session و Invoice Reverse.
- ✅ Actor identity دیگر از request.AppUserId برای عملیات حساس پذیرفته نمی‌شود؛ در endpointهای محافظت‌شده شناسهٔ کاربر جاری Server منبع Audit/AppUserId است.
- ✅ CI Smoke یک Operator محدود را عمداً با Permission ناقص وارد می‌کند و 403 واقعی برای عملیات بدون مجوز را اثبات می‌کند.
- ✅ Run #520 (تاریخی): Build/Test .NET، Migration/Server Smoke، Dashboard Lint/Build و Browser Smoke سبز؛ Approval اجرایی Reverse فاکتور از درخواست اپراتور تا تأیید مدیر و اجرای واقعی تست شد.

### موارد تکمیل‌شدهٔ مرحلهٔ ۷
- ✅ Multi-cashier concurrency و conflict handling: `UpdatedAt` به‌عنوان Concurrency Token سروری، به‌روزرسانی خودکار زمان تغییر، تبدیل تعارض EF به HTTP 409 فارسی و تست دو اپراتور روی یک رکورد؛ CI #514 سبز.
- ✅ Approval اجرایی متصل به Reverse واقعی فاکتور: مسیر درخواست با `finance.manage`، تصمیم با `approval.decide`، اجرای Reverse داخل Transaction تصمیم، Audit، و جلوگیری از تأیید توسط ثبت‌کننده؛ CI #520 سبز.
- ✅ Hardening اولیهٔ Permission در UI: ناوبری بر اساس Permission واقعی فیلتر می‌شود، Navigation/Command/Hotkey بدون مجوز به صفحه وارد نمی‌شود، و Users & Shift کنترل‌های user.manage و shift.manage را جداگانه رعایت می‌کند؛ نقش‌های Admin/Owner هم مانند Server دسترسی سراسری دارند؛ CI #527 سبز.
- ✅ Ledger/Payroll واقعی پرسنل و پرداخت حقوق — Server، Ledger، پروفایل حقوق، روش پرداخت/رسید و Approval اجرایی روی head جاری پیاده و با CI #591 در Build/Test، Migration/Server Smoke، Dashboard Lint/Build و Browser Smoke تأیید شد.
- ✅ Hardening Read/Write برای دامنه‌های فعال Server-backed: Customer، Dashboard/Session، Buffet/Inventory، Users/Shift/Payroll و Reports؛ کنترل‌های UI با Permissionهای واقعی هم‌تراز و endpointهای حساس Server-side محافظت شدند.
- ✅ صفحات Mock/آینده مثل Accounts، Games، Tariffs و ClientShell عمداً به Stageهای بعدی منتقل شدند و تا آماده‌شدن dependency واقعی فعال‌سازی نشده‌اند.
- ✅ Approvalهای حساس فعال: Invoice Reverse و Wallet Refund دارای مسیر درخواست/تصمیم/اجرای واقعی Server-side، Transaction و Audit هستند؛ Wallet Refund با CI #591 تا اجرای واقعی و Ledger/Reference آن تست شد.
- ✅ Generic Approval API هم به Actionهای شناخته‌شده و Permission متناظر محدود شد و Action ناشناخته در Smoke با 400 رد می‌شود.


## وضعیت اجرایی مرحلهٔ ۸ — PC Agent Foundation

### Gate نهایی Stage 8 — CI Run #676
**Stage 8 Foundation تکمیل و با Run #676 سبز تأیید شد.**

موارد تکمیل‌شده:
- ✅ Agent Identity / Device Registration در Shared + Server
- ✅ persistent Agent token و restart بدون bootstrap token
- ✅ Heartbeat و Last Seen سروری
- ✅ Health/Telemetry پایه
- ✅ SignalR reconnect و stale connection handling
- ✅ Online/Offline state واقعی در Dashboard
- ✅ Agent Command persistence + SignalR dispatch + acknowledgement + result persistence
- ✅ ping command smoke
- ✅ تست هم‌زمان چند Agent و جلوگیری از DeviceId تکراری
- ✅ Server-authoritative Session timing
- ✅ Pause/Resume سروری
- ✅ Extend/Reduce سروری با TimeAdjustment
- ✅ Dashboard merge بر مبنای Server truth
- ✅ CI Build/Test + Migration/Server Smoke + Dashboard Lint/Build + Browser Smoke سبز

مواردی که عمداً به Stage 9 منتقل شدند:
- ⏩ Lock/Unlock واقعی
- ⏩ Agent-driven Session Start/End
- ⏩ Kiosk/Shell policy و فرمان‌های واقعی Client
- ⏩ Safe Offline/Recovery عملیاتی سطح Client
- ⬜ تست فیزیکی 2–3 PC و سپس rollout گسترده 40+؛ این مورد Gate استقرار فیزیکی است، نه معیار سبزشدن Foundation در CI

قاعده: Stage 8 مرجع هویت، ارتباط، command transport و Server truth را می‌بندد. Stage 9 همین transport را به فرمان‌های واقعی Client/Kiosk وصل می‌کند.

## تکمیل‌های سراسری مرحله ۳ — نیازهای جدید اپراتور

### UX انتخاب و چندانتخاب
- Regression نهایی داشبورد در Run 295 با Playwright سبز شد: انتخاب تکی، Ctrl، Shift، Drag و بررسی خالی‌بودن متن انتخاب‌شده.
- رفتار Selection یکسان باید هر جا «چند ایستگاه/کلاینت» معنی دارد قابل استفاده باشد.
- داشبورد: انتخاب تکی، Ctrl/⌘، Shift، Drag، Ctrl/⌘+Drag.
- کلاینت‌ها: همان قرارداد انتخاب تکی/چندتایی + Drag.
- پس از Drag نباید Click ناخواسته اجرا شود.
- متن کارت‌ها نباید توسط Drag انتخاب شود.
- در صفحات بدون عملیات گروهی، چندانتخاب اجباری اضافه نشود تا UI شلوغ نشود.

### Accounts — مدیریت بر اساس Pool/Platform
- نمایش همهٔ اکانت‌ها در یک Grid شلوغ نیست.
- سطح اول: پلتفرم/Launcher (Steam، Epic، Riot، Battle.net).
- سطح دوم: فقط اکانت‌های همان Pool.
- سطح جزئیات: وضعیت، بازی‌های مجاز، کلاینت تخصیص‌یافته، Guard/2FA، مالک و انقضا.
- در ادامه باید License Pool/Account Lease/Health به Server Domain متصل شود.
- این الگو با سیستم‌های فعلی گیم‌نت هم‌راستاست؛ iCafeCloud حساب‌ها را به Pool بر اساس نوع Launcher/بازی گروه‌بندی و وضعیت آزاد/درحال‌استفاده/قفل را جدا نمایش می‌دهد. citeturn683069search1turn683069search4

### Games — Master/Detail
- لیست بازی‌ها در یک سمت با Search/Filter.
- انتخاب یک بازی → پنل جزئیات همان بازی در سمت دیگر.
- اطلاعات تنظیمی در Detail Panel متمرکز می‌شود؛ Grid کارت‌های بزرگ برای ۵۰+ بازی مرجع اصلی نیست.
- دسته‌بندی، Search، Cover، Launcher، مسیر، exe، پارامتر اجرا، Target و وضعیت باید در Detail Panel مدیریت شوند.
- Game Library نهایی باید قابلیت Search/Category/Scope/Process Detection داشته باشد؛ این الگو با کتابخانه‌های گیم‌نت فعلی نیز هم‌جهت است. citeturn683069search0turn683069search4

### Customer — Refund/کسر اعتبار
- عملیات «کسر اعتبار / بازگشت وجه» باید کنار شارژ، بدهی و اعتبار رایگان در پروفایل مشتری دیده شود.
- Refund نیازمند مبلغ + دلیل است.
- Refund باید به Ledger و Audit وصل شود و حذف/ویرایش مستقیم موجودی جایگزین آن نباشد.
- برای Refundهای حساس، Permission/Approval لازم است.
- الگوی ثبت Refund با مبلغ و توضیح و قابل‌پیگیری در لاگ، در نرم‌افزارهای گیم‌نت موجود نیز استفاده می‌شود. citeturn683069search9turn683069search3

### Settings — مرکز تنظیمات واقعی
تنظیمات باید به گروه‌های روشن تقسیم شوند:
- نمایش و Layout
- هشدار، صدا و Popup
- رفتار جلسه، تسویه و رند
- Backup/Recovery
- شبکه و دو اینترنت
- Client Policy / Wake-on-LAN
- حقوق و شیفت
- امنیت و قفل بخش‌ها
- Hotkey و Keyboard
- ظاهر/تقویم/واحد پول
- سیاست حذف، Refund و Approval

### Users/Shift — حقوق و حسابداری پرسنل
- پروفایل پرسنل باید نوع حقوق (ساعتی/ماهانه)، نرخ ساعتی، حقوق ماهانه، ساعت کاری، اضافه‌کاری، پاداش و کسری را نگه دارد.
- Shift باید Start Cash، Cash Sales، Refund، Expense، Cash End، Difference و توضیح تطبیق دستی را ثبت کند.
- وجه نقد دریافت‌شده خارج از نرم‌افزار باید «تطبیق دستی صندوق + دلیل» داشته باشد؛ نباید بی‌دلیل مستقیماً از حقوق اپراتور کم شود.
- پاداش و کسری حقوق باید عملیات جداگانه، دارای دلیل و در مرحلهٔ بعد Permission/Approval/Audit داشته باشند.
- الگوهای فعلی گیم‌نت‌ها روی Shift مستقل برای هر کارمند، شروع با مبلغ نقدی، گزارش پایان شیفت، اختلاف صندوق و گزارش قابل‌خروجی تأکید دارند. citeturn436697search6turn436697search13turn436697search14

### Security — قفل سطح بخش
- هر بخش حساس می‌تواند رمز جدا داشته باشد.
- نمونه‌ها: مشتریان، اکانت‌ها، تنظیمات، کاربران/شیفت، گزارش‌ها، بازی‌ها، تعرفه‌ها، کلاینت‌ها، بوفه.
- باز شدن هر بخش فقط پس از ورود رمز خودش در همان نشست مدیریتی مجاز است.
- این لایه فعلاً UX است؛ Permission/Server باید منبع حقیقت نهایی باشد.
- Permissionهای واقعی باید بر مشاهده/ویرایش/اجرا/Refund/Reverse/Settings تفکیک شوند؛ سیستم‌های گیم‌نت موجود نیز دسترسی‌های ریزدانه برای Reports، Members، License Pool، Remote Control و موارد مشابه دارند. citeturn436697search1turn436697search2turn436697search9

## اصلاحات اجرایی جدید — داشبورد / Attention Center

### پیگیری شارژ دستی
- هر شارژ دستیِ زمان جلسه باید با **مبلغ، یوزر/مشتری، ایستگاه و زمان ثبت** قابل پیگیری باشد.
- مرکز «نیازمند توجه» باید بتواند شارژ ثبت‌شده را تا پایان جلسه جلوی چشم اپراتور نگه دارد.
- اعتبار پیش‌پرداختی جلسه دارای زمان پایان محاسباتی است؛ نزدیک پایان هشدار بدهد و بعد از اتمام اعتبار، «اقدام لازم» ایجاد کند.
- پس از پایان جلسه، مبلغ باقی‌مانده باید از مبلغ پیش‌پرداختی تفکیک شود؛ در صورت عدم دریافت وجه، وضعیت «پرداخت باقی‌مانده» در پیگیری اپراتور باقی بماند.
- برای عملیات مالی آینده، این پیگیری‌ها باید به Ledger / Invoice / Audit واقعی سرور متصل شوند؛ نسخهٔ فعلی فقط UX و رفتار Mock را تثبیت می‌کند.

### شروع جلسه
- PC/رایانه همیشه **۱ نفر** است و انتخاب ۲/۳/۴ نفر برای آن نمایش داده نمی‌شود.
- PS5/PS4/سایر منابعی که چندنفره هستند می‌توانند انتخاب نفرات داشته باشند.
- نرخ جلسه از تعرفهٔ خودکار می‌آید ولی اپراتور باید بتواند **نرخ ساعتی همان جلسه را دستی وارد یا اصلاح کند**.

### مبلغ‌های دستی
- برای شارژ، Flow مالی و عملیات مشتری، مبلغ اصلی باید دستی قابل ورود باشد.
- Presetهای اجباری ۵۰/۱۰۰/۲۰۰/۵۰۰ هزار تومان مرجع اصلی ورود مبلغ نیستند و در UI اصلی حذف شدند.

### صفحه مشتریان
- ساختار رسمی: **لیست مشتریان + پروفایل جزئیات**.
- در لیست، اطلاعات سریع باید شامل نام، کد، نام کاربری، تماس کوتاه، VIP، کیف پول، بدهی و وضعیت باشد.
- پروفایل باید سه شاخص مالی، اطلاعات هویتی، بسته/VIP، مصرف روزانه و تاریخچه تراکنش را بدون ردیف‌های سفید و نامنظم نشان دهد.
- تاریخچهٔ تراکنش به شکل رویدادهای خوانا نمایش داده می‌شود و در مسیر بعدی به Ledger/Data Table واقعی متصل خواهد شد.

## قرارداد اجرایی آیتم ۳ — Session Center

### هدف
مرکز واحد مشاهده و عملیات یک جلسه فعال، بدون خروج اپراتور از داشبورد.

### مالکیت و ورودی
- مالک: Dashboard
- ورودی: StationDto زنده + CustomerRecord متناظر + زمان جاری
- باز شدن با کلیک روی ایستگاه busy/paused یا «جزئیات کامل جلسه» در منوی راست‌کلیک.

### اطلاعات نمایش‌داده‌شده
- وضعیت جلسه و تایمر زنده
- مشتری/مهمان و کد
- نرخ جلسه
- تعداد نفرات
- هزینه زمان
- بوفه
- جمع فعلی
- اعتبار/شارژ ثبت‌شده
- مبلغ قابل دریافت
- بدهی مشتری

### عملیات
- توقف / ادامه
- شارژ جلسه
- تمدید
- کاهش زمان
- تسویه
- بستن با Esc یا کلیک بیرون

### مرز واقعی‌سازی
نسخه فعلی با Mock/State UI رفتار را تثبیت می‌کند. در اتصال نهایی، هر عملیات باید مطابق Action Map از Server Command + Permission + Persistence + Audit + SignalR عبور کند.

### فایل‌ها
- src/Dashboard/src/features/session/SessionCenter.tsx
- src/Dashboard/src/pages/DashboardPage.tsx
- src/Dashboard/src/App.css
- docs/08-action-map.md

## اصلاح گردش کار شروع PC و پرداخت پایان بازی

- شروع جلسه روی رایانه دیگر لیست بلند مشتریان ندارد؛ اپراتور یوزر/کد/موبایل را تایپ می‌کند و با Enter جست‌وجو می‌شود.
- در مرحلهٔ ورود یوزر، قیمت، تعرفه و مبلغ انتخابی نمایش داده نمی‌شود؛ فقط هویت مشتری، کیف پول، بدهی و وضعیت پروفایل نمایش داده می‌شود.
- اگر کیف پول خالی باشد، هشدار واضح «شارژ کیف پول یا پروفایل» نمایش داده می‌شود؛ تصمیم دریافت وجه می‌تواند به پایان بازی منتقل شود.
- PC همیشه یک نفر است.
- پایان بازی از دریافت وجه جدا شده است: با پایان جلسه، مبلغ قابل دریافت در ستون ثابت سمت چپ داشبورد قرار می‌گیرد.
- برای هر پرداخت در انتظار سه اقدام سریع وجود دارد: «تسویه شد»، «از کیف پول» و «ثبت بدهی».
- «از کیف پول» در صورت کمبود موجودی، موجودی موجود را کسر کرده و مابه‌التفاوت را به بدهی تبدیل می‌کند.
- «ثبت بدهی» فقط بدهی را ثبت می‌کند و دریافت وجه واقعی محسوب نمی‌شود.
- نرخ ساعتی جلسه از مرحله ورود یوزر حذف شده و ویرایش دستی آن داخل Session Center انجام می‌شود؛ نرخ فقط روی همان جلسه اثر دارد.
- مرکز پیگیری به جای یک دکمهٔ کوچک، به یک ستون عمودی ثابت در سمت چپ داشبورد تبدیل شده است: پرداخت‌های در انتظار، نیازمند توجه و آخرین عملیات.
- تشخیص خودکار واقعی پایان بازی هنوز به Agent/Process Detection وابسته است؛ فعلاً همان مسیر «پایان بازی» در Dashboard منبع ایجاد پرداخت در انتظار است.

## قرارداد اجرایی آیتم ۸ — Undo / Reverse UX

- Undo در محصول به معنی حذف رکورد نیست؛ همیشه یک عملیات معکوس با رکورد جدید ثبت می‌شود.
- عملیات برگشت‌پذیر فعلی: شارژ جلسه، تمدید، کاهش زمان و فروش بوفه.
- عملیات دارای Side Effect مالی/Session حساس مثل تسویه، پاک نمی‌شوند و تا اتصال Reverse واقعی سرور وارد Undo خودکار نمی‌شوند.
- تاریخچهٔ اصلی باقی می‌ماند و یک رویداد «برگشت عملیات» به Timeline اضافه می‌شود.
- در معماری نهایی، Reverse باید Server Command + Permission + Audit + Transaction باشد.
- فایل: src/Dashboard/src/components/ReverseDialog.tsx

## قرارداد اجرایی آیتم ۷ — Approval Flow

- عملیات حساس باید بتواند قبل از اجرا وارد وضعیت «نیازمند تأیید» شود.
- کاربر اولیه در این نسخه بر اساس نقش Demo (`operator` / `manager` / `owner`) تشخیص داده می‌شود.
- نمونهٔ فعلی: تخفیف بیشتر از ۱۰٪ توسط اپراتور بدون تأیید ادامه پیدا نمی‌کند.
- پنجرهٔ تأیید سه مفهوم روشن دارد: دلیل نیاز به تأیید، عملیات موردنظر، تأیید و ادامه یا انصراف.
- نسخهٔ فعلی فقط UX/State را تثبیت می‌کند؛ تأیید نهایی باید بعداً توسط Server Permission + Approval Record + Audit کنترل شود و UI به هیچ وجه منبع حقیقت مجوز نیست.
- فایل اصلی: src/Dashboard/src/components/ApprovalDialog.tsx

## Vertical Slice آیتم B9 — Wallet Ledger

- Server از WalletTransaction موجود به‌عنوان دفتر تراکنش استفاده می‌کند؛ جدول موازی ساخته نشد.
- GET دفتر کیف پول: `/api/customers/{customerId}/wallet-ledger`
- POST تراکنش کیف پول: `/api/customers/{customerId}/wallet-transactions`
- Credit/Debit، کنترل موجودی، تراکنش SQLite و Audit در یک مسیر پایدار ثبت می‌شوند.
- Dashboard adapter در `src/Dashboard/src/services/walletLedgerService.ts` برای API واقعی و Mock fallback دارد.
- Customer Profile اکنون دفتر کیف پول را با مبلغ، جهت، توضیح، زمان و ماندهٔ بعد از تراکنش نشان می‌دهد.
- تغییرات کیف پول Dashboard شامل Flow مستقیم، کسر، تسویه از کیف پول، پرداخت‌های معوق و شارژ کیف پول به همین Ledger adapter منتقل شد. گام بعدی B9: اتصال Permission/Server Command کامل و همگام‌سازی همهٔ Invoice/Refundها با Ledger.

## تکمیل B10 — Reference در Refund

- Refund کیف پول اکنون می‌تواند `SourceTransactionId` داشته باشد.
- Server مبدأ را برای همان مشتری و از نوع Credit بررسی می‌کند.
- مبلغ Refund از مجموع Refundهای قبلی همان مبدأ بیشتر نمی‌تواند باشد.
- رکورد اصلی حذف نمی‌شود و رکورد Debit جدید با `ReferenceTransactionId` ثبت می‌شود.
- Migration: `20261002090000_WalletRefundReference`.
- Customer Profile باید قبل از ثبت Refund یک تراکنش Credit را به‌عنوان مبدأ انتخاب کند.

## قرارداد اجرایی آیتم ۶ — Error UX استاندارد

- هر خطای کاربر باید سه بخش داشته باشد: چه شد، معنی/علت قابل‌فهم، اقدام بعدی.
- پیام خام Exception، HTTP status، stack trace یا متن فنی API نباید مستقیماً به کاربر نمایش داده شود.
- حالت‌های پایه: شبکه، دسترسی، اعتبارسنجی، تعارض، پیدا نشدن، خطای عمومی.
- هر جا امکان ادامهٔ امن وجود دارد باید یک دکمهٔ اقدام بعدی مثل «تلاش مجدد» یا «بررسی اطلاعات» ارائه شود.
- کامپوننت پایه: src/Dashboard/src/components/UserErrorBanner.tsx
- اتصال فعلی: خطای دریافت Dashboard API در src/Dashboard/src/App.tsx.
- در مراحل بعدی باید تمام عملیات Server Command/Agent از همین قرارداد استفاده کنند.

## قرارداد اجرایی آیتم ۵ — عملیات اخیر اپراتور

- مالکیت: Dashboard
- هدف: نمایش آخرین عملیات واقعی ثبت‌شده در نشست فعلی اپراتور، بدون رفتن به گزارش‌ها.
- منبع فعلی: SessionTimelineEventهای ثبت‌شده توسط عملیات جلسه.
- نمایش: حداکثر ۹ رویداد اخیر با عنوان، ایستگاه، توضیح و ساعت.
- رویدادهای فعلی: شروع، توقف، ادامه، شارژ، تمدید، کاهش زمان، بوفه و تسویه.
- در معماری نهایی، رویدادهای Audit/OperatorAction سرور باید منبع حقیقت باشند و با نقش/Permission فیلتر شوند.

## قرارداد اجرایی آیتم ۴ — Timeline کوتاه ایستگاه

- رویدادهای عملیاتی جلسه با ترتیب معکوس زمانی نمایش داده می‌شوند.
- رویدادهای فعلی: شروع، توقف، ادامه، شارژ، تمدید، کاهش زمان، بوفه و تسویه.
- هر رویداد دارای زمان، عنوان، توضیح و در صورت نیاز مبلغ است.
- Timeline کوتاه است و فقط آخرین رویدادهای مفید همان جلسه را نشان می‌دهد؛ تاریخچهٔ کامل Audit جایگزین آن نیست.
- منبع فعلی: state محلی UI/Mock؛ در اتصال نهایی باید از Event/Audit واقعی سرور تغذیه شود.
- مالکیت نمایش: Session Center / Dashboard.

## قرارداد اجرایی آیتم ۲ — جست‌وجوی سراسری و مرکز فرمان

### هدف
یک نقطه ورود واحد برای پیدا کردن رکوردهای عملیاتی و اجرای فرمان‌های پرتکرار، بدون جابه‌جایی بی‌دلیل بین صفحه‌ها.

### ورودی‌ها
- متن جست‌وجو: نام، کد، شماره، موبایل، شناسه، بخشی از عنوان یا اطلاعات رکورد.
- کلیدهای کیبورد: Ctrl+K، ↑، ↓، Enter و Esc.
- فرمان انتخاب‌شده از فهرست فرمان‌های موجود.

### منابع داده فعلی
- ایستگاه‌ها از DashboardSnapshotDto.stations
- مشتری، کالا، تعرفه، بازی، اکانت، کلاینت، کاربر و فاکتور از mockService
- فرمان‌ها از قرارداد داخل GlobalCommandCenter.tsx

### خروجی
- نتیجهٔ رکورد به تفکیک نوع: ایستگاه، مشتری، بوفه، تعرفه، بازی، اکانت، کلاینت، کاربر و فاکتور.
- فرمان‌های قابل اجرا با توضیح کوتاه.
- انتخاب نتیجه → هدایت به صفحه مالک همان قابلیت و ارسال gamenet-search-selection.
- انتخاب فرمان → هدایت به صفحه مالک و ارسال gamenet-command.

### کنترل‌های UI
- دکمهٔ هدر «جست‌وجو · Ctrl+K»
- ورودی واحد
- نتایج گروه‌بندی‌شده
- Highlight نتیجه فعال
- اجرای با Enter
- حرکت با ↑/↓
- بستن با Esc یا کلیک بیرون

### وابستگی و مرز نهایی
نسخهٔ فعلی مرکز، تجربهٔ جست‌وجو/فرمان را روی داده‌های موجود UI کامل می‌کند. اجرای نهایی هر فرمان همچنان باید در مسیر Server Command → Permission → Domain Rule → Persistence → Audit → SignalR قرار گیرد؛ این بخش عمداً تا تکمیل دامنهٔ سرور جعل نمی‌شود.

### فایل‌های مربوط
- src/Dashboard/src/features/search/GlobalCommandCenter.tsx
- src/Dashboard/src/App.tsx
- src/Dashboard/src/App.css
- docs/08-action-map.md
- docs/10-product-completion-backlog.md

## اصلاح UX انتخاب ایستگاه‌ها — مرحله ۳

- متن داخل کارت‌های ایستگاه قابل انتخاب/Drag نیست.
- کلیک معمولی رفتار عملیاتی قبلی کارت را حفظ می‌کند.
- Ctrl/⌘ برای انتخاب یا لغو انتخاب تکی کار می‌کند.
- Shift برای انتخاب بازه بین کارت مبنا و کارت مقصد کار می‌کند.
- Drag روی فضای کارت، کادر انتخاب می‌سازد و چند ایستگاه را هم‌زمان انتخاب می‌کند.
- Ctrl/⌘ + Drag به انتخاب فعلی اضافه می‌کند.
- Esc انتخاب‌ها را پاک می‌کند و Ctrl/⌘+A تمام ایستگاه‌های قابل‌مشاهده را انتخاب می‌کند.
- انتخاب فعال با نشان تیک و شمارندهٔ «ایستگاه انتخاب شده» در داشبورد قابل مشاهده است.
- CI روی HEAD این اصلاح سبز شد.

## فهرست اصلی

> مرحله اصلی فعلی: **۷ — Users & Permissions**؛ هستهٔ مراحل ۴، ۵ و ۶ تکمیل و تست شده‌اند و باقی‌مانده‌های امنیت/Permission/Approval/Payroll در همین مرحله بسته می‌شوند.

### A) Operational Intelligence / UX
1. ✅ مرکز «نیازمند توجه» در داشبورد — پیاده‌سازی و CI سبز شد
2. ✅ جست‌وجوی سراسری و مرکز جست‌وجو/فرمان — Build/Test/CI + Smoke Test تعاملی سبز
3. ✅ پنل جزئیات جلسه (Session Center) — Build/Test/CI + Smoke Test تعاملی سبز
4. ✅ Timeline کوتاه ایستگاه — Build/Test/CI + Smoke Test تعاملی سبز
5. ✅ عملیات اخیر اپراتور — Build/Test/CI + Smoke Test تعاملی سبز
6. ✅ Error UX استاندارد: خطا → معنی → اقدام بعدی — Build/Test/CI + Smoke تعاملی سبز
7. ✅ Approval Flow برای عملیات حساس — Build/Test/CI + Smoke Test تعاملی سبز
8. ✅ Undo UX برای عملیات برگشت‌پذیر (Reverse واقعی، بدون حذف رکورد) — Build/Test/CI + Smoke Test تعاملی سبز

## اجرای مرحله اصلی ۴ — B11 Settlement

### وضعیت فعلی
- Settlement UI از همان Billing Engine استفاده می‌کند و محاسبه جداگانه/قدیمی مودال حذف شد.
- Breakdown شامل زمان، دقیقه قابل صورتحساب، نرخ، بوفه، اعتبار پیش‌پرداخت، تخفیف و رند است.
- بخش «چرا این مبلغ؟» مسیر محاسبه را برای اپراتور توضیح می‌دهد.
- برای پرداخت نقدی مبلغ دریافتی قابل ورود است و مبلغ برگشتی محاسبه می‌شود.
- Free Time در Breakdown لحاظ می‌شود.
- Split Payment هنوز جداست و در B12 تکمیل می‌شود.
- Invoice/Settlement واقعی Server مالک نهایی ثبت و Transaction/Audit آن روی Server است؛ مسیر Dashboard به endpoint واقعی Settlement متصل است.

## اجرای مرحله اصلی ۴ — B10 Refund

### مسیر فعلی
- endpoint مستقل: POST /api/customers/{customerId}/wallet-refunds
- دلیل Refund اجباری است.
- موجودی قبل از ثبت Refund کنترل می‌شود.
- تراکنش اصلی حذف نمی‌شود؛ یک Ledger debit جدید برای بازگشت وجه ثبت می‌شود.
- Audit با Action = WalletRefund ثبت می‌شود.
- Customer Profile از سرویس Refund اختصاصی استفاده می‌کند.
- تست Server برای حفظ رکورد اصلی + ثبت Debit + Audit اضافه شد.

### مرز باقی‌مانده
- ✅ Permission سروری و Audit برای Wallet Refund.
- ✅ Approval اجرایی برای Refund حساس با اجرای واقعی در Transaction.
- ✅ Reference صریح به تراکنش مبدأ در Ledger Refund.
- ✅ Reverse واقعی Session/Buffet از مسیر Invoice Reverse؛ Refundهای مستقل Package هنوز دامنهٔ جداگانه محسوب می‌شوند.
- ⬜ تکمیل سناریوهای مستقل Package Refund، در صورت ورود به دامنهٔ محصول.

## وضعیت تاریخی مرحله اصلی ۴ — Finance & Session Core

**هستهٔ مرحله ۴ تکمیل شد.** B9 تا B17 مسیرهای اصلی مالی/جلسه را روی Server/SQLite/Audit و CI/Smoke سبز تثبیت کرده‌اند. موارد مشترک Permission/Approval نهایی عمداً به مرحلهٔ ۷ منتقل شده‌اند و موارد Customer Domain که به مدل مشتری مربوط‌اند، در مرحلهٔ ۵ ادامه پیدا می‌کنند.

## شروع مرحله اصلی ۵ — Customer & VIP Domain
### پیشروی ثبت‌شدهٔ مرحله ۵ — وضعیت تأییدشده در CI

- **Customer CRUD سروری**: Create/Update + Alias/NationalId/VipTier + Unique validation + Audit + SQLite migration + Dashboard adapter/UI + CI سبز ✅
- **VIP Package**: Catalog + ساخت پکیج + تخصیص پکیج + Activation/Expiry + Daily/Total Minutes + Discount + Audit/API/UI ✅
- **Debt**: ایجاد بدهی به‌صورت Draft Invoice، نمایش از Server، تسویه با Cash/Card/Wallet، InvoicePayment و Audit؛ مسیر در CI Smoke سبز ✅
- **Password/Credential**: Password با PBKDF2 ذخیره می‌شود؛ تغییر رمز و Customer Authentication واقعی و در CI Smoke تست‌شده ✅
- **Concurrent Login Limit**: acquire/release سروری، محدودیت واقعی و CI Smoke برای سقف ورود هم‌زمان ✅
- **VIP Usage**: مصرف Daily/Total از Sessionهای واقعی Server محاسبه می‌شود؛ بازهٔ مصرف به Activation/Expiry محدود است و CI Smoke کاهش مصرف را اثبات می‌کند ✅
- **VipTier / VipPackage consistency**: تخصیص پکیج Tier را همگام می‌کند و ویرایش ناسازگار با پکیج فعال را رد می‌کند ✅
- **Customer History**: Wallet/Benefit/Invoice/Session از Server جمع می‌شوند و CI Smoke ثبت Session جدید را بررسی می‌کند ✅
- **نتیجه Stage 5 Server-side**: هستهٔ دامنه Customer/VIP از نظر CRUD، مالی، Credential و History تکمیل و با CI/Smoke تثبیت شده است ✅
- **Carry-over عمدی**: اتصال Login/Session مشتری به Agent واقعی تا مرحلهٔ ۸ (PC Agent Foundation) نگه داشته می‌شود؛ قبل از ساخت Agent، Client واقعی برای اثبات End-to-End وجود ندارد. این مورد به‌عنوان نقص فراموش‌شده محسوب نمی‌شود.
### محدوده
- Customer منبع حقیقت سرور
- CRUD مشتری
- پروفایل مالی/هویتی
- Wallet / Debt / Free Time / Free Money
- VIP Package و مصرف روزانه
- Concurrent Login Limit
- History / Ledger
- Password/credential UX
- آماده‌سازی Client Login برای Agent

## اجرای مرحله اصلی ۶ — Buffet & Inventory Domain

### برش تأییدشده Stage 6 — CI #467
- **Catalog سروری**: دریافت و ایجاد کالا با نام/دسته/قیمت فروش/قیمت خرید/موجودی اولیه ✅
- **ویرایش کالا**: Endpoint سروری و اتصال Dashboard به ویرایش کالا ✅
- **Inventory Adjustment**: ورود/خروج موجودی سروری با تراکنش Inventory و Audit ✅
- **Atomic Sale**: فروش بوفه موجودی را داخل Transaction کم می‌کند و فروش بیشتر از موجودی را بدون تغییر موجودی رد می‌کند ✅
- **Inventory History**: گردش موجودی از Server قابل دریافت و در Dashboard نمایش داده می‌شود ✅
- **CI Smoke**: ایجاد کالا → اصلاح موجودی → فروش → رد فروش نامعتبر → بررسی ثابت‌ماندن موجودی → History → ویرایش کالا، در Run #467 سبز شد ✅
- **تأییدشده در Run #482**: حداقل موجودی قابل‌تنظیم (`MinimumStock`)، واحد شمارش (`Unit`)، نوع تراکنش (`InventoryTransaction.Kind`) و مسیرهای واقعی Waste/Return در Server + Dashboard + Migration ✅
- **تأییدشده در Run #482**: فروش بوفه به Draft Invoice همان Session، انتخاب Session مقصد، نهایی‌سازی همان Invoice در Settlement، و Reverse/Cancel با برگشت موجودی و اعتبار زمانی ✅
- **تکمیل Purchase**: ثبت خرید با بهای واحد، ثبت تاریخی UnitCost/UnitPrice در InventoryTransaction و محاسبه بهای میانگین موجودی پیاده‌سازی شد ✅
- **تکمیل Profit**: گزارش سود بوفه بر مبنای گردش تاریخی Sale/Purchase/Waste/Return و اتصال آن به Reports Center پیاده‌سازی شد؛ Reverse نیز بهای تاریخی فروش را حفظ می‌کند ✅
- **تأیید نهایی در Run #488**: Build/Test .NET، Migration/Server Smoke، Dashboard Lint/Build، Preview و Dashboard Interaction Smoke همگی سبز شدند ✅
- **Stage 6**: اکنون تکمیل و تست‌شده است ✅

## وضعیت مرحله اصلی ۳ — Operational Completion

**یادداشت تاریخی:** در زمان ثبت این بخش، A1 تا A8 تکمیل شده بودند و مرحله بعدی ۴ بود. این متن صرفاً سابقه است و جایگاه فعلی پروژه را تعیین نمی‌کند.

### B) Finance / Session
9. ✅ Wallet Ledger واقعی — API/SQLite/Audit/adapter و مسیرهای مالی Dashboard سروری شدند؛ ادامهٔ مدل مشتری و Permissionهای نهایی به مراحل ۵ و ۷ منتقل شد
10. ✅ Refund / Reverse واقعی — Refund کیف پول + Reference مبدأ + Approval UX + Reverse اتمیک Invoice/Session در سرور + Persistence Test + CI/Smoke سبز
11. ✅ Settlement کامل + Breakdown + Why this amount? — Billing Engine/Breakdown/Why/Received/Change + Server Atomic Settlement + Invoice/Wallet/Audit + اتصال Dashboard به Session/Invoice سرور + تست اتمیک/Smoke سبز
12. ✅ Split Payment — UI + ثبت اتمیک سروری + Invoice + Audit + Smoke سبز؛ hardening Permission در مرحله ۷
13. ✅ Shift Settlement + Shift Handover — Shift Open/Close سروری + فروش نقدی از InvoicePayment + هزینه + تطبیق نقدی + Audit + Build/Test/Smoke سبز؛ hardening Users/Permission در مرحله ۷
14. ✅ Session Transfer / Change Tariff / Change Persons — Session Center + Server transaction/Audit برای تغییر نرخ/نفر/انتقال + Build/Test/Smoke سبز؛ hardening Permission در مرحله ۷
15. ✅ Free Time + Free Money کامل — دامنه و Ledger مستقل سروری، اعطا/کسر، مصرف در Settlement و Build/Test/Smoke سبز؛ ادامهٔ Customer Domain در مرحله ۵
16. ✅ Concurrent Login Limit — Guard و endpointهای acquire/release سروری + migration + client enforcement + CI/Smoke سبز
17. ✅ Expense / Profit واقعی و قابل ممیزی — Expense/Shift/Operating Profit Server + SQLite + Audit + گزارش تراکنش فاکتور سروری + CI/Smoke سبز

### C) Client / Agent
18. Client Health / Heartbeat / Telemetry
19. Group Command Tracking
20. Client Update Center + Safe Update + Rollback
21. Recovery Center
22. واقعی‌سازی Client Commands
23. Safe Kiosk/Shell
24. Client Favorites / Recent / Search / Category
25. Client Now Playing / Fullscreen hiding
26. Client Policy برای نرم‌افزارهای مجاز

### D) Inventory / Buffet / Account
27. Buffet Order Lifecycle واقعی
28. Inventory Adjustment / Waste / Return / Minimum Stock
29. Account Pool Health
30. Account Lease History
31. Game Library واقعی + Cover + Process Detection
32. Trailer فقط در صورت مصرف واقعی

### E) Reporting / Management
33. Heatmap استفاده از ایستگاه/ساعت
34. گزارش درآمد/کارکرد هر ایستگاه
35. گزارش سود/حاشیه سود
36. Audit Explorer
37. Reservation + Waitlist زنده داخل Dashboard
38. Event Mode / Tournament-ready architecture
39. Internal Notes
40. Station/Client Health Dashboard
41. Dashboard Network State تفکیک‌شده: سرور داخلی / LAN / اینترنت ۱ / اینترنت ۲
42. Multi-cashier concurrency / conflict handling

### F) UI System
43. تثبیت چهار Primitive بصری: Summary Card / Entity Card / Info Panel / Data Table/List
44. بازطراحی هدفمند Profile/Session/Tariff/Shift با Info Panel
45. تبدیل Account/Audit/Finance History به Data Table در جاهایی که مقایسه مهم است
46. Responsive + dense operator mode
47. Keyboard-first operator UX
48. Persian terminology catalogue

## ترتیب اجرایی
A1 → A2 → A3 → A4 → A5 → A6 → A7 → A8
→ B9 → B10 → B11 → B12 → B13 → B14 → B15 → B16 → B17
→ C18 → C19 → C20 → C21 → C22 → C23 → C24 → C25 → C26
→ D27 → D28 → D29 → D30 → D31 → D32
→ E33 → E34 → E35 → E36 → E37 → E38 → E39 → E40 → E41 → E42
→ F43 → F44 → F45 → F46 → F47 → F48

## قرارداد اجرایی B15 — Free Time + Free Money

- Free Time (دقیقه رایگان) از Free Money (اعتبار رایگان تومانی) جدا نگه داشته می‌شود.
- Free Time فقط روی زمان قابل صورتحساب اثر می‌گذارد.
- Free Money در تسویه قابل مصرف است و از موجودی اعتبار رایگان مشتری کم می‌شود.
- مصرف Free Money در تاریخچه مشتری و گزارش مالی با روش پرداخت مستقل «اعتبار رایگان» ثبت می‌شود.
- اتصال نهایی سرور باید مصرف Free Time/Free Money را Transaction/Audit کند.

## قرارداد اجرایی B14 — Session Transfer / Change Tariff / Change Persons

- نرخ جلسه داخل Session Center قابل تغییر است و فقط روی همان جلسه اثر دارد.
- نفرات جلسه داخل Session Center قابل تغییر است؛ PC همیشه ۱ نفر باقی می‌ماند.
- جلسه فعال/متوقف را می‌توان به ایستگاه آزاد منتقل کرد و وضعیت جلسه، مشتری، زمان، شارژ و بوفه همراه آن منتقل می‌شوند.
- ایستگاه مبدأ آزاد می‌شود و رویداد انتقال در Timeline ثبت می‌شود.
- نسخه نهایی سروری باید انتقال را به‌صورت Transaction و با Permission/Audit ثبت کند.

## قرارداد اجرایی B13 — Shift Settlement + Handover

- بستن شیفت دیگر با prompt انجام نمی‌شود.
- اپراتور در فرم تسویه، وجه مورد انتظار، وجه شمارش‌شده، تطبیق نقدی خارج از سیستم و اختلاف را می‌بیند.
- یادداشت تحویل شیفت برای مشکلات دستگاه، بدهی، سفارش باز یا هر موضوع منتقل‌شونده ثبت می‌شود.
- وجه خارج از سیستم جدا از فروش نرم‌افزاری ثبت می‌شود و نباید به‌صورت پنهان از حقوق کم شود.
- در نسخه سروری نهایی: Shift Close + Handover باید Transaction/Audit/Permission داشته باشد.

## قرارداد اجرایی B11 — Server Atomic Settlement

- سرویس `SessionSettlementService` تسویه را در یک transaction ثبت می‌کند.
- مجموع سهم‌های پرداخت باید دقیقاً با مبلغ نهایی برابر باشد.
- سهم کیف پول ابتدا موجودی را بررسی و سپس WalletTransaction از نوع Debit ثبت می‌کند.
- Invoice با Status=Paid و InvoiceItem برای جلسه ثبت می‌شود.
- Session به Completed می‌رود و EndAt/TotalAmount ثبت می‌شود.
- AuditLog با روش‌ها و مبلغ‌های پرداخت ثبت می‌شود.
- دو تست سرور اضافه شده: تسویه ترکیبی واقعی و رد split نامعتبر بدون تغییر Session/Wallet.
- endpoint: POST /api/sessions/{sessionId}/settle
- اتصال Dashboard هنوز باید Session واقعی سرور را به این endpoint متصل کند.

## قرارداد اجرایی B12 — Split Payment

- پرداخت نهایی یک جلسه می‌تواند بین نقدی، کارتخوان و کیف پول تقسیم شود.
- مبلغ هر روش دستی وارد می‌شود و جمع سه روش باید دقیقاً برابر مبلغ قابل دریافت باشد.
- سهم کیف پول از Ledger واقعی کم می‌شود و کمبود موجودی مانع ثبت می‌شود.
- برای هر سهم نقد/کارت گزارش مالی جدا ثبت می‌شود، ولی Invoice جلسه یک تسویه ترکیبی واحد باقی می‌ماند.
- در Timeline جلسه، روش‌ها و مبلغ هر سهم قابل پیگیری هستند.
- در نسخه نهایی Server باید تراکنش Split را به‌صورت atomic ثبت کند و Audit/Permission نیز داشته باشد.

## وضعیت
مرحله اصلی ۳ و هستهٔ اصلی مرحلهٔ ۴ بسته شده‌اند. Stage 5 و Stage 6 و Stage 7 بسته و Merge شده‌اند. **Stage 8 Foundation نیز با Run #676 سبز و تأیید شده است و اکنون مسیر اجرایی پروژه وارد Stage 9 — Real Client Commands & Kiosk می‌شود.** CI فعلی شامل Build/Test، مهاجرت/Startup سرور، smoke واقعی Customer/VIP API و smoke تعاملی Dashboard است.


## قرارداد سراسری Update / Release — از همین مرحله لازم‌الاجرا
- Update قابلیت جانبی آخر پروژه نیست؛ هر Feature باید با فرض «قابل انتشار و قابل برگشت» طراحی شود.
- هر Release چهار شناسهٔ مستقل دارد: ProductVersion، SchemaVersion، ApiContractVersion و MinimumClientVersion؛ RecommendedClientVersion هم برای پیشنهاد ارتقا نگه داشته می‌شود.
- تغییر API تا وقتی additive و سازگار است نسخهٔ جدید نمی‌خواهد؛ /api/v2 فقط برای Breaking Change واقعی استفاده می‌شود.
- قبل از Migration دیتابیس واقعی، Backup قابل‌بازیابی باید وجود داشته باشد؛ Migration خطرناک نباید با حذف یا بازسازی خودکار دیتابیس پوشانده شود.
- Server فعلاً قرارداد Manifest را از /api/release/manifest ارائه می‌کند؛ Updater/Delta/Rollback عملیاتی برای مراحل ۱۰ و ۱۴ است.
- چرخهٔ انتشار آینده: Build → Test → Smoke/E2E → Release Manifest → Canary → Health Check → Rollback/Publish.
- Clientهای قدیمی نباید بی‌دلیل با Release ناسازگار از کار بیفتند؛ Minimum/Recommended Client Version برای کنترل سازگاری است.
- کانال‌های stable و canary در معماری حفظ می‌شوند و Canary عملیاتی بعد از Agent واقعی فعال می‌شود.

## Audit Checkpoint — Stages 1–9 — 2026-10-02

این بخش وضعیت تأییدشدهٔ نرم‌افزاری Stageهای 1 تا 9 را ثبت می‌کند و باید از CI/کد واقعی خوانده شود.

## وضعیت Stageها
- ✅ Stage 1 — Foundation
- ✅ Stage 2 — Prototype & Behavior Transfer
- ✅ Stage 3 — Operational Completion
- ✅ Stage 4 — Finance & Session Core
- ✅ Stage 5 — Customer & VIP Domain
- ✅ Stage 6 — Buffet & Inventory Domain
- ✅ Stage 7 — Users & Permissions — merge شده در `main`
- ✅ Stage 8 — PC Agent Foundation — Run #676
- ✅ Stage 9 — دو برش اصلی نرم‌افزاری:
  - Lock/Unlock واقعی — Run #690
  - Agent-driven Session Start/End — Run #744

## Stage 9 — مواردی که واقعاً تأیید شدند
- ✅ Agent Command transport: persistence/correlation/ack/result
- ✅ `client.control` و Audit برای فرمان‌های Agent
- ✅ Lock/Unlock واقعی Client با وضعیت authoritative روی Server
- ✅ heartbeat reconciliation و timeout/failure handling
- ✅ Kiosk policy پایه و LockOnDisconnect
- ✅ Customer Authentication مرتبط با DeviceId
- ✅ Agent-driven Session Start با Station و Tariff منبع‌حقیقت Server
- ✅ Agent-driven Session End با CustomerLogin مالک همان Agent
- ✅ پایان Session با `EndAt` و سقف زمان صورتحساب بر اساس زمان Server
- ✅ آزادسازی CustomerLogin در پایان جلسه
- ✅ settlement همان Session از مسیر موجود Server
- ✅ broadcast زنده تغییر Session به Dashboard
- ✅ جلوگیری از تخصیص یک Station فعال به بیش از یک Agent فعال
- ✅ regressionهای جدید برای قیمت‌دستکاری‌شده، login ownership و duplicate station assignment
- ✅ stale Agent با LockOnDisconnect نیز روی Server قفل می‌شود
- ✅ Dashboard build/lint و Browser Smoke — Run #744

## اصلاحات مهم آخرین ممیزی Stage 9
در آخرین ممیزی مشخص شد چند نقطه از قرارداد Server-authoritative نیاز به hardening داشت:
1. Agent دیگر نمی‌تواند `TariffId` یا `HourlyRateOverride` دلخواه خود را به Session تحمیل کند؛ تعرفهٔ فعال Station منبع حقیقت است.
2. پایان Session بدون CustomerLogin معتبر همان DeviceId رد می‌شود.
3. هر Station فعال فقط می‌تواند به یک Agent فعال متصل باشد.
4. stale heartbeat با Policy `LockOnDisconnect` قفل authoritative ایجاد می‌کند.
5. timeout فرمان‌های Agent دوباره‌پردازش/دوباره Audit نمی‌شوند.
6. تست CI برای این مرزها اضافه شد.
7. یک خطای کامپایل Dashboard در `DashboardPage` نیز اصلاح و Run #744 سبز شد.

## مرزهای عمداً باقی‌ماندهٔ Stage 9
این موارد هنوز «حل‌نشدهٔ پنهان» نیستند و عمداً در برش‌های بعدی باقی مانده‌اند:
- ⏩ Full Kiosk/Shell policy و command catalog گسترده
- ⏩ Full customer-facing Client login UX و اتصال UX به Agent Session Start/End
- ⏩ Safe Offline/Recovery عملیاتی کامل
- ⬜ Validation فیزیکی روی 2–3 PC واقعی
- ⬜ rollout کنترل‌شده برای 40+ PC

## Debtهای Cross-Stage
- `mockService.getTariffs()` هنوز در بعضی مسیرهای Dashboard وجود دارد و باید در Stage واقعی Tariff/Domain جایگزین شود.
- Accounts/Games/Tariffs/ClientShell نباید قبل از dependency واقعی به‌صورت Mock-فعال وارد محصول شوند.
- Installer/Updater/Rollback عملیاتی هنوز طبق معماری به Stageهای بعد منتقل شده‌اند.
- Installer همچنان local-only است.

## Gate
Stage 9 از نظر برش‌های نرم‌افزاری تأییدشده بود و سپس Stage 10 اجرا و تکمیل شد. Full Kiosk/Shell، UX کامل مشتری و validation فیزیکی 2–3 PC عمداً از scope Stage 10 خارج مانده‌اند. Server همچنان منبع حقیقت است و هر Command باید Permission → Persistence → SignalR → Ack/Result → Audit را حفظ کند.


## Stage 14.1 — Settings Information Architecture / UI Hardening — 2026-10-05

### وضعیت
- 🟡 **در حال تثبیت** — شاخه `stage14-settings-ia`.
- Settings از یک grid شلوغ به IA دسته‌بندی‌شده با Search و navigation تبدیل شده است.
- دسته‌های رسمی این برش: داشبورد و نمایش، جلسه و تسویه، هشدارها، شبکه، Backup/Recovery، امنیت و دسترسی، کاربران و شیفت، بازی و کلاینت، ظاهر/محلی‌سازی و میانبرها.
- رفتار تنظیمات عمداً حفظ شده؛ این برش Server-backed Settings را جعل نمی‌کند و وضعیت local-only را شفاف نمایش می‌دهد.
- Browser Smoke برای جست‌وجوی تنظیمات و navigation دسته‌ها اضافه شده است.

### Gate این برش
- ⬜ Build
- ⬜ .NET Tests
- ⬜ EF validation
- ⬜ Server/Migration/Health Smoke
- ⬜ Dashboard Lint/Build
- ⬜ Dashboard Browser Smoke

### مرز بعدی
- ⏭️ Stage 14.2 — Dashboard PC grouping / Internet 1-2 UX + dense responsive hardening
- ⏭️ Stage 14.3 — remaining UI/accessibility/operational polish

> Stage 14.1 تا زمانی که exact-head همه Gateهای بالا را سبز نکند Done محسوب نمی‌شود.

## Product Improvement Queue — ثبت در ممیزی Stage 1–9

این موارد از نظر محصول مهم‌اند اما نباید با patchهای پراکنده و بدون جایگاه معماری وارد کد شوند. هر مورد در مرحلهٔ مناسب و پس از تعریف/تست اجرا می‌شود.

### 1) Settings Information Architecture — Stage 14 / UI Hardening
- صفحهٔ تنظیمات از حالت پراکنده و شلوغ خارج شود.
- تنظیمات بر اساس حوزه‌های روشن دسته‌بندی شوند: داشبورد و نمایش، جلسه/تسویه، هشدارها، شبکه، Backup/Recovery، امنیت و دسترسی، کاربران/شیفت، Client Policy، ظاهر و Hotkey.
- جست‌وجوی تنظیمات و مسیر دسترسی سریع در نظر گرفته شود تا مالک برای پیدا کردن یک گزینه مجبور به جست‌وجوی بین کارت‌های نامرتبط نباشد.
- تنظیمات خطرناک/حساس باید توضیح اثر و سطح دسترسی مشخص داشته باشند.
- تا زمان واقعی‌شدن Server-backed Settings، تنظیمات Mock/local نباید به‌عنوان تنظیمات عملیاتی نهایی تلقی شوند.

### 2) Fine-Grained Permission + Scope — طراحی از الان، پیاده‌سازی کنار Reporting/Finance Access
- Permission صرفاً «دسترسی به صفحه» نباشد؛ باید Action و Scope نیز قابل تعریف باشد.
- نمونه Scope: نوع گزارش، بازهٔ زمانی مجاز، سقف مبلغ، Read/Export، و دسترسی به جزئیات مالی.
- مثال: اپراتور می‌تواند گزارش درآمد را ببیند اما فقط برای ۱ یا ۲ روز اخیر، بدون دسترسی به کل ماه یا Export.
- این Scope باید Server-side enforce شود و UI فقط بازتاب آن باشد.
- طراحی باید با PermissionCatalog فعلی سازگار و audit-friendly باشد، نه یک سیستم موازی Permission.

### 3) Operator Account Management — اصلاح مستقیم قبل از عبور از این Debt
- API ساخت/ویرایش کاربر وجود داشت اما مسیر Dashboard برای تعریف اپراتور در UsersPage قابل دسترسی نبود.
- یک entry point واضح «اپراتور جدید» به صفحهٔ کاربران اضافه می‌شود.
- پس از ساخت حساب، انتخاب Permissionها از همان صفحه انجام می‌شود.
- Create/Edit همچنان از API واقعی Server استفاده می‌کند؛ رمز عبور و نقش Server-side اعتبارسنجی می‌شوند.

### 4) Dashboard PC Grouping by Internet — Stage 14 / Dashboard UX
- وقتی گروه‌بندی PC بر اساس Internet انتخاب می‌شود، گروه‌ها باید به‌صورت بصری از هم جدا شوند، نه صرفاً با برچسب.
- برای هر گروه یک header/line واضح مثل «اینترنت ۱» و «اینترنت ۲» نمایش داده شود.
- مرزبندی باید در zoom و viewهای مختلف خوانا بماند و وضعیت Online/Offline Agent را هم مخفی نکند.
- این مورد باید همراه با Dashboard dense mode و responsive hardening اجرا شود تا چند patch پراکنده روی Cardها ایجاد نشود.

### قاعدهٔ اجرایی این صف
- این موارد فعلاً ثبت محصول هستند و نباید قبل از مرحلهٔ مناسب وارد کد شوند، مگر مورد ۳ که یک gap روشن در مدیریت کاربران است و با یک اصلاح محدود UI بسته می‌شود.
- در پایان هر Stage ممیزی، نیازهای جدید UI/عملیاتی به همین صف اضافه می‌شوند و محل اجرای مناسب برایشان تعیین می‌شود.
- Release/Update نهایی بعد از تثبیت این اصلاحات و قبل از rollout گسترده انجام می‌شود.


## وضعیت نهایی Stage 10 — Client Lifecycle — 2026-10-03

- ✅ Current main commit: `5e9453e741552d638989f79f538cbc55aceeae66`
- ✅ Final CI Run #842: **success**
- ✅ persisted lifecycle state and server-authoritative compatibility
- ✅ package manifest/download + SHA256/size verification
- ✅ version-isolated install + previous healthy version
- ✅ controlled restart/watchdog + fresh health confirmation
- ✅ manual rollback + fresh rollback health confirmation
- ✅ final Update/Rollback result is correlated across restart and is reported only after post-restart health
- ✅ `AwaitingHealth` survives intentional restart disconnects
- ✅ rollback fallback is preserved until healthy commit
- ✅ recommended Client version aligned with `0.7.1`
- ✅ regression test added for rollback marker preservation through healthy startup

### Gate استقرار Stage 10

- ⬜ validation روی 2–3 PC واقعی GameNet
- ⬜ rollout کنترل‌شدهٔ 40+ PC

این دو مورد deployment/production gate هستند؛ در CI/مهندسی نرم‌افزار Stage 10 تکمیل شده‌اند و تا انجام سخت‌افزار واقعی نباید به‌عنوان validation فیزیکی Done علامت بخورند.

### بعد از Stage 10

**Stage 11 — Games & Accounts** مرحلهٔ بعدی است:
Game Library واقعی → Account Pool/Lease → Process Detection، بدون فعال‌کردن Mock به‌عنوان قابلیت واقعی.


## Slice 14.11 — Pending Payment / Open Customer Account Dashboard — APPROVED SPEC

این برش مورد تأیید محصول برای «در انتظار پرداخت» است و باید به‌صورت یک قابلیت یکپارچه Server-backed اجرا شود؛ کارت‌های موقت مرورگر مجاز نیستند.

### قرارداد محصول
1. **یک مشتری = یک حساب باز = یک کارت/ردیف**؛ شارژ جدید، خرید جدید بوفه یا اصلاحات همان حساب را به‌روزرسانی می‌کند و آیتم جدید برای همان مشتری ساخته نمی‌شود.
2. حساب باز از **Draft Invoice مرتبط با Session** به‌عنوان منبع حقیقت استفاده می‌کند؛ بعد از «پرداخت بعداً»، Session نهایی شده ولی Invoice تا زمان پرداخت Draft می‌ماند.
3. کارت پیش‌فرض باید بسیار فشرده باشد تا ۱۰–۲۰ مشتری هم‌زمان قابل مدیریت باشند.
4. سه View هم‌راستا با رایانه‌ها: **کارتی / فشرده / لیستی**.
5. در حالت عادی فقط هویت مشتری، ایستگاه، خلاصهٔ شارژ، خلاصهٔ بوفه، مبلغ قابل پرداخت و عملیات پرداخت دیده شود.
6. جزئیات به‌صورت **بازشوندهٔ شاخه‌ای** فقط داخل همان کارت/ردیف نمایش داده شود: + شارژها، + بوفه، + محاسبه مبلغ.
7. شارژها به‌صورت اقلام جداگانه با مبلغ و روش پرداخت ثبت شوند؛ جمع شارژ و تعداد آن‌ها در حالت بسته دیده شود.
8. بوفه به‌صورت محصول تجمیعی با تعداد و مبلغ نمایش داده شود؛ جزئیات اقلام فقط در حالت باز دیده شود.
9. محاسبهٔ مبلغ نهایی فقط از Server-authoritative breakdown پیروی کند: هزینه بازی + بوفه − اعتبار شارژ مصرف‌شده − تخفیف/اعتبارات مجاز = مبلغ نهایی.
10. **اعتبار باقی‌ماندهٔ شارژ** با تخفیف اشتباه نشود و دوبار از مشتری کم نشود؛ مقدار مصرف‌شده و باقی‌مانده باید قابل تشخیص باشند.
11. پرداخت بعدی فقط از همان حساب باز انجام شود و پس از موفقیت، کارت/ردیف از لیست «در انتظار پرداخت» حذف شود.
12. خرید بوفه بعد از پایان Session نیز در صورت وجود حساب باز باید به همان Invoice اضافه شود.
13. شارژ زمان باید Server-backed و durable باشد؛ Session.PrepaidAmount به‌تنهایی برای تاریخچهٔ چند شارژ کافی نیست.
14. شارژ نقدی/کارت/کیف پول باید اثر مالی و Audit داشته باشد؛ کیف پول هنگام ثبت شارژ فوراً از Ledger کم شود.
15. پرداخت نهایی کارت می‌تواند نقدی، کارتخوان یا کیف پول باشد؛ کنترل موجودی کیف پول Server-side باقی بماند.
16. عملیات حساس باید Permission + Persistence + Audit + خطای فارسی داشته باشند و محاسبهٔ نهایی هرگز فقط در React انجام نشود.
17. مرتب‌سازی فشردهٔ Pendingها: **مشتری / رایانه / مبلغ نهایی / زمان انتظار / تعداد شارژ / تعداد بوفه / وضعیت** و کلیک دوم جهت را معکوس کند.
18. View و باز/بسته‌بودن شاخه‌ها presentation-only است و نباید مبلغ یا Ledger را تغییر دهد.
19. داشبورد باید پس از شارژ/فروش/پرداخت‌بعداً و نیز تغییرات ناشی از بخش بوفه، Pendingها را سریع Refresh کند؛ داده از Server قابل بازیابی مجدد باشد.
20. «ثبت بدهی» در این بخش نباید یک بدهی تکراری از همان Invoice بسازد؛ Pending Draft خودش حساب بدهکار را نمایندگی می‌کند.

### قرارداد فنی
- Entity جدید SessionCharge برای نگهداری جزئیات هر شارژ زمان، مرتبط با Session/Invoice/User.
- Endpointهای Server برای ثبت شارژ، پایان Session با settle-later، دریافت Pending accounts و تسویه Pending invoice.
- فروش بوفه باید بتواند علاوه بر Session فعال، به Pending Invoice مشخص نیز متصل شود.
- Shift cash reconciliation باید InvoicePaymentهای واقعی دریافت‌شده در زمان شارژ را نیز بشناسد، حتی اگر Invoice هنوز Draft باشد.
- تست Server برای شارژ، pay-later، تجمیع یک Invoice، فروش بوفه روی Pending و تسویه نهایی لازم است.
- Browser Smoke برای سه View، Expand/Collapse شاخه‌ها، به‌روزرسانی همان مشتری و حذف کارت پس از پرداخت لازم است.

### Gate
- ⬜ Build
- ⬜ .NET Tests
- ⬜ EF validation / migration
- ⬜ Server/Migration/Health Smoke
- ⬜ Dashboard Lint/Build
- ⬜ Dashboard Browser Smoke
- 🟡 physical validation remains separate from this software slice.
