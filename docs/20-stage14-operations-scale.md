# Stage 14 — Operations, Reservation, Waitlist & Event Completion

## زنجیره‌های واقعی
### Reservation
`Customer → Reservation → Station → Operator/Audit`

### Waitlist
`Customer → Waitlist Reservation → Assignment → Reservation → Station`

### Event
`Operator → GameEvent → Participant(Customer) → Seed/Status → Audit`

### Operations
`Server Station → Agent/Session/Lease Health → Dashboard`

هیچ lifecycle موازی برای Session/Customer ساخته نشده است.

## تکمیل‌شده
- Reservation create/list/confirm/cancel/check-in/complete.
- Waitlist create/list/assign.
- overlap guard با SQLite-safe materialization.
- Station management با create/update/soft-disable و Audit.
- persisted NetworkRoute برای internet1 / internet2 / LAN.
- Operations health برای Station/Agent/Session/Lease و route count.
- Event/Tournament domain: create, participant, seed, capacity, list, start, complete, cancel.
- Event conflict/participant checks و Audit.
- Dashboard OperationsPage از mockService جدا شده است.
- Event tab و Participant registration از Server API استفاده می‌کنند.
- concurrency foundation قبلی UpdatedAt/DB guards برای تغییرات هم برقرار است.

## قرارداد امنیت
- همه mutationها Permission سروری + transaction + Audit دارند.
- حذف Station destructive نیست؛ soft-disable است.
- Reservation و Event state فقط Server-owned است.

## Gate
Implementation این Stage انجام شده؛ exact-SHA CI و runtime smoke هنوز gate اثبات رسمی هستند.
