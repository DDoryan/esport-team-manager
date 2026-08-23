using EsportTeamManager.Infrastructure.Identity;
using EsportTeamManager.Infrastructure.Persistence;
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

var builder = WebApplication.CreateBuilder(args);

string connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("La chaîne de connexion est introuvable.");

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));

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

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IEmailService, DevelopmentEmailService>();
}

var app = builder.Build();

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