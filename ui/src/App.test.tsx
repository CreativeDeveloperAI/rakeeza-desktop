import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import indexHtml from '../index.html?raw';
import { App } from './App';
import { strings } from './strings';

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe('App', () => {
  it('renders the app name from the central strings', () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise(() => {})));
    render(<App />);
    expect(screen.getByRole('heading', { level: 1 }).textContent).toBe(strings.appName);
  });

  it('shows the host once the bridge answers', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (_url: string, init: RequestInit) => {
        const { id } = JSON.parse(init.body as string) as { id: string };
        const result = { echo: 'ping', host: 'devHost', serverTimeUtc: '2026-01-01T00:00:00+00:00' };
        return new Response(JSON.stringify({ id, ok: true, result, error: null }));
      }),
    );
    render(<App />);
    expect(await screen.findByText(`${strings.bridge.connected}: ${strings.bridge.hosts.devHost}`)).toBeTruthy();
  });

  it('shows the Arabic message when the bridge rejects', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (_url: string, init: RequestInit) => {
        const { id } = JSON.parse(init.body as string) as { id: string };
        const error = { code: 'bridge.unknown_command', details: null };
        return new Response(JSON.stringify({ id, ok: false, result: null, error }));
      }),
    );
    render(<App />);
    expect(await screen.findByText(strings.errors['bridge.unknown_command']!)).toBeTruthy();
  });

  it('ships an Arabic, right-to-left document', () => {
    expect(indexHtml).toMatch(/<html lang="ar" dir="rtl">/);
  });
});
