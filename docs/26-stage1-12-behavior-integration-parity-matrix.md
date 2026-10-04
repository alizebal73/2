# Stage 1–12 Behavior / Integration Parity Matrix

## معیار
این ماتریس «رفتار تأییدشده محصول فعلی» را ثبت می‌کند؛ معیار، قرارداد مصوب فعلی و مسیر واقعی Server/Client/Dashboard است، نه مقایسه‌ی ظاهری با Mock قدیمی.

| Stage | رفتار/قرارداد | مالک حقیقت | شواهد فعلی |
|---|---|---|---|
| 1 | Build/Restore/Tests/EF/Startup/Health | CI + Server | `.github/workflows/ci.yml` exact-head gate |
| 2 | انتخاب چند ایستگاه با Ctrl/Shift/Drag، Session Center، Persian Error UX، مدیریت اپراتور | Dashboard + Server | `e2e/dashboard-smoke.spec.cjs` |
| 2 | Error state نباید داده Mock جایگزین کند؛ Retry باید action واقعی باشد | Dashboard | Dashboard smoke خطای `/api/dashboard` |
| 3 | Dashboard عملیات روزانه را از serviceهای واقعی می‌خواند | Server API + Dashboard services | Operations/Dashboard/Reports real-service paths |
| 4 | Session pricing/settlement از Server Truth و ledger/invoice می‌آید | Server | `SessionPricingService`, `SessionSettlementService`, settlement tests |
| 5 | Customer/VIP/wallet/debt/free benefits/concurrent login | Server | CustomerLoginService + Wallet/Benefit tests + CI smoke |
| 6 | Buffet sale/inventory/stock adjustment transaction + audit | Server | Program inventory routes + server tests/CI |
| 7 | Authentication/Permission/Approval/Audit | Server | AuthorizationService + Approval tests |
| 8 | Agent identity/heartbeat/reconnect/command persistence | Server + Agent | AgentHub/PresenceMonitor + CI |
| 9 | Lock/Unlock/Kiosk/Logout-lock/Agent-driven Session | Server + Agent | AgentHub/Client flow + CI smoke |
| 10 | Update/Rollback/Watchdog/Health confirmation | Server + Agent | lifecycle contracts/tests + CI |
| 11 | Game Catalog + Account Pool/Lease allocation/release | Server | AccountPoolService + concurrency tests |
| 12 | CustomerLogin → Session → Game → Lease → Credential → Agent → End/Release | Server owns all sensitive state | exact Agent lifecycle smoke + AccountPool tests + Game Sync command status |

## Stage 12 canonical chain

`CustomerLogin + AgentIdentity → Session → Game → AccountLease → Credential → Agent → Session End → Lease Release → Game.activeUsers`

### Required invariants

1. Session باید `CustomerLoginId`, `AgentDeviceId`, `StationId` و در صورت نیاز `GameId` را persist کند.
2. یک CustomerLogin یا Station نمی‌تواند همزمان دو Session فعال معتبر داشته باشد.
3. Credential فقط برای Session فعال و Agent مالک آن، با Login فعال و Game معتبر صادر می‌شود.
4. Secret خام هرگز وارد Dashboard DTO نمی‌شود.
5. Lease allocation اتمیک است؛ release باید با terminal lifecycle سازگار باشد.
6. EndSession و AgentLogoutAndLock نباید Login نامرتبط همان Agent را ببندند.
7. EndSession + Lease Release نباید در دو transaction مستقل commit شوند.
8. `Sent` در AgentCommand موفقیت اجرای Game Sync نیست؛ `Succeeded` و state محلی Agent باید تأیید شود.
9. `Game.activeUsers` از Sessionهای Active بازسازی می‌شود، نه state مستقل.
10. Disconnect/stale Agent باید Leaseهای فعال را آزاد و وضعیت Agent را authoritative تغییر دهد.

## نتیجه

- Stage 1–12 از نظر implementation contract پوشش دارند.
- Stage 2 از نظر behavior contract اکنون ماتریس صریح و قابل ردیابی دارد.
- Stage 1–12 فقط بعد از Green شدن exact latest SHA از نظر verification بسته اعلام می‌شوند.
- Pixel-perfect parity، physical PC validation، installer/field rollout و backup/restore drill در Release/Field gates باقی می‌مانند و با این ماتریس به‌صورت کاذب Done نمی‌شوند.