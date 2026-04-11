const baseUrl = process.env.WEUP_BACKEND_URL || "http://127.0.0.1:5074";

const response = await fetch(
  `${baseUrl.replace(/\/$/, "")}/internal/seed/reset`,
  {
    method: "POST",
  },
);

if (!response.ok) {
  const body = await response.text();
  console.error(`[seed] Reset failed with status ${response.status}: ${body}`);
  process.exit(1);
}

const payload = await response.json();
console.log(`[seed] Loaded ${payload.seedVersion} from ${payload.datasetPath}`);
console.log(
  `[seed] Markets=${payload.marketCount} Venues=${payload.venueCount} Events=${payload.eventCount} Users=${payload.userCount}`,
);
