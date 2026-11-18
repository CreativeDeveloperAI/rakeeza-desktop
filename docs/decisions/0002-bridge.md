# ADR-0002: جسر الأوامر بين الواجهة والمضيف

- الحالة: مقبول (التنفيذ في الخطوة 0.3)
- التاريخ: 2026-11-18

## القرار
- الواجهة لا تعرف إلّا دالّة واحدة: `invoke<TReq, TRes>(command: string, request: TReq): Promise<TRes>`.
- كلّ أمر سجلّ C# (record) للطلب وآخر للردّ، ومعالج `ICommand<TReq,TRes>` في `Rakeeza.Commands`.
- طريقة النقل:
  - في Desktop: `window.chrome.webview.postMessage` بمغلّف `{ id, command, payload }`، والردّ `{ id, ok, result | error }`.
  - في DevHost: `POST /invoke/{command}` بالمغلّف نفسه.
- أنواع TypeScript **تُولَّد** من سجلّات C#، وفحص في CI يرفض أيّ فرق بين المولَّد والمحفوظ.
- الأخطاء التي يراها المستخدم رموزٌ ثابتة، تُترجم إلى رسائل عربية من ملفّ النصوص المركزي.

## العواقب
- لا يوجد منطق أعمال في الواجهة ولا في المضيف.
- المسار نفسه يُختبر من الطرف إلى الطرف على Linux عبر DevHost.
