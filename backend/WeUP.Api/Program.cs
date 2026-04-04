using WeUP.Api.Endpoints;
using WeUP.Api.Observability;
using WeUP.Application.Ingestion;
using WeUP.Application.Moderation;
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

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Services
// ---------------------------------------------------------------------------

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "WeUP API", Version = "v1 (Phase 0 stub)" });
});

// CORS — allow the Next.js frontend during local development
builder.Services.AddCors(opts =>
{
    opts.AddPolicy("LocalDev", policy =>
        policy.WithOrigins("http://localhost:3000")
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

builder.Services.AddSingleton<IEventRepository, StubEventRepository>();
builder.Services.AddSingleton<IEventSubmissionRepository, StubEventRepository>();
builder.Services.AddSingleton<ISaveRepository, StubSaveRepository>();

// Ingestion services
builder.Services.AddHttpClient("ingestion");
builder.Services.AddSingleton<IIngestionJobRepository, InMemoryIngestionJobRepository>();
builder.Services.AddSingleton<IIngestionAuditWriter, ConsoleIngestionAuditWriter>();

// Adapters — each registered as IIngestionAdapter so IngestionDispatcher receives all via IEnumerable<IIngestionAdapter>
builder.Services.AddSingleton<IIngestionAdapter, ManualSubmissionAdapter>();
builder.Services.AddSingleton<IIngestionAdapter, LinkAdapter>();
builder.Services.AddSingleton<IIngestionAdapter, VenuePageAdapter>();
builder.Services.AddSingleton<IIngestionDispatcher, IngestionDispatcher>();

// Flyer pipeline
builder.Services.AddSingleton<IFlyerStorageService, LocalFileStorageService>();
builder.Services.AddSingleton<IOcrService, StubOcrService>();
builder.Services.AddSingleton<IFlyerTextPostProcessor, FlyerTextPostProcessor>();
builder.Services.AddSingleton<ILlmEventNormalizer, HeuristicLlmNormalizer>();
builder.Services.AddSingleton<IGeocodingService, StubGeocodingService>();
builder.Services.AddSingleton<IFlyerConfidenceEvaluator, FlyerConfidenceEvaluator>();
builder.Services.AddSingleton<IFlyerIngestionPipeline, FlyerIngestionPipeline>();

// Moderation queue (P13), publish eligibility (P14), review actions (P15)
builder.Services.AddSingleton<IModerationQueueRepository, InMemoryModerationQueue>();
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
builder.Services.AddSingleton<IUserProfileRepository, InMemoryUserRepository>();
builder.Services.AddSingleton<ITokenService, BearerTokenService>();
builder.Services.AddSingleton<UserAuthService>();

// User persistence services (P17)
builder.Services.AddSingleton<IItineraryRepository, InMemoryItineraryRepository>();
builder.Services.AddSingleton<IUserPreferencesRepository, InMemoryPreferencesRepository>();

// Event submission workflow (P18)
builder.Services.AddSingleton<IEventSubmissionService, InMemorySubmissionRepository>();

// Spatial query services (P19) — bounding box, district, viewport queries
builder.Services.AddSingleton<IViewportQueryService, ViewportQueryService>();

// Market policy services (P20) — market boundaries, freeze rules, assignment
builder.Services.AddSingleton<IMarketPolicyService, MarketPolicyService>();

// Analytics services (P23) — event recording, privacy-compliant telemetry
builder.Services.AddSingleton<IAnalyticsService, ConsoleAnalyticsService>();

// Media upload services (P25/P26) — flyer asset management with lifecycle and validation
builder.Services.AddSingleton<IFlyerAssetStore, InMemoryFlyerAssetStore>();
builder.Services.AddSingleton<IFlyerAssetValidator, FlyerAssetValidator>();
builder.Services.AddSingleton<IFlyerUploadService, LocalFlyerUploadService>();

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
app.UseHttpsRedirection();

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
app.MapHealthChecks("/health");

// Seed flyer assets from /seed/flyers/ directory (dev only)
if (app.Environment.IsDevelopment())
{
    var flyerStore = app.Services.GetRequiredService<IFlyerAssetStore>();
    var seedService = new WeUP.Infrastructure.Media.FlyerSeedService(flyerStore);
    var seedDir = Path.Combine(Directory.GetCurrentDirectory(), "seed", "flyers");
    await seedService.SeedAsync(seedDir);
}

app.Run();

/// <summary>Required for integration test host access.</summary>
public partial class Program { }
