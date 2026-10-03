# Stage 12 — Current Checkpoint (2026-10-03)

## مرجع توسعه
- Branch: `completion/control-20261003`
- PR: #19 — Completion Control
- Main base: `93ba89c9231bd821f45f93ab2aa7b9c25b8cc338`
- آخرین head ثبت‌شده: `cd77000b3bbd6de7458be6f06001610cc853ae04`

## وضعیت Stage 12

زنجیره هدف:
`CustomerLogin + AgentIdentity → Session → Game → AccountLease → Credential → Agent → Session End → Lease Release → Game.activeUsers`

اصلاحات معماری انجام‌شده:
1. `Session.CustomerLoginId` به مدل اضافه شد.
2. migration رسمی `20261003150000_SessionCustomerLogin` اضافه شد.
3. model snapshot و relationship/navigation همسان شد.
4. Agent Session Start، Login واقعی را در Session persist می‌کند.
5. duplicate active Session برای همان CustomerLogin رد می‌شود.
6. EndSession هم Agent ownership و هم CustomerLogin ownership را دقیق بررسی می‌کند.
7. Credential acquisition/get فقط به Login persisted و Agent/Session/Game binding معتبر پاسخ می‌دهد.
8. Agent logout فقط Login متعلق به همان Session را می‌بندد.
9. Session End + Lease Release در یک transaction قرار گرفته‌اند.
10. Game Sync علاوه بر manifest/marker، lifecycle واقعی `AgentCommand` را هم تا `Succeeded` در CI بررسی می‌کند.
11. smoke جدید duplicate Session و wrong CustomerLogin در EndSession را صریحاً رد می‌کند.
12. AccountPool regression test برای persisted Login و inactive Login اضافه شد.

## وضعیت CI

آخرین اجرای رسمی دیده‌شده قبل از آخرین head روی self-hosted runner در حالت pending بوده است. برای head `cd77000b3bbd6de7458be6f06001610cc853ae04` هنوز اجرای رسمی جدید مشاهده نشده است.

بنابراین:
- Build/Test/EF/Server smoke روی head نهایی هنوز به‌صورت رسمی green اعلام نمی‌شود.
- این موضوع gate است و Stage 12 تا عبور همین head Done اعلام نمی‌شود.

## معیارهای Done باقی‌مانده

1. اجرای CI روی exact final SHA و سبز شدن:
   - Build
   - Tests
   - EF snapshot/model
   - Server startup/migration smoke
   - Stage 8/9 Agent lifecycle
   - Stage 10 update/rollback
   - Stage 12 Game Sync → AgentCommand Succeeded
   - Session Start → activeUsers
   - Credential/Lease
   - Session End → atomic release
   - Dashboard build/smoke
2. بررسی migration `SessionCustomerLogin` روی DB خالی و DB دارای migrationهای قبلی.
3. بعد از سبز شدن exact SHA، بازبینی نهایی diff و ثبت evidence.

## قواعد ادامه

- main مستقیم دستکاری نشود.
- Stage 13 قبل از Done واقعی Stage 12 شروع نشود.
- هیچ marker بدون state/command evidence به معنی موفقیت تلقی نشود.
- هر تغییر بعدی فقط در صورت تعلق به lifecycle/persistence/concurrency/contract/verification این زنجیره باشد.
