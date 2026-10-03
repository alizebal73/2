# GameNet — Completion Control Plan (Authoritative)

> از 2026-10-03 این سند مرجع تصمیم‌گیری برای تکمیل محصول است. نقشه‌راه‌ها و checklistهای قبلی فقط سابقه تاریخی‌اند و برای تصمیم جدید معیار نیستند.

## هدف نهایی

«100% کامل» یعنی یک GameNet Manager قابل استفاده واقعی در گیم‌نت؛ نه فقط مجموعه‌ای از endpointها یا یک CI سبز.

شرایط نهایی:
- عملیات روزانه واقعی و بدون fake/mock path
- Server تنها منبع حقیقت
- بازیابی stateهای حساس بعد از restart/crash/disconnect
- کنترل race و transaction در عملیات حساس
- اتصال واقعی Dashboard ↔ API/Hub ↔ DB ↔ Agent
- backup/restore، update/rollback و deployment واقعی
- تست چند دستگاه و rollout کنترل‌شده

## تعریف 100%

هر قابلیت باید از چهار سطح عبور کند:

1. Real — پیاده‌سازی واقعی دارد.
2. Correct — مدل داده، قرارداد، security، persistence و concurrency آن درست است.
3. Verified — تست مناسب + CI روی همان SHA.
4. Field-proven — شواهد عملی در staging/محیط واقعی.

تا سطح لازم از هر چهار مورد ثابت نشده، قابلیت Done نیست.

## وضعیت واقعی baseline

مبنای کار شاخه stage12-session-lease و head فعلی پروژه در 2026-10-03 است.

هسته‌های موجود:
- Server + SQLite/EF Core + SignalR
- Dashboard React/TypeScript
- Windows Client Agent
- Customer/Login/Station/Agent
- Session/tariff/settlement و هسته مالی موجود
- Buffet/Inventory
- Users/Permissions/Approval
- Agent heartbeat/reconnect/command lifecycle
- Client update/rollback/watchdog/health
- Game Catalog
- Account Pool/Lease
- Session → Game → Agent → Lease → Credential
- Game Apply/Sync واقعی

این baseline هنوز 100% نیست و حتی CI فعلی روی همان head سبز نیست.

## مسیر تکمیل جدید — Wave Based

### Wave 0 — Baseline & Control
مرجع واحد، branch قابل بازیابی، ثبت head/CI و commitهای کوچک.

### Wave 1 — Integrity Core
EF/migration/SQLite، UTC storage، transaction، atomic allocation/release، idempotency، persistence، DataProtection، contract stability و حذف fake paths.

کار جاری این Wave:
- رفع خانواده خطای DateTimeOffset در Queryهای SQLite، نه فقط یک property.
- اجرای Build → Test → EF → Server smoke روی همان SHA.

### Wave 2 — Business Truth
Customer/Login، Station، Session start/extend/pause/end/transfer، tariff/VIP، wallet/debt/payment/discount، invoice/settlement/reversal، buffet و shift.

قاعده هر عملیات حساس:
Permission → Validation → Transaction → Persistence → Audit → Result → Reverse/Cancel (هرجا لازم است).

### Wave 3 — Agent & PC Reality
identity، heartbeat، reconnect، command/ACK/result، lock/unlock/logoff/restart/shutdown/message، session display، kiosk/shell، game launch، process detection/telemetry، recovery و ownership.

Process Detection تا قبل از contract و telemetry واقعی fake نمی‌شود.

### Wave 4 — Dashboard & UX Closure
dashboard live cards، customer/session controls، games/accounts، permissions، reports/audit، settings، RTL/Persian، keyboard-first، error/empty/loading states و هماهنگی real-time.

### Wave 5 — Operations & Scale
reporting scopes، audit explorer، fine-grained permissions، notification/event queue، reservations/waitlist، recovery workflows، multi-cashier consistency و health/status.

### Wave 6 — Data Safety & Release
backup واقعی، retention، DataProtection key backup، restore test، production config، secrets، package integrity، migration upgrades، update/rollback، canary، health gate و installer/setup.

### Wave 7 — Field Proof
1 Server+Agent → 2–3 PC → چند اپراتور → restart/crash/network loss → restore → update/rollback → rollout کنترل‌شده 40+ PC.

## روش اجرای هر تغییر

Root Cause → Domain owner → Data model → Contract → Concurrency/transaction → Persistence/migration → UI/Agent impact → Test design → Implementation → exact-SHA verification → checkpoint/commit → ادامه.

## اولویت

1. correctness
2. persistence/recovery
3. race/data corruption
4. security/permission bypass
5. contract mismatch
6. missing real behavior
7. operational UX
8. polish

## مدارک نهایی

چهار ماتریس باید قابل بازسازی باشند:
- Architecture/Dependency Matrix
- Data & Migration Matrix
- Feature/Contract/E2E Matrix
- Deployment/Recovery/Field Evidence

قدیمی بودن Stage یا checklist دلیل Done بودن نیست؛ رفتار واقعی فعلی ملاک است.
