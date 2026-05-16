using System.Collections.Concurrent;
using WeUP.Domain.Users;

namespace WeUP.Infrastructure.Auth;

public sealed class InMemoryTokenService : ITokenService
{
    private readonly ConcurrentDictionary<string, string> _tokens = new();
    private readonly ConcurrentDictionary<string, RefreshToken> _refreshTokens = new();

    public int ExpiresInSeconds => 3600;

    public string IssueToken(string userId, string email, string[] roles)
    {
        var token = $"stub-token-{userId}-{Guid.NewGuid():N}";
        _tokens[token] = userId;
        return token;
    }

    public string? ValidateToken(string token)
    {
        if (_tokens.TryGetValue(token, out var userId))
        {
            return userId;
        }

        if (token.StartsWith("stub-token-"))
        {
            var parts = token.Split('-');
            if (parts.Length >= 3) return parts[2];
        }

        return null;
    }

    public string IssueRefreshToken()
    {
        return $"stub-refresh-{Guid.NewGuid():N}";
    }

    public Task SaveRefreshTokenAsync(string userId, string token, CancellationToken ct = default)
    {
        _refreshTokens[token] = new RefreshToken
        {
            UserId = userId,
            Token = token,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> GetRefreshTokenAsync(string token, CancellationToken ct = default)
    {
        _refreshTokens.TryGetValue(token, out var refreshToken);
        return Task.FromResult(refreshToken);
    }

    public Task RevokeRefreshTokenAsync(string token, string? replacedByToken = null, CancellationToken ct = default)
    {
        if (_refreshTokens.TryGetValue(token, out var refreshToken))
        {
            refreshToken.IsRevoked = true;
            refreshToken.ReplacedByToken = replacedByToken;
        }
        return Task.CompletedTask;
    }
}
