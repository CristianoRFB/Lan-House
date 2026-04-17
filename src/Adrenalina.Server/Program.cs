using Adrenalina.Server;

var app = AdrenalinaServerBootstrap.BuildApplication(new AdrenalinaServerHostOptions
{
    Args = args
});

app.Run();
