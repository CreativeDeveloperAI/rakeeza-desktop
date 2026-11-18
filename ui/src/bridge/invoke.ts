import type { BridgeErrorPayload } from './envelope';
import type { CommandMap, CommandName } from './generated';
import { detectTransport, type Transport } from './transport';

/** رفض عائد من المضيف برمز ثابت؛ الرسالة العربية تأتي من ملفّ النصوص. */
export class BridgeError extends Error {
  readonly code: string;
  readonly details: Readonly<Record<string, string>>;

  constructor(error: BridgeErrorPayload) {
    super(error.code);
    this.name = 'BridgeError';
    this.code = error.code;
    this.details = error.details ?? {};
  }
}

export type Invoke = <K extends CommandName>(
  command: K,
  request: CommandMap[K]['request'],
) => Promise<CommandMap[K]['response']>;

export function createInvoke(transport: Transport, newId: () => string = () => crypto.randomUUID()): Invoke {
  return async (command, request) => {
    const id = newId();
    const response = await transport({ id, command, payload: request });
    if (response.id !== id) {
      throw new BridgeError({ code: 'bridge.invalid_envelope', details: null });
    }
    if (!response.ok) {
      throw new BridgeError(response.error ?? { code: 'internal', details: null });
    }
    return response.result as never;
  };
}

let defaultInvoke: Invoke | undefined;

/** نقطة الدخول الوحيدة للواجهة نحو المضيف. */
export const invoke: Invoke = (command, request) => {
  defaultInvoke ??= createInvoke(detectTransport());
  return defaultInvoke(command, request);
};
