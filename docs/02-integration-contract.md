# GameNet — Integration Contract & Chain Architecture

این سند از 2026-10-03 مرجع اتصال هر قابلیت/زنجیره جدید به زنجیره‌های موجود است.

## 1. تشخیص واقعی معماری فعلی

کد فعلی سه خانواده مسیر دارد:

### A) مسیر مرجع سروری — الگوی قابل توسعه
- Dashboard/REST → Server
- احراز هویت Server-side
- Permission Server-side
- Validation روی Server
- Transaction وقتی چند state باید با هم اتمیک تغییر کنند
- Persistence در DB
- Audit هم‌زمان با تغییر حساس
- Response فقط بعد از commit
- در صورت نیاز SignalR برای event/command
- Test در سطح domain و E2E

نمونه‌های مرجع: Session + Settlement، Invoice Reverse + Approval، Wallet Refund + Approval، Agent command lifecycle، Account Lease allocation/release.

### B) مسیر Agent
- Agent با DeviceId + Bearer Token هویت می‌گیرد.
- اتصال SignalR باید دوطرفه تأیید شود.
- Server وضعیت online/connection/group را authoritative نگه می‌دارد.
- Heartbeat هم presence و هم group-healing است.
- Command ابتدا در DB ثبت می‌شود، سپس از Group مخصوص Device ارسال می‌شود.
- Agent ACK می‌دهد.
- Update/Rollback دو مرحله acceptance و final health/commit دارند.
- disconnect/stale بخشی از lifecycle معمول و دارای recovery است.

### C) مسیر قدیمی/Mock
در Dashboard هنوز mockService و بعضی صفحات مثل Operations به آن متصل‌اند. این مسیر مرجع معماری جدید نیست و قابلیت جدید نباید از آن الگو بگیرد.

## 2. زنجیره استاندارد

هر feature باید مسیر زیر را داشته باشد:

UI/Agent → Contract → Authentication/Identity → Permission/Ownership → Validation → Transaction/Domain → Persistence → Audit → External/Agent Action → Result → Event/UI

حذف هر خانه باید دلیل فنی مشخص داشته باشد.

## 3. هویت‌ها

Operator: Cookie/Bearer session + DB resolve + Server permission.

Agent: DeviceId header + Bearer AgentToken؛ برای عملیات حساس SignalR از ResolveConnectedDevice استفاده می‌شود.

Customer: CustomerLogin با binding به ClientKey/DeviceId و ConcurrentLoginLimit؛ این هویت با AppUser permission یکی نیست.

Approval Actor: AppUser دوم با approval.decide؛ self-approval ممنوع.

## 4. عبور Permission

Dashboard/Operator: Request → ResolveUser → RequirePermission → Validate → Execute

Agent: Hub authentication → ResolveConnectedDevice → ownership checks → domain action

UI permission فقط UX است و Security boundary نیست.

## 5. Transaction

Transaction برای عملیاتی است که چند state وابسته را اتمیک تغییر می‌دهد.

Session start: Session + Station Occupied + Audit + commit.
Settlement: Invoice + Items + Payments + Wallet/Benefit + Session Completed + Audit + commit.
Approval execution: Approval state + Domain operation + Audit + commit.

Nested transaction ممنوع؛ ownership transaction باید مشخص باشد.

## 6. Persistence

state حساس در DB است. Local Agent state فقط برای recovery/lifecycle محلی است و Server truth را override نمی‌کند.

## 7. Audit

Mutation حساس همراه Action + EntityName + EntityId + Actor + Details در Audit ثبت می‌شود.

## 8. Approval

Approval wrapper اجرای یک domain action حساس است:
Requester permission → ApprovalRequest Pending → Approver approval.decide → self-approval ممنوع → domain operation واقعی → Approved/Rejected → Audit → commit.

Approval نباید فقط UI dialog باشد.

## 9. Agent Command

Command lifecycle معمولی: Requested/Persisted → Sent → Succeeded/Failed

Lifecycle-sensitive: Sent → Accepted/AwaitingHealth → Succeeded/RolledBack/Failed

Sent به معنی «روی PC انجام شد» نیست.

## 10. Event

SignalR event source of truth نیست. DB/Server state source of truth است. Event فقط notification/UI refresh است و read API باید state را قابل بازسازی کند.

## 11. Secret

Credential secret در DB encrypted/persistent است؛ Dashboard نباید secret بگیرد؛ فقط Agent مجاز و در زمان لازم با lease/session binding و expiry.

## 12. Contract

قبل از implementation باید Request/Response/Command/Event DTO، identity key، state machine، error codes، idempotency، retry، persistence owner، audit actions، permission، approval و recovery تعریف شوند.

## 13. State Machine

هر lifecycle باید transition صریح داشته باشد. Transition نامعتبر باید conflict/validation بدهد.

نمونه: Session = Active → Paused → Active → Ended → Completed
نمونه: Command = Sent → Accepted → AwaitingHealth → Succeeded
نمونه: Lease = Active → Released/Cancelled

## 14. Idempotency

release، EndSession، disconnect cleanup، stale cleanup، command acknowledgement، approval decision و finalize باید در retry امن باشند.

## 15. SQLite

SQLite provider بخشی از architecture است. قبل از DateTimeOffset comparison/order یا provider-specific SQL باید translation واقعی بررسی شود.

Storage زمان جدید: API = DateTimeOffset، SQLite = UTC DateTime مگر دلیل فنی مستند.

## 16. Concurrency

Station، CustomerLogin، AccountPool، Wallet، Inventory، Shift و Approval نیازمند race policy هستند.
راهکارها: transaction، concurrency token، atomic update، affected-row check، unique constraint.

## 17. Dashboard

Dashboard فقط Server API را می‌خواند و mutation را به Server می‌سپارد. Event برای refresh است. موفقیت فقط بعد از نتیجه واقعی نمایش داده می‌شود.
mockService مسیر تولیدی feature جدید نیست.

## 18. اتصال زنجیره جدید

Feature جدید باید lifecycle زنجیره‌های قبلی را مصرف کند، نه اینکه Session/Agent/Lease دوم بسازد.

الگوی Stage 12:
CustomerLogin + AgentIdentity → Session → Game → Lease → Credential → Agent → Session End → Lease Release → Game.activeUsers

## 19. Ownership

| State | Owner |
|---|---|
| AppUser session | Server |
| CustomerLogin | Server |
| Station state | Server |
| Session lifecycle | Server |
| tariff result | Server |
| money/wallet | Server |
| Agent presence | Server |
| Agent local lifecycle | Client local + Server mirror |
| Game catalog | Server |
| Agent game manifest | Agent local, generated from Server |
| AccountLease | Server |
| Credential secret | Server encrypted storage → Agent transient |
| Approval | Server |
| Dashboard presentation | Dashboard |

## 20. Integration-complete

هر feature وقتی integration-complete است که contract، owner، permission، transaction boundary، migration، audit، error/retry/idempotency، Agent/Dashboard boundary، تست و update/rollback اثرش مشخص باشد.

## 21. نتیجه برای Stage 12

قبل از ادامه Stage 12 این 9 مورد باید بررسی شوند:
1. Session start/end فقط transition مرجع موجود را مصرف کند.
2. Lease allocation/release دقیقاً به lifecycle Session متصل باشد.
3. Game.activeUsers تنها projection از Session Active باشد.
4. Credential فقط پس از ownership + active login تحویل شود.
5. Game Sync بین request acceptance و command completion تمایز داشته باشد.
6. Dashboard game/account state فقط از Server read شود.
7. reconnect/disconnect/stale cleanup مشخص باشد.
8. تست‌ها invariant واقعی را ثابت کنند نه marker.
9. mock در این زنجیره از product path خارج شود.

این سند الگوی اتصال است؛ قبل از هر feature جدید باید با آن تطبیق انجام شود.