using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace WeUP.Infrastructure.Persistence;

public sealed class WeUpDbContextFactory : IDesignTimeDbContextFactory<WeUpDbContext>
{
    public WeUpDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        var startupProject = ResolveStartupProjectPath();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(startupProject)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("WeUpDb")
            ?? throw new InvalidOperationException("ConnectionStrings:WeUpDb is required to create the design-time DbContext.");

        var optionsBuilder = new DbContextOptionsBuilder<WeUpDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(WeUpDbContext).Assembly.FullName));

        return new WeUpDbContext(optionsBuilder.Options);
    }

    private static string ResolveStartupProjectPath()
    {
        var current = Directory.GetCurrentDirectory();
        var candidate = Path.GetFullPath(Path.Combine(current, "..", "WeUP.Api"));
        return Directory.Exists(candidate) ? candidate : current;
    }
}