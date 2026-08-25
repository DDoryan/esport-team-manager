using EsportTeamManager.Application.Accounts;
using EsportTeamManager.Application.Emails;
using EsportTeamManager.Infrastructure.Emails;
using EsportTeamManager.Infrastructure.Identity;
using EsportTeamManager.Infrastructure.Persistence;
using EsportTeamManager.Tests.Integration.Persistence;
using EsportTeamManager.Tests.TestDoubles;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EsportTeamManager.Tests.Integration.Identity;

public sealed class AccountPasswordResetServiceTests
{
    [Fact]
    public void PasswordResetTokenProviderOptions_UsesOneHourLifespan()
    {
        PasswordResetTokenProviderOptions options = new();

        Assert.Equal(TimeSpan.FromHours(1), options.TokenLifespan);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_WhenEmailIsUnknown_ReturnsNeutralSuccessWithoutSendingEmail()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingPasswordResetLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        IAccountPasswordResetService passwordResetService = scope.ServiceProvider.GetRequiredService<IAccountPasswordResetService>();
        DevelopmentEmailService emailService = scope.ServiceProvider.GetRequiredService<DevelopmentEmailService>();

        AccountPasswordResetResult result = await passwordResetService.RequestPasswordResetAsync("unknown@example.test");

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);
        Assert.Empty(emailService.SentEmails);
        Assert.Null(linkFactory.LastUserId);
        Assert.Null(linkFactory.LastToken);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_WhenUserIsUnconfirmed_ReturnsNeutralSuccessWithoutSendingEmail()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingPasswordResetLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountPasswordResetService passwordResetService = scope.ServiceProvider.GetRequiredService<IAccountPasswordResetService>();
        DevelopmentEmailService emailService = scope.ServiceProvider.GetRequiredService<DevelopmentEmailService>();
        ApplicationUser user = await CreateUserAsync(userManager, "unconfirmed-reset@example.test", "UnconfirmedReset", "UR1", currentDateUtc, false);

        AccountPasswordResetResult result = await passwordResetService.RequestPasswordResetAsync(user.Email!);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);
        Assert.Empty(emailService.SentEmails);
        Assert.Null(linkFactory.LastUserId);
        Assert.Null(linkFactory.LastToken);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_WhenConfirmedUserExists_SendsEmailAndPersistsRateLimit()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingPasswordResetLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountPasswordResetService passwordResetService = scope.ServiceProvider.GetRequiredService<IAccountPasswordResetService>();
        DevelopmentEmailService emailService = scope.ServiceProvider.GetRequiredService<DevelopmentEmailService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser user = await CreateUserAsync(userManager, "send-reset@example.test", "SendReset", "SR1", currentDateUtc, true);

        AccountPasswordResetResult result = await passwordResetService.RequestPasswordResetAsync(user.Email!);
        EmailMessage sentEmail = Assert.Single(emailService.SentEmails);

        context.ChangeTracker.Clear();
        ApplicationUser? persistedUser = await userManager.FindByIdAsync(user.Id.ToString());

        Assert.True(result.Succeeded);
        Assert.Equal(user.Email, sentEmail.Recipient);
        Assert.Equal("Réinitialisez votre mot de passe", sentEmail.Subject);
        Assert.Contains("Ce lien est valable pendant une heure", sentEmail.HtmlContent);
        Assert.Contains("une seule fois", sentEmail.HtmlContent);
        Assert.Equal(user.Id, linkFactory.LastUserId);
        Assert.False(string.IsNullOrWhiteSpace(linkFactory.LastToken));
        Assert.NotNull(persistedUser);
        Assert.Equal(1, persistedUser.PasswordResetEmailCount);
        Assert.Equal(currentDateUtc, persistedUser.PasswordResetEmailWindowStartedAtUtc);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_WhenThreeEmailsWereSentWithinOneHour_DoesNotSendFourthEmail()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingPasswordResetLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountPasswordResetService passwordResetService = scope.ServiceProvider.GetRequiredService<IAccountPasswordResetService>();
        DevelopmentEmailService emailService = scope.ServiceProvider.GetRequiredService<DevelopmentEmailService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser user = await CreateUserAsync(userManager, "limited-reset@example.test", "LimitedReset", "LR1", currentDateUtc, true);

        for (int requestNumber = 0; requestNumber < 4; requestNumber++)
        {
            AccountPasswordResetResult result = await passwordResetService.RequestPasswordResetAsync(user.Email!);

            Assert.True(result.Succeeded);
            Assert.Empty(result.Errors);
        }

        context.ChangeTracker.Clear();
        ApplicationUser? persistedUser = await userManager.FindByIdAsync(user.Id.ToString());

        Assert.Equal(3, emailService.SentEmails.Count);
        Assert.NotNull(persistedUser);
        Assert.Equal(3, persistedUser.PasswordResetEmailCount);
        Assert.Equal(currentDateUtc, persistedUser.PasswordResetEmailWindowStartedAtUtc);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_WhenOneHourHasElapsed_StartsNewWindowAndSendsEmail()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingPasswordResetLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountPasswordResetService passwordResetService = scope.ServiceProvider.GetRequiredService<IAccountPasswordResetService>();
        DevelopmentEmailService emailService = scope.ServiceProvider.GetRequiredService<DevelopmentEmailService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser user = await CreateUserAsync(userManager, "new-window@example.test", "NewWindow", "NW1", currentDateUtc, true);

        for (int requestNumber = 0; requestNumber < 3; requestNumber++)
        {
            await passwordResetService.RequestPasswordResetAsync(user.Email!);
        }

        timeProvider.Advance(TimeSpan.FromHours(1));

        AccountPasswordResetResult result = await passwordResetService.RequestPasswordResetAsync(user.Email!);

        context.ChangeTracker.Clear();
        ApplicationUser? persistedUser = await userManager.FindByIdAsync(user.Id.ToString());

        Assert.True(result.Succeeded);
        Assert.Equal(4, emailService.SentEmails.Count);
        Assert.NotNull(persistedUser);
        Assert.Equal(1, persistedUser.PasswordResetEmailCount);
        Assert.Equal(currentDateUtc.AddHours(1), persistedUser.PasswordResetEmailWindowStartedAtUtc);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenTokenIsValid_ChangesPassword()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingPasswordResetLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountPasswordResetService passwordResetService = scope.ServiceProvider.GetRequiredService<IAccountPasswordResetService>();
        ApplicationUser user = await CreateUserAsync(userManager, "valid-reset@example.test", "ValidReset", "VR1", currentDateUtc, true);
        string token = await userManager.GeneratePasswordResetTokenAsync(user);

        AccountPasswordResetResult result = await passwordResetService.ResetPasswordAsync(user.Id, token, "NewPassword1!");

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);
        Assert.True(await userManager.CheckPasswordAsync(user, "NewPassword1!"));
        Assert.False(await userManager.CheckPasswordAsync(user, "InitialPassword1!"));
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenTokenWasAlreadyUsed_ReturnsFailureAndKeepsFirstNewPassword()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingPasswordResetLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountPasswordResetService passwordResetService = scope.ServiceProvider.GetRequiredService<IAccountPasswordResetService>();
        ApplicationUser user = await CreateUserAsync(userManager, "used-reset@example.test", "UsedReset", "UR2", currentDateUtc, true);
        string token = await userManager.GeneratePasswordResetTokenAsync(user);

        AccountPasswordResetResult firstResult = await passwordResetService.ResetPasswordAsync(user.Id, token, "FirstPassword1!");
        AccountPasswordResetResult secondResult = await passwordResetService.ResetPasswordAsync(user.Id, token, "SecondPassword1!");

        Assert.True(firstResult.Succeeded);
        Assert.False(secondResult.Succeeded);
        Assert.Contains("Le lien de réinitialisation est invalide, a expiré ou a déjà été utilisé.", secondResult.Errors);
        Assert.True(await userManager.CheckPasswordAsync(user, "FirstPassword1!"));
        Assert.False(await userManager.CheckPasswordAsync(user, "SecondPassword1!"));
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenTokenIsInvalid_DoesNotChangePassword()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingPasswordResetLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountPasswordResetService passwordResetService = scope.ServiceProvider.GetRequiredService<IAccountPasswordResetService>();
        ApplicationUser user = await CreateUserAsync(userManager, "invalid-reset@example.test", "InvalidReset", "IR1", currentDateUtc, true);

        AccountPasswordResetResult result = await passwordResetService.ResetPasswordAsync(user.Id, "invalid-token", "NewPassword1!");

        Assert.False(result.Succeeded);
        Assert.Contains("Le lien de réinitialisation est invalide, a expiré ou a déjà été utilisé.", result.Errors);
        Assert.True(await userManager.CheckPasswordAsync(user, "InitialPassword1!"));
        Assert.False(await userManager.CheckPasswordAsync(user, "NewPassword1!"));
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenTokenHasExpired_DoesNotChangePassword()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingPasswordResetLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory, TimeSpan.FromSeconds(-1));
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountPasswordResetService passwordResetService = scope.ServiceProvider.GetRequiredService<IAccountPasswordResetService>();
        ApplicationUser user = await CreateUserAsync(userManager, "expired-reset@example.test", "ExpiredReset", "ER1", currentDateUtc, true);
        string token = await userManager.GeneratePasswordResetTokenAsync(user);

        AccountPasswordResetResult result = await passwordResetService.ResetPasswordAsync(user.Id, token, "NewPassword1!");

        Assert.False(result.Succeeded);
        Assert.Contains("Le lien de réinitialisation est invalide, a expiré ou a déjà été utilisé.", result.Errors);
        Assert.True(await userManager.CheckPasswordAsync(user, "InitialPassword1!"));
        Assert.False(await userManager.CheckPasswordAsync(user, "NewPassword1!"));
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenPasswordDoesNotMeetPolicy_ReturnsTranslatedErrors()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingPasswordResetLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountPasswordResetService passwordResetService = scope.ServiceProvider.GetRequiredService<IAccountPasswordResetService>();
        ApplicationUser user = await CreateUserAsync(userManager, "weak-reset@example.test", "WeakReset", "WR1", currentDateUtc, true);
        string token = await userManager.GeneratePasswordResetTokenAsync(user);

        AccountPasswordResetResult result = await passwordResetService.ResetPasswordAsync(user.Id, token, "short");

        Assert.False(result.Succeeded);
        Assert.Contains("Le mot de passe doit contenir au moins 8 caractères.", result.Errors);
        Assert.Contains("Le mot de passe doit contenir une lettre majuscule.", result.Errors);
        Assert.Contains("Le mot de passe doit contenir un chiffre.", result.Errors);
        Assert.Contains("Le mot de passe doit contenir un caractère non alphanumérique.", result.Errors);
        Assert.True(await userManager.CheckPasswordAsync(user, "InitialPassword1!"));
    }

    private static ServiceProvider CreateServiceProvider(string connectionString, TimeProvider timeProvider, RecordingPasswordResetLinkFactory linkFactory, TimeSpan? tokenLifespan = null)
    {
        ServiceCollection services = new();

        services.AddLogging();
        services.AddDataProtection();
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

            options.Tokens.ProviderMap.Add("PasswordReset", new TokenProviderDescriptor(typeof(PasswordResetTokenProvider<ApplicationUser>)));
            options.Tokens.PasswordResetTokenProvider = "PasswordReset";
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        services.Configure<PasswordResetTokenProviderOptions>(options =>
        {
            options.TokenLifespan = tokenLifespan ?? TimeSpan.FromHours(1);
        });

        services.AddTransient<PasswordResetTokenProvider<ApplicationUser>>();
        services.AddSingleton(timeProvider);
        services.AddSingleton(linkFactory);
        services.AddSingleton<IPasswordResetLinkFactory>(linkFactory);
        services.AddSingleton<DevelopmentEmailService>();
        services.AddSingleton<IEmailService>(serviceProvider => serviceProvider.GetRequiredService<DevelopmentEmailService>());
        services.AddScoped<IAccountPasswordResetService, AccountPasswordResetService>();

        return services.BuildServiceProvider();
    }

    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> userManager, string email, string pseudo, string tag, DateTimeOffset createdAtUtc, bool confirmed)
    {
        ApplicationUser user = new(Guid.NewGuid(), email, pseudo, tag, createdAtUtc, createdAtUtc);
        IdentityResult creationResult = await userManager.CreateAsync(user, "InitialPassword1!");

        Assert.True(creationResult.Succeeded, string.Join(" | ", creationResult.Errors.Select(error => error.Description)));

        if (!confirmed)
        {
            return user;
        }

        string confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        IdentityResult confirmationResult = await userManager.ConfirmEmailAsync(user, confirmationToken);

        Assert.True(confirmationResult.Succeeded, string.Join(" | ", confirmationResult.Errors.Select(error => error.Description)));

        user.MarkAsConfirmed(createdAtUtc);

        IdentityResult updateResult = await userManager.UpdateAsync(user);

        Assert.True(updateResult.Succeeded, string.Join(" | ", updateResult.Errors.Select(error => error.Description)));

        return user;
    }
}