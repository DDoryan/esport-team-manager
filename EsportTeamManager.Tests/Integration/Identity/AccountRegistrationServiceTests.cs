using EsportTeamManager.Application.Accounts;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Infrastructure.Identity;
using EsportTeamManager.Infrastructure.Persistence;
using EsportTeamManager.Tests.Integration.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EsportTeamManager.Tests.TestDoubles;
using EsportTeamManager.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace EsportTeamManager.Tests.Integration.Identity;

public sealed class AccountRegistrationServiceTests
{
    [Fact]
    public async Task RegisterAsync_WhenRequestIsValid_CreatesPendingUser()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        IAccountRegistrationService registrationService = scope.ServiceProvider.GetRequiredService<IAccountRegistrationService>();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        RegisterAccountRequest request = new("player@example.test", "Player", "A01", "Test123!", true, true);

        RegisterAccountResult result = await registrationService.RegisterAsync(request);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);

        ApplicationUser? user = await userManager.FindByEmailAsync("player@example.test");

        Assert.NotNull(user);
        Assert.Equal("Player", user.Pseudo);
        Assert.Equal("A01", user.Tag);
        Assert.Equal("Player#A01", user.UserName);
        Assert.Equal(AccountStatus.PendingConfirmation, user.AccountStatus);
        Assert.False(user.EmailConfirmed);
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailDiffersOnlyByCase_ReturnsFailure()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        IAccountRegistrationService registrationService = scope.ServiceProvider.GetRequiredService<IAccountRegistrationService>();

        RegisterAccountRequest firstRequest = new("player@example.test", "FirstPlayer", "A01", "Test123!", true, true);
        RegisterAccountRequest duplicateRequest = new("PLAYER@EXAMPLE.TEST", "SecondPlayer", "B02", "Test123!", true, true);

        RegisterAccountResult firstResult = await registrationService.RegisterAsync(firstRequest);
        RegisterAccountResult duplicateResult = await registrationService.RegisterAsync(duplicateRequest);

        Assert.True(firstResult.Succeeded);
        Assert.False(duplicateResult.Succeeded);
        Assert.Contains("Cette adresse e-mail est déjà utilisée.", duplicateResult.Errors);
    }

    [Fact]
    public async Task RegisterAsync_WhenIdentityDiffersOnlyByCase_ReturnsFailure()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        IAccountRegistrationService registrationService = scope.ServiceProvider.GetRequiredService<IAccountRegistrationService>();

        RegisterAccountRequest firstRequest = new("first@example.test", "Player", "A01", "Test123!", true, true);
        RegisterAccountRequest duplicateRequest = new("second@example.test", "player", "a01", "Test123!", true, true);

        RegisterAccountResult firstResult = await registrationService.RegisterAsync(firstRequest);
        RegisterAccountResult duplicateResult = await registrationService.RegisterAsync(duplicateRequest);

        Assert.True(firstResult.Succeeded);
        Assert.False(duplicateResult.Succeeded);
        Assert.Contains("Cette combinaison de pseudonyme et de tag est déjà utilisée.", duplicateResult.Errors);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task RegisterAsync_WhenRequiredDeclarationIsMissing_DoesNotCreateUser(bool minimumAgeConfirmed, bool termsAccepted)
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        IAccountRegistrationService registrationService = scope.ServiceProvider.GetRequiredService<IAccountRegistrationService>();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        RegisterAccountRequest request = new("player@example.test", "Player", "A01", "Test123!", minimumAgeConfirmed, termsAccepted);

        RegisterAccountResult result = await registrationService.RegisterAsync(request);
        ApplicationUser? user = await userManager.FindByEmailAsync("player@example.test");

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);
        Assert.Null(user);
    }

    [Theory]
    [InlineData("Test1!")]
    [InlineData("TEST123!")]
    [InlineData("test123!")]
    [InlineData("TestTest!")]
    [InlineData("Test1234")]
    public async Task RegisterAsync_WhenPasswordDoesNotMeetPolicy_DoesNotCreateUser(string password)
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        IAccountRegistrationService registrationService = scope.ServiceProvider.GetRequiredService<IAccountRegistrationService>();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        RegisterAccountRequest request = new("player@example.test", "Player", "A01", password, true, true);

        RegisterAccountResult result = await registrationService.RegisterAsync(request);
        ApplicationUser? user = await userManager.FindByEmailAsync("player@example.test");

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);
        Assert.Null(user);
    }

    [Fact]
    public async Task RegisterAsync_WhenTermsAreAccepted_StoresCurrentTermsAcceptance()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        IAccountRegistrationService registrationService = scope.ServiceProvider.GetRequiredService<IAccountRegistrationService>();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        RegisterAccountRequest request = new("legal@example.test", "LegalPlayer", "L02", "Test1234!", true, true);

        RegisterAccountResult result = await registrationService.RegisterAsync(request);
        ApplicationUser? user = await userManager.FindByEmailAsync(request.Email);
        LegalAcceptance? legalAcceptance = user is null ? null : await context.LegalAcceptances.AsNoTracking().SingleOrDefaultAsync(acceptance => acceptance.UserId == user.Id);

        Assert.True(result.Succeeded);
        Assert.NotNull(user);
        Assert.NotNull(legalAcceptance);
        Assert.Equal(user.Id, legalAcceptance.UserId);
        Assert.Equal(1, legalAcceptance.LegalDocumentVersionId);
        Assert.NotEqual(default, legalAcceptance.AcceptedAtUtc);
    }

    [Fact]
    public async Task RegisterAsync_WhenConfirmationEmailFails_DoesNotLogSensitiveRequestData()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        RecordingLogger<AccountRegistrationService> logger = new();
        FailingAccountEmailConfirmationService emailConfirmationService = new();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, emailConfirmationService, logger);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        IAccountRegistrationService registrationService = scope.ServiceProvider.GetRequiredService<IAccountRegistrationService>();

        RegisterAccountRequest request = new("sensitive@example.test", "SensitivePlayer", "S01", "Secret123!", true, true);

        RegisterAccountResult result = await registrationService.RegisterAsync(request);

        Assert.False(result.Succeeded);

        RecordedLogEntry entry = Assert.Single(logger.Entries);

        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.False(string.IsNullOrWhiteSpace(entry.Message));
        Assert.False(entry.Message.Contains(request.Email, StringComparison.OrdinalIgnoreCase));
        Assert.False(entry.Message.Contains(request.Password, StringComparison.Ordinal));
        Assert.False(entry.Message.Contains(request.Pseudo, StringComparison.OrdinalIgnoreCase));
        Assert.False(entry.Message.Contains(request.Tag, StringComparison.OrdinalIgnoreCase));
        Assert.Null(entry.Exception);
    }

    private static ServiceProvider CreateServiceProvider(string connectionString, IAccountEmailConfirmationService? emailConfirmationService = null, ILogger<AccountRegistrationService>? registrationLogger = null)
    {
        ServiceCollection services = new();

        services.AddLogging();

        if (registrationLogger is not null)
        {
            services.AddSingleton(registrationLogger);
        }

        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireDigit = true;
            options.Password.RequireNonAlphanumeric = true;
            options.User.RequireUniqueEmail = true;
            options.User.AllowedUserNameCharacters = null!;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>();

        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IAccountEmailConfirmationService>(emailConfirmationService ?? new SuccessfulAccountEmailConfirmationService());
        services.AddScoped<IAccountRegistrationService, AccountRegistrationService>();

        return services.BuildServiceProvider();
    }
}