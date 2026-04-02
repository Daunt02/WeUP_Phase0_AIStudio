using WeUP.Contracts.Users;

namespace WeUP.Domain.Users;

/// <summary>
/// Manages a user's personally ordered event plan.
/// Items are ordered by Position (1-based). Adding at an occupied position shifts others down.
/// </summary>
public interface IItineraryRepository
{
    Task<ItineraryResponse> GetAsync(string userId, CancellationToken ct = default);
    Task<ItineraryItemResponse> AddAsync(string userId, AddToItineraryRequest request, CancellationToken ct = default);
    Task<ItineraryItemResponse> UpdateItemAsync(string userId, string itemId, UpdateItineraryItemRequest request, CancellationToken ct = default);
    Task<ItineraryItemResponse> RemoveAsync(string userId, string itemId, CancellationToken ct = default);
}

/// <summary>Stores per-user discovery and notification preferences.</summary>
public interface IUserPreferencesRepository
{
    Task<UserPreferencesDto> GetAsync(string userId, CancellationToken ct = default);
    Task<UserPreferencesDto> UpsertAsync(string userId, UpdatePreferencesRequest request, CancellationToken ct = default);
}
