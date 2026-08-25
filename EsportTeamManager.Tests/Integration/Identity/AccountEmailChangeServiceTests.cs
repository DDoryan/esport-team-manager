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

public sealed class AccountEmailChangeServiceTests
{
    [Fact]
    public void EmailChangeTokenProviderOptions_UsesOneHourLifespan()
    {
        EmailChangeTokenProviderOptions options = new();

        Assert.Equal(TimeSpan.FromHours(1), options.TokenLifespan);
    }

    [Fact]
    public async Task RequestEmailChangeAsync_WhenRequestIsValid_ReservesEmailAndSendsConfirmation()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingEmailChangeLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountEmailChangeService emailChangeService = scope.ServiceProvider.GetRequiredService<IAccountEmailChangeService>();
        DevelopmentEmailService emailService = scope.ServiceProvider.GetRequiredService<DevelopmentEmailService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser user = await CreateConfirmedUserAsync(userManager, "current@example.test", "CurrentUser", "CU1", currentDateUtc);

        ChangeAccountEmailRequest request = new(user.Id, "InitialPassword1!", " new-address@example.test ");

        ChangeAccountEmailResult result = await emailChangeService.RequestEmailChangeAsync(request);
        EmailMessage sentEmail = Assert.Single(emailService.SentEmails);

        context.ChangeTracker.Clear();
        ApplicationUser? persistedUser = await userManager.FindByIdAsync(user.Id.ToString());

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);
        Assert.NotNull(persistedUser);
        Assert.Equal("current@example.test", persistedUser.Email);
        Assert.Equal("new-address@example.test", persistedUser.PendingEmail);
        Assert.Equal(userManager.NormalizeEmail("new-address@example.test"), persistedUser.NormalizedPendingEmail);
        Assert.Equal(currentDateUtc.AddHours(1), persistedUser.PendingEmailExpiresAtUtc);
        Assert.Equal("new-address@example.test", sentEmail.Recipient);
        Assert.Equal("Confirmez votre nouvelle adresse électronique", sentEmail.Subject);
        Assert.Contains("Ce lien est valable pendant une heure", sentEmail.HtmlContent);
        Assert.Contains("une seule fois", sentEmail.HtmlContent);
        Assert.Contains("Votre ancienne adresse reste active", sentEmail.HtmlContent);
        Assert.Equal(user.Id, linkFactory.LastUserId);
        Assert.False(string.IsNullOrWhiteSpace(linkFactory.LastToken));
    }

    [Fact]
    public async Task RequestEmailChangeAsync_WhenPasswordIsInvalid_ReturnsFailureWithoutReservation()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingEmailChangeLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountEmailChangeService emailChangeService = scope.ServiceProvider.GetRequiredService<IAccountEmailChangeService>();
        DevelopmentEmailService emailService = scope.ServiceProvider.GetRequiredService<DevelopmentEmailService>();
        ApplicationUser user = await CreateConfirmedUserAsync(userManager, "password@example.test", "PasswordUser", "PU1", currentDateUtc);

        ChangeAccountEmailResult result = await emailChangeService.RequestEmailChangeAsync(new ChangeAccountEmailRequest(user.Id, "WrongPassword1!", "new@example.test"));

        Assert.False(result.Succeeded);
        Assert.Contains("Le mot de passe actuel est incorrect.", result.Errors);
        Assert.Empty(emailService.SentEmails);
        Assert.Null(user.PendingEmail);
        Assert.Null(linkFactory.LastToken);
    }

    [Fact]
    public async Task RequestEmailChangeAsync_WhenEmailIsAlreadyActive_ReturnsFailure()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingEmailChangeLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountEmailChangeService emailChangeService = scope.ServiceProvider.GetRequiredService<IAccountEmailChangeService>();
        DevelopmentEmailService emailService = scope.ServiceProvider.GetRequiredService<DevelopmentEmailService>();
        ApplicationUser requester = await CreateConfirmedUserAsync(userManager, "requester@example.test", "Requester", "RQ1", currentDateUtc);

        await CreateConfirmedUserAsync(userManager, "used@example.test", "UsedEmail", "UE1", currentDateUtc);

        ChangeAccountEmailResult result = await emailChangeService.RequestEmailChangeAsync(new ChangeAccountEmailRequest(requester.Id, "InitialPassword1!", " USED@EXAMPLE.TEST "));

        Assert.False(result.Succeeded);
        Assert.Contains("Cette adresse e-mail est déjà utilisée ou réservée.", result.Errors);
        Assert.Empty(emailService.SentEmails);
        Assert.Null(requester.PendingEmail);
    }

    [Fact]
    public async Task RequestEmailChangeAsync_WhenEmailIsReservedByAnotherUser_ReturnsFailure()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingEmailChangeLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountEmailChangeService emailChangeService = scope.ServiceProvider.GetRequiredService<IAccountEmailChangeService>();
        DevelopmentEmailService emailService = scope.ServiceProvider.GetRequiredService<DevelopmentEmailService>();
        ApplicationUser requester = await CreateConfirmedUserAsync(userManager, "requester@example.test", "Requester", "RQ1", currentDateUtc);
        ApplicationUser reservationOwner = await CreateConfirmedUserAsync(userManager, "owner@example.test", "Owner", "OW1", currentDateUtc);
        string normalizedReservedEmail = userManager.NormalizeEmail("reserved@example.test") ?? throw new InvalidOperationException("L’adresse réservée n’a pas pu être normalisée.");

        reservationOwner.ReservePendingEmail("reserved@example.test", normalizedReservedEmail, currentDateUtc);

        IdentityResult updateResult = await userManager.UpdateAsync(reservationOwner);

        Assert.True(updateResult.Succeeded);

        ChangeAccountEmailResult result = await emailChangeService.RequestEmailChangeAsync(new ChangeAccountEmailRequest(requester.Id, "InitialPassword1!", " RESERVED@EXAMPLE.TEST "));

        Assert.False(result.Succeeded);
        Assert.Contains("Cette adresse e-mail est déjà utilisée ou réservée.", result.Errors);
        Assert.Empty(emailService.SentEmails);
        Assert.Null(requester.PendingEmail);
    }

    [Fact]
    public async Task RequestEmailChangeAsync_WhenPreviousReservationHasExpired_ReleasesAndReservesEmail()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingEmailChangeLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountEmailChangeService emailChangeService = scope.ServiceProvider.GetRequiredService<IAccountEmailChangeService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser requester = await CreateConfirmedUserAsync(userManager, "requester@example.test", "Requester", "RQ1", currentDateUtc);
        ApplicationUser previousOwner = await CreateConfirmedUserAsync(userManager, "owner@example.test", "Owner", "OW1", currentDateUtc);
        string normalizedReleasedEmail = userManager.NormalizeEmail("released@example.test") ?? throw new InvalidOperationException("L’adresse réservée n’a pas pu être normalisée.");

        previousOwner.ReservePendingEmail("released@example.test", normalizedReleasedEmail, currentDateUtc.AddHours(-2));

        IdentityResult updateResult = await userManager.UpdateAsync(previousOwner);

        Assert.True(updateResult.Succeeded);

        ChangeAccountEmailResult result = await emailChangeService.RequestEmailChangeAsync(new ChangeAccountEmailRequest(requester.Id, "InitialPassword1!", "released@example.test"));

        context.ChangeTracker.Clear();

        ApplicationUser? persistedRequester = await userManager.FindByIdAsync(requester.Id.ToString());
        ApplicationUser? persistedPreviousOwner = await userManager.FindByIdAsync(previousOwner.Id.ToString());

        Assert.True(result.Succeeded);
        Assert.NotNull(persistedRequester);
        Assert.NotNull(persistedPreviousOwner);
        Assert.Equal("released@example.test", persistedRequester.PendingEmail);
        Assert.Null(persistedPreviousOwner.PendingEmail);
        Assert.Null(persistedPreviousOwner.NormalizedPendingEmail);
        Assert.Null(persistedPreviousOwner.PendingEmailExpiresAtUtc);
    }

    [Fact]
    public async Task ConfirmEmailChangeAsync_WhenTokenIsValid_ChangesEmailOnlyOnce()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingEmailChangeLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountEmailChangeService emailChangeService = scope.ServiceProvider.GetRequiredService<IAccountEmailChangeService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser user = await CreateConfirmedUserAsync(userManager, "old@example.test", "EmailUser", "EU1", currentDateUtc);

        ChangeAccountEmailResult requestResult = await emailChangeService.RequestEmailChangeAsync(new ChangeAccountEmailRequest(user.Id, "InitialPassword1!", "new@example.test"));
        string token = linkFactory.LastToken ?? throw new InvalidOperationException("Le jeton de changement d’adresse n’a pas été généré.");

        ChangeAccountEmailResult confirmationResult = await emailChangeService.ConfirmEmailChangeAsync(user.Id, token);
        ChangeAccountEmailResult secondConfirmationResult = await emailChangeService.ConfirmEmailChangeAsync(user.Id, token);

        context.ChangeTracker.Clear();
        ApplicationUser? persistedUser = await userManager.FindByIdAsync(user.Id.ToString());

        Assert.True(requestResult.Succeeded);
        Assert.True(confirmationResult.Succeeded);
        Assert.False(secondConfirmationResult.Succeeded);
        Assert.NotNull(persistedUser);
        Assert.Equal("new@example.test", persistedUser.Email);
        Assert.Equal(userManager.NormalizeEmail("new@example.test"), persistedUser.NormalizedEmail);
        Assert.True(persistedUser.EmailConfirmed);
        Assert.Null(persistedUser.PendingEmail);
        Assert.Null(persistedUser.NormalizedPendingEmail);
        Assert.Null(persistedUser.PendingEmailExpiresAtUtc);
    }

    [Fact]
    public async Task ConfirmEmailChangeAsync_WhenReservationHasExpired_KeepsCurrentEmailAndClearsReservation()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);
        RecordingEmailChangeLinkFactory linkFactory = new();

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, timeProvider, linkFactory);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountEmailChangeService emailChangeService = scope.ServiceProvider.GetRequiredService<IAccountEmailChangeService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser user = await CreateConfirmedUserAsync(userManager, "old@example.test", "ExpiredUser", "EX1", currentDateUtc);

        ChangeAccountEmailResult requestResult = await emailChangeService.RequestEmailChangeAsync(new ChangeAccountEmailRequest(user.Id, "InitialPassword1!", "expired@example.test"));
        string token = linkFactory.LastToken ?? throw new InvalidOperationException("Le jeton de changement d’adresse n’a pas été généré.");

        timeProvider.Advance(TimeSpan.FromHours(1));

        ChangeAccountEmailResult confirmationResult = await emailChangeService.ConfirmEmailChangeAsync(user.Id, token);

        context.ChangeTracker.Clear();
        ApplicationUser? persistedUser = await userManager.FindByIdAsync(user.Id.ToString());

        Assert.True(requestResult.Succeeded);
        Assert.False(confirmationResult.Succeeded);
        Assert.Contains("Le lien de changement d’adresse est invalide ou a expiré.", confirmationResult.Errors);
        Assert.NotNull(persistedUser);
        Assert.Equal("old@example.test", persistedUser.Email);
        Assert.Null(persistedUser.PendingEmail);
        Assert.Null(persistedUser.NormalizedPendingEmail);
        Assert.Null(persistedUser.PendingEmailExpiresAtUtc);
    }

    private static ServiceProvider CreateServiceProvider(string connectionString, TimeProvider timeProvider, RecordingEmailChangeLinkFactory linkFactory)
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

            options.Tokens.ProviderMap.Add("EmailChange", new TokenProviderDescriptor(typeof(EmailChangeTokenProvider<ApplicationUser>)));
            options.Tokens.ChangeEmailTokenProvider = "EmailChange";
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        services.Configure<EmailChangeTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromHours(1);
        });

        services.AddTransient<EmailChangeTokenProvider<ApplicationUser>>();
        services.AddSingleton(timeProvider);
        services.AddSingleton(linkFactory);
        services.AddSingleton<IEmailChangeLinkFactory>(linkFactory);
        services.AddSingleton<DevelopmentEmailService>();
        services.AddSingleton<IEmailService>(serviceProvider => serviceProvider.GetRequiredService<DevelopmentEmailService>());
        services.AddScoped<IAccountEmailChangeService, AccountEmailChangeService>();

        return services.BuildServiceProvider();
    }

    private static async Task<ApplicationUser> CreateConfirmedUserAsync(UserManager<ApplicationUser> userManager, string email, string pseudo, string tag, DateTimeOffset createdAtUtc)
    {
        ApplicationUser user = new(Guid.NewGuid(), email, pseudo, tag, createdAtUtc, createdAtUtc);
        IdentityResult creationResult = await userManager.CreateAsync(user, "InitialPassword1!");

        Assert.True(creationResult.Succeeded, string.Join(" | ", creationResult.Errors.Select(error => error.Description)));

        string confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        IdentityResult confirmationResult = await userManager.ConfirmEmailAsync(user, confirmationToken);

        Assert.True(confirmationResult.Succeeded, string.Join(" | ", confirmationResult.Errors.Select(error => error.Description)));

        user.MarkAsConfirmed(createdAtUtc);

        IdentityResult updateResult = await userManager.UpdateAsync(user);

        Assert.True(updateResult.Succeeded, string.Join(" | ", updateResult.Errors.Select(error => error.Description)));

        return user;
    }
}