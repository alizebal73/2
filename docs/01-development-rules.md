# روش رسمی توسعه و ادامه GameNet Manager

این فایل «قانون کار پروژه» است و باید در هر چت جدید، قبل از تغییر کد، به‌عنوان مرجع روش توسعه خوانده شود.

## 1. اصل اصلی

main شاخهٔ پایدار پروژه است؛ محل کار روزمره و آزمایش تغییرات نیست.

هیچ تغییر کدی، migration، تغییر CI، تغییر UI یا refactor مستقیماً روی main انجام نمی‌شود؛ مگر در شرایط اضطراری که همان تغییر از مسیر مشخص‌شده در این سند عبور کرده باشد.

هر Stage باید شاخهٔ مستقل خودش را داشته باشد.
الگوی نام‌گذاری: stage1-... ، stage2-... ، ... ، stage12-session-lease

شاخهٔ Stage باید از آخرین baseline سبز و تأییدشدهٔ Stage قبلی ساخته شود، نه از یک commit آزمایشی یا شکست‌خورده.

## 2. وضعیت مهم Stage 12

شاخهٔ رسمی Stage 12:
stage12-session-lease

baseline فعلی قبل از شروع Stage 12:
7d6d6f753ab7741b6ea29a57a3a2beb1b34b348b

این commit همان head سبز پایان Stage 11 است.

ادامهٔ واقعی Stage 12 باید فقط روی stage12-session-lease انجام شود.

## 3. چرخهٔ صحیح هر Stage

1. بررسی baseline قبلی.
2. بررسی کامل structure / behavior / prerequisites / connections / files / UI.
3. مشخص‌کردن هدف Stage و Definition of Done.
4. استفاده از branch مخصوص همان Stage.
5. اجرای کار در برش‌های کوچک و قابل‌تست.
6. بعد از هر برش: Build، تست مرتبط و Smoke/E2E مرتبط.
7. فقط وقتی برش واقعاً کار کرد، commit شود.
8. بعد از تکمیل Stage، CI کامل روی همان branch اجرا شود.
9. اختلاف branch با baseline و تغییرات ناخواسته بررسی شود.
10. PR به main ساخته شود.
11. CI و بررسی نهایی PR انجام شود.
12. فقط بعد از سبز بودن کامل merge به main.
13. پس از merge، main دوباره verify شود.
14. سند Stage و handoff مطابق وضعیت واقعی کد به‌روزرسانی شود.

## 4. قانون: یک Stage، یک شاخه

در طول یک Stage نباید بخشی روی main، بخشی روی branch دیگر و بخشی روی backup انجام شود.
همهٔ تغییرات Stage باید در branch مخصوص همان Stage جمع شوند.
Branchهای backup فقط برای نجات و بازگشت هستند، نه توسعهٔ روزمره.

## 5. توسعه به روش Vertical Slice

واحد اصلی پیشرفت پروژه یک جریان واقعی end-to-end است، نه فقط ساخت چند مدل یا API جدا.

نمونه Stage 12:
Session → Game → Agent → AccountLease → Credential Boundary → Session End → Release

Fake success، mock response و پیام queued جعلی ممنوع است.

## 6. Server مرجع واحد

Server تنها Source of Truth است.
Dashboard و Agent کلاینت هستند و تصمیم authoritative مالی، امنیتی یا وضعیت عملیاتی را خودشان نهایی نمی‌کنند.

## 7. Mock به جای قابلیت واقعی ممنوع

اگر قابلیت هنوز Backend/Agent قرارداد واقعی ندارد، دکمهٔ موفقیت جعلی، API fake یا پیام موفقیت ساختگی نباید ساخته شود.
قابلیت ناقص یا باید غیرفعال و با برچسب Stage بعدی باشد، یا هنوز وارد UI نشود.

## 8. Security Boundary

Secret، Password، SecretHash یا credential بلندمدت نباید به Dashboard/browser داده شود.
Credential باید فقط در context معتبر Agent/Lease، با عمر محدود و قابلیت invalid شدن پس از Release تحویل شود.

## 9. Domain و Migration

هر تغییر Domain باید با Entity، DbContext، Relationship/FK، Migration، Snapshot، Metadata، Seed/Test data، DTO و تست تطبیق داده شود.
Snapshot دستی بدون تطبیق دقیق با مدل ممنوع است.

بعد از Migration: Build → Tests → Database startup/migration smoke

## 10. CI معیار نهایی

Stage فقط وقتی Done است که آخرین commit همان Stage روی همان branch Build، Tests، Migration/Server Smoke، Agent/Session Smoke، Dashboard Build/Lint و Browser Smoke لازم را سبز کرده باشد.

CI مربوط به SHA قدیمی، معیار SHA جدید نیست.

## 11. وقتی CI شکست خورد

اول شکست طبقه‌بندی شود:

A) Product bug: خود کد/مدل/contract مشکل دارد؛ root cause کد اصلاح شود.
B) Test bug: محیط تست یا assertion مشکل دارد؛ تست اصلاح شود، ولی assertion نباید برای پنهان‌کردن bug ضعیف شود.
C) Infrastructure/flaky: runner یا process cleanup مشکل دارد؛ زیرساخت یا retry کنترل‌شده اصلاح شود و flaky به‌عنوان product success حساب نشود.

بعد از اصلاح، فقط همان branch دوباره CI شود.

## 12. Commit و تغییرات

هر commit ترجیحاً یک هدف مشخص داشته باشد.
تغییرات بی‌ربط چند subsystem در یک commit جمع نشوند.
اگر patch اشتباه شد، اول branch به وضعیت معتبر برگردانده شود و سپس ادامه داده شود؛ زنجیره‌ای از patchهای نامرتبط روی مشکل ساخته نشود.

## 13. Checkpoint اجباری

بعد از هر major step ثبت شود:
- آخرین SHA
- وضعیت CI
- چه چیزی واقعاً تکمیل شد
- چه چیزی باقی مانده
- dependency مرحله بعد
- فایل‌های اصلی
- smoke/testهای اضافه‌شده

این اطلاعات در handoff همان Stage نوشته شود.

## 14. شروع چت جدید

در هر چت جدید ابتدا این فایل خوانده شود؛ سپس docs/00-index.md و handoff آخرین Stage.
بعد branch، آخرین SHA و CI همان SHA بررسی شوند.
ادامهٔ کار فقط بر اساس وضعیت واقعی Git انجام شود، نه حافظهٔ چت قبلی.

## 15. ترتیب صحیح گسترش برنامه

Domain/DB → Server Service → Server API/Hub → Agent/Client → Integration/E2E → Dashboard → UI polish → Installer/Release

UI نباید قبل از قرارداد واقعی Backend قابلیت جعلی نشان دهد.

## 16. Stage 12

Stage 12 فقط روی stage12-session-lease توسعه داده شود.

Vertical Slice اصلی:
Authoritative Session → Game → Agent → AccountLease → Short-lived Credential → Game launch/apply contract → Session end → Lease release

اهداف مرتبط:
- automatic release on Agent disconnect/stale heartbeat
- real Game activeUsers
- real operational Game status
- Agent-backed Game Apply/Sync

هر هدف باید در برش مستقل و قابل تست اجرا شود.

## 17. قانون توقف

اگر baseline قبلی سبز نیست، Stage جدید شروع نشود.
اول baseline repair شود.
Stage بعدی نباید با workaround مشکل Stage قبلی را پنهان کند.

## 18. اشتباهاتی که نباید تکرار شوند

- توسعهٔ طولانی مستقیم روی main
- مخلوط‌کردن چند Stage در یک branch
- شروع Stage بعدی قبل از green baseline
- ادعای Done بدون CI همان SHA
- ساخت mock به‌عنوان functionality واقعی
- اصلاح test فقط برای سبز شدن بدون root-cause fix
- تغییر هم‌زمان Domain/CI/UI/Installer بدون dependency plan
- فراموش‌کردن migration/snapshot/handoff
- اتکا به یک چت قدیمی بدون خواندن وضعیت واقعی Git

## 19. قانون سادهٔ شروع هر روز

قبل از اولین تغییر کد این‌ها باید معلوم باشند:

الان روی چه branch هستیم؟
آخرین green SHA چیست؟
آخرین CI مربوط به همین SHA چیست؟
هدف فقط کدام Stage است؟
اولین Vertical Slice قابل اثبات چیست؟

اگر پاسخ یکی نامعلوم است، اول audit؛ بعد coding.

## قانون نهایی

Main = پایدار و قابل اعتماد.
Stage Branch = محل توسعه.
PR = مسیر ورود تغییرات به Main.
CI سبز همان SHA = شرط پذیرش.
Vertical Slice واقعی = واحد اصلی پیشرفت.
Handoff + این فایل = حافظهٔ رسمی پروژه برای چت‌های بعدی.

## 20. استثنای Bootstrap برای Release Readiness

تا وقتی baseline قابل‌اعتماد فعلی تثبیت نشده، فقط تغییرات بسیار محدودِ مربوط به CI trigger، Release Readiness roadmap و governance bootstrap می‌توانند برای جلوگیری از Runهای بی‌دلیل مستقیماً روی main ثبت شوند.

این استثنا شامل تغییر محصول، migration، UI یا feature نیست.

پس از تثبیت baseline و اجرای acceptance gate روی همان SHA، ادامه اصلاح باگ‌ها باید دوباره روی branch اختصاصی Stage انجام شود.


## 21. اولویت اجرایی Setup-First

تا قبل از First Installable Build، این هدف از ترتیب Stageهای قدیمی مهم‌تر است:

Setup → Install Server → Install Client → Real Test → Bug Fix → Component Update

Stageهای Release Readiness که مستقیماً blocker نصب پایه نیستند می‌توانند موقتاً متوقف بمانند.

قانون «baseline سبز قبل از Stage جدید» در این بازه به این معناست که **قبل از تغییر گسترده محصول، وضعیت واقعی نصب/Build بررسی شود**؛ نه اینکه نصب اولیه تا تکمیل تمام 13 Stage به تعویق بیفتد.

پس از First Installable Build، هر تغییر باید تا حد امکان در کوچک‌ترین component ممکن محدود شود و برای آن regression test و update مستقل داشته باشد.
