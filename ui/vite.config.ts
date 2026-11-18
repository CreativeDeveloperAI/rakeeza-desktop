/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// WebView2 يحمّل الملفّات من مجلّد محلّي، لذلك المسارات نسبية.
export default defineConfig({
  base: './',
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    // في التطوير تُمرَّر أوامر الجسر إلى DevHost (dotnet run --project src/Rakeeza.DevHost).
    proxy: {
      '/invoke': 'http://localhost:5239',
    },
  },
  build: {
    outDir: 'dist',
    target: 'es2023',
  },
  test: {
    environment: 'jsdom',
    include: ['src/**/*.test.{ts,tsx}'],
  },
});
