import { describe, expect, it, vi } from 'vitest';
import type { BridgeRequestEnvelope, BridgeResponseEnvelope } from './envelope';
import { BridgeErrorCodes } from './generated';
import { BridgeError, createInvoke } from './invoke';
import { httpTransport, webViewTransport, type WebViewBridge } from './transport';

const ok = (id: string, result: unknown): BridgeResponseEnvelope => ({ id, ok: true, result, error: null });

describe('createInvoke', () => {
  it('sends the envelope and returns the typed result', async () => {
    const transport = vi.fn(async (req: BridgeRequestEnvelope) =>
      ok(req.id, { echo: 'hi', host: 'devHost', serverTimeUtc: '2026-01-01T00:00:00+00:00' }),
    );
    const invoke = createInvoke(transport, () => 'req-1');

    const response = await invoke('system.ping', { message: 'hi' });

    expect(transport).toHaveBeenCalledWith({ id: 'req-1', command: 'system.ping', payload: { message: 'hi' } });
    expect(response.host).toBe('devHost');
  });

  it('throws BridgeError with the stable code on rejection', async () => {
    const invoke = createInvoke(async (req) => ({
      id: req.id,
      ok: false,
      result: null,
      error: { code: BridgeErrorCodes.invalidPayload, details: null },
    }));

    await expect(invoke('system.ping', { message: 'x' })).rejects.toMatchObject({
      name: 'BridgeError',
      code: 'bridge.invalid_payload',
    });
  });

  it('rejects a response that answers a different request', async () => {
    const invoke = createInvoke(async () => ok('someone-else', {}), () => 'mine');

    await expect(invoke('system.ping', { message: 'x' })).rejects.toBeInstanceOf(BridgeError);
  });
});

describe('httpTransport', () => {
  it('POSTs JSON to /invoke', async () => {
    const fetchImpl = vi.fn(async () => new Response(JSON.stringify(ok('a', 1)), { status: 200 }));
    const transport = httpTransport('/invoke', fetchImpl as unknown as typeof fetch);

    await transport({ id: 'a', command: 'system.ping', payload: {} });

    expect(fetchImpl).toHaveBeenCalledWith('/invoke', expect.objectContaining({ method: 'POST' }));
  });

  it('fails on a non-2xx HTTP status', async () => {
    const fetchImpl = vi.fn(async () => new Response('', { status: 500 }));
    const transport = httpTransport('/invoke', fetchImpl as unknown as typeof fetch);

    await expect(transport({ id: 'a', command: 'x.y', payload: {} })).rejects.toThrow('Bridge HTTP 500');
  });
});

describe('webViewTransport', () => {
  function fakeWebView() {
    const listeners: ((event: { data: unknown }) => void)[] = [];
    const posted: BridgeRequestEnvelope[] = [];
    const webview: WebViewBridge = {
      postMessage: (message) => posted.push(message as BridgeRequestEnvelope),
      addEventListener: (_type, listener) => listeners.push(listener),
    };
    const reply = (data: unknown) => listeners.forEach((l) => l({ data }));
    return { webview, posted, reply };
  }

  it('matches out-of-order replies to their requests by id', async () => {
    const { webview, posted, reply } = fakeWebView();
    const transport = webViewTransport(webview);

    const first = transport({ id: '1', command: 'a.b', payload: {} });
    const second = transport({ id: '2', command: 'a.b', payload: {} });
    expect(posted.map((p) => p.id)).toEqual(['1', '2']);

    reply(ok('2', 'second'));
    reply({ unrelated: true });
    reply(ok('1', 'first'));

    await expect(first).resolves.toMatchObject({ result: 'first' });
    await expect(second).resolves.toMatchObject({ result: 'second' });
  });
});
