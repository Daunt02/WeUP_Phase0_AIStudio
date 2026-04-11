const args = new Map();
for (let index = 2; index < process.argv.length; index += 1) {
  const token = process.argv[index];
  if (token.startsWith("--")) {
    const [key, value] = token.slice(2).split("=");
    args.set(key, value ?? "true");
  }
}

const baseUrl = (
  args.get("baseUrl") ||
  process.env.WEUP_SMOKE_BASE_URL ||
  process.env.WEUP_BACKEND_URL ||
  "http://127.0.0.1:5074"
).replace(/\/$/, "");
const deployed = args.get("deployed") === "true" || args.get("deployed") === "";

async function expectOk(name, promiseFactory) {
  const response = await promiseFactory();
  if (!response.ok) {
    const body = await response.text();
    throw new Error(`${name} failed with ${response.status}: ${body}`);
  }

  return response;
}

if (!deployed) {
  await expectOk("seed reset", () =>
    fetch(`${baseUrl}/internal/seed/reset`, { method: "POST" }),
  );
}

await expectOk("health", () => fetch(`${baseUrl}/health`));

const loginResponse = await expectOk("auth login", () =>
  fetch(`${baseUrl}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email: "camille+phase0@weup.test" }),
  }),
);
const auth = await loginResponse.json();
const authHeader = { Authorization: `Bearer ${auth.token}` };

await expectOk("auth me", () =>
  fetch(`${baseUrl}/auth/me`, { headers: authHeader }),
);

await expectOk("map feed", () =>
  fetch(`${baseUrl}/api/events/map`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      bounds: { minLat: 37.7, maxLat: 37.85, minLng: -122.52, maxLng: -122.37 },
      window: {
        startUtc: "2026-04-11T00:00:00Z",
        endUtc: "2026-04-13T00:00:00Z",
        timezone: "America/Los_Angeles",
      },
    }),
  }),
);

await expectOk("save endpoint", () =>
  fetch(`${baseUrl}/api/users/me/saves/evt-sf-midnight-groove`, {
    method: "POST",
    headers: authHeader,
  }),
);
await expectOk("moderation queue", () =>
  fetch(`${baseUrl}/api/moderation/queue?pageSize=5`),
);

console.log(`[smoke] Passed against ${baseUrl}`);
