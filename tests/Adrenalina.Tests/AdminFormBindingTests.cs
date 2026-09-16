using Adrenalina.Server.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Adrenalina.Tests;

public sealed class AdminFormBindingTests
{
    [Theory]
    [InlineData(typeof(MachinesController), nameof(MachinesController.Save), "Form")]
    [InlineData(typeof(UsersController), nameof(UsersController.Save), "Form")]
    [InlineData(typeof(UsersController), nameof(UsersController.Ledger), "LedgerForm")]
    [InlineData(typeof(SettingsController), nameof(SettingsController.Save), "Form")]
    [InlineData(typeof(ReportsController), nameof(ReportsController.Export), "Filter")]
    [InlineData(typeof(SessionsController), nameof(SessionsController.Start), "StartForm")]
    public void Nested_form_actions_bind_the_prefix_rendered_by_the_view(Type controllerType, string actionName, string expectedPrefix)
    {
        var action = controllerType.GetMethod(actionName);
        Assert.NotNull(action);

        var requestParameter = action!.GetParameters()
            .Single(parameter => parameter.ParameterType != typeof(CancellationToken));
        var bind = requestParameter.GetCustomAttributes(typeof(BindAttribute), inherit: true)
            .Cast<BindAttribute>()
            .SingleOrDefault();

        Assert.NotNull(bind);
        Assert.Equal(expectedPrefix, bind!.Prefix);
    }
}
