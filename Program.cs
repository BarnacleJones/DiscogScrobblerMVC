using Serilog;
using DiscogsApiClient;
using DiscogScrobblerMVC;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using DiscogScrobblerMVC.Data;
using DiscogScrobblerMVC.Data.Entities;
using DiscogScrobblerMVC.Models;
using DiscogScrobblerMVC.Services;
using DiscogScrobblerMVC.Services.Background;
using DiscogScrobblerMVC.Services.Discogs;
using DiscogScrobblerMVC.Services.Interfaces;
using DiscogScrobblerMVC.Services.LastFm;
using DiscogScrobblerMVC.Services.Queues;
using DiscogScrobblerMVC.Services.Settings;
using DiscogScrobblerMVC.Services.Utilities;


var builder = WebApplication.CreateBuilder(args);

// Secrets and machine-specific overrides (must load before ConnectionStrings / Serilog use config)
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

//Add logging
Log.Logger = new LoggerConfiguration()
    .Enrich.With(new LogDisplayTimeEnricher(builder.Configuration["App:LogTimeZoneId"]))
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();


// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlite(connectionString,
        sqlite => sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("App"));
builder.Services.Configure<LastFmOptions>(builder.Configuration.GetSection(LastFmOptions.SectionName));

if (builder.Configuration.GetValue("Hosting:TrustForwardedHeaders", false))
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache();

builder.Services.AddHttpClient();
builder.Services.AddScoped<ILastFmOAuthService, LastFmOAuthService>();

// Relaxed password rules and no email confirmation — fine for private/low-risk; tighten for public internet.
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 1;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddDiscogsApiClient(options =>
{
    options.UserAgent = "DiscogScrobblerMVC";
    options.ConsumerKey = builder.Configuration["Discogs:ConsumerKey"] ?? throw new InvalidOperationException("NO Discogs Consumer Key");
    options.ConsumerSecret = builder.Configuration["Discogs:ConsumerSecret"] ?? throw new InvalidOperationException("NO Discogs Consumer Secret");
});

builder.Services.AddSingleton<DiscogsExclusiveGate>();
builder.Services.AddScoped<IDiscogsService, DiscogsService>();
builder.Services.AddScoped<IReleaseService, ReleaseService>();
builder.Services.AddScoped<ICollectionService, CollectionService>();
builder.Services.AddScoped<ICollectionBrowseService, CollectionBrowseService>();
builder.Services.AddScoped<IArtistService, ArtistService>();
builder.Services.AddScoped<ILabelService, LabelService>();
builder.Services.AddScoped<IScrobbleService, ScrobbleService>();
builder.Services.AddScoped<ITrackService, TrackService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<ISettingsPageService, SettingsPageService>();
builder.Services.AddScoped<IAccountApprovalService, AccountApprovalService>();
builder.Services.AddHostedService<DiscogsBackgroundService>();
builder.Services.AddSingleton<IDiscogsSyncQueue, DiscogsSyncQueue>();
builder.Services.AddHostedService<DiscogsOnDemandSyncService>();
builder.Services.AddSingleton<IDiscogsMetadataRefreshQueue, DiscogsMetadataRefreshQueue>();
builder.Services.AddHostedService<DiscogsMetadataRefreshHostedService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (app.Configuration.GetValue("Hosting:TrustForwardedHeaders", false))
{
    app.UseForwardedHeaders();
}

app.UseHttpsRedirection();
var configuredImagePath = app.Configuration.GetValue<string>("App:ImageBasePath");
var resolvedImagePath = CoverStoragePathResolver.ResolveImageBasePath(app.Environment.ContentRootPath, configuredImagePath);
var imageSearchPaths = new[]
{
    resolvedImagePath,
    Path.Combine(Path.GetTempPath(), "DiscogScrobblerMVC", "images"),
};

var imageProviders = new List<IFileProvider>();
foreach (var imagePath in imageSearchPaths.Distinct(StringComparer.Ordinal))
{
    try
    {
        Directory.CreateDirectory(imagePath);
        imageProviders.Add(new PhysicalFileProvider(imagePath));
    }
    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
    {
        app.Logger.LogWarning(ex, "Could not enable local cover static files from {Path}.", imagePath);
    }
}

if (imageProviders.Count > 0)
{
    // Serve local cover assets with long-lived caching; filenames are stable per release.
    var fileProvider = imageProviders.Count == 1
        ? imageProviders[0]
        : new CompositeFileProvider(imageProviders);

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = fileProvider,
        RequestPath = "/images",
        OnPrepareResponse = ctx =>
        {
            const int oneYearInSeconds = 60 * 60 * 24 * 365;
            ctx.Context.Response.Headers.CacheControl = $"public,max-age={oneYearInSeconds},immutable";
        }
    });
}
else
{
    app.Logger.LogWarning("No local cover image directory is available; /images requests may fail.");
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// migrations on startup + ensure Identity roles exist
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await roleManager.RoleExistsAsync(AppRoles.Admin))
    {
        var created = await roleManager.CreateAsync(new IdentityRole(AppRoles.Admin));
        if (!created.Succeeded)
        {
            var errors = string.Join("; ", created.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Could not create {AppRoles.Admin} role: {errors}");
        }
    }
}

// Discogs owner APIs (collection sync, collection value): each user stores a personal access token via Settings.

app.Run();
