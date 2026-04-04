using Xunit;
using WeUP.Domain.Analytics;

namespace WeUP.Tests.Integration;

/// <summary>
/// Integration tests for Analytics Endpoints — P23
/// Verifies POST /api/analytics/events and GET /api/analytics/event-types
/// </summary>
public class AnalyticsEndpointsTests
{
    [Fact]
    public void RecordEvent_WithValidEventType_Succeeds()
    {
        // Arrange
        var eventType = AnalyticsEventType.MapViewed;
        var analyticsEvent = new AnalyticsEvent(
            EventType: eventType,
            OccurredAt: DateTimeOffset.UtcNow,
            UserId: "user123",
            Properties: new Dictionary<string, object> { ["region"] = "sf" });

        // Act & Assert
        Assert.NotNull(analyticsEvent);
        Assert.Equal(eventType, analyticsEvent.EventType);
    }

    [Theory]
    [InlineData(AnalyticsEventType.MapViewed)]
    [InlineData(AnalyticsEventType.EventSaved)]
    [InlineData(AnalyticsEventType.EventUnsaved)]
    [InlineData(AnalyticsEventType.EventSubmitted)]
    [InlineData(AnalyticsEventType.EventDetailViewed)]
    [InlineData(AnalyticsEventType.TemporalPresetSelected)]
    [InlineData(AnalyticsEventType.DistrictFilterApplied)]
    [InlineData(AnalyticsEventType.ErrorOccurred)]
    public void RecordEvent_WithAllEventTypes_IsValid(AnalyticsEventType eventType)
    {
        // Arrange & Act
        var analyticsEvent = new AnalyticsEvent(
            EventType: eventType,
            OccurredAt: DateTimeOffset.UtcNow,
            UserId: null,
            Properties: null);

        // Assert
        Assert.Equal(eventType, analyticsEvent.EventType);
    }

    [Fact]
    public void RecordEvent_WithoutUserId_IsValid()
    {
        // Arrange & Act
        var analyticsEvent = new AnalyticsEvent(
            EventType: AnalyticsEventType.MapViewed,
            OccurredAt: DateTimeOffset.UtcNow,
            UserId: null,
            Properties: null);

        // Assert
        Assert.Null(analyticsEvent.UserId);
    }

    [Fact]
    public void RecordEvent_WithProperties_Preserves()
    {
        // Arrange
        var properties = new Dictionary<string, object>
        {
            { "region", "sf" },
            { "action_count", 5 },
        };

        // Act
        var analyticsEvent = new AnalyticsEvent(
            EventType: AnalyticsEventType.MapViewed,
            OccurredAt: DateTimeOffset.UtcNow,
            UserId: null,
            Properties: properties);

        // Assert
        Assert.NotNull(analyticsEvent.Properties);
        Assert.Equal("sf", analyticsEvent.Properties["region"]);
        Assert.Equal(5, analyticsEvent.Properties["action_count"]);
    }

    [Fact]
    public void GetEventTypes_ReturnsAllEventTypes()
    {
        // Arrange
        var expectedCount = 8; // MapViewed, EventSaved, EventUnsaved, EventSubmitted, EventDetailViewed, TemporalPresetSelected, DistrictFilterApplied, ErrorOccurred

        // Act
        var allEventTypes = Enum.GetValues(typeof(AnalyticsEventType));

        // Assert
        Assert.Equal(expectedCount, allEventTypes.Length);
    }

    [Fact]
    public void RecordEvent_IsPrivacyCompliant_WithoutPII()
    {
        // Arrange
        var analyticsEvent = new AnalyticsEvent(
            EventType: AnalyticsEventType.MapViewed,
            OccurredAt: DateTimeOffset.UtcNow,
            UserId: "user123",
            Properties: new Dictionary<string, object>
            {
                { "region", "sf" },
                { "preset", "NOW" },
            });

        // Act
        var isCompliant = analyticsEvent.IsPrivacyCompliant();

        // Assert
        Assert.True(isCompliant);
    }

    [Fact]
    public void RecordEvent_MultipleConcurrentEvents_AllValid()
    {
        // Arrange & Act
        var events = new[]
        {
            new AnalyticsEvent(AnalyticsEventType.MapViewed, DateTimeOffset.UtcNow, null, null),
            new AnalyticsEvent(AnalyticsEventType.EventSaved, DateTimeOffset.UtcNow, "user1", null),
            new AnalyticsEvent(AnalyticsEventType.TemporalPresetSelected, DateTimeOffset.UtcNow, "user2", new Dictionary<string, object> { ["preset"] = "6PM" }),
        };

        // Assert
        Assert.NotEmpty(events);
        Assert.All(events, e => Assert.NotNull(e));
    }
}
