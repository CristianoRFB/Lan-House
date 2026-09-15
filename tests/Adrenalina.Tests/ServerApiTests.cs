using System.Net;
using System.Net.Http.Json;
using Adrenalina.Application;
using Adrenalina.Server;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Adrenalina.Tests;

public sealed class ServerApiTests
{
    [Fact]
    public void ProductionLanBindingRejectsPlainHttp()
    {
        var root = Path.Combine(Path.GetTempPath(), "Adrenalina.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "wwwroot"));

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                AdrenalinaServerBootstrap.BuildApplication(new AdrenalinaServerHostOptions
                {
                    ContentRootPath = root,
                    WebRootPath = Path.Combine(root, "wwwroot"),
                    DataRootPath = Path.Combine(root, "admin-data"),
                    Urls = "http://0.0.0.0:0",
                    EnvironmentName = "Production",
                    UseHttpsRedirection = false
                }));

            Assert.Contains("exposição na LAN exige HTTPS", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HealthIsAnonymousAndAdministrativeRoutesRequireAuthentication()
    {
        var root = Path.Combine(Path.GetTempPath(), "Adrenalina.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "wwwroot"));
        var app = AdrenalinaServerBootstrap.BuildApplication(new AdrenalinaServerHostOptions
        {
            ContentRootPath = root,
            WebRootPath = Path.Combine(root, "wwwroot"),
            DataRootPath = Path.Combine(root, "admin-data"),
            Urls = "http://127.0.0.1:0",
            EnvironmentName = "Production",
            UseHttpsRedirection = false
        });

        try
        {
            await AdrenalinaServerBootstrap.InitializeAsync(app);
            await app.StartAsync();
            var addresses = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()?.Addresses;
            var address = Assert.Single(addresses!);

            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            {
                BaseAddress = new Uri(address)
            };

            using var health = await client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, health.StatusCode);
            Assert.Contains("Adrenalina.Server", await health.Content.ReadAsStringAsync());
            Assert.Equal("nosniff", health.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("DENY", health.Headers.GetValues("X-Frame-Options").Single());
            Assert.Equal("no-referrer", health.Headers.GetValues("Referrer-Policy").Single());

            using var readiness = await client.GetAsync("/health/ready");
            Assert.Equal(HttpStatusCode.OK, readiness.StatusCode);
            Assert.Contains("ready", await readiness.Content.ReadAsStringAsync());

            using var dashboard = await client.GetAsync("/dashboard");
            Assert.Equal(HttpStatusCode.Redirect, dashboard.StatusCode);
            Assert.Equal("/auth/login", dashboard.Headers.Location?.AbsolutePath);

            using var missingProof = await client.PostAsJsonAsync("/api/client/login", new ClientLoginRequest
            {
                MachineKey = "inexistente",
                Login = "cliente",
                Pin = "0000"
            });
            Assert.Equal(HttpStatusCode.Unauthorized, missingProof.StatusCode);

            for (var attempt = 0; attempt < 9; attempt++)
            {
                var timestamp = DateTime.UtcNow;
                var nonce = Guid.NewGuid().ToString("N");
                using var loginAttempt = await client.PostAsJsonAsync("/api/client/login", new ClientLoginRequest
                {
                    MachineKey = "inexistente",
                    Login = "cliente",
                    Pin = "0000",
                    RequestTimestampUtc = timestamp,
                    Nonce = nonce,
                    MachineProof = MachineAuthentication.CreateProof("inexistente", timestamp, nonce, "login")
                });
                Assert.Equal(HttpStatusCode.Unauthorized, loginAttempt.StatusCode);
            }

            using var limitedAttempt = await client.PostAsJsonAsync("/api/client/login", new ClientLoginRequest
            {
                MachineKey = "inexistente",
                Login = "cliente",
                Pin = "0000"
            });
            Assert.Equal(HttpStatusCode.TooManyRequests, limitedAttempt.StatusCode);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
