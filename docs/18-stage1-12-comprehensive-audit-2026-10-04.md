# ممیزی جامع Stage 1 تا 12 — 2026-10-04

## نتیجه اجرایی

این ممیزی بر اساس کد فعلی شاخه `completion/control-20261003`، migration/snapshot، Shared contracts، Server/Dashboard/Client، تست‌ها، Integration Contract و شواهد GitHub Actions انجام شد.

### مبنای evidence

- آخرین head سبز کامل قبل از آخرین integrity hardening: `5faacbd2766d87f62435873697d1428e5f0ebd84`
- CI: Run #1125 روی self-hosted runner `Server`
- Run #1125 روی همان SHA: Build ✅، .NET Test ✅، EF model validation ✅، Server startup/migration/health ✅، Stage 11 Game + Account Pool smoke ✅، Stage 8 Agent foundation smoke ✅، Stage 9 lock/unlock/kiosk/session smoke ✅، Stage 10 update/rollback/recovery smoke ✅، Stage 12 Game Sync → AgentCommand Succeeded ✅، Stage 12 Agent manifest persistence ✅، Stage 12 Session → Game → Agent → Lease → Credential → End/Release ✅، CustomerLogin release / Station Available / activeUsers=0 / settlement=Paid ✅، Dashboard lint/build/preview/browser smoke ✅
- نکته: بعد از این Green، یک hardening جدید برای جلوگیری از race در Session ownership اضافه شد؛ بنابراین head بعدی باید دوباره exact-SHA CI شود و تا آن زمان Done-بودن آن hardening هنوز verified نیست.

# جدول ممیزی 1 تا 12

| Stage | نتیجه فعلی | Evidence / واقعیت | گپ باقی‌مانده |
|---|---|---|---|
| 1 — Foundation | ✅ Verified / ⚠️ Field Gate | ساختار Server/Dashboard/Client/Shared، Build، EF، Startup/Health در CI فعلی | استقرار واقعی و recovery فیزیکی هنوز Gate عمومی محصول است |
| 2 — Prototype & Behavior Transfer | ✅ Historical / ⚠️ Parity Gap | رفتار prototype به Dashboard/Server منتقل شده و Dashboard build/browser smoke سبز است | parity کامل رفتار/ظاهر به‌صورت ماتریس قابل بازسازی و regression مستقل ثبت نشده |
| 3 — Operational Completion | ✅ Server-Truth | Snapshotها، timer سروری، station/customer/session state از Server خوانده می‌شوند | `OperationsPage` هنوز mockService دارد؛ DEV fallback هم وجود دارد. این gap محصول است، نه الگوی Stage 12 |
| 4 — Finance & Session Core | ✅ Verified historically + current smoke coverage | Settlement، Wallet/Refund/Reverse، Pause/Resume/Time Adjustment و Session server truth در مسیر فعلی وجود دارند؛ current CI بخش Settlement را دوباره اجرا می‌کند | هنوز همه سناریوهای مالی با E2E مستقل در یک ماتریس واحد بازاجرا نمی‌شوند |
| 5 — Customer & VIP | ✅ CI Verified | Run #1125: Customer CRUD، VIP، credentials، concurrent login، debt | field proof و سناریوهای گسترده عملیاتی بعداً |
| 6 — Buffet & Inventory | ✅ CI Verified | Run #1125: create/update، purchase/waste/return، sale، low stock، profit، reverse/stock restore | field proof و گزارش‌های عملیاتی گسترده‌تر |
| 7 — Users/Permissions/Approvals | ✅ CI Verified | Run #1125: permission hardening، self-approval guard، invoice reverse approval، wallet refund approval، payroll approval | scopeهای ریزتر مثل amount/time/export هنوز Waveهای آینده |
| 8 — Agent Foundation | ✅ CI Verified | registration، identity persistence، SignalR، heartbeat/offline، multi-Agent، command ACK/result | تست فیزیکی چند PC هنوز باقی است |
| 9 — Agent/Kiosk/Operational Session | ✅ CI Verified | lock/unlock، kiosk/logout-lock، ownership، Agent-driven Session Start/End، duplicate station-Agent guard | process detection/telemetry واقعی و field rollout باقی است |
| 10 — Client Lifecycle | ✅ CI Verified | compatibility، update، rollback، degraded، restart/recovery/healthy state | rollout واقعی و installer/update field gate باقی است |
| 11 — Game + Account Pool | ✅ CI Verified | Game CRUD/archive، Account Pool CRUD/unlock، allocate/release، atomic state | lifecycle عملیاتی Session/Agent/credential متعلق به Stage 12 است |
| 12 — Integration Vertical Slice | ✅ Verified on Run #1125 / ⚠️ New integrity hardening pending CI | CustomerLogin → Session → Game → Lease → Credential → Agent → End → Release؛ Game Sync تا Succeeded؛ secret boundary؛ activeUsers authoritative | hardening جدید active Session uniqueness باید روی head جدید exact-SHA سبز شود |

# زنجیره مرجع که نباید شکسته شود

`CustomerLogin + AgentIdentity → Session → Game → AccountLease → Credential → Agent → Session End → Lease Release → Game.activeUsers`

قوانین لازم برای Stageهای بعد:

1. state owner همیشه Server است.
2. CustomerLogin، AgentDevice، Station و Session باید ownership صریح داشته باشند.
3. Permission و ownership قبل از mutation بررسی می‌شوند.
4. stateهای وابسته در یک transaction واحد تغییر می‌کنند.
5. mutation حساس audit دارد.
6. Agent command ابتدا persist و بعد dispatch می‌شود.
7. `Sent` موفقیت اجرا نیست؛ final state باید از Server خوانده شود.
8. secret هرگز وارد Dashboard/browser نمی‌شود.
9. release و disconnect/stale recovery idempotent است.
10. هر concurrency invariant باید در DB/test هم قابل اثبات باشد.
11. migration + model + snapshot + designer باید همسان باشند.
12. زنجیره جدید نباید lifecycle موازی برای Session/Agent/Lease بسازد.

# Hardening جدیدی که از همین ممیزی استخراج شد

در Agent Session Start قبلی، duplicate guard به `AnyAsync` متکی بود و برای race هم‌زمان تضمین DB-level نداشت.

اصلاح انجام‌شده:
- `Session.CustomerLoginId` در DB persisted است.
- فقط یک Session فعال می‌تواند همان CustomerLogin را داشته باشد.
- فقط یک Session فعال می‌تواند همان Station را داشته باشد.
- این invariant با filtered unique index روی SQLite enforce می‌شود.
- migration جدید: `20261004010000_ActiveSessionOwnershipGuards`
- regression test جدید: `ActiveSessionsCannotShareCustomerLoginOrStation`

این تغییر باید روی exact head جدید دوباره در CI عبور کند.

# Carry-forwardهای واقعی بعد از Stage 12

این موارد نقص پنهان Stage 12 نیستند و محل مشخص دارند:

### Product/UX
- `OperationsPage` هنوز mockService دارد.
- بعضی settings/page locks فقط UX هستند و security boundary نیستند.
- parity کامل Prototype→React هنوز ماتریس regression مستقل ندارد.

### Agent/PC
- Process Detection واقعی هنوز ساخته نشده.
- telemetry/process contract واقعی هنوز نداریم.
- تست فیزیکی 2–3 PC هنوز انجام نشده.

### Data Safety / Release
- Backup/Restore واقعی Server-side کامل نشده.
- DataProtection key backup/restore و restore drill باقی است.
- Installer/package integrity/canary rollout باقی است.

### Operations/Scale
- fine-grained permission scopes برای report/export/amount/time باقی است.
- notification/event queue جامع باقی است.
- reservation/waitlist و recovery workflowهای عملیاتی گسترده باقی است.
- multi-cashier scale هنوز Gate اختصاصی دارد.

# Verdict

### Stage 1 تا 7
هسته محصول از نظر پیاده‌سازی و شواهد تاریخی/CI تثبیت شده است؛ اما field-proven نیست.

### Stage 8 تا 10
Vertical Agent/Client lifecycle در CI واقعی روی self-hosted runner تثبیت شده است؛ physical deployment هنوز Gate است.

### Stage 11
Server-backed Game + Account Pool واقعی و verified است.

### Stage 12
Vertical Slice واقعی و در Run #1125 روی SHA `5faacbd...` سبز و verified شده است. Hardening جدید concurrency بعد از این run اضافه شده و باید exact-SHA دوباره verify شود.

## Gate بعدی

تا سبز شدن exact head بعد از `ActiveSessionOwnershipGuards`:
- Stage 13 شروع نشود.
- merge به main انجام نشود.
- وضعیت Stage 12 به‌صورت «Verified + pending hardening verification» باقی بماند.

بعد از Green شدن:
**Stage 12 از نظر implementation/verification بسته می‌شود و Wave بعدی را فقط با همین Integration Contract شروع می‌کنیم.**