namespace WeUP.Api.FeatureFlags
{
    public class FeatureFlagsOptions
    {
        public bool FlyerOcr { get; set; }
        public bool LinkIngestion { get; set; }
        public bool VenuePageIngestion { get; set; }
        public bool ModerationDashboard { get; set; }
        public bool ProfilePreferences { get; set; }
        public bool SocialTrustFeatures { get; set; }

        // Operational kill switches
        public bool IngestionDisabled { get; set; }
        public bool SubmissionDisabled { get; set; }
        public bool ModerationFallbackDisabled { get; set; }
    }
}
