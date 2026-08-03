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

            using var dashboard = await client.GetAsync("/dashboard");
            Assert.Equal(HttpStatusCode.Redirect, dashboard.StatusCode);
            Assert.Equal("/auth/login", dashboard.Headers.Location?.AbsolutePath);

            for (var attempt = 0; attempt < 10; attempt++)
            {
                using var loginAttempt = await client.PostAsJsonAsync("/api/client/login", new ClientLoginRequest
                {
                    MachineKey = "inexistente",
                    Login = "cliente",
                    Pin = "0000"
                });
                Assert.Equal(HttpStatusCode.OK, loginAttempt.StatusCode);
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
