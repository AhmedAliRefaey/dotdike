using Dike.Core;
using Dike.Infrastructure;
using Dike.Web.Authentication;
using Dike.Web.Localization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddSingleton<Microsoft.Extensions.Options.IConfigureOptions<Microsoft.AspNetCore.Mvc.MvcOptions>, LocalizedModelBindingMessages>();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture("en").AddSupportedCultures("en").AddSupportedUICultures("en", "ar");
    options.RequestCultureProviders = [new CookieRequestCultureProvider()];
    options.ApplyCurrentCultureToResponseHeaders = true;
});
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Accounts", "Administrator");
    options.Conventions.AuthorizeFolder("/Company", "Administrator");
    options.Conventions.AuthorizePage("/Auth/ChangePassword");
}).AddMvcOptions(options =>
{
    options.Filters.Add<LocalizedPageFilter>();
    options.Filters.Add<CompanyConnectionFilter>();
}).AddDataAnnotationsLocalization(options =>
    options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(UiText)));
var identityPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(identityPath);
builder.Services.AddDbContext<CompanyIdentityDbContext>(options => options.UseSqlite(
    builder.Configuration.GetConnectionString("Identity") ?? $"Data Source={Path.Combine(identityPath, "identity.db")}"));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 12;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<CompanyIdentityDbContext>().AddDefaultTokenProviders()
    .AddErrorDescriber<LocalizedIdentityErrors>();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Auth/Login";
    options.AccessDeniedPath = "/Auth/AccessDenied";
    options.Cookie.Name = "Dike.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(1);
    options.SlidingExpiration = false;
});
builder.Services.AddAuthorization(options =>
{
    foreach (var role in new[] { "Administrator", "Accountant", "Controller", "Viewer" })
        options.AddPolicy(role, policy => policy.RequireAuthenticatedUser().RequireRole(role));
});
builder.Services.AddScoped<INetSuiteReader, UnconfiguredNetSuiteReader>();
builder.Services.AddScoped<CurrentCompany>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<StaffService>();
builder.Services.AddScoped<CompanyAdministrator>();
builder.Services.AddScoped<CompanyOdoo>();
builder.Services.AddSingleton<CompanyOdooStore>();
builder.Services.AddSingleton<IOdooClientFactory, OdooClientFactory>();
builder.Services.AddSingleton<CompanyNetSuite>();
builder.Services.AddHttpClient("NetSuite", client => client.Timeout = TimeSpan.FromSeconds(90))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
    .RemoveAllLoggers();
builder.Services.AddDbContext<DikeDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("Dike")));
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    Guid? legacyOdooCompany = app.Environment.IsDevelopment() && Guid.TryParse(app.Configuration["Odoo:LocalCompanyId"], out var boundCompany)
        ? boundCompany : null;
    await IdentitySchema.InitializeAsync(scope.ServiceProvider.GetRequiredService<CompanyIdentityDbContext>(), legacyOdooCompany);
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Administrator", "Accountant", "Controller", "Viewer" })
        if (!await roles.RoleExistsAsync(role))
        {
            var result = await roles.CreateAsync(new IdentityRole(role));
            if (!result.Succeeded) throw new InvalidOperationException("Could not initialize company roles.");
        }
}
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Error"); app.UseHsts(); app.UseHttpsRedirection(); }
app.UseStaticFiles();
app.UseRequestLocalization();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapGet("/health", () => Results.Ok(new { status = "alive" }));
app.Run();
