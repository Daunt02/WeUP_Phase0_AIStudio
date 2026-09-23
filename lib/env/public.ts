import { PublicEnv, parseFeatureFlags } from "./schema";

// Friendly runtime helper: safe to import in client code.
export const IS_DEVELOPMENT = process.env.NODE_ENV === "development";

function loadRaw() {
  return {
    NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN:
      process.env.NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN,
    NEXT_PUBLIC_API_BASE_URL: process.env.NEXT_PUBLIC_API_BASE_URL || "/",
    NEXT_PUBLIC_ANALYTICS_ENABLED: process.env.NEXT_PUBLIC_ANALYTICS_ENABLED,
    NEXT_PUBLIC_BACKEND_ANALYTICS_ENDPOINT:
      process.env.NEXT_PUBLIC_BACKEND_ANALYTICS_ENDPOINT,
    NEXT_PUBLIC_FEATURE_FLAGS: process.env.NEXT_PUBLIC_FEATURE_FLAGS,
  } as const;
}

function validate(raw: ReturnType<typeof loadRaw>): PublicEnv {
  if (!raw.NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN) {
    throw new Error(
      "[env] Missing required public config: NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN.\n" +
        "Add it to your environment or .env.local and restart the dev server.\n" +
        "This value is intentionally required so the app fails early with a clear message.",
    );
  }

  return {
    NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN: raw.NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN,
    NEXT_PUBLIC_API_BASE_URL: raw.NEXT_PUBLIC_API_BASE_URL,
    NEXT_PUBLIC_ANALYTICS_ENABLED: raw.NEXT_PUBLIC_ANALYTICS_ENABLED === "true",
    NEXT_PUBLIC_BACKEND_ANALYTICS_ENDPOINT:
      raw.NEXT_PUBLIC_BACKEND_ANALYTICS_ENDPOINT,
    NEXT_PUBLIC_FEATURE_FLAGS: parseFeatureFlags(raw.NEXT_PUBLIC_FEATURE_FLAGS),
  };
}

// Export a stable config object safe for client imports. Only reads NEXT_PUBLIC_* keys.
export const publicEnv: PublicEnv = validate(loadRaw());

export function getPublicEnv(): PublicEnv {
  return publicEnv;
}

export default publicEnv;
