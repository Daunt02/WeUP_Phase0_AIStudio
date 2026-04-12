using Xunit;
using WeUP.Domain.Media;
using WeUP.Infrastructure.Media;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;

namespace WeUP.Tests.Integration;

/// <summary>
/// P25/P26: Flyer asset validation, lifecycle transitions, and store behavior.
/// </summary>
public class MediaEndpointsTests
{
    // ── P25 contract shape tests ─────────────────────────────────────────────

    [Fact]
    public void FlyerAsset_Constructs_WithCorrectShape()
    {
        var asset = new FlyerAsset(
            "id1",
            "flyer.jpg",
            12345,
            "image/jpeg",
            "user1",
            DateTimeOffset.UtcNow,
            "ProcessingPending",
            "hash",
            "flyers/202604/id1.jpg",
            1,
            null,
            512,
            512,
            false);
        Assert.Equal("id1", asset.AssetId);
        Assert.Equal("flyer.jpg", asset.OriginalFilename);
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    public void FlyerPolicy_AllowedMimeTypes_ContainsExpected(string mime)
    {
        Assert.Contains(mime, FlyerPolicy.AllowedMimeTypes);
    }

    [Fact]
    public void FlyerPolicy_MaxFileSize_IsTenMb()
    {
        Assert.Equal(10 * 1024 * 1024, FlyerPolicy.MaxFileSizeBytes);
    }

    // ── P26 lifecycle transition tests ───────────────────────────────────────

    [Fact]
    public void Lifecycle_Initialized_CanTransition_ToUploaded()
    {
        var next = FlyerAssetLifecycle.Transition(FlyerAssetStatus.Initialized, FlyerAssetStatus.Uploaded);
        Assert.Equal(FlyerAssetStatus.Uploaded, next);
    }

    [Fact]
    public void Lifecycle_Uploaded_CanTransition_ToProcessingPending()
    {
        var next = FlyerAssetLifecycle.Transition(FlyerAssetStatus.Uploaded, FlyerAssetStatus.ProcessingPending);
        Assert.Equal(FlyerAssetStatus.ProcessingPending, next);
    }

    [Fact]
    public void Lifecycle_Uploaded_CanTransition_ToValidationFailed()
    {
        var next = FlyerAssetLifecycle.Transition(FlyerAssetStatus.Uploaded, FlyerAssetStatus.ValidationFailed);
        Assert.Equal(FlyerAssetStatus.ValidationFailed, next);
    }

    [Fact]
    public void Lifecycle_ReviewPending_CanTransition_ToApproved()
    {
        var next = FlyerAssetLifecycle.Transition(FlyerAssetStatus.ReviewPending, FlyerAssetStatus.Approved);
        Assert.Equal(FlyerAssetStatus.Approved, next);
    }

    [Fact]
    public void Lifecycle_ReviewPending_CanTransition_ToRejected()
    {
        var next = FlyerAssetLifecycle.Transition(FlyerAssetStatus.ReviewPending, FlyerAssetStatus.Rejected);
        Assert.Equal(FlyerAssetStatus.Rejected, next);
    }

    [Fact]
    public void Lifecycle_IllegalTransition_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            FlyerAssetLifecycle.Transition(FlyerAssetStatus.Initialized, FlyerAssetStatus.Approved));
        Assert.Contains("Illegal", ex.Message);
    }

    [Fact]
    public void Lifecycle_Archived_IsTerminal()
    {
        Assert.True(FlyerAssetLifecycle.IsTerminal(FlyerAssetStatus.Archived));
        Assert.False(FlyerAssetLifecycle.IsTerminal(FlyerAssetStatus.ProcessingPending));
    }

    [Fact]
    public void Lifecycle_Approved_IsSafeForDisplay()
    {
        Assert.True(FlyerAssetLifecycle.IsSafeForDisplay(FlyerAssetStatus.Approved));
        Assert.False(FlyerAssetLifecycle.IsSafeForDisplay(FlyerAssetStatus.ReviewPending));
        Assert.False(FlyerAssetLifecycle.IsSafeForDisplay(FlyerAssetStatus.Rejected));
    }

    [Fact]
    public void Lifecycle_CanTransition_ReturnsFalse_WhenIllegal()
    {
        Assert.False(FlyerAssetLifecycle.CanTransition(FlyerAssetStatus.Archived, FlyerAssetStatus.Approved));
        Assert.False(FlyerAssetLifecycle.CanTransition(FlyerAssetStatus.Approved, FlyerAssetStatus.Initialized));
    }

    // ── P26 validator tests ──────────────────────────────────────────────────

    [Fact]
    public async Task Validator_RejectsDisallowedMimeType()
    {
        var store = new InMemoryFlyerAssetStore();
        var validator = CreateValidator(store);
        var stream = new MemoryStream(CreatePngBytes(256, 256));

        var result = await validator.ValidateAsync(stream, "application/pdf", "doc.pdf", "user1");

        Assert.False(result.IsValid);
        Assert.Contains("not allowed", result.FailureReason);
    }

    [Fact]
    public async Task Validator_RejectsEmptyFile()
    {
        var store = new InMemoryFlyerAssetStore();
        var validator = CreateValidator(store);
        var stream = new MemoryStream(Array.Empty<byte>());

        var result = await validator.ValidateAsync(stream, "image/jpeg", "empty.jpg", "user1");

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validator_RejectsOversizedFile()
    {
        var store = new InMemoryFlyerAssetStore();
        var validator = CreateValidator(store);
        var oversized = new byte[FlyerPolicy.MaxFileSizeBytes + 1];
        for (var i = 0; i < oversized.Length; i++)
        {
            oversized[i] = 0x1;
        }
        var stream = new MemoryStream(oversized);

        var result = await validator.ValidateAsync(stream, "image/jpeg", "big.jpg", "user1");

        Assert.False(result.IsValid);
        Assert.Contains("exceeds", result.FailureReason);
    }

    [Fact]
    public async Task Validator_RejectsCorruptedFile_WrongMagicBytes()
    {
        var store = new InMemoryFlyerAssetStore();
        var validator = CreateValidator(store);
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00, 0x00, 0x00, 0x00 };
        var stream = new MemoryStream(bytes);

        var result = await validator.ValidateAsync(stream, "image/jpeg", "fake.jpg", "user1");

        Assert.False(result.IsValid);
        Assert.Contains("corrupted", result.FailureReason);
    }

    [Fact]
    public async Task Validator_RejectsExtensionMismatch()
    {
        var store = new InMemoryFlyerAssetStore();
        var validator = CreateValidator(store);
        var stream = new MemoryStream(CreatePngBytes(256, 256));

        var result = await validator.ValidateAsync(stream, "image/png", "file.jpg", "user1");

        Assert.False(result.IsValid);
        Assert.Contains("Extension", result.FailureReason);
    }

    [Fact]
    public async Task Validator_DetectsDuplicate_SameHashSameSubmitter()
    {
        var store = new InMemoryFlyerAssetStore();
        var validator = CreateValidator(store);

        var png = CreatePngBytes(256, 256);

        // First upload — store a record with the hash of this file
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(png)).ToLowerInvariant();
        var existing = new FlyerAssetRecord
        {
            AssetId = "existing-asset",
            OriginalFilename = "first.png",
            FileSizeBytes = png.Length,
            ContentType = "image/png",
            SubmitterId = "user1",
            UploadedAt = DateTimeOffset.UtcNow,
            Status = FlyerAssetStatus.ProcessingPending,
            ContentHash = hash,
            StorageKey = "existing-key",
            CanonicalContentType = "image/png",
            WidthPx = 256,
            HeightPx = 256,
        };
        await store.SaveAsync(existing);

        // Second upload — same file, same submitter
        var stream = new MemoryStream(png);
        var result = await validator.ValidateAsync(stream, "image/png", "first.png", "user1");

        Assert.True(result.IsDuplicate);
        Assert.Equal("existing-asset", result.DuplicateAssetId);
    }

    [Fact]
    public async Task Validator_AcceptsValidJpeg()
    {
        var store = new InMemoryFlyerAssetStore();
        var validator = CreateValidator(store);
        var png = CreatePngBytes(256, 256);
        var stream = new MemoryStream(png);

        var result = await validator.ValidateAsync(stream, "image/png", "valid.png", "user1");

        Assert.True(result.IsValid);
        Assert.NotNull(result.ContentHash);
        Assert.False(result.IsDuplicate);
        Assert.Equal(256, result.WidthPx);
        Assert.Equal(256, result.HeightPx);
    }

    [Fact]
    public async Task Validator_RejectsTooSmallImage()
    {
        var store = new InMemoryFlyerAssetStore();
        var validator = CreateValidator(store);
        var png = CreatePngBytes(64, 64);
        var stream = new MemoryStream(png);

        var result = await validator.ValidateAsync(stream, "image/png", "tiny.png", "user1");

        Assert.False(result.IsValid);
        Assert.Contains("at least", result.FailureReason);
    }

    // ── P26 in-memory store tests ────────────────────────────────────────────

    [Fact]
    public async Task Store_SaveAndRetrieve_ReturnsRecord()
    {
        var store = new InMemoryFlyerAssetStore();
        var record = MakeRecord("a1", "user1", FlyerAssetStatus.ProcessingPending);
        await store.SaveAsync(record);

        var found = await store.GetAsync("a1");
        Assert.NotNull(found);
        Assert.Equal("a1", found.AssetId);
    }

    [Fact]
    public async Task Store_UpdateStatus_EnforcesLifecycle()
    {
        var store = new InMemoryFlyerAssetStore();
        await store.SaveAsync(MakeRecord("a2", "user1", FlyerAssetStatus.ReviewPending));

        var updated = await store.UpdateStatusAsync("a2", FlyerAssetStatus.Approved);
        Assert.Equal(FlyerAssetStatus.Approved, updated.Status);
    }

    [Fact]
    public async Task Store_UpdateStatus_ThrowsOnIllegalTransition()
    {
        var store = new InMemoryFlyerAssetStore();
        await store.SaveAsync(MakeRecord("a3", "user1", FlyerAssetStatus.Archived));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.UpdateStatusAsync("a3", FlyerAssetStatus.Approved));
    }

    [Fact]
    public async Task Store_ListBySubmitter_ExcludesOtherUsers()
    {
        var store = new InMemoryFlyerAssetStore();
        await store.SaveAsync(MakeRecord("a4", "user1", FlyerAssetStatus.ProcessingPending));
        await store.SaveAsync(MakeRecord("a5", "user2", FlyerAssetStatus.ProcessingPending));

        var user1Assets = await store.ListBySubmitterAsync("user1");
        Assert.Single(user1Assets);
        Assert.Equal("a4", user1Assets[0].AssetId);
    }

    [Fact]
    public async Task Store_FindByHash_MatchesCorrectly()
    {
        var store = new InMemoryFlyerAssetStore();
        var record = MakeRecord("a6", "user1", FlyerAssetStatus.ProcessingPending, contentHash: "abc123");
        await store.SaveAsync(record);

        var found = await store.FindByHashAsync("abc123", "user1");
        Assert.NotNull(found);

        var notFound = await store.FindByHashAsync("abc123", "user2");
        Assert.Null(notFound);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static FlyerAssetRecord MakeRecord(
        string assetId,
        string submitterId,
        FlyerAssetStatus status,
        string contentHash = "hash")
    =>  new()
    {
        AssetId = assetId,
        OriginalFilename = "test.jpg",
        FileSizeBytes = 1000,
        ContentType = "image/jpeg",
        SubmitterId = submitterId,
        UploadedAt = DateTimeOffset.UtcNow,
        Status = status,
        ContentHash = contentHash,
        StorageKey = $"{assetId}_test.jpg",
        CanonicalContentType = "image/jpeg",
        WidthPx = 512,
        HeightPx = 512,
    };

    private static FlyerAssetValidator CreateValidator(IFlyerAssetStore store) =>
        new(new ImageSharpFileSignatureInspector(), new Sha256ChecksumService(), new FlyerDuplicateDetector(store));

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        return stream.ToArray();
    }
}
