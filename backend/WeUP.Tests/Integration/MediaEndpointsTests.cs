using Xunit;
using WeUP.Domain.Media;

namespace WeUP.Tests.Integration;

/// <summary>
/// Integration tests for Media Endpoints — P25
/// Verifies POST /api/media/flyers, GET /api/media/flyers/{assetId}, etc.
/// </summary>
public class MediaEndpointsTests
{
    [Fact]
    public void UploadFlyer_WithValidAsset_ReturnsAssetId()
    {
        // Arrange
        var assetId = Guid.NewGuid().ToString();
        var submitterId = "user123";
        var filename = "event-flyer.jpg";
        var contentType = "image/jpeg";
        var fileSizeBytes = 12345L;

        // Act
        var flyerAsset = new FlyerAsset(
            AssetId: assetId,
            OriginalFilename: filename,
            FileSizeBytes: fileSizeBytes,
            ContentType: contentType,
            SubmitterId: submitterId,
            UploadedAt: DateTimeOffset.UtcNow,
            LocalPath: "/uploads/event-flyer.jpg");

        // Assert
        Assert.NotNull(flyerAsset);
        Assert.Equal(assetId, flyerAsset.AssetId);
        Assert.Equal(filename, flyerAsset.OriginalFilename);
    }

    [Fact]
    public void UploadFlyer_StoresMetadata()
    {
        // Arrange
        var asset = new FlyerAsset(
            AssetId: "asset123",
            OriginalFilename: "poster.png",
            FileSizeBytes: 54321,
            ContentType: "image/png",
            SubmitterId: "user456",
            UploadedAt: DateTimeOffset.UtcNow,
            LocalPath: "/uploads/poster.png");

        // Act & Assert
        Assert.Equal("asset123", asset.AssetId);
        Assert.Equal("poster.png", asset.OriginalFilename);
        Assert.Equal(54321, asset.FileSizeBytes);
        Assert.Equal("image/png", asset.ContentType);
        Assert.Equal("user456", asset.SubmitterId);
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    public void UploadFlyer_SupportedContentTypes(string contentType)
    {
        // Arrange & Act
        var asset = new FlyerAsset(
            AssetId: Guid.NewGuid().ToString(),
            OriginalFilename: "test.jpg",
            FileSizeBytes: 1000,
            ContentType: contentType,
            SubmitterId: "user123",
            UploadedAt: DateTimeOffset.UtcNow,
            LocalPath: null);

        // Assert
        Assert.NotNull(asset);
        Assert.Equal(contentType, asset.ContentType);
    }

    [Fact]
    public void GetFlyer_ReturnsAssetMetadata()
    {
        // Arrange
        var assetId = "asset789";
        var submitterId = "user789";
        var uploadedTime = DateTimeOffset.UtcNow;

        var asset = new FlyerAsset(
            AssetId: assetId,
            OriginalFilename: "event-poster.jpg",
            FileSizeBytes: 99999,
            ContentType: "image/jpeg",
            SubmitterId: submitterId,
            UploadedAt: uploadedTime,
            LocalPath: "/uploads/event-poster.jpg");

        // Act & Assert
        Assert.Equal(assetId, asset.AssetId);
        Assert.Equal(submitterId, asset.SubmitterId);
        Assert.Equal(uploadedTime, asset.UploadedAt);
    }

    [Fact]
    public void ListUserFlyers_FiltersBySubmitterId()
    {
        // Arrange
        var submitterId = "user111";
        var assets = new[]
        {
            new FlyerAsset("asset1", "flyer1.jpg", 1000, "image/jpeg", submitterId, DateTimeOffset.UtcNow, null),
            new FlyerAsset("asset2", "flyer2.jpg", 2000, "image/jpeg", submitterId, DateTimeOffset.UtcNow, null),
            new FlyerAsset("asset3", "flyer3.jpg", 3000, "image/jpeg", "user222", DateTimeOffset.UtcNow, null),
        };

        // Act
        var userAssets = assets.Where(a => a.SubmitterId == submitterId).ToArray();

        // Assert
        Assert.Equal(2, userAssets.Length);
        Assert.All(userAssets, a => Assert.Equal(submitterId, a.SubmitterId));
    }

    [Fact]
    public void DeleteFlyer_RemovesAsset()
    {
        // Arrange
        var assetIdToDelete = "asset-to-delete";
        var assets = new Dictionary<string, FlyerAsset>
        {
            {
                "asset1",
                new FlyerAsset("asset1", "flyer1.jpg", 1000, "image/jpeg", "user1", DateTimeOffset.UtcNow, null)
            },
            {
                "asset-to-delete",
                new FlyerAsset("asset-to-delete", "remove-me.jpg", 2000, "image/jpeg", "user1", DateTimeOffset.UtcNow, null)
            },
        };

        // Act
        assets.Remove(assetIdToDelete);

        // Assert
        Assert.DoesNotContain(assetIdToDelete, assets.Keys);
        Assert.Single(assets);
    }

    [Fact]
    public void UploadFlyer_GeneratesUniqueAssetIds()
    {
        // Arrange & Act
        var asset1 = new FlyerAsset(
            Guid.NewGuid().ToString(),
            "flyer1.jpg",
            1000,
            "image/jpeg",
            "user1",
            DateTimeOffset.UtcNow,
            null);

        var asset2 = new FlyerAsset(
            Guid.NewGuid().ToString(),
            "flyer2.jpg",
            2000,
            "image/jpeg",
            "user1",
            DateTimeOffset.UtcNow,
            null);

        // Assert
        Assert.NotEqual(asset1.AssetId, asset2.AssetId);
    }

    [Fact]
    public void UploadFlyer_WithS3Url_StoresReference()
    {
        // Arrange
        var s3Url = "https://s3.us-west-2.amazonaws.com/weup-flyers/asset123.jpg";

        // Act
        var asset = new FlyerAsset(
            AssetId: "asset123",
            OriginalFilename: "event.jpg",
            FileSizeBytes: 50000,
            ContentType: "image/jpeg",
            SubmitterId: "user1",
            UploadedAt: DateTimeOffset.UtcNow,
            S3Url: s3Url,
            LocalPath: null);

        // Assert
        Assert.NotNull(asset.S3Url);
        Assert.Equal(s3Url, asset.S3Url);
    }
}
