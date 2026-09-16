using Adrenalina.Application;
using Adrenalina.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.RateLimiting;
using System.Security.Cryptography.X509Certificates;
using Adrenalina.Server.Infrastructure;

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
            WebRootPath = options.WebRootPath ?? Path.Combine(options.ContentRootPath ?? AppContext.BaseDirectory, "wwwroot"),
            EnvironmentName = options.EnvironmentName
        };

        var builder = WebApplication.CreateBuilder(builderOptions);

        var certificatePath = builder.Configuration["Kestrel:Certificates:Default:Path"];
        var certificatePassword = builder.Configuration["Kestrel:Certificates:Default:Password"];
        var certificateThumbprint = options.CertificateThumbprint ?? builder.Configuration["Kestrel:Certificates:Default:Thumbprint"];
        var configuredUrls = options.Urls ?? builder.Configuration["urls"] ?? builder.Configuration["ASPNETCORE_URLS"];
        var usesHttps = configuredUrls?.Contains("https://", StringComparison.OrdinalIgnoreCase) == true;
        var listensOnAllInterfaces = configuredUrls?.Contains("0.0.0.0", StringComparison.OrdinalIgnoreCase) == true ||
                                     configuredUrls?.Contains('*') == true ||
                                     configuredUrls?.Contains('+') == true;
        if (usesHttps && string.IsNullOrWhiteSpace(certificatePath) && string.IsNullOrWhiteSpace(certificateThumbprint))
        {
            throw new InvalidOperationException(
                "Um binding HTTPS exige Kestrel:Certificates:Default:Path ou Thumbprint configurado fora do repositorio.");
        }

        if (listensOnAllInterfaces && usesHttps is false && !string.Equals(options.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(builder.Environment.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "A exposição na LAN exige HTTPS em produção. Configure um certificado antes de habilitar a rede local.");
        }

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

        if (!string.IsNullOrWhiteSpace(configuredUrls))
        {
            builder.WebHost.UseUrls(configuredUrls);
            if (!listensOnAllInterfaces)
            {
                builder.Configuration["AllowedHosts"] = "localhost;127.0.0.1";
            }
            else
            {
                // A LAN só é habilitada por opt-in explícito do Admin.
                builder.Configuration["AllowedHosts"] = "*";
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
                cookieOptions.ExpireTimeSpan = TimeSpan.FromHours(8);
            });

        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<MachineReplayGuard>();
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
            rateLimiterOptions.AddPolicy("client-pairing", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "local",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 12,
                        Window = TimeSpan.FromMinutes(10),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });
        builder.Services.AddControllersWithViews();
        builder.Services.AddAdrenalinaServerPlatform(builder.Configuration, builder.Environment);

        builder.WebHost.ConfigureKestrel(kestrelOptions =>
        {
            if (!string.IsNullOrWhiteSpace(certificatePath))
            {
                kestrelOptions.ConfigureHttpsDefaults(httpsOptions =>
                {
                    httpsOptions.ServerCertificate = new X509Certificate2(
                        certificatePath,
                        certificatePassword);
                });
            }
            else if (!string.IsNullOrWhiteSpace(certificateThumbprint))
            {
                var certificate = FindCertificate(certificateThumbprint);
                kestrelOptions.ConfigureHttpsDefaults(httpsOptions => httpsOptions.ServerCertificate = certificate);
            }

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
        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            await next();
        });
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            service = "Adrenalina.Server",
            timestampUtc = DateTime.UtcNow
        })).AllowAnonymous();

        app.MapGet("/health/ready", async (ICafeManagementService service, CancellationToken cancellationToken) =>
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            try
            {
                var integrity = await service.CheckDatabaseIntegrityAsync(timeout.Token);
                return integrity.Healthy
                    ? Results.Ok(new { status = "ready", service = "Adrenalina.Server", timestampUtc = DateTime.UtcNow })
                    : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
            catch
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }).AllowAnonymous();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Dashboard}/{action=Index}/{id?}");

        return app;
    }

    private static X509Certificate2 FindCertificate(string thumbprint)
    {
        var normalized = thumbprint.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();
        foreach (var location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
        {
            using var store = new X509Store(StoreName.My, location);
            store.Open(OpenFlags.ReadOnly);
            var certificate = store.Certificates
                .Find(X509FindType.FindByThumbprint, normalized, validOnly: false)
                .OfType<X509Certificate2>()
                .FirstOrDefault();
            if (certificate is not null)
            {
                return certificate;
            }
        }

        throw new InvalidOperationException(
            $"O certificado HTTPS com thumbprint '{normalized}' não foi encontrado em CurrentUser\\My ou LocalMachine\\My.");
    }

    public static async Task InitializeAsync(WebApplication app, CancellationToken cancellationToken = default)
    {
        using var scope = app.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICafeManagementService>();
        await service.EnsureInitializedAsync(cancellationToken);
    }
}
