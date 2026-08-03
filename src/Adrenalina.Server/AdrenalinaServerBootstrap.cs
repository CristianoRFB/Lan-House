using Adrenalina.Application;
using Adrenalina.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.RateLimiting;

namespace Adrenalina.Server;

public static class AdrenalinaServerBootstrap
{
    public static WebApplication BuildApplication(AdrenalinaServerHostOptions? options = null)
    {
        options ??= new AdrenalinaServerHostOptions();

        var builderOptions = new WebApplicationOptions
        {
            Args = options.Args,
            ApplicationName = typeof(AdrenalinaServerBootstrap).Assembly.FullName,
            ContentRootPath = options.ContentRootPath ?? AppContext.BaseDirectory,
            WebRootPath = options.WebRootPath ?? Path.Combine(options.ContentRootPath ?? AppContext.BaseDirectory, "wwwroot")
        };

        var builder = WebApplication.CreateBuilder(builderOptions);

        // O servidor embutido precisa rodar sem depender de acesso ao Event Log do Windows.
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole();
        builder.Logging.AddDebug();

        var configuredDataRoot = options.DataRootPath ?? builder.Configuration["Adrenalina:RootDirectory"];
        var serverDataRoot = string.IsNullOrWhiteSpace(configuredDataRoot)
            ? AdrenalinaPaths.GetAdminDataRoot()
            : configuredDataRoot;
        builder.Logging.AddProvider(new RollingFileLoggerProvider(Path.Combine(serverDataRoot, "logs", "Server.log")));

        if (!string.IsNullOrWhiteSpace(options.DataRootPath))
        {
            builder.Configuration["Adrenalina:RootDirectory"] = options.DataRootPath;
        }

        if (!string.IsNullOrWhiteSpace(options.Urls))
        {
            builder.WebHost.UseUrls(options.Urls);
            if (!options.Urls.Contains("0.0.0.0", StringComparison.OrdinalIgnoreCase) &&
                !options.Urls.Contains('*') && !options.Urls.Contains('+'))
            {
                builder.Configuration["AllowedHosts"] = "localhost;127.0.0.1";
            }
        }

        var dataProtectionRoot = options.DataRootPath ?? AdrenalinaPaths.GetAdminDataRoot();
        var dataProtectionDirectory = Path.Combine(dataProtectionRoot, "keys");
        Directory.CreateDirectory(dataProtectionDirectory);
        builder.Services.AddDataProtection()
            .SetApplicationName("Adrenalina.Admin")
            .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionDirectory));

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(cookieOptions =>
            {
                cookieOptions.LoginPath = "/auth/login";
                cookieOptions.AccessDeniedPath = "/auth/access-denied";
                cookieOptions.Cookie.Name = "Adrenalina.Admin";
                cookieOptions.Cookie.HttpOnly = true;
                cookieOptions.Cookie.SameSite = SameSiteMode.Strict;
                cookieOptions.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                cookieOptions.SlidingExpiration = true;
            });

        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(rateLimiterOptions =>
        {
            rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            rateLimiterOptions.AddPolicy("admin-login", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "local",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
            rateLimiterOptions.AddPolicy("client-api", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "local",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 120,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
            rateLimiterOptions.AddPolicy("client-login", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "local",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });
        builder.Services.AddControllersWithViews();
        builder.Services.AddAdrenalinaServerPlatform(builder.Configuration, builder.Environment);

        builder.WebHost.ConfigureKestrel(kestrelOptions =>
        {
            kestrelOptions.Limits.MaxRequestBodySize = 64 * 1024;
            kestrelOptions.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
            kestrelOptions.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(30);
        });

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/error");
            app.UseHsts();
        }

        if (options.UseHttpsRedirection)
        {
            app.UseHttpsRedirection();
        }

        app.UseStaticFiles();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            service = "Adrenalina.Server",
            timestampUtc = DateTime.UtcNow
        })).AllowAnonymous();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Dashboard}/{action=Index}/{id?}");

        return app;
    }

    public static async Task InitializeAsync(WebApplication app, CancellationToken cancellationToken = default)
    {
        using var scope = app.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICafeManagementService>();
        await service.EnsureInitializedAsync(cancellationToken);
    }
}
