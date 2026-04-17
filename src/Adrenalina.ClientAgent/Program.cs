using Adrenalina.ClientAgent;
using Adrenalina.Infrastructure;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "Adrenalina Client Agent");
builder.Services.Configure<ClientAgentOptions>(builder.Configuration.GetSection("ClientAgent"));
builder.Services.AddAdrenalinaClientRuntimeStore(builder.Configuration);
builder.Services.AddHttpClient("adrenalina-server", (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<ClientAgentOptions>>().Value;
    client.BaseAddress = new Uri(options.ServerBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.SyncIntervalSeconds));
});
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
