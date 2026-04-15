namespace WeUP.Api.Configuration;

public enum PersistenceMode
{
    Stub,
    Postgres,
}

public sealed class WeUpRuntimeOptions
{
    public const string SectionName = "WeUP";

    public string? PersistenceMode { get; set; }
    public bool? UseStubRepositories { get; set; }
    public string LaunchMarket { get; set; } = "austin-tx";
    public FrontendOptions Frontend { get; set; } = new();
    public DatabaseOptions Database { get; set; } = new();
    public ReleaseOptions Release { get; set; } = new();

    public PersistenceMode ResolvePersistenceMode()
    {
        var explicitMode = ParseExplicitMode();
        PersistenceMode? legacyMode = UseStubRepositories switch
        {
            true => Configuration.PersistenceMode.Stub,
            false => Configuration.PersistenceMode.Postgres,
            null => null,
        };

        if (explicitMode is not null && legacyMode is not null && explicitMode != legacyMode)
        {
            throw new InvalidOperationException(
                $"Configuration conflict: '{SectionName}:PersistenceMode={explicitMode}' disagrees with legacy '{SectionName}:UseStubRepositories={UseStubRepositories}'. Remove the legacy flag or align both values.");
        }

        return explicitMode ?? legacyMode
            ?? throw new InvalidOperationException(
                $"Missing backend runtime mode. Configure '{SectionName}:PersistenceMode' as 'Stub' or 'Postgres'.");
    }

    private Configuration.PersistenceMode? ParseExplicitMode()
    {
        if (string.IsNullOrWhiteSpace(PersistenceMode))
        {
            return null;
        }

        return Enum.TryParse<Configuration.PersistenceMode>(PersistenceMode, ignoreCase: true, out var parsed)
            ? parsed
            : throw new InvalidOperationException(
                $"Unsupported '{SectionName}:PersistenceMode' value '{PersistenceMode}'. Use 'Stub' or 'Postgres'.");
    }

    public sealed class FrontendOptions
    {
        public string[] AllowedOrigins { get; set; } = ["http://localhost:3000", "http://127.0.0.1:3000"];
    }

    public sealed class DatabaseOptions
    {
        public string ConnectionStringName { get; set; } = "WeUpDb";
        public bool AutoApplyMigrations { get; set; }
        public bool RequireConnectivity { get; set; } = true;
        public bool FailOnPendingMigrations { get; set; } = true;
    }

    public sealed class ReleaseOptions
    {
        public bool Enabled { get; set; }
        public bool RequireDatabaseRuntime { get; set; } = true;
        public bool RequireAuthService { get; set; } = true;
        public bool RequireTelemetryExportEndpoint { get; set; }
        public bool RequireContractManifestValidation { get; set; } = true;
        public string ContractManifestPath { get; set; } = "contracts/backend-contract-manifest.json";
    }
}