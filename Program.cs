using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using SFCDashboardMobile.Authentication;
using SFCDashboardMobile.Data;
using SFCDashboardMobile.Middleware;
using SFCDashboardMobile.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())); // Add this line

// First register PERecordSyncService as a singleton so it can be retrieved
builder.Services.AddSingleton<PERecordSyncService>();
// Then register it as a hosted service using the same instance
builder.Services.AddHostedService(provider => provider.GetRequiredService<PERecordSyncService>());
builder.Services.AddHostedService<OLAViolationService>();
// Load Azure AD Configuration
var azureAdConfig = builder.Configuration.GetSection("AzureAd");
var isDevelopment = builder.Environment.IsDevelopment();

if (!isDevelopment && !string.IsNullOrEmpty(azureAdConfig["ClientId"]))
{
    // Use Azure AD authentication in production
    builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(azureAdConfig);
}
else
{
    // Use a dummy authentication scheme in development to prevent errors
    builder.Services.AddAuthentication("DummyScheme")
        .AddScheme<AuthenticationSchemeOptions, DummyAuthenticationHandler>("DummyScheme", options => { });
}

// Add MVC Controllers with Conditional Authentication
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});

// Add Razor Pages
builder.Services.AddRazorPages()
    .AddMicrosoftIdentityUI();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Configure middleware
if (!isDevelopment)
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication(); // Must be called, even in development mode
app.UseAuthorization();


app.UseUserRegistration();

//app.MapStaticAssets();
//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller=PlannedEvents}/{action=Index}/{id?}")
//    .WithStaticAssets();
//app.MapRazorPages()
//   .WithStaticAssets();

app.Run();