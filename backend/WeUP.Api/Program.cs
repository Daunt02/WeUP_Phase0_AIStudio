using WeUP.Api.Endpoints;
using WeUP.Domain.Events;
using WeUP.Domain.Users;
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
// Stub repositories — replaced by EF Core implementations in P08–P09
builder.Services.AddSingleton<IEventRepository, StubEventRepository>();
builder.Services.AddSingleton<IEventSubmissionRepository, StubEventRepository>();
builder.Services.AddSingleton<ISaveRepository, StubSaveRepository>();

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
app.MapHealthChecks("/health");

app.Run();

/// <summary>Required for integration test host access.</summary>
public partial class Program { }
