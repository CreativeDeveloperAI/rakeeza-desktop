import type { BridgeRequestEnvelope, BridgeResponseEnvelope } from './envelope';

/** ينقل مغلّفًا واحدًا إلى المضيف ويعيد ردّه. */
export type Transport = (request: BridgeRequestEnvelope) => Promise<BridgeResponseEnvelope>;

/** الجزء الذي نستعمله من واجهة WebView2 (window.chrome.webview). */
export interface WebViewBridge {
  postMessage(message: unknown): void;
  addEventListener(type: 'message', listener: (event: { data: unknown }) => void): void;
}

declare global {
  interface Window {
    chrome?: { webview?: WebViewBridge };
  }
}

/** DevHost: ‏POST /invoke بالمغلّف نفسه. */
export function httpTransport(endpoint = '/invoke', fetchImpl?: typeof fetch): Transport {
  return async (request) => {
    const response = await (fetchImpl ?? globalThis.fetch)(endpoint, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    });
    if (!response.ok) {
      throw new Error(`Bridge HTTP ${response.status}`);
    }
    return (await response.json()) as BridgeResponseEnvelope;
  };
}

/** Desktop: ‏postMessage إلى WebView2، ومطابقة الردود بالمعرّف. */
export function webViewTransport(webview: WebViewBridge): Transport {
  const pending = new Map<string, (response: BridgeResponseEnvelope) => void>();

  webview.addEventListener('message', (event) => {
    const response = event.data as Partial<BridgeResponseEnvelope> | null;
    if (!response || typeof response.id !== 'string') {
      return;
    }
    const resolve = pending.get(response.id);
    if (resolve) {
      pending.delete(response.id);
      resolve(response as BridgeResponseEnvelope);
    }
  });

  return (request) =>
    new Promise((resolve) => {
      pending.set(request.id, resolve);
      webview.postMessage(request);
    });
}

/** يختار النقل بحسب المضيف: WebView2 إن وُجد، وإلّا HTTP. */
export function detectTransport(win: Window = window): Transport {
  const webview = win.chrome?.webview;
  return webview ? webViewTransport(webview) : httpTransport();
}
