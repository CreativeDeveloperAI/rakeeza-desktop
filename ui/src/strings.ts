// كلّ نصوص الواجهة تمرّ من هنا؛ لا نصّ عربي مكتوب مباشرة داخل المكوّنات.
// يتوسّع هذا إلى ملفّ نصوص مركزي كامل في الخطوة 0.10.
import type { HostKind } from './bridge/generated';

export const strings = {
  appName: 'ركيزة',
  appTagline: 'ركيزة الأرشيف',
  bridge: {
    connecting: 'جارٍ الاتّصال بالمضيف…',
    connected: 'متّصل',
    hosts: {
      devHost: 'مضيف التطوير',
      desktop: 'تطبيق سطح المكتب',
    } satisfies Record<HostKind, string>,
  },
  errors: {
    'bridge.invalid_envelope': 'تعذّر فهم الطلب المرسل إلى التطبيق.',
    'bridge.unknown_command': 'هذه العملية غير متاحة في هذا الإصدار.',
    'bridge.invalid_payload': 'البيانات المرسلة غير مكتملة أو غير صحيحة.',
    'bridge.cancelled': 'أُلغيت العملية.',
    internal: 'حدث خطأ غير متوقّع. حاول مرّة أخرى.',
  } as Record<string, string>,
} as const;

/** الرسالة العربية لرمز خطأ، مع رسالة عامّة إن لم يُعرف الرمز. */
export function errorMessage(code: string): string {
  return strings.errors[code] ?? strings.errors['internal'] ?? code;
}
