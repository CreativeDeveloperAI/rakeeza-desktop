// شكل المغلّف العابر للجسر، مطابق لـ BridgeRequest/BridgeResponse في Rakeeza.Commands.Bridge.

export interface BridgeRequestEnvelope {
  id: string;
  command: string;
  payload: unknown;
}

export interface BridgeErrorPayload {
  code: string;
  details: Record<string, string> | null;
}

export interface BridgeResponseEnvelope {
  id: string;
  ok: boolean;
  result: unknown;
  error: BridgeErrorPayload | null;
}
