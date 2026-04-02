using System.Collections.Concurrent;
using WeUP.Domain.Users;

namespace WeUP.Infrastructure.Auth;

/// <summary>
/// Issues opaque bearer tokens (UUID) mapped to user IDs.
/// Phase 0 implementation — in-memory, non-persistent, 24 h expiry.
///
/// Upgrade path (P16.5+):
///   Replace with JwtTokenService using Microsoft.AspNetCore.Authentication.JwtBearer.
///   Set WeUp:Auth:JwtSecret and WeUp:Auth:JwtIssuer in appsettings.
/// </summary>
public sealed class BearerTokenService : ITokenService
{
    public int ExpiresInSeconds => 86_400; // 24 hours

    // Sweep expired tokens after every N validations to bound memory growth.
    private const int SweepInterval = 200;
    private int _validateCount;

    private sealed record TokenEntry(string UserId, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, TokenEntry> _tokens = new();

    public string IssueToken(string userId)
    {
        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        _tokens[token] = new TokenEntry(userId, DateTimeOffset.UtcNow.AddSeconds(ExpiresInSeconds));
        return token;
    }

    public string? ValidateToken(string token)
    {
        if (!_tokens.TryGetValue(token, out var entry)) return null;
        if (entry.ExpiresAt < DateTimeOffset.UtcNow)
        {
            _tokens.TryRemove(token, out _);
            return null;
        }

        // Periodic sweep — remove expired entries so the dictionary stays bounded.
        if (Interlocked.Increment(ref _validateCount) % SweepInterval == 0)
            SweepExpired();

        return entry.UserId;
    }

    private void SweepExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (key, entry) in _tokens)
        {
            if (entry.ExpiresAt < now)
                _tokens.TryRemove(key, out _);
        }
    }
}
