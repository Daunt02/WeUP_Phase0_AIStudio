using Microsoft.EntityFrameworkCore;
using WeUP.Contracts.Auth;
using WeUP.Domain.Users;
using WeUP.Infrastructure.Persistence;
using WeUP.Infrastructure.Persistence.Entities;

namespace WeUP.Infrastructure.Auth;

public sealed class EfUserProfileRepository(WeUpDbContext db) : IUserProfileRepository
{
    public async Task<UserProfileDto?> GetByIdAsync(string userId, CancellationToken ct = default)
    {
        var entity = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.PublicId == userId, ct);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<UserProfileDto?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var entity = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<UserProfileDto> CreateAsync(string email, string? displayName, string? homeMarket, CancellationToken ct = default)
    {
        var entity = new UserProfileEntity
        {
            Id = Guid.NewGuid(),
            PublicId = $"user-{Guid.NewGuid():N}",
            Email = email,
            DisplayName = displayName,
            HomeMarket = homeMarket,
            OnboardingState = OnboardingStates.New,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        db.UserProfiles.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<UserProfileDto> UpdateAsync(string userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var entity = await db.UserProfiles.FirstOrDefaultAsync(u => u.PublicId == userId, ct)
            ?? throw new InvalidOperationException($"User '{userId}' not found.");

        entity.DisplayName = request.DisplayName ?? entity.DisplayName;
        entity.HomeMarket = request.HomeMarket ?? entity.HomeMarket;
        entity.OnboardingState = request.OnboardingState ?? entity.OnboardingState;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    private static UserProfileDto ToDto(UserProfileEntity entity) =>
        new(entity.PublicId, entity.Email, entity.DisplayName, entity.HomeMarket, entity.OnboardingState, entity.CreatedAt);
}