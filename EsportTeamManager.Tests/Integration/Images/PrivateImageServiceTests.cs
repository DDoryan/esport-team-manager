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
using EsportTeamManager.Application.Teams;
using EsportTeamManager.Infrastructure.Teams;
using Microsoft.AspNetCore.Identity;

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
    public async Task ReplaceTeamLogoAsync_WithValidImage_ReplacesFilesAndKeepsSingleDatabaseRow()
    {
        Team team = await AddTeamAsync();
        byte[] initialBytes = await CreatePngAsync(900, 600);

        await using MemoryStream initialContent = new(initialBytes);
        StorePrivateImageResult initialResult = await _service.StoreTeamLogoAsync(new StorePrivateImageRequest(team.TeamId, "initial-logo.png", initialContent));

        Assert.True(initialResult.Succeeded);

        ImageFile initialImage = Assert.IsType<ImageFile>(initialResult.Image);
        Guid initialImageFileId = initialImage.ImageFileId;
        string initialOptimizedStorageKey = initialImage.OptimizedStorageKey;
        string initialThumbnailStorageKey = initialImage.ThumbnailStorageKey;
        string initialOptimizedPath = GetPhysicalPath(initialOptimizedStorageKey);
        string initialThumbnailPath = GetPhysicalPath(initialThumbnailStorageKey);
        byte[] replacementBytes = await CreatePngAsync(400, 900);

        Assert.True(File.Exists(initialOptimizedPath));
        Assert.True(File.Exists(initialThumbnailPath));

        await using MemoryStream replacementContent = new(replacementBytes);
        ReplaceTeamLogoRequest replacementRequest = new(team.OwnerUserId, team.TeamId, "replacement-logo.png", replacementContent);

        StorePrivateImageResult replacementResult = await _service.ReplaceTeamLogoAsync(replacementRequest);

        Assert.True(replacementResult.Succeeded);

        ImageFile replacementImage = Assert.IsType<ImageFile>(replacementResult.Image);
        ImageFile persistedImage = await _context.ImageFiles.AsNoTracking().SingleAsync(image => image.TeamLogoForTeamId == team.TeamId);

        Assert.Equal(initialImageFileId, replacementImage.ImageFileId);
        Assert.Equal(initialImageFileId, persistedImage.ImageFileId);
        Assert.Equal("replacement-logo.png", persistedImage.OriginalFileName);
        Assert.NotEqual(initialOptimizedStorageKey, persistedImage.OptimizedStorageKey);
        Assert.NotEqual(initialThumbnailStorageKey, persistedImage.ThumbnailStorageKey);
        Assert.False(File.Exists(initialOptimizedPath));
        Assert.False(File.Exists(initialThumbnailPath));
        Assert.True(File.Exists(GetPhysicalPath(persistedImage.OptimizedStorageKey)));
        Assert.True(File.Exists(GetPhysicalPath(persistedImage.ThumbnailStorageKey)));
        Assert.InRange(persistedImage.WidthPixels, 1, 512);
        Assert.InRange(persistedImage.HeightPixels, 1, 512);
        Assert.Equal(1, await _context.ImageFiles.CountAsync());
        AssertTemporaryDirectoryIsEmpty();
    }

    [Fact]
    public async Task ReplaceTeamLogoAsync_WhenActorIsNotOwner_RejectsReplacementAndKeepsCurrentFiles()
    {
        Team team = await AddTeamAsync();
        byte[] initialBytes = await CreatePngAsync(900, 600);

        await using MemoryStream initialContent = new(initialBytes);
        StorePrivateImageResult initialResult = await _service.StoreTeamLogoAsync(new StorePrivateImageRequest(team.TeamId, "initial-logo.png", initialContent));

        Assert.True(initialResult.Succeeded);

        ImageFile initialImage = Assert.IsType<ImageFile>(initialResult.Image);
        string initialOptimizedStorageKey = initialImage.OptimizedStorageKey;
        string initialThumbnailStorageKey = initialImage.ThumbnailStorageKey;
        byte[] replacementBytes = await CreatePngAsync(512, 512);

        await using MemoryStream replacementContent = new(replacementBytes);
        ReplaceTeamLogoRequest replacementRequest = new(Guid.NewGuid(), team.TeamId, "unauthorized-logo.png", replacementContent);

        StorePrivateImageResult replacementResult = await _service.ReplaceTeamLogoAsync(replacementRequest);

        Assert.False(replacementResult.Succeeded);
        Assert.Null(replacementResult.Image);
        Assert.NotEmpty(replacementResult.Errors);

        ImageFile persistedImage = await _context.ImageFiles.AsNoTracking().SingleAsync(image => image.TeamLogoForTeamId == team.TeamId);

        Assert.Equal(initialImage.ImageFileId, persistedImage.ImageFileId);
        Assert.Equal("initial-logo.png", persistedImage.OriginalFileName);
        Assert.Equal(initialOptimizedStorageKey, persistedImage.OptimizedStorageKey);
        Assert.Equal(initialThumbnailStorageKey, persistedImage.ThumbnailStorageKey);
        Assert.True(File.Exists(GetPhysicalPath(initialOptimizedStorageKey)));
        Assert.True(File.Exists(GetPhysicalPath(initialThumbnailStorageKey)));
        Assert.Equal(1, await _context.ImageFiles.CountAsync());
        AssertTemporaryDirectoryIsEmpty();
    }

    [Fact]
    public async Task ReplaceTeamLogoAsync_WithInvalidImage_KeepsCurrentDatabaseRowAndFiles()
    {
        Team team = await AddTeamAsync();
        byte[] initialBytes = await CreatePngAsync(900, 600);

        await using MemoryStream initialContent = new(initialBytes);
        StorePrivateImageResult initialResult = await _service.StoreTeamLogoAsync(new StorePrivateImageRequest(team.TeamId, "initial-logo.png", initialContent));

        Assert.True(initialResult.Succeeded);

        ImageFile initialImage = Assert.IsType<ImageFile>(initialResult.Image);
        string initialOptimizedStorageKey = initialImage.OptimizedStorageKey;
        string initialThumbnailStorageKey = initialImage.ThumbnailStorageKey;
        byte[] invalidBytes = Encoding.UTF8.GetBytes("This is not a valid image.");

        await using MemoryStream invalidContent = new(invalidBytes);
        ReplaceTeamLogoRequest replacementRequest = new(team.OwnerUserId, team.TeamId, "invalid.png", invalidContent);

        StorePrivateImageResult replacementResult = await _service.ReplaceTeamLogoAsync(replacementRequest);

        Assert.False(replacementResult.Succeeded);
        Assert.Null(replacementResult.Image);
        Assert.NotEmpty(replacementResult.Errors);

        ImageFile persistedImage = await _context.ImageFiles.AsNoTracking().SingleAsync(image => image.TeamLogoForTeamId == team.TeamId);

        Assert.Equal(initialImage.ImageFileId, persistedImage.ImageFileId);
        Assert.Equal("initial-logo.png", persistedImage.OriginalFileName);
        Assert.Equal(initialOptimizedStorageKey, persistedImage.OptimizedStorageKey);
        Assert.Equal(initialThumbnailStorageKey, persistedImage.ThumbnailStorageKey);
        Assert.True(File.Exists(GetPhysicalPath(initialOptimizedStorageKey)));
        Assert.True(File.Exists(GetPhysicalPath(initialThumbnailStorageKey)));
        Assert.Equal(2, Directory.EnumerateFiles(_storageRoot, "*", SearchOption.AllDirectories).Count());
        AssertTemporaryDirectoryIsEmpty();
    }

    [Fact]
    public async Task GetTeamLogoThumbnailAsync_AllowsOnlyActiveTeamMember()
    {
        Team team = await AddTeamAsync();
        int playerRoleId = await _context.TeamRoles
            .Where(role => role.Code == "Player")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        TeamMembership ownerMembership = new(Guid.NewGuid(), team.TeamId, team.OwnerUserId, playerRoleId, DateTimeOffset.UtcNow);

        _context.TeamMemberships.Add(ownerMembership);

        await _context.SaveChangesAsync();

        byte[] sourceBytes = await CreatePngAsync(900, 600);

        await using MemoryStream sourceContent = new(sourceBytes);
        StorePrivateImageResult storageResult = await _service.StoreTeamLogoAsync(new StorePrivateImageRequest(team.TeamId, "logo.png", sourceContent));

        Assert.True(storageResult.Succeeded);

        PrivateImageContent? authorizedResult = await _service.GetTeamLogoThumbnailAsync(team.OwnerUserId, team.TeamId);
        PrivateImageContent? unauthorizedResult = await _service.GetTeamLogoThumbnailAsync(Guid.NewGuid(), team.TeamId);
        PrivateImageContent authorizedImage = Assert.IsType<PrivateImageContent>(authorizedResult);

        await using Stream authorizedContent = authorizedImage.Content;

        Assert.Equal("image/webp", authorizedImage.MediaType);
        Assert.True(authorizedContent.CanRead);
        Assert.True(authorizedContent.Length > 0);
        Assert.Null(unauthorizedResult);
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

    [Fact]
    public async Task GetStrategyImageAsync_AllowsOnlyActiveTeamMember()
    {
        Team team = await AddTeamAsync();
        int playerRoleId = await _context.TeamRoles
            .Where(role => role.Code == "Player")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        TeamMembership ownerMembership = new(Guid.NewGuid(), team.TeamId, team.OwnerUserId, playerRoleId, DateTimeOffset.UtcNow);

        _context.TeamMemberships.Add(ownerMembership);

        Strategy strategy = new(team.TeamId, ownerMembership.TeamMembershipId, 1, "Exécution site A", StrategySide.Attack, "Description de la stratégie.", null, DateTimeOffset.UtcNow);

        _context.Strategies.Add(strategy);
        await _context.SaveChangesAsync();

        byte[] sourceBytes = await CreatePngAsync(1600, 900);

        await using MemoryStream sourceContent = new(sourceBytes);
        StorePrivateImageResult storageResult = await _service.StoreStrategyImageAsync(new StorePrivateImageRequest(strategy.StrategyId, "strategy.png", sourceContent));

        Assert.True(storageResult.Succeeded);

        PrivateImageContent? authorizedResult = await _service.GetStrategyImageAsync(team.OwnerUserId, team.TeamId, strategy.StrategyId);
        PrivateImageContent? unauthorizedResult = await _service.GetStrategyImageAsync(Guid.NewGuid(), team.TeamId, strategy.StrategyId);
        PrivateImageContent authorizedImage = Assert.IsType<PrivateImageContent>(authorizedResult);

        await using Stream authorizedContent = authorizedImage.Content;

        Assert.Equal("image/webp", authorizedImage.MediaType);
        Assert.True(authorizedContent.CanRead);
        Assert.True(authorizedContent.Length > 0);
        Assert.Null(unauthorizedResult);
    }

    [Fact]
    public async Task UpdateInformationAsync_WithValidLogo_PersistsTeamLogoAndTraceTogether()
    {
        Team team = await AddTeamAsync();
        UserTeamService teamService = new(_context, new UpperInvariantLookupNormalizer(), _service, TimeProvider.System, NullLogger<UserTeamService>.Instance);
        byte[] sourceBytes = await CreatePngAsync(900, 600);

        await using MemoryStream content = new(sourceBytes);
        UpdateTeamInformationRequest request = new(team.OwnerUserId, team.TeamId, "Phoenix Elite", "PHE", "Équipe principale.", "Europe/London", "phoenix-logo.png", content);

        UpdateTeamInformationResult result = await teamService.UpdateInformationAsync(request);

        Assert.True(result.Succeeded);
        Assert.False(result.AccessDenied);
        Assert.Empty(result.Errors);

        Team persistedTeam = await _context.Teams.AsNoTracking().SingleAsync(item => item.TeamId == team.TeamId);
        ImageFile persistedImage = await _context.ImageFiles.AsNoTracking().SingleAsync(image => image.TeamLogoForTeamId == team.TeamId);
        ActionTrace trace = await _context.ActionTraces.AsNoTracking().SingleAsync(item => item.ActionCode == "TEAM_INFORMATION_UPDATED");

        Assert.Equal("Phoenix Elite", persistedTeam.Name);
        Assert.Equal("PHE", persistedTeam.Tag);
        Assert.Equal("Équipe principale.", persistedTeam.Description);
        Assert.Equal("Europe/London", persistedTeam.TimeZoneId);
        Assert.Equal("phoenix-logo.png", persistedImage.OriginalFileName);
        Assert.True(File.Exists(GetPhysicalPath(persistedImage.OptimizedStorageKey)));
        Assert.True(File.Exists(GetPhysicalPath(persistedImage.ThumbnailStorageKey)));
        Assert.Equal(team.OwnerUserId, trace.ActorUserId);
        Assert.Equal(team.TeamId, trace.TeamId);
        Assert.Equal(nameof(Team), trace.ObjectType);
        Assert.Equal(team.TeamId.ToString(), trace.ObjectIdentifier);
        Assert.Equal(TraceOutcome.Succeeded, trace.Outcome);
        AssertTemporaryDirectoryIsEmpty();
    }

    [Fact]
    public async Task UpdateInformationAsync_WithInvalidLogo_DoesNotPersistTeamChangesOrTrace()
    {
        Team team = await AddTeamAsync();
        UserTeamService teamService = new(_context, new UpperInvariantLookupNormalizer(), _service, TimeProvider.System, NullLogger<UserTeamService>.Instance);
        byte[] invalidBytes = Encoding.UTF8.GetBytes("This is not a valid image.");

        await using MemoryStream content = new(invalidBytes);
        UpdateTeamInformationRequest request = new(team.OwnerUserId, team.TeamId, "Phoenix Elite", "PHE", "Équipe principale.", "Europe/London", "invalid.png", content);

        UpdateTeamInformationResult result = await teamService.UpdateInformationAsync(request);

        Assert.False(result.Succeeded);
        Assert.False(result.AccessDenied);
        Assert.NotEmpty(result.Errors);

        Team persistedTeam = await _context.Teams.AsNoTracking().SingleAsync(item => item.TeamId == team.TeamId);

        Assert.Equal("Phoenix Academy", persistedTeam.Name);
        Assert.Equal("PHX", persistedTeam.Tag);
        Assert.Null(persistedTeam.Description);
        Assert.Equal("Europe/Paris", persistedTeam.TimeZoneId);
        Assert.Empty(await _context.ImageFiles.AsNoTracking().ToListAsync());
        Assert.Empty(await _context.ActionTraces.AsNoTracking().ToListAsync());
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