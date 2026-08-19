using EsportTeamManager.Application.Accounts;
using EsportTeamManager.Application.Emails;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Infrastructure.Emails;
using EsportTeamManager.Infrastructure.Identity;
using EsportTeamManager.Infrastructure.Persistence;
using EsportTeamManager.Tests.Integration.Persistence;
using EsportTeamManager.Tests.TestDoubles;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EsportTeamManager.Tests.Integration.Identity;

public sealed class AccountEmailConfirmationServiceTests
{
    [Fact]
    public async Task SendConfirmationEmailAsync_WhenUserExists_SendsConfirmationEmail()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingEmailConfirmationLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountEmailConfirmationService confirmationService = scope.ServiceProvider.GetRequiredService<IAccountEmailConfirmationService>();
        DevelopmentEmailService emailService = scope.ServiceProvider.GetRequiredService<DevelopmentEmailService>();
        ApplicationUser user = await CreateUserAsync(userManager, "send@example.test", "SendPlayer", "S01", currentDateUtc);

        AccountEmailConfirmationResult result = await confirmationService.SendConfirmationEmailAsync(user.Id);
        EmailMessage sentEmail = Assert.Single(emailService.SentEmails);

        Assert.True(result.Succeeded);
        Assert.Equal(user.Email, sentEmail.Recipient);
        Assert.Equal("Confirmez votre adresse électronique", sentEmail.Subject);
        Assert.Contains("Ce lien est valable pendant 24 heures.", sentEmail.HtmlContent);
        Assert.Equal(user.Id, linkFactory.LastUserId);
        Assert.False(string.IsNullOrWhiteSpace(linkFactory.LastToken));
        Assert.Equal(currentDateUtc, user.EmailConfirmationSentAtUtc);
    }

    [Fact]
    public async Task ConfirmEmailAsync_WhenTokenIsValid_ActivatesAccount()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingEmailConfirmationLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountEmailConfirmationService confirmationService = scope.ServiceProvider.GetRequiredService<IAccountEmailConfirmationService>();
        ApplicationUser user = await CreateUserAsync(userManager, "confirm@example.test", "ConfirmPlayer", "C01", currentDateUtc);

        await confirmationService.SendConfirmationEmailAsync(user.Id);

        AccountEmailConfirmationResult result = await confirmationService.ConfirmEmailAsync(user.Id, linkFactory.LastToken!);
        ApplicationUser? confirmedUser = await userManager.FindByIdAsync(user.Id.ToString());

        Assert.True(result.Succeeded);
        Assert.NotNull(confirmedUser);
        Assert.True(confirmedUser.EmailConfirmed);
        Assert.Equal(AccountStatus.Active, confirmedUser.AccountStatus);
        Assert.Equal(currentDateUtc, confirmedUser.ConfirmedAtUtc);
    }

    [Fact]
    public async Task ConfirmEmailAsync_WhenTokenWasAlreadyUsed_ReturnsFailure()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingEmailConfirmationLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountEmailConfirmationService confirmationService = scope.ServiceProvider.GetRequiredService<IAccountEmailConfirmationService>();
        ApplicationUser user = await CreateUserAsync(userManager, "reused@example.test", "ReusedPlayer", "R01", currentDateUtc);

        await confirmationService.SendConfirmationEmailAsync(user.Id);
        await confirmationService.ConfirmEmailAsync(user.Id, linkFactory.LastToken!);

        AccountEmailConfirmationResult result = await confirmationService.ConfirmEmailAsync(user.Id, linkFactory.LastToken!);

        Assert.False(result.Succeeded);
        Assert.Contains("Ce lien de confirmation a déjà été utilisé.", result.Errors);
    }

    [Fact]
    public async Task ResendConfirmationEmailAsync_WhenDelayHasNotElapsed_DoesNotSendAnotherEmail()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingEmailConfirmationLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountEmailConfirmationService confirmationService = scope.ServiceProvider.GetRequiredService<IAccountEmailConfirmationService>();
        DevelopmentEmailService emailService = scope.ServiceProvider.GetRequiredService<DevelopmentEmailService>();
        ApplicationUser user = await CreateUserAsync(userManager, "limited@example.test", "LimitedPlayer", "L01", currentDateUtc);

        await confirmationService.SendConfirmationEmailAsync(user.Id);

        AccountEmailConfirmationResult result = await confirmationService.ResendConfirmationEmailAsync(user.Email!);

        Assert.True(result.Succeeded);
        Assert.Single(emailService.SentEmails);
    }

    [Fact]
    public async Task ResendConfirmationEmailAsync_WhenDelayHasElapsed_SendsAnotherEmail()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingEmailConfirmationLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountEmailConfirmationService confirmationService = scope.ServiceProvider.GetRequiredService<IAccountEmailConfirmationService>();
        DevelopmentEmailService emailService = scope.ServiceProvider.GetRequiredService<DevelopmentEmailService>();
        ApplicationUser user = await CreateUserAsync(userManager, "resend@example.test", "ResendPlayer", "R02", currentDateUtc);

        await confirmationService.SendConfirmationEmailAsync(user.Id);
        timeProvider.Advance(TimeSpan.FromSeconds(60));

        AccountEmailConfirmationResult result = await confirmationService.ResendConfirmationEmailAsync(user.Email!);

        Assert.True(result.Succeeded);
        Assert.Equal(2, emailService.SentEmails.Count);
    }

    [Fact]
    public async Task ResendConfirmationEmailAsync_WhenEmailIsUnknown_ReturnsNeutralSuccess()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingEmailConfirmationLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        IAccountEmailConfirmationService confirmationService = scope.ServiceProvider.GetRequiredService<IAccountEmailConfirmationService>();
        DevelopmentEmailService emailService = scope.ServiceProvider.GetRequiredService<DevelopmentEmailService>();

        AccountEmailConfirmationResult result = await confirmationService.ResendConfirmationEmailAsync("unknown@example.test");

        Assert.True(result.Succeeded);
        Assert.Empty(emailService.SentEmails);
    }

    [Fact]
    public async Task ConfirmEmailAsync_WhenTokenIsInvalid_DoesNotActivateAccount()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingEmailConfirmationLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountEmailConfirmationService confirmationService = scope.ServiceProvider.GetRequiredService<IAccountEmailConfirmationService>();
        ApplicationUser user = await CreateUserAsync(userManager, "invalid@example.test", "InvalidPlayer", "I01", currentDateUtc);

        AccountEmailConfirmationResult result = await confirmationService.ConfirmEmailAsync(user.Id, "invalid-token");
        ApplicationUser? unchangedUser = await userManager.FindByIdAsync(user.Id.ToString());

        Assert.False(result.Succeeded);
        Assert.NotNull(unchangedUser);
        Assert.False(unchangedUser.EmailConfirmed);
        Assert.Equal(AccountStatus.PendingConfirmation, unchangedUser.AccountStatus);
        Assert.Null(unchangedUser.ConfirmedAtUtc);
    }

    [Fact]
    public async Task ConfirmEmailAsync_WhenTokenHasExpired_DoesNotActivateAccount()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingEmailConfirmationLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory, TimeSpan.FromSeconds(-1));
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountEmailConfirmationService confirmationService = scope.ServiceProvider.GetRequiredService<IAccountEmailConfirmationService>();
        ApplicationUser user = await CreateUserAsync(userManager, "expired-token@example.test", "ExpiredToken", "E02", currentDateUtc);

        await confirmationService.SendConfirmationEmailAsync(user.Id);

        AccountEmailConfirmationResult result = await confirmationService.ConfirmEmailAsync(user.Id, linkFactory.LastToken!);
        ApplicationUser? unchangedUser = await userManager.FindByIdAsync(user.Id.ToString());

        Assert.False(result.Succeeded);
        Assert.Contains("Le lien de confirmation est invalide ou a expiré.", result.Errors);
        Assert.NotNull(unchangedUser);
        Assert.False(unchangedUser.EmailConfirmed);
        Assert.Equal(AccountStatus.PendingConfirmation, unchangedUser.AccountStatus);
        Assert.Null(unchangedUser.ConfirmedAtUtc);
    }

    private static ServiceProvider CreateServiceProvider(string connectionString, TimeProvider timeProvider, RecordingEmailConfirmationLinkFactory linkFactory, TimeSpan? tokenLifespan = null)
    {
        ServiceCollection services = new();

        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.User.AllowedUserNameCharacters = null!;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = tokenLifespan ?? TimeSpan.FromHours(24);
        });

        services.AddSingleton(timeProvider);
        services.AddSingleton(linkFactory);
        services.AddSingleton<IEmailConfirmationLinkFactory>(linkFactory);
        services.AddSingleton<DevelopmentEmailService>();
        services.AddSingleton<IEmailService>(serviceProvider => serviceProvider.GetRequiredService<DevelopmentEmailService>());
        services.AddScoped<IAccountEmailConfirmationService, AccountEmailConfirmationService>();

        return services.BuildServiceProvider();
    }

    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> userManager, string email, string pseudo, string tag, DateTimeOffset createdAtUtc)
    {
        ApplicationUser user = new(Guid.NewGuid(), email, pseudo, tag, createdAtUtc, createdAtUtc);
        IdentityResult creationResult = await userManager.CreateAsync(user, "Test1234!");

        Assert.True(creationResult.Succeeded, string.Join(" | ", creationResult.Errors.Select(error => error.Description)));

        return user;
    }
}