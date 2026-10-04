# Stage 12 — Final Checkpoint

## مرجع
- Branch: `completion/control-20261003`
- PR: #19 — Completion Control
- Main base: `93ba89c9231bd821f45f93ab2aa7b9c25b8cc338`
- Verified SHA: `c630fafb2c4a216f57ecc52e137f3272b5904124`

## نتیجه
Stage 12 به‌عنوان **Vertical Slice Reference** تکمیل و end-to-end verified شد.

زنجیره مرجع:
`CustomerLogin + AgentIdentity → Session → Game → AccountLease → Credential → Agent → Session End → Lease Release → Game.activeUsers`

## اصلاحات نهایی
1. `Session.CustomerLoginId` به‌عنوان ownership persisted اضافه شد.
2. migration `20261003150000_SessionCustomerLogin` + snapshot/designer همسان شدند.
3. Agent Session Start Login واقعی را به Session متصل می‌کند و duplicate active Session برای همان Login را رد می‌کند.
4. EndSession مالکیت Agent و CustomerLogin را دقیق بررسی می‌کند و Login اشتباه را رد می‌کند.
5. Credential acquisition/get فقط از Login persisted + Agent + Session + Game معتبر عبور می‌کند؛ secret خام وارد Dashboard نمی‌شود.
6. Agent logout فقط Login متعلق به Session همان Agent را می‌بندد.
7. Session End و Lease Release داخل یک transaction انجام می‌شوند؛ Session با Lease گیرکرده commit نمی‌شود.
8. Disconnect/stale Agent مسیر release Lease دارد.
9. Game Sync فقط `Sent` را completion نمی‌داند؛ completion واقعی با `AgentCommand = Succeeded` و manifest واقعی Agent اثبات می‌شود.
10. `Game.activeUsers` از Sessionهای Active محاسبه می‌شود و state موازی ندارد.
11. smoke، duplicate Session، wrong CustomerLogin، Login release، Station release، Lease release و settlement را هم کنترل می‌کند.

## Evidence رسمی
**GitHub Actions Run #1124 — exact SHA `c630fafb2c4a216f57ecc52e137f3272b5904124` — Success**

سبز شد:
- .NET Restore / Build / Test
- EF model/snapshot validation
- Rebuild after EF validation
- Server startup + migration smoke
- Agent lifecycle
- Stage 10 update/rollback/recovery smoke
- Stage 11 Game/Account Pool smoke
- Stage 12 Game Sync → AgentCommand Succeeded
- Game manifest persistence on Agent
- Session Start + CustomerLogin ownership + `Game.activeUsers=1`
- Credential + AccountLease → InUse
- Session End + settlement
- CustomerLogin release
- Station → Available
- AccountLease → Free
- `Game.activeUsers=0`
- Dashboard install/lint/build
- Dashboard preview + interaction smoke

Run روی self-hosted Windows/X64 runner با نام `Server` اجرا شده است.

## درس معماری ثبت‌شده برای زنجیره‌های بعدی
زنجیره جدید **هرگز lifecycle موازی برای state موجود نمی‌سازد**. قبل از implementation باید این مسیر روی chain قبلی تطبیق داده شود:

`UI/Agent → Contract → Identity → Permission/Ownership → Validation → Transaction/Domain → Persistence → Audit → Agent/External Action → Result → Event/UI`

و برای هر زنجیره:
- Owner هر state مشخص باشد.
- Request/Response/Command/Event قرارداد روشن داشته باشد.
- transaction boundary و concurrency strategy مشخص باشد.
- persistence و migration همزمان با model تغییر کند.
- `Sent` یا marker صرفاً به معنی completion تلقی نشود.
- retry/idempotency و recovery از ابتدا لحاظ شود.
- تست transition و invariant باشد، نه فقط اجرای متد.
- بعد از exact-SHA verification، checkpoint ثبت شود.

## گپ‌های خارج از Stage 12
- OperationsPage هنوز mock-based است.
- Agent status realtime هنوز polling است.
- localStorage page locks امنیت واقعی نیستند.
- سیاست یکپارچه UTC/query compatibility باید در Wave Integrity تکمیل شود.
- backup/restore واقعی، rollout چند-PC و installer/field proof هنوز Gateهای محصول‌اند.

## وضعیت
**Stage 12 Done — Reference Vertical Slice approved for future chain design.**

Stage بعدی فقط پس از re-entry audit اختصاصی خودش شروع می‌شود؛ این قرارداد و این chain مرجع اتصال آن خواهد بود.
## Latest audit hardening — 2026-10-04

- `Session.HourlyRateSnapshot` now has explicit EF decimal mapping (`decimal(18,2)`), matching its migration target.
- Reservation index parity was corrected with migration `20261004130000_ReservationIndexParity`.
- The runtime model/snapshot mismatch discovered by exact CI was:
  - stale `IX_Reservations_StationId`
  - stale unique/filtered `IX_Reservations_StationId_StartAt`
  - missing explicit decimal mapping for `HourlyRateSnapshot`
- CI validation was hardened so a native EF nonzero exit cannot be swallowed and diagnostic migration scaffolding uses the correct EF timestamp glob.
- `docs/26-stage1-12-behavior-integration-parity-matrix.md` now records the approved behavior/integration contract for Stages 1–12.
- Latest branch head: `b1708b419e22bcf8fa7732a0314e3d7ee4448c0a`.
- Exact-head CI run #1309 exists but is pending because stale run #1298 is still reported `in_progress` on the self-hosted runner. Do not call Stage 1–12 Verified until #1309 completes successfully.