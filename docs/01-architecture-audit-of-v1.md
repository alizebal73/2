# Architecture Audit of v1

Audit source: repository `alizebal73/2`

## Current measured hotspots

At the audited main baseline:
- `src/Server/Program.cs`: ~5,845 lines / ~236 KB
- `src/Client/Program.cs`: ~1,432 lines / ~52 KB
- `src/Dashboard/src/pages/DashboardPage.tsx`: ~1,528 lines / ~91 KB
- `src/Dashboard/src/App.css`: ~4,213 lines / ~80 KB
- `src/Server/Data/GameNetDbContext.cs`: ~722 lines / ~36 KB
- `src/Server/Hubs/AgentHub.cs`: ~871 lines / ~35 KB
- `src/Server/Data/DomainEntities.cs`: ~464 lines
- `.github/workflows/ci.yml`: ~110 KB

These are not automatically bugs. They are structural risk indicators because they combine many responsibilities and make review/verification harder.

## Current endpoint concentration

The current Program.cs contains endpoint groups for:
- notifications
- tariffs
- games
- account pool
- agents
- releases
- reports
- audit
- authentication
- users/payroll/approvals
- stations
- dashboard
- customers/VIP/login
- buffet/inventory
- debt/wallet
- finance
- shifts
- sessions

The target architecture moves each group behind its owning module.

## Current domain concentration

Domain entities for Station, Customer, Product, User, Approval, Payroll, VIP, Game, GameAccount, Agent/Client, Reservation, Session, Invoice, Payment, Wallet, Benefits, Inventory, Shift, Expense, Notification and Audit currently share broad Data-layer files.

The target architecture gives each bounded business area ownership of its own domain model.

## Current Dashboard concentration

DashboardPage currently combines:
- station grid
- live clock
- filters/grouping
- customer lookup
- session start/end
- payment
- split payment
- pause/resume
- rate/person changes
- transfers
- agent commands
- context menus
- notifications/attention behavior

The target breaks these into feature components and use-oriented hooks/services.

## Current Agent concentration

Client Program contains:
- registration
- state persistence
- heartbeat
- SignalR
- commands
- session flow
- game processes
- update lifecycle
- watchdog
- health/recovery

Target separates these concerns into services with one executable composition root.

## Architectural defects to prevent in v2

1. Endpoint logic in composition root.
2. UI-side duplication of business calculations.
3. Transport classes becoming business services.
4. Cross-module direct database access without a clear boundary.
5. Secrets crossing trust boundaries unnecessarily.
6. Unit tests being treated as sufficient proof of HTTP/runtime correctness.
7. Runtime files stored next to binaries.
8. Giant workflow scripts hiding CI logic.
