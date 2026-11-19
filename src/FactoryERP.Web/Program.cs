using System.Globalization;
using CurrieTechnologies.Razor.SweetAlert2;
using FactoryERP.Core.Entities;
using FactoryERP.Infrastructure.Data;
using FactoryERP.Web.Components;
using FactoryERP.Web.Extensions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

var dominicanCulture = new CultureInfo("es-DO");
dominicanCulture.NumberFormat.CurrencySymbol = "RD$";
dominicanCulture.NumberFormat.CurrencyDecimalSeparator = ".";
dominicanCulture.NumberFormat.CurrencyGroupSeparator = ",";
dominicanCulture.NumberFormat.NumberDecimalSeparator = ".";
dominicanCulture.NumberFormat.NumberGroupSeparator = ",";
dominicanCulture.NumberFormat.CurrencyPositivePattern = 2;
dominicanCulture.NumberFormat.CurrencyNegativePattern = 12;

CultureInfo.DefaultThreadCurrentCulture = dominicanCulture;
CultureInfo.DefaultThreadCurrentUICulture = dominicanCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddSweetAlert2();
builder.Services.AddFactoryErpCoreServices(builder.Configuration);
builder.Services.AddFactoryErpSecurity(builder.Configuration, builder.Environment);
builder.Services.AddRazorComponents().AddInteractiveServerComponents(options =>
{
    options.DetailedErrors = builder.Environment.IsDevelopment();
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(5);
});

var app = builder.Build();
app.UseForwardedHeaders();
app.UseOwaspSecurityHeaders();

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(dominicanCulture),
    SupportedCultures = [dominicanCulture],
    SupportedUICultures = [dominicanCulture]
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.MapStaticAssets();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.MapGet("/health", async (AppDbContext db) =>
{
    bool dbOk = await db.Database.CanConnectAsync();
    return dbOk
        ? Results.Ok(new { status = "healthy", service = "FactoryERP", database = "connected", timestamp = DateTime.UtcNow })
        : Results.Json(new { status = "unhealthy", service = "FactoryERP", database = "disconnected" }, statusCode: 503);
}).AllowAnonymous();

app.MapPost("/login", async (
    HttpContext httpContext,
    [FromForm] string email,
    [FromForm] string password,
    SignInManager<ApplicationUser> signInManager,
    IAntiforgery antiforgery,
    ILogger<Program> logger) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(httpContext);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.LocalRedirect("/login?error=InvalidCredentials");
    }

    var user = await signInManager.UserManager.FindByEmailAsync(email);
    if (user is { IsActive: false })
    {
        return Results.LocalRedirect("/login?error=InactiveUser");
    }

    var result = await signInManager.PasswordSignInAsync(email, password, isPersistent: true, lockoutOnFailure: true);
    if (result.Succeeded)
    {
        logger.LogInformation("Inicio de sesión exitoso para {Email}.", email);
        return Results.LocalRedirect("/");
    }

    return result.IsLockedOut
        ? Results.LocalRedirect("/login?error=AccountLocked")
        : Results.LocalRedirect("/login?error=InvalidCredentials");
}).RequireRateLimiting("LoginLimiter").DisableAntiforgery();

app.MapPost("/logout", async (
    HttpContext httpContext,
    IAntiforgery antiforgery,
    SignInManager<ApplicationUser> signInManager) =>
{
    await antiforgery.ValidateRequestAsync(httpContext);
    await signInManager.SignOutAsync();
    return Results.LocalRedirect("/login");
});

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var context = services.GetRequiredService<AppDbContext>();

    for (int attempt = 1; attempt <= 8; attempt++)
    {
        try
        {
            await DbInitializer.InitializeAsync(services, context);
            logger.LogInformation("Base de datos FactoryERP inicializada correctamente.");
            break;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Intento {Attempt}/8 de inicializar BD falló.", attempt);
            if (attempt < 8) await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}

app.Run();
