using System.Security.Cryptography;
using WeUP.Domain.Media;

namespace WeUP.Infrastructure.Media;

/// <summary>
/// Seeds the IFlyerAssetStore from real Houston flyer files in the /flyers/ directory at startup.
/// Falls back to synthetic stubs in /seed/flyers/ if real flyers aren't present.
/// OCR-extracted from real Houston event flyers (April 2025).
/// </summary>
public sealed class FlyerSeedService
{
    private readonly IFlyerAssetStore _store;

    // Maps real flyer filename (without extension) → Houston event metadata
    // Ordered to match StubEventRepository.SampleMapCards
    private static readonly (string Filename, string Extension, string EventTitle, string SubmitterId)[] SeedManifest =
    [
        ("IMG_6661",                                              "PNG", "First Friday — Alternative Night Market",                      "seed-promoter"),
        ("att.-WO_ZOhxl0746HZJ_wrwt7qkknqNgoENAf86pxQzzTg",    "jpg", "Patrick Squier Live",                                          "seed-promoter"),
        ("IMG_6667",                                              "PNG", "Effin: Dennett / Joli / Jilli",                                "seed-promoter"),
        ("IMG_6671",                                              "PNG", "Foundation Room After Dark — VIP Bay Experience",              "seed-promoter"),
        ("IMG_6678",                                              "PNG", "Noche de Selena",                                              "seed-promoter"),
        ("IMG_6679",                                              "PNG", "Freestyle Session + House Class",                              "seed-promoter"),
        ("IMG_6681",                                              "PNG", "Iistbahnhof: Ariel Zetina / Lauren Flax / Partok / S4M23",    "seed-promoter"),
        ("IMG_6664",                                              "PNG", "Desert Hearts — Mikey Lion / Lee Reynolds / Marbs",            "seed-promoter"),
        ("IMG_6662",                                              "PNG", "Mahmut Orhon",                                                 "seed-promoter"),
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

        foreach (var (filename, ext, eventTitle, submitterId) in SeedManifest)
        {
            var filePath = Path.Combine(seedDirectory, $"{filename}.{ext}");
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[FlyerSeedService] Missing seed file: {filename}.{ext} — skipping");
                continue;
            }

            var storageKey = $"seed_{filename}.{ext}";
            var existing = await _store.ListBySubmitterAsync(submitterId, ct);
            if (existing.Any(a => a.StorageKey == storageKey))
                continue;

            var fileBytes = await File.ReadAllBytesAsync(filePath, ct);
            var contentHash = Convert.ToHexString(SHA256.HashData(fileBytes)).ToLowerInvariant();
            var contentType = ext.Equals("jpg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg" : "image/png";

            var record = new FlyerAssetRecord
            {
                AssetId          = Guid.NewGuid().ToString("N"),
                OriginalFilename = $"{filename}.{ext}",
                FileSizeBytes    = fileBytes.Length,
                ContentType      = contentType,
                SubmitterId      = submitterId,
                UploadedAt       = DateTimeOffset.UtcNow.AddDays(-1),
                Status           = FlyerAssetStatus.Approved,
                ContentHash      = contentHash,
                StorageKey       = storageKey,
                LocalPath        = filePath,
            };

            await _store.SaveAsync(record, ct);
            Console.WriteLine($"[FlyerSeedService] Seeded: {filename} ({eventTitle}, assetId={record.AssetId[..8]}…)");
        }
    }
}
