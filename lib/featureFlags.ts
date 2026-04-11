type FlagMap = {
  flyerOcr: boolean;
  linkIngestion: boolean;
  venuePageIngestion: boolean;
  moderationDashboard: boolean;
  profilePreferences: boolean;
  socialTrustFeatures: boolean;

  // Operational kill switches
  ingestionDisabled: boolean;
  submissionDisabled: boolean;
  moderationFallbackDisabled: boolean;
};

function envBool(key: string, def = false): boolean {
  const v = process.env[key];
  if (!v) return def;
  return ["1", "true", "yes", "on"].includes(v.toLowerCase());
}

export const featureFlags: FlagMap = {
  flyerOcr: envBool("NEXT_PUBLIC_FEATURE_flyerOcr", false),
  linkIngestion: envBool("NEXT_PUBLIC_FEATURE_linkIngestion", false),
  venuePageIngestion: envBool("NEXT_PUBLIC_FEATURE_venuePageIngestion", false),
  moderationDashboard: envBool(
    "NEXT_PUBLIC_FEATURE_moderationDashboard",
    false,
  ),
  profilePreferences: envBool("NEXT_PUBLIC_FEATURE_profilePreferences", false),
  socialTrustFeatures: envBool(
    "NEXT_PUBLIC_FEATURE_socialTrustFeatures",
    false,
  ),

  ingestionDisabled: envBool("NEXT_PUBLIC_FEATURE_ingestionDisabled", false),
  submissionDisabled: envBool("NEXT_PUBLIC_FEATURE_submissionDisabled", false),
  moderationFallbackDisabled: envBool(
    "NEXT_PUBLIC_FEATURE_moderationFallbackDisabled",
    false,
  ),
};

export function isFeatureEnabled(flag: keyof FlagMap): boolean {
  return Boolean(featureFlags[flag]);
}
