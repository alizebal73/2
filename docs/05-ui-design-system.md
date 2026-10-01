# سند طراحی UI — سیستم طراحی و قرارداد داده (نسخه‌ی قفل‌شده)
> **قرار مهم:** پروتوتایپ [prototype/index.html](../prototype/index.html) از تاریخ ۱۴۰۵/۰۷/۰۹ «سند طراحی تاییدشده» است و بازنویسی نمی‌شود.
> کد واقعی UI (React) باید عیناً همین طراحی را پیاده کند؛ هیچ بازطراحی‌ای در مرحله‌ی کد مجاز نیست.

---

## ۱) توکن‌های طراحی (CSS Variables → دقیقاً همین‌ها در React)

```css
/* رنگ‌ها — بدون تغییر منتقل شود */
--bg:#070a14; --bg2:#0c1120; --panel:#101728; --panel2:#151d33; --line:#1e2a44;
--txt:#e9eef8; --dim:#8b98b8;
--green:#22e58a;   /* آزاد / موفق */
--blue:#3aa9ff;    /* رزرو / اطلاعات */
--purple:#a78bfa;  /* اعتبار رایگان / پکیج */
--orange:#ffb454;  /* پول / هزینه */
--red:#ff5d73;     /* در حال بازی / بدهی / خطر */
--cyan:#2dd4bf;    /* آمار جانبی */
--gold:#ffd166;    /* VIP گلد / صاحب گیم‌نت */
```

- **فونت:** Vazirmatn (در نسخه‌ی نهایی لوکال، داخل خود برنامه — بدون گوگل فونت)
- **جهت:** RTL کامل، `dir="rtl"` روی ریشه
- **شعاع گردی:** کارت‌ها 15px، دکمه‌ها 10px، بج‌ها 99px
- **پس‌زمینه‌ی کارت‌ها:** `linear-gradient(165deg, panel, panel2)` + بوردر `--line`
- **اعداد مالی و تایمر:** `font-variant-numeric: tabular-nums` + ارقام فارسی `toLocaleString('fa-IR')`
- **نقش رنگ وضعیت ایستگاه:** آزاد=سبز، بازی=قرمز+پالس، رزرو=آبی، خارج از سرویس=خاکستری

## ۲) صفحه‌ها → مسیرهای React (تطبیق یک‌به‌یک)

| پروتوتایپ (id) | مسیر React | کامپوننت‌های اصلی |
|---|---|---|
| `page-dash` | `/` (Dashboard) | StatCard, ZoneFilter, ViewModeSwitch[کارتی/فشرده/لیستی], StationCard, ModalStartSession, ModalSettle |
| `page-cust` | `/customers` | CustomerStats, CustomerList(جست‌وجو+فیلتر همه/VIP/بدهکار), CustomerProfile(مالی+پکیج+تاریخچه), ModalPackageVip |
| `page-buffet` | `/buffet` | BuffetStats, CategoryTabs, ProductCard(نوار موجودی), CartPanel(هدف: جلسه/مستقل) |
| `page-reports` | `/reports` | PeriodTabs[۷روز/ماهانه/سالانه], RevenueChart(تفکیک نقد/کارت), FinanceSummary, StationUsageTable |
| `page-users` | `/users` | UserList(نقش: صاحب/مدیر/اپراتور), PermissionMatrix(۱۲+ دسترسی، چک‌باکس), ShiftPanel(باز/بستن، فروش شیفت) |
| `page-settings` | `/settings` | SettingsPanel ×۶ (نمایش/هشدارها/فاکتور/بکاپ/شبکه/ظاهر) با Switch و SegSelect |

صفحه‌های آینده (قبل از ساخت در همین جدول اضافه شود): تعرفه‌ها `/tariffs`، رزرو `/reservations`، شل مشتری (کلاینت) `/client-shell`

## ۳) قرارداد داده — دیتای دمو = DTO آینده‌ی API

دیتای دموی پروتوتایپ عمداً با همین شکل نوشته شده؛ API هسته باید همین DTOها را برگرداند و UI هیچ تغییری نمی‌خواهد:

```ts
Station   { id, name, zone:'pc'|'console'|'table', type, ratePerHour, state:'free'|'busy'|'resv'|'off' }
Session   { id, stationId, state, startedAt, minutes, persons, amountSoFar, customer?, tariffId }
Customer  { id, name, alias, mobile, nationalId, username, vip:'gold'|'silver'|'none',
            wallet:number(toman-long), debt:number, giftCredit:number, discountLevel, package? }
VipPackage{ id, title, price, durationDays, dailyHourCap, overflowRule:'half-hourly'|'hourly', buffetDiscountPct, perks[] }
Product   { id, name, category, price, buyPrice, stock, maxStock }
CartItem  { productId, qty, target:{sessionId}|'standalone' }
Shift     { id, userId, openedAt, closedAt?, salesTotal }
AppUser   { id, name, role:'owner'|'admin'|'operator', permissions:string[] }
Invoice   { sessionId, timeAmount, items[], discount, rounding, finalAmount, payMethod:'cash'|'card'|'wallet' }
Settings  { viewMode:'v-card'|'v-compact'|'v-list', zonesGrouping, showLiveCost, alarms{...},
            invoice{defaultMode, autoRound, printAuto}, backup{auto, keep, target}, network{serverAddr, wol} }
```

**قاعده‌ی پول:** همه‌ی مبالغ `long` تومان (بدون float). **قاعده‌ی تاریخ:** ذخیره‌ی میلادی/UTC، نمایش شمسی فقط در لایه‌ی UI.

## ۴) مراحل اتصال UI به هسته (بدون بازنویسی)

1. حالا: UI در React با **MockService** (یک فایل، همان DTOهای بالا) کار می‌کند — دقیقاً رفتار پروتوتایپ.
2. بعد: هسته‌ی ASP.NET Core همان DTOها را با REST + SignalR می‌دهد.
3. اتصال: فقط MockService با ApiService (همان متدها: getStations, startSession, settle...) جایگزین می‌شود؛ SignalR برای تایمر زنده جای setInterval را می‌گیرد.
4. نتیجه: **صفر بازنویسی UI** — فقط منبع داده عوض می‌شود.

## ۵) چیزهایی که در پروتوتایپ تایید شده و نباید عوض شود
- گرید ۶۱ ایستگاه زون‌بندی‌شده + ۳ حالت نمایش (کارتی/فشرده/لیستی)
- کارت ایستگاه: بج وضعیت، تایمر، هزینه‌ی لحظه‌ای، نفرات، نوار پیشرفت، پالس
- مشتریان: چیدمان لیست (راست) + پروفایل (چپ)، نشان VIP، سه صندوق مالی، هشدار «لیمیت خورده»
- پکیج VIP: سیلور/گلد با سقف ساعت روزانه، مازاد نیم‌بها، روزهای باقیمانده
- بوفه: سبد با هدف «روی جلسه» یا «مستقل»، نوار موجودی با هشدار کمبود
- گزارش‌ها: نمودار با تفکیک نقد/کارت، سود خالص، کارکرد ایستگاه
- کاربران: ماتریس دسترسی صاحب/مدیر/اپراتور + شیفت با فروش
- تنظیمات: ۶ پنل با سوییچ‌های واقعی (فعلاً در MockService ذخیره، بعداً در دیتابیس)


## ۶) Primitive Controls — قرارداد مشترک فرم‌ها
از ۱۴۰۵/۱۰/۱۰ تمام `input`, `select` و `textarea`های عادی باید از استایل پایه GameNet استفاده کنند: پس‌زمینه تیره، border واحد، radius واحد، focus state سبز، placeholder استاندارد و حالت disabled مشخص. هیچ فرم جدیدی نباید به ظاهر پیش‌فرض سفید مرورگر متکی باشد.

کامپوننت‌های منطقی مشترک در ادامه:
- TextInput / NumericInput / LtrInput
- Select
- MoneyInput
- DurationInput
- SearchInput
- Modal / ConfirmModal
- Button Primary/Default/Danger/Small
- Badge / StatusPill
- DataTable
- Toast

این قرارداد جای Prototype را عوض نمی‌کند؛ فقط ظاهر کنترل‌های خام را یکدست می‌کند.
