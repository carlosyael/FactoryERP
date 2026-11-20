using System.Text;
using System.Threading.RateLimiting;
using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Infrastructure.Data;
using FactoryERP.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace FactoryERP.Web.Extensions;

public static class SecuritySetupExtensions
{
    public static IServiceCollection AddFactoryErpSecurity(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        string? keysPath = Environment.GetEnvironmentVariable("DATA_PROTECTION_KEYS_PATH");
        var dataProtection = services.AddDataProtection().SetApplicationName("FactoryERP");
        if (!string.IsNullOrWhiteSpace(keysPath))
        {
            Directory.CreateDirectory(keysPath);
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }
        else
        {
            dataProtection.PersistKeysToDbContext<AppDbContext>();
        }

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(20);
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders()
        .AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/login";
            options.AccessDeniedPath = "/login";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromHours(10);
            options.SlidingExpiration = true;
        });

        ConfigureJwt(services, configuration, environment);
        ConfigureRateLimiting(services);

        services.AddAuthorization(options =>
        {
            foreach (string permission in Permissions.GetAll())
            {
                options.AddPolicy(permission, policy => policy.RequireClaim("Permission", permission));
            }
        });
        services.AddCascadingAuthenticationState();
        return services;
    }

    public static IApplicationBuilder UseOwaspSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
            context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
            context.Response.Headers.Append("Content-Security-Policy",
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
                "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com; " +
                "font-src 'self' data: https://cdn.jsdelivr.net https://fonts.gstatic.com; " +
                "img-src 'self' data: blob: https:; " +
                "connect-src 'self' wss: ws:; " +
                "frame-ancestors 'self';");
            await next();
        });
    }

    private static void ConfigureJwt(IServiceCollection services, IConfiguration configuration, IWebHostEnvironment env)
    {
        var jwt = configuration.GetSection("JwtSettings");
        string? secret = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? jwt["SecretKey"];
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException("JWT_SECRET_KEY debe configurarse con al menos 32 bytes.");
        }

        services.AddAuthentication().AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.RequireHttpsMetadata = !env.IsDevelopment();
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                ValidateIssuer = true,
                ValidIssuer = jwt["Issuer"] ?? "FactoryErpApi",
                ValidateAudience = true,
                ValidAudience = jwt["Audience"] ?? "FactoryErpClients",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });
    }

    private static void ConfigureRateLimiting(IServiceCollection services)
    {
        static string ClientIp(HttpContext ctx) =>
            ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded) && !string.IsNullOrWhiteSpace(forwarded)
                ? forwarded.ToString().Split(',')[0].Trim()
                : ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        services.AddRateLimiter(options =>
        {
            options.AddPolicy("LoginLimiter", ctx => RateLimitPartition.GetFixedWindowLimiter(
                ClientIp(ctx), _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));
            options.AddPolicy("ApiLimiter", ctx => RateLimitPartition.GetFixedWindowLimiter(
                ClientIp(ctx), _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 2 }));
            options.OnRejected = async (ctx, token) =>
            {
                ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await ctx.HttpContext.Response.WriteAsync("Demasiadas solicitudes. Intente nuevamente en un minuto.", token);
            };
        });
    }
}
