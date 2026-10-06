# GameNet Manager Release Installer

این پوشه تعریف بسته‌بندی Release فعلی است.

خروجی:
- Server Setup: self-contained Windows x64
- Client Setup: self-contained Windows x64

Server Setup:
- مسیر نصب قابل انتخاب
- مسیر Data Root قابل انتخاب
- Windows Service با نام GameNet Manager Server
- TCP 5080 برای LAN
- اجرای Server با NetworkService
- نگهداری داده Runtime خارج از install directory
- دریافت admin password و Agent registration token هنگام نصب

Client Setup:
- مسیر نصب قابل انتخاب
- Server URL قابل انتخاب
- Agent Registration Token قابل انتخاب
- Agent name قابل انتخاب
- اجرای Agent هنگام logon کاربر

Build prerequisite:
- .NET 10 SDK
- Node.js/npm
- Inno Setup 7.x / ISCC.exe

نسخه فعلی بررسی‌شده Inno Setup: 7.1.0.

## روش فعلی پروژه — Setup-First

نسخه فعلی قبل از هر Release نهایی با این ترتیب جلو می‌رود:

1. ساخت Server Setup و Client Setup
2. نصب واقعی Server
3. نصب واقعی Client روی PC جدا
4. تست registration / Station / Customer / Session
5. ثبت باگ واقعی
6. اصلاح component مسئول
7. تولید Local Update برای همان component
8. تست Update روی نسخه نصب‌شده

Setup اولیه «Final Release» نیست؛ First Installable Build است.

مقصد اصلی Update در فاز اول local/intranet است و Cloud URL بعداً اضافه می‌شود.

داده‌های Database و DataRoot نباید با Update نرم‌افزار حذف شوند.
