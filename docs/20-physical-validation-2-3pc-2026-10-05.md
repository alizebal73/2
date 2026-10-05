# Physical Validation — 2–3 PC Gate — 2026-10-05

## هدف

این سند پروتکل رسمی Gate استقرار فیزیکی GameNet Manager است. این Gate باید روی حداقل ۲ رایانه واقعی و ترجیحاً ۳ رایانه در شبکه واقعی فروشگاه اجرا شود. عبور CI به‌تنهایی این Gate را سبز نمی‌کند.

**وضعیت فعلی: 🟡 IN PROGRESS — پروتکل آماده است؛ اجرای فیزیکی هنوز انجام نشده است.**

## مرجع و محدودیت

Server تنها منبع حقیقت است. نتیجه فقط وقتی Pass است که رفتار مشاهده‌شده روی PC، Server، Dashboard و در صورت وجود Audit/Session با هم سازگار باشد.

این تست برای محیط واقعی طراحی شده و نباید با داده یا توکن واقعی مشتریان اجرا شود. یک Customer آزمایشی و یک Agent/Station آزمایشی برای هر PC استفاده شود.

## چیدمان پیشنهادی

| نقش | نمونه |
|---|---|
| Server | PC/Server اصلی GameNet |
| Dashboard/Operator | همان Server یا یک PC مدیریتی |
| Client 1 | PC-01 واقعی |
| Client 2 | PC-02 واقعی |
| Client 3 | PC-03 واقعی، در صورت امکان |
| شبکه | LAN واقعی فروشگاه |
| Agent URL | `http://<SERVER-IP>:5080` |

برای هر PC، `GAMENET_STATION_ID` باید Station همان دستگاه باشد و `GAMENET_AGENT_DEVICE_ID` نباید بین دستگاه‌ها تکراری باشد.

ثبت اولیه Agent از `GAMENET_AGENT_REGISTRATION_TOKEN` انجام می‌شود و پس از ثبت، Token پایدار Agent در state محلی نگهداری می‌شود. مقدار واقعی Token نباید در Git یا این سند ثبت شود.

## پیش‌شرط‌های ورود به تست

- [ ] Main روی همان build مورد آزمایش است.
- [ ] Server از LAN قابل دسترسی است.
- [ ] `/api/health` از شبکه پاسخ 200 می‌دهد.
- [ ] Dashboard با Login واقعی باز می‌شود.
- [ ] حداقل ۲ Station واقعی برای تست ساخته شده‌اند.
- [ ] برای هر Station، Agent فعال و DeviceId یکتا تعریف می‌شود.
- [ ] یک Customer آزمایشی با اعتبار/تعرفه مناسب وجود دارد.
- [ ] یک Tariff واقعی و قابل استفاده وجود دارد.
- [ ] در صورت تست Game/Account، یک Game و یک Account Pool آزمایشی آماده است.
- [ ] Backup قابل Verify قبل از تست در دسترس است.
- [ ] نتیجه‌های قبل از تست ثبت شده و DB واقعی فروشگاه بدون هماهنگی تغییر نمی‌کند.

## قرارداد Agent هر PC

نمونه متغیرهای محیطی روی Client:

```text
GAMENET_SERVER_URL=http://<SERVER-IP>:5080
GAMENET_AGENT_REGISTRATION_TOKEN=<TEMPORARY_TOKEN>
GAMENET_STATION_ID=<STATION_GUID>
GAMENET_AGENT_NAME=PC-01
GAMENET_AGENT_DEVICE_ID=<UNIQUE_DEVICE_ID>
GAMENET_AGENT_DATA_DIR=<LOCAL_AGENT_DATA>
```

بعد از اولین Registration، وجود state پایدار Agent بررسی شود. برای اجرای تکراری تست نباید DeviceId یک PC به PC دیگر کپی شود.

## ماتریس تست اصلی

### 1. Registration و Identity

**ورودی:** Agent جدید روی PC-01 و PC-02.

**انتظار:**
- هر Agent با DeviceId متفاوت در Server ثبت شود.
- Station درست به Agent متصل باشد.
- Dashboard وضعیت Online/Connected را نشان دهد.
- Restart Agent باعث ساخت Identity جدید نشود.

**شواهد:** DeviceId، StationId، Agent status و لاگ ثبت.

### 2. Lock / Unlock

**سناریو:** از Dashboard فرمان Lock و سپس Unlock برای هر PC.

**Pass:**
- فرمان به همان Agent می‌رسد.
- وضعیت authoritative روی Server تغییر می‌کند.
- Client واقعاً Lock و Unlock را اجرا می‌کند.
- وضعیت Dashboard با Client یکسان می‌ماند.
- Audit فرمان قابل مشاهده است.

**Fail:** اگر UI تغییر کند ولی Client یا Server state تغییر نکند، تست Fail است.

### 3. Customer Login و مالکیت Device

**سناریو:** Customer آزمایشی روی PC-01 Login شود و سپس همان Login از PC-02 استفاده نشود.

**Pass:**
- Login به Device/Station درست وابسته است.
- Login متعلق به Agent دیگر پذیرفته نمی‌شود.
- Server مالکیت را enforce می‌کند، نه Client UI.

### 4. Session Start

**سناریو:** Session واقعی از PC-01 شروع شود.

**Pass:**
- Session در Server ساخته شود.
- Station درست به Session متصل باشد.
- CustomerLogin همان Device را داشته باشد.
- Session state در Dashboard به Active برسد.
- تایمر بر اساس Server time حرکت کند.
- تعرفه از Server truth گرفته شود.

### 5. Game / Account Lease (در صورت استفاده)

**سناریو:** برای Session یک Game و سپس Account Pool آزمایشی انتخاب شود.

**Pass:**
- Account از Free به InUse برود.
- Lease به Session/Game/Agent همان PC متصل باشد.
- Account همزمان از PC دیگر قابل تخصیص نباشد.
- پس از پایان Session به Free برگردد.

### 6. Session End و Release

**سناریو:** Session روی PC-01 به‌صورت طبیعی پایان داده شود.

**Pass:**
- Session به Ended/Completed مورد انتظار برسد.
- CustomerLogin آزاد شود.
- Station آزاد شود.
- Leaseهای فعال Session آزاد شوند.
- Dashboard بعد از refresh همان وضعیت Server را نشان دهد.
- Audit مناسب ثبت شده باشد.

### 7. قطع ارتباط Agent

**سناریو:** ارتباط شبکه PC-01 با Server عمداً قطع شود.

**Pass:**
- Server بعد از آستانه heartbeat، Agent را Offline/Stale تشخیص دهد.
- Lifecycle به حالت Degraded/Offline مورد انتظار برود.
- اگر `LockOnDisconnect` فعال است، Client امن قفل شود یا قفل authoritative Server ثبت شود.
- Lease فعال در صورت سناریوی stale release شود.
- Notification/Audit مربوط به Agent offline ایجاد شود.

**اندازه‌گیری:** زمان از آخرین heartbeat تا تشخیص Offline ثبت شود.

### 8. بازگشت ارتباط

**سناریو:** شبکه PC-01 دوباره وصل شود.

**Pass:**
- Agent reconnect کند.
- Server state دوباره Online شود.
- Agent state از Recovering به Running برگردد.
- Dashboard وضعیت جدید را بدون restart اجباری دریافت کند.
- Session فعال نباید به‌صورت تصادفی Duplicate شود.

### 9. دو Agent همزمان

**سناریو:** PC-01 و PC-02 همزمان Online باشند و هر دو Session جداگانه انجام دهند.

**Pass:**
- stateها قاطی نشوند.
- Station و Device ownership مستقل باقی بماند.
- Session یک PC از PC دیگر قابل کنترل نباشد.
- Account Lease همزمان دوباره تخصیص پیدا نکند.

### 10. قطع Server در زمان Session

**سناریو:** Server در حالی که Agent آنلاین است موقتاً از دسترس خارج شود.

**Pass:**
- Client وارد Recovery/Reconnecting شود.
- رفتار امن UI حفظ شود.
- پس از بازگشت Server، Agent دوباره authenticate/confirm شود.
- Server state پس از reconnect authoritative باشد.
- Session duplicated یا orphaned ایجاد نشود.

### 11. Restart خود Agent

**سناریو:** فرآیند Client روی PC-01 بسته و دوباره اجرا شود.

**Pass:**
- DeviceId و Agent identity حفظ شوند.
- Agent دوباره register نشود مگر state/token از بین رفته باشد.
- Server Agent را به همان Station بشناسد.
- وضعیت heartbeat دوباره Running شود.

### 12. Backup / Restore Pre-Production

قبل از هر Restore در محیط واقعی:

- [ ] Backup جدید ایجاد شد.
- [ ] Backup Verify سبز شد.
- [ ] Restore فقط با داده آزمایشی اجرا می‌شود.
- [ ] Safety backup قبل از Apply موجود است.
- [ ] بعد از restart، داده مورد انتظار برگشت.
- [ ] Agent/Session state بعد از Restore دوباره بررسی شد.

## تست 2-PC حداقلی برای قبولی Gate

حداقل مسیر قابل قبول:

`PC-01 Register → Online → Lock → Unlock → Customer Login → Session Start → Timer → Disconnect → Safe Recovery → Reconnect → Session End → Release`

همزمان:

`PC-02 Online مستقل → Session مستقل → بدون cross-device ownership`

سپس باید همین سناریو یک بار دیگر با نقش‌های PC-01/PC-02 جابه‌جا شود تا رفتار به یک دستگاه وابسته نباشد.

## تست 3-PC پیشنهادی

PC-03 به‌عنوان فشار همزمان اضافه شود:

- PC-01 Session فعال
- PC-02 Session فعال
- PC-03 Login/Lock/Unlock
- قطع و وصل یکی از Agentها در حالی که دو دستگاه دیگر فعال‌اند
- بررسی Dashboard و Server truth بعد از هر تغییر

## شواهد لازم برای هر سناریو

برای هر تست این موارد ثبت شوند:

- تاریخ/ساعت
- PC/Station
- DeviceId
- Scenario ID
- اقدام انجام‌شده
- نتیجه Client
- نتیجه Server
- نتیجه Dashboard
- Audit/Notification مرتبط
- Screenshot در صورت خطا
- PASS / FAIL
- توضیح

## معیار خروج

Gate فقط وقتی **✅ PASS** است که:

1. حداقل ۲ PC واقعی بدون cross-device defect کار کنند.
2. Session Start/End و Timer روی Client واقعی درست باشد.
3. Lock/Unlock واقعی و Server-authoritative باشد.
4. Disconnect/Reconnect رفتار امن و قابل پیش‌بینی داشته باشد.
5. Identity Agent بعد از restart پایدار بماند.
6. در سناریوهای همزمان، Session/Lease بین PCها قاطی نشود.
7. Backup/Restore قبل از ورود به داده واقعی Verify شده باشد.
8. هیچ خطای Critical unresolved در Server، Agent یا Dashboard باقی نماند.

## وضعیت فعلی Gate

- 🟢 پروتکل و معیار قبولی: آماده
- 🟡 اجرای روی PCهای واقعی: انجام‌نشده
- ⬜ 2-PC PASS
- ⬜ 3-PC PASS
- ⬜ Release Gate
- ⬜ Controlled 40+ PC rollout

**این سند عمداً Gate را سبز اعلام نمی‌کند؛ شواهد فیزیکی باید از محیط واقعی فروشگاه جمع شود.**
