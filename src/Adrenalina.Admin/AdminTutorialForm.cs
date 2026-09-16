namespace Adrenalina.Admin;

public sealed class AdminTutorialForm : Form
{
    private static readonly (string Title, string Body)[] Steps =
    [
        ("1. Inicie o ambiente", "Clique em Iniciar servidor e abrir painel para subir o servidor local, preparar o banco e abrir o painel web. O endereço em Clientes na rede é o que será usado pelos Clients."),
        ("2. Faça o primeiro acesso", "Abra initial-admin-access.txt na pasta de dados do Admin e use as credenciais geradas. Se ele não existir, abra o login e clique em Recuperar acesso neste computador ADMIN. Entre no painel e troque a senha imediatamente em Usuários."),
        ("3. Configure a operação", "Revise Configurações para nome da lan house, valores, mensagens e backups. Cadastre usuários quando necessário e crie as máquinas em Máquinas."),
        ("4. Conecte os Clients", "Em cada máquina, abra Adrenalina.Client, informe a URL do ADMIN e use PAREAR CONFIGURAÇÃO. Aprove a solicitação no painel; a sincronização passa a ser automática."),
        ("5. Opere no painel", "Use Painel para o resumo, Máquinas para conexão, Sessões para tempo de uso, Usuários para perfis e financeiro e Relatórios para exportações e auditoria."),
        ("6. Quando precisar de ajuda", "Reabra este guia pelo botão Tutorial ou pelas Configurações do app. No painel web, use Ajuda para buscar orientações, consultar o checklist e refazer o guia.")
    ];

    private readonly Label _stepLabel = new();
    private readonly Label _progressLabel = new();
    private readonly Button _backButton = new();
    private readonly Button _skipButton = new();
    private readonly Button _nextButton = new();
    private readonly Button _finishButton = new();
    private int _currentStep;

    public AdminTutorialForm()
    {
        Text = "Tutorial do administrador";
        Width = 900;
        Height = 620;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 520);
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
            Height = 86
        };
        headerPanel.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Text = "Primeiros passos do ADMIN",
            Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold),
            ForeColor = Color.White
        });
        _progressLabel.Dock = DockStyle.Bottom;
        _progressLabel.AutoSize = true;
        _progressLabel.ForeColor = Color.FromArgb(127, 217, 199);
        _progressLabel.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        headerPanel.Controls.Add(_progressLabel);

        var stepPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(22),
            BackColor = Color.FromArgb(19, 27, 43)
        };
        stepPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stepPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _stepLabel.Dock = DockStyle.Fill;
        _stepLabel.AutoSize = false;
        _stepLabel.Padding = new Padding(0, 0, 0, 14);
        _stepLabel.Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold);
        _stepLabel.ForeColor = Color.White;
        _stepLabel.MaximumSize = new Size(780, 0);
        stepPanel.Controls.Add(_stepLabel, 0, 0);

        var footerText = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            ForeColor = Color.FromArgb(193, 204, 220),
            Font = new Font("Segoe UI", 11f),
            Padding = new Padding(0, 4, 0, 0),
            MaximumSize = new Size(780, 0)
        };
        stepPanel.Controls.Add(footerText, 0, 1);

        var footerPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            Padding = new Padding(0, 16, 0, 0)
        };

        ConfigureButton(_finishButton, "Concluir", Color.FromArgb(52, 132, 84));
        ConfigureButton(_nextButton, "Próximo", Color.FromArgb(57, 96, 168));
        ConfigureButton(_skipButton, "Pular", Color.FromArgb(70, 84, 106));
        ConfigureButton(_backButton, "Voltar", Color.FromArgb(70, 84, 106));

        _finishButton.Click += (_, _) => CloseWithResult();
        _skipButton.Click += (_, _) => CloseWithResult();
        _backButton.Click += (_, _) =>
        {
            if (_currentStep > 0)
            {
                _currentStep--;
                UpdateStep(footerText);
            }
        };
        _nextButton.Click += (_, _) =>
        {
            if (_currentStep < Steps.Length - 1)
            {
                _currentStep++;
                UpdateStep(footerText);
            }
        };

        footerPanel.Controls.Add(_finishButton);
        footerPanel.Controls.Add(_nextButton);
        footerPanel.Controls.Add(_skipButton);
        footerPanel.Controls.Add(_backButton);

        layout.Controls.Add(headerPanel, 0, 0);
        layout.Controls.Add(stepPanel, 0, 1);
        layout.Controls.Add(footerPanel, 0, 2);
        Controls.Add(layout);

        Shown += (_, _) => UpdateStep(footerText);
    }

    private void UpdateStep(Label bodyLabel)
    {
        var step = Steps[_currentStep];
        _progressLabel.Text = $"Etapa {_currentStep + 1} de {Steps.Length}";
        _stepLabel.Text = step.Title;
        bodyLabel.Text = step.Body;
        _backButton.Visible = _currentStep > 0;
        _nextButton.Visible = _currentStep < Steps.Length - 1;
        _finishButton.Visible = _currentStep == Steps.Length - 1;
        (_currentStep == Steps.Length - 1 ? _finishButton : _nextButton).Focus();
    }

    private void CloseWithResult()
    {
        DialogResult = DialogResult.OK;
        Close();
    }

    private static void ConfigureButton(Button button, string text, Color backColor)
    {
        button.Text = text;
        button.AutoSize = true;
        button.Padding = new Padding(16, 8, 16, 8);
        button.Margin = new Padding(8, 0, 0, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = backColor;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        button.FlatAppearance.BorderSize = 0;
    }
}
