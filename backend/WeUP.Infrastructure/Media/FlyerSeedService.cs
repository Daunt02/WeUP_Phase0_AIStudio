using System.Security.Cryptography;
using WeUP.Domain.Media;

namespace WeUP.Infrastructure.Media;

/// <summary>
/// Seeds the IFlyerAssetStore from JPEG files in the /seed/flyers/ directory at startup.
/// Matches seeded flyer filenames to the 5 stub LA events so the dev pipeline
/// has realistic assets to show without requiring real uploads.
/// </summary>
public sealed class FlyerSeedService
{
    private readonly IFlyerAssetStore _store;

    // Maps seed filename (without extension) → stub event metadata
    private static readonly (string Filename, string EventTitle, string SubmitterId)[] SeedManifest =
    [
        ("evening-street-market", "Evening Street Market", "seed-promoter"),
        ("indie-music-night",     "Indie Music Night",     "seed-promoter"),
        ("community-yoga",        "Community Yoga",        "seed-promoter"),
        ("tech-meetup",           "Tech Meetup",           "seed-promoter"),
        ("rooftop-cinema",        "Rooftop Cinema",        "seed-promoter"),
    ];

    public FlyerSeedService(IFlyerAssetStore store)
    {
        _store = store;
    }

    public async Task SeedAsync(string seedDirectory, CancellationToken ct = default)
    {
        if (!Directory.Exists(seedDirectory))
        {
            Console.WriteLine($"[FlyerSeedService] Seed directory not found: {seedDirectory} — skipping");
            return;
        }

        foreach (var (filename, eventTitle, submitterId) in SeedManifest)
        {
            var filePath = Path.Combine(seedDirectory, $"{filename}.jpg");
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[FlyerSeedService] Missing seed file: {filename}.jpg — skipping");
                continue;
            }

            // Skip if already seeded (store is populated across restarts when using persistent store)
            var storageKey = $"seed_{filename}.jpg";
            var existing = await _store.ListBySubmitterAsync(submitterId, ct);
            if (existing.Any(a => a.StorageKey == storageKey))
                continue;

            var fileBytes = await File.ReadAllBytesAsync(filePath, ct);
            var contentHash = Convert.ToHexString(SHA256.HashData(fileBytes)).ToLowerInvariant();

            var record = new FlyerAssetRecord
            {
                AssetId          = Guid.NewGuid().ToString("N"),
                OriginalFilename = $"{filename}.jpg",
                FileSizeBytes    = fileBytes.Length,
                ContentType      = "image/jpeg",
                SubmitterId      = submitterId,
                UploadedAt       = DateTimeOffset.UtcNow.AddDays(-1),
                Status           = FlyerAssetStatus.Approved,  // seed assets are pre-approved
                ContentHash      = contentHash,
                StorageKey       = storageKey,
                LocalPath        = filePath,
            };

            await _store.SaveAsync(record, ct);
            Console.WriteLine($"[FlyerSeedService] Seeded: {filename} (assetId={record.AssetId[..8]}…)");
        }
    }
}
