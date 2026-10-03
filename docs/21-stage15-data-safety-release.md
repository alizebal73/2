# Stage 15 — Data Safety, Backup, Restore & Release Safety

## Backup chain
`Operator Permission → Server BackupService → SQLite snapshot → DataProtection Keys → Manifest/Hash → Verify → Backup retention`

## Restore chain
`Verified Backup → Restore Candidate → Pending Restore Marker → Server Restart → Safety Copy → DB + DataProtection replacement → Initialize/Migrate`

## تکمیل‌شده
- Backup واقعی Server-side.
- SQLite `VACUUM INTO` برای snapshot.
- کپی DataProtection-Keys همراه Backup.
- Database SHA-256 verification.
- Applied migration verification.
- Archive SHA-256 به‌صورت hash واقعی فایل archive محاسبه می‌شود و داخل manifest self-referential نیست.
- retention با keep count.
- daily scheduler با یک backup در روز.
- Restore وسط اجرای Server انجام نمی‌شود؛ فقط candidate آماده و در restart اعمال می‌شود.
- قبل از restore یک safety DB copy ساخته می‌شود.
- SettingsPage به API واقعی Backup متصل شده است.
- Permission `backup.manage` و Audit برای تغییر تنظیمات/Backup/Restore.
- Test واقعی Backup/Verify/Restore preparation اضافه شده است.
- Pending Restore قبل از InitializeDatabaseAsync اعمال می‌شود.

## Release contract
- Backup باید قبل از migration خطرناک قابل‌بازیابی باشد.
- ProductVersion / Schema / API / Client compatibility باید در Release Gate حفظ شوند.
- Update/Rollback قبلی Stage 10 هنوز مرجع Client lifecycle است.

## Gate
Implementation نرم‌افزاری انجام شده؛ Restore Drill روی ماشین واقعی و installer/update canary هنوز Field Evidence هستند.
