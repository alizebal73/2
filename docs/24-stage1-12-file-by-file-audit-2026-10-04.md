# ممیزی عمیق و اجرایی Stage 1 تا 12 — 2026-10-04

## روش
این ممیزی از روی tree و فایل‌های واقعی شاخه `completion/control-20261003` انجام شده است؛ معیار تصمیم همان Integration Contract است:

`Server Truth → Contract → Identity → Permission/Ownership → Validation → Transaction → Persistence → Audit → Agent/External Action → Result → UI Event`

هیچ Stage فقط به‌خاطر Build سبز «کامل» محسوب نمی‌شود. هر mutation حساس باید مالک state، permission/ownership، transaction، persistence، audit، retry/idempotency و recovery مشخص داشته باشد.

## Inventory
- Repository blobs: 218
- Non-migration source/test/e2e files: 137
- Server source files: 35
- Client source files: 5
- Shared contract/project files: 16
- Dashboard source files/assets: 54
- Test files: 12
- E2E files: 3

تمام فایل‌های source در چهار دسته Server/Client/Shared/Dashboard و سپس Tests/E2E بررسی شدند؛ migrationها و snapshot نیز جداگانه از نظر model parity و schema ownership بررسی شدند.

## Verdict فعلی Stage 1 تا 12

| Stage | وضعیت | مشکل واقعی باقی‌مانده |
|---|---|---|
| 1 Foundation | ⚠️ Software تقریباً تثبیت‌شده | head فعلی باید دوباره Build/EF/Startup را سبز کند؛ field deployment/recovery هنوز gate محصول است |
| 2 Prototype/Behavior Transfer | ⚠️ قابل‌اجرا ولی parity کامل اثبات نشده | regression matrix کامل Prototype→Dashboard وجود ندارد |
| 3 Operational Completion | ✅ Server-truth | UIهای عملیات از مسیر real server عبور می‌کنند؛ mockService production path در tree فعلی حذف شده |
| 4 Finance/Session Core | ✅ هسته verified | full field finance drill هنوز باقی است |
| 5 Customer/VIP | ✅ هسته verified | production seed و برخی field scenarios باید اصلاح/اثبات شوند |
| 6 Buffet/Inventory | ⚠️ sale atomic است، manual stock adjustment هنوز race-safe نیست | adjustment باید transaction + atomic claim داشته باشد |
| 7 Users/Permissions/Approval | ✅ هسته verified | permission scopeهای ریزتر خارج از Stage 1-12 |
| 8 Agent Foundation | ✅ هسته verified | physical multi-PC gate |
| 9 Agent/Kiosk/Session | ⚠️ contract gap | Restart/Shutdown در UI/Agent وجود دارند ولی در Server command support به‌طور کامل هماهنگ نبودند |
| 10 Client Lifecycle | ✅ update/rollback core verified | physical canary/installer gate |
| 11 Game + Account Pool | ✅ server-backed verified | process detection/real game process control خارج از این Stage است |
| 12 Vertical Integration | ✅ architecture درست، head جدید نیازمند exact CI | CustomerLogin persisted، active Session uniqueness، Lease/Credential/release chain درست؛ exact head باید سبز شود |

## مشکلاتی که همین ممیزی آن‌ها را قطعی کرد

### AUDIT-01 — Head فعلی حتی Compile نمی‌شد
فایل‌ها:
- `src/Server/Data/GameNetDbContext.cs`
- `src/Server/Data/Migrations/GameNetDbContextModelSnapshot.cs`
- `src/Server/Program.cs`
- `src/Client/Program.cs`

ریشه:
- رشته filtered index در C# malformed شده بود.
- route کیف پول `catch` بدون `try` داشت.
- heartbeat از `AgentState.DataDirectory` استفاده می‌کرد، درحالی‌که DataDirectory در AgentState مالک آن نیست.

روش درست:
- syntax را از source واقعی اصلاح کنیم، نه workaround CI.
- transaction route را دوباره در try/catch واقعی قرار دهیم.
- DataDirectory را به‌عنوان dependency صریح heartbeat پاس دهیم.

Verification:
- Build exact-SHA.

### AUDIT-02 — Production Seeder هنوز demo customer profile می‌ساخت
فایل: `src/Server/Data/DatabaseSeeder.cs`

ریشه:
- `EnsureCustomerProfilesAsync` قبل از شرط `includeDemoData` برای DB اولیه اجرا می‌شد.
- یک double-if بدون braces نیز readability و maintenance را خراب کرده بود.

روش درست:
- Production فقط reference data + Admin با secret تنظیم‌شده را بسازد.
- Customer/Product/Station demo فقط وقتی `includeDemoData=true`.
- Production بدون `GAMENET_ADMIN_PASSWORD` fail-fast.
- در DB موجود نیز demo profileها نباید silently seed شوند.

Verification:
- production-like seed test
- development demo seed test

### AUDIT-03 — Manual Inventory Adjustment هنوز race-safe نیست
فایل: `src/Server/Program.cs` مسیر `/api/buffet/products/{productId}/stock`

ریشه:
- stock ابتدا read می‌شود و بعد entity تغییر می‌کند.
- فقط buffet sale مسیر atomic `ExecuteUpdate WHERE StockQuantity >= quantity` دارد.
- بنابراین دو adjustment همزمان می‌توانند lost update/oversell ایجاد کنند.

روش درست:
- transaction
- lock/writer acquisition برای همان Product
- validate direction/quantity
- atomic stock claim
- ثبت InventoryTransaction + Audit در همان transaction
- Purchase cost update نیز داخل همان transaction و بر اساس stock واقعی.

Verification:
- concurrent stock adjustment test
- no negative stock
- inventory ledger parity

### AUDIT-04 — Restart/Shutdown command contract کامل نبود
فایل‌ها:
- `src/Shared/Contracts/AgentContracts.cs`
- `src/Server/Program.cs`
- `src/Server/Hubs/AgentHub.cs`
- `src/Client/Program.cs`
- `src/Dashboard/src/pages/ClientShellPage.tsx`

ریشه:
- Client و Dashboard type داشتند.
- Agent handler نیز case داشت.
- اما `AgentCommandTypes.IsSupported` آن‌ها را رد می‌کرد؛ پس UI مسیر واقعی تا Agent نداشت.
- lifecycle command semantics هم final success را قبل از observable health کامل نمی‌کرد.

روش درست:
- command types در Shared یکسان شوند.
- power commands permission جدا (`client.power`) داشته باشند.
- Server command persistence + audit + dispatch همان مسیر استاندارد باشد.
- Restart/Shutdown ابتدا `AwaitingHealth` شوند.
- Agent command id/type در state محلی persist شود و پس از boot/reconnect final result را به Server بدهد.
- UI تا `Succeeded` نگوید؛ برای power command قبل از reboot فقط «درخواست پذیرفته شد / در انتظار health» نمایش دهد.

Verification:
- contract test برای IsSupported
- command persistence/ACK test
- no-success-before-health regression
- CI هرگز Restart/Shutdown واقعی را اجرا نکند.

## مواردی که بررسی شدند و در حال حاضر نیاز به rewrite ندارند

- CustomerLogin acquisition هم‌اکنون transaction + customer writer lock دارد و concurrency test دارد.
- Active Session ownership با unique filtered index برای CustomerLogin و Station enforce شده است.
- Buffet sale atomic claim دارد.
- Tariff CRUD واقعی Server-backed است.
- Global Search از real services می‌خواند؛ mockService production path فعلی ندارد.
- OperationsPage از server services استفاده می‌کند.
- ReportsPage server-backed است.
- Game Sync موفقیت را تا AgentCommand=Succeeded دنبال می‌کند.
- Session→Game→Lease→Credential→Agent→End/Release ownership صریح شده است.
- raw credential secret وارد Dashboard DTO نمی‌شود.
- stale/disconnect lease release وجود دارد.
- update/rollback دارای lifecycle two-phase و health confirmation هستند.

## Carry-forwardهای واقعی که «نقص Stage 1-12 نرم‌افزاری» نیستند
- installer/final setup و نصب جداگانه Server/Client
- physical kiosk/shell proof روی 2–3 PC
- real process detection/telemetry مصرفی بازی
- backup/restore drill نهایی
- canary rollout
- controlled 40+ PC deployment
- full Prototype pixel/behavior parity matrix
- notification queue سراسری
- keyboard/responsive final pass

این‌ها با mock یا checkbox بسته نمی‌شوند؛ field/release evidence لازم دارند.

## قانون اجرای بعدی
1. ابتدا compile blockers.
2. سپس AUDIT-02/03/04.
3. سپس exact-SHA CI.
4. اگر Green شد، Stage 1-12 implementation state را بسته و فقط field gates را carry forward می‌کنیم.
5. هر Stage جدید باید ابتدا همین Integration Contract را audit کند و lifecycle موازی نسازد.

## Inventory path index
### Server
src/Server/Data/AccountPoolService.cs
src/Server/Data/AgentCommandEntity.cs
src/Server/Data/AgentDeviceEntity.cs
src/Server/Data/AgentPresenceMonitor.cs
src/Server/Data/AuthorizationService.cs
src/Server/Data/BackupScheduler.cs
src/Server/Data/BackupService.cs
src/Server/Data/CustomerLoginService.cs
src/Server/Data/DatabaseSeeder.cs
src/Server/Data/DomainEntities.cs
src/Server/Data/EventService.cs
src/Server/Data/GameAccountPoolEntities.cs
src/Server/Data/GameCredentialProtectionService.cs
src/Server/Data/GameNetDbContext.cs
src/Server/Data/InvoiceReverseService.cs
src/Server/Data/OperationsService.cs
src/Server/Data/PasswordSecurity.cs
src/Server/Data/ReportingService.cs
src/Server/Data/ReservationService.cs
src/Server/Data/SessionPricingService.cs
src/Server/Data/SessionSettlementService.cs
src/Server/Data/SessionTiming.cs
src/Server/Data/StationEntity.cs
src/Server/Data/WalletRefundService.cs
src/Server/EventEndpoints.cs
src/Server/GameNetManager.Server.csproj
src/Server/GameNetManager.Server.http
src/Server/Hubs/AgentHub.cs
src/Server/Hubs/DashboardHub.cs
src/Server/Program.cs
src/Server/Properties/launchSettings.json
src/Server/ReservationEndpoints.cs
src/Server/appsettings.Development.json
src/Server/appsettings.Production.json
src/Server/appsettings.json

### Client
src/Client/AgentLockScreen.cs
src/Client/ClientUpdateCommandParser.cs
src/Client/ClientUpdateManager.cs
src/Client/GameNetManager.Client.csproj
src/Client/Program.cs

### Shared
src/Shared/Contracts/AccountPoolOperationsContracts.cs
src/Shared/Contracts/AgentContracts.cs
src/Shared/Contracts/ClientLifecycleContracts.cs
src/Shared/Contracts/CustomerContracts.cs
src/Shared/Contracts/DashboardSnapshotDto.cs
src/Shared/Contracts/EventContracts.cs
src/Shared/Contracts/GameAccountPoolContracts.cs
src/Shared/Contracts/ManagementReportContracts.cs
src/Shared/Contracts/OperationsContracts.cs
src/Shared/Contracts/ReportingContracts.cs
src/Shared/Contracts/ReservationContracts.cs
src/Shared/Contracts/ServerInfoDto.cs
src/Shared/Contracts/StationDto.cs
src/Shared/Contracts/TariffContracts.cs
src/Shared/Contracts/WalletLedgerContracts.cs
src/Shared/GameNetManager.Shared.csproj

### Dashboard
src/Dashboard/src/App.css
src/Dashboard/src/App.tsx
src/Dashboard/src/assets/hero.png
src/Dashboard/src/assets/react.svg
src/Dashboard/src/assets/vite.svg
src/Dashboard/src/components/ApprovalDialog.tsx
src/Dashboard/src/components/ReverseDialog.tsx
src/Dashboard/src/components/SectionLockDialog.tsx
src/Dashboard/src/components/TopNavigation.tsx
src/Dashboard/src/components/UserErrorBanner.tsx
src/Dashboard/src/features/attention/DashboardAttentionSidebar.tsx
src/Dashboard/src/features/client/ClientExperience.css
src/Dashboard/src/features/client/ClientExperience.tsx
src/Dashboard/src/features/search/GlobalCommandCenter.tsx
src/Dashboard/src/features/session/SessionCenter.tsx
src/Dashboard/src/index.css
src/Dashboard/src/main.tsx
src/Dashboard/src/pages/AccountsPage.tsx
src/Dashboard/src/pages/BuffetPage.tsx
src/Dashboard/src/pages/ClientShellPage.tsx
src/Dashboard/src/pages/CustomersPage.tsx
src/Dashboard/src/pages/DashboardPage.tsx
src/Dashboard/src/pages/GamesPage.tsx
src/Dashboard/src/pages/LoginPage.tsx
src/Dashboard/src/pages/OperationsPage.tsx
src/Dashboard/src/pages/ReportsPage.tsx
src/Dashboard/src/pages/SettingsPage.tsx
src/Dashboard/src/pages/TariffsPage.tsx
src/Dashboard/src/pages/UsersPage.tsx
src/Dashboard/src/services/accountPoolService.ts
src/Dashboard/src/services/agentService.ts
src/Dashboard/src/services/authService.ts
src/Dashboard/src/services/backupService.ts
src/Dashboard/src/services/billingEngine.ts
src/Dashboard/src/services/buffetService.ts
src/Dashboard/src/services/clientIdentityService.ts
src/Dashboard/src/services/customerAuthService.ts
src/Dashboard/src/services/customerLoginService.ts
src/Dashboard/src/services/customerService.ts
src/Dashboard/src/services/dashboardAdapter.ts
src/Dashboard/src/services/financeService.ts
src/Dashboard/src/services/freeBenefitService.ts
src/Dashboard/src/services/gameService.ts
src/Dashboard/src/services/operationsService.ts
src/Dashboard/src/services/payrollService.ts
src/Dashboard/src/services/reportService.ts
src/Dashboard/src/services/securityService.ts
src/Dashboard/src/services/sessionService.ts
src/Dashboard/src/services/shiftService.ts
src/Dashboard/src/services/tariffService.ts
src/Dashboard/src/services/vipPackageService.ts
src/Dashboard/src/services/walletLedgerService.ts
src/Dashboard/src/types.ts
src/Dashboard/src/utils/userError.ts

### Tests
tests/Client.Tests/ClientUpdateManagerTests.cs
tests/Client.Tests/GameNetManager.Client.Tests.csproj
tests/Server.Tests/AccountPoolTests.cs
tests/Server.Tests/AuthorizationTests.cs
tests/Server.Tests/BackupServiceTests.cs
tests/Server.Tests/ConcurrencyTests.cs
tests/Server.Tests/GameNetManager.Server.Tests.csproj
tests/Server.Tests/InvoiceReverseTests.cs
tests/Server.Tests/PersistenceModelTests.cs
tests/Server.Tests/SessionSettlementTests.cs
tests/Server.Tests/Stage131415Tests.cs
tests/Server.Tests/UnitTest1.cs

### E2E
e2e/dashboard-smoke.cjs
e2e/dashboard-smoke.spec.cjs
e2e/playwright.config.cjs
