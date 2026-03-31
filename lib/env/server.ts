export type ServerEnv = {
  DISABLE_HMR: boolean;
  MAPBOX_SECRET?: string;
  API_KEY?: string;
};

export function getServerEnv(): ServerEnv {
  return {
    DISABLE_HMR: process.env.DISABLE_HMR === 'true',
    MAPBOX_SECRET: process.env.MAPBOX_SECRET,
    API_KEY: process.env.API_KEY,
  };
}

export function requireServerEnv(name: keyof ServerEnv): string {
  const v = (process.env as any)[name];
  if (!v) throw new Error(`[env] Missing required server env: ${name}`);
  return v;
}
