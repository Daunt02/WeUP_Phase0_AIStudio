using System.Text.Json;
using WeUP.Contracts.Events;
using WeUP.Contracts.Saves;
using WeUP.Domain.Users;

namespace WeUP.Api.Configuration;

public sealed class ReleaseStartupValidator(
    ILogger<ReleaseStartupValidator> logger,
    WeUpRuntimeOptions options,
    PersistenceRuntime runtime,
    IServiceProvider serviceProvider,
    IWebHostEnvironment environment)
{
    public Task ValidateAsync(CancellationToken ct = default)
    {
        if (!options.Release.Enabled)
        {
            logger.LogInformation("Release startup validation disabled for environment {EnvironmentName}.", environment.EnvironmentName);
            return Task.CompletedTask;
        }

        if (options.Release.RequireDatabaseRuntime && !runtime.UsesDatabase)
        {
            throw new InvalidOperationException("Release mode requires WeUP:PersistenceMode=Postgres. Stub persistence is not allowed.");
        }

        if (options.Release.RequireAuthService)
        {
            var tokenService = serviceProvider.GetService<ITokenService>();
            if (tokenService is null)
            {
                throw new InvalidOperationException("Release mode requires ITokenService registration, but none was found.");
            }
        }

        if (options.Release.RequireTelemetryExportEndpoint && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")))
        {
            throw new InvalidOperationException("Release mode requires OTEL_EXPORTER_OTLP_ENDPOINT to be configured.");
        }

        if (options.Release.RequireContractManifestValidation)
        {
            ValidateContractManifest();
        }

        logger.LogInformation("Release startup validation completed successfully.");
        return Task.CompletedTask;
    }

    private void ValidateContractManifest()
    {
        var manifestPath = ResolveManifestPath(options.Release.ContractManifestPath);
        if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
        {
            throw new InvalidOperationException($"Release mode requires contract manifest at '{options.Release.ContractManifestPath}', but it was not found.");
        }

        var manifest = JsonSerializer.Deserialize<ContractManifest>(File.ReadAllText(manifestPath))
            ?? throw new InvalidOperationException("Unable to deserialize backend contract manifest for release validation.");

        var expected = new Dictionary<string, string[]>
        {
            [nameof(GeoBoundingBox)] = GetJsonPropertyNames<GeoBoundingBox>(),
            [nameof(TimeWindowRequest)] = GetJsonPropertyNames<TimeWindowRequest>(),
            [nameof(LocalityFilterRequest)] = GetJsonPropertyNames<LocalityFilterRequest>(),
            [nameof(MapFeedRequest)] = GetJsonPropertyNames<MapFeedRequest>(),
            [nameof(EventMapCardDto)] = GetJsonPropertyNames<EventMapCardDto>(),
            [nameof(EventMapClusterDto)] = GetJsonPropertyNames<EventMapClusterDto>(),
            [nameof(MapFeedResponse)] = GetJsonPropertyNames<MapFeedResponse>(),
            [nameof(EventMapItemDto)] = GetJsonPropertyNames<EventMapItemDto>(),
            [nameof(EventMapFeedQueryDto)] = GetJsonPropertyNames<EventMapFeedQueryDto>(),
            [nameof(EventMapFeedClusterDto)] = GetJsonPropertyNames<EventMapFeedClusterDto>(),
            [nameof(EventMapDensityControlDto)] = GetJsonPropertyNames<EventMapDensityControlDto>(),
            [nameof(EventMapFeedV1ResponseDto)] = GetJsonPropertyNames<EventMapFeedV1ResponseDto>(),
            [nameof(CalendarFeedRequest)] = GetJsonPropertyNames<CalendarFeedRequest>(),
            [nameof(EventCalendarDto)] = GetJsonPropertyNames<EventCalendarDto>(),
            [nameof(CalendarFeedResponse)] = GetJsonPropertyNames<CalendarFeedResponse>(),
            [nameof(MediaRefDto)] = GetJsonPropertyNames<MediaRefDto>(),
            [nameof(EventDetailDto)] = GetJsonPropertyNames<EventDetailDto>(),
            [nameof(EventDetailResponse)] = GetJsonPropertyNames<EventDetailResponse>(),
            [nameof(DraftSubmissionRequest)] = GetJsonPropertyNames<DraftSubmissionRequest>(),
            [nameof(SubmissionDto)] = GetJsonPropertyNames<SubmissionDto>(),
            [nameof(SubmissionListResponse)] = GetJsonPropertyNames<SubmissionListResponse>(),
            [nameof(SubmitForReviewResponse)] = GetJsonPropertyNames<SubmitForReviewResponse>(),
            [nameof(SavedEventDto)] = GetJsonPropertyNames<SavedEventDto>(),
            [nameof(SavedEventsResponse)] = GetJsonPropertyNames<SavedEventsResponse>(),
            [nameof(SaveEventResponse)] = GetJsonPropertyNames<SaveEventResponse>(),
        };

        foreach (var (name, fields) in expected)
        {
            if (!manifest.Contracts.TryGetValue(name, out var manifestFields))
            {
                throw new InvalidOperationException($"Contract manifest drift detected: missing entry '{name}'.");
            }

            var expectedCsv = string.Join(",", fields.OrderBy(x => x));
            var actualCsv = string.Join(",", manifestFields.OrderBy(x => x));
            if (!string.Equals(expectedCsv, actualCsv, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Contract manifest drift detected for '{name}'. Expected [{expectedCsv}] but found [{actualCsv}].");
            }
        }
    }

    private static string ResolveManifestPath(string configuredPath)
    {
        var candidates = new[]
        {
            Path.GetFullPath(configuredPath),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", configuredPath)),
            Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, configuredPath)),
            Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", configuredPath)),
        };

        return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private static string[] GetJsonPropertyNames<T>() => typeof(T)
        .GetProperties()
        .Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name))
        .OrderBy(name => name)
        .ToArray();

    private sealed record ContractManifest(
        [property: System.Text.Json.Serialization.JsonPropertyName("contracts")]
        Dictionary<string, string[]> Contracts);
}
