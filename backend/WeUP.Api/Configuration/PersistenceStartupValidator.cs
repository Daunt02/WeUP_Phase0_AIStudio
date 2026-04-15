using Microsoft.EntityFrameworkCore;
using WeUP.Infrastructure.Persistence;

namespace WeUP.Api.Configuration;

public sealed class PersistenceStartupValidator(
    ILogger<PersistenceStartupValidator> logger,
    IServiceScopeFactory scopeFactory,
    PersistenceRuntime runtime,
    WeUpRuntimeOptions options)
{
    public async Task ValidateAsync(CancellationToken ct = default)
    {
        if (!runtime.UsesDatabase)
        {
            logger.LogInformation("Starting in stub persistence mode.");
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WeUpDbContext>();

        if (options.Database.AutoApplyMigrations)
        {
            logger.LogInformation("Applying pending EF Core migrations using connection '{ConnectionStringName}'.", runtime.ConnectionStringName);
            await db.Database.MigrateAsync(ct);
        }

        if (options.Database.RequireConnectivity && !await db.Database.CanConnectAsync(ct))
        {
            throw new InvalidOperationException(
                $"Postgres persistence mode is enabled, but the backend cannot connect using ConnectionStrings:{runtime.ConnectionStringName}.");
        }

        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToArray();
        if (options.Database.FailOnPendingMigrations && pending.Length > 0)
        {
            throw new InvalidOperationException(
                $"Database is reachable but has pending EF Core migrations: {string.Join(", ", pending)}. Apply them or enable WeUP:Database:AutoApplyMigrations.");
        }

        logger.LogInformation("Postgres persistence mode validated successfully.");
    }
}