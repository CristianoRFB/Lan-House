using Adrenalina.Server.Help;

namespace Adrenalina.Tests;

public sealed class HelpContentTests
{
    [Fact]
    public void HelpContent_SeparatesAdminOnlyArticlesAndChecklist()
    {
        var admin = HelpContent.Build(isAdmin: true);
        var operatorView = HelpContent.Build(isAdmin: false);

        Assert.Contains(admin.Articles, article => article.Id == "usuarios");
        Assert.Contains(admin.Articles, article => article.Id == "relatorios");
        Assert.DoesNotContain(operatorView.Articles, article => article.Id == "usuarios");
        Assert.DoesNotContain(operatorView.Articles, article => article.Id == "relatorios");
        Assert.True(admin.Checklist.Count > operatorView.Checklist.Count);
        Assert.All(operatorView.Checklist, item => Assert.False(item.AdminOnly));
    }

    [Fact]
    public void HelpContent_ContainsOnlyRealAdrenalinaWorkflows()
    {
        var help = HelpContent.Build(isAdmin: true);

        Assert.Contains(help.Articles, article => article.Id == "conectar-clientes");
        Assert.Contains(help.Articles, article => article.Id == "problemas-conexao");
        Assert.DoesNotContain(help.Articles, article => article.Title.Contains("delivery", StringComparison.OrdinalIgnoreCase));
        Assert.All(help.Articles, article => Assert.NotEmpty(article.Steps));
        Assert.All(help.Articles, article => Assert.All(article.Related, related => Assert.Contains(help.Articles, candidate => candidate.Id == related)));
    }
}
