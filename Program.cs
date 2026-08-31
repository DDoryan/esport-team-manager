using EsportTeamManager.Infrastructure.Identity;
using EsportTeamManager.Infrastructure.Persistence;
using EsportTeamManager.Infrastructure.PostgreSql.Migrations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using RepriseWeb.Middleware;
using EsportTeamManager.Application.Emails;
using EsportTeamManager.Infrastructure.Emails;
using EsportTeamManager.Application.Accounts;
using EsportTeamManager.Web.Services.Accounts;
using EsportTeamManager.Application.Teams;
using EsportTeamManager.Infrastructure.Teams;
using EsportTeamManager.Application.Activities;
using EsportTeamManager.Infrastructure.Activities;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using EsportTeamManager.Application.Notifications;
using EsportTeamManager.Infrastructure.Notifications;
using EsportTeamManager.Application.Images;
using EsportTeamManager.Infrastructure.Images;
using EsportTeamManager.Application.Strategies;
using EsportTeamManager.Infrastructure.Strategies;

var builder = WebApplication.CreateBuilder(args);

string? railwayPort = Environment.GetEnvironmentVariable("PORT");

if (int.TryParse(railwayPort, out int parsedRailwayPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{parsedRailwayPort}");
}

string databaseProvider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("La chaîne de connexion est introuvable.");
string postgreSqlMigrationsAssemblyName = typeof(PostgreSqlMigrationsAssemblyMarker).Assembly.GetName().Name ?? throw new InvalidOperationException("L’assembly des migrations PostgreSQL est introuvable.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (string.Equals(databaseProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(connectionString, postgreSqlOptions =>
        {
            postgreSqlOptions.MigrationsAssembly(postgreSqlMigrationsAssemblyName);
        });

        return;
    }

    if (!string.Equals(databaseProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Le fournisseur de base de données « {databaseProvider} » n’est pas pris en charge.");
    }

    options.UseSqlite(connectionString);
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
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

    options.Tokens.ProviderMap.Add("PasswordReset", new TokenProviderDescriptor(typeof(PasswordResetTokenProvider<ApplicationUser>)));
    options.Tokens.PasswordResetTokenProvider = "PasswordReset";

    options.Tokens.ProviderMap.Add("EmailChange", new TokenProviderDescriptor(typeof(EmailChangeTokenProvider<ApplicationUser>)));
    options.Tokens.ChangeEmailTokenProvider = "EmailChange";
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddTransient<PasswordResetTokenProvider<ApplicationUser>>();
builder.Services.AddTransient<EmailChangeTokenProvider<ApplicationUser>>();

if (builder.Environment.IsProduction())
{
    builder.Services.AddDataProtection().SetApplicationName("EsportTeamManager").PersistKeysToDbContext<ApplicationDbContext>();
}
else
{
    builder.Services.AddDataProtection().SetApplicationName("EsportTeamManager");
}

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(24);
});

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__Host-EsportTeamManager.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Path = "/";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddHealthChecks();

builder.Services.AddOptions<PrivateImageStorageOptions>()
    .Bind(builder.Configuration.GetSection(PrivateImageStorageOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => options.TeamLogoMaximumEdgePixels <= options.MaximumDimensionPixels, "La dimension optimisée des logos doit respecter la dimension maximale autorisée.")
    .Validate(options => options.StrategyImageMaximumEdgePixels <= options.MaximumDimensionPixels, "La dimension optimisée des stratégies doit respecter la dimension maximale autorisée.")
    .Validate(options => options.ThumbnailMaximumEdgePixels <= options.TeamLogoMaximumEdgePixels && options.ThumbnailMaximumEdgePixels <= options.StrategyImageMaximumEdgePixels, "La miniature doit être plus petite que les images optimisées.")
    .ValidateOnStart();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IEmailConfirmationLinkFactory, EmailConfirmationLinkFactory>();
builder.Services.AddScoped<IEmailChangeLinkFactory, EmailChangeLinkFactory>();
builder.Services.AddScoped<IAccountEmailChangeService, AccountEmailChangeService>();
builder.Services.AddScoped<IAccountEmailConfirmationService, AccountEmailConfirmationService>();
builder.Services.AddScoped<IPasswordResetLinkFactory, PasswordResetLinkFactory>();
builder.Services.AddScoped<IAccountPasswordResetService, AccountPasswordResetService>();
builder.Services.AddScoped<IAccountProfileService, AccountProfileService>();
builder.Services.AddScoped<IUnconfirmedAccountCleanupService, UnconfirmedAccountCleanupService>();
builder.Services.AddHostedService<UnconfirmedAccountCleanupBackgroundService>();
builder.Services.AddScoped<IAccountRegistrationService, AccountRegistrationService>();
builder.Services.AddScoped<IAccountAuthenticationService, AccountAuthenticationService>();
builder.Services.AddScoped<IUserTeamService, UserTeamService>();
builder.Services.AddScoped<IUserNotificationService, UserNotificationService>();
builder.Services.AddScoped<IPrivateImageService, PrivateImageService>();
builder.Services.AddScoped<IActivityCalendarService, ActivityCalendarService>();
builder.Services.AddScoped<IActivityCreationService, ActivityCreationService>();
builder.Services.AddScoped<IActivityEditingService, ActivityEditingService>();
builder.Services.AddScoped<IMapCatalogService, MapCatalogService>();
builder.Services.AddScoped<IStrategyListService, StrategyListService>();

if (builder.Environment.IsProduction())
{
    builder.Services.AddOptions<BrevoEmailOptions>().Bind(builder.Configuration.GetSection(BrevoEmailOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();

    builder.Services.AddHttpClient<IEmailService, BrevoEmailService>(httpClient =>
    {
        httpClient.BaseAddress = new Uri("https://api.brevo.com/");
        httpClient.Timeout = TimeSpan.FromSeconds(30);
    });
}
else
{
    builder.Services.AddSingleton<IEmailService, DevelopmentEmailService>();
}

bool isRailway = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RAILWAY_PROJECT_ID"));

var app = builder.Build();

if (app.Environment.IsProduction())
{
    await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
    ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    await context.Database.MigrateAsync();
}

app.UseForwardedHeaders();

app.UseMiddleware<CorrelationIdMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!isRailway)
{
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapHealthChecks("/health");

app.MapControllers();

var rootRouteDefaults = new
{
    controller = "Teams",
    action = "Entry"
};

app.MapControllerRoute(name: "root", pattern: "", defaults: rootRouteDefaults).WithMetadata(new SuppressLinkGenerationMetadata()).WithStaticAssets();
app.MapControllerRoute(name: "default", pattern: "{controller}/{action=Index}/{id?}").WithStaticAssets();

app.Run();

public partial class Program
{
}