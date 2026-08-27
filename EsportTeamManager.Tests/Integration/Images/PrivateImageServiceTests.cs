using EsportTeamManager.Application.Images;
using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Infrastructure.Images;
using EsportTeamManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using System.Text;
using Image = SixLabors.ImageSharp.Image;

namespace EsportTeamManager.Tests.Integration.Images;

public sealed class PrivateImageServiceTests : IAsyncLifetime
{
    private readonly string _storageRoot;
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private PrivateImageService _service = null!;

    public PrivateImageServiceTests()
    {
        _storageRoot = Path.Combine(Path.GetTempPath(), "EsportTeamManager.Tests", Guid.NewGuid().ToString("N"));
    }

    public async Task InitializeAsync()
    {
        SQLitePCL.Batteries_V2.Init();

        Directory.CreateDirectory(_storageRoot);

        _connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        await _connection.OpenAsync();

        DbContextOptions<ApplicationDbContext> contextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(contextOptions);
        await _context.Database.EnsureCreatedAsync();

        PrivateImageStorageOptions storageOptions = new()
        {
            RootPath = _storageRoot,
            MaximumUploadSizeBytes = 1_048_576,
            MinimumDimensionPixels = 64,
            MaximumDimensionPixels = 2048,
            MaximumPixelCount = 4_000_000,
            TeamLogoMaximumEdgePixels = 512,
            StrategyImageMaximumEdgePixels = 1024,
            ThumbnailMaximumEdgePixels = 128,
            WebpQuality = 80
        };

        TestHostEnvironment environment = new(_storageRoot);

        _service = new PrivateImageService(
            _context,
            environment,
            Options.Create(storageOptions),
            TimeProvider.System,
            NullLogger<PrivateImageService>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();

        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, true);
        }
    }

    [Fact]
    public async Task StoreTeamLogoAsync_WithValidPng_CreatesOptimizedPrivateFiles()
    {
        Team team = await AddTeamAsync();
        byte[] sourceBytes = await CreatePngAsync(900, 600);

        await using MemoryStream content = new(sourceBytes);
        StorePrivateImageRequest request = new(team.TeamId, "logo-phoenix.png", content);

        StorePrivateImageResult result = await _service.StoreTeamLogoAsync(request);

        Assert.True(result.Succeeded);
        ImageFile imageFile = Assert.IsType<ImageFile>(result.Image);
        Assert.Equal(team.TeamId, imageFile.TeamLogoForTeamId);
        Assert.Null(imageFile.StrategyImageForStrategyId);
        Assert.Equal("image/webp", imageFile.MediaType);
        Assert.EndsWith(".webp", imageFile.InternalFileName);
        Assert.InRange(imageFile.WidthPixels, 1, 512);
        Assert.InRange(imageFile.HeightPixels, 1, 512);

        string optimizedPath = GetPhysicalPath(imageFile.OptimizedStorageKey);
        string thumbnailPath = GetPhysicalPath(imageFile.ThumbnailStorageKey);

        Assert.True(File.Exists(optimizedPath));
        Assert.True(File.Exists(thumbnailPath));
        Assert.Equal("image/webp", Image.DetectFormat(optimizedPath).DefaultMimeType);
        Assert.Equal("image/webp", Image.DetectFormat(thumbnailPath).DefaultMimeType);

        using Image thumbnail = await Image.LoadAsync(thumbnailPath);

        Assert.InRange(thumbnail.Width, 1, 128);
        Assert.InRange(thumbnail.Height, 1, 128);
        Assert.Equal(1, await _context.ImageFiles.CountAsync());
        AssertTemporaryDirectoryIsEmpty();
    }

    [Fact]
    public async Task StoreTeamLogoAsync_WithFakePng_RejectsFileAndCleansTemporaryFiles()
    {
        Team team = await AddTeamAsync();
        byte[] sourceBytes = Encoding.UTF8.GetBytes("This is not a real PNG image.");

        await using MemoryStream content = new(sourceBytes);
        StorePrivateImageRequest request = new(team.TeamId, "fake.png", content);

        StorePrivateImageResult result = await _service.StoreTeamLogoAsync(request);

        Assert.False(result.Succeeded);
        Assert.Null(result.Image);
        Assert.NotEmpty(result.Errors);
        Assert.Equal(0, await _context.ImageFiles.CountAsync());
        AssertTemporaryDirectoryIsEmpty();
    }

    [Fact]
    public async Task StoreTeamLogoAsync_WithCorruptedPng_RejectsFileAndCleansTemporaryFiles()
    {
        Team team = await AddTeamAsync();
        byte[] sourceBytes = await CreatePngAsync(256, 256);

        Array.Resize(ref sourceBytes, 24);

        await using MemoryStream content = new(sourceBytes);
        StorePrivateImageRequest request = new(team.TeamId, "corrupted.png", content);

        StorePrivateImageResult result = await _service.StoreTeamLogoAsync(request);

        Assert.False(result.Succeeded);
        Assert.Null(result.Image);
        Assert.NotEmpty(result.Errors);
        Assert.Equal(0, await _context.ImageFiles.CountAsync());
        AssertTemporaryDirectoryIsEmpty();
    }

    [Fact]
    public async Task StoreTeamLogoAsync_WithGif_RejectsUnsupportedActualFormat()
    {
        Team team = await AddTeamAsync();
        byte[] sourceBytes = await CreateGifAsync(256, 256);

        await using MemoryStream content = new(sourceBytes);
        StorePrivateImageRequest request = new(team.TeamId, "image.png", content);

        StorePrivateImageResult result = await _service.StoreTeamLogoAsync(request);

        Assert.False(result.Succeeded);
        Assert.Null(result.Image);
        Assert.NotEmpty(result.Errors);
        Assert.Equal(0, await _context.ImageFiles.CountAsync());
        AssertTemporaryDirectoryIsEmpty();
    }

    [Fact]
    public async Task StoreTeamLogoAsync_WithDimensionsBelowMinimum_RejectsImage()
    {
        Team team = await AddTeamAsync();
        byte[] sourceBytes = await CreatePngAsync(63, 128);

        await using MemoryStream content = new(sourceBytes);
        StorePrivateImageRequest request = new(team.TeamId, "too-small.png", content);

        StorePrivateImageResult result = await _service.StoreTeamLogoAsync(request);

        Assert.False(result.Succeeded);
        Assert.Null(result.Image);
        Assert.NotEmpty(result.Errors);
        Assert.Equal(0, await _context.ImageFiles.CountAsync());
        AssertTemporaryDirectoryIsEmpty();
    }

    [Fact]
    public async Task StoreTeamLogoAsync_WithDimensionsAboveMaximum_RejectsImage()
    {
        Team team = await AddTeamAsync();
        byte[] sourceBytes = await CreatePngAsync(2049, 64);

        await using MemoryStream content = new(sourceBytes);
        StorePrivateImageRequest request = new(team.TeamId, "too-wide.png", content);

        StorePrivateImageResult result = await _service.StoreTeamLogoAsync(request);

        Assert.False(result.Succeeded);
        Assert.Null(result.Image);
        Assert.NotEmpty(result.Errors);
        Assert.Equal(0, await _context.ImageFiles.CountAsync());
        AssertTemporaryDirectoryIsEmpty();
    }

    [Fact]
    public async Task StoreTeamLogoAsync_WithFileAboveMaximumSize_RejectsFile()
    {
        Team team = await AddTeamAsync();
        byte[] sourceBytes = new byte[1_048_577];

        await using MemoryStream content = new(sourceBytes);
        StorePrivateImageRequest request = new(team.TeamId, "too-large.png", content);

        StorePrivateImageResult result = await _service.StoreTeamLogoAsync(request);

        Assert.False(result.Succeeded);
        Assert.Null(result.Image);
        Assert.NotEmpty(result.Errors);
        Assert.Equal(0, await _context.ImageFiles.CountAsync());
        AssertTemporaryDirectoryIsEmpty();
    }

    [Fact]
    public async Task StoreTeamLogoAsync_WithExifMetadata_RemovesMetadataFromStoredFiles()
    {
        Team team = await AddTeamAsync();
        byte[] sourceBytes = await CreateJpegWithExifAsync(900, 600);

        await using MemoryStream content = new(sourceBytes);
        StorePrivateImageRequest request = new(team.TeamId, "photograph.jpg", content);

        StorePrivateImageResult result = await _service.StoreTeamLogoAsync(request);

        Assert.True(result.Succeeded);
        ImageFile imageFile = Assert.IsType<ImageFile>(result.Image);

        using Image optimizedImage = await Image.LoadAsync(GetPhysicalPath(imageFile.OptimizedStorageKey));
        using Image thumbnailImage = await Image.LoadAsync(GetPhysicalPath(imageFile.ThumbnailStorageKey));

        Assert.Null(optimizedImage.Metadata.ExifProfile);
        Assert.Null(optimizedImage.Metadata.IptcProfile);
        Assert.Null(optimizedImage.Metadata.XmpProfile);
        Assert.Null(optimizedImage.Metadata.IccProfile);
        Assert.Null(thumbnailImage.Metadata.ExifProfile);
        Assert.Null(thumbnailImage.Metadata.IptcProfile);
        Assert.Null(thumbnailImage.Metadata.XmpProfile);
        Assert.Null(thumbnailImage.Metadata.IccProfile);
        AssertTemporaryDirectoryIsEmpty();
    }

    [Fact]
    public async Task StoreStrategyImageAsync_WithValidImage_CreatesStrategyImage()
    {
        Team team = await AddTeamAsync();
        Strategy strategy = new(team.TeamId, Guid.NewGuid(), 1, "Exécution site A", StrategySide.Attack, "Description de la stratégie.", null, DateTimeOffset.UtcNow);

        _context.Strategies.Add(strategy);
        await _context.SaveChangesAsync();

        byte[] sourceBytes = await CreatePngAsync(1600, 900);

        await using MemoryStream content = new(sourceBytes);
        StorePrivateImageRequest request = new(strategy.StrategyId, "strategy.png", content);

        StorePrivateImageResult result = await _service.StoreStrategyImageAsync(request);

        Assert.True(result.Succeeded);
        ImageFile imageFile = Assert.IsType<ImageFile>(result.Image);
        Assert.Null(imageFile.TeamLogoForTeamId);
        Assert.Equal(strategy.StrategyId, imageFile.StrategyImageForStrategyId);
        Assert.Equal("image/webp", imageFile.MediaType);
        Assert.InRange(imageFile.WidthPixels, 1, 1024);
        Assert.InRange(imageFile.HeightPixels, 1, 1024);
        Assert.True(File.Exists(GetPhysicalPath(imageFile.OptimizedStorageKey)));
        Assert.True(File.Exists(GetPhysicalPath(imageFile.ThumbnailStorageKey)));
        AssertTemporaryDirectoryIsEmpty();
    }

    private async Task<Team> AddTeamAsync()
    {
        Team team = new(Guid.NewGuid(), Guid.NewGuid(), "Phoenix Academy", DateTimeOffset.UtcNow, "PHX");

        _context.Teams.Add(team);
        await _context.SaveChangesAsync();

        return team;
    }

    private string GetPhysicalPath(string storageKey)
    {
        return Path.Combine(_storageRoot, storageKey.Replace('/', Path.DirectorySeparatorChar));
    }

    private void AssertTemporaryDirectoryIsEmpty()
    {
        string temporaryDirectory = Path.Combine(_storageRoot, ".temporary");

        if (!Directory.Exists(temporaryDirectory))
        {
            return;
        }

        Assert.Empty(Directory.EnumerateFiles(temporaryDirectory, "*", SearchOption.AllDirectories));
    }

    private static async Task<byte[]> CreatePngAsync(int width, int height)
    {
        using Image<Rgba32> image = new(width, height, new Rgba32(24, 96, 160, 255));
        await using MemoryStream stream = new();

        await image.SaveAsPngAsync(stream);

        return stream.ToArray();
    }

    private static async Task<byte[]> CreateGifAsync(int width, int height)
    {
        using Image<Rgba32> image = new(width, height, new Rgba32(24, 96, 160, 255));
        await using MemoryStream stream = new();

        await image.SaveAsGifAsync(stream);

        return stream.ToArray();
    }

    private static async Task<byte[]> CreateJpegWithExifAsync(int width, int height)
    {
        using Image<Rgba32> image = new(width, height, new Rgba32(24, 96, 160, 255));
        await using MemoryStream stream = new();

        image.Metadata.ExifProfile = new ExifProfile();
        image.Metadata.ExifProfile.SetValue(ExifTag.Artist, "Confidential author");
        image.Metadata.ExifProfile.SetValue(ExifTag.ImageDescription, "Confidential description");

        JpegEncoder encoder = new()
        {
            Quality = 90,
            SkipMetadata = false
        };

        await image.SaveAsJpegAsync(stream, encoder);

        return stream.ToArray();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = typeof(PrivateImageServiceTests).Assembly.FullName ?? nameof(PrivateImageServiceTests);

        public string ContentRootPath { get; set; }

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public TestHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
        }
    }
}