using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using WeUP.Contracts.Auth;
using WeUP.Contracts.Events;
using WeUP.Contracts.Saves;
using WeUP.Domain.Events;
using WeUP.Domain.Users;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Seed;
using Xunit;

namespace WeUP.Tests.Integration;

public sealed class PostgresPersistenceApiTests : IClassFixture<PostgresPersistenceApiTests.PostgresApiFactory>
{
    private readonly PostgresApiFactory _factory;
    private readonly HttpClient _client;

    public PostgresPersistenceApiTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Health_ReturnsHealthyPersistenceDetails()
    {
        await _factory.ResetSeedAsync();

        var response = await _client.GetAsync("/health");
        response.EnsureSuccessStatusCode();

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", payload.RootElement.GetProperty("status").GetString());
        var persistence = payload.RootElement.GetProperty("checks").GetProperty("persistence");
        Assert.Equal("Healthy", persistence.GetProperty("status").GetString());
        Assert.Equal("Postgres", persistence.GetProperty("data").GetProperty("mode").GetString());
    }

    [Fact]
    public async Task MapCalendarAndDetail_Flows_ReadSeededPostgresData()
    {
        await _factory.ResetSeedAsync();

        var window = new TimeWindowRequest(
            DateTimeOffset.Parse("2026-04-11T00:00:00Z"),
            DateTimeOffset.Parse("2026-04-13T00:00:00Z"),
            "America/Los_Angeles");

        var map = await _client.PostAsJsonAsync("/api/events/map", new MapFeedRequest(
            new GeoBoundingBox(37.70, 37.85, -122.52, -122.37),
            window));
        map.EnsureSuccessStatusCode();
        var mapPayload = await map.Content.ReadFromJsonAsync<MapFeedResponse>();
        Assert.NotNull(mapPayload);
        Assert.Contains(mapPayload!.Events, evt => evt.Id == "evt-sf-midnight-groove");

        var calendar = await _client.PostAsJsonAsync("/api/events/calendar", new CalendarFeedRequest(window));
        calendar.EnsureSuccessStatusCode();
        var calendarPayload = await calendar.Content.ReadFromJsonAsync<CalendarFeedResponse>();
        Assert.NotNull(calendarPayload);
        Assert.Contains(calendarPayload!.Items, evt => evt.Id == "evt-sf-rooftop-signals");

        var detail = await _client.GetAsync("/api/events/evt-sf-waterfront-jazz");
        detail.EnsureSuccessStatusCode();
        var detailPayload = await detail.Content.ReadFromJsonAsync<EventDetailResponse>();
        Assert.NotNull(detailPayload?.Event);
        Assert.Equal("Waterfront Jazz Circuit", detailPayload!.Event!.Title);
    }

    [Fact]
    public async Task SaveAndUnsave_Flow_PersistsAcrossRequests()
    {
        await _factory.ResetSeedAsync();
        await AuthenticateAsync();

        var save = await _client.PostAsync("/api/users/me/saves/evt-sf-midnight-groove", content: null);
        save.EnsureSuccessStatusCode();

        var saves = await _client.GetAsync("/api/users/me/saves?page=1&pageSize=20");
        saves.EnsureSuccessStatusCode();
        var list = await saves.Content.ReadFromJsonAsync<SavedEventsResponse>();
        Assert.NotNull(list);
        Assert.Contains(list!.Items, item => item.EventId == "evt-sf-midnight-groove");

        var unsave = await _client.DeleteAsync("/api/users/me/saves/evt-sf-midnight-groove");
        unsave.EnsureSuccessStatusCode();

        var after = await _client.GetAsync("/api/users/me/saves?page=1&pageSize=20");
        after.EnsureSuccessStatusCode();
        var afterList = await after.Content.ReadFromJsonAsync<SavedEventsResponse>();
        Assert.NotNull(afterList);
        Assert.DoesNotContain(afterList!.Items, item => item.EventId == "evt-sf-midnight-groove");
    }

    [Fact]
    public async Task SubmissionCreationAndSubmit_Flow_PersistsInDatabase()
    {
        await _factory.ResetSeedAsync();
        await AuthenticateAsync();

        var create = await _client.PostAsJsonAsync("/api/events/submissions", new DraftSubmissionRequest(
            Title: "Persistence Night",
            VenueName: "Public Works",
            Address: "161 Erie St, San Francisco, CA 94103",
            StartUtc: DateTimeOffset.Parse("2026-04-13T04:00:00Z"),
            EndUtc: DateTimeOffset.Parse("2026-04-13T07:00:00Z"),
            Timezone: "America/Los_Angeles",
            Category: "nightlife",
            Description: "EF-backed submission smoke test",
            Tags: ["Late Night"],
            FlyerAssetIds: null));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var draft = await create.Content.ReadFromJsonAsync<SubmissionDto>();
        Assert.NotNull(draft);

        var get = await _client.GetAsync($"/api/events/submissions/{draft!.SubmissionId}");
        get.EnsureSuccessStatusCode();

        var submit = await _client.PostAsync($"/api/events/submissions/{draft.SubmissionId}/submit", content: null);
        Assert.Equal(HttpStatusCode.Accepted, submit.StatusCode);

        var status = await _client.GetAsync($"/api/events/submissions/{draft.SubmissionId}/status");
        status.EnsureSuccessStatusCode();
        var body = await status.Content.ReadAsStringAsync();
        Assert.Contains("SubmittedForReview", body);
    }

    [Fact]
    public async Task EfRepositories_ReadAndWriteAgainstSameDatabase()
    {
        await _factory.ResetSeedAsync();

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
        var saves = scope.ServiceProvider.GetRequiredService<ISaveRepository>();
        var events = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var db = scope.ServiceProvider.GetRequiredService<WeUpDbContext>();

        var user = await users.GetByEmailAsync("camille+phase0@weup.test");
        Assert.NotNull(user);

        var saveResult = await saves.SaveEventAsync(user!.UserId, "evt-sf-midnight-groove");
        Assert.True(saveResult.Saved);
        Assert.Equal(2, await db.SavedEvents.CountAsync());

        var detail = await events.GetEventDetailAsync("evt-sf-midnight-groove");
        Assert.NotNull(detail.Event);
        Assert.Equal("evt-sf-midnight-groove", detail.Event!.Id);
    }

    [Fact]
    public void PostgresMode_WithoutConnectionString_FailsFast()
    {
        using var factory = new MisconfiguredPostgresFactory();
        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("ConnectionStrings:WeUpDb", ex.Message);
    }

    private async Task AuthenticateAsync()
    {
        var login = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("camille+phase0@weup.test"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
    }

    public sealed class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("weup_phase0_test")
            .WithUsername("weup")
            .WithPassword("weup_test")
            .Build();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("WeUP:PersistenceMode", "Postgres");
            builder.UseSetting("WeUP:Database:ConnectionStringName", "WeUpDb");
            builder.UseSetting("WeUP:Database:AutoApplyMigrations", "true");
            builder.UseSetting("WeUP:Database:RequireConnectivity", "true");
            builder.UseSetting("WeUP:Database:FailOnPendingMigrations", "true");
            builder.UseSetting("ConnectionStrings:WeUpDb", _postgres.GetConnectionString());
            builder.UseSetting("SeedData:EnableOnStartup", "true");
            builder.UseSetting("SeedData:EnableResetEndpoint", "true");
            builder.UseSetting("SeedData:DatasetPath", "..\\..\\seed\\phase0-dataset.json");
        }

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();
        }

        public async Task ResetSeedAsync()
        {
            using var scope = Services.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<Phase0SeedService>();
            await seeder.ResetAsync();
        }

        async Task IAsyncLifetime.DisposeAsync()
        {
            await _postgres.DisposeAsync();
            await DisposeAsync();
        }
    }

    private sealed class MisconfiguredPostgresFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("WeUP:PersistenceMode", "Postgres");
            builder.UseSetting("ConnectionStrings:WeUpDb", "");
        }
    }
}