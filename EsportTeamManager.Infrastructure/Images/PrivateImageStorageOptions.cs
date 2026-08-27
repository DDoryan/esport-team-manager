using System.ComponentModel.DataAnnotations;

namespace EsportTeamManager.Infrastructure.Images;

public sealed class PrivateImageStorageOptions
{
    public const string SectionName = "PrivateImageStorage";

    [Required]
    public string RootPath { get; set; } = "App_Data/private-images";

    [Range(1, 52_428_800)]
    public long MaximumUploadSizeBytes { get; set; } = 10_485_760;

    [Range(1, 4_096)]
    public int MinimumDimensionPixels { get; set; } = 64;

    [Range(1, 32_768)]
    public int MaximumDimensionPixels { get; set; } = 8_192;

    [Range(1, 100_000_000)]
    public long MaximumPixelCount { get; set; } = 40_000_000;

    [Range(1, 8_192)]
    public int TeamLogoMaximumEdgePixels { get; set; } = 1_024;

    [Range(1, 8_192)]
    public int StrategyImageMaximumEdgePixels { get; set; } = 2_560;

    [Range(1, 2_048)]
    public int ThumbnailMaximumEdgePixels { get; set; } = 480;

    [Range(1, 100)]
    public int WebpQuality { get; set; } = 82;
}