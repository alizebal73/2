## 1. Integration Before Implementation
قبل از ساخت هر زنجیره جدید، زنجیره‌های قبلی، owner هر state، identity، Permission، transaction boundary، persistence، audit، approval، command/event protocol، retry/idempotency و recovery باید استخراج و با آن تطبیق داده شوند. قابلیت جدید نباید lifecycle موازی برای state موجود بسازد.

# GameNet — Engineering Laws (Mandatory)

این قوانین از 2026-10-03 قوانین حاکم بر ادامه توسعه هستند.

## 1. Correctness over Green CI
سبز شدن CI هدف نهایی نیست. تغییر معماری بد برای سبز کردن تست ممنوع است.

## 2. Root Cause First
قبل از تغییر کد، data model، query translation، transaction، concurrency، lifecycle، contract، ownership و deployment بررسی می‌شوند.

## 3. Server Is the Source of Truth
زمان، قیمت، Session، Lease، Permission، مالی و state حساس در Server تعیین می‌شوند.

## 4. No Fake Success
Mock فقط در مرز توسعه/طراحی و با برچسب روشن. مسیر اصلی محصول نباید fake-success داشته باشد.

## 5. Sensitive Action Completeness
عملیات حساس باید تا جای لازم این زنجیره را داشته باشد:
Permission → Validation → Transaction → Persistence → Audit → Result → Reverse/Cancel.

## 6. Persistence Is Part of the Feature
state مهم باید بعد از restart، reconnect و crash قابل بازیابی باشد.

## 7. SQLite Compatibility Is Explicit
Queryهای SQLite باید واقعاً توسط provider ترجمه شوند؛ انتخاب type بر اساس compatibility واقعی است.

## 8. UTC at Storage Boundary
در SQLite زمان persisted با قرارداد UTC و ترجیحاً DateTime نگهداری می‌شود. DateTimeOffset فقط در API/contract boundary ساخته می‌شود، مگر دلیل فنی مستند.

## 9. Atomic Allocation
تخصیص رقابتی باید atomic/transactional باشد و rows affected برای تشخیص race بررسی شود.

## 10. Idempotency
EndSession، release، disconnect recovery و retryهای command باید در برابر اجرای دوباره امن باشند.

## 11. Migration Discipline
هر schema change فقط با migration. Model، migration و snapshot باید با هم سازگار باشند.

## 12. Contract Discipline
Shared DTO، REST، SignalR و Agent باید یک قرارداد واحد داشته باشند؛ breaking change بدون مسیر مهاجرت ممنوع.

## 13. Secret Boundary
Secret خام فقط در کوتاه‌ترین مسیر لازم منتقل شود. Dashboard نباید secret credential را دریافت کند.

## 14. Permission Is Server-Side
مخفی کردن button امنیت نیست؛ Server باید action حساس را enforce کند.

## 15. Test the Behavior
تست outcome واقعی را اثبات کند. assertion برای سبز شدن حذف یا ضعیف نمی‌شود.

## 16. Concurrency Must Be Tested
هر عملیات حساس حداقل یک race test واقعی دارد.

## 17. Recovery Is Normal
power loss، restart، crash، network loss، stale agent و failed command جزو رفتار production هستند.

## 18. Update Safety
هر تغییر schema/contract/state باید اثرش بر migration، update و rollback بررسی شود.

## 19. UI Never Lies
اگر action واقعاً انجام نشده، UI نباید success یا state جعلی نشان دهد.

## 20. Vertical Slice
قابلیت واقعی یعنی زنجیره لازم آن کار کند:
UI → API/Hub → Domain → DB → Agent/External boundary → Result → UI.

## 21. One Logical Change, One Recoverable Commit
کار طولانی نباید یک uncommitted worktree عظیم و غیرقابل‌بازیابی باشد.

## 22. Main Is a Release Branch
توسعه روی branch جدا؛ main فقط بعد از evidence کافی و CI سبز.

## 23. No Casual Rewrite
rewrite ناگهانی Program.cs، DB یا Agent ممنوع؛ refactor باید تدریجی و قابل تست باشد.

## 24. Every Gap Gets a Home
هر gap باید Fix now / Carry-forward با دلیل / Won't-do با دلیل داشته باشد؛ gap معلق ممنوع.

## 25. 100% Means Field-Ready
«کامل» یعنی قابل استقرار و استفاده واقعی، با شواهد عملی لازم؛ نه صرفاً feature-complete روی کاغذ.
