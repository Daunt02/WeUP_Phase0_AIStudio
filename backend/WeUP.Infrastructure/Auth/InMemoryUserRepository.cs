using System.Collections.Concurrent;
using WeUP.Contracts.Auth;
using WeUP.Domain.Users;

namespace WeUP.Infrastructure.Auth;

/// <summary>
/// Phase 0 in-memory user store. Replace with EfUserProfileRepository when
/// the Postgres connection is active (see docs/configuration.md).
/// </summary>
public sealed class InMemoryUserRepository : IUserProfileRepository
{
    private readonly ConcurrentDictionary<string, UserRecord> _byId      = new();
    private readonly ConcurrentDictionary<string, string>     _emailToId = new(StringComparer.OrdinalIgnoreCase);

    public Task<UserProfileDto?> GetByIdAsync(string userId, CancellationToken ct = default)
    {
        _byId.TryGetValue(userId, out var rec);
        return Task.FromResult(rec?.ToDto());
    }

    public Task<UserProfileDto?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        if (!_emailToId.TryGetValue(email, out var id)) return Task.FromResult<UserProfileDto?>(null);
        // Guard: index and record store must stay consistent; defensively handle any divergence.
        if (!_byId.TryGetValue(id, out var rec)) return Task.FromResult<UserProfileDto?>(null);
        return Task.FromResult<UserProfileDto?>(rec.ToDto());
    }

    public Task<UserProfileDto> CreateAsync(string email, string? displayName, string? homeMarket, CancellationToken ct = default)
    {
        var rec = new UserRecord(
            UserId: Guid.NewGuid().ToString("N"),
            Email: email,
            DisplayName: displayName,
            HomeMarket: homeMarket,
            OnboardingState: OnboardingStates.New,
            CreatedAt: DateTimeOffset.UtcNow);

        _byId[rec.UserId]  = rec;
        _emailToId[email]  = rec.UserId;

        return Task.FromResult(rec.ToDto());
    }

    public Task<UserProfileDto> UpdateAsync(string userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        if (!_byId.TryGetValue(userId, out var rec))
            throw new InvalidOperationException($"User '{userId}' not found.");

        var updated = rec with
        {
            DisplayName     = request.DisplayName     ?? rec.DisplayName,
            HomeMarket      = request.HomeMarket      ?? rec.HomeMarket,
            OnboardingState = request.OnboardingState ?? rec.OnboardingState,
        };

        _byId[userId] = updated;
        return Task.FromResult(updated.ToDto());
    }

    private sealed record UserRecord(
        string UserId, string Email,
        string? DisplayName, string? HomeMarket,
        string OnboardingState, DateTimeOffset CreatedAt)
    {
        public UserProfileDto ToDto() =>
            new(UserId, Email, DisplayName, HomeMarket, OnboardingState, CreatedAt);
    }
}
