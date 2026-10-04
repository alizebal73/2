# Stage 1 — Foundation Audit — 2026-10-04

## Scope of this step

فقط Foundation بررسی می‌شود؛ هیچ Stage دیگری عمداً در این قدم تغییر داده نمی‌شود.

مرجع Foundation:
- Repository / branch discipline
- Server / Client / Dashboard / Shared project structure
- Solution/project references
- Self-hosted Windows CI
- .NET/Node toolchain contract
- EF migration/model validation
- Server startup + health
- Dashboard install/build/preview
- قابل‌بازیابی بودن مسیر توسعه و checkpoint

## Exact head audited

- Branch: `completion/control-20261003`
- SHA: `4582d3a91a800fbce0b971c50285ec2dfe509f88`
- Commit: `fix: order CustomerLogin rollback exception filters correctly`
- آخرین CI برای این SHA تا زمان این ممیزی موجود نبود.

## Findings

### Foundation software
✅ ساختار چهار مرزی Server / Client / Shared / Dashboard پابرجاست.

✅ Solution/project references و migration infrastructure در tree فعلی وجود دارند.

✅ CI روی self-hosted Windows/X64 تعریف شده و مسیر Build/Test/EF/Startup/Dashboard را پوشش می‌دهد.

✅ EF model/snapshot validation در CI وجود دارد.

✅ Server startup + migration + health smoke در CI وجود دارد.

✅ Dashboard install/lint/build/preview/browser smoke در CI وجود دارد.

### Foundation gaps

1. **Exact-head verification gap**
   آخرین head فعلی هنوز روی همین SHA دوباره Build/Test/EF/Startup/Dashboard verify نشده بود.

   تصمیم:
   - با همین audit checkpoint یک CI اجرای جدید روی exact SHA ایجاد می‌شود.
   - تا سبز شدن آن، Foundation از نظر software در وضعیت `Verified pending exact-head CI` می‌ماند.

2. **Field deployment gate**
   نصب واقعی Server و Client روی دو ماشین مستقل، recovery فیزیکی و rollout چند PC هنوز gate عمومی Release است؛ این نقص نرم‌افزاری Foundation نیست و در این قدم تغییر داده نمی‌شود.

## Verdict

Stage 1 از نظر implementation:
**Complete**

Stage 1 از نظر verification:
**Pending exact-head CI**

Stage 1 تا حد field-ready:
**Not a Stage-1 software blocker; carried to deployment/release gate**

## Rule for next step

بعد از exact-head CI:
- اگر سبز بود، Stage 1 بسته می‌شود و این قدم پایان می‌یابد.
- اگر قرمز بود، فقط blocker مربوط به Foundation/Build/CI همین head اصلاح می‌شود.
- تا بسته شدن این قدم، Stage 2 دست‌نخورده می‌ماند.
