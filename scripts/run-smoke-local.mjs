import { spawn } from "node:child_process";
import { setTimeout as delay } from "node:timers/promises";

const baseUrl = (
  process.env.WEUP_BACKEND_URL || "http://127.0.0.1:5074"
).replace(/\/$/, "");
const healthUrl = `${baseUrl}/health`;

function launchBackend() {
  const backend = spawn(
    "dotnet",
    ["run", "--project", "backend/WeUP.Api/WeUP.Api.csproj", "--no-build"],
    {
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT:
          process.env.ASPNETCORE_ENVIRONMENT || "Development",
        ASPNETCORE_URLS: baseUrl,
        SeedData__EnableOnStartup: "true",
        SeedData__EnableResetEndpoint: "true",
      },
      stdio: ["ignore", "pipe", "pipe"],
    },
  );

  backend.stdout.on("data", (chunk) => {
    process.stdout.write(`[backend] ${chunk}`);
  });

  backend.stderr.on("data", (chunk) => {
    process.stderr.write(`[backend] ${chunk}`);
  });

  return backend;
}

async function waitForHealth(timeoutMs = 60000) {
  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    try {
      const response = await fetch(healthUrl);
      if (response.ok) {
        return;
      }
    } catch {
      // Backend is not ready yet.
    }

    await delay(500);
  }

  throw new Error(`Timed out waiting for backend health at ${healthUrl}`);
}

async function runSmoke() {
  return new Promise((resolve, reject) => {
    const smoke = spawn(
      process.execPath,
      ["scripts/run-smoke.mjs", `--baseUrl=${baseUrl}`],
      {
        env: process.env,
        stdio: "inherit",
      },
    );

    smoke.on("error", reject);
    smoke.on("exit", (code) => {
      if (code === 0) {
        resolve();
      } else {
        reject(
          new Error(`Smoke checks failed with exit code ${code ?? "unknown"}`),
        );
      }
    });
  });
}

async function stopBackend(backend) {
  if (backend.killed || backend.exitCode !== null) {
    return;
  }

  backend.kill("SIGTERM");

  const exited = await Promise.race([
    new Promise((resolve) => backend.once("exit", () => resolve(true))),
    delay(5000).then(() => false),
  ]);

  if (!exited) {
    backend.kill("SIGKILL");
  }
}

const backend = launchBackend();

backend.on("error", (error) => {
  console.error(`[smoke] Failed to start backend: ${error.message}`);
});

try {
  await waitForHealth();
  await runSmoke();
} finally {
  await stopBackend(backend);
}
