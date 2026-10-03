# Stage 13 — Reporting & Audit Completion

## مرجع معماری
Stage 13 هیچ منبع داده مستقلی ندارد. همه گزارش‌ها از Server truth خوانده می‌شوند:
`Dashboard → REST → Permission → ReportingService → SQLite → Result`.

## تکمیل‌شده
- گزارش خلاصه درآمد/هزینه/سود عملیاتی/تعداد Session/فاکتور/مشتری.
- گزارش عملکرد هر Station با دقیقه صورتحساب‌شده و درآمد.
- Heatmap روز/ساعت بر اساس Session واقعی.
- گزارش Customer/VIP با مصرف VIP فقط در Activation/Expiry.
- گزارش Operator/Shift.
- Audit Explorer با فیلتر زمان/کاربر/عملیات/موجودیت/جستجو.
- Finance CSV export از Server.
- Permissionهای `reports.view` و `reports.export`.
- Dashboard ReportsPage از mock گزارش استفاده نمی‌کند.
- تست Server برای aggregation واقعی گزارش اضافه شده است.

## قرارداد ضد خطای تکراری
- Event/Mock منبع حقیقت نیست.
- DateTimeOffset جدید قبل از اضافه‌شدن query باید با SQLite translation ممیزی شود.
- Report فقط از stateهای persisted ساخته می‌شود.
- Export نیز Server-side permission دارد.

## Gate
Stage 13 از نظر implementation بسته است؛ Done نهایی فقط با Build/Test/EF/Server/Dashboard روی exact SHA معتبر است.
