using WeUP.Api.Endpoints;
using WeUP.Application.Ingestion;
using WeUP.Domain.Events;
using WeUP.Domain.Flyer;
using WeUP.Domain.Ingestion;
using WeUP.Domain.Users;
using WeUP.Infrastructure.Flyer;
using WeUP.Infrastructure.Ingestion;
using WeUP.Infrastructure.Ingestion.Adapters;
using WeUP.Infrastructure.Persistence;

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
}

app.UseCors("LocalDev");
app.UseHttpsRedirection();

// Auth middleware seam — full implementation in P16
// app.UseAuthentication();
// app.UseAuthorization();

// ---------------------------------------------------------------------------
// Endpoints
// ---------------------------------------------------------------------------

app.MapEventEndpoints();
app.MapSaveEndpoints();
app.MapIngestionEndpoints();
app.MapFlyerEndpoints();
app.MapHealthChecks("/health");

app.Run();

/// <summary>Required for integration test host access.</summary>
public partial class Program { }
