using System.Collections.Concurrent;
using WeUP.Contracts.Users;
using WeUP.Domain.Users;

namespace WeUP.Infrastructure.Auth;

/// <summary>
/// Phase 0 in-memory preferences store.
/// Returns safe defaults for users with no saved preferences.
/// </summary>
public sealed class InMemoryPreferencesRepository : IUserPreferencesRepository
{
    private const double DefaultRadiusMeters = 8_000;

    private readonly ConcurrentDictionary<string, UserPreferencesDto> _store = new();

    public Task<UserPreferencesDto> GetAsync(string userId, CancellationToken ct = default)
    {
        var prefs = _store.GetValueOrDefault(userId) ?? Defaults(userId);
        return Task.FromResult(prefs);
    }

    public Task<UserPreferencesDto> UpsertAsync(string userId, UpdatePreferencesRequest request, CancellationToken ct = default)
    {
        var current = _store.GetValueOrDefault(userId) ?? Defaults(userId);

        var updated = new UserPreferencesDto(
            UserId:                userId,
            PreferredCategories:   request.PreferredCategories   ?? current.PreferredCategories,
            HomeRadiusMeters:      request.HomeRadiusMeters      ?? current.HomeRadiusMeters,
            NotifyOnNewEvents:     request.NotifyOnNewEvents     ?? current.NotifyOnNewEvents,
            NotifyOnSaveReminders: request.NotifyOnSaveReminders ?? current.NotifyOnSaveReminders,
            PreferredTimeZone:     request.PreferredTimeZone     ?? current.PreferredTimeZone,
            LastKnownMapCenterLat: request.LastKnownMapCenterLat ?? current.LastKnownMapCenterLat,
            LastKnownMapCenterLng: request.LastKnownMapCenterLng ?? current.LastKnownMapCenterLng,
            UpdatedAt:             DateTimeOffset.UtcNow);

        _store[userId] = updated;
        return Task.FromResult(updated);
    }

    private static UserPreferencesDto Defaults(string userId) =>
        new(userId, [], DefaultRadiusMeters,
            NotifyOnNewEvents: false, NotifyOnSaveReminders: false,
            PreferredTimeZone: null,
            LastKnownMapCenterLat: null,
            LastKnownMapCenterLng: null,
            UpdatedAt: DateTimeOffset.UtcNow);
}
