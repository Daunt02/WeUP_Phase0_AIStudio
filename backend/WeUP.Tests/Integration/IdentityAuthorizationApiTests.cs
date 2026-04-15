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

public sealed class IdentityAuthorizationApiTests : IClassFixture<IdentityAuthorizationApiTests.IdentityApiFactory>
{
    private readonly HttpClient _client;

    public IdentityAuthorizationApiTests(IdentityApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    [Fact]
    public async Task Saves_WithoutAuth_Return401()
    {
        var response = await _client.GetAsync("/api/users/me/saves?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Moderation_WithoutAuth_Return401()
    {
        var response = await _client.GetAsync("/api/moderation/queue?pageSize=5");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Moderation_WithNonModerator_Return403()
    {
        await AuthenticateAsync("camille+phase0@weup.test");

        var response = await _client.GetAsync("/api/moderation/queue?pageSize=5");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Moderation_WithModerator_Return200()
    {
        await AuthenticateAsync("moderator+phase0@weup.test");

        var response = await _client.GetAsync("/api/moderation/queue?pageSize=5");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ModerationDecision_ActorStamp_IgnoresSpoofedBodyActorId()
    {
        await AuthenticateAsync("moderator+phase0@weup.test");

        var decisionResponse = await _client.PostAsJsonAsync(
            "/api/moderation/reviews/mod-sf-neon-market/approve",
            new ReviewDecisionRequest(
                ActorId: "spoofed-user-id",
                Decision: ReviewDecisionKind.Approve,
                Comment: "Approve with spoofed actor id in payload"));

        Assert.Equal(HttpStatusCode.OK, decisionResponse.StatusCode);

        var historyResponse = await _client.GetAsync("/api/moderation/reviews/mod-sf-neon-market/history?pageSize=20");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        var history = await historyResponse.Content.ReadFromJsonAsync<ReviewAuditRecord[]>();
        Assert.NotNull(history);
        Assert.NotEmpty(history!);

        var stamped = history!.FirstOrDefault(r =>
            string.Equals(r.Comment, "Approve with spoofed actor id in payload", StringComparison.Ordinal));
        Assert.NotNull(stamped);
        Assert.Equal("user-sf-moderator", stamped!.ActorId);
        Assert.NotEqual("spoofed-user-id", stamped.ActorId);
    }

    [Fact]
    public async Task RoleManagement_WithNonModerator_Return403()
    {
        await AuthenticateAsync("camille+phase0@weup.test");

        var response = await _client.GetAsync("/api/admin/users/user-sf-camille/roles");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RoleManagement_WithModerator_CanUpdateRoles()
    {
        await AuthenticateAsync("moderator+phase0@weup.test");

        var update = await _client.PutAsJsonAsync(
            "/api/admin/users/user-sf-camille/roles",
            new UpdateUserRolesRequest(["user", "moderator"]));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var updated = await update.Content.ReadFromJsonAsync<UserRolesResponse>();
        Assert.NotNull(updated);
        Assert.Contains(UserRoles.Moderator, updated!.Roles, StringComparer.OrdinalIgnoreCase);

        var get = await _client.GetAsync("/api/admin/users/user-sf-camille/roles");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var resolved = await get.Content.ReadFromJsonAsync<UserRolesResponse>();
        Assert.NotNull(resolved);
        Assert.Contains(UserRoles.Moderator, resolved!.Roles, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmissionOwnership_EnforcedAcrossUsers()
    {
        await AuthenticateAsync("camille+phase0@weup.test");

        var create = await _client.PostAsJsonAsync("/api/events/submissions", new DraftSubmissionRequest(
            Title: "Ownership Test",
            VenueName: "Public Works",
            Address: "161 Erie St, San Francisco, CA 94103",
            StartUtc: DateTimeOffset.Parse("2026-04-13T04:00:00Z"),
            EndUtc: DateTimeOffset.Parse("2026-04-13T07:00:00Z"),
            Timezone: "America/Los_Angeles",
            Category: "nightlife",
            Description: "Ownership boundary verification",
            Tags: ["Ownership"],
            FlyerAssetIds: null));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var draft = await create.Content.ReadFromJsonAsync<SubmissionDto>();
        Assert.NotNull(draft);

        await AuthenticateAsync("moderator+phase0@weup.test");

        var byId = await _client.GetAsync($"/api/events/submissions/{draft!.SubmissionId}");
        Assert.Equal(HttpStatusCode.Forbidden, byId.StatusCode);

        var status = await _client.GetAsync($"/api/events/submissions/{draft.SubmissionId}/status");
        Assert.Equal(HttpStatusCode.Forbidden, status.StatusCode);
    }

    private async Task AuthenticateAsync(string email)
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var login = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(email));
        login.EnsureSuccessStatusCode();

        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
    }

    public sealed class IdentityApiFactory : WebApplicationFactory<Program>
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
