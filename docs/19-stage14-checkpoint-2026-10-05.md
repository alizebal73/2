# Stage 14 — Checkpoint 2026-10-05

## Main certified baseline

- ✅ Stage 14.1 — Settings Information Architecture / UI Hardening
- ✅ Exact-head CI: Run #1429
- ✅ All CI gates green, including Dashboard Browser Smoke
- ✅ PR #36 merged into main
- ✅ Certified merge commit: `4bc6cd4484647eb6eb029aef53c947149f9e1da9`

## Slice 14.1 — COMPLETE

### Scope delivered

- Settings category navigation and searchable IA
- Stable anchors for settings sections
- Explicit local-only settings notice; no fake Server-backed Settings capability
- Browser Smoke for search, category navigation and unique anchors
- Notification/settings E2E selectors hardened against strict-mode ambiguity

### Gates

- ✅ Build
- ✅ .NET Tests
- ✅ EF validation
- ✅ Server/Migration/Health Smoke
- ✅ Dashboard Lint/Build
- ✅ Dashboard Preview
- ✅ Dashboard Browser Smoke

## Slice 14.2 — COMPLETE

**Dashboard PC grouping by Internet 1/2 + dense responsive hardening**

### Delivered

- ✅ `Station.Network` persisted server-side with EF migration and model snapshot.
- ✅ Dashboard `GET /api/dashboard` returns the authoritative Station.Network value.
- ✅ Demo seed distributes PC-01..20 to Internet 1 and PC-21..40 to Internet 2.
- ✅ Dashboard grouping renders explicit Persian labels for Internet 1 / Internet 2.
- ✅ Existing Status / VIP-Normal / Remaining Time grouping options preserved.
- ✅ Grouping/filtering remains presentation-only and does not alter server truth or session state.
- ✅ Responsive hardening covers workspace, toolbar and dense station cards down to the tested 520px viewport.
- ✅ Browser Smoke covers Internet grouping, grouping/filter switching and responsive critical path.
- ✅ Exact-head CI: Run #1435.
- ✅ Main certification: Run #1436, Attempt 2.
- ✅ Certified merge commit: `63ac3f67357a0970df504a62c9ab16cc1ba4f53a`.

### Not Done

- Server-backed Settings
- Physical 2–3 PC validation
- Real Backup/Restore
- Stage 14.3 accessibility / operational polish

## Recovery Rule

Resume from certified main commit `63ac3f67357a0970df504a62c9ab16cc1ba4f53a`. Do not infer completion from chat history.

## Next

Start **Stage 14.3 — Accessibility / UI / operational polish** from the certified main baseline above.


## Slice 14.3 — COMPLETE

**Accessibility / UI / operational polish**

### Delivered

- ✅ Skip link and keyboard entry to main content
- ✅ Active navigation semantics via aria-current
- ✅ Accessible API / SignalR status announcements
- ✅ Notification expanded state and labelled region semantics
- ✅ Reduced-motion support
- ✅ Last-valid-snapshot messaging when live API data is unavailable
- ✅ Browser Smoke for the new accessibility and stale-data paths

### Gates

- ✅ Exact-head CI: Run #1439
- ✅ Main certification: Run #1440
- ✅ Certified merge commit: b69fa15c7ea166a65c5932f8ceb11fb34429eb4e

## Slice 14.4 — COMPLETE

**Server-backed Settings**

### Delivered in current branch

- ✅ Server-persistent AppSetting global scope with EF migration + model snapshot.
- ✅ Independent settings.view / settings.manage permissions.
- ✅ GET/PUT /api/settings with type/range validation and Persian errors.
- ✅ Atomic save path with AuditLog for changed settings.
- ✅ Dashboard server synchronization and explicit server-save action.
- ✅ Hotkeys and UX section locks remain explicitly local rather than being falsely presented as global server state.
- ✅ Server API Smoke and Browser Smoke added for the new path.

### Gates

- ✅ Exact-head CI: Run #1445 on exact Stage 14.4 head `5c6b4575ec46f387e70e715d87ff79e340cf7c14`.
- ✅ Main certification: Run #1446 on merge commit `b3a749db8974a7f75cedc0e9168bff94fc38cad9`.
- ✅ All CI gates green, including EF model validation, Server startup/migration smoke, Dashboard build/lint/preview and Browser Smoke.

## Slice 14.5 — COMPLETE

**Real Backup / Restore**

### Delivered

- ✅ SQLite Online Backup-based database archive.
- ✅ Database + DataProtection Keys packaged together.
- ✅ SHA-256 + SQLite integrity verification.
- ✅ Retention, automatic daily schedule and manual backup endpoint.
- ✅ Separate backup permissions and audited operations.
- ✅ Pending Restore applied during Server startup before EF Migration, with a pre-restore safety archive.
- ✅ Settings UI connected to manual backup, Verify and Restore preparation.
- ✅ Unit coverage for Create/Verify/Prepare/Apply restore.
- ✅ Startup path for Pending Restore is covered in the real Server code path and the restore lifecycle is unit-tested end-to-end.
- ℹ️ The certified CI workflow was deliberately kept unchanged after the Stage 14.5 implementation; it does **not** include a dedicated restart-based Pending Restore smoke step.

### Gates

- ✅ Exact-head CI: Run #1461 on d9304b271321f22d9323553fbb017eb3effcdf6f.
- ✅ Main certification: Run #1462 on merge commit 7dc7d4b8b7cff2e02c4eb302f024161d5ed05049.
- ✅ All CI gates green, including Build/Test, EF validation, Server startup/migration, Dashboard Build/Lint/Preview and Browser Smoke.

## Slice 14.6 — SOFTWARE PRE-FLIGHT COMPLETE / PHYSICAL GATE PENDING

**Client Experience + Server/Agent boundary hardening**

### Software pre-flight delivered

- ✅ Server-backed Client Catalog for real Game/Buffet metadata.
- ✅ Customer Identity/State bound to real Server state; authoritative Lock state is surfaced to Client.
- ✅ Client requests (message/charge/move/unlock/buffet) are Server-backed and audited.
- ✅ Lock / Logout-Lock use the real Agent command path.
- ✅ Real Game Launch/Stop path: Client → Server → Agent → OS Process.
- ✅ Server validates CustomerLogin + Session + Station + Game + Agent ownership.
- ✅ Agent re-validates live Session immediately before launching a process.
- ✅ Client process ownership is keyed by SessionId + GameId; cross-session Stop is rejected.
- ✅ Dashboard fake billing/approval paths removed; operator discount limit is enforced Server-side.
- ✅ Exact-head CI: Run #1528 on `8ab0b642f7166f214fd52f94cf1005862e8b00b1`.
- ✅ Main certification: Run #1529 on merge commit `bf88dd1d91875d12d33c58607776d5e928279bfc`.

### Physical Gate

- ✅ Protocol: `docs/20-physical-validation-2-3pc-2026-10-05.md`
- 🟡 Physical execution pending
- ⬜ PC-01 + PC-02 PASS
- ⬜ PC-03 optional PASS
- ⬜ Stage 14.6 certified

## Next

ساخت Setup/Installer فقط بعد از این Software Pre-flight انجام می‌شود؛ Gate بعدی برای محصول، اجرای واقعی روی 2–3 PC است و سپس controlled 40+ PC rollout.


## Slice 14.7 — F1 Customer Workspace + Station Right-click Completion

### Delivered in main after the 2026-10-05 software pre-flight

- ✅ Existing Dashboard station right-click menu preserved; it was **not** rebuilt as a parallel menu.
- ✅ Right-click now also exposes Agent **Ping**, **Restart Client** and **Shutdown Client** where the required permission and online Agent state allow it.
- ✅ Restart/Shutdown remain Server-authorized through the existing `client.power` permission and Agent command transport; the UI does not fake local power control.
- ✅ Offline Agent commands are hidden from the right-click menu instead of presenting actions that cannot execute.
- ✅ Right-click closes safely on outside click, Escape, resize or scroll and clamps to the viewport.
- ✅ F1 keeps the existing Server-backed customer financial operations and now loads the customer's real Server history into the workspace.
- ✅ After each F1 financial operation, the customer summary and recent history are refreshed from Server instead of falling back to the initial snapshot.
- ✅ Browser Smoke added for F1 customer selection/history and the right-click Agent Ping path, including the new Restart/Shutdown menu visibility.

### Validation gate

- 🟡 Code/test changes committed to `main`.
- 🟡 Self-hosted Windows Runner execution pending/required for Build, Dashboard lint/build and Browser Smoke certification.
- ⬜ Main certification for Slice 14.7.

### Boundary

This slice is a completion/hardening pass over existing Stage 14 functionality. It does not replace the existing Session Center, Agent command transport, permission model, or Server-as-source-of-truth architecture.


## Slice 14.8 — Dashboard Station Sort Strip

- ✅ Added compact station sort row to the main dashboard workspace.
- ✅ Criteria: رایانه، شناسه، نام خانوادگی، زمان باقی‌مانده، بدهکاری، توضیحات، وضعیت رایانه.
- ✅ Active criterion is visually highlighted.
- ✅ Re-clicking the active criterion toggles ascending/descending order.
- ✅ Sorting is applied to the visible station collection and remains consistent across card, compact and list views.
- ✅ When PC grouping is enabled, the selected sort order is preserved inside the groups.
- ✅ Shift-range selection follows the current sorted visible order.
- ✅ Browser Smoke added for all seven sort controls, direction toggle and list view interaction.
- 🟡 Self-hosted Windows Runner certification pending for this slice.

## Slice 14.9 — Buffet Warehouse → Showcase → Sale Flow

### Delivered

- ✅ Buffet inventory is now explicitly separated into warehouse stock and showcase stock.
- ✅ Existing pre-split product stock is migrated into showcase stock so existing sellable inventory is not lost; warehouse stock starts at zero for those legacy rows.
- ✅ New purchases and initial stock enter the warehouse.
- ✅ A dedicated atomic transfer-to-showcase operation moves quantity from warehouse to showcase and records paired inventory transactions.
- ✅ Buffet sales consume showcase stock only; warehouse stock cannot silently satisfy a sale.
- ✅ One-click `+ ویترین` moves one unit from warehouse to showcase.
- ✅ Buffet UI shows warehouse and showcase quantities per product.
- ✅ Daily server-backed per-product sales report added, including quantity and revenue.
- ✅ Inventory history now records whether a movement belongs to Warehouse or Showcase.
- ✅ Existing sale/inventory audit and transaction persistence remain Server-authoritative.
- ✅ Browser Smoke added for warehouse/showcase separation, one-click transfer and today's sales display.

### Boundary

This slice intentionally preserves the existing Product/Invoice model and adds a stock-location boundary rather than creating a parallel buffet system. Sales, inventory changes, audit and today's report continue to derive from Server state.

### Validation

- 🟡 Changes committed to `main`.
- 🟡 Self-hosted Windows Runner Build/Test/Browser Smoke certification still required.
## Slice 14.10 — Buffet Inventory Anti-Abuse / Operator Boundary

- ✅ Buffet sales remain available through `buffet.sell`.
- ✅ Inventory mutation is now Server-side restricted to `Admin` / `Owner`; old `buffet.inventory` permission cannot bypass this boundary.
- ✅ Product creation, editing, stock adjustment and warehouse→showcase transfer all use the same primary-manager authorization gate.
- ✅ Operator UI hides purchase, edit, warehouse add/remove, waste, return and showcase replenishment controls.
- ✅ Operator product API response does not expose real warehouse quantity or purchase cost.
- ✅ Inventory transaction history endpoint is also restricted to the primary-manager boundary.
- ✅ Unit regression test covers Operator/Manager denied and Admin/Owner allowed.
- ✅ Browser Smoke covers operator buffet view and absence of inventory mutation controls.
- 🟡 Self-hosted Windows Runner Build/Test/Browser Smoke certification required.

## Slice 14.11 — Pending Payment / Open Customer Account — 2026-10-06

- ✅ مشخصات قطعی Product Completion برای حساب باز مشتری ثبت شد.
- ✅ مدل مورد تأیید: یک مشتری = یک Draft Invoice باز مرتبط با Session؛ یک کارت/ردیف فشرده؛ Viewهای Card/Compact/List؛ شاخه‌های بازشونده برای شارژ، بوفه و محاسبه.
- ✅ قرار است شارژهای زمان به‌صورت Server-backed ledger مجزا از Session.PrepaidAmount ثبت شوند تا چند شارژ قابل ردیابی باشند.
- ✅ «پرداخت بعداً» Session را نهایی و Station را آزاد می‌کند ولی Invoice را تا زمان پرداخت Draft نگه می‌دارد.
- ✅ خرید بوفه روی Pending Invoice همان مشتری تجمیع می‌شود و کارت موجود را Update می‌کند.
- ⬜ پیاده‌سازی و تست این Slice.
- ⬜ self-hosted Windows Runner certification.


## Slice 14.11 Implementation Checkpoint — 2026-10-06
- ✅ Feature implementation now spans Server settlement/account flow, Dashboard pending-payment UI, and Buffet-to-pending-account sales.
- ✅ Compact view is intentionally dense; Card/Compact/List remain available in the same dashboard section.
- ✅ Expandable branches expose charges, grouped buffet items and server-derived calculation details without taking over the page.
- ⬜ Self-hosted Runner certification is still pending. The latest GitHub Actions runs for the pushed commits finish with zero jobs, so no build/test/browser result is being treated as green.


## Slice 14.11 Final Model Hardening — 2026-10-06
- ✅ مدل نهایی Pending به Customer-level Open Account اصلاح شد.
- ✅ چند Session/PC برای یک مشتری می‌توانند داخل همان Pending Account جمع شوند.
- ✅ Debt API نیز به همان Customer Account متصل شد و پرداخت‌های قبلی از مبلغ قابل پرداخت کم می‌شوند.
- ✅ Buffet Pending اکنون به Customer Account بدون وابستگی اجباری به یک Session متصل می‌شود.
- ✅ Migration برای ادغام Draftهای قبلی بدون از دست دادن child references اضافه شد.
- ⬜ Self-hosted Runner certification هنوز pending است.


## Slice 14.11 Finalization — Customer Account + Session Allocation — 2026-10-06
- ✅ Customer Account is the single Draft account for a customer's open balance.
- ✅ Live Session charges can post into that account while retaining SessionId allocation.
- ✅ InvoicePayments now carry optional SessionId, so a settlement can pay only the selected Session's due without consuming another Session's balance.
- ✅ A Session settlement keeps the Customer Account Draft when another Session for the same customer is still active.
- ✅ Multiple sessions/PCs, debts and buffet items can coexist on the same Customer Account.
- ✅ Pending calculation now includes time, buffet, other charges/debts, reductions, prepaid usage and remaining amount.
- ✅ Existing draft customer/debt/completed-session records are consolidated by the new database migration without dropping child financial references.
- 🟡 Build/Test/Browser Smoke certification is still blocked by the repository's self-hosted runner: current workflow runs complete with zero jobs.


## Slice 14.12 — Unified Customer Operations + Debt Handoff — 2026-10-06

### قرارداد محصول
- ✅ دوبار کلیک روی PC مشغول/متوقف، همان Workspace عملیات مشتری را باز می‌کند؛ مسیر F1 نیز به همین Workspace می‌رسد.
- ✅ Workspace دو تب عملیاتی «شارژ» و «بوفه» دارد.
- ✅ در Session فعال، تب شارژ مستقیماً «شارژ زمان همین جلسه» را با نقد/کارتخوان/کیف پول ثبت می‌کند.
- ✅ محصولات بوفه از Server خوانده می‌شوند؛ دوبارکلیک روی هر محصول یک واحد را به انتخاب‌های مشتری اضافه می‌کند و دکمهٔ نهایی همهٔ اقلام را یکجا روی Server ثبت می‌کند.
- ✅ مقصد بوفه هوشمند است: Session فعال ← همان Session/Account، حساب باز ← همان Customer Account، بدون Session و بدون حساب باز ← با تأیید اپراتور حساب جدید مشتری.
- ✅ برای یک مشتری، تفاوت مسیر UI باعث ساخت Invoice دوم نمی‌شود.
- ✅ Pending در پایان شیفت می‌تواند با دکمهٔ «بدهی» به Debt تبدیل شود، بدون ساخت Invoice دوم.
- ✅ Pending فقط Customer Accountهای حالت PendingPayment را نشان می‌دهد؛ Debt در پروفایل/بخش بدهی مشتری دیده می‌شود.
- ✅ مبلغ بدهی مشتری دیگر از حساب‌های PendingPayment یا Session فعال محاسبه نمی‌شود.
- ✅ تمام تغییرات حساس Server-authoritative و Audit-friendly باقی می‌مانند.

### قرارداد فنی
- Invoice.AccountState دو حالت PendingPayment و Debt را از هم جدا می‌کند.
- Migration جدید 20261006190000_CustomerAccountDebtState.
- Endpoint جدید POST /api/pending-settlements/{invoiceId}/mark-debt.
- مقصد جدید customer برای فروش بوفه، با تشخیص Server-side حساب/Session مشتری.
- Dashboard Pending دکمهٔ «بدهی» دارد.
- Workspace مشتری، شارژ Session و بوفه را در یک مسیر اپراتوری یکپارچه می‌کند.

### Gate
- ⬜ Self-hosted Build
- ⬜ .NET Tests
- ⬜ EF/Migration validation
- ⬜ Dashboard Build/Lint
- ⬜ Browser Smoke سناریوی دوکلیک → مشتری → شارژ → بوفه → Pending → بدهی
- ⬜ Real GameNet validation
