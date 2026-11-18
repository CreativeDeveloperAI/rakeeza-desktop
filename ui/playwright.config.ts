import { defineConfig, devices } from '@playwright/test';
import { fileURLToPath } from 'node:url';

const port = 4173;
const isCi = Boolean(process.env['CI']);
const uiDist = fileURLToPath(new URL('./dist', import.meta.url));
const devHost = fileURLToPath(new URL('../src/Rakeeza.DevHost', import.meta.url));

// اختبارات الطرف إلى الطرف على Linux: الواجهة المبنيّة (ui/dist) يقدّمها DevHost،
// فيمرّ الجسر عبر POST /invoke الحقيقي. شغّل `npm run build` أوّلًا.
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
    command: `dotnet run --project "${devHost}" --no-launch-profile -- --urls http://127.0.0.1:${port} --DevHost:UiDist="${uiDist}"`,
    url: `http://127.0.0.1:${port}/health`,
    reuseExistingServer: !isCi,
    timeout: 180_000,
  },
});
