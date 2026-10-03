# Stage 12 — Final Checkpoint (2026-10-03)

## مرجع توسعه
- Branch: `completion/control-20261003`
- PR: #19 — Completion Control
- Main base: `93ba89c9231bd821f45f93ab2aa7b9c25b8cc338`
- آخرین head اجراییِ تأییدشده قبل از این مستندات: `e1c2c78d4e6acb362eb01f03f535370ff6300fc4`

## نتیجه Stage 12

زنجیره مرجع:
`CustomerLogin + AgentIdentity → Session → Game → AccountLease → Credential → Agent → Session End → Lease Release → Game.activeUsers`

Stage 12 روی Run #1110 با head `e1c2c78d4e6acb362eb01f03f535370ff6300fc4` به‌صورت end-to-end عبور کرد.

### مواردی که نهایی و verified شدند
1. `Session.CustomerLoginId` به‌عنوان ownership persisted اضافه شد.
2. migration رسمی `20261003150000_SessionCustomerLogin` و target Designer/snapshot همسان شدند.
3. migration برای legacy active Sessionهای بدون Login، فقط در حالت unambiguous backfill انجام می‌دهد؛ داده مبهم nullable باقی می‌ماند.
4. Agent Session Start، Login واقعی را به Session متصل می‌کند و duplicate active Session برای همان Login را رد می‌کند.
5. EndSession، Agent و CustomerLogin مالک Session را دقیق validate می‌کند و Login اشتباه/نبود Login را رد می‌کند.
6. Credential acquisition/get فقط با Login persisted + Agent + Session + Game معتبر انجام می‌شود و secret خام در Dashboard قرار نمی‌گیرد.
7. Agent logout فقط Login متعلق به همان Session را می‌بندد.
8. Session End و Lease Release در یک transaction قرار گرفتند.
9. Disconnect/stale Agent مسیر release Lease دارد.
10. Game Sync از persist/dispatch تا `AgentCommand = Succeeded` و manifest واقعی Agent اثبات شد.
11. `Game.activeUsers` از Sessionهای Active محاسبه و در E2E کنترل شد.
12. CI race مربوط به observation قبل از EndSession با handshake و guard deterministic شد.
13. Dashboard Game Sync وضعیت `Sent` را موفقیت نهایی فرض نمی‌کند و `RolledBack/Failed` را ناموفق می‌داند.
14. Dashboard build و interaction smoke سبز شد.

## Evidence رسمی

Run #1110:
- .NET Build: Success
- .NET Tests: Success
- EF model/snapshot validation: Success
- Rebuild after EF validation: Success
- Server startup + migration smoke: Success
- Stage 8/9 Agent lifecycle: Success
- Stage 10 update/rollback/recovery: Success
- Stage 11 Game/Account Pool smoke: Success
- Stage 12 Game Sync → AgentCommand Succeeded: Success
- Session Start → Game.activeUsers: Success
- Credential/AccountLease → InUse: Success
- Session End → CustomerLogin release + Lease Free + activeUsers zero: Success
- Dashboard install/lint/build: Success
- Dashboard preview + interaction smoke: Success

## Lessons ثبت‌شده برای زنجیره‌های بعدی
- `CustomerLogin → Session` نباید با inference بر اساس Customer + Device دوباره ساخته شود؛ ownership باید persisted باشد.
- terminal lifecycle و resource release باید transaction boundary مشخص و سازگار داشته باشند.
- `Sent` در command transport هرگز completion نیست.
- migration، snapshot و target model باید سه‌گانه‌ی همسان باشند.
- CI markerها باید state را observe کنند و نباید marker تکراری را دوباره به‌عنوان transition جدید پردازش کنند.
- قبل از هر زنجیره جدید، Integration Contract و owner/permission/transaction/recovery chain باید بررسی شود.

## گپ‌های باقی‌مانده خارج از Stage 12
- OperationsPage هنوز mock-based است و باید در Wave Business/UX به مسیر واقعی Server منتقل شود.
- Agent status realtime هنوز polling است و باید در Wave UX/Realtime تعیین تکلیف شود.
- localStorage page locks امنیت واقعی نیستند و باید فقط به‌عنوان UX در نظر گرفته شوند.
- سیاست یکپارچه زمان/UTC و audit/query compatibility همچنان در Wave Integrity باید ادامه یابد.
- Field proof فیزیکی روی چند PC، rollout واقعی، backup/restore واقعی و installer gate هنوز خارج از این Stage هستند.

## Rule of completion
Stage 12 دیگر blocker زنجیره‌های بعدی نیست و به‌عنوان Vertical Slice Reference ثبت شد.
قابلیت بعدی باید با همان Integration Contract ساخته شود و نباید lifecycle موازی برای stateهای موجود ایجاد کند.

## مسیر بعدی
Stage 13 یا Wave بعدی فقط بعد از re-entry audit اختصاصی همان زنجیره شروع می‌شود؛ Stage 12 دیگر محل افزودن قابلیت جدید نیست مگر برای defect واقعی در همین lifecycle.

## Handoff / lessons ثبت‌شده برای ادامه

### چرا این اصلاحات انجام شدند
- `CustomerLogin → Session` باید persisted ownership باشد، نه query inference بر اساس Customer + Device.
- `EndSession` و `AgentLogoutAndLock` نباید Loginهای نامرتبط همان Agent را ببندند.
- `Session End → Lease Release` باید یک transaction boundary واحد داشته باشد؛ commit شدن Session با Lease باقی‌مانده ممنوع است.
- `Game Sync Sent` فقط پذیرش/ارسال است؛ موفقیت واقعی با `AgentCommand.Status = Succeeded` و state محلی Agent اثبات می‌شود.
- Snapshot و migration باید همزمان با مدل به‌روز شوند؛ relationship بدون navigation metadata نیز gap است.

### Exact latest state
- Latest head at checkpoint write: `533e8812a0627edd9dc1ce46be9d362ad3a6089d`
- PR #19 is open and non-draft.
- CI Run #1094 is executing on self-hosted runner `Server`.
- در آخرین مشاهده: Restore موفق، Build موفق و Test در حال اجرا بوده است.
- Stage 12 تا سبز شدن Run #1094 روی همین SHA و عبور تمام smokeهای Stage 12 Done اعلام نمی‌شود.

### بعد از سبز شدن CI
1. exact-SHA evidence ثبت شود.
2. migration `SessionCustomerLogin` روی DB خالی و upgrade DB smoke تأیید شود.
3. Stage 12 final audit refresh شود.
4. فقط بعد از این، Wave بعدی با Integration Contract شروع شود.
