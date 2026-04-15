using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeUP.Contracts.Users;
using WeUP.Domain.Users;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Auth;

public sealed class EfUserPreferencesRepository(WeUpDbContext db) : IUserPreferencesRepository
{
    private const double DefaultRadiusMeters = 8_000;

    public async Task<UserPreferencesDto> GetAsync(string userId, CancellationToken ct = default)
    {
        var user = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.PublicId == userId, ct)
            ?? throw new InvalidOperationException($"User '{userId}' not found.");

        var entity = await db.UserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);

        return entity is null ? Defaults(userId) : ToDto(userId, entity);
    }

    public async Task<UserPreferencesDto> UpsertAsync(string userId, UpdatePreferencesRequest request, CancellationToken ct = default)
    {
        var user = await db.UserProfiles
            .FirstOrDefaultAsync(u => u.PublicId == userId, ct)
            ?? throw new InvalidOperationException($"User '{userId}' not found.");

        var entity = await db.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);

        if (entity is null)
        {
            entity = new UserPreferencesEntity
            {
                UserId = user.Id,
                PreferredCategoriesJson = "[]",
                HomeRadiusMeters = DefaultRadiusMeters,
                NotifyOnNewEvents = false,
                NotifyOnSaveReminders = false,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            db.UserPreferences.Add(entity);
        }

        var currentCategories = ParseCategories(entity.PreferredCategoriesJson);

        var nextCategories = request.PreferredCategories ?? currentCategories;
        entity.PreferredCategoriesJson = JsonSerializer.Serialize(nextCategories);
        entity.HomeRadiusMeters = request.HomeRadiusMeters ?? entity.HomeRadiusMeters;
        entity.NotifyOnNewEvents = request.NotifyOnNewEvents ?? entity.NotifyOnNewEvents;
        entity.NotifyOnSaveReminders = request.NotifyOnSaveReminders ?? entity.NotifyOnSaveReminders;
        entity.PreferredTimeZone = request.PreferredTimeZone ?? entity.PreferredTimeZone;
        entity.LastKnownMapCenterLat = request.LastKnownMapCenterLat ?? entity.LastKnownMapCenterLat;
        entity.LastKnownMapCenterLng = request.LastKnownMapCenterLng ?? entity.LastKnownMapCenterLng;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return ToDto(userId, entity);
    }

    private static UserPreferencesDto ToDto(string userId, UserPreferencesEntity entity) =>
        new(
            userId,
            ParseCategories(entity.PreferredCategoriesJson),
            entity.HomeRadiusMeters,
            entity.NotifyOnNewEvents,
            entity.NotifyOnSaveReminders,
            entity.PreferredTimeZone,
            entity.LastKnownMapCenterLat,
            entity.LastKnownMapCenterLng,
            entity.UpdatedAt);

    private static UserPreferencesDto Defaults(string userId) =>
        new(
            userId,
            [],
            DefaultRadiusMeters,
            NotifyOnNewEvents: false,
            NotifyOnSaveReminders: false,
            PreferredTimeZone: null,
            LastKnownMapCenterLat: null,
            LastKnownMapCenterLng: null,
            UpdatedAt: DateTimeOffset.UtcNow);

    private static string[] ParseCategories(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
