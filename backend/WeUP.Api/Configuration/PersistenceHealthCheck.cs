using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WeUP.Infrastructure.Persistence;

namespace WeUP.Api.Configuration;

public sealed class PersistenceHealthCheck(
    IServiceScopeFactory scopeFactory,
    PersistenceRuntime runtime,
    WeUpRuntimeOptions options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!runtime.UsesDatabase)
        {
            return HealthCheckResult.Healthy("Stub persistence mode is active.", new Dictionary<string, object>
            {
                ["mode"] = runtime.Mode.ToString(),
                ["provider"] = "stub",
            });
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WeUpDbContext>();
        var canConnect = await db.Database.CanConnectAsync(cancellationToken);
        var pending = canConnect ? (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray() : [];

        if (!canConnect)
        {
            return HealthCheckResult.Unhealthy("Database connection failed.", data: new Dictionary<string, object>
            {
                ["mode"] = runtime.Mode.ToString(),
                ["connectionStringName"] = runtime.ConnectionStringName,
            });
        }

        if (options.Database.FailOnPendingMigrations && pending.Length > 0)
        {
            return HealthCheckResult.Unhealthy("Database has pending migrations.", data: new Dictionary<string, object>
            {
                ["mode"] = runtime.Mode.ToString(),
                ["pendingMigrations"] = pending,
            });
        }

        return HealthCheckResult.Healthy("Persistence layer is ready.", new Dictionary<string, object>
        {
            ["mode"] = runtime.Mode.ToString(),
            ["provider"] = db.Database.ProviderName ?? "unknown",
            ["pendingMigrationCount"] = pending.Length,
        });
    }
}