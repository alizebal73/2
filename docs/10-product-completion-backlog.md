# Product Completion Backlog — تکمیل جامع GameNet Manager

این فایل فهرست مرجع مواردی است که در مرور جامع محصول شناسایی شده‌اند. ترتیب اجرا وابستگی‌ها را رعایت می‌کند و هر مورد فقط پس از پیاده‌سازی + تست به وضعیت Done می‌رسد.

## قوانین اجرا
- همه متن‌های قابل مشاهده کاربر فارسی و قابل‌فهم باشند.
- Server تنها منبع حقیقت است.
- Mock نباید در نسخه نهایی به‌عنوان قابلیت واقعی نمایش داده شود.
- هر عملیات حساس: Permission + Persistence + Audit + خطای فارسی + Reverse/Cancel در صورت نیاز.
- هر مرحله قبل از رفتن به مرحله بعد باید Build/Test/CI و در صورت UI، تست تعاملی لازم را پاس کند.
- وابستگی‌ها زودتر از مصرف‌کننده پیاده‌سازی می‌شوند.

## فهرست اصلی

### A) Operational Intelligence / UX
1. مرکز «نیازمند توجه» در داشبورد
2. جست‌وجوی سراسری و مرکز جست‌وجو/فرمان
3. پنل جزئیات جلسه (Session Center)
4. Timeline کوتاه ایستگاه
5. عملیات اخیر اپراتور
6. Error UX استاندارد: خطا → معنی → اقدام بعدی
7. Approval Flow برای عملیات حساس
8. Undo UX برای عملیات برگشت‌پذیر (Reverse واقعی، بدون حذف رکورد)

### B) Finance / Session
9. Wallet Ledger واقعی
10. Refund / Reverse واقعی
11. Settlement کامل + Breakdown + Why this amount?
12. Split Payment
13. Shift Settlement + Shift Handover
14. Session Transfer / Change Tariff / Change Persons
15. Free Time + Free Money کامل
16. Concurrent Login Limit
17. Expense / Profit واقعی و قابل ممیزی

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

## وضعیت
تمام موارد ابتدا «باز» هستند. هر مورد فقط پس از پیاده‌سازی و تست واقعی به Done تغییر می‌کند.
