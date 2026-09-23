const nextJest = require("next/jest");

const createJestConfig = nextJest({
  // Provide the path to your Next.js app to load next.config.js and .env files in your test environment
  dir: "./",
});

// Add any custom config to be passed to Jest
const customJestConfig = {
  setupFilesAfterEnv: ["<rootDir>/jest.setup.js"],
  testEnvironment: "jest-environment-jsdom",
  moduleNameMapper: {
    "^@/(.*)$": "<rootDir>/$1",
  },
  testMatch: [
    "**/__tests__/**/*.test.(ts|tsx|js)",
    "**/?(*.)+(spec|test).(ts|tsx|js)",
  ],
  // frontend-vue is a separate Vue 3 + vitest project; its suites cannot run
  // under the root jest runner (no vue/vitest modules at root).
  testPathIgnorePatterns: [
    "<rootDir>/e2e/",
    "<rootDir>/playwright.config.ts",
    "<rootDir>/frontend-vue/",
  ],
  collectCoverageFrom: [
    "components/**/*.{js,jsx,ts,tsx}",
    "services/**/*.{js,jsx,ts,tsx}",
    "features/**/*.{js,jsx,ts,tsx}",
    "!**/*.d.ts",
    "!**/node_modules/**",
  ],
};

// createJestConfig is exported this way to ensure that next/jest can load the Next.js config which is async
module.exports = createJestConfig(customJestConfig);
