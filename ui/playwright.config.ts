import { defineConfig, devices } from '@playwright/test';

const port = 4173;
const isCi = Boolean(process.env['CI']);

// اختبارات الطرف إلى الطرف على Linux. حاليًا تعمل ضدّ الواجهة المبنيّة (vite preview)،
// ومن الخطوة 0.3 تعمل ضدّ DevHost.
export default defineConfig({
  testDir: './e2e',
  forbidOnly: isCi,
  retries: isCi ? 1 : 0,
  reporter: isCi ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: `http://127.0.0.1:${port}`,
    locale: 'ar',
    trace: 'off',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: `npm run preview -- --host 127.0.0.1 --port ${port} --strictPort`,
    url: `http://127.0.0.1:${port}`,
    reuseExistingServer: !isCi,
    timeout: 60_000,
  },
});
