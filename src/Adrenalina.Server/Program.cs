using Adrenalina.Server;

var app = AdrenalinaServerBootstrap.BuildApplication(new AdrenalinaServerHostOptions
{
    Args = args
});

await AdrenalinaServerBootstrap.InitializeAsync(app);
await app.RunAsync();
