# Next Generation Roadmap

## Gate 0 — Release Freeze
Do not start architectural migration before:
- Final Setup exists
- Server Setup installed on real Server
- Client Setup installed on real client PC(s)
- Stage 14.6 physical validation passed
- Release approval granted

Tag current product:
`v1.0.0-release`

Freeze `alizebal73/2` as the stable production line.

## Gate 1 — Create independent repository
Create:
`alizebal73/gamenet-manager-next`

Initial content:
- architecture charter
- module map
- migration rules
- compatibility rules
- acceptance test catalog

No copy-paste of the whole old codebase yet.

## Gate 2 — Empty architecture skeleton
Create:
- Server.Host
- BuildingBlocks
- Modules
- Agent
- ClientShell
- Dashboard
- Tests
- Installer

Build must be green before feature migration.

## Gate 3 — Architecture guardrails
Add:
- dependency architecture tests
- API auth/permission gate tests
- standard ProblemDetails contract
- logging/trace correlation
- test categories
- migration discipline

## Gate 4 — First vertical slice
Start with Stations.

Deliver end-to-end:
- Domain
- Application
- Persistence
- API
- Authorization
- Dashboard
- Realtime
- tests

Do not migrate everything before validating the pattern.

## Gate 5 — Identity & Access
Migrate:
- users
- roles
- permissions
- operator sessions
- approvals
- audit
- authentication

## Gate 6 — Customers
Migrate:
- customer profile
- customer login
- VIP packages
- wallet
- benefits
- debt
- history

## Gate 7 — Sessions & Pricing
Migrate:
- Session state machine
- timing
- pause/resume
- transfer
- time adjustment
- pricing engine
- concurrent ownership
- recovery

## Gate 8 — Billing & Finance
Migrate:
- invoice
- invoice item
- payments
- settlement
- refunds
- reversals
- shifts
- expenses
- ledger
- discount/free-benefit rules

## Gate 9 — Inventory/Buffet
Migrate:
- product
- stock
- inventory transactions
- sales
- purchase
- waste
- return
- profit reports

All stock mutations atomic.

## Gate 10 — Games & Account Pool
Migrate:
- game catalog
- targeting
- account pool
- lease
- credential protection
- server authorization

## Gate 11 — Agent
Separate:
- registration
- identity
- connection
- heartbeat
- commands
- session ownership
- game process ownership
- lock/kiosk
- update
- rollback
- diagnostics

## Gate 12 — Client Shell
Separate client UI from Windows Agent:
- login
- remaining time
- game catalog
- status
- local IPC

## Gate 13 — Dashboard reconstruction
Split large pages into feature modules:
- dashboard
- stations
- sessions
- customers
- billing
- buffet
- games
- accounts
- reports
- users
- settings
- client shell

Server state and UI state become separate.

## Gate 14 — Reporting & Operations
Move:
- reports
- notifications
- backups
- settings
- diagnostics
- release/update management

## Gate 15 — Installer
Build:
- Server Setup
- Client Setup
- selectable install paths
- separate runtime data root
- Windows Service registration
- firewall rule
- secret provisioning
- health validation
- safe uninstall/upgrade

## Gate 16 — Data migration
From v1 to v2:
1. backup
2. schema migration
3. row counts
4. financial totals
5. wallet balances
6. inventory balances
7. session state
8. audit history
9. reconciliation report

## Gate 17 — Dual validation
Run v1 and v2 against the same controlled scenarios and compare:
- sessions
- time
- pricing
- invoices
- payments
- wallet
- debt
- inventory
- reports
- station state

## Gate 18 — v2 release
Only after:
- CI green
- integration green
- E2E green
- installer green
- migration green
- physical validation green

Tag:
`v2.0.0`

## Migration Rule

First preserve behavior, then improve behavior.

```
v1 Accepted Behavior
    ↓
Acceptance Tests
    ↓
v2 Implementation
    ↓
Parity Validation
    ↓
Improvement
```
