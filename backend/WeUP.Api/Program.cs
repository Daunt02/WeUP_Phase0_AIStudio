using WeUP.Api.Endpoints;
using WeUP.Api.Observability;
using Microsoft.EntityFrameworkCore;
using WeUP.Application.Ingestion;
using WeUP.Application.Moderation;
using WeUP.Application.Resolution;
using WeUP.Application.Users;
using WeUP.Domain.Events;
using WeUP.Domain.Flyer;
using WeUP.Domain.Ingestion;
using WeUP.Domain.Moderation;
using WeUP.Domain.Users;
using WeUP.Domain.Spatial;
using WeUP.Domain.Markets;
using WeUP.Domain.Analytics;
using WeUP.Domain.Media;
using WeUP.Domain.Resolution;
using WeUP.Infrastructure.Auth;
using WeUP.Infrastructure.Flyer;
using WeUP.Infrastructure.Submissions;
using WeUP.Infrastructure.Ingestion;
using WeUP.Infrastructure.Ingestion.Adapters;
using WeUP.Infrastructure.Moderation;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Spatial;
using WeUP.Infrastructure.Markets;
using WeUP.Infrastructure.Analytics;
using WeUP.Infrastructure.Media;
using WeUP.Infrastructure.Resolution;
using WeUP.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Services
// ---------------------------------------------------------------------------

builder.Services.AddEndpointsApiExplorer();
builder.Services.Configure<MediaIntakeOptions>(builder.Configuration.GetSection(MediaIntakeOptions.SectionName));
builder.Services.Configure<VideoIntakeOptions>(builder.Configuration.GetSection(VideoIntakeOptions.SectionName));
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "WeUP API", Version = "v1 (Phase 0 stub)" });
});

// CORS — allow the Next.js frontend during local development
builder.Services.AddCors(opts =>
{
    opts.AddPolicy("LocalDev", policy =>
        policy.WithOrigins("http://localhost:3000", "http://127.0.0.1:3000")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Domain / Application services
// To switch from stubs to EF Core:
//   1. Add EF + Npgsql packages (see backend/WeUP.Infrastructure/Persistence/Migrations/README.md)
//   2. Set WeUpDb connection string in appsettings / environment
//   3. Replace the three lines below with the EF registrations (uncommented):
//
// var connStr = builder.Configuration.GetConnectionString("WeUpDb")
//     ?? throw new InvalidOperationException("WeUpDb connection string is required.");
// builder.Services.AddDbContext<WeUpDbContext>(opts => opts.UseNpgsql(connStr));
// builder.Services.AddScoped<IEventRepository, EfEventRepository>();
// builder.Services.AddScoped<IEventSubmissionRepository, EfEventRepository>();
// builder.Services.AddScoped<ISaveRepository, EfSaveRepository>();

builder.Services.AddSingleton<StubEventRepository>();
builder.Services.AddSingleton<IEventRepository>(sp => sp.GetRequiredService<StubEventRepository>());
builder.Services.AddSingleton<IEventSubmissionRepository>(sp => sp.GetRequiredService<StubEventRepository>());
builder.Services.AddSingleton<StubSaveRepository>();
builder.Services.AddSingleton<ISaveRepository>(sp => sp.GetRequiredService<StubSaveRepository>());

// Ingestion services
builder.Services.AddHttpClient("ingestion").AddHttpMessageHandler<WeUP.Api.Observability.CorrelationIdDelegatingHandler>();
var connStr = builder.Configuration.GetConnectionString("WeUpDb");
var preferSeededInMemoryIngestion =
    builder.Environment.IsDevelopment() ||
    builder.Environment.IsEnvironment("Testing") ||
    builder.Configuration.GetValue<bool>("SeedData:EnableOnStartup") ||
    builder.Configuration.GetValue<bool>("SeedData:EnableResetEndpoint");

builder.Services.AddSingleton<InMemoryIngestionJobRepository>();

if (!preferSeededInMemoryIngestion && !string.IsNullOrWhiteSpace(connStr))
{
    builder.Services.AddDbContext<WeUpDbContext>(opts => opts.UseNpgsql(connStr));
    builder.Services.AddScoped<IIngestionJobRepository, EfIngestionJobRepository>();
    builder.Services.AddScoped<IIngestionAuditWriter, ConsoleIngestionAuditWriter>();
}
else
{
    builder.Services.AddSingleton<IIngestionJobRepository>(sp => sp.GetRequiredService<InMemoryIngestionJobRepository>());
    builder.Services.AddSingleton<IIngestionAuditWriter, ConsoleIngestionAuditWriter>();
}

builder.Services.AddSingleton<IEventSourceAdapter, ManualSubmissionAdapter>();
builder.Services.AddSingleton<IEventSourceAdapter, LinkAdapter>();
builder.Services.AddSingleton<IEventSourceAdapter, VenuePageAdapter>();
builder.Services.AddSingleton<IEventSourceAdapterResolver, EventSourceAdapterResolver>();
builder.Services.AddScoped<IIngestionCoordinator, IngestionCoordinator>();

// Flyer pipeline
builder.Services.AddSingleton<IFlyerTextPostProcessor, FlyerTextPostProcessor>();
builder.Services.AddSingleton<IFlyerOcrService, StubFlyerOcrService>();
builder.Services.AddSingleton<IFlyerNormalizationService, HeuristicFlyerNormalizationService>();
builder.Services.AddSingleton<IFlyerConfidenceEvaluator, FlyerConfidenceEvaluator>();
builder.Services.AddScoped<IFlyerIngestionPipeline, FlyerIngestionPipeline>();

// Moderation queue (P13), publish eligibility (P14), review actions (P15)
builder.Services.AddSingleton<InMemoryModerationQueue>();
builder.Services.AddSingleton<IModerationQueueRepository>(sp => sp.GetRequiredService<InMemoryModerationQueue>());
builder.Services.AddSingleton<IAuditTrailService, InMemoryAuditTrail>();
builder.Services.AddSingleton<IModerationQueueService, ModerationQueueService>();
builder.Services.AddSingleton<IConfidenceScoringService, ConfidenceScoringService>();
builder.Services.AddSingleton<IEligibilityRuleSet, PublishEligibilityRuleSet>();
builder.Services.AddSingleton<IPublishEligibilityService, PublishEligibilityService>();
builder.Services.AddSingleton<ReviewActionService>();
builder.Services.AddSingleton<IReviewActionService>(sp => sp.GetRequiredService<ReviewActionService>());
builder.Services.AddSingleton<IRollbackService>(sp => sp.GetRequiredService<ReviewActionService>());

// Auth services (P16)
// Phase 0: in-memory token store. Real JWT: add JwtBearer, set WeUp:Auth:JwtSecret in appsettings.
builder.Services.AddSingleton<InMemoryUserRepository>();
builder.Services.AddSingleton<IUserProfileRepository>(sp => sp.GetRequiredService<InMemoryUserRepository>());
builder.Services.AddSingleton<ITokenService, BearerTokenService>();
builder.Services.AddSingleton<UserAuthService>();

// User persistence services (P17)
builder.Services.AddSingleton<IItineraryRepository, InMemoryItineraryRepository>();
builder.Services.AddSingleton<IUserPreferencesRepository, InMemoryPreferencesRepository>();

// Event submission workflow (P18)
builder.Services.AddSingleton<InMemorySubmissionRepository>();
builder.Services.AddSingleton<IEventSubmissionService>(sp => sp.GetRequiredService<InMemorySubmissionRepository>());

// Spatial query services (P19) — bounding box, district, viewport queries
builder.Services.AddSingleton<IViewportQueryService, ViewportQueryService>();

// Market policy services (P20) — market boundaries, freeze rules, assignment
builder.Services.AddSingleton<MarketPolicyService>();
builder.Services.AddSingleton<IMarketPolicyService>(sp => sp.GetRequiredService<MarketPolicyService>());

// Analytics services (P23) — event recording, privacy-compliant telemetry
builder.Services.AddSingleton<IAnalyticsService, ConsoleAnalyticsService>();

// Media upload services (P25/P26) — flyer asset management with lifecycle and validation
builder.Services.AddSingleton<IFileSignatureInspector, ImageSharpFileSignatureInspector>();
builder.Services.AddSingleton<IMediaChecksumService, Sha256ChecksumService>();
builder.Services.AddSingleton<IFlyerAssetLifecyclePolicy, FlyerAssetLifecyclePolicy>();
builder.Services.AddScoped<IFlyerAssetValidator, FlyerAssetValidator>();
builder.Services.AddScoped<IFlyerUploadService, LocalFlyerUploadService>();
builder.Services.AddSingleton<IMediaStorageService, LocalMediaStorageService>();
builder.Services.AddSingleton<MediaIntakeValidation>();

// Video flyer intake services (P28)
builder.Services.AddSingleton<VideoIntakeValidation>();
builder.Services.AddSingleton<IVideoStorageService, LocalVideoStorageService>();
builder.Services.AddSingleton<IVideoMetadataReader, DeterministicVideoMetadataReader>();
builder.Services.AddSingleton<IVideoFrameExtractor, DeterministicVideoFrameExtractor>();
builder.Services.AddSingleton<IPosterSelectionService, HeuristicPosterSelectionService>();
builder.Services.AddScoped<IVideoDerivedAssetRegistrar, VideoDerivedAssetRegistrar>();
builder.Services.AddScoped<IVideoProcessingOrchestrator, VideoProcessingOrchestrator>();
builder.Services.AddScoped<IVideoFlyerUploadService, VideoFlyerUploadService>();
builder.Services.AddScoped<IVideoDerivedAssetQueryService, VideoDerivedAssetQueryService>();

if (!preferSeededInMemoryIngestion && !string.IsNullOrWhiteSpace(connStr))
{
    builder.Services.AddScoped<IFlyerAssetStore, EfFlyerAssetStore>();
    builder.Services.AddScoped<IFlyerDuplicateDetector, FlyerDuplicateDetector>();
    builder.Services.AddScoped<IMediaIntakeRepository, EfMediaIntakeRepository>();
    builder.Services.AddScoped<IMediaIntakeService, MediaIntakeService>();

    // Flyer provenance and evidence services (P27) — durable EF/Postgres path
    builder.Services.AddScoped<IProvenanceRepository, EfProvenanceRepository>();
    builder.Services.AddScoped<IFlyerEvidenceRepository, EfFlyerEvidenceRepository>();
    builder.Services.AddScoped<IFlyerIntakeService, FlyerIntakeService>();
    builder.Services.AddScoped<IFlyerEvidenceQueryService, FlyerEvidenceQueryService>();

    // Video flyer persistence (P29) — durable EF/Postgres path
    builder.Services.AddScoped<IVideoFlyerRepository, EfVideoFlyerRepository>();
    builder.Services.AddScoped<IVideoDerivedAssetRepository, EfVideoDerivedAssetRepository>();

    // Entity resolution (P12) — durable EF/Postgres path
    builder.Services.AddScoped<IEntityResolutionRepository, EfEntityResolutionRepository>();
}
else
{
    builder.Services.AddSingleton<IFlyerAssetStore, InMemoryFlyerAssetStore>();
    builder.Services.AddSingleton<IFlyerDuplicateDetector, FlyerDuplicateDetector>();
    builder.Services.AddSingleton<IMediaIntakeRepository, InMemoryMediaIntakeRepository>();
    builder.Services.AddSingleton<IMediaIntakeService, MediaIntakeService>();

    // Flyer provenance and evidence services (P27) — dev/test in-memory path
    builder.Services.AddSingleton<IProvenanceRepository, InMemoryProvenanceRepository>();
    builder.Services.AddSingleton<IFlyerEvidenceRepository, InMemoryFlyerEvidenceRepository>();
    builder.Services.AddSingleton<IFlyerIntakeService, FlyerIntakeService>();
    builder.Services.AddSingleton<IFlyerEvidenceQueryService, FlyerEvidenceQueryService>();

    // Video flyer persistence (P29) — dev/test in-memory path
    builder.Services.AddSingleton<InMemoryVideoFlyerRepository>();
    builder.Services.AddSingleton<IVideoFlyerRepository>(sp => sp.GetRequiredService<InMemoryVideoFlyerRepository>());
    builder.Services.AddSingleton<InMemoryVideoDerivedAssetRepository>();
    builder.Services.AddSingleton<IVideoDerivedAssetRepository>(sp => sp.GetRequiredService<InMemoryVideoDerivedAssetRepository>());

    // Entity resolution (P12) — dev/test in-memory path
    builder.Services.AddSingleton<IEntityResolutionRepository, InMemoryEntityResolutionRepository>();
}

builder.Services.AddScoped<IEventDuplicateDetector, DeterministicEventDuplicateDetector>();
builder.Services.AddScoped<IMergePlanner, DeterministicMergePlanner>();
builder.Services.AddScoped<IEntityResolutionService, EntityResolutionService>();
builder.Services.AddSingleton<Phase0SeedLoader>();
builder.Services.AddSingleton<Phase0SeedService>();

// Observability setup (P22) — correlation IDs, structured logging
builder.AddWeUPObservability();

// OpenTelemetry seam — wired fully in P22
// builder.Services.AddOpenTelemetry()...

// Health checks
builder.Services.AddHealthChecks();

// ---------------------------------------------------------------------------
// Pipeline
// ---------------------------------------------------------------------------

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Optional: log seed data readiness on startup
    // Set SeedData:OnStartup=true in appsettings.Development.json to enable
    var seedOnStartup = builder.Configuration.GetValue<bool>("SeedData:OnStartup");
    if (seedOnStartup)
    {
        app.Logger.LogInformation("Phase 0 seed data mode active. Run 'npm run seed-data' from the frontend directory to populate test data.");
    }
}

// Observability middleware (P22) — correlation IDs
app.UseWeUPObservability();

// Simple request logging middleware to aid smoke tests and debugging
app.Use(async (context, next) =>
{
    var logger = app.Logger;
    logger.LogInformation("Incoming request: {Method} {Path}", context.Request.Method, context.Request.Path);
    await next();
    logger.LogInformation("Response: {StatusCode} for {Method} {Path}", context.Response.StatusCode, context.Request.Method, context.Request.Path);
});

app.UseCors("LocalDev");
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

// Auth middleware — Phase 0 uses BearerTokenService; JwtBearer added in P16.5+
// app.UseAuthentication();
// app.UseAuthorization();

// ---------------------------------------------------------------------------
// Endpoints
// ---------------------------------------------------------------------------

app.MapAuthEndpoints();
app.MapItineraryEndpoints();
app.MapSubmissionEndpoints();
app.MapEventEndpoints();
app.MapSaveEndpoints();
app.MapIngestionEndpoints();
app.MapFlyerEndpoints();
app.MapModerationEndpoints();
app.MapSpatialEndpoints(); // P19: Spatial/bounding-box queries
app.MapMarketEndpoints(); // P20: Market/taxonomy queries
app.MapTemporalEndpoints(); // P21: Temporal preset queries
app.MapAnalyticsEndpoints(); // P23: Analytics event recording
app.MapMediaEndpoints(); // P25: Flyer media intake
app.MapVideoFlyerEndpoints(); // P28: Video flyer intake
app.MapResolutionEndpoints(); // P12: Deterministic entity resolution and merge
app.MapHealthChecks("/health");

var enableSeedResetEndpoint = builder.Configuration.GetValue<bool>("SeedData:EnableResetEndpoint") || app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing");
if (enableSeedResetEndpoint)
{
    app.MapSeedEndpoints();
}

var enableSeedOnStartup = builder.Configuration.GetValue<bool?>("SeedData:EnableOnStartup") ?? app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing");
if (enableSeedOnStartup)
{
    var seeder = app.Services.GetRequiredService<Phase0SeedService>();
    var snapshot = await seeder.ResetAsync();
    app.Logger.LogInformation("Loaded Phase 0 seed dataset {Version} with {EventCount} events and {ModerationCount} moderation items.", snapshot.SeedVersion, snapshot.EventCount, snapshot.ModerationItemCount);
}

// Seed flyer assets from /seed/flyers/ directory (dev only)
if (app.Environment.IsDevelopment())
{
    var flyerStore = app.Services.GetRequiredService<IFlyerAssetStore>();
    var seedService = new WeUP.Infrastructure.Media.FlyerSeedService(flyerStore);
    // Try /flyers/ at project root first (real Houston flyers), fall back to seed/flyers/
    var rootFlyersDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "flyers");
    var seedFlyersDir = Path.Combine(Directory.GetCurrentDirectory(), "seed", "flyers");
    var seedDir = Directory.Exists(rootFlyersDir) ? rootFlyersDir : seedFlyersDir;
    await seedService.SeedAsync(seedDir);
}

app.Run();

/// <summary>Required for integration test host access.</summary>
public partial class Program { }
