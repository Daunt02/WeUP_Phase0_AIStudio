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

    private sealed record TokenEntry(string UserId, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, TokenEntry> _tokens = new();

    public string IssueToken(string userId)
    {
        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"); // 64-char hex
        var entry = new TokenEntry(userId, DateTimeOffset.UtcNow.AddSeconds(ExpiresInSeconds));
        _tokens[token] = entry;
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
        return entry.UserId;
    }
}
