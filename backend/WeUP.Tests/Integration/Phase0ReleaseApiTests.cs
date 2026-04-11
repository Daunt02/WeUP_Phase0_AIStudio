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
        var queueResponse = await _client.GetAsync("/api/moderation/queue?pageSize=5");
        queueResponse.EnsureSuccessStatusCode();

        var queue = await queueResponse.Content.ReadFromJsonAsync<ModerationQueueResponse>();
        Assert.NotNull(queue);
        Assert.Contains(queue!.Items, item => item.ItemId == "mod-sf-neon-market");

        var approve = await _client.PostAsJsonAsync("/api/moderation/queue/mod-sf-neon-market/approve", new
        {
            actorId = "user-sf-moderator",
            note = "Release hardening approval path",
            publishedEventId = "evt-sf-midnight-groove",
        });

        approve.EnsureSuccessStatusCode();
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