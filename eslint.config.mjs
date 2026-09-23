import { defineConfig } from "eslint/config";
import next from "eslint-config-next";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

export default defineConfig([
  // frontend-vue is a separate Vue 3 project with no eslint setup of its own;
  // the Next/React rule set misfires on Vue composables (false positives).
  { ignores: ["frontend-vue/**"] },
  {
    extends: [...next],
  },
  // Migrated from legacy .eslintrc.json (WEUP-A4): keep the client-code
  // process.env guard that the legacy config enforced on app/components/hooks.
  {
    files: ["app/**/*.{ts,tsx}", "components/**/*.{ts,tsx}", "hooks/**/*.{ts,tsx}"],
    rules: {
      "no-restricted-syntax": [
        "error",
        {
          selector: "MemberExpression[object.name=process][property.name=env]",
          message: "Do not access process.env directly in client code. Import from lib/env/public.ts instead.",
        },
      ],
    },
  },
]);
