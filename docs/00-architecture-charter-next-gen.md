# Architecture Charter — GameNet Manager Next Generation

## 1. تصمیمات قطعی

### Architecture
- Modular Monolith
- Vertical Slice / Feature-oriented organization
- Dependency boundaries داخل هر Module
- عدم مهاجرت به Microservices مگر با نیاز واقعی

### Core Runtime
- ASP.NET Core / .NET LTS
- React + TypeScript برای Dashboard
- Windows Agent برای PC
- SignalR برای realtime
- SQLite در نسل اول، با طراحی Persistence قابل انتقال به PostgreSQL

### Authority
Server تنها Source of Truth است.
- مالی
- Session
- Customer Login
- Station Ownership
- Game Launch authorization
- Account Lease
- Inventory
- Audit

هیچ Client یا Dashboard مجاز به تصمیم مستقل درباره state حساس نیست.

## 2. Module Map

- Identity & Access
- Customers
- Stations
- Sessions
- Billing & Finance
- Inventory / Buffet
- Games & Account Pool
- Agents
- Reporting
- Operations
- Notifications

## 3. Rules

1. Domain نباید به EF Core، ASP.NET یا Dashboard وابسته باشد.
2. UI نباید Business Rule را دوباره پیاده‌سازی کند.
3. Endpoint باید Authentication/Authorization مشخص داشته باشد.
4. تغییر مالی باید Transaction + Ledger/Audit داشته باشد.
5. زمان در persistence به UTC ذخیره و در UI به فارسی نمایش داده شود.
6. پول با integer minor unit مناسب پروژه ذخیره شود؛ برای تومان فعلی integer-safe design ترجیح داده می‌شود.
7. هر Feature دارای Unit + Integration + E2E coverage متناسب با ریسک باشد.
8. Realtime transport از Business Logic جدا باشد.
9. Runtime data داخل install directory قرار نگیرد.
10. Secrets داخل Git، bundle یا Dashboard قرار نگیرند.

## 4. Target Backend Shape

```
Server.Host/
BuildingBlocks/
Modules/
  Identity/
  Customers/
  Stations/
  Sessions/
  Billing/
  Inventory/
  Games/
  Agents/
  Reporting/
  Operations/
Agent/
ClientShell/
Dashboard/
Tests/
Installer/
```

هر Module ترجیحاً:

```
Domain/
Application/
Api/
Infrastructure/
```

را دارد، اما تا زمانی که واقعاً نیاز نباشد به چندین Assembly کوچک تقسیم نمی‌شود.

## 5. Composition Root

Program.cs نسل جدید فقط مسئول:
- configuration
- dependency injection
- middleware
- authentication
- database
- realtime
- module registration
- host lifecycle

است.

هیچ Business Workflow بزرگی نباید در Program.cs باقی بماند.

## 6. Agent Boundary

Agent یک Windows Service مستقل از Client UI است.

```
Client Shell
  -> local IPC
Agent Service
  -> SignalR/HTTPS
Server
```

Agent مسئول OS-level actions است؛ Client Shell مسئول تجربه مشتری.

## 7. Testing Pyramid

```
Domain Unit
    ↓
Module Integration
    ↓
HTTP/API Integration
    ↓
Browser E2E
    ↓
Real Agent / Real PC
```

سبز شدن Unit Test به‌تنهایی Release Approval محسوب نمی‌شود.

## 8. Security Baseline

- AuthN/AuthZ by default
- explicit permissions
- rate limiting for sensitive login endpoints
- CSRF/antiforgery for browser cookie APIs where applicable
- protected client identity
- secrets out of repository
- audit on sensitive mutations
- least privilege
- secure error contract
