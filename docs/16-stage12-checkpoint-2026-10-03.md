# Stage 12 — Final Certification Checkpoint 2026-10-05

## Active branch

repair/stages1-12-integrity-20261005

## Stable main

main = cc5304a1c6642ce0061a3155f0dbea9a51453067

## Certified branch head

ae18da81761b82dcea5d11d69934fd47426c7f3d

## Final CI

- Run #1369 — success
- Build ✅
- .NET Tests ✅
- EF model validation ✅
- Server startup / migration / health smoke ✅
- Stage 8 Agent transport ✅
- Stage 9 Lock/Kiosk/Session smoke ✅
- Stage 10 Update/Rollback/Recovery smoke ✅
- Stage 11 Game/Account Pool smoke ✅
- Stage 12 real Game Apply/Sync ✅
- Stage 12 Session → Game → Agent → Account Lease → Credential → Session End → Release ✅
- Customer/VIP/Concurrent Login smoke ✅
- Dashboard install/lint/build ✅
- Dashboard browser smoke: 3/3 ✅

## Certification result

**Stage 12 — DONE از نظر مهندسی نرم‌افزار.**

مسیر واقعی تأییدشده:

Customer → CustomerLogin → Station → Agent → Session → Game → AccountLease → Credential → Agent → Session End → Lease Release

- Game Apply/Sync روی Agent مانیفست محلی واقعی ایجاد می‌کند.
- activeUsers از Sessionهای authoritative محاسبه می‌شود.
- Account Pool از Free به InUse و پس از پایان Session دوباره به Free برمی‌گردد.
- Settlement از مسیر موجود Server انجام شد و عدد آن با settlement preview همان Session تطبیق داده شد.
- Dashboard پس از این تغییرات build و browser smoke را پاس کرد.

## Remaining release / production gates

- Backup/Restore واقعی دیتابیس + DataProtection Keys
- Validation فیزیکی روی 2–3 PC واقعی
- rollout کنترل‌شدهٔ 40+ PC
- Fine-grained Permission/Scope، Reporting/Audit و Notification Queue در Stage 13
- Process Detection واقعی در مسیر telemetry آینده

## Recovery rule

اگر کار قطع شد، از همین checkpoint و commit بالا ادامه بده. وضعیت را از CI/کد بخوان، نه از checkpointهای قدیمی‌تر.

## Do not do

- main را برای ادامهٔ Stage 12 دستکاری نکن.
- CI assertionها را برای سبز کردن مصنوعی ضعیف نکن.
- Real Game Apply/Sync را دوباره fake نکن.
- Legacy GameAccount را حذف نکن.
- Gateهای فیزیکی/Production را به‌جای انجام واقعی Done علامت نزن.

## Next software stage

**Stage 13 — Reporting & Audit**
با محورهای: Report Center، Audit Explorer، Fine-Grained Permission/Scope، Notification/Event Queue و مصرف telemetry واقعی.
