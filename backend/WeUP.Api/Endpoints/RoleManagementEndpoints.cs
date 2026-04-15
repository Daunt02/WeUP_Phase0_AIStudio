using WeUP.Contracts.Auth;
using WeUP.Domain.Users;

namespace WeUP.Api.Endpoints;

public static class RoleManagementEndpoints
{
    public static IEndpointRouteBuilder MapRoleManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/users")
            .WithTags("Role Management")
            .AddEndpointFilter<ModeratorAuthorizationFilter>();

        group.MapGet("/{userId}/roles", async (
            string userId,
            IUserRoleRepository roles,
            CancellationToken ct) =>
        {
            var assigned = await roles.GetRolesAsync(userId, ct);
            return Results.Ok(new UserRolesResponse(userId, assigned, DateTimeOffset.UtcNow));
        })
        .WithName("GetUserRoles")
        .WithSummary("Get role assignments for a user");

        group.MapPut("/{userId}/roles", async (
            string userId,
            UpdateUserRolesRequest request,
            IUserRoleRepository roles,
            CancellationToken ct) =>
        {
            try
            {
                var updated = await roles.SetRolesAsync(userId, request.Roles, ct);
                return Results.Ok(new UserRolesResponse(userId, updated, DateTimeOffset.UtcNow));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound();
            }
        })
        .WithName("UpdateUserRoles")
        .WithSummary("Set role assignments for a user");

        return app;
    }
}
