# Chat Handoff — GameNet Manager — 2026-10-02

این فایل برای انتقال دقیق وضعیت پروژه بین چت‌هاست. در چت جدید، قبل از هر تغییر کد، این فایل و سپس فایل‌های پروژه را در همین ریپو بررسی کن.

## 0) مرجع مطلق پروژه

- فقط ریپوی مورد کار: `alizebal73/2`
- ریپوهای دیگر را برای این پروژه بررسی/ویرایش نکن.
- محصول: GameNet Manager / GameNet Pro برای مدیریت یک GameNet واقعی، با هدف اجرای پایدار روی 40+ دستگاه.
- معماری مرجع: Server منبع حقیقت است؛ Dashboard و Client نباید state مالی/عملیاتی مستقل و authoritative داشته باشند.
- branch فعال: `stage8-pc-agent-foundation`
- base merge commit: `bc4307f73edb0e535f7a57061aace9dac95e3c1e`
- branch Stage 8 از merge موفق Stage 7 ساخته شده است؛ head جدید باید قبل از هر تغییر دوباره از GitHub خوانده شود.
- PR #2 — `Stage 7: real operator auth, users, permissions and approvals` — **merged** در `main`.
- Merge commit: `bc4307f73edb0e535f7a57061aace9dac95e3c1e`.
- Stage 8 branch: `stage8-pc-agent-foundation`.
- CI مبنای بسته‌شدن Stage 7: Run #593 روی head `a7135fe...` → **success**.
- CI Run #504 که قبلاً در حال اجرا بود مربوط به head قدیمی `6b05f292...` بود؛ مبنای فعلی نیست. مبنای معتبر فعلی Run #555 روی `4c283e...` است.
- main پس از merge Stage 7: `bc4307f73edb0e535f7a57061aace9dac95e3c1e`

## 1) قوانین غیرقابل شکستن پروژه

- همه متن‌های قابل مشاهده کاربر فارسی و قابل‌فهم باشند.
- Server تنها منبع حقیقت است.
- Mock فقط تا وقتی جایگزین واقعی و تست‌شده وجود ندارد قابل نگهداری است؛ Mock نباید به‌عنوان قابلیت واقعی ارائه شود.
- عملیات حساس باید تا حد لازم Permission + Persistence + Audit + خطای فارسی + Reverse/Cancel داشته باشد.
- هیچ مرحله‌ای Done محسوب نشود مگر پیاده‌سازی + Build/Test + CI و برای UI تست تعاملی لازم را پاس کرده باشد.
- وابستگی‌ها باید زودتر از مصرف‌کننده تکمیل شوند.
- قبل از شروع هر تغییر مهم، اثر آن بر Update/Release و امکان آپدیت امن نسخه بعدی بررسی شود.
- Installer/build artifact فعلاً نباید به GitHub upload شود؛ در صورت نیاز خروجی محلی روی E: نگهداری می‌شود.
- CI روی self-hosted Windows runner پروژه اجرا می‌شود.
- از بازنویسی بزرگ و پرریسک Program.cs بدون نیاز واقعی خودداری شود؛ modularization مرحله‌ای است.
- Permission UI به‌تنهایی کافی نیست؛ endpoint سمت Server باید واقعاً مجوز را enforce کند.
- AppUserId در عملیات حساس نباید از request body قابل اعتماد باشد؛ actor واقعی باید از session/auth سمت Server مشخص شود. احراز هویت Agent در Stage 8 مسیر جدا دارد.

## 2) معماری فعلی

### Server
- ASP.NET Core 10
- EF Core + SQLite
- SignalR
- مسیر اصلی: `src/Server`
- `Program.cs` هنوز بزرگ است اما باید تدریجی modular شود.
- سرویس‌های مهم فعلی شامل Session Settlement و Invoice Reverse.
- `App_Data/gamenet.db` برای دیتابیس runtime.
- Database migration/initialization واقعی است.

### Dashboard
- React + TypeScript + Vite
- مسیر: `src/Dashboard`
- login واقعی با session سمت Server
- Users/Permissions از API واقعی
- Command Center و navigation باید Permission واقعی را رعایت کنند
- Dashboard نباید authority را فقط از route/UI بگیرد.

### Client
- مسیر: `src/Client`
- فعلاً Client واقعی Windows Agent کامل نشده و هنوز بخشی از قابلیت‌های Stage 8/9 باقی است.
- Agent واقعی باید از Server فرمان و policy بگیرد، heartbeat/health/telemetry داشته باشد و در قطع ارتباط رفتار امن داشته باشد.

### Shared
- `src/Shared` برای قراردادهای مشترک.

## 3) مراحل اصلی پروژه و وضعیت

### Stage 1 — Foundation
بسته شده:
- repo/solution foundation
- Server/Dashboard/Client/Shared skeleton
- CI و self-hosted runner
- پایهٔ پایدار برای build/test

### Stage 2 — Prototype & Behavior Transfer
بسته شده:
- انتقال رفتارهای پایه Prototype به معماری جدید
- مسیرهای اصلی Dashboard/Server تثبیت شده‌اند.

### Stage 3 — Operational Completion
بسته شده:
- عملیات روزمره اپراتور و UX پایه
- الگوی Dashboard عملیاتی و کارت‌های دستگاه
- کارت عادی نباید قیمت ساعتی را نشان دهد.
- کارت باید این‌ها را نشان دهد:
  1. نام/شماره دستگاه
  2. username/شناسه مشتری متصل
  3. نام و نام خانوادگی مشتری
  4. زمان باقی‌مانده وقتی واقعاً session end time دارد
  5. بدهی مشتری
  6. یادداشت کوتاه
  7. وضعیت واقعی دستگاه

### Stage 4 — Finance & Session Core
هستهٔ عملیاتی بسته شده:
- ledger/financial core
- settlement
- reverse/restore
- session lifecycle
- split payment / shift-related foundations
- concurrency protections و مسیرهای مالی واقعی در حال hardening نهایی Stage 7 هستند.

### Stage 5 — Customer & VIP Domain
بسته شده و smoke-tested:
- Customer CRUD
- VIP package
- Debt
- PBKDF2 customer credentials
- Customer Auth
- concurrent customer login
- VIP usage
- customer history
- Agent-side customer login عمداً به Stage 8 موکول است.

### Stage 6 — Buffet & Inventory Domain
بسته شده و smoke-tested:
- buffet catalog
- create/update product
- initial stock
- MinimumStock + Unit
- InventoryTransaction
- Adjustment / Waste / Return
- low-stock
- inventory history
- atomic buffet sale
- session-linked buffet sale
- session draft invoice
- settlement/reuse of draft invoice
- reverse/cancel with stock restore
- purchase + UnitCost + weighted average CostPrice
- historical sale pricing
- profit report
- Reports Center summary
- migration and smoke verified
- Stage 6 merge to main: `fa68c6e8740f8165789e711e56b2024e76e20921`

### Stage 7 — Users & Permissions
**بسته و merge شده است.**

پیاده‌سازی و تست‌شده:
- AppUser
- AppUserSession
- real operator authentication
- HttpOnly server session cookie
- PBKDF2 password hashing
- `/api/auth/login`
- `/api/auth/me`
- `/api/auth/logout`
- AppUser CRUD
- canonical Permission catalog
- permission assignment
- ApprovalRequest persistence
- approval decision
- Audit for operator/auth/user/permission/approval actions
- Dashboard login gate
- real Users/Permissions UI
- server-side permission enforcement across sensitive Customer/VIP/Buffet/Inventory/Debt/Wallet/Benefits/Finance/Shift/Session/Invoice Reverse endpoints
- sensitive actor identity from authenticated server session, not request body
- negative smoke: limited Operator gets 403 on missing permission
- multi-cashier optimistic concurrency using UpdatedAt with HTTP 409 on stale writes
- executable approval flow for invoice Reverse:
  - finance.manage requests reverse
  - approval.decide executes approval
  - requester self-approval rejected
  - reverse is audited
  - inventory is restored
- initial Dashboard permission hardening:
  - main navigation filters by real permission
  - Command Center / keyboard navigation cannot enter unauthorized sections
  - Users & Shift separates user.manage from shift.manage
  - Admin/Owner retain global server authorization behavior
- latest head CI #555 is green.

## 4) Stage 7 — وضعیت نهایی

موارد Stage 7 که روی head نهایی پیاده‌سازی و با CI سبز تأیید شدند:
- Payroll/employee ledger و salary payment/receipt/approval
- Read/Write Permission hardening در دامنه‌های فعال Server-backed
- Approval اجرایی Invoice Reverse و Wallet Refund
- actor identity از Server session
- generic approval action allowlist
- multi-cashier stale-write / HTTP 409

Mockهای Accounts/Games/Tariffs/ClientShell عمداً برای Stageهای دارای dependency واقعی نگه داشته شدند.

## 4.5) Stage 8 — PC Agent Foundation

هدف این مرحله ساخت اولین **Vertical Slice واقعی PC** است، نه پرکردن UI با Mock.

ترتیب:
1. قرارداد هویت Agent و Device Registration در Server/Shared
2. Heartbeat و Online/Offline state سروری
3. Health/Telemetry پایه با timestamp و last-seen
4. اتصال پایدار SignalR با reconnect و تشخیص stale connection
5. Lock/Unlock و Session Start/End به‌عنوان اولین فرمان‌های واقعی
6. Timer زنده بر مبنای دادهٔ معتبر Server
7. قطع/وصل سرور و رفتار امن Offline
8. تست عملی روی 2–3 PC واقعی، سپس مقیاس‌دادن الگو برای 40+ دستگاه

قواعد Stage 8:
- Server منبع حقیقت Device/Session state است.
- Agent اجازه ندارد state مالی/اعتباری را authoritative کند.
- هر فرمان دارای correlation/idempotency و نتیجهٔ قابل ممیزی باشد.
- در قطع ارتباط، Agent باید fail-safe باشد و آخرین وضعیت را از live state متمایز کند.
- هیچ Mock Agent به‌عنوان قابلیت واقعی پذیرفته نمی‌شود.

## 4) Stage 7 — سابقهٔ موارد تکمیل‌شده

1. **Full server-backed payroll/employee ledger and salary payments**
   - پرسنل/حقوق واقعی، نه UI نمایشی.
   - ledger مستقل برای هر پرسنل.
   - حقوق محاسبه‌شده، پرداخت، مانده، پیش‌پرداخت، پاداش، کسری مصوب، مساعده/وام، اصلاحات دستی با دلیل.
   - «بدهی مالک به کارمند» و «بدهی کارمند به مالک» جدا نگه داشته شود.
   - خسارت/کسری رکورد مستقل داشته باشد.
   - پرداخت حقوق Receipt/Payment واقعی داشته باشد.
   - Permission/Approval برای پرداخت حقوق، پاداش، کسری، خسارت و اصلاح حقوق.

2. **Complete Read/Write Permission hardening across all domain pages**
   - فقط navigation را مخفی نکن؛ عملیات Read/Write هر صفحه را با permission واقعی هماهنگ کن.
   - هر endpoint حساس Server باید authority واقعی داشته باشد.
   - GETهای حساس هم بر اساس نقش/permission مناسب محدود شوند.
   - UI و Server باید یک مدل permission واحد داشته باشند.

3. **Connect remaining sensitive UI actions to Approval flows**
   - هر عملیاتی که business rule آن حساس/قابل سوءاستفاده است باید قبل از رفتن Stage 8 تصمیم‌گیری و در صورت نیاز به Approval وصل شود.
   - Approval باید server-side و transaction-safe باشد.

4. **تکمیل smoke/E2Eهای Stage 7**
   - مسیرهای payroll
   - read/write authorization
   - approvalهای باقی‌مانده
   - stale-write / multi-cashier
   - پایان شیفت و اثر آن روی ledger/finance
   - جلوگیری از برگشت accidental به mock behavior

## 5) موردی که قبلاً مشکل ایجاد کرد و باید دوباره تکرار نشود

- ترتیب declarations در Program.cs باعث CS8803 شده بود؛ declaration/helper باید جای درست داشته باشد.
- EF expression tree با local function در Select باعث CS8110 شد؛ ابتدا query را materialize و سپس mapping را در memory انجام دادیم.
- PendingModelChangesWarning با nullable mismatch در Snapshot رخ داد؛ `AppUserSession.ExpiresAt` باید required/non-null باشد و Snapshot با model واقعی هماهنگ بماند.
- بعد از permission enforcement، smokeهای قدیمی که بدون operator session endpoint می‌زدند باید با authenticated WebRequestSession اجرا شوند.
- CIهای قبلی متعدد cancel شدند؛ فقط آخرین run مربوط به head فعلی مبنای وضعیت است.

## 6) Permission Catalog مرجع فعلی

کاتالوگ فعلی:
- session.start
- session.manage
- session.settle
- customer.manage
- customer.wallet
- customer.debt
- buffet.sell
- buffet.inventory
- finance.view
- finance.manage
- shift.manage
- tariff.manage
- game.manage
- account.manage
- client.control
- user.manage
- approval.decide
- audit.view

Admin/Owner طبق AuthorizationService دسترسی global دارند؛ سایر نقش‌ها باید permission صریح داشته باشند.

## 7) Approval / Finance مرجع

برای Reverse صورتحساب که در Stage 7 اجرایی شده:
- Operator دارای finance.manage درخواست می‌دهد.
- درخواست در ApprovalRequest ذخیره می‌شود.
- approval.decide آن را اجرا می‌کند.
- self-approval رد می‌شود.
- عملیات Reverse باید transaction-safe باشد.
- Audit ثبت می‌شود.
- stock/side effects در صورت نیاز restore می‌شوند.

این الگو باید برای عملیات حساس بعدی reused شود، نه اینکه برای هر حوزه یک سیستم Approval جدا ساخته شود.

## 8) Update / Release Architecture که باید همیشه رعایت شود

مرجع: `docs/11-update-release-architecture.md`

چهار/پنج version contract جدا هستند:
- ProductVersion
- SchemaVersion
- ApiContractVersion
- MinimumClientVersion
- RecommendedClientVersion

Manifest:
- `GET /api/release/manifest`

پیش‌فرض فعلی:
- productVersion = 0.6.0
- apiContractVersion = 1
- minimumClientVersion = 0.1.0
- recommendedClientVersion = 0.1.0
- updateChannel = stable
- schemaVersion از migrationهای applied می‌آید.

قواعد:
- تغییرات API تا حد ممکن additive باشند.
- `/api/v2` فقط برای breaking change واقعی.
- قبل از migration واقعی backup.
- health/smoke بعد از migration.
- destructive DB reset خودکار ممنوع.
- release flow مرجع:
  Build → Test → Migration Smoke → Dashboard Build/Lint → E2E → Manifest/Package → Canary → Health Check → Publish
- Installer هنوز نباید به GitHub upload شود.
- Updater/Rollback واقعی برای Client در Stage 10/14 است:
  manifest verify → package verify → safe/atomic install → keep previous healthy version → rollback on crash/health failure → controlled restart → canary → per-PC update state.

## 9) تصمیم‌های معماری تأییدشده برای مسیر بعد

1. قبل از polish فرعی، یک vertical slice واقعی PC در Stage 8:
   - اتصال Agent به Server
   - lock/unlock
   - session start/end
   - live timer
   - disconnect/reconnect
   - safe offline behavior
   - تست روی 2–3 PC واقعی قبل از گسترش به 40 دستگاه.

2. Customer client باید auth واقعی داشته باشد؛ Server/Agent مجوز نهایی است.

3. Program.cs باید مرحله‌ای شکسته شود، نه rewrite یک‌باره.

4. Backup خودکار و قابل‌بازیابی باید قبل از استفاده واقعی اجباری شود.

5. E2E یک روز کاری کامل باید بعد از تثبیت مسیرهای واقعی اضافه شود:
   shift start → session start → charge → buffet → extension/stop → settlement → refund/reverse → shift close

6. Dashboard hardening:
   - 1366×768
   - dense operator mode برای سالن
   - footer overlap ممنوع
   - connection state واضح
   - last known data از live data جدا
   - responsive/tablet
   - contrast
   - font sizing
   - SVG/icon به‌جای emoji در مسیرهای حساس

7. Physical layout view برای PC/PS بعداً Stage 13/14، نه الان.

8. Reliable notification queue برای expiry/debt/server-client disconnect/command errors بعداً.

## 10) ترتیب اجرای پیشنهادی از همین نقطه

**Stage 7 بسته شده؛ اکنون فقط Stage 8 را اجرا کن.**

گام A: Stage 8 foundation
- قرارداد Device/Agent را از Shared تا Server نهایی کن.
- کامل‌کردن server persistence + APIs + ledger + payments.
- جایگزینی mock UI مربوط به payroll فقط وقتی backend واقعی حاضر است.

گام B:
- audit کامل endpointها و صفحات برای Read/Write Permission.
- ماتریس permission ↔ endpoint ↔ dashboard control بساز و هر مورد ناقص را اصلاح کن.

گام C:
- Approval برای تمام عملیات حساس باقی‌مانده.
- transaction safety + audit + reverse/cancel.

گام D:
- CI smoke و E2E جامع Stage 7.
- فقط پس از Green واقعی، Stage 7 را Done/merge کن.

**اکنون Stage 8 فعال است.**

Stage 8:
- PC Agent Foundation
- heartbeat
- health
- telemetry
- stable Agent identity
- reconnect
- online/offline state
- vertical slice روی 2–3 PC واقعی
- Server-side device truth
- آماده‌سازی برای 40+ PC

سپس:
- Stage 9 Real Client Commands & Kiosk
- Stage 10 Client Lifecycle / Update / Rollback
- Stage 11 Games & Accounts
- Stage 12 Reporting & Audit
- Stage 13 Reservations & Operations Scale
- Stage 14 UI/Deployment Hardening

## 11) چیزهایی که فعلاً نباید انجام شود

- Stage 8/9 را قبل از بسته‌شدن قراردادهای Stage 7 شروع نکن، مگر یک dependency فنی کوچک که صراحتاً برای Stage 7 لازم باشد.
- Mock را صرفاً برای ظاهر «کامل» نگه ندار.
- installer pipeline را قبل از تثبیت محصول نهایی، محور اصلی کار نکن.
- برای هر Permission سیستم جداگانه نساز؛ PermissionCatalog سروری مرجع واحد بماند.
- data authority را به Dashboard یا Client منتقل نکن.
- برای گزارش یا UI، state مالی را local-only نکن.

## 12) فایل‌های کلیدی که در چت جدید ابتدا باید خوانده شوند

حداقل:
- `docs/10-product-completion-backlog.md`
- `docs/11-update-release-architecture.md`
- این فایل: `docs/12-chat-handoff-2026-10-02.md`
- `src/Server/Program.cs`
- `src/Server/Data/GameNetDbContext.cs`
- `src/Server/Data/DatabaseSeeder.cs`
- `src/Server/AuthorizationService.cs`
- `src/Server/PasswordSecurity.cs`
- migrationهای اخیر
- `src/Dashboard/src/App.tsx`
- `src/Dashboard/src/pages/UsersPage.tsx`
- `src/Dashboard/src/services/authService.ts`
- `src/Dashboard/src/types.ts`
- `e2e/playwright*.cjs`
- `.github/workflows/ci.yml`

در بررسی بعدی، فقط به این فایل اتکا نکن؛ فایل‌های واقعی و commit/CI فعلی را هم دوباره تطبیق بده.

## 13) دستور شروع چت جدید

در چت جدید این جمله را بده:

«ریپوی فقط `alizebal73/2` را بررسی کن. اول `docs/12-chat-handoff-2026-10-02.md`، `docs/10-product-completion-backlog.md` و `docs/11-update-release-architecture.md` را بخوان، سپس head/PR/CI و فایل‌های واقعی را با آن‌ها تطبیق بده. وضعیت را از فایل‌ها حدس نزن. Stage 7 را فقط از commit/CI واقعی تطبیق بده؛ سپس مستقیماً Stage 8 را از branch `stage8-pc-agent-foundation` ادامه بده. هیچ موردی را Done حساب نکن مگر واقعاً تست شده باشد.»

## 14) وضعیت لحظهٔ ثبت این فایل

- تاریخ: 2026-10-02
- main merge commit بعد از Stage 7: `bc4307f73edb0e535f7a57061aace9dac95e3c1e`
- PR #2: **merged**
- Stage 8 branch: `stage8-pc-agent-foundation`
- Stage 7: **Done و merge شده**؛ CI نهایی سبز است.
- Stage 8 Foundation: **Done و با Run #676 سبز تأیید شده**
- Stage 8 verified head: `4ef20a3facf2aaa613e1d20f52bf977f2570fde5`
- مرحلهٔ بعدی: **Stage 9 — Real Client Commands & Kiosk**

## Continuation Update — current Stage 7 head

- Repo: `alizebal73/2` only.
- Branch: `stage7-users-permissions`.
- PR: #2 — Stage 7 real operator auth, users, permissions and approvals.
- Stage 7 current verified state: payroll/employee ledger, salary payment method+receipt, approval flow, wallet refund approval execution, invoice reverse approval, active-domain Read/Write UI hardening, authenticated actor enforcement, and generic approval action allowlist are implemented.
- Run #591 on head `645f2f9a185119f83c9e0f70f432a19ac463d696` passed .NET Build/Test, migration/server smoke, Dashboard lint/build and browser interaction smoke.
- Roadmap was updated after that verification to close Payroll and active-domain permission/approval items; future Mock domains remain mapped to their later real Server/Agent stages.
- Latest documentation head is newer than #591 because the roadmap/PR metadata were updated afterward. A docs-only CI run must be checked before declaring the branch's final CI green.
- Installer is still local-only and has not been published to GitHub.

# Audit Checkpoint — Stages 1–8 — 2026-10-02

این بخش «حقیقت فعلی ممیزی» است و بر یادداشت‌های تاریخی قدیمی‌تر اولویت دارد.

## نتیجه کلی
- Repo مورد بررسی فقط `alizebal73/2`.
- Stage 1 تا Stage 7: بسته، تست‌شده و Stage 7 با PR #2 به `main` merge شده است.
- Stage 8 Foundation: **Done و با CI Run #676 سبز تأیید شد.**
- Current verified Stage 8 head: `4ef20a3facf2aaa613e1d20f52bf977f2570fde5`.
- CI Run #676: Build/Test، Migration/Server Smoke، Dashboard Lint/Build، Preview و Browser Interaction Smoke همگی `success`.
- Stage 8 دیگر شامل Lock/Unlock، Agent-driven Session Start/End، Kiosk/Shell و rollout فیزیکی نیست؛ این‌ها عمداً Stage 9+ هستند.

## ممیزی Stage 1 تا 7
- Stage 1 Foundation: ✅
- Stage 2 Prototype & Behavior Transfer: ✅
- Stage 3 Operational Completion: ✅
- Stage 4 Finance & Session Core: ✅
- Stage 5 Customer & VIP Domain: ✅
- Stage 6 Buffet & Inventory Domain: ✅
- Stage 7 Users/Permissions/Approvals: ✅ Merge شده در `bc4307f73edb0e535f7a57061aace9dac95e3c1e` و با CI نهایی سبز.

## Stage 8 — موارد تأییدشده
- ✅ Agent Device identity و registration token
- ✅ persistent per-device token و restart بدون bootstrap token
- ✅ dedicated `/hubs/agent` و authenticated Agent connection
- ✅ heartbeat، LastSeen، basic telemetry
- ✅ stale presence monitor و online/offline state واقعی Dashboard
- ✅ resilient Agent reconnect و atomic local state
- ✅ persisted `AgentCommands` با Server-side `client.control`
- ✅ SignalR command dispatch و acknowledgement/result persistence با `ping`
- ✅ smoke هم‌زمان چند Agent و unique DeviceId
- ✅ Server-authoritative Session timing
- ✅ Pause/Resume و TimeAdjustment سروری + Migration/Audit
- ✅ Dashboard merge بر اساس Server truth
- ✅ Run #676 سبز روی Build/Test/Server smoke/Dashboard lint-build/E2E

## Stage 8 — موارد عمداً Stage 9+
- ⏩ Lock/Unlock واقعی
- ⏩ Agent-driven Session Start/End
- ⏩ Kiosk/Shell policy و full Client command catalog
- ⏩ Safe Offline/Recovery عملیاتی سطح Client
- ⬜ تست فیزیکی 2–3 PC و rollout 40+ به‌عنوان validation/deployment gate

## تصمیم ورود به Stage 9
Stage 8 Foundation بسته است. Stage 9 از همین merge commit شروع می‌شود و هدفش تبدیل Command Transport موجود به فرمان‌های واقعی Client/Kiosk است؛ اولین برش باید Lock/Unlock واقعی با Permission، persistence، acknowledgement، timeout/failure و audit باشد و سپس Session Start/End از Agent به آن متصل شود.

## یافته‌های ممیزی Cross-Stage
- `Tariff` هنوز در Dashboard برای برخی مسیرها از `mockService.getTariffs()` می‌آید؛ تا Stage واقعی Tariff به‌عنوان قابلیت واقعی Done محسوب نمی‌شود.
- Mockهای Accounts/Games/Tariffs/ClientShell عمداً به Stageهای بعدی منتقل شده‌اند.
- Timeline/UX محلی فقط تا جایی مجاز است که منبع حقیقت مالی/مجوز/دامنه نباشد.
- Release/Update architecture و migration safety باید در تمام Stageهای بعد حفظ شود.

## قاعده ادامه
- هیچ آیتم Stage 9 را قبل از تست واقعی Done علامت نزن.
- Server همچنان منبع حقیقت است.
- هر command: Permission → persistence/correlation → SignalR dispatch → acknowledgement/result → Audit.
- قبل از rollout گسترده، validation فیزیکی 2–3 PC الزامی است.

## یافته‌های Cross-Stage که نباید گم شوند

### 1) Tariff source
Dashboard در حال حاضر برای بارگذاری تعرفه از `mockService.getTariffs()` استفاده می‌کند، در حالی که Session توسط Server ثبت می‌شود و نرخ نهایی در Session سروری قابل ذخیره/اصلاح است.

نتیجه ممیزی:
- این مورد به‌عنوان «فراموش‌شده» حذف نمی‌شود.
- باید به‌عنوان debt معماری در مسیر واقعی‌سازی Tariff Server/Domain ثبت بماند.
- Accounts / Games / Tariffs / ClientShell طبق تصمیم معماری هنوز در Stageهای بعدی هستند و نباید با Mock به‌عنوان قابلیت واقعی Done تلقی شوند.

### 2) Mock UX در مقابل Server authority
در برخی صفحات هنوز state محلی برای Timeline/UX وجود دارد؛ این موضوع فقط تا جایی قابل‌قبول است که منبع حقیقت مالی/مجوز/دامنه نباشد.
هر عملیات مالی یا حساس جدید باید همان مسیر Server → Permission → Domain → Persistence → Audit → SignalR را طی کند.

### 3) Release / Migration
Release Manifest، version contracts و migration safety در معماری ثبت شده‌اند.
Installer هنوز local-only است و نباید وارد GitHub شود.
Updater/Rollback عملیاتی Client در Stageهای بعدی باقی می‌ماند.

## Gate برای ورود به Stage 9 — Current

- ✅ Run #676 سبز و Stage 8 Foundation بسته شده است.
- ✅ Command Transport پایه، persistence و acknowledgement آماده و تست شده‌اند.
- ⏩ Stage 9 اکنون باز است: Lock/Unlock واقعی، Kiosk/Shell و سپس Agent-driven Session Start/End روی همین transport ساخته می‌شوند.
- ⬜ validation فیزیکی 2–3 PC و rollout 40+ همچنان Gate استقرار واقعی است و قبل از rollout گسترده باید انجام شود.

# Audit Checkpoint — Stages 1–9 — 2026-10-02

این بخش حقیقت اجرایی فعلی است؛ بر ادعاهای قدیمی‌تر همین فایل اولویت دارد.

## وضعیت تأییدشده
- Repo: `alizebal73/2` فقط.
- Stage 1 تا 7 بسته و Stage 7 در `main` merge شده است.
- Stage 8 Foundation با Run #676 سبز و merge شده است.
- Stage 9 Lock/Unlock در `main` با Run #690 سبز merge شده است.
- Stage 9 Agent-driven Session Start/End روی branch `stage9-session-agent` با **Run #744 سبز** تأیید شده است.
- آخرین head تأییدشدهٔ این برش: `cbbe511e4689721e4591a1a76e6a7dbe2a6965e0`.

## Stage 9 — چیزهایی که تست واقعی دارند
- ✅ Agent Command transport با persistence/correlation/ack/result
- ✅ Permission `client.control` + Audit
- ✅ Lock/Unlock واقعی و Server-authoritative state
- ✅ timeout/failure handling و heartbeat reconciliation
- ✅ Kiosk policy پایه + LockOnDisconnect
- ✅ Customer Auth وابسته به DeviceId
- ✅ Agent-driven Session Start/End
- ✅ Server-assigned Station و Server-authoritative active Tariff
- ✅ عدم پذیرش HourlyRateOverride/TariffId جعلی از Agent
- ✅ CustomerLogin ownership برای Session End
- ✅ EndAt و billing cap روی Server
- ✅ آزادسازی CustomerLogin بعد از Session End
- ✅ settlement Session Ended از مسیر موجود
- ✅ AgentSessionChanged → Dashboard refresh
- ✅ جلوگیری از دو Agent فعال برای یک Station
- ✅ stale Agent با LockOnDisconnect روی Server قفل می‌شود
- ✅ regressionهای CI برای مرزهای بالا
- ✅ .NET Build/Test + Migration/Server Smoke + Dashboard Lint/Build + Browser Smoke در Run #744

## اصلاحات آخرین دور
- Session Start Agent دیگر قیمت را از Client قبول نمی‌کند؛ Station/Tariff سرور منبع حقیقت است.
- Session End بدون CustomerLogin معتبر همان Device رد می‌شود.
- Station assignment برای Agent فعال unique شده است.
- Presence monitor در stale disconnect Policy قفل را اعمال می‌کند.
- timeout command دوباره fail/audit نمی‌شود.
- خطای کامپایل `DashboardPage` مربوط به destructuring `serverInfo` نیز اصلاح شد.

## هنوز عمداً خارج از برش فعلی
- ⏩ Full Kiosk/Shell و command catalog گسترده
- ⏩ Full customer-facing Client UX برای شروع/پایان جلسه
- ⏩ Safe Offline/Recovery عملیاتی کامل
- ⬜ validation فیزیکی روی 2–3 PC واقعی
- ⬜ rollout کنترل‌شدهٔ 40+ PC
- ⏩ Update/Rollback واقعی و installer publishing در Stageهای بعد

## Debtهای Cross-Stage
- بعضی مسیرهای Dashboard هنوز `mockService.getTariffs()` دارند.
- Accounts/Games/Tariffs/ClientShell تا dependency واقعی آماده نشده نباید Mock-فعال شوند.
- Installer همچنان local-only است.

## تصمیم ادامه
**Stage 10 هنوز باز نمی‌شود.**
ابتدا همین برش Stage 9 باید در `main` merge و وضعیت مستندات ثبت شود؛ سپس فقط با رعایت Gateهای فیزیکی و scope باقی‌ماندهٔ Stage 9 به مرحلهٔ انتشار بعدی می‌رویم.


## Product Improvement Decisions — بعد از ممیزی Stage 1–9

چهار نیاز محصول ثبت شدند و محل اجرای آن‌ها عمداً تفکیک شد تا باعث patchهای پراکنده نشوند:

- **Settings**: بازطراحی Information Architecture و مرتب‌سازی مدرن در Stage 14 / UI Hardening.
- **Permissions**: توسعه از Permission سطح صفحه به Action + Scope + Read/Export + بازهٔ زمانی، با enforce سروری؛ طراحی از الان و اجرای اصلی کنار Reporting/Finance Access.
- **Operator Management**: API واقعی وجود داشت ولی UI ساخت/ویرایش اپراتور ناقص بود؛ یک برش کوچک UI برای «اپراتور جدید» و «ویرایش حساب» در حال تکمیل است.
- **PC Internet Grouping**: گروه‌بندی اینترنت ۱/۲ باید header/line بصری واضح داشته باشد و همراه با Dashboard hardening اجرا شود.
- **Update**: این اصلاحات نباید با installer/Updater عجولانه مخلوط شوند؛ Release/Update در پایان برش پایدار و قبل از rollout واقعی انجام می‌شود.

قاعده: هر نیاز مشابه جدید در ممیزی‌ها ثبت می‌شود و محل اجرای مناسبش تعیین می‌شود؛ patch پراکنده روی UI اصلی ممنوع.
