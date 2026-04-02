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
    private static readonly string[] DefaultCategories = [];
    private const double DefaultRadiusMeters = 8_000; // 8 km default

    private readonly ConcurrentDictionary<string, UserPreferencesDto> _store = new();

    public Task<UserPreferencesDto> GetAsync(string userId, CancellationToken ct = default)
    {
        if (_store.TryGetValue(userId, out var prefs)) return Task.FromResult(prefs);

        return Task.FromResult(new UserPreferencesDto(
            userId, DefaultCategories, DefaultRadiusMeters,
            NotifyOnNewEvents: false,
            NotifyOnSaveReminders: false,
            PreferredTimeZone: null,
            UpdatedAt: DateTimeOffset.UtcNow));
    }

    public Task<UserPreferencesDto> UpsertAsync(string userId, UpdatePreferencesRequest request, CancellationToken ct = default)
    {
        var current = _store.GetValueOrDefault(userId) ??
            new UserPreferencesDto(userId, DefaultCategories, DefaultRadiusMeters, false, false, null, DateTimeOffset.UtcNow);

        var updated = new UserPreferencesDto(
            UserId: userId,
            PreferredCategories:    request.PreferredCategories    ?? current.PreferredCategories,
            HomeRadiusMeters:       request.HomeRadiusMeters       ?? current.HomeRadiusMeters,
            NotifyOnNewEvents:      request.NotifyOnNewEvents      ?? current.NotifyOnNewEvents,
            NotifyOnSaveReminders:  request.NotifyOnSaveReminders  ?? current.NotifyOnSaveReminders,
            PreferredTimeZone:      request.PreferredTimeZone      ?? current.PreferredTimeZone,
            UpdatedAt: DateTimeOffset.UtcNow);

        _store[userId] = updated;
        return Task.FromResult(updated);
    }
}
