using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Adrenalina.Agent;

public static class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Contains("--recover", StringComparer.OrdinalIgnoreCase))
        {
            WindowsPolicyEnforcer.RestoreNormalOnDisk();
            return;
        }

        if (args.Contains("--provision-secret", StringComparer.OrdinalIgnoreCase))
        {
            var secret = await Console.In.ReadToEndAsync();
            AgentSecretStore.Save(secret.Trim());
            return;
        }

        if (args.Contains("--provision-from-client", StringComparer.OrdinalIgnoreCase))
        {
            AgentSecretStore.ProvisionFromClientFile(
                GetArgument(args, "--client-credential-file"),
                GetArgument(args, "--credential-id"));
            return;
        }

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddWindowsService(options => options.ServiceName = "Adrenalina Client Agent");
        builder.Logging.AddEventLog(settings => settings.SourceName = "Adrenalina Client Agent");
        builder.Services.AddSingleton<WindowsPolicyEnforcer>();
        builder.Services.AddHostedService<AgentWorker>();
        await builder.Build().RunAsync();
    }

    private static string GetArgument(string[] args, string name)
    {
        var index = Array.FindIndex(args, value => string.Equals(value, name, StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            throw new ArgumentException($"Argumento obrigatório ausente: {name}.");
        }

        return args[index + 1];
    }
}
