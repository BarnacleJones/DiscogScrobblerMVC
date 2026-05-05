using Serilog;
using DiscogsApiClient;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using DiscogScrobblerMVC.Data;
using DiscogScrobblerMVC.Services;
using Hqub.Lastfm;
using Microsoft.Extensions.DependencyInjection.Extensions;


var builder = WebApplication.CreateBuilder(args);

//Add logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();


// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

//Local settings with API secrets not checked in
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddDiscogsApiClient(options =>
{
    options.UserAgent = "DiscogScrobblerMVC";
    options.ConsumerKey = builder.Configuration["Discogs:ConsumerKey"] ?? throw new InvalidOperationException("NO Discogs Consumer Key");
    options.ConsumerSecret = builder.Configuration["Discogs:ConsumerSecret"] ?? throw new InvalidOperationException("NO Discogs Consumer Secret");
});

builder.Services.AddScoped<IDiscogsService, DiscogsService>();
builder.Services.AddHostedService<DiscogsBackgroundService>();
// builder.Services.AddSingleton(sp =>
//     new LastfmClient(
//         builder.Configuration["LastFm:ApiKey"]!,
//         builder.Configuration["LastFm:ApiSecret"]!
//     )
// );
// builder.Services.AddScoped<ILastFmService, LastFmService>();

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
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
