using System;
using Microsoft.Extensions.Options;

namespace WeUP.Api.FeatureFlags
{
    public interface IFeatureFlagService
    {
        bool IsEnabled(string flagName);
        bool FlyerOcrEnabled { get; }
        bool LinkIngestionEnabled { get; }
        bool VenuePageIngestionEnabled { get; }
        bool ModerationDashboardEnabled { get; }
        bool ProfilePreferencesEnabled { get; }
        bool SocialTrustFeaturesEnabled { get; }

        bool IngestionDisabled { get; }
        bool SubmissionDisabled { get; }
        bool ModerationFallbackDisabled { get; }
    }

    public class FeatureFlagService : IFeatureFlagService
    {
        private readonly FeatureFlagsOptions _options;

        public FeatureFlagService(IOptions<FeatureFlagsOptions> options)
        {
            _options = options.Value;
        }

        public bool IsEnabled(string flagName) => flagName?.ToLowerInvariant() switch
        {
            "flyerocr" => FlyerOcrEnabled,
            "linkingestion" => LinkIngestionEnabled,
            "venuepageingestion" => VenuePageIngestionEnabled,
            "moderationdashboard" => ModerationDashboardEnabled,
            "profilepreferences" => ProfilePreferencesEnabled,
            "socialtrustfeatures" => SocialTrustFeaturesEnabled,
            _ => false
        };

        public bool FlyerOcrEnabled => _options.FlyerOcr;
        public bool LinkIngestionEnabled => _options.LinkIngestion;
        public bool VenuePageIngestionEnabled => _options.VenuePageIngestion;
        public bool ModerationDashboardEnabled => _options.ModerationDashboard;
        public bool ProfilePreferencesEnabled => _options.ProfilePreferences;
        public bool SocialTrustFeaturesEnabled => _options.SocialTrustFeatures;

        public bool IngestionDisabled => _options.IngestionDisabled;
        public bool SubmissionDisabled => _options.SubmissionDisabled;
        public bool ModerationFallbackDisabled => _options.ModerationFallbackDisabled;
    }
}
