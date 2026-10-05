# GameNet Manager — Next Generation

این شاخه فقط «دفتر معماری و نقشه راه نسل بعد» است و نباید برای توسعه نسخه Release فعلی استفاده شود.

## مرجع
- Repository فعلی: `alizebal73/2`
- Stable/Release baseline فعلی در زمان ایجاد این شاخه: `ad38ca91ef9abe08ae56d70beb8c8ebb82fbf777`
- شاخهٔ Release امنیت/SQLite که در زمان ثبت این سند باز بود: `fix-preinstaller-security-and-sqlite-gates`
- آخرین head مشاهده‌شده آن PR: `5384d6e35ab9f14e2e9460be958a64f2bdff0e00`

## قانون
هیچ بازطراحی معماری در `main` فعلی قبل از:
1. Final Setup
2. نصب واقعی Server/Client
3. Physical Validation روی 2–3 PC
4. تأییدیه نهایی Release

انجام نمی‌شود.

بعد از Release، هدف ساخت Repository مستقل با نام پیشنهادی:
`alizebal73/gamenet-manager-next`

## فلسفه
نسل جدید باید رفتار قابل قبول نسخه Release را حفظ کند، ولی مرزهای Domain، Application، API، Persistence، Realtime، Dashboard، Agent، Client Shell، Test و Installer را از هم جدا کند.
