# Action Map نهایی — رفتار عملیاتی GameNet Manager

> تاریخ: ۱۴۰۵/۱۰/۱۰
>
> این سند قرارداد رفتاری UI و Backend است. قبل از هر بازطراحی یا افزودن دکمه، باید رفتار آن در این سند مشخص باشد.
>
> منابع تحقیقاتی اصلی: مستندات و صفحات رسمی iCafeCloud درباره Billing، Pricing، POS/Top-up، Reports، License Pool و Booking. این منابع برای مقایسه الگوهای عملیاتی استفاده شده‌اند؛ قرارداد نهایی این پروژه بر اساس نیاز GameNet-98 نوشته شده است.

## 1. اصول غیرقابل تغییر

1. Dashboard مرکز عملیات روزانه است؛ اپراتور برای عملیات پرتکرار نباید بین چند صفحه جابه‌جا شود.
2. Server تنها منبع حقیقت است؛ UI فقط Command/Query ارسال می‌کند.
3. هر عملیات مالی باید تراکنش پایدار و Audit داشته باشد.
4. هر عملیات حساس باید Permission سمت Server داشته باشد؛ مخفی‌کردن دکمه به‌تنهایی Permission نیست.
5. هر عملیات قابل برگشت باید مسیر Cancel/Refund/Reverse تعریف‌شده داشته باشد.
6. مقدار محاسبه‌شده، تخفیف، رند و مبلغ نهایی جدا ذخیره شوند.
7. Session، Customer، Wallet، Invoice و Shift موجودیت‌های جدا اما مرتبط‌اند.
8. قابلیت نمایشی یا Mock نباید در UI نهایی به‌عنوان عملیات واقعی وانمود شود.
9. هر قابلیت فقط یک مالک اصلی در Navigation دارد.
10. OperationsPage فقط تا پایان مهاجرت Stage 1B ابزار QA است و مقصد نهایی UI نیست.

## 1.1 قانون زبان رابط و پیام‌ها

تمام محتوای قابل مشاهده برای کاربر باید فارسی و قابل‌فهم باشد:
- نام دکمه‌ها، منوها، برچسب‌ها، فرم‌ها و عملیات
- اعلان‌ها، Toastها، هشدارها، تأییدیه‌ها و پیام‌های موفقیت
- خطاهای اعتبارسنجی، خطاهای شبکه، خطاهای Server/Agent و پیام‌های Permission
- Empty State، وضعیت‌های اتصال، راهنمای میانبرها و متن‌های Modal
- متن‌های اپراتوری Dashboard و Client باید فارسی باشند.

استثنا فقط برای نام واقعی بازی‌ها، برندها، نام فایل/مسیر، IP/Domain، شناسه‌های فنی و مقادیر یا داده‌ای است که ذاتاً باید به همان شکل نمایش داده شود. اصطلاح فنی در صورت نمایش به کاربر باید معادل فارسی روشن داشته باشد و در صورت نیاز نام فنی در کنار آن بیاید.

هیچ خطای خام Backend/Browser/HTTP/Exception نباید مستقیم به کاربر نمایش داده شود؛ Server/Client باید آن را به پیام فارسی قابل‌فهم تبدیل کند.

## 2. ناوبری نهایی

- داشبورد
- مشتریان
- بوفه
- تعرفه‌ها
- بازی‌ها
- کلاینت‌ها
- اکانت‌ها
- گزارش‌ها
- کاربران و شیفت
- تنظیمات

«مدیریت» منوی مستقل نیست.

## 3. منوی راست‌کلیک ایستگاه

### گروه جلسه
- شروع جلسه
- توقف موقت (Pause)
- ادامه جلسه (Resume)
- تمدید زمان
- کاهش زمان
- تغییر تعرفه/نفرات در جلسه
- انتقال جلسه
- پایان و تسویه
- خارج از سرویس

### گروه مشتری و مالی
- مشاهده پروفایل مشتری
- شارژ کیف پول
- ثبت بدهی
- اعتبار مالی رایگان
- زمان رایگان
- افزودن بوفه به جلسه
- مشاهده فاکتور جاری
- مشاهده تراکنش‌های جلسه

### گروه کلاینت
- ارسال پیام
- Screenshot
- قفل/خروج مشتری
- ورود با شناسه
- تغییر اینترنت ۱/۲
- قطع/وصل اینترنت
- Restart Shell
- Restart Windows
- Shutdown
- تنظیمات کلاینت

عملیات خطرناک مانند Shutdown و تغییرات شبکه باید تأیید و Permission مناسب داشته باشند.

## 4. چرخه Session

### Start
ورودی:
- stationId
- customerId یا Guest
- tariffId/روش تعیین تعرفه
- sessionMode: prepaid/postpaid/member/offer/free
- نفرات
- مبلغ یا مدت در حالت prepaid
- اپراتور/کاربر جاری

خروجی:
- sessionId
- startAt
- resolved tariff
- initial balance/remaining minutes

### Pause
- زمان صورتحساب متوقف می‌شود.
- زمان Pause جزو زمان قابل‌صورتحساب نیست.
- Audit ثبت می‌شود.
- Session بسته نمی‌شود.

### Resume
- Session از حالت paused خارج می‌شود.
- سیاست قیمت‌گذاری Resume از موتور Billing خوانده می‌شود.

### Extend / Reduce
هر دو عملیات باید پشتیبانی شوند.
- extendMinutes مثبت
- reduceMinutes مثبت
- دلایل/اپراتور/زمان در Audit
- نتیجه در Billing و Session history

### Transfer
- مبدأ و مقصد
- حفظ Session و سابقه آن
- ثبت دو طرف عملیات در Audit
- مقصد باید از نظر state و type مجاز باشد.

### End/Checkout
محاسبه نهایی باید جداگانه نگه‌داری شود:
- rawAmount
- package/free time consumption
- wallet/debt effects
- discount
- rounding
- buffet
- finalAmount
- paymentMethod
- receivedAmount/change

## 5. موتور تعرفه و قیمت

تعرفه نباید صرفاً «قیمت ساعتی + قیمت شب» باشد.

مدل نهایی:
- Station Group
- Customer Tier
- Day of Week
- Time Slot
- Price Band
- Minimum Charge
- Rounding Policy
- Prepaid Policy
- Postpaid Policy
- Package/Offer Policy

قیمت باید در زمان شروع/تغییر وضعیت جلسه Resolve شود و نتیجه Resolve در Billing Event قابل مشاهده باشد.

نمونه:
PC + VIP + شنبه + ۱۸:۰۰ تا ۲۲:۰۰ → Price Band X

تغییرات قیمت آینده نباید فاکتور گذشته را تغییر دهند.

## 6. حالت‌های پرداخت و Session

چهار حالت اصلی:
- Prepaid
- Postpaid
- Member/Wallet
- Offer/Package

یک حالت پنجم لازم است:
- Free Time

Free Time باید همچنان Session واقعی داشته باشد تا در گزارش و Audit دیده شود.

## 7. Wallet و Ledger

موجودی کیف پول فقط یک عدد نیست.

هر تغییر باید Ledger Entry داشته باشد:
- id
- customerId
- amount
- direction
- type
- source
- sessionId?
- invoiceId?
- paymentMethod?
- operatorId
- createdAt
- reference
- note

انواع پایه:
- TopUp
- SessionCharge
- BuffetCharge
- PackagePurchase
- GiftMoney
- Refund
- ManualAdjustment
- DebtSettlement

موجودی = Sum Ledger معتبر.

## 8. زمان رایگان و اعتبار مالی رایگان

دو مفهوم جدا:

### Free Money
اعتبار پولی هدیه.

### Free Time
دقیقه/ساعت هدیه.

هر دو باید:
- دلیل
- اپراتور
- زمان
- مشتری
- مرجع/جلسه
داشته باشند.

## 9. Customer Profile

پروفایل نهایی باید حداقل این بخش‌ها را داشته باشد:

### مشخصات
کد، نام، لقب، موبایل، کد ملی، username، status.

### مالی
Wallet، Debt، Free Money، Free Time.

### VIP/Package
Package، tier، باقی‌مانده کل، مصرف امروز، سقف روزانه، تاریخ انقضا، قواعد زمان مازاد.

### Session History
جلسات، ایستگاه، مدت، تعرفه، مبلغ و وضعیت.

### Financial Ledger
شارژ، برداشت، بدهی، Refund، هدیه و پرداخت.

### Access
Password/PIN، وضعیت فعال/مسدود، محدودیت Login هم‌زمان.

### Actions
ویرایش، شارژ، بدهی، هدیه، VIP/Package، تغییر رمز، مسدود/فعال، مشاهده Ledger.

حذف فیزیکی مشتری عملیات عادی نیست؛ Deactivate/Block مسیر اصلی است.

## 10. Refund / Reverse

Refund باید یک قابلیت واقعی باشد، نه حذف رکورد.

Refund می‌تواند برای:
- Session
- Buffet Order
- Wallet TopUp
- Package
رخ دهد؛ مشروط به Permission.

هر Refund یک سند جدید با Reference به تراکنش قبلی ایجاد می‌کند و تراکنش اصلی حذف نمی‌شود.

## 11. POS / Settlement

مودال تسویه باید یک جمع‌بندی کامل نمایش دهد:

- زمان بازی
- تعرفه و دلیل انتخاب آن
- مصرف Package/Free Time
- بوفه
- تخفیف
- رند
- مبلغ نهایی
- Wallet
- Debt
- مبلغ دریافتی
- مبلغ برگشتی
- روش پرداخت

روش‌های پایه:
- Cash
- Card
- Wallet
- Debt
و در آینده قابلیت QR/درگاه بدون بازطراحی مدل مالی.

## 12. Shift / Cash Register

بستن شیفت باید Summary Modal داشته باشد، نه prompt.

Summary:
- Opening Cash
- Session Sales
- Buffet Sales
- TopUps
- Refunds
- Discounts
- Expenses
- Expected Cash
- Counted Cash
- Difference

بعد از Close:
- Shift قفل مالی می‌شود.
- اصلاحات فقط با Permission/Audit.

## 13. Buffet

### فروش
- سبد
- مشتری/جلسه/فروش مستقل
- مقدار
- تخفیف در صورت مجاز
- پرداخت
- ثبت Order

### چرخه Order
Draft → Confirmed → Paid/OnAccount → Cancelled/Refunded

### موجودی
- خرید/ورودی
- فروش
- اصلاح موجودی
- ضایعات
- برگشت
- حداقل موجودی
- تاریخچه تغییر موجودی

دکمه «+ محصول جدید» باید واقعی باشد؛ وضعیت Mock/پیام نمایشی در نسخه نهایی مجاز نیست.

## 14. Reservation / Waitlist

رزرو و صف مالکیت Dashboard را دارند.

رزرو:
- مشتری
- ایستگاه/نوع
- شروع
- مدت
- ظرفیت
- وضعیت
- Cancel/No-show

صف:
- مشتری
- نوع ایستگاه
- زمان ورود به صف
- اولویت
- تخصیص
- لغو

این قابلیت‌ها نباید باعث ایجاد «مرکز مدیریت» مستقل شوند.

## 15. Client Management

عملیات معتبر:
- Online/Offline
- Session/User/Game
- Message
- Screenshot
- Lock/Logout
- Switch Internet
- Toggle Internet
- Restart Shell
- Restart Windows
- Shutdown
- Settings
- Group Actions

عملیات Group باید دقیقاً همان Command را به چند Client ارسال کند و نتیجه هر Client جدا قابل ردیابی باشد.

Remote فقط زمانی در UI نهایی نمایش داده شود که Agent واقعاً آن را پشتیبانی کند.

## 16. Game Library

ضروری:
- Name
- Version
- Category
- Install Path
- Executable
- Launch Args
- Connection Type
- Active/Hidden
- Target Client Group
- Target VIP/Normal
- Apply/Sync
- Process Detection در صورت License Pool

اختیاری:
- Cover

فعلاً غیرضروری:
- Trailer
مگر اینکه در Client UI واقعاً مصرف مشخص داشته باشد.

## 17. Account Pool

برای هر Lease:
- accountId
- gameId
- clientId
- sessionId
- assignedAt
- releasedAt
- releaseReason
- login result

حالت‌ها:
Free → Reserved → InUse → Released / Locked

Password هرگز به Client/Customer UI ارسال نشود.

## 18. Reports / Audit

گزارش مالی باید حداقل:
- Session Sales
- Buffet Sales
- Package Sales
- TopUps
- Refunds
- Discounts
- Expenses
- Cash
- Card
- Wallet
- Debt
- Shift
را پوشش دهد.

Audit باید:
- actor
- action
- target
- before/after در عملیات حساس
- timestamp
- source/client
- reference
را نگه دارد.

## 19. Permission

Permissionهای هسته:
- session.start
- session.pause
- session.resume
- session.extend
- session.reduce
- session.transfer
- session.checkout
- customer.view
- customer.edit
- wallet.topup
- wallet.adjust
- debt.add
- gift.money
- gift.time
- refund.create
- discount.apply
- tariff.manage
- buffet.sell
- inventory.adjust
- shift.open
- shift.close
- report.view
- audit.view
- client.control
- client.settings
- network.switch
- game.manage
- account.manage
- user.manage
- settings.manage
- recovery.manage

تخفیف، Refund، Manual Adjustment، Shutdown و Recovery باید Permission جدا داشته باشند.

## 20. قابلیت‌های فعلی که باید حذف/ادغام/مشروط شوند

### حذف از Navigation
- مرکز مدیریت / مدیریت

### فعلاً پنهان یا حذف مگر با نیاز واقعی
- Trailer بازی
- CCBOOT
- PXE
- Remote Client

این‌ها فقط در صورت وجود Backend/Agent واقعی دوباره فعال شوند.

### تبدیل از UI ظاهری به قابلیت واقعی
- + محصول جدید
- Remote
- Screenshot
- Restart/Shutdown
- Customer financial actions
- Package changes
- Refund

### قابلیت‌های کم‌شده که باید اضافه شوند
- Pause/Resume
- Reduce Time
- Free Time
- Refund/Reverse
- Pricing Schedule
- Minimum Charge
- Price Resolve/Event
- Wallet Ledger
- Shift Settlement Summary
- Concurrent Login Limit
- Order lifecycle
- Inventory adjustment/reason
- Full session financial history

## 21. Definition of Done برای هر Action

هر دکمه فقط وقتی Done است که:
1. UI state درست شود.
2. Server command تعریف شود.
3. Permission بررسی شود.
4. Domain rule اجرا شود.
5. DB transaction پایدار شود.
6. Audit مناسب ثبت شود.
7. SignalR state لازم به‌روزرسانی شود.
8. خطای قابل‌فهم فارسی داشته باشد.
9. مسیر Cancel/Reverse در صورت نیاز مشخص باشد.
10. تست واحد/یکپارچه یا UI لازم برای آن اجرا شود.

## 22. ترتیب پیاده‌سازی

1. Pricing/Session Domain Contract
2. Wallet/Ledger/Refund
3. Session Pause/Resume/Extend/Reduce/Transfer
4. Settlement/Invoice/Shift
5. Customer/Package/Free Time/Concurrent Login
6. Buffet Order/Inventory
7. Dashboard + SignalR
8. Client Agent Commands
9. Reports/Audit
10. حذف نهایی OperationsPage

## منابع تحقیق
- iCafeCloud — Billing/Pricing: https://www.icafecloud.com/features/internet-cafe-billing-software.htm
- iCafeCloud — Gaming Centers: https://www.icafecloud.com/solutions/gaming-centers.htm
- iCafeCloud — License Pools: https://www.icafecloud.com/features/game-license-sharing.htm
- iCafeCloud — Create License Pool: https://www.icafecloud.com/wiki-create-a-license-pool.htm
