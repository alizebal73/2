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