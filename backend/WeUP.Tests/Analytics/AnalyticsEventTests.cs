using Xunit;
using WeUP.Domain.Analytics;

namespace WeUP.Tests.Analytics;

/// <summary>
/// Tests for AnalyticsEvent — P23 Product Analytics
/// </summary>
public class AnalyticsEventTests
{
    [Fact]
    public void AnalyticsEvent_WithValidData_CreatesSuccessfully()
    {
        // Arrange
        var eventType = AnalyticsEventType.MapViewed;
        var userId = "user123";
        var properties = new Dictionary<string, object> { ["region"] = "sf" };

        // Act
        var analyticsEvent = new AnalyticsEvent(
            EventType: eventType,
            OccurredAt: DateTimeOffset.UtcNow,
            UserId: userId,
            Properties: properties);

        // Assert
        Assert.NotNull(analyticsEvent);
        Assert.Equal(eventType, analyticsEvent.EventType);
        Assert.Equal(userId, analyticsEvent.UserId);
    }

    [Fact]
    public void AnalyticsEvent_WithoutUserId_IsValid()
    {
        // Act
        var analyticsEvent = new AnalyticsEvent(
            EventType: AnalyticsEventType.MapViewed,
            OccurredAt: DateTimeOffset.UtcNow,
            UserId: null,
            Properties: null);

        // Assert
        Assert.Null(analyticsEvent.UserId);
    }

    [Fact]
    public void AnalyticsEvent_AllEventTypesAreDefined()
    {
        // Arrange
        var eventTypes = new[]
        {
            AnalyticsEventType.MapViewed,
            AnalyticsEventType.EventSaved,
            AnalyticsEventType.EventUnsaved,
            AnalyticsEventType.EventSubmitted,
            AnalyticsEventType.EventDetailViewed,
            AnalyticsEventType.TemporalPresetSelected,
            AnalyticsEventType.DistrictFilterApplied,
            AnalyticsEventType.ErrorOccurred,
        };

        // Act & Assert
        foreach (var eventType in eventTypes)
        {
            var analyticsEvent = new AnalyticsEvent(
                EventType: eventType,
                OccurredAt: DateTimeOffset.UtcNow,
                UserId: null,
                Properties: null);

            Assert.Equal(eventType, analyticsEvent.EventType);
        }
    }

    [Fact]
    public void AnalyticsEvent_IsPrivacyCompliant_WithoutPII_ReturnsTrue()
    {
        // Arrange
        var analyticsEvent = new AnalyticsEvent(
            EventType: AnalyticsEventType.MapViewed,
            OccurredAt: DateTimeOffset.UtcNow,
            UserId: "user123",
            Properties: new Dictionary<string, object> { ["region"] = "sf" });

        // Act
        var isCompliant = analyticsEvent.IsPrivacyCompliant();

        // Assert
        Assert.True(isCompliant);
    }
}
