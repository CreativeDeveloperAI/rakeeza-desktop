import { expect, test } from '@playwright/test';

test('the UI reaches DevHost through the bridge', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByTestId('bridge-status')).toHaveText('متّصل: مضيف التطوير');
});

test('system.ping round-trips over POST /invoke', async ({ request }) => {
  const response = await request.post('/invoke', {
    data: { id: 'e2e-1', command: 'system.ping', payload: { message: 'مرحبا' } },
  });

  expect(response.ok()).toBe(true);
  const body = await response.json();
  expect(body).toMatchObject({ id: 'e2e-1', ok: true, error: null, result: { echo: 'مرحبا', host: 'devHost' } });
});

test('an unknown command is rejected with a stable code', async ({ request }) => {
  const response = await request.post('/invoke', {
    data: { id: 'e2e-2', command: 'nope.nothing', payload: {} },
  });

  expect(await response.json()).toMatchObject({ id: 'e2e-2', ok: false, error: { code: 'bridge.unknown_command' } });
});
