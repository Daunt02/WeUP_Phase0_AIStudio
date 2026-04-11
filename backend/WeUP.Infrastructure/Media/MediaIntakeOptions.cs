namespace WeUP.Infrastructure.Media;

public sealed class MediaIntakeOptions
{
    public const string SectionName = "MediaIntake";

    public long MaxFileSizeBytes { get; set; } = 12 * 1024 * 1024;
    public string[] AllowedContentTypes { get; set; } = ["image/jpeg", "image/png", "image/webp"];
    public string LocalStorageRoot { get; set; } = "uploads/media";
}
