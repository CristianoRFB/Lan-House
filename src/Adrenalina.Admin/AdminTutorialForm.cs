namespace Adrenalina.Admin;

public sealed class AdminTutorialForm : Form
{
    public AdminTutorialForm()
    {
        Text = "Tutorial do administrador";
        Width = 940;
        Height = 720;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(820, 620);
        FormBorderStyle = FormBorderStyle.Sizable;
        BackColor = Color.FromArgb(15, 20, 31);
        ForeColor = Color.White;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            ColumnCount = 1,
            RowCount = 3,
            BackColor = BackColor
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var headerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 96
        };

        headerPanel.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Text = "Primeiros passos do ADMIN",
            Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold),
            ForeColor = Color.White,
            Margin = new Padding(0, 0, 0, 10)
        });

        headerPanel.Controls.Add(new Label
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            MaximumSize = new Size(840, 0),
            Text = "Este guia cobre o aplicativo desktop do administrador e tambem o painel web. Sempre que precisar, reabra o tutorial nas configuracoes do app ou use o menu Tutorial dentro do painel.",
            Font = new Font("Segoe UI", 10f),
            ForeColor = Color.FromArgb(193, 204, 220)
        });

        var tutorialTextBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(19, 27, 43),
            ForeColor = Color.FromArgb(230, 236, 246),
            Font = new Font("Segoe UI", 10.5f),
            ScrollBars = RichTextBoxScrollBars.Vertical,
            DetectUrls = false,
            Text = BuildTutorialText()
        };

        var footerPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false
        };

        var closeButton = new Button
        {
            Text = "Concluir",
            AutoSize = true,
            Padding = new Padding(18, 8, 18, 8),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(52, 132, 84),
            ForeColor = Color.White,
            Margin = new Padding(10, 0, 0, 0)
        };
        closeButton.FlatAppearance.BorderSize = 0;
        closeButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };

        footerPanel.Controls.Add(closeButton);

        layout.Controls.Add(headerPanel, 0, 0);
        layout.Controls.Add(tutorialTextBox, 0, 1);
        layout.Controls.Add(footerPanel, 0, 2);

        Controls.Add(layout);
    }

    private static string BuildTutorialText()
    {
        return string.Join(
            Environment.NewLine,
            [
                "1. COMO COMECAR",
                "",
                "- Abra o Adrenalina.Admin no computador do administrador.",
                "- Clique em Iniciar servidor e abrir painel para iniciar o servidor local, preparar o banco SQLite e abrir o painel.",
                "- No topo do aplicativo voce sempre encontra dois enderecos importantes:",
                "  Painel local: usado pelo proprio administrador no computador principal.",
                "  Clientes na rede: endereco/IP que deve ser informado nas maquinas cliente.",
                "",
                "2. LOGIN INICIAL",
                "",
                "- No primeiro acesso ao painel web, use:",
                "  Login: admin",
                "  Senha: adrenalina123",
                "  PIN: 1234",
                "- Se o painel embutido nao abrir, use Abrir painel no navegador. O sistema continua funcionando normalmente.",
                "",
                "3. FUNCOES DO APP DESKTOP DO ADMIN",
                "",
                "- Iniciar servidor e abrir painel: sobe o servidor local, valida o banco e abre o painel quando voce quiser.",
                "- Abrir painel no navegador: usa o navegador padrao quando voce preferir ou quando o modo embutido nao estiver disponivel.",
                "- Copiar acesso inicial: copia login inicial, senha inicial e endereco para clientes.",
                "- Backup manual: gera um backup do banco local do sistema.",
                "- Configuracoes: permite reativar o tutorial e escolher abrir o painel direto no navegador.",
                "- Tutorial: reabre este passo a passo a qualquer momento.",
                "- Fechar sistema: mostra confirmacao para encerrar o aplicativo e, se quiser, gerar um backup final antes de sair.",
                "",
                "4. FLUXO RECOMENDADO DE OPERACAO",
                "",
                "- Abra o ADMIN, clique em Iniciar servidor e abrir painel e confirme o endereco exibido em Clientes na rede.",
                "- Abra o painel e entre com a conta de administrador.",
                "- Revise Configuracoes para nome da lan house, valores, mensagens e bloqueios.",
                "- Cadastre usuarios antes de liberar as primeiras maquinas, quando necessario.",
                "- Informe o IP do ADMIN nas maquinas cliente.",
                "- Acompanhe solicitacoes, sessoes e maquinas pelo painel web.",
                "",
                "5. O QUE EXISTE NO PAINEL WEB",
                "",
                "- Painel: mostra maquinas online, sessoes ativas, solicitacoes pendentes, uso recente e logs.",
                "- Usuarios: cadastra usuarios, define perfil, PIN, senha, saldo, limite de anotacao e observacoes.",
                "- Maquinas: acompanha status, processos recentes e permite enviar comandos remotos.",
                "- Sessoes: inicia, ajusta e encerra sessoes de PCs e consoles.",
                "- Relatorios: exporta arquivos e revisa solicitacoes e logs.",
                "- Configuracoes: define comportamento geral do sistema.",
                "- Tutorial: abre o guia completo dentro do proprio painel web.",
                "",
                "6. COMO USAR A TELA USUARIOS",
                "",
                "- Use Salvar usuario para criar ou atualizar contas.",
                "- Perfis disponiveis:",
                "  Admin: acesso total ao painel.",
                "  Especial: operacao administrativa com permissoes elevadas.",
                "  Ghost: uso livre sem cobranca normal.",
                "  Comum: cliente padrao com saldo, anotacao e tempo.",
                "- Use Lancamento financeiro para registrar creditos, debitos, anotacoes e promessas.",
                "",
                "7. COMO USAR A TELA MAQUINAS",
                "",
                "- Veja se a maquina esta online, em sessao, bloqueada ou offline.",
                "- Consulte hostname, IP, tipo da maquina e processos recentes.",
                "- Comandos rapidos disponiveis: bloquear tela, reiniciar, logout, limpar temporarios e outras acoes remotas.",
                "- Envie avisos personalizados quando precisar falar com uma maquina especifica.",
                "",
                "8. COMO USAR A TELA SESSOES",
                "",
                "- Escolha a maquina, o usuario e a quantidade de minutos.",
                "- Defina valor por hora, modo demo e se o cronometro deve aparecer no cliente.",
                "- Em sessoes ativas, use Ajustar para adicionar minutos ou anotacao.",
                "- Use Encerrar para finalizar a sessao quando necessario.",
                "",
                "9. COMO USAR RELATORIOS",
                "",
                "- Escolha o periodo e o formato do arquivo.",
                "- Gere exportacoes em TXT, Excel ou PDF.",
                "- Consulte tambem o log recente e as solicitacoes em aberto para auditoria.",
                "",
                "10. COMO USAR CONFIGURACOES",
                "",
                "- Ajuste nome da lan house, tema, modo de atualizacao e rotina de backup.",
                "- Defina valores padrao de PC e console.",
                "- Configure mensagem de boas-vindas, saida e bloqueio.",
                "- Revise lista branca e lista negra de programas.",
                "- Salve as configuracoes antes de colocar as maquinas em uso.",
                "",
                "11. COMO O CLIENTE ENTRA NO SISTEMA",
                "",
                "- Abra o Adrenalina.Client em cada maquina cliente.",
                "- No primeiro uso, informe o endereco mostrado no ADMIN em Clientes na rede.",
                "- Salve a configuracao da maquina.",
                "- O cliente faz login com usuario e PIN.",
                "- Pedidos de cadastro ou mais tempo chegam para aprovacao no painel do administrador.",
                "",
                "12. ONDE ENCONTRAR AJUDA DEPOIS",
                "",
                "- No app desktop: use Configuracoes ou Tutorial.",
                "- No painel web: use o menu Tutorial.",
                "- Para operacao diaria, comece pelo Painel e depois use Usuarios, Maquinas, Sessoes, Relatorios e Configuracoes conforme a necessidade."
            ]);
    }
}
