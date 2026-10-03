# GameNet — Stage 12 Re-entry Architecture Audit

## هدف
این گزارش Stage 12 را از روی Integration Contract بررسی می‌کند تا قبل از ادامه کدنویسی، زنجیره جدید دقیقاً به lifecycleهای موجود متصل شود.

## زنجیره مرجع
CustomerLogin + AgentIdentity → Session → Game → AccountLease → Credential → Agent → Session End → Lease Release → Game.activeUsers

## نقاطی که هم‌اکنون با الگوی مرجع هم‌راستا هستند

1. Agent هویت خود را با DeviceId + Bearer Token ثابت می‌کند.
2. اتصال SignalR با ConfirmConnection تأیید می‌شود.
3. Heartbeat وضعیت Agent را persistent می‌کند و group را heal می‌کند.
4. Command قبل از ارسال در AgentCommand ذخیره می‌شود.
5. Session Agent-side مالکیت Agent، Station و CustomerLogin را validate می‌کند.
6. Game.activeUsers از Sessionهای Active محاسبه می‌شود و state مستقل جداگانه ندارد.
7. Lease allocation دارای atomic claim است.
8. Secret خام به Dashboard DTO وارد نمی‌شود.
9. Disconnect و stale Agent مسیر release دارند.
10. Session End اکنون Session/Login/Station و Lease Release را در یک transaction هماهنگ می‌کند؛ در خطای release، EndSession commit نمی‌شود.

## شکاف‌های معماری که باید قبل از اعلام Done اصلاح/تعیین تکلیف شوند

### 1. Game Sync semantics
Endpoint sync درخواست را persist و dispatch می‌کند و response آن Sent/Failed برای dispatch است، نه اثبات اجرای موفق روی Agent.
این semantics قابل قبول است، اما UI باید آن را «ارسال شد» بداند و برای completion از AgentCommand status بخواند.

### 2. Dashboard live Agent events
DashboardHub فعلاً ServerReady و AgentSessionChanged را مصرف می‌کند؛ وضعیت Agentها در Dashboard هنوز با polling پنج‌ثانیه‌ای خوانده می‌شود.
این الزاماً correctness bug نیست، ولی event propagation کامل نیست و باید در Wave UX/Realtime تعیین تکلیف شود.

### 3. Mock path خارج از Stage 12
OperationsPage هنوز مستقیماً از mockService استفاده می‌کند و mockService مجموعه بزرگی از stateهای fake دارد.
این مشکل Stage 12 نیست، اما در تعریف Field-ready محصول یک gap واقعی است و نباید الگوی feature جدید شود.

### 4. Local-only page locks/settings
بعضی lockهای Dashboard در localStorage هستند و خود SettingsPage تصریح می‌کند که UX-only هستند.
این لایه برای UI قابل استفاده است ولی permission امنیتی نیست؛ action حساس باید Server permission داشته باشد.

### 5. Query/time consistency
مدل پروژه هنوز DateTimeOffset فراوان دارد. در Stage 12 ثابت شد که SQLite برای بعضی comparison/orderها مشکل ترجمه دارد.
برای هر query جدید باید provider translation بررسی شود و timestampهای جدید طبق policy Integration Contract طراحی شوند.

### 6. Stage 12 E2E
تست E2E باید برای هر transition این invariantها را ثابت کند:
- بعد از Session Start: Session Active، Station Occupied، Game.activeUsers=1، CustomerLogin فعال، Lease هنوز Free تا زمان Acquire.
- بعد از Acquire: Lease InUse و credential فقط در Agent مسیر.
- بعد از Session End: Session Ended، Station Available، CustomerLogin released، Lease Free.
- بعد از Settlement: Session Completed و Invoice Paid.
- بعد از disconnect/stale: leaseهای Agent آزاد می‌شوند.

### 7. Direct service vs lifecycle integration
Unit test مستقیم ReleaseActiveForSession کافی نیست. باید Agent Session End واقعی نیز release را ثابت کند. این invariant در E2E باید باقی بماند.

## قرارداد زمانی اجرای Stage 12
Stage 12 فقط وقتی ادامه می‌یابد که تغییر بعدی به یکی از این خانه‌ها تعلق داشته باشد:
1. lifecycle واقعی
2. persistence/concurrency
3. contract/ownership
4. Agent command/credential boundary
5. verification

تغییر صرفاً UI یا workaround CI در این Stage انجام نمی‌شود.

## مرحله بعد
ابتدا semantics و invariantهای Stage 12 تکمیل می‌شوند، سپس Game Sync completion/read path و test evidence آن اصلاح می‌شود. پس از آن Stage 12 دوباره از ابتدا تا انتها با Integration Contract اجرا می‌شود.

## Decision
تا تکمیل این audit، هیچ قابلیت Stage بعدی نباید به Stage 12 متصل شود. Stage 12 باید یک vertical slice مرجع برای نحوه اتصال featureهای آینده باشد.