# گیم‌نت منیجر

پروژه از prototype به ساختار اجرایی مرحله‌ی ۱ منتقل شده است:

```text
GameNetManager.sln
src/Server     ASP.NET Core 10, EF Core, SQLite, SignalR
src/Dashboard  React, TypeScript, Vite
src/Client     .NET 10 SignalR console client
src/Shared     DTO contracts مشترک
tests          xUnit
docs           اسناد دامنه و معماری
prototype      طرح‌های HTML قبلی
```

## نیازمندی‌ها

- .NET SDK 10
- Node.js 22 یا جدیدتر و npm

## اجرای Development

یک بار restore و نصب ابزار EF:

```bash
dotnet restore GameNetManager.sln
dotnet tool restore
npm install --prefix src/Dashboard
```

ترمینال اول، سرور را اجرا می‌کند. در اولین اجرا migration اعمال می‌شود، SQLite در `src/Server/App_Data/gamenet.development.db` ساخته می‌شود و ۶۱ ایستگاه seed می‌شوند.

```bash
dotnet run --project src/Server
```

ترمینال دوم، داشبورد React را اجرا می‌کند:

```bash
npm run dev --prefix src/Dashboard
```

آدرس dashboard: `http://localhost:5173`؛ درخواست‌های `/api` و `/hubs` از proxy Vite به سرور روی پورت `5080` می‌روند. APIها: `/api/health` و `/api/dashboard`؛ SignalR Hub: `/hubs/dashboard`.

برای اتصال کلاینت .NET در ترمینال سوم:

```bash
dotnet run --project src/Client
```

آدرس سرور کلاینت را می‌توان با متغیر `GAMENET_SERVER_URL` تغییر داد.

## Production و Migration

خروجی React به `src/Server/wwwroot` ساخته می‌شود و در Production توسط ASP.NET Core سرو می‌شود:

```bash
npm run build --prefix src/Dashboard
ASPNETCORE_ENVIRONMENT=Production dotnet run --no-launch-profile --project src/Server
```

سرور migrationهای موجود را هنگام startup اعمال می‌کند. برای ساخت migration بعدی:

```bash
dotnet tool run dotnet-ef migrations add MigrationName --project src/Server --startup-project src/Server --output-dir Data/Migrations
```

Development و Production از فایل‌های SQLite جدا استفاده می‌کنند. از رمزها یا داده‌های prototype برای محیط واقعی استفاده نکنید.

## بررسی

```bash
dotnet test GameNetManager.sln
npm run build --prefix src/Dashboard
```

صفحه‌های prototype برای مقایسه‌ی طراحی همچنان در `prototype/index.html` و `prototype/client.html` موجودند.