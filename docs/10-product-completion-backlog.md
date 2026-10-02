# Product Completion Backlog — تکمیل جامع GameNet Manager

این فایل فهرست مرجع مواردی است که در مرور جامع محصول شناسایی شده‌اند. ترتیب اجرا وابستگی‌ها را رعایت می‌کند و هر مورد فقط پس از پیاده‌سازی + تست به وضعیت Done می‌رسد.

## قوانین اجرا
- همه متن‌های قابل مشاهده کاربر فارسی و قابل‌فهم باشند.
- Server تنها منبع حقیقت است.
- Mock نباید در نسخه نهایی به‌عنوان قابلیت واقعی نمایش داده شود.
- هر عملیات حساس: Permission + Persistence + Audit + خطای فارسی + Reverse/Cancel در صورت نیاز.
- هر مرحله قبل از رفتن به مرحله بعد باید Build/Test/CI و در صورت UI، تست تعاملی لازم را پاس کند.
- وابستگی‌ها زودتر از مصرف‌کننده پیاده‌سازی می‌شوند.

## نقشهٔ یکپارچهٔ ۱۴ مرحلهٔ اصلی پروژه

برای جلوگیری از قاطی‌شدن «فاز معماری»، «مرحلهٔ محصول» و «آیتم backlog»، از اینجا شماره‌گذاری اصلی پروژه این است:

1. **Foundation** — ریپو، Runner، CI، اسکلت Server/Dashboard
2. **Prototype & Behavior Transfer** — انتقال Prototype و رفتارهای پایه به React
3. **Operational Completion** — تکمیل عملیات روزانه اپراتور و UX؛ A1 تا A8
4. **Finance & Session Core** — Ledger، Reverse واقعی، Settlement، Split Payment، شیفت و انتقال جلسه
5. **Customer & VIP Domain** — مشتری، کیف پول، بدهی، Free Time/Money، محدودیت ورود
6. **Buffet & Inventory Domain** — سفارش بوفه، موجودی، ضایعات، مرجوعی، حداقل موجودی
7. **Users & Permissions** — Permission واقعی، Approval سروری، چندصندوقی و تعارض عملیات
8. **PC Agent Foundation** — Heartbeat، Health، Telemetry و اتصال ۴۰ PC
9. **Real Client Commands & Kiosk** — فرمان‌های واقعی، Shell/Kiosk و Policy
10. **Client Lifecycle** — Update/Rollback، Recovery و سلامت چرخهٔ کلاینت
11. **Games & Accounts** — Game Library واقعی، Process Detection، Account Pool و Lease
12. **Reporting & Audit** — Heatmap، درآمد/کارکرد، سود، Audit Explorer و گزارش‌های کامل
13. **Reservations & Operations Scale** — Reservation/Waitlist، Event/Tournament، Network State و Multi-cashier
14. **UI/Deployment Hardening** — Primitiveهای UI، DataTable/InfoPanel، Keyboard-first، Desktop Shell، Installer و انتشار نهایی

**جایگاه فعلی:** مرحلهٔ اصلی **۳ — Operational Completion**.  
آیتم‌های A1 تا A8 زیر همین مرحله هستند. پس از بسته‌شدن واقعی A1 تا A8، وارد مرحلهٔ اصلی **۴ — Finance & Session Core** می‌شویم.

این ۱۴ مرحله یک نقشهٔ اجرایی واحد برای پروژه است؛ فازهای قدیمی ۴گانهٔ معماری و مراحل فنی ۰ تا ۷ اسناد قبلی به‌عنوان سابقهٔ معماری باقی می‌مانند و برای شماره‌گذاری روزمره ملاک نیستند.

## تکمیل‌های سراسری مرحله ۳ — نیازهای جدید اپراتور

### UX انتخاب و چندانتخاب
- Regression نهایی داشبورد در Run 295 با Playwright سبز شد: انتخاب تکی، Ctrl، Shift، Drag و بررسی خالی‌بودن متن انتخاب‌شده.
- رفتار Selection یکسان باید هر جا «چند ایستگاه/کلاینت» معنی دارد قابل استفاده باشد.
- داشبورد: انتخاب تکی، Ctrl/⌘، Shift، Drag، Ctrl/⌘+Drag.
- کلاینت‌ها: همان قرارداد انتخاب تکی/چندتایی + Drag.
- پس از Drag نباید Click ناخواسته اجرا شود.
- متن کارت‌ها نباید توسط Drag انتخاب شود.
- در صفحات بدون عملیات گروهی، چندانتخاب اجباری اضافه نشود تا UI شلوغ نشود.

### Accounts — مدیریت بر اساس Pool/Platform
- نمایش همهٔ اکانت‌ها در یک Grid شلوغ نیست.
- سطح اول: پلتفرم/Launcher (Steam، Epic، Riot، Battle.net).
- سطح دوم: فقط اکانت‌های همان Pool.
- سطح جزئیات: وضعیت، بازی‌های مجاز، کلاینت تخصیص‌یافته، Guard/2FA، مالک و انقضا.
- در ادامه باید License Pool/Account Lease/Health به Server Domain متصل شود.
- این الگو با سیستم‌های فعلی گیم‌نت هم‌راستاست؛ iCafeCloud حساب‌ها را به Pool بر اساس نوع Launcher/بازی گروه‌بندی و وضعیت آزاد/درحال‌استفاده/قفل را جدا نمایش می‌دهد. citeturn683069search1turn683069search4

### Games — Master/Detail
- لیست بازی‌ها در یک سمت با Search/Filter.
- انتخاب یک بازی → پنل جزئیات همان بازی در سمت دیگر.
- اطلاعات تنظیمی در Detail Panel متمرکز می‌شود؛ Grid کارت‌های بزرگ برای ۵۰+ بازی مرجع اصلی نیست.
- دسته‌بندی، Search، Cover، Launcher، مسیر، exe، پارامتر اجرا، Target و وضعیت باید در Detail Panel مدیریت شوند.
- Game Library نهایی باید قابلیت Search/Category/Scope/Process Detection داشته باشد؛ این الگو با کتابخانه‌های گیم‌نت فعلی نیز هم‌جهت است. citeturn683069search0turn683069search4

### Customer — Refund/کسر اعتبار
- عملیات «کسر اعتبار / بازگشت وجه» باید کنار شارژ، بدهی و اعتبار رایگان در پروفایل مشتری دیده شود.
- Refund نیازمند مبلغ + دلیل است.
- Refund باید به Ledger و Audit وصل شود و حذف/ویرایش مستقیم موجودی جایگزین آن نباشد.
- برای Refundهای حساس، Permission/Approval لازم است.
- الگوی ثبت Refund با مبلغ و توضیح و قابل‌پیگیری در لاگ، در نرم‌افزارهای گیم‌نت موجود نیز استفاده می‌شود. citeturn683069search9turn683069search3

### Settings — مرکز تنظیمات واقعی
تنظیمات باید به گروه‌های روشن تقسیم شوند:
- نمایش و Layout
- هشدار، صدا و Popup
- رفتار جلسه، تسویه و رند
- Backup/Recovery
- شبکه و دو اینترنت
- Client Policy / Wake-on-LAN
- حقوق و شیفت
- امنیت و قفل بخش‌ها
- Hotkey و Keyboard
- ظاهر/تقویم/واحد پول
- سیاست حذف، Refund و Approval

### Users/Shift — حقوق و حسابداری پرسنل
- پروفایل پرسنل باید نوع حقوق (ساعتی/ماهانه)، نرخ ساعتی، حقوق ماهانه، ساعت کاری، اضافه‌کاری، پاداش و کسری را نگه دارد.
- Shift باید Start Cash، Cash Sales، Refund، Expense، Cash End، Difference و توضیح تطبیق دستی را ثبت کند.
- وجه نقد دریافت‌شده خارج از نرم‌افزار باید «تطبیق دستی صندوق + دلیل» داشته باشد؛ نباید بی‌دلیل مستقیماً از حقوق اپراتور کم شود.
- پاداش و کسری حقوق باید عملیات جداگانه، دارای دلیل و در مرحلهٔ بعد Permission/Approval/Audit داشته باشند.
- الگوهای فعلی گیم‌نت‌ها روی Shift مستقل برای هر کارمند، شروع با مبلغ نقدی، گزارش پایان شیفت، اختلاف صندوق و گزارش قابل‌خروجی تأکید دارند. citeturn436697search6turn436697search13turn436697search14

### Security — قفل سطح بخش
- هر بخش حساس می‌تواند رمز جدا داشته باشد.
- نمونه‌ها: مشتریان، اکانت‌ها، تنظیمات، کاربران/شیفت، گزارش‌ها، بازی‌ها، تعرفه‌ها، کلاینت‌ها، بوفه.
- باز شدن هر بخش فقط پس از ورود رمز خودش در همان نشست مدیریتی مجاز است.
- این لایه فعلاً UX است؛ Permission/Server باید منبع حقیقت نهایی باشد.
- Permissionهای واقعی باید بر مشاهده/ویرایش/اجرا/Refund/Reverse/Settings تفکیک شوند؛ سیستم‌های گیم‌نت موجود نیز دسترسی‌های ریزدانه برای Reports، Members، License Pool، Remote Control و موارد مشابه دارند. citeturn436697search1turn436697search2turn436697search9

## اصلاحات اجرایی جدید — داشبورد / Attention Center

### پیگیری شارژ دستی
- هر شارژ دستیِ زمان جلسه باید با **مبلغ، یوزر/مشتری، ایستگاه و زمان ثبت** قابل پیگیری باشد.
- مرکز «نیازمند توجه» باید بتواند شارژ ثبت‌شده را تا پایان جلسه جلوی چشم اپراتور نگه دارد.
- اعتبار پیش‌پرداختی جلسه دارای زمان پایان محاسباتی است؛ نزدیک پایان هشدار بدهد و بعد از اتمام اعتبار، «اقدام لازم» ایجاد کند.
- پس از پایان جلسه، مبلغ باقی‌مانده باید از مبلغ پیش‌پرداختی تفکیک شود؛ در صورت عدم دریافت وجه، وضعیت «پرداخت باقی‌مانده» در پیگیری اپراتور باقی بماند.
- برای عملیات مالی آینده، این پیگیری‌ها باید به Ledger / Invoice / Audit واقعی سرور متصل شوند؛ نسخهٔ فعلی فقط UX و رفتار Mock را تثبیت می‌کند.

### شروع جلسه
- PC/رایانه همیشه **۱ نفر** است و انتخاب ۲/۳/۴ نفر برای آن نمایش داده نمی‌شود.
- PS5/PS4/سایر منابعی که چندنفره هستند می‌توانند انتخاب نفرات داشته باشند.
- نرخ جلسه از تعرفهٔ خودکار می‌آید ولی اپراتور باید بتواند **نرخ ساعتی همان جلسه را دستی وارد یا اصلاح کند**.

### مبلغ‌های دستی
- برای شارژ، Flow مالی و عملیات مشتری، مبلغ اصلی باید دستی قابل ورود باشد.
- Presetهای اجباری ۵۰/۱۰۰/۲۰۰/۵۰۰ هزار تومان مرجع اصلی ورود مبلغ نیستند و در UI اصلی حذف شدند.

### صفحه مشتریان
- ساختار رسمی: **لیست مشتریان + پروفایل جزئیات**.
- در لیست، اطلاعات سریع باید شامل نام، کد، نام کاربری، تماس کوتاه، VIP، کیف پول، بدهی و وضعیت باشد.
- پروفایل باید سه شاخص مالی، اطلاعات هویتی، بسته/VIP، مصرف روزانه و تاریخچه تراکنش را بدون ردیف‌های سفید و نامنظم نشان دهد.
- تاریخچهٔ تراکنش به شکل رویدادهای خوانا نمایش داده می‌شود و در مسیر بعدی به Ledger/Data Table واقعی متصل خواهد شد.

## قرارداد اجرایی آیتم ۳ — Session Center

### هدف
مرکز واحد مشاهده و عملیات یک جلسه فعال، بدون خروج اپراتور از داشبورد.

### مالکیت و ورودی
- مالک: Dashboard
- ورودی: StationDto زنده + CustomerRecord متناظر + زمان جاری
- باز شدن با کلیک روی ایستگاه busy/paused یا «جزئیات کامل جلسه» در منوی راست‌کلیک.

### اطلاعات نمایش‌داده‌شده
- وضعیت جلسه و تایمر زنده
- مشتری/مهمان و کد
- نرخ جلسه
- تعداد نفرات
- هزینه زمان
- بوفه
- جمع فعلی
- اعتبار/شارژ ثبت‌شده
- مبلغ قابل دریافت
- بدهی مشتری

### عملیات
- توقف / ادامه
- شارژ جلسه
- تمدید
- کاهش زمان
- تسویه
- بستن با Esc یا کلیک بیرون

### مرز واقعی‌سازی
نسخه فعلی با Mock/State UI رفتار را تثبیت می‌کند. در اتصال نهایی، هر عملیات باید مطابق Action Map از Server Command + Permission + Persistence + Audit + SignalR عبور کند.

### فایل‌ها
- src/Dashboard/src/features/session/SessionCenter.tsx
- src/Dashboard/src/pages/DashboardPage.tsx
- src/Dashboard/src/App.css
- docs/08-action-map.md

## اصلاح گردش کار شروع PC و پرداخت پایان بازی

- شروع جلسه روی رایانه دیگر لیست بلند مشتریان ندارد؛ اپراتور یوزر/کد/موبایل را تایپ می‌کند و با Enter جست‌وجو می‌شود.
- در مرحلهٔ ورود یوزر، قیمت، تعرفه و مبلغ انتخابی نمایش داده نمی‌شود؛ فقط هویت مشتری، کیف پول، بدهی و وضعیت پروفایل نمایش داده می‌شود.
- اگر کیف پول خالی باشد، هشدار واضح «شارژ کیف پول یا پروفایل» نمایش داده می‌شود؛ تصمیم دریافت وجه می‌تواند به پایان بازی منتقل شود.
- PC همیشه یک نفر است.
- پایان بازی از دریافت وجه جدا شده است: با پایان جلسه، مبلغ قابل دریافت در ستون ثابت سمت چپ داشبورد قرار می‌گیرد.
- برای هر پرداخت در انتظار سه اقدام سریع وجود دارد: «تسویه شد»، «از کیف پول» و «ثبت بدهی».
- «از کیف پول» در صورت کمبود موجودی، موجودی موجود را کسر کرده و مابه‌التفاوت را به بدهی تبدیل می‌کند.
- «ثبت بدهی» فقط بدهی را ثبت می‌کند و دریافت وجه واقعی محسوب نمی‌شود.
- نرخ ساعتی جلسه از مرحله ورود یوزر حذف شده و ویرایش دستی آن داخل Session Center انجام می‌شود؛ نرخ فقط روی همان جلسه اثر دارد.
- مرکز پیگیری به جای یک دکمهٔ کوچک، به یک ستون عمودی ثابت در سمت چپ داشبورد تبدیل شده است: پرداخت‌های در انتظار، نیازمند توجه و آخرین عملیات.
- تشخیص خودکار واقعی پایان بازی هنوز به Agent/Process Detection وابسته است؛ فعلاً همان مسیر «پایان بازی» در Dashboard منبع ایجاد پرداخت در انتظار است.

## قرارداد اجرایی آیتم ۸ — Undo / Reverse UX

- Undo در محصول به معنی حذف رکورد نیست؛ همیشه یک عملیات معکوس با رکورد جدید ثبت می‌شود.
- عملیات برگشت‌پذیر فعلی: شارژ جلسه، تمدید، کاهش زمان و فروش بوفه.
- عملیات دارای Side Effect مالی/Session حساس مثل تسویه، پاک نمی‌شوند و تا اتصال Reverse واقعی سرور وارد Undo خودکار نمی‌شوند.
- تاریخچهٔ اصلی باقی می‌ماند و یک رویداد «برگشت عملیات» به Timeline اضافه می‌شود.
- در معماری نهایی، Reverse باید Server Command + Permission + Audit + Transaction باشد.
- فایل: src/Dashboard/src/components/ReverseDialog.tsx

## قرارداد اجرایی آیتم ۷ — Approval Flow

- عملیات حساس باید بتواند قبل از اجرا وارد وضعیت «نیازمند تأیید» شود.
- کاربر اولیه در این نسخه بر اساس نقش Demo (`operator` / `manager` / `owner`) تشخیص داده می‌شود.
- نمونهٔ فعلی: تخفیف بیشتر از ۱۰٪ توسط اپراتور بدون تأیید ادامه پیدا نمی‌کند.
- پنجرهٔ تأیید سه مفهوم روشن دارد: دلیل نیاز به تأیید، عملیات موردنظر، تأیید و ادامه یا انصراف.
- نسخهٔ فعلی فقط UX/State را تثبیت می‌کند؛ تأیید نهایی باید بعداً توسط Server Permission + Approval Record + Audit کنترل شود و UI به هیچ وجه منبع حقیقت مجوز نیست.
- فایل اصلی: src/Dashboard/src/components/ApprovalDialog.tsx

## Vertical Slice آیتم B9 — Wallet Ledger

- Server از WalletTransaction موجود به‌عنوان دفتر تراکنش استفاده می‌کند؛ جدول موازی ساخته نشد.
- GET دفتر کیف پول: `/api/customers/{customerId}/wallet-ledger`
- POST تراکنش کیف پول: `/api/customers/{customerId}/wallet-transactions`
- Credit/Debit، کنترل موجودی، تراکنش SQLite و Audit در یک مسیر پایدار ثبت می‌شوند.
- Dashboard adapter در `src/Dashboard/src/services/walletLedgerService.ts` برای API واقعی و Mock fallback دارد.
- Customer Profile اکنون دفتر کیف پول را با مبلغ، جهت، توضیح، زمان و ماندهٔ بعد از تراکنش نشان می‌دهد.
- تغییرات کیف پول Dashboard شامل Flow مستقیم، کسر، تسویه از کیف پول، پرداخت‌های معوق و شارژ کیف پول به همین Ledger adapter منتقل شد. گام بعدی B9: اتصال Permission/Server Command کامل و همگام‌سازی همهٔ Invoice/Refundها با Ledger.

## تکمیل B10 — Reference در Refund

- Refund کیف پول اکنون می‌تواند `SourceTransactionId` داشته باشد.
- Server مبدأ را برای همان مشتری و از نوع Credit بررسی می‌کند.
- مبلغ Refund از مجموع Refundهای قبلی همان مبدأ بیشتر نمی‌تواند باشد.
- رکورد اصلی حذف نمی‌شود و رکورد Debit جدید با `ReferenceTransactionId` ثبت می‌شود.
- Migration: `20261002090000_WalletRefundReference`.
- Customer Profile باید قبل از ثبت Refund یک تراکنش Credit را به‌عنوان مبدأ انتخاب کند.

## قرارداد اجرایی آیتم ۶ — Error UX استاندارد

- هر خطای کاربر باید سه بخش داشته باشد: چه شد، معنی/علت قابل‌فهم، اقدام بعدی.
- پیام خام Exception، HTTP status، stack trace یا متن فنی API نباید مستقیماً به کاربر نمایش داده شود.
- حالت‌های پایه: شبکه، دسترسی، اعتبارسنجی، تعارض، پیدا نشدن، خطای عمومی.
- هر جا امکان ادامهٔ امن وجود دارد باید یک دکمهٔ اقدام بعدی مثل «تلاش مجدد» یا «بررسی اطلاعات» ارائه شود.
- کامپوننت پایه: src/Dashboard/src/components/UserErrorBanner.tsx
- اتصال فعلی: خطای دریافت Dashboard API در src/Dashboard/src/App.tsx.
- در مراحل بعدی باید تمام عملیات Server Command/Agent از همین قرارداد استفاده کنند.

## قرارداد اجرایی آیتم ۵ — عملیات اخیر اپراتور

- مالکیت: Dashboard
- هدف: نمایش آخرین عملیات واقعی ثبت‌شده در نشست فعلی اپراتور، بدون رفتن به گزارش‌ها.
- منبع فعلی: SessionTimelineEventهای ثبت‌شده توسط عملیات جلسه.
- نمایش: حداکثر ۹ رویداد اخیر با عنوان، ایستگاه، توضیح و ساعت.
- رویدادهای فعلی: شروع، توقف، ادامه، شارژ، تمدید، کاهش زمان، بوفه و تسویه.
- در معماری نهایی، رویدادهای Audit/OperatorAction سرور باید منبع حقیقت باشند و با نقش/Permission فیلتر شوند.

## قرارداد اجرایی آیتم ۴ — Timeline کوتاه ایستگاه

- رویدادهای عملیاتی جلسه با ترتیب معکوس زمانی نمایش داده می‌شوند.
- رویدادهای فعلی: شروع، توقف، ادامه، شارژ، تمدید، کاهش زمان، بوفه و تسویه.
- هر رویداد دارای زمان، عنوان، توضیح و در صورت نیاز مبلغ است.
- Timeline کوتاه است و فقط آخرین رویدادهای مفید همان جلسه را نشان می‌دهد؛ تاریخچهٔ کامل Audit جایگزین آن نیست.
- منبع فعلی: state محلی UI/Mock؛ در اتصال نهایی باید از Event/Audit واقعی سرور تغذیه شود.
- مالکیت نمایش: Session Center / Dashboard.

## قرارداد اجرایی آیتم ۲ — جست‌وجوی سراسری و مرکز فرمان

### هدف
یک نقطه ورود واحد برای پیدا کردن رکوردهای عملیاتی و اجرای فرمان‌های پرتکرار، بدون جابه‌جایی بی‌دلیل بین صفحه‌ها.

### ورودی‌ها
- متن جست‌وجو: نام، کد، شماره، موبایل، شناسه، بخشی از عنوان یا اطلاعات رکورد.
- کلیدهای کیبورد: Ctrl+K، ↑، ↓، Enter و Esc.
- فرمان انتخاب‌شده از فهرست فرمان‌های موجود.

### منابع داده فعلی
- ایستگاه‌ها از DashboardSnapshotDto.stations
- مشتری، کالا، تعرفه، بازی، اکانت، کلاینت، کاربر و فاکتور از mockService
- فرمان‌ها از قرارداد داخل GlobalCommandCenter.tsx

### خروجی
- نتیجهٔ رکورد به تفکیک نوع: ایستگاه، مشتری، بوفه، تعرفه، بازی، اکانت، کلاینت، کاربر و فاکتور.
- فرمان‌های قابل اجرا با توضیح کوتاه.
- انتخاب نتیجه → هدایت به صفحه مالک همان قابلیت و ارسال gamenet-search-selection.
- انتخاب فرمان → هدایت به صفحه مالک و ارسال gamenet-command.

### کنترل‌های UI
- دکمهٔ هدر «جست‌وجو · Ctrl+K»
- ورودی واحد
- نتایج گروه‌بندی‌شده
- Highlight نتیجه فعال
- اجرای با Enter
- حرکت با ↑/↓
- بستن با Esc یا کلیک بیرون

### وابستگی و مرز نهایی
نسخهٔ فعلی مرکز، تجربهٔ جست‌وجو/فرمان را روی داده‌های موجود UI کامل می‌کند. اجرای نهایی هر فرمان همچنان باید در مسیر Server Command → Permission → Domain Rule → Persistence → Audit → SignalR قرار گیرد؛ این بخش عمداً تا تکمیل دامنهٔ سرور جعل نمی‌شود.

### فایل‌های مربوط
- src/Dashboard/src/features/search/GlobalCommandCenter.tsx
- src/Dashboard/src/App.tsx
- src/Dashboard/src/App.css
- docs/08-action-map.md
- docs/10-product-completion-backlog.md

## اصلاح UX انتخاب ایستگاه‌ها — مرحله ۳

- متن داخل کارت‌های ایستگاه قابل انتخاب/Drag نیست.
- کلیک معمولی رفتار عملیاتی قبلی کارت را حفظ می‌کند.
- Ctrl/⌘ برای انتخاب یا لغو انتخاب تکی کار می‌کند.
- Shift برای انتخاب بازه بین کارت مبنا و کارت مقصد کار می‌کند.
- Drag روی فضای کارت، کادر انتخاب می‌سازد و چند ایستگاه را هم‌زمان انتخاب می‌کند.
- Ctrl/⌘ + Drag به انتخاب فعلی اضافه می‌کند.
- Esc انتخاب‌ها را پاک می‌کند و Ctrl/⌘+A تمام ایستگاه‌های قابل‌مشاهده را انتخاب می‌کند.
- انتخاب فعال با نشان تیک و شمارندهٔ «ایستگاه انتخاب شده» در داشبورد قابل مشاهده است.
- CI روی HEAD این اصلاح سبز شد.

## فهرست اصلی

> مرحله اصلی فعلی: **۴ — Finance & Session Core**؛ ترتیب B9 → B17 حفظ می‌شود.

### A) Operational Intelligence / UX
1. ✅ مرکز «نیازمند توجه» در داشبورد — پیاده‌سازی و CI سبز شد
2. ✅ جست‌وجوی سراسری و مرکز جست‌وجو/فرمان — Build/Test/CI + Smoke Test تعاملی سبز
3. ✅ پنل جزئیات جلسه (Session Center) — Build/Test/CI + Smoke Test تعاملی سبز
4. ✅ Timeline کوتاه ایستگاه — Build/Test/CI + Smoke Test تعاملی سبز
5. ✅ عملیات اخیر اپراتور — Build/Test/CI + Smoke Test تعاملی سبز
6. ✅ Error UX استاندارد: خطا → معنی → اقدام بعدی — Build/Test/CI + Smoke تعاملی سبز
7. ✅ Approval Flow برای عملیات حساس — Build/Test/CI + Smoke Test تعاملی سبز
8. ✅ Undo UX برای عملیات برگشت‌پذیر (Reverse واقعی، بدون حذف رکورد) — Build/Test/CI + Smoke Test تعاملی سبز

## اجرای مرحله اصلی ۴ — B11 Settlement

### وضعیت فعلی
- Settlement UI از همان Billing Engine استفاده می‌کند و محاسبه جداگانه/قدیمی مودال حذف شد.
- Breakdown شامل زمان، دقیقه قابل صورتحساب، نرخ، بوفه، اعتبار پیش‌پرداخت، تخفیف و رند است.
- بخش «چرا این مبلغ؟» مسیر محاسبه را برای اپراتور توضیح می‌دهد.
- برای پرداخت نقدی مبلغ دریافتی قابل ورود است و مبلغ برگشتی محاسبه می‌شود.
- Free Time در Breakdown لحاظ می‌شود.
- Split Payment هنوز جداست و در B12 تکمیل می‌شود.
- Invoice/Settlement واقعی Server هنوز باید مالک نهایی محاسبه و ثبت شود.

## اجرای مرحله اصلی ۴ — B10 Refund

### مسیر فعلی
- endpoint مستقل: POST /api/customers/{customerId}/wallet-refunds
- دلیل Refund اجباری است.
- موجودی قبل از ثبت Refund کنترل می‌شود.
- تراکنش اصلی حذف نمی‌شود؛ یک Ledger debit جدید برای بازگشت وجه ثبت می‌شود.
- Audit با Action = WalletRefund ثبت می‌شود.
- Customer Profile از سرویس Refund اختصاصی استفاده می‌کند.
- تست Server برای حفظ رکورد اصلی + ثبت Debit + Audit اضافه شد.

### مرز باقی‌مانده
- Permission سروری
- Approval برای Refundهای حساس
- Reference صریح به تراکنش/Invoice مبدأ
- Refund واقعی برای Session/Buffet/Package
- Reverse کامل و چندمرحله‌ای
این موارد در B10/B11 و مرحله ۷/مالی تکمیل می‌شوند.

## وضعیت مرحله اصلی ۳ — Operational Completion

**تکمیل شد.** A1 تا A8 همگی پیاده‌سازی شدند و CI + Dashboard Interaction Smoke روی HEAD نهایی سبز است. مرحله اصلی بعدی: **۴ — Finance & Session Core**.

### B) Finance / Session
9. [~] Wallet Ledger واقعی — API/SQLite/Audit/adapter + مشتری مالی سروری + حذف Mock fallback انجام شد؛ Permission/Server Command نهایی و تکمیل Customer Domain در مرحله ۵ اصلی باقی است
10. [~] Refund / Reverse واقعی — Refund کیف پول Server/API + Audit + UI + Approval + Reference تراکنش مبدأ + محدودیت مبلغ نسبت به مبدأ + Persistence Test + CI/Playwright Run 309 سبز شد؛ Reverse کامل Session/Buffet/Package و Permission سروری هنوز باقی است
11. [~] Settlement کامل + Breakdown + Why this amount? — Billing Engine/Breakdown/Why/Received/Change + Server Atomic Settlement + Invoice/Wallet/Audit + تست اتمیک و Smoke سبز؛ اتصال مستقیم Dashboard به Session/Invoice سرور و Permission نهایی باقی
12. ✅ Split Payment — UI + ثبت اتمیک سروری + Invoice + Audit + Dashboard Smoke سبز؛ Permission نهایی طبق مرحله ۷ تکمیل می‌شود
13. [~] Shift Settlement + Shift Handover — Shift Open/Close سروری + فروش نقدی از InvoicePayment + هزینه + تطبیق نقدی + Audit + Build/Test/Smoke سبز؛ اتصال کامل Users/Permission هنوز باقی
14. [~] Session Transfer / Change Tariff / Change Persons — Session Center + Server transaction/Audit برای تغییر نرخ/نفر/انتقال + Build/Test/Smoke سبز؛ Permission نهایی مرحله ۷ باقی
15. [~] Free Time + Free Money کامل — Ledger مستقل سروری، اعطا/کسر، مصرف در Settlement و Build/Test/Smoke سبز؛ مهاجرت کامل Customer Domain/Permission نهایی باقی
16. [~] Concurrent Login Limit — قرارداد مشتری + Mock Login Guard + Client UX پیاده شد؛ enforcement سروری/Agent و چنددستگاهی واقعی باقی
17. [~] Expense / Profit واقعی و قابل ممیزی — Expense/Operating Profit Server + SQLite + Audit + Reports adapter اضافه شد؛ CI/Smoke سبز و اتصال کامل Invoice/COGS/Profit باقی

### C) Client / Agent
18. Client Health / Heartbeat / Telemetry
19. Group Command Tracking
20. Client Update Center + Safe Update + Rollback
21. Recovery Center
22. واقعی‌سازی Client Commands
23. Safe Kiosk/Shell
24. Client Favorites / Recent / Search / Category
25. Client Now Playing / Fullscreen hiding
26. Client Policy برای نرم‌افزارهای مجاز

### D) Inventory / Buffet / Account
27. Buffet Order Lifecycle واقعی
28. Inventory Adjustment / Waste / Return / Minimum Stock
29. Account Pool Health
30. Account Lease History
31. Game Library واقعی + Cover + Process Detection
32. Trailer فقط در صورت مصرف واقعی

### E) Reporting / Management
33. Heatmap استفاده از ایستگاه/ساعت
34. گزارش درآمد/کارکرد هر ایستگاه
35. گزارش سود/حاشیه سود
36. Audit Explorer
37. Reservation + Waitlist زنده داخل Dashboard
38. Event Mode / Tournament-ready architecture
39. Internal Notes
40. Station/Client Health Dashboard
41. Dashboard Network State تفکیک‌شده: سرور داخلی / LAN / اینترنت ۱ / اینترنت ۲
42. Multi-cashier concurrency / conflict handling

### F) UI System
43. تثبیت چهار Primitive بصری: Summary Card / Entity Card / Info Panel / Data Table/List
44. بازطراحی هدفمند Profile/Session/Tariff/Shift با Info Panel
45. تبدیل Account/Audit/Finance History به Data Table در جاهایی که مقایسه مهم است
46. Responsive + dense operator mode
47. Keyboard-first operator UX
48. Persian terminology catalogue

## ترتیب اجرایی
A1 → A2 → A3 → A4 → A5 → A6 → A7 → A8
→ B9 → B10 → B11 → B12 → B13 → B14 → B15 → B16 → B17
→ C18 → C19 → C20 → C21 → C22 → C23 → C24 → C25 → C26
→ D27 → D28 → D29 → D30 → D31 → D32
→ E33 → E34 → E35 → E36 → E37 → E38 → E39 → E40 → E41 → E42
→ F43 → F44 → F45 → F46 → F47 → F48

## قرارداد اجرایی B15 — Free Time + Free Money

- Free Time (دقیقه رایگان) از Free Money (اعتبار رایگان تومانی) جدا نگه داشته می‌شود.
- Free Time فقط روی زمان قابل صورتحساب اثر می‌گذارد.
- Free Money در تسویه قابل مصرف است و از موجودی اعتبار رایگان مشتری کم می‌شود.
- مصرف Free Money در تاریخچه مشتری و گزارش مالی با روش پرداخت مستقل «اعتبار رایگان» ثبت می‌شود.
- اتصال نهایی سرور باید مصرف Free Time/Free Money را Transaction/Audit کند.

## قرارداد اجرایی B14 — Session Transfer / Change Tariff / Change Persons

- نرخ جلسه داخل Session Center قابل تغییر است و فقط روی همان جلسه اثر دارد.
- نفرات جلسه داخل Session Center قابل تغییر است؛ PC همیشه ۱ نفر باقی می‌ماند.
- جلسه فعال/متوقف را می‌توان به ایستگاه آزاد منتقل کرد و وضعیت جلسه، مشتری، زمان، شارژ و بوفه همراه آن منتقل می‌شوند.
- ایستگاه مبدأ آزاد می‌شود و رویداد انتقال در Timeline ثبت می‌شود.
- نسخه نهایی سروری باید انتقال را به‌صورت Transaction و با Permission/Audit ثبت کند.

## قرارداد اجرایی B13 — Shift Settlement + Handover

- بستن شیفت دیگر با prompt انجام نمی‌شود.
- اپراتور در فرم تسویه، وجه مورد انتظار، وجه شمارش‌شده، تطبیق نقدی خارج از سیستم و اختلاف را می‌بیند.
- یادداشت تحویل شیفت برای مشکلات دستگاه، بدهی، سفارش باز یا هر موضوع منتقل‌شونده ثبت می‌شود.
- وجه خارج از سیستم جدا از فروش نرم‌افزاری ثبت می‌شود و نباید به‌صورت پنهان از حقوق کم شود.
- در نسخه سروری نهایی: Shift Close + Handover باید Transaction/Audit/Permission داشته باشد.

## قرارداد اجرایی B11 — Server Atomic Settlement

- سرویس `SessionSettlementService` تسویه را در یک transaction ثبت می‌کند.
- مجموع سهم‌های پرداخت باید دقیقاً با مبلغ نهایی برابر باشد.
- سهم کیف پول ابتدا موجودی را بررسی و سپس WalletTransaction از نوع Debit ثبت می‌کند.
- Invoice با Status=Paid و InvoiceItem برای جلسه ثبت می‌شود.
- Session به Completed می‌رود و EndAt/TotalAmount ثبت می‌شود.
- AuditLog با روش‌ها و مبلغ‌های پرداخت ثبت می‌شود.
- دو تست سرور اضافه شده: تسویه ترکیبی واقعی و رد split نامعتبر بدون تغییر Session/Wallet.
- endpoint: POST /api/sessions/{sessionId}/settle
- اتصال Dashboard هنوز باید Session واقعی سرور را به این endpoint متصل کند.

## قرارداد اجرایی B12 — Split Payment

- پرداخت نهایی یک جلسه می‌تواند بین نقدی، کارتخوان و کیف پول تقسیم شود.
- مبلغ هر روش دستی وارد می‌شود و جمع سه روش باید دقیقاً برابر مبلغ قابل دریافت باشد.
- سهم کیف پول از Ledger واقعی کم می‌شود و کمبود موجودی مانع ثبت می‌شود.
- برای هر سهم نقد/کارت گزارش مالی جدا ثبت می‌شود، ولی Invoice جلسه یک تسویه ترکیبی واحد باقی می‌ماند.
- در Timeline جلسه، روش‌ها و مبلغ هر سهم قابل پیگیری هستند.
- در نسخه نهایی Server باید تراکنش Split را به‌صورت atomic ثبت کند و Audit/Permission نیز داشته باشد.

## وضعیت
مرحله اصلی ۳ — Operational Completion — بسته است و A1 تا A8 همگی از نظر Build/Test/Smoke تثبیت شده‌اند. مرحله اصلی ۴ — Finance & Session Core — در حال تکمیل است؛ B9 تا B17 vertical slice دارند، اما هر مورد فقط بعد از اتصال نهایی Server/Permission/Transaction/Audit به Done می‌رسد. HEAD فعلی روی CI و Dashboard Interaction Smoke سبز است.
