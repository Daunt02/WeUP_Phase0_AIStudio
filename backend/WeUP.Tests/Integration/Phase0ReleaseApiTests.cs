using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using WeUP.Contracts.Auth;
using WeUP.Contracts.Events;
using WeUP.Contracts.Moderation;
using Xunit;

namespace WeUP.Tests.Integration;

public sealed class Phase0ReleaseApiTests : IClassFixture<Phase0ReleaseApiTests.Phase0ApiFactory>
{
    private readonly HttpClient _client;

    public Phase0ReleaseApiTests(Phase0ApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MapFeed_ReturnsSeededPrimaryEvent()
    {
        var response = await _client.PostAsJsonAsync("/api/events/map", new MapFeedRequest(
            new GeoBoundingBox(37.70, 37.85, -122.52, -122.37),
            new TimeWindowRequest(DateTimeOffset.Parse("2026-04-11T00:00:00Z"), DateTimeOffset.Parse("2026-04-13T00:00:00Z"), "America/Los_Angeles")));

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<MapFeedResponse>();

        Assert.NotNull(payload);
        Assert.Contains(payload!.Events, evt => evt.Id == "evt-sf-midnight-groove");
    }

    [Fact]
    public async Task Auth_Save_And_Me_Flow_WorkAgainstSeededUser()
    {
        var login = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("camille+phase0@weup.test"));
        login.EnsureSuccessStatusCode();

        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        var me = await _client.GetAsync("/auth/me");
        me.EnsureSuccessStatusCode();

        var save = await _client.PostAsync("/api/users/me/saves/evt-sf-midnight-groove", content: null);
        save.EnsureSuccessStatusCode();

        var saves = await _client.GetAsync("/api/users/me/saves?page=1&pageSize=10");
        saves.EnsureSuccessStatusCode();

        var payload = await saves.Content.ReadAsStringAsync();
        Assert.Contains("evt-sf-midnight-groove", payload);
    }

    [Fact]
    public async Task ModerationQueue_ApproveFlow_WorksForSeededItem()
    {
        await AuthenticateAsync("moderator+phase0@weup.test");

        var queueResponse = await _client.GetAsync("/api/moderation/queue?pageSize=5");
        queueResponse.EnsureSuccessStatusCode();

        var queue = await queueResponse.Content.ReadFromJsonAsync<ModerationQueueResponse>();
        Assert.NotNull(queue);
        Assert.Contains(queue!.Items, item => item.ItemId == "mod-sf-neon-market");

        var approve = await _client.PostAsJsonAsync("/api/moderation/queue/mod-sf-neon-market/approve", new
        {
            actorId = "ignored-by-server",
            note = "Release hardening approval path",
            publishedEventId = "evt-sf-midnight-groove",
        });

        approve.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task MapFeedV1_Returns422_ForInvalidBboxShape()
    {
        var response = await _client.GetAsync(
            "/api/events/map-feed/v1?bbox=-122.52,37.70,-122.37&timeWindowPreset=weekend");

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
    }

    [Fact]
    public async Task MapFeedV1_Returns422_WhenNoTemporalWindowProvided()
    {
        var response = await _client.GetAsync(
            "/api/events/map-feed/v1?bbox=-122.52,37.70,-122.37,37.85");

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
    }

    [Fact]
    public async Task MapFeedV1_Returns422_ForInvalidAbsoluteWindowRange()
    {
        var response = await _client.GetAsync(
            "/api/events/map-feed/v1?bbox=-122.52,37.70,-122.37,37.85&fromUtc=2026-04-13T00:00:00Z&toUtc=2026-04-11T00:00:00Z");

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
    }

    [Fact]
    public async Task MapFeedV1_ReturnsDeterministicMarkerStates_WithSavedInvariant()
    {
        await AuthenticateAsync("camille+phase0@weup.test");

        var response = await _client.GetAsync(
            BuildMapFeedV1Path(
                "-122.52,37.70,-122.37,37.85",
                fromUtc: "2026-04-11T00:00:00Z",
                toUtc: "2026-04-13T00:00:00Z",
                includeSavedOnly: false));

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<EventMapFeedV1ResponseDto>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload!.Events);
        Assert.All(payload.Events, evt => Assert.False(string.IsNullOrWhiteSpace(evt.EventId)));
        Assert.NotNull(payload.Clusters);
        Assert.NotNull(payload.DensityControl);
        Assert.Equal("client_v1", payload.ClusterStrategy);
        Assert.True(payload.DensityControl.SelectedMarkerBypassEnabled);
        Assert.Equal("zoom_or_expand", payload.DensityControl.ExpansionBehavior);

        var uniqueEventIds = payload.Events.Select(evt => evt.EventId).Distinct().ToArray();
        Assert.Equal(payload.Events.Length, uniqueEventIds.Length);
        Assert.DoesNotContain(payload.Events, evt => evt.MarkerState == "low-confidence-hidden");

        var saved = payload.Events.Single(evt => evt.EventId == "evt-sf-rooftop-signals");
        Assert.True(saved.SavedByCurrentUser);
        Assert.Equal("saved", saved.MarkerState);

        Assert.All(
            payload.Events.Where(evt => !evt.SavedByCurrentUser),
            evt => Assert.Equal("default", evt.MarkerState));
    }

    [Fact]
    public async Task MapFeedV1_IncludeSavedOnly_ReturnsOnlySavedEvents()
    {
        await AuthenticateAsync("camille+phase0@weup.test");

        var response = await _client.GetAsync(
            BuildMapFeedV1Path(
                "-122.52,37.70,-122.37,37.85",
                fromUtc: "2026-04-11T00:00:00Z",
                toUtc: "2026-04-13T00:00:00Z",
                includeSavedOnly: true));

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<EventMapFeedV1ResponseDto>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload!.Events);
        Assert.All(payload.Events, evt => Assert.True(evt.SavedByCurrentUser));
        Assert.All(payload.Events, evt => Assert.Equal("saved", evt.MarkerState));
        Assert.Equal(payload.Events.Length, payload.Events.Select(evt => evt.EventId).Distinct().Count());
        Assert.NotNull(payload.Clusters);
        Assert.NotNull(payload.DensityControl);
        Assert.True(payload.DensityControl.SelectedMarkerBypassEnabled);
    }

    private static string BuildMapFeedV1Path(
        string bbox,
        string? timeWindowPreset = null,
        string? fromUtc = null,
        string? toUtc = null,
        bool includeSavedOnly = false)
    {
        var query = $"bbox={Uri.EscapeDataString(bbox)}&includeSavedOnly={includeSavedOnly.ToString().ToLowerInvariant()}";

        if (!string.IsNullOrWhiteSpace(timeWindowPreset))
        {
            query += $"&timeWindowPreset={Uri.EscapeDataString(timeWindowPreset)}";
        }

        if (!string.IsNullOrWhiteSpace(fromUtc))
        {
            query += $"&fromUtc={Uri.EscapeDataString(fromUtc)}";
        }

        if (!string.IsNullOrWhiteSpace(toUtc))
        {
            query += $"&toUtc={Uri.EscapeDataString(toUtc)}";
        }

        return $"/api/events/map-feed/v1?{query}";
    }

    private async Task AuthenticateAsync(string email)
    {
        var login = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(email));
        login.EnsureSuccessStatusCode();

        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
    }

    public sealed class Phase0ApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("SeedData:EnableOnStartup", "true");
            builder.UseSetting("SeedData:EnableResetEndpoint", "true");
            builder.UseSetting("SeedData:DatasetPath", "..\\..\\seed\\phase0-dataset.json");
        }
    }
}