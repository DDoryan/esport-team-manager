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
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

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

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.RequireHeaderSymmetry = true;
    options.ForwardedForHeaderName = "X-Real-IP";
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("100.64.0.0/10"));
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("fd12::/16"));
});

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IEmailConfirmationLinkFactory, EmailConfirmationLinkFactory>();
builder.Services.AddScoped<IAccountEmailConfirmationService, AccountEmailConfirmationService>();
builder.Services.AddScoped<IUnconfirmedAccountCleanupService, UnconfirmedAccountCleanupService>();
builder.Services.AddHostedService<UnconfirmedAccountCleanupBackgroundService>();
builder.Services.AddScoped<IAccountRegistrationService, AccountRegistrationService>();
builder.Services.AddScoped<IAccountAuthenticationService, AccountAuthenticationService>();
builder.Services.AddScoped<IUserTeamService, UserTeamService>();
builder.Services.AddScoped<IActivityCalendarService, ActivityCalendarService>();
builder.Services.AddScoped<IActivityCreationService, ActivityCreationService>();
builder.Services.AddScoped<IActivityEditingService, ActivityEditingService>();

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

app.UseHttpsRedirection();

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