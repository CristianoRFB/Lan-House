using Adrenalina.Server.ViewModels;

namespace Adrenalina.Server.Help;

public static class HelpContent
{
    public const int CurrentVersion = 1;

    public static HelpPageViewModel Build(bool isAdmin)
    {
        var articles = Articles
            .Where(article => article.AdminOnly is false || isAdmin)
            .ToArray();

        return new HelpPageViewModel
        {
            Version = CurrentVersion,
            Articles = articles,
            Categories = articles
                .Select(article => article.Category)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(category => category)
                .ToArray(),
            Checklist = ChecklistItems
                .Where(item => item.AdminOnly is false || isAdmin)
                .ToArray()
        };
    }

    private static readonly HelpArticleViewModel[] Articles =
    [
        new(
            "primeiros-passos",
            "Começando",
            "Como colocar o Adrenalina para funcionar",
            "Siga esta ordem no primeiro uso para preparar o servidor e conectar as máquinas.",
            [
                "Abra o Adrenalina ADMIN no computador que ficará como servidor.",
                "Clique em Iniciar servidor e abrir painel.",
                "Confirme o endereço exibido em Clientes na rede quando a LAN estiver habilitada.",
                "Abra o painel, use o acesso inicial e troque a senha do administrador.",
                "Cadastre as máquinas antes de preparar cada Adrenalina Client."
            ],
            ["primeiro uso", "servidor", "configurar", "conectar"],
            ["conectar-clientes", "login-inicial"]),
        new(
            "login-inicial",
            "Começando",
            "Como entrar pela primeira vez",
            "O acesso inicial usa admin admin e deve ser trocado logo no primeiro acesso.",
            [
                "Abra initial-admin-access.txt na pasta de dados do Admin.",
                "Use login, senha e PIN informados no arquivo; a senha inicial é admin admin.",
                "Se o arquivo não existir ou você perdeu a senha, volte à tela de login e clique em Recuperar acesso.",
                "A recuperação só funciona no próprio computador ADMIN, restaura admin admin e desbloqueia o admin.",
                "Depois de entrar, clique em Trocar senha no menu lateral e salve uma nova senha; o arquivo temporário é removido automaticamente."
            ],
            ["login", "senha", "pin", "acesso"],
            ["seguranca", "usuarios"]),
        new(
            "conectar-clientes",
            "Máquinas",
            "Como conectar uma máquina cliente",
            "O Client usa a URL do ADMIN e o pareamento aprovado para sincronizar automaticamente.",
            [
                "No ADMIN, abra Máquinas e cadastre nome, tipo e grupo.",
                "No Client, informe o endereço mostrado em Clientes na rede.",
                "No ADMIN, clique em INICIAR CONFIGURAÇÃO e copie o código temporário.",
                "No Client, clique em PAREAR CONFIGURAÇÃO, informe o código e aguarde aprovação.",
                "O Client continuará tentando sincronizar no intervalo configurado."
            ],
            ["client", "máquina", "lan", "rede", "conexão", "sincronização"],
            ["maquinas", "problemas-conexao"]),
        new(
            "painel",
            "Painel",
            "O que aparece no Painel",
            "O dashboard resume a operação local para você decidir onde agir primeiro.",
            [
                "Maquinas online mostra os clientes que enviaram contato recentemente.",
                "Maquinas em uso e Sessoes ativas mostram o uso em andamento.",
                "Solicitacoes reúne pedidos de cadastro e de mais tempo enviados pelos Clients.",
                "Os gráficos mostram uso por dia e por máquina; os logs ajudam a investigar eventos."
            ],
            ["dashboard", "painel", "indicadores", "resumo"],
            ["solicitacoes", "maquinas", "sessoes"]),
        new(
            "maquinas",
            "Máquinas",
            "Como cadastrar e acompanhar máquinas",
            "A tela Máquinas é o ponto de cadastro e acompanhamento dos computadores e consoles.",
            [
                "Preencha nome, chave única, tipo, grupo e observações. A chave deve ter de 16 a 100 caracteres; ela não é o código temporário de 6 dígitos.",
                "Use uma chave como pc-01-chave-segura. O código que vai para o Client aparece somente depois de clicar em INICIAR CONFIGURAÇÃO.",
                "Para editar, clique em Editar cadastro no cartão da máquina; os dados voltarão para o formulário acima.",
                "Use o status, hostname, IP e último contato para verificar a conexão.",
                "Envie um aviso quando precisar falar com uma máquina específica.",
                "Alternar a exibição do tempo muda apenas o que o Client mostra ao usuário.",
                "O Client não reinicia o Windows, encerra programas nem remove arquivos."
            ],
            ["máquinas", "pc", "console", "hostname", "ip", "offline"],
            ["conectar-clientes", "problemas-conexao"]),
        new(
            "sessoes",
            "Sessões",
            "Como iniciar, ajustar e encerrar uma sessão",
            "As sessões controlam o tempo de uso e o registro financeiro de cada máquina.",
            [
                "Escolha a máquina e, quando houver, o usuário cadastrado.",
                "Para um atendimento sem conta, use Uso livre / Ghost ou informe um nome livre.",
                "Defina minutos, valor por hora e se a cobrança será contabilizada.",
                "Em uma sessão ativa, use Ajustar para acrescentar minutos, anotação ou um motivo.",
                "Use Encerrar quando o atendimento terminar."
            ],
            ["sessão", "tempo", "cobrança", "minutos", "ajustar", "encerrar"],
            ["usuarios", "painel"]),
        new(
            "solicitacoes",
            "Sessões",
            "Como tratar solicitações dos Clients",
            "Pedidos de cadastro e de mais tempo ficam pendentes até uma decisão do operador.",
            [
                "Revise a máquina, o nome, o login e a mensagem enviados pelo Client.",
                "Clique em Aprovar quando os dados estiverem corretos.",
                "Clique em Rejeitar e explique o motivo quando não puder atender.",
                "A decisão fica registrada no painel e no histórico do sistema."
            ],
            ["solicitação", "cadastro", "mais tempo", "aprovar", "rejeitar"],
            ["sessoes", "relatorios"]),
        new(
            "usuarios",
            "Usuários e financeiro",
            "Como administrar usuários e lançamentos",
            "Use esta área para controlar perfis, acesso ao painel, saldo e anotações.",
            [
                "Informe nome, login e PIN de quatro dígitos para o acesso no Client.",
                "Perfis Admin e Especial têm acesso ao painel; Ghost e Comum são perfis de uso.",
                "Ao editar uma conta, deixe PIN ou senha vazios para manter o valor atual.",
                "Use bloqueio ou conta temporária quando o acesso precisar de limite.",
                "Registre créditos, débitos, anotações e promessas em Lançamento financeiro."
            ],
            ["usuário", "perfil", "admin", "especial", "ghost", "comum", "saldo", "anotação"],
            ["seguranca", "sessoes"],
            AdminOnly: true),
        new(
            "relatorios",
            "Relatórios",
            "Como exportar e auditar informações",
            "Relatórios ajudam no fechamento e na investigação de eventos recentes.",
            [
                "Escolha as datas de início e fim.",
                "Selecione TXT, Excel ou PDF e clique em Gerar arquivo.",
                "Consulte solicitações em aberto antes de encerrar o turno.",
                "Use o log recente para conferir alterações, eventos e endereços IP."
            ],
            ["relatório", "exportar", "excel", "pdf", "txt", "auditoria", "log"],
            ["solicitacoes", "seguranca"],
            AdminOnly: true),
        new(
            "configuracoes",
            "Configurações",
            "O que pode ser configurado",
            "As configurações definem valores padrão, mensagens mostradas no Client e retenção de backups.",
            [
                "Defina o nome da lan house e a retenção dos backups manuais.",
                "Revise valores padrão de PC e console e o limite de anotação.",
                "Personalize as mensagens de boas-vindas, saída e bloqueio.",
                "Salve antes de liberar novas sessões."
            ],
            ["configuração", "valor", "mensagem", "backup", "retenção"],
            ["primeiros-passos", "seguranca"],
            AdminOnly: true),
        new(
            "seguranca",
            "Segurança",
            "Cuidados básicos de segurança",
            "A operação é local, mas as credenciais continuam sendo responsabilidade da equipe.",
            [
                "Não compartilhe a senha do administrador entre funcionários.",
                "Crie um usuário separado para cada pessoa que usa o painel.",
                "Escolha o perfil mínimo necessário para cada função.",
                "Saia da conta ao usar um computador compartilhado.",
                "Nunca compartilhe arquivos de credenciais, chaves ou dados de acesso."
            ],
            ["segurança", "senha", "permissão", "credencial", "privacidade"],
            ["usuarios", "problemas-conexao"]),
        new(
            "problemas-conexao",
            "Erros e problemas",
            "O que fazer quando o Client fica offline",
            "Sem Server, a estação sem sessão permanece bloqueada; o Client tenta sincronizar novamente.",
            [
                "Confira se o ADMIN está aberto e se o servidor está ativo.",
                "Confirme se a URL usa o endereço da rede local, não localhost.",
                "Verifique se o pareamento foi aprovado e se o Agent está instalado e saudável.",
                "Use Testar conexão nas configurações do Client.",
                "Se persistir, confira firewall, certificado HTTPS quando a LAN estiver em produção e o log do servidor."
            ],
            ["offline", "erro", "conexão", "firewall", "rede", "sincronizar"],
            ["conectar-clientes", "maquinas", "seguranca"])
    ];

    private static readonly HelpChecklistItemViewModel[] ChecklistItems =
    [
        new("admin-server", "Iniciar o servidor no app ADMIN", "primeiros-passos"),
        new("admin-login", "Entrar no painel e trocar a senha inicial", "login-inicial"),
        new("admin-settings", "Revisar nome, valores e mensagens", "configuracoes", AdminOnly: true),
        new("admin-machine", "Cadastrar a primeira máquina", "maquinas"),
        new("admin-client", "Conectar e testar um Client", "conectar-clientes"),
        new("admin-user", "Cadastrar um usuário ou validar uso livre", "usuarios", AdminOnly: true),
        new("admin-session", "Iniciar uma sessão de teste", "sessoes"),
        new("admin-report", "Conferir relatório e log", "relatorios", AdminOnly: true)
    ];
}
