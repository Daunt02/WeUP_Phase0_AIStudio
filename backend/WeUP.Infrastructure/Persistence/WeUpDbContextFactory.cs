using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WeUP.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef migrations add</c>.
/// Reads the connection string from the WEUPDB_CONNSTR environment variable
/// or falls back to a local development default.
/// </summary>
public sealed class WeUpDbContextFactory : IDesignTimeDbContextFactory<WeUpDbContext>
{
    public WeUpDbContext CreateDbContext(string[] args)
    {
        var connStr = Environment.GetEnvironmentVariable("WEUPDB_CONNSTR")
            ?? "Host=localhost;Database=weup_phase0_dev;Username=weup;Password=CHANGE_ME";

        var opts = new DbContextOptionsBuilder<WeUpDbContext>()
            .UseNpgsql(connStr)
            .Options;

        return new WeUpDbContext(opts);
    }
}
