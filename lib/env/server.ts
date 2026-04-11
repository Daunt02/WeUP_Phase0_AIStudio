// This module is strictly server-only. If it is accidentally imported into
// client code the check below will surface a clear runtime error early.
if (typeof window !== "undefined") {
  throw new Error(
    "[env] lib/env/server.ts must not be imported from client-side code",
  );
}

export type ServerEnv = {
  DISABLE_HMR: boolean;
  MAPBOX_SECRET?: string;
  API_KEY?: string;
  API_URL?: string; // base URL used by server scripts (optional)
};

export function getServerEnv(): ServerEnv {
  return {
    DISABLE_HMR: process.env.DISABLE_HMR === "true",
    MAPBOX_SECRET: process.env.MAPBOX_SECRET,
    API_KEY: process.env.API_KEY,
    API_URL: process.env.API_URL,
  };
}

export function requireServerEnv(name: keyof ServerEnv): string {
  const v = (process.env as any)[name];
  if (!v) throw new Error(`[env] Missing required server env: ${name}`);
  return v;
}
