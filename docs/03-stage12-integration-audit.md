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

## موارد خارج از Stage 12 که برای Waves بعدی ثبت شدند

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

### 6. Stage 12 E2E — Verified
Run #1110 این transitionها را به‌صورت end-to-end ثابت کرد: Session Start، CustomerLogin ownership، Game Sync command completion، AccountLease InUse، Session End، CustomerLogin release، Lease Free و Game.activeUsers=0.
Settlement path نیز از مسیر موجود Server smoke شده است و Stage 12 دیگر به unit-test-only evidence متکی نیست.

### 7. Direct service vs lifecycle integration — Verified
ReleaseActiveForSession unit test علاوه بر Agent Session End واقعی در smoke اجرا شده و release واقعی Lease بعد از EndSession مشاهده شده است.

## قرارداد زمانی اجرای Stage 12
Stage 12 فقط وقتی ادامه می‌یابد که تغییر بعدی به یکی از این خانه‌ها تعلق داشته باشد:
1. lifecycle واقعی
2. persistence/concurrency
3. contract/ownership
4. Agent command/credential boundary
5. verification

تغییر صرفاً UI یا workaround CI در این Stage انجام نمی‌شود.

## نتیجه
Stage 12 به‌عنوان Vertical Slice Reference از نظر lifecycle، persistence/concurrency، ownership/contract، Agent credential boundary و verification تأیید شد.

## Decision
هیچ feature جدیدی نباید lifecycle موازی برای Session/Login/Lease/Game ایجاد کند. زنجیره‌های بعدی باید قبل از implementation با Integration Contract ممیزی شوند. گپ‌های mock/UI/realtime/field خارج از Stage 12 در Waveهای بعدی owner خواهند داشت.