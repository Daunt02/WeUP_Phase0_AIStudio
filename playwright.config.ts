import { defineConfig } from "@playwright/test";

const isCi = Boolean(process.env.CI);

export default defineConfig({
  testDir: "./e2e",
  timeout: 60_000,
  fullyParallel: false,
  retries: isCi ? 1 : 0,
  use: {
    baseURL: "http://127.0.0.1:3000",
    trace: "on-first-retry",
    screenshot: "only-on-failure",
  },
  webServer: [
    {
      command: "npm run dev:backend:stub",
      url: "http://127.0.0.1:5074/health",
      reuseExistingServer: !isCi,
      timeout: 120_000,
    },
    {
      command: "npm run dev:frontend:test",
      url: "http://127.0.0.1:3000",
      reuseExistingServer: !isCi,
      timeout: 120_000,
    },
  ],
});
