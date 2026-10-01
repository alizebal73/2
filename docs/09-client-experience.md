# سند تجربه کاربری کلاینت — Client Experience UX
> قرارداد UX کلاینت مشتری برای نسخه React فعلی و پوسته نهایی WPF/Agent. این سند جایگزین رفتارهای واقعی Server/Agent نیست؛ فقط قرارداد تجربه و محل نمایش عملیات است.

## 1. هدف
کلاینت برای مشتری است، نه اپراتور. صفحه باید هنگام بازی کم‌مزاحمت، سریع و قابل فهم باشد:
- مرکز صفحه: Game Library
- هدر: PC، زمان باقی‌مانده، موجودی و حساب
- پایین صفحه: Dock هوشمند
- عملیات مدیریتی: پنهان و محدود به مسیر اپراتور/Agent

## 2. اصل Dock
Dock پایین صفحه ثابت است ولی باید کم‌ارتفاع و کم‌مزاحمت بماند.
- Hover روی آیتم‌های دارای Drawer، بعد از تأخیر کوتاه، Drawer را باز می‌کند.
- حرکت موس از آیتم به Drawer باید Drawer را باز نگه دارد.
- خروج موس، بعد از تأخیر کوتاه، Drawer را می‌بندد.
- کلیک روی آیتم Drawer را قفل می‌کند.
- Escape یا کلیک بیرون Drawer را می‌بندد.
- Dock نباید هنگام بازی فضای قابل استفاده را دائماً اشغال کند.

## 3. Drawer نرم‌افزارها
«نرم‌افزارها» نمونه اصلی این الگو است.
Drawer به شکل یک مستطیل افقی کم‌ارتفاع از پایین Dock بالا می‌آید و برنامه‌های مجاز را نشان می‌دهد:
- Chrome
- Discord
- Telegram
- Music
- OBS
- Calculator

هر برنامه فقط یک Entry در Client Policy است. Client UI صرفاً درخواست اجرای آن را می‌دهد؛ اجرای واقعی توسط Agent و طبق Policy/Permission انجام می‌شود.

## 4. Game Library
مرکز صفحه برای بازی‌هاست:
- Card / Compact / List
- Zoom
- Search و Category در مسیر تکامل بعدی
- Cover واقعی از Game Library
- Hover روی کارت: Quick Actions محدود
- Click: Launch
- Account Picker برای بازی‌های دارای Account Pool
- Running badge و Now Playing

## 5. Quick Action در کارت بازی
Quick Action نباید جای Context Menu را بگیرد. فقط عملیات پرکاربرد و کم‌خطر:
- اجرا
- علاقه‌مندی
- اطلاعات/جزئیات
برای عملیات حساس، Context Menu یا مسیر تأیید استفاده شود.

## 6. Now Playing
وقتی بازی فعال است:
- نام بازی
- وضعیت اجرا
- کنترل توقف در صورت مجاز بودن
نمایش داده شود.
در حالت Fullscreen/فعالیت بازی، UI باید تا حد ممکن مخفی شود.

## 7. Client Actions
قابلیت‌های اصلی:
- Login / Guest
- زمان زنده
- Wallet / Free Money / Free Time / Debt
- Account / Package
- Game Launch
- Account Pool
- Buffet Request
- Charge Request
- Operator Message
- Move Request
- Internet Status
- Lock / Logout

عملیات واقعی Restart/Shutdown/Screenshot/Network باید فقط بعد از Agent + Server Command + Permission + Audit فعال شوند.

## 8. Operator Path
ورود اپراتور و آزادسازی سیستم از مسیر کلاینت مجاز است، ولی این‌ها باید به Server/Agent متصل شوند و PIN/Permission دمو نهایی نباشند.
کلاینت نباید خودش منبع حقیقت مالی، Session یا Permission باشد.

## 9. Server Dashboard — Quick Actions
در داشبورد اپراتور:
- کارت ایستگاه باقی می‌ماند.
- Hover کارت می‌تواند چند Quick Action پرکاربرد را نشان دهد.
- Right-click همچنان منوی کامل Action Map را باز می‌کند.
- Quick Action نباید منوی کامل را حذف کند.
- عملیات مالی/حساس همچنان از مسیرهای مجاز، Permission و Audit عبور می‌کنند.

پیشنهاد Quick Action:
- شروع جلسه برای ایستگاه آزاد
- تسویه برای جلسه فعال
- Pause/Resume
- تمدید
- «بیشتر» برای Context Menu

## 10. محدودیت‌های طراحی
- انیمیشن‌ها کوتاه و سبک
- بدون پنل‌های متعدد هم‌زمان
- بدون منوهای تو‌در‌تو
- بدون سفیدشدن کنترل‌ها
- RTL و Vazirmatn
- هماهنگ با توکن‌های `docs/05-ui-design-system.md`
- قابلیت Mock نباید در نسخه نهایی به‌عنوان عملیات واقعی نمایش داده شود.

## 11. مسیر ارتقا
1. UX/Dock/Drawer در Prototype React
2. تست بصری و تعامل
3. اتصال به Client Agent
4. اتصال به Server Commands/SignalR
5. حذف رفتارهای Mock
6. انتقال پوسته به WPF/WebView2 در فاز نهایی
