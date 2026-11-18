# ADR-0002: جسر الأوامر بين الواجهة والمضيف

- الحالة: منفَّذ (الخطوة 0.3)
- التاريخ: 2026-11-18

## القرار
- الواجهة لا تعرف إلّا دالّة واحدة: `invoke<TReq, TRes>(command: string, request: TReq): Promise<TRes>`.
- كلّ أمر سجلّ C# (record) للطلب وآخر للردّ، ومعالج `ICommand<TReq,TRes>` في `Rakeeza.Commands`.
- طريقة النقل:
  - في Desktop: `window.chrome.webview.postMessage` بمغلّف `{ id, command, payload }`، والردّ `{ id, ok, result | error }`.
  - في DevHost: `POST /invoke` واحد بالمغلّف نفسه (اسم الأمر داخل المغلّف، لا في المسار).
- اسم الأمر `group.action` بأحرف صغيرة، ويُعلن بالسمة `[Command("...")]` على المعالج، ويُكتشف بالانعكاس عند الإقلاع.
- JSON الجسر صارم (`BridgeJson.Options`): أسماء camelCase، والتعدادات نصوص camelCase لا أرقام، والحقول غير المعروفة أو الناقصة أو `null` في غير موضعها رفضٌ بالرمز `bridge.invalid_payload`، والأرقام المكتوبة نصوصًا مرفوضة.
- رموز الأخطاء الثابتة: `bridge.invalid_envelope` و`bridge.unknown_command` و`bridge.invalid_payload` و`bridge.cancelled` و`internal`، وأيّ رمز خاصّ بالمجال يرميه المعالج عبر `CommandException`. الاستثناءات غير المتوقّعة تُسجَّل ولا تصل تفاصيلها إلى الواجهة.
- في Desktop تُخدم الواجهة من المجلّد المحلّي `ui/` عبر الأصل الافتراضي `https://app.rakeeza.local/`، ولا تُقبل رسائل الجسر من أيّ أصل آخر.
- أنواع TypeScript **تُولَّد** من سجلّات C# بالأداة `tools/Rakeeza.TypeGen` إلى `ui/src/bridge/generated.ts`، واختبار في CI يرفض أيّ فرق بين المولَّد والمحفوظ.
- الأخطاء التي يراها المستخدم رموزٌ ثابتة، تُترجم إلى رسائل عربية من ملفّ النصوص المركزي.

## العواقب
- لا يوجد منطق أعمال في الواجهة ولا في المضيف.
- المسار نفسه يُختبر من الطرف إلى الطرف على Linux عبر DevHost.
