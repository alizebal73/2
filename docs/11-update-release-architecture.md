# قرارداد معماری Update / Release — GameNet Manager

این سند مرجع معماری انتشار و به‌روزرسانی پروژه است. هدف آن ساختن Updater کامل در همین لحظه نیست؛ هدف این است که از همین مرحله هر تغییر طوری ساخته شود که بعداً بدون شکستن داده و کلاینت‌ها قابل انتشار، تشخیص و برگشت باشد.

## قرارداد نسخه
- ProductVersion: نسخه واقعی محصول
- SchemaVersion: آخرین Migration اعمال‌شده
- ApiContractVersion: نسخه قرارداد API
- MinimumClientVersion: کمترین نسخه Agent/Client مجاز
- RecommendedClientVersion: نسخه پیشنهادی

Server این مقادیر را از /api/release/manifest ارائه می‌کند و مقدار پیش‌فرض امن دارد.

## سازگاری API
تغییرات additive تا جای ممکن بدون تغییر ApiContractVersion منتشر می‌شوند. /api/v2 فقط برای Breaking Change واقعی استفاده می‌شود.

## دیتابیس
قبل از Migration واقعی باید Backup قابل‌بازیابی وجود داشته باشد، سپس Migration، Health Check و Smoke انجام شود. حذف خودکار دیتابیس در خطای Migration ممنوع است و مسیر Recovery/Restore باید مشخص باشد.

## چرخه Release
Build → Unit/Integration Test → Migration Smoke → Dashboard Build/Lint → E2E → Manifest/Package → Canary → Health Check → انتشار عمومی.

فعلاً Installer/Package عملیاتی به GitHub Upload نمی‌شود و خروجی نهایی روی مسیر محلی Build نگه‌داری می‌شود.

## Update Client
در مراحل بعدی Verify Manifest/Package، دانلود امن، نصب Atomic یا نسخه مستقل، نگه‌داری نسخه سالم، Rollback خودکار، Restart کنترل‌شده، Canary محدود و ثبت وضعیت Update هر PC الزامی است.

پیاده‌سازی اجرایی Update/Rollback در مرحله ۱۰ و hardening انتشار در مرحله ۱۴ است.

## قانون اصلی
Build سبز شرط لازم است، نه تضمین کافی؛ Release Gate باید Build/Test + Migration + Smoke/E2E + Version Compatibility + Health/Rollback را پوشش دهد.
