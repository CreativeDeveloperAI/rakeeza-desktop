# ركيزة لسطح المكتب: دليل العمل في المستودع

تطبيق Windows أصيل يعيد بناء "ركيزة" على **قاعدة البيانات القديمة نفسها**.
الخطّة في `docs/plan.md`، والقرارات في `docs/decisions/`.

## البنية

| المسار | الدور | المنصّة |
|---|---|---|
| `src/Rakeeza.Core` | منطق المجال: التشفير والتطبيع والقواعد. لا I/O خاصّ بمنصّة | net10.0 |
| `src/Rakeeza.Data` | SQLite3MC وDapper وSQL صريح، وثوابت توافق Laravel في مكان واحد | net10.0 |
| `src/Rakeeza.Commands` | أوامر الجسر `ICommand<TReq,TRes>`، وهي الطبقة الوحيدة التي تراها الواجهة | net10.0 |
| `src/Rakeeza.DevHost` | ASP.NET Minimal API للتطوير وPlaywright فقط، ولا يُشحن | net10.0 |
| `src/Rakeeza.Desktop` | مضيف WPF وWebView2 رقيق، وتطبيق خدمات Windows خلف واجهاتها | net10.0-windows |
| `ui/` | React 19 وTypeScript وVite، وواجهة RTL عربية | — |
| `tests/` | اختبارات xUnit (ومنها اختبار حدود الطبقات) | net10.0 |

اتّجاه الاعتماد: `Core ← Data ← Commands ← (DevHost | Desktop)`. يفرضه
`tests/Rakeeza.Architecture.Tests`، فكلّ مشروع جديد تحت `src/` يجب تسجيله هناك.

## الأوامر

```bash
dotnet build Rakeeza.slnx
dotnet test Rakeeza.slnx
dotnet run --project src/Rakeeza.DevHost        # http://localhost:5239/health
cd ui && npm ci && npm run build && npm test
cd ui && npm run e2e -- --trace off              # Playwright بعد npm run build
```

ملفّات `packages.lock.json` تُرفع، وCI يستعمل `dotnet restore --locked-mode`. بعد تغيير أيّ حزمة شغّل `dotnet restore` وارفع ملفّات القفل.
يعمل CI (`.github/workflows/ci.yml`) على Linux وعلى `windows-latest`.

مشروع `Rakeeza.Desktop` **يُترجَم** على Linux (بفضل `EnableWindowsTargeting`) لكنّه لا يعمل إلّا على Windows.

## قواعد لا تُكسر

1. **المخطّط القديم هو الحقيقة**: `docs/legacy/legacy-schema.sql`. ممنوع `ALTER` أو `DROP` على جداول Laravel، وممنوع الكتابة في جدول `migrations`. الإضافات تكون جداول جديدة أو أعمدة nullable، وتُسجَّل في `desktop_migrations`.
2. **التوافق**: المعرّفات UUIDv7 نصّية، و`device_id` يُملأ دائمًا، والتواريخ UTC بصيغة `Y-m-d` أو `Y-m-d H:i:s`، والقيم المنطقية 0/1، وJSON نصّ، والأصناف المتعدّدة الأشكال أسماء أصناف PHP كاملة (`App\Models\...`) تُعرَّف في جدول ربط **واحد** داخل `Rakeeza.Data`.
3. **التشفير مطابق بايتًا ببايت** للنظام القديم، ويُختبر على fixtures حقيقية في `docs/legacy/fixtures/`.
4. **الصلاحيات تُطبَّق في الاستعلام** (السرّية خصوصًا)، لا في الواجهة.
5. **لا منطق في Desktop**: كلّ خدمة خاصّة بـWindows (المسح والـOCR والطباعة وفتح الملفّات) لها واجهة في Core أو Commands، وبديل وهمي في DevHost.
6. **لا نصّ عربي مكتوب مباشرة** في المكوّنات أو رسائل الرفض، بل عبر ملفّ النصوص المركزي.
7. **لا تخمين**: إن نقص تفصيل يغيّر السلوك، اسأل، واكتب ما افترضته في وصف الـPR.

## الأسلوب

- C#: `TreatWarningsAsErrors`، ومحلّلات `latest-recommended`، وnamespaces بنطاق الملفّ، وإدارة مركزية للحزم في `Directory.Packages.props`.
- TypeScript: وضع strict مع `noUncheckedIndexedAccess` و`exactOptionalPropertyTypes`.
- إصدارات الحزم مثبّتة بدقّة (لا `^`).
- PR صغيرة لكلّ خطوة من `docs/plan.md`.
