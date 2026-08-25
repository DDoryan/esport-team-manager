using EsportTeamManager.Application.Accounts;
using EsportTeamManager.Infrastructure.Identity;
using EsportTeamManager.Infrastructure.Persistence;
using EsportTeamManager.Tests.Integration.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EsportTeamManager.Tests.Integration.Identity;

public sealed class AccountProfileServiceTests
{
    private const string ValidEmail = "profile@example.test";

    private const string ValidPassword = "Test123!";

    private const string NewPassword = "Changed123!";

    [Fact]
    public async Task GetProfileAsync_WhenUserExists_ReturnsImmutableIdentityAndEmail()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        CreateHttpContext(scope.ServiceProvider);

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountProfileService profileService = scope.ServiceProvider.GetRequiredService<IAccountProfileService>();

        ApplicationUser user = await CreateConfirmedUserAsync(userManager);

        AccountProfile? profile = await profileService.GetProfileAsync(user.Id);

        Assert.NotNull(profile);
        Assert.Equal("ProfileTest", profile.Pseudo);
        Assert.Equal("A05", profile.Tag);
        Assert.Equal(ValidEmail, profile.Email);
        Assert.False(typeof(AccountProfile).GetProperty(nameof(AccountProfile.Pseudo))!.CanWrite);
        Assert.False(typeof(AccountProfile).GetProperty(nameof(AccountProfile.Tag))!.CanWrite);
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenCurrentPasswordIsInvalid_ReturnsErrorWithoutChangingPassword()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        CreateHttpContext(scope.ServiceProvider);

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountProfileService profileService = scope.ServiceProvider.GetRequiredService<IAccountProfileService>();

        ApplicationUser user = await CreateConfirmedUserAsync(userManager);
        string initialSecurityStamp = await userManager.GetSecurityStampAsync(user);

        ChangeAccountPasswordResult result = await profileService.ChangePasswordAsync(new ChangeAccountPasswordRequest(user.Id, "Wrong123!", NewPassword));
        string currentSecurityStamp = await userManager.GetSecurityStampAsync(user);

        Assert.False(result.Succeeded);
        Assert.Contains("Le mot de passe actuel est incorrect.", result.Errors);
        Assert.Equal(initialSecurityStamp, currentSecurityStamp);
        Assert.True(await userManager.CheckPasswordAsync(user, ValidPassword));
        Assert.False(await userManager.CheckPasswordAsync(user, NewPassword));
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenCurrentPasswordIsValid_ChangesSecurityStampAndRefreshesCurrentSession()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);

        Guid userId;
        string initialSecurityStamp;
        string authenticationCookie;

        await using (AsyncServiceScope loginScope = serviceProvider.CreateAsyncScope())
        {
            DefaultHttpContext loginContext = CreateHttpContext(loginScope.ServiceProvider);

            UserManager<ApplicationUser> userManager = loginScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            SignInManager<ApplicationUser> signInManager = loginScope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();

            ApplicationUser user = await CreateConfirmedUserAsync(userManager);

            userId = user.Id;
            initialSecurityStamp = await userManager.GetSecurityStampAsync(user);

            SignInResult signInResult = await signInManager.PasswordSignInAsync(user, ValidPassword, true, false);

            Assert.True(signInResult.Succeeded);

            authenticationCookie = loginContext.Response.Headers.SetCookie.ToString().Split(';', 2)[0];
        }

        await using AsyncServiceScope changePasswordScope = serviceProvider.CreateAsyncScope();

        DefaultHttpContext changePasswordContext = CreateHttpContext(changePasswordScope.ServiceProvider);
        changePasswordContext.Request.Headers.Cookie = authenticationCookie;

        UserManager<ApplicationUser> changePasswordUserManager = changePasswordScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAccountProfileService profileService = changePasswordScope.ServiceProvider.GetRequiredService<IAccountProfileService>();

        ChangeAccountPasswordResult result = await profileService.ChangePasswordAsync(new ChangeAccountPasswordRequest(userId, ValidPassword, NewPassword));

        ApplicationUser updatedUser = await changePasswordUserManager.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("Le compte de test est introuvable.");
        string currentSecurityStamp = await changePasswordUserManager.GetSecurityStampAsync(updatedUser);
        string refreshedAuthenticationCookie = changePasswordContext.Response.Headers.SetCookie.ToString();

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);
        Assert.NotEqual(initialSecurityStamp, currentSecurityStamp);
        Assert.False(await changePasswordUserManager.CheckPasswordAsync(updatedUser, ValidPassword));
        Assert.True(await changePasswordUserManager.CheckPasswordAsync(updatedUser, NewPassword));
        Assert.Contains("__Host-EsportTeamManager.Auth", refreshedAuthenticationCookie);
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

            options.SignIn.RequireConfirmedEmail = true;

            options.User.RequireUniqueEmail = true;
            options.User.AllowedUserNameCharacters = null!;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();

        services.AddScoped<IAccountProfileService, AccountProfileService>();

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
        ApplicationUser user = new(Guid.NewGuid(), ValidEmail, "ProfileTest", "A05", utcNow, utcNow);

        IdentityResult creationResult = await userManager.CreateAsync(user, ValidPassword);

        Assert.True(creationResult.Succeeded);

        string confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        IdentityResult confirmationResult = await userManager.ConfirmEmailAsync(user, confirmationToken);

        Assert.True(confirmationResult.Succeeded);

        return user;
    }
}