# ممیزی عمیق Stage 1 تا 12 — 2026-10-04

## روش ممیزی
کل tree مخزن روی شاخه `completion/control-20261003` بررسی شد:
- 214 فایل در repository tree
- 114 فایل source غیر migration
- Server / Client / Dashboard / Shared / Tests
- migration و model snapshot
- Integration Contract و Stage auditهای قبلی
- مسیرهای mock، permission، persistence، transaction، concurrency، Agent command و recovery

مرجع تصمیم:
`Server Truth → Contract → Identity → Permission/Ownership → Validation → Transaction → Persistence → Audit → Agent/External Action → Result → UI Event`

## نتیجه فعلی

### مواردی که هسته Stage 1 تا 12 در آن‌ها verified است
- Foundation/Build/EF/Startup و Health
- Server-authoritative Session timing/Settlement
- Customer/VIP/Wallet/Debt/Benefits
- Buffet/Inventory/Invoice integration
- Permission/Approval foundation
- Agent identity/heartbeat/reconnect/command persistence
- Kiosk/Lock/Unlock/Logout-lock
- Client update/rollback/recovery
- Game Catalog + Account Pool/Lease/Credential boundary
- Stage 12 CustomerLogin → Session → Game → Lease → Credential → Agent → End/Release
- ActiveSession DB ownership guards
- Game Sync Command lifecycle تا Succeeded
- Process telemetry persistence از Agent

### گپ‌های نرم‌افزاری واقعی که باید بسته شوند

#### GAP-01 — Tariff Management هنوز Mock است
**مشکل:** `TariffsPage.tsx` مستقیماً از `mockService` استفاده می‌کند و backend CRUD واقعی برای Tariff ندارد.
**اثر:** Stage 4 / pricing source-of-truth ناقص؛ تغییر تعرفه UI روی Server ثبت نمی‌شود.
**اصلاح صحیح:** Shared tariff contracts + Server permission `tariff.view/manage` + CRUD واقعی با soft-deactivate و Audit + Dashboard service/page واقعی. Mock tariff path حذف شود.
**Verification:** CRUD + duplicate/name validation + audit + Dashboard build/smoke.

#### GAP-02 — Production Seed داده‌ی Demo و رمز پیش‌فرض دارد
**مشکل:** Seeder در DB خالی مشتری/محصول/تعداد ثابت ایستگاه و password fallback را Seed می‌کند.
**اثر:** ریسک داده جعلی و credential ناامن در نصب واقعی.
**اصلاح صحیح:** Base seed فقط Permission/StationType/Tariff/VIP package/Admin را بسازد؛ demo customer/product/stations فقط Development. Production بدون `GAMENET_ADMIN_PASSWORD` fail-fast کند.
**Verification:** production-like seed test + development demo seed test.

#### GAP-03 — Customer Login concurrency فقط application-level است
**مشکل:** شمارش active login قبل از insert انجام می‌شود و برای race هم‌زمان DB invariant ندارد.
**اثر:** امکان عبور از ConcurrentLoginLimit زیر درخواست هم‌زمان.
**اصلاح صحیح:** transaction را با write-lock روی Customer serialize کن، سپس count و insert؛ SQLite lock conflict را به conflict قابل‌فهم تبدیل کن.
**Verification:** concurrent acquire test.

#### GAP-04 — Buffet stock sale check/decrement atomic نیست
**مشکل:** stock خوانده و در entity کم می‌شود؛ دو sale هم‌زمان می‌توانند هر دو موجودی قدیمی را ببینند.
**اثر:** oversell/lost update.
**اصلاح صحیح:** هر item با `ExecuteUpdate WHERE StockQuantity >= requested` claim شود و در صورت affected=0 کل transaction rollback/conflict شود.
**Verification:** concurrent sale test.

#### GAP-05 — Global Command Center هنوز Mock data می‌خواند
**مشکل:** Customer/Product/Tariff/Client/User/Invoice را از `mockService` می‌گیرد.
**اثر:** نتیجه جستجو می‌تواند وضعیت جعلی نشان دهد.
**اصلاح صحیح:** فقط Server services/APIهای واقعی؛ برای entityهایی که API جمعی ندارند نتیجه mock ساخته نشود.
**Verification:** source audit + Dashboard build/smoke.

#### GAP-06 — ClientShell هنوز Mock و Fake-success است
**مشکل:** Client list و عملیات‌هایی مثل network switch/restart/message/screenshot با state محلی/notice شبیه اجرای واقعی نشان داده می‌شوند.
**اثر:** UI دروغ می‌گوید و operator تصمیم اشتباه می‌گیرد.
**اصلاح صحیح:** Agent status واقعی + command API برای عملیات واقعاً پشتیبانی‌شده؛ عملیات بدون backend باید از مسیر success حذف/disabled شوند، نه fake.
**Verification:** Agent command status smoke و no-mock production path.

#### GAP-07 — Dashboard DEV fallback به Mock
**مشکل:** `App.tsx` در خطای API به mock stations fallback می‌کند.
**اثر:** در محیطی که Server unavailable است UI می‌تواند داده جعلی نشان دهد.
**اصلاح صحیح:** fallback حذف؛ error state واضح و retry.
**Verification:** browser smoke با API failure.

#### GAP-08 — Settings/Page Lock فقط localStorage است
**مشکل:** lockهای صفحه UX-only هستند.
**اثر:** مرز امنیتی نیست.
**اصلاح صحیح:** حفظ UX lock مجاز است، اما mutation حساس فقط Server permission داشته باشد؛ UI نباید آن را به‌عنوان security boundary معرفی کند.
**تصمیم:** permission backend موجود است؛ در این audit فقط UI wording و fallbackهای حساس بازبینی می‌شوند، rewrite کامل Settings خارج از Stage 1-12 است.

### GAPهای خارج از implementation Stage 1-12
- Backup/Restore drill واقعی
- Installer/Separate Server-Client deployment
- Physical 2–3 PC/40+ rollout
- Canary update
- notification queue جامع
- final responsive/keyboard pass
این‌ها Field/Release gates هستند و با mock یا checkbox بسته نمی‌شوند.

## ترتیب اجرای اصلاحات
1. GAP-02 Seeder/credential safety
2. GAP-03 CustomerLogin concurrency
3. GAP-04 Inventory atomic sale
4. GAP-01 Tariff Server Truth
5. GAP-05 Global Search
6. GAP-06 ClientShell fake-success
7. GAP-07 Dashboard mock fallback
8. regression tests + exact-SHA CI
9. final Stage 1-12 audit refresh

## قانون ضد تکرار
هیچ Stage جدیدی قبل از بسته‌شدن regression این گپ‌ها شروع نمی‌شود.
