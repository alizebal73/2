# Stage 11 — Server-backed Game Catalog + Account Pool/Lease

## هدف
خروج کامل Game و Account از Dashboard Mock و ساخت مدل جدید Account Pool/Lease مستقل از GameAccount قدیمی.

## تصمیم‌های معماری
- `GameAccount` قدیمی حذف یا مهاجرت نمی‌شود؛ برای سازگاری تاریخی باقی می‌ماند.
- مدل جدید:
  - `AccountPoolEntry` = موجودی/حساب قابل تخصیص
  - `AccountLease` = رکورد مالکیت موقت و قابل ممیزی تخصیص
- Server تنها منبع حقیقت وضعیت آزاد/درحال‌استفاده/قفل است.
- Dashboard فقط API واقعی Server را مصرف می‌کند.
- تخصیص اکانت با تراکنش کوتاه + `ExecuteUpdate` شرطی انجام می‌شود؛ اگر ردیف آزاد در لحظه تغییر کرده باشد، عملیات موفق تلقی نمی‌شود.
- رمز/Secret در پاسخ API برگردانده نمی‌شود و فقط hash آن در این مرحله نگهداری می‌شود؛ ارسال credential به Agent خارج از Stage 11 است.

## Game
- CRUD واقعی Server-backed برای Game.
- فیلدهای UI قبلی شامل نسخه، مسیر، exe، launch args، وضعیت، نوع اتصال و target در مدل Server ذخیره می‌شوند.
- حذف Game به‌صورت archive انجام می‌شود و اگر Lease فعال داشته باشد رد می‌شود.

## Account Pool
- CRUD واقعی برای حساب‌های Pool.
- unlock واقعی Server-side.
- فهرست Leaseهای فعال.
- Allocate و Release واقعی.
- audit برای ایجاد/ویرایش/رفع قفل/تخصیص/آزادسازی.

## تست
- تست Allocate/Release با SQLite.
- تست رقابت دو تخصیص برای یک حساب؛ فقط یک Lease باید موفق شود.
- CI باید Build/Test، migration/startup و smoke API را روی self-hosted runner تأیید کند.

## خروجی مورد نیاز برای بستن Stage 11
1. Build سبز.
2. تست‌های .NET سبز.
3. migration جدید بدون خطا اعمال شود.
4. smoke واقعی Game create/list/update/archive.
5. smoke واقعی Account Pool create/list.
6. smoke allocate → lease active → release → free.
7. Dashboard build/lint و E2E بدون استفاده از mock برای Game/Account.
8. هیچ مرحله‌ای Done قبل از تأیید CI head نهایی نیست.

## Stage 12
پس از سبز شدن Stage 11، مرحله بعد باید فقط از همین head ادامه پیدا کند و ابتدا وابستگی‌های باقی‌مانده Game/Account را دوباره بررسی کند؛ مخصوصاً اتصال Lease به Session/Agent و مسیر واقعی credential delivery، بدون شکستن `GameAccount` قدیمی.

## وضعیت نهایی

- ✅ Stage 11 implementation is complete.
- ✅ CI Run #895 verified the full pre-hardening Stage 11 surface: .NET Build/Test, migration/startup smoke, Dashboard install/lint/build, preview startup and browser interaction smoke.
- ✅ Final hardening added Game CRUD smoke, Account Pool CRUD/unlock smoke, persisted Lease release reason, real Account login editing, and removed the fake Game Apply/Sync success path.
- ⏳ The latest post-hardening `main` head must still have a green CI run before it becomes the Stage 12 baseline.

### Intentional Stage 12 boundary

The following are deliberate Stage 12 boundaries, not hidden Stage 11 defects:
- Game `activeUsers`/operational status becomes real operational data in Stage 12.
- Lease ↔ Session/Agent lifecycle integration and automatic release belong to Stage 12.
- Secure credential delivery to Agent belongs to Stage 12; Dashboard never receives Secret/SecretHash.
- Real Game Apply/Sync to Agents belongs to Stage 12; Stage 11 no longer presents a fake successful sync.
- Lease history/health and process detection remain later backlog items unless Stage 12 dependency analysis pulls them forward.
