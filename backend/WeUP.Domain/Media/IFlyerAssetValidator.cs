namespace WeUP.Domain.Media;

/// <summary>
/// P26: Server-side validator for incoming flyer assets.
/// Validates MIME type, file signature, size, and duplicate detection.
/// </summary>
public interface IFlyerAssetValidator
{
    /// <summary>
    /// Validates a flyer file stream before it is stored or assigned a lifecycle status.
    /// Must be called before any UploadFlyerAsync writes to storage.
    /// </summary>
    /// <param name="stream">Raw file bytes. Stream position is reset to 0 before reading.</param>
    /// <param name="declaredContentType">MIME type as declared by the uploader.</param>
    /// <param name="filename">Original filename (used for extension mismatch check).</param>
    /// <param name="submitterId">Uploader identity (used for duplicate scoping).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<FlyerValidationResult> ValidateAsync(
        Stream stream,
        string declaredContentType,
        string filename,
        string submitterId,
        string? sourceReference = null,
        bool allowSameSourceReupload = false,
        CancellationToken ct = default);
}
