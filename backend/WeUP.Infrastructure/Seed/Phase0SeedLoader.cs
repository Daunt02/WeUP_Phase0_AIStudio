using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace WeUP.Infrastructure.Seed;

public sealed class Phase0SeedLoader(IConfiguration configuration, IHostEnvironment environment)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public Phase0SeedDataset Load()
    {
        var datasetPath = ResolveDatasetPath();
        if (!File.Exists(datasetPath))
        {
            throw new FileNotFoundException($"Seed dataset not found at '{datasetPath}'.", datasetPath);
        }

        var json = File.ReadAllText(datasetPath);
        var dataset = JsonSerializer.Deserialize<Phase0SeedDataset>(json, JsonOptions)
            ?? throw new InvalidOperationException("Unable to deserialize Phase 0 seed dataset.");

        return dataset;
    }

    public string ResolveDatasetPath()
    {
        var configured = configuration["SeedData:DatasetPath"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(
                Path.IsPathRooted(configured)
                    ? configured
                    : Path.Combine(environment.ContentRootPath, configured));
        }

        var candidates = new[]
        {
            Path.Combine(environment.ContentRootPath, "..", "..", "seed", "phase0-dataset.json"),
            Path.Combine(environment.ContentRootPath, "..", "seed", "phase0-dataset.json"),
        };

        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (File.Exists(full))
            {
                return full;
            }
        }

        return Path.GetFullPath(candidates[0]);
    }
}