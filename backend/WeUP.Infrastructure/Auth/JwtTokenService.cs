using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using WeUP.Domain.Users;
using WeUP.Infrastructure.Persistence;

namespace WeUP.Infrastructure.Auth;

public sealed class JwtTokenService(IConfiguration config, WeUpDbContext db) : ITokenService
{
    private const string SecretPath = "WeUP:Auth:JwtSecret";
    private const string ExpiryPath = "WeUP:Auth:JwtExpiryMinutes";
    private const string RefreshExpiryPath = "WeUP:Auth:RefreshExpiryDays";

    public int ExpiresInSeconds => (config.GetValue<int?>(ExpiryPath) ?? 60) * 60;
    public int RefreshExpiresInDays => config.GetValue<int?>(RefreshExpiryPath) ?? 7;

    public string IssueToken(string userId, string email, string[] roles)
    {
        var secret = config[SecretPath];
        if (string.IsNullOrEmpty(secret))
        {
            throw new InvalidOperationException($"JWT Secret is not configured at {SecretPath}.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddSeconds(ExpiresInSeconds),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string? ValidateToken(string token)
    {
        var secret = config[SecretPath];
        if (string.IsNullOrEmpty(secret)) return null;

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(secret);

        try
        {
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            return jwtToken.Claims.First(x => x.Type == JwtRegisteredClaimNames.Sub).Value;
        }
        catch
        {
            return null;
        }
    }

    public string IssueRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public async Task SaveRefreshTokenAsync(string userId, string token, CancellationToken ct = default)
    {
        var refreshToken = new RefreshToken
        {
            UserId = userId,
            Token = token,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(RefreshExpiresInDays)
        };

        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync(ct);
    }

    public async Task<RefreshToken?> GetRefreshTokenAsync(string token, CancellationToken ct = default)
    {
        return await db.RefreshTokens.FirstOrDefaultAsync(x => x.Token == token, ct);
    }

    public async Task RevokeRefreshTokenAsync(string token, string? replacedByToken = null, CancellationToken ct = default)
    {
        var refreshToken = await db.RefreshTokens.FirstOrDefaultAsync(x => x.Token == token, ct);
        if (refreshToken != null)
        {
            refreshToken.IsRevoked = true;
            refreshToken.ReplacedByToken = replacedByToken;
            await db.SaveChangesAsync(ct);
        }
    }
}
