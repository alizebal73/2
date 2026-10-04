# ممیزی سراسری Stage 1 تا 12 — نهایی‌سازی 2026-10-05

## نتیجه اجرایی

این ممیزی کد واقعی، قراردادهای Shared، Server/Dashboard/Client، Migration/EF، Agent lifecycle، CI و وابستگی Stageهای آینده را با هم تطبیق می‌دهد.

- Stage 1 تا 7: هستهٔ محصول، Finance/Session، Customer/VIP، Buffet/Inventory و Users/Permissions/Approval بر مبنای شواهد CI تاریخی تثبیت شده‌اند.
- Stage 8: Agent Foundation با هویت، Heartbeat، reconnect، command transport و Server truth تثبیت شده است.
- Stage 9: Command/Kiosk و Agent-driven Session Start/End بر مبنای CI تاریخی تثبیت شده است؛ validation فیزیکی 2–3 PC همچنان Gate استقرار است.
- Stage 10: Update/Rollback/Watchdog/Health با CI تاریخی تثبیت شده است.
- Stage 11: Game Catalog + Account Pool/Lease از Dashboard Mock خارج شده‌اند. Process Detection صریحاً carry-forward است و تا آماده‌شدن telemetry/process contract نباید جعلی شود.
- Stage 12: اتصال Session↔Game↔Agent↔Lease↔Credential، release/recovery، activeUsers و Real Game Apply/Sync روی HEAD نهایی سبز و تأیید شده است؛ Stage 12 از نظر مهندسی نرم‌افزار Done است.

## اتصال‌های کلیدی

1. Customer → CustomerLogin → Session
   - Customer Auth سروری است.
   - CustomerLogin به Device/Agent متصل است.
   - Agent-driven Session مالکیت Login را دوباره در Server کنترل می‌کند.

2. Station → AgentDevice → Session
   - Station منبع حقیقت جایگاه فیزیکی است.
   - Agent registration و duplicate station assignment سروری enforce می‌شود.
   - Session در مسیر Agent-driven دارای AgentDeviceId است.

3. Session → Game
   - GameId روی Session nullable است تا Sessionهای بدون بازی تاریخی/عمومی از کار نیفتند.
   - activeUsers فقط Sessionهای Active دارای GameId را شمارش می‌کند.

4. Session → AccountLease → AccountPoolEntry
   - allocation سروری و transaction-safe است.
   - Lease به Session/Game/Agent محدود شده است.
   - release idempotent است.
   - disconnect/stale heartbeat نیز release را انجام می‌دهد.

5. Credential → Agent
   - SecretHash/SecretCiphertext در Server نگهداری می‌شود.
   - Dashboard DTOها Secret را دریافت نمی‌کنند.
   - credential فقط داخل Agent Hub و در پنجرهٔ کوتاه Lease قابل دریافت است.

6. Game → Agent Apply/Sync
   - endpoint واقعی sync، Permission game.manage و Audit دارد.
   - Server target Agentها را تعیین می‌کند.
   - فرمان با Agent Command persistence + SignalR ارسال می‌شود.
   - Agent یک manifest واقعی محلی و atomic ایجاد می‌کند.
   - هیچ fake-success برای Apply/Sync وجود ندارد.

## اصلاحات انجام‌شده در این ممیزی

- خطای release اشتباه در شروع Session حذف شد.
- nested transaction در AgentLogoutAndLock حذف شد.
- EndSession اکنون Leaseهای فعال را بعد از commit آزاد می‌کند.
- stale Agent نیز Leaseها را آزاد می‌کند.
- Command transport به گروه ثابت per-device منتقل شد تا reconnect race باعث dispatch به ConnectionId قدیمی نشود.
- DataProtection key ring برای بقای credential encryption بین restartها به مسیر ثابت Server منتقل شد.
- dependency اضافی DataProtection پس از مشخص‌شدن namespace واقعی حذف شد.
- CI CustomerAuth قبل از Agent Session smoke restored شد.
- Real Game Apply/Sync contract + Server dispatch + Agent manifest + Dashboard control + E2E verification اضافه شد.
- Backup/Restore جعلی Dashboard غیرفعال شد؛ تا زمانی که Backup واقعی Server-side و recovery test وجود ندارد، UI ادعای موفقیت نمی‌کند.

## گپ‌های واقعی باقی‌مانده

### A. Backup / Recovery
Backup واقعی هنوز پیاده‌سازی نشده است.
- باید Database + DataProtection Keys را پوشش دهد.
- retention، مقصد و schedule باید Server-side باشند.
- restore باید واقعاً قابل‌بازیابی تست شود.
- این Gate قبل از rollout واقعی است و در Stage 15/Release Gate قرار دارد.

### B. Process Detection
هیچ Process Detection واقعی در Client وجود ندارد.
این نقص Stage 11 نیست؛ به telemetry/process contract آینده منتقل شده است.

### C. Fine-grained Permission / Scope
Permissionهای فعلی Action-level هستند؛ Reporting/Export/time-scope/amount-scope باید Server-side در Stage 13 طراحی و enforce شود.

### D. Physical Deployment
تست واقعی روی 2–3 PC و سپس rollout کنترل‌شدهٔ 40+ PC هنوز انجام نشده و باید Gate استقرار باقی بماند.

### E. Notification Queue
اعلان‌های مهم هنوز به یک queue سروری جامع برای expiry/debt/disconnect/command errors متصل نشده‌اند و در Stage 13/14 قرار دارند.

### F. Program.cs Architecture
Program.cs هنوز monolithic است؛ شکستن مرحله‌ای آن برای Stage 15 انجام شود، نه با rewrite ناگهانی.

### G. Repository Governance
GitHub API در این اتصال اجازهٔ خواندن branch protection را نداد و metadata عمومی نیز protected بودن main را نشان نمی‌داد. بنابراین محافظت اجباری main قابل‌تأیید نیست و باید به‌عنوان configuration Gate مستقل بررسی شود.

## نقشهٔ آینده

- Stage 13 — Reporting & Audit
  - Report Center واقعی
  - Audit Explorer
  - fine-grained report/export permissions
  - notification/event queue
  - process/health telemetry consumers

- Stage 14 — Reservations & Operations Scale
  - Reservation/Waitlist
  - Event/Tournament
  - network state / multi-cashier scale
  - operational recovery workflows

- Stage 15 — UI/Deployment Hardening
  - Backup/Recovery واقعی
  - DataProtection key backup/restore
  - UI primitives / DataTable / keyboard-first
  - Desktop Shell
  - Installer
  - Canary / health / release
  - production deployment hardening

## Stage Gate

Stage 12 در HEAD نهایی ae18da81761b82dcea5d11d69934fd47426c7f3d با CI Run #1369 این Gate را سبز کرده است:
Build → Test → EF validation → Server/Migration Smoke → Agent transport → Game Apply/Sync → Session/Game/Lease/Credential E2E → Dashboard Smoke.
بنابراین Stage 12 از نظر نرم‌افزاری بسته است؛ Gateهای فیزیکی/Production همچنان جدا هستند.
