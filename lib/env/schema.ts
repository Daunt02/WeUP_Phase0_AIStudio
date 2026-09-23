export type PublicEnv = {
  NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN: string;
  NEXT_PUBLIC_API_BASE_URL: string;
  NEXT_PUBLIC_ANALYTICS_ENABLED: boolean;
  NEXT_PUBLIC_BACKEND_ANALYTICS_ENDPOINT?: string;
  NEXT_PUBLIC_FEATURE_FLAGS?: Record<string, boolean>;
};

export function parseFeatureFlags(raw?: string): Record<string, boolean> | undefined {
  if (!raw) return undefined;
  try {
    const parsed = JSON.parse(raw);
    if (typeof parsed === 'object' && parsed !== null) return parsed as Record<string, boolean>;
  } catch (e) {
    // fall back to comma-separated list: FEATURE_A,FEATURE_B
    return raw.split(',').reduce((acc: Record<string, boolean>, cur) => {
      const k = cur.trim();
      if (k) acc[k] = true;
      return acc;
    }, {});
  }
  return undefined;
}
