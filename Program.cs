using System.Net.Http.Headers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using DiscogScrobblerMVC.Data;
using DiscogScrobblerMVC.Services;
using Hqub.Lastfm;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddHttpClient<IDiscogsService, DiscogsService>(client =>
{
    client.BaseAddress = new Uri("https://api.discogs.com/");
    // Required by Discogs — must identify your app
    client.DefaultRequestHeaders.UserAgent
        .ParseAdd("DiscogScrobblerMVC/1.0 +https://yourdomain.com");
    // Personal token from appsettings
    var token = builder.Configuration["Discogs:PersonalToken"];
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Discogs", $"token={token}");
});

builder.Services.AddSingleton(sp =>
    new LastfmClient(
        builder.Configuration["LastFm:ApiKey"]!,
        builder.Configuration["LastFm:ApiSecret"]!
    )
);
builder.Services.AddScoped<ILastFmService, LastFmService>();

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
