using EsportTeamManager.Application.Accounts;
using EsportTeamManager.Infrastructure.Identity;
using EsportTeamManager.Infrastructure.Persistence;
using EsportTeamManager.Tests.Integration.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EsportTeamManager.Tests.Integration.Identity;

public sealed class AccountAuthenticationServiceTests
{
    private const string ValidEmail = "authentication@example.test";

    private const string ValidPassword = "Test123!";

    [Fact]
    public async Task LoginAsync_WhenCredentialsAreValid_Succeeds()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        CreateHttpContext(scope.ServiceProvider);

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountAuthenticationService authenticationService = scope.ServiceProvider.GetRequiredService<IAccountAuthenticationService>();

        await CreateConfirmedUserAsync(userManager);

        LoginAccountResult result = await authenticationService.LoginAsync(new LoginAccountRequest(ValidEmail, ValidPassword, false));

        Assert.True(result.Succeeded);
        Assert.Empty(result.ErrorMessage);
    }

    [Fact]
    public async Task LoginAsync_WhenCredentialsAreInvalid_ReturnsGenericError()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        CreateHttpContext(scope.ServiceProvider);

        IAccountAuthenticationService authenticationService = scope.ServiceProvider.GetRequiredService<IAccountAuthenticationService>();

        LoginAccountResult result = await authenticationService.LoginAsync(new LoginAccountRequest("unknown@example.test", ValidPassword, false));

        Assert.False(result.Succeeded);
        Assert.Equal("Connexion impossible. Vérifiez vos informations ou réessayez plus tard.", result.ErrorMessage);
    }

    [Fact]
    public async Task LoginAsync_WhenFiveAttemptsFail_LocksAccount()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        CreateHttpContext(scope.ServiceProvider);

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountAuthenticationService authenticationService = scope.ServiceProvider.GetRequiredService<IAccountAuthenticationService>();

        await CreateConfirmedUserAsync(userManager);

        for (int attempt = 0; attempt < 5; attempt++)
        {
            LoginAccountResult failedResult = await authenticationService.LoginAsync(new LoginAccountRequest(ValidEmail, "Wrong123!", false));

            Assert.False(failedResult.Succeeded);
        }

        ApplicationUser? user = await userManager.FindByEmailAsync(ValidEmail);

        Assert.NotNull(user);
        Assert.True(await userManager.IsLockedOutAsync(user));

        LoginAccountResult validCredentialsResult = await authenticationService.LoginAsync(new LoginAccountRequest(ValidEmail, ValidPassword, false));

        Assert.False(validCredentialsResult.Succeeded);
    }

    [Fact]
    public async Task LoginAsync_WhenRememberMeIsSelected_CreatesPersistentCookie()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        DefaultHttpContext httpContext = CreateHttpContext(scope.ServiceProvider);

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountAuthenticationService authenticationService = scope.ServiceProvider.GetRequiredService<IAccountAuthenticationService>();

        await CreateConfirmedUserAsync(userManager);

        LoginAccountResult result = await authenticationService.LoginAsync(new LoginAccountRequest(ValidEmail, ValidPassword, true));
        string authenticationCookie = httpContext.Response.Headers.SetCookie.ToString();

        Assert.True(result.Succeeded);
        Assert.Contains("__Host-EsportTeamManager.Auth", authenticationCookie);
        Assert.Contains("expires=", authenticationCookie.ToLowerInvariant());
    }

    [Fact]
    public async Task LogoutAsync_WhenUserIsSignedIn_ExpiresAuthenticationCookie()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        DefaultHttpContext httpContext = CreateHttpContext(scope.ServiceProvider);

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountAuthenticationService authenticationService = scope.ServiceProvider.GetRequiredService<IAccountAuthenticationService>();

        await CreateConfirmedUserAsync(userManager);
        await authenticationService.LoginAsync(new LoginAccountRequest(ValidEmail, ValidPassword, false));

        httpContext.Response.Headers.Clear();

        await authenticationService.LogoutAsync();

        string authenticationCookie = httpContext.Response.Headers.SetCookie.ToString();

        Assert.Contains("__Host-EsportTeamManager.Auth", authenticationCookie);
        Assert.Contains("expires=", authenticationCookie.ToLowerInvariant());
    }

    private static ServiceProvider CreateServiceProvider(string connectionString)
    {
        ServiceCollection services = new();

        services.AddLogging();
        services.AddDataProtection();
        services.AddHttpContextAccessor();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
            options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
        })
        .AddCookie(IdentityConstants.ApplicationScheme, options =>
        {
            options.Cookie.Name = "__Host-EsportTeamManager.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.Path = "/";
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
        });

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireDigit = true;
            options.Password.RequireNonAlphanumeric = true;

            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

            options.SignIn.RequireConfirmedEmail = true;

            options.User.RequireUniqueEmail = true;
            options.User.AllowedUserNameCharacters = null!;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();

        services.AddScoped<IAccountAuthenticationService, AccountAuthenticationService>();

        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext CreateHttpContext(IServiceProvider serviceProvider)
    {
        DefaultHttpContext httpContext = new()
        {
            RequestServices = serviceProvider
        };

        httpContext.Request.Scheme = "https";
        httpContext.Response.Body = Stream.Null;

        serviceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

        return httpContext;
    }

    private static async Task<ApplicationUser> CreateConfirmedUserAsync(UserManager<ApplicationUser> userManager)
    {
        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        ApplicationUser user = new(Guid.NewGuid(), ValidEmail, "AuthTest", "A03", utcNow, utcNow);

        IdentityResult creationResult = await userManager.CreateAsync(user, ValidPassword);

        Assert.True(creationResult.Succeeded);

        string confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        IdentityResult confirmationResult = await userManager.ConfirmEmailAsync(user, confirmationToken);

        Assert.True(confirmationResult.Succeeded);

        return user;
    }
}