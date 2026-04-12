namespace WeUP.Infrastructure.Media;

public sealed class VideoIntakeOptions
{
    public const string SectionName = "VideoIntake";

    /// <summary>Maximum acceptable upload size. Default: 500 MB.</summary>
    public long MaxFileSizeBytes { get; set; } = 500L * 1024 * 1024;

    /// <summary>MIME types accepted for video flyer uploads.</summary>
    public string[] AllowedContentTypes { get; set; } =
    [
        "video/mp4",
        "video/webm",
        "video/quicktime",
        "video/x-msvideo",
        "video/mpeg",
        "video/ogg",
    ];

    /// <summary>Root directory for local video file storage.</summary>
    public string LocalStorageRoot { get; set; } = "uploads/video";
}
