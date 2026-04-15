import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const manifestPath = path.join(
  root,
  "contracts",
  "backend-contract-manifest.json",
);

function fail(message) {
  console.error(`[release-gate] ${message}`);
  process.exit(1);
}

function readBoolean(value) {
  if (!value) return false;
  return ["1", "true", "yes", "on"].includes(value.toLowerCase());
}

function requireEnv(name, reason) {
  const value = process.env[name];
  if (!value || !value.trim()) {
    fail(`Missing ${name}. ${reason}`);
  }
}

if (!fs.existsSync(manifestPath)) {
  fail(`Contract manifest is missing at ${manifestPath}.`);
}

let manifest;
try {
  manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
} catch (error) {
  fail(
    `Contract manifest is not valid JSON: ${error instanceof Error ? error.message : String(error)}`,
  );
}

if (
  !manifest ||
  typeof manifest !== "object" ||
  !manifest.contracts ||
  typeof manifest.contracts !== "object"
) {
  fail("Contract manifest must contain a top-level 'contracts' object.");
}

const requiredContracts = [
  "MapFeedRequest",
  "MapFeedResponse",
  "EventDetailResponse",
  "SavedEventsResponse",
  "SubmissionDto",
];

for (const name of requiredContracts) {
  if (!manifest.contracts[name]) {
    fail(`Contract manifest entry '${name}' is missing.`);
  }
}

const releaseEnabled =
  readBoolean(process.env.WEUP_RELEASE_MODE) ||
  readBoolean(process.env.WeUP__Release__Enabled);
if (releaseEnabled) {
  requireEnv(
    "WeUP__PersistenceMode",
    "Release mode requires explicit runtime mode.",
  );
  if (!/^postgres$/i.test(process.env.WeUP__PersistenceMode)) {
    fail("Release mode requires WeUP__PersistenceMode=Postgres.");
  }

  requireEnv(
    "ConnectionStrings__WeUpDb",
    "Release mode requires database connectivity.",
  );
  requireEnv(
    "OTEL_EXPORTER_OTLP_ENDPOINT",
    "Release mode requires telemetry export endpoint.",
  );
}

console.log(
  `[release-gate] Environment validation passed (release mode ${releaseEnabled ? "enabled" : "disabled"}).`,
);
