using System.Text.Json;
using WeUP.Contracts.Events;
using WeUP.Contracts.Saves;
using Xunit;

namespace WeUP.Tests.Integration;

public sealed class ContractManifestTests
{
    [Fact]
    public void BackendContractManifest_MatchesBackendContracts()
    {
        var manifest = JsonSerializer.Deserialize<ContractManifest>(File.ReadAllText(GetManifestPath()))
            ?? throw new InvalidOperationException("Unable to deserialize backend contract manifest.");

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
            [nameof(EventCalendarFeedQueryDto)] = GetJsonPropertyNames<EventCalendarFeedQueryDto>(),
            [nameof(CalendarEventItemDto)] = GetJsonPropertyNames<CalendarEventItemDto>(),
            [nameof(EventCalendarFeedV1ResponseDto)] = GetJsonPropertyNames<EventCalendarFeedV1ResponseDto>(),
            [nameof(CalendarFeedRequest)] = GetJsonPropertyNames<CalendarFeedRequest>(),
            [nameof(EventCalendarDto)] = GetJsonPropertyNames<EventCalendarDto>(),
            [nameof(EventCalendarCardDto)] = GetJsonPropertyNames<EventCalendarCardDto>(),
            [nameof(CalendarFeedResponse)] = GetJsonPropertyNames<CalendarFeedResponse>(),
            [nameof(MediaRefDto)] = GetJsonPropertyNames<MediaRefDto>(),
            [nameof(EventDetailProvenanceSummaryDto)] = GetJsonPropertyNames<EventDetailProvenanceSummaryDto>(),
            [nameof(EventDetailDto)] = GetJsonPropertyNames<EventDetailDto>(),
            [nameof(EventDetailResponse)] = GetJsonPropertyNames<EventDetailResponse>(),
            [nameof(EventModerationDto)] = GetJsonPropertyNames<EventModerationDto>(),
            [nameof(EventMergeLineageSummaryDto)] = GetJsonPropertyNames<EventMergeLineageSummaryDto>(),
            [nameof(EventPublishEligibilityDto)] = GetJsonPropertyNames<EventPublishEligibilityDto>(),
            [nameof(PublishBlockerSummaryDto)] = GetJsonPropertyNames<PublishBlockerSummaryDto>(),
            [nameof(EligibilityConfidenceSummaryDto)] = GetJsonPropertyNames<EligibilityConfidenceSummaryDto>(),
            [nameof(EligibilityFieldCompletenessSummaryDto)] = GetJsonPropertyNames<EligibilityFieldCompletenessSummaryDto>(),
            [nameof(DraftSubmissionRequest)] = GetJsonPropertyNames<DraftSubmissionRequest>(),
            [nameof(SubmissionDto)] = GetJsonPropertyNames<SubmissionDto>(),
            [nameof(SubmissionListResponse)] = GetJsonPropertyNames<SubmissionListResponse>(),
            [nameof(SubmitForReviewResponse)] = GetJsonPropertyNames<SubmitForReviewResponse>(),
            [nameof(SavedEventDto)] = GetJsonPropertyNames<SavedEventDto>(),
            [nameof(SavedEventsResponse)] = GetJsonPropertyNames<SavedEventsResponse>(),
            [nameof(SaveEventRequestDto)] = GetJsonPropertyNames<SaveEventRequestDto>(),
            [nameof(SaveEventResponseDto)] = GetJsonPropertyNames<SaveEventResponseDto>(),
            [nameof(UnsaveEventRequestDto)] = GetJsonPropertyNames<UnsaveEventRequestDto>(),
            [nameof(SavedStateDto)] = GetJsonPropertyNames<SavedStateDto>(),
        };

        Assert.Equal(expected.Keys.OrderBy(x => x), manifest.Contracts.Keys.OrderBy(x => x));

        foreach (var (name, propertyNames) in expected)
        {
            Assert.True(manifest.Contracts.TryGetValue(name, out var actual), $"Manifest entry '{name}' was not found.");
            Assert.Equal(propertyNames, actual!.OrderBy(x => x).ToArray());
        }
    }

    private static string[] GetJsonPropertyNames<T>() => typeof(T)
        .GetProperties()
        .Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name))
        .OrderBy(name => name)
        .ToArray();

    private static string GetManifestPath()
    {
        // Try multiple search paths for the manifest file
        var candidates = new[]
        {
            // Relative to assembly location (backend/WeUP.Tests/bin/Debug/net8.0)
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "contracts", "backend-contract-manifest.json"),
            // Five levels up (workspace root)
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "contracts", "backend-contract-manifest.json"),
            // From current working directory (might be workspace root or backend dir)
            Path.Combine(Environment.CurrentDirectory, "contracts", "backend-contract-manifest.json"),
            Path.Combine(Environment.CurrentDirectory, "..", "contracts", "backend-contract-manifest.json"),
            // Try looking at parent of parent directories
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "contracts", "backend-contract-manifest.json"),
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
                return fullPath;
        }

        // If nothing found, return the first candidate path for error reporting
        return Path.GetFullPath(candidates[0]);
    }

    private sealed record ContractManifest(
        [property: System.Text.Json.Serialization.JsonPropertyName("contracts")]
        Dictionary<string, string[]> Contracts);
}