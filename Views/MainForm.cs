using System.Drawing;
using OSPVivoScraper.Models;
using OSPVivoScraper.Controllers;
using OSPVivoScraper.Services;
using Timer = System.Windows.Forms.Timer;

namespace OSPVivoScraper.Views;

public partial class MainForm : Form
{   
    // =========================================
    // UI COMPONENTS
    // =========================================
    
    private TextBox txtUsuario = null!;
    private TextBox txtSenha = null!;
    private Button btnMostrarSenha = null!;
    private CheckBox chkHeadless = null!;
    private Button btnIniciar = null!;
    private RichTextBox txtLogs = null!;
    private Label lblArquivo = null!;
    private Button btnSelecionarCsv = null!;
    private Button btnLimparCsv = null!;
    private ProgressBar barraProgresso = null!;
    private Label lblProgresso = null!;
    private Button btnLimparLogs = null!;
    private Button btnBaixarLogs = null!;
    private Button btnParar = null!;
    private Label lblEstimativa = null!;
    private Label lblConfigDir = null!;
    private Button btnLimparSessao = null!;
    private Button btnSalvar = null!;

    private RadioButton rbDraft = null!;
    private RadioButton rbMedicao = null!;
    private RadioButton rbCancelados = null!;
    private RadioButton rbMemoriaDraft = null!;
    private RadioButton rbMemoriaMedicao = null!;

    // StatusBar fields
    private StatusStrip? statusStrip = null!;
    private ToolStripStatusLabel? statusLabel = null!;
    private ToolStripStatusLabel? clockLabel = null!;  
    private System.Windows.Forms.Timer? clockTimer = null!;
    private System.Windows.Forms.Timer? _estimativaTimer;  


    // Evento de verificação de atualização
    private UpdateService? _updateService;
    private Timer? _updateCheckTimer;

    // =========================================
    // SERVICES & CONTROLLERS
    // =========================================
    
    private readonly ConfigService _configService;
    private readonly SessionService _sessionService;
    private readonly LogService _logService;
    private readonly ScraperController _controller;
    
    private string? _caminhoCsv;
    private CancellationTokenSource? _cts;
    private DateTime? _tempoInicio;
    private int _totalIds;
    
    // =========================================
    // CONSTRUCTOR
    // =========================================
    
    public MainForm()
    {
        // Inicializar serviços
        _configService = new ConfigService();
        _sessionService = new SessionService();
        _logService = new LogService();
        _controller = new ScraperController(_configService, _sessionService, _logService);
        
        // Assinar eventos
        _controller.OnLog += AdicionarLog;
        _controller.OnProgress += AtualizarProgresso;
        _controller.OnRunningStateChanged += OnRunningStateChanged;
        _controller.OnDataSaved += OnDataSaved;
        _controller.OnPartialDataSaved += OnPartialDataSaved;
        _controller.OnTimeUpdate += OnTimeUpdate;
        
        InitializeComponent();
        CarregarConfiguracoes();
        IniciarVerificacaoAtualizacoes();
        CriarBotaoSuporte();
        
        // Mostrar diretório de configurações
        AdicionarLog($"📁 Diretório de configurações: {_configService.ConfigDir}");
        lblConfigDir.Text = $"Config: {_configService.ConfigDir}";
    }

    private void InitializeComponent()
    {
        ConfigurarJanela();
        CriarInterface();
        CriarRodape();
        AdicionarTooltips();
    }

    // =========================================
    // UI CONFIGURATION
    // =========================================
    
    private void ConfigurarJanela()
    {
        Text = "OSP Vivo Web Scraper";
        Size = new Size(1000, 1040);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(245, 245, 245);
        Font = new Font("Segoe UI", 10);
        AutoScroll = true;
        
        //funçao janela fixa => FormBorderStyle = FormBorderStyle.FixedSingle;
        FormBorderStyle = FormBorderStyle.Sizable; // Permitir redimensionamento
        MaximizeBox = false;
        MinimizeBox = true;
        
        // Carregar ícone personalizado
        try
        {
            string iconPath = Path.Combine(Application.StartupPath, "Assets", "ico_osp.ico");
            if (File.Exists(iconPath))
            {
                Icon = new Icon(iconPath);
            }
            else
            {
                // Fallback para o ícone padrão do executável
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
        }
        catch
        {
            // Se falhar, continua sem ícone
        }
    }

    private void CriarInterface()
    {
        CriarTitulo();
        CriarConfiguracoes();
        CriarCsv();
        CriarModoExtracao();
        CriarProgresso();
        CriarLogs();
        CriarStatusBar();
    }

  private void CriarTitulo()
    {
        // Botão de suporte - primeiro para ficar atrás (ou usar BringToFront depois)
        var btnSuporte = new Button();
        btnSuporte.Text = "🆘 Suporte";
        btnSuporte.Size = new Size(120, 32);
        btnSuporte.Location = new Point(20, 15);
        btnSuporte.BackColor = Color.FromArgb(0, 120, 215);
        btnSuporte.ForeColor = Color.White;
        btnSuporte.FlatStyle = FlatStyle.Flat;
        btnSuporte.FlatAppearance.BorderSize = 0;
        btnSuporte.Cursor = Cursors.Hand;
        btnSuporte.Click += BtnSuporte_Click;
        Controls.Add(btnSuporte);
        
        // Versão (canto superior direito)
        var versao = new Label();
        versao.Text = "Versão: v2.1.0";
        versao.ForeColor = Color.Gray;
        versao.Font = new Font("Segoe UI", 10);
        versao.AutoSize = true;
        versao.Location = new Point(855, 15);
        Controls.Add(versao);
        
        // Criar um painel principal para centralizar tudo
        var tituloPanel = new Panel();
        tituloPanel.Size = new Size(940, 60);
        tituloPanel.Location = new Point(20, 10);
        tituloPanel.BackColor = Color.Transparent;
        
        // Criar um FlowLayoutPanel para centralizar logo + título
        var centralPanel = new FlowLayoutPanel();
        centralPanel.FlowDirection = FlowDirection.LeftToRight;
        centralPanel.AutoSize = true;
        centralPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        centralPanel.BackColor = Color.Transparent;
        
        // Ícone (imagem)
        var pictureBox = new PictureBox();
        pictureBox.Size = new Size(58, 58);
        pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        pictureBox.Margin = new Padding(0, 6, 10, 6);
        
        // Carregar a logo PNG
        try
        {
            string logoPath = Path.Combine(Application.StartupPath, "Assets", "osp.png");
            if (File.Exists(logoPath))
            {
                pictureBox.Image = Image.FromFile(logoPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao carregar logo: {ex.Message}");
        }
        
        // Título
        var titulo = new Label();
        titulo.Text = "OSP Vivo Web Scraper";
        titulo.Font = new Font("Segoe UI", 24, FontStyle.Bold);
        titulo.ForeColor = Color.FromArgb(64, 64, 64);
        titulo.AutoSize = true;
        titulo.Margin = new Padding(0, 6, 0, 6);
        
        centralPanel.Controls.Add(pictureBox);
        centralPanel.Controls.Add(titulo);
        
        // Calcular posição centralizada
        int larguraConteudo = centralPanel.PreferredSize.Width;
        int posX = (tituloPanel.Width - larguraConteudo) / 2;
        
        centralPanel.Location = new Point(posX, 0);
        tituloPanel.Controls.Add(centralPanel);
        Controls.Add(tituloPanel);
        
        // Trazer botão e versão para frente
        btnSuporte.BringToFront();
        versao.BringToFront();
        
        // Label para mostrar diretório de config
        lblConfigDir = new Label();
        lblConfigDir.Text = "";
        lblConfigDir.Font = new Font("Segoe UI", 8);
        lblConfigDir.ForeColor = Color.Gray;
        lblConfigDir.AutoSize = true;
        lblConfigDir.Location = new Point(20, 80);
        Controls.Add(lblConfigDir);
    }
    private void CriarConfiguracoes()
    {
        var grupo = new GroupBox();
        grupo.Text = "🔧 Configurações";
        grupo.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        grupo.Size = new Size(940, 170);
        grupo.Location = new Point(20, 100);
        Controls.Add(grupo);

        var lblUsuario = new Label();
        lblUsuario.Text = "👤 Usuário:";
        lblUsuario.Location = new Point(15, 35);
        lblUsuario.AutoSize = true;
        grupo.Controls.Add(lblUsuario);

        txtUsuario = new TextBox();
        txtUsuario.Size = new Size(800, 30);
        txtUsuario.Location = new Point(120, 30);
        txtUsuario.PlaceholderText = "Usuário do OSP Control";
        txtUsuario.Font = new Font("Segoe UI", 10);
        grupo.Controls.Add(txtUsuario);

        var lblSenha = new Label();
        lblSenha.Text = "🔒 Senha:";
        lblSenha.Location = new Point(15, 75);
        lblSenha.AutoSize = true;
        grupo.Controls.Add(lblSenha);

        txtSenha = new TextBox();
        txtSenha.Size = new Size(760, 30);
        txtSenha.Location = new Point(120, 70);
        txtSenha.UseSystemPasswordChar = true;
        txtSenha.PlaceholderText = "Senha do OSP Control";
        txtSenha.Font = new Font("Segoe UI", 10);
        grupo.Controls.Add(txtSenha);

        btnMostrarSenha = new Button();
        btnMostrarSenha.Text = "👁";
        btnMostrarSenha.Size = new Size(40, 30);
        btnMostrarSenha.Location = new Point(890, 70);
        btnMostrarSenha.Font = new Font("Segoe UI", 9);
        btnMostrarSenha.Click += BtnMostrarSenha_Click;
        grupo.Controls.Add(btnMostrarSenha);

        chkHeadless = new CheckBox();
        chkHeadless.Text = "👻 Executar em modo oculto (Headless)";
        chkHeadless.Location = new Point(20, 115);
        chkHeadless.AutoSize = true;
        chkHeadless.Font = new Font("Segoe UI", 9);
        grupo.Controls.Add(chkHeadless);

        // Botão Salvar Credenciais - AGORA como campo da classe
        btnSalvar = new Button();
        btnSalvar.Text = "💾 Salvar Credenciais";
        btnSalvar.Size = new Size(300, 35);
        btnSalvar.Location = new Point(580, 110);
        btnSalvar.BackColor = Color.FromArgb(0, 120, 215);
        btnSalvar.ForeColor = Color.White;
        btnSalvar.FlatStyle = FlatStyle.Flat;
        btnSalvar.FlatAppearance.BorderSize = 0;
        btnSalvar.Cursor = Cursors.Hand;
        btnSalvar.Click += BtnSalvar_Click;
        grupo.Controls.Add(btnSalvar);
        
        // Botão Limpar Sessão - AGORA como campo da classe
        btnLimparSessao = new Button();
        btnLimparSessao.Text = "🗑️ Limpar Sessão";
        btnLimparSessao.Size = new Size(230, 35);
        btnLimparSessao.Location = new Point(330, 110);
        btnLimparSessao.BackColor = Color.FromArgb(108, 117, 125);
        btnLimparSessao.ForeColor = Color.White;
        btnLimparSessao.FlatStyle = FlatStyle.Flat;
        btnLimparSessao.FlatAppearance.BorderSize = 0;
        btnLimparSessao.Cursor = Cursors.Hand;
        btnLimparSessao.Click += BtnLimparSessao_Click;
        grupo.Controls.Add(btnLimparSessao);
    }

    private void CriarCsv()
    {
        var grupo = new GroupBox();
        grupo.Text = "📁 Arquivo CSV";
        grupo.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        grupo.Size = new Size(940, 135);
        grupo.Location = new Point(20, 280);
        Controls.Add(grupo);

        lblArquivo = new Label();
        lblArquivo.Text = "📄 Nenhum arquivo selecionado";
        lblArquivo.Location = new Point(20, 30);
        lblArquivo.Size = new Size(850, 40);
        lblArquivo.BackColor = Color.FromArgb(240, 240, 240);
        lblArquivo.BorderStyle = BorderStyle.FixedSingle;
        lblArquivo.Padding = new Padding(5);
        grupo.Controls.Add(lblArquivo);

        btnSelecionarCsv = new Button();
        btnSelecionarCsv.Text = "📂 Selecionar CSV";
        btnSelecionarCsv.Size = new Size(200, 40);
        btnSelecionarCsv.Location = new Point(20, 80);
        btnSelecionarCsv.BackColor = Color.FromArgb(0, 120, 215);
        btnSelecionarCsv.ForeColor = Color.White;
        btnSelecionarCsv.FlatStyle = FlatStyle.Flat;
        btnSelecionarCsv.FlatAppearance.BorderSize = 0;
        btnSelecionarCsv.Cursor = Cursors.Hand;
        btnSelecionarCsv.Click += BtnSelecionarCsv_Click;
        grupo.Controls.Add(btnSelecionarCsv);

        btnLimparCsv = new Button();
        btnLimparCsv.Text = "🗑️ Limpar";
        btnLimparCsv.Size = new Size(200, 40);
        btnLimparCsv.Location = new Point(240, 80);
        btnLimparCsv.BackColor = Color.FromArgb(108, 117, 125);
        btnLimparCsv.ForeColor = Color.White;
        btnLimparCsv.FlatStyle = FlatStyle.Flat;
        btnLimparCsv.FlatAppearance.BorderSize = 0;
        btnLimparCsv.Cursor = Cursors.Hand;
        btnLimparCsv.Click += BtnLimparCsv_Click;
        grupo.Controls.Add(btnLimparCsv);
    }

    private void CriarModoExtracao()
    {
        var grupo = new GroupBox();
        grupo.Text = "🎯 Modo de Extração";
        grupo.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        grupo.Size = new Size(940, 150);
        grupo.Location = new Point(20, 425);
        Controls.Add(grupo);

        var tableLayout = new TableLayoutPanel();
        tableLayout.Size = new Size(900, 100);
        tableLayout.Location = new Point(20, 30);
        tableLayout.ColumnCount = 2;
        tableLayout.RowCount = 3;
        tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        
        rbDraft = new RadioButton();
        rbDraft.Text = "🧾 Draft";
        rbDraft.AutoSize = true;
        rbDraft.Checked = true;
        rbDraft.Font = new Font("Segoe UI", 10);
        tableLayout.Controls.Add(rbDraft, 0, 0);
        
        rbMemoriaDraft = new RadioButton();
        rbMemoriaDraft.Text = "🧮 Memória de Cálculo Draft";
        rbMemoriaDraft.AutoSize = true;
        rbMemoriaDraft.Font = new Font("Segoe UI", 10);
        tableLayout.Controls.Add(rbMemoriaDraft, 1, 0);
        
        rbMedicao = new RadioButton();
        rbMedicao.Text = "📊 Medição";
        rbMedicao.AutoSize = true;
        rbMedicao.Font = new Font("Segoe UI", 10);
        tableLayout.Controls.Add(rbMedicao, 0, 1);
        
        rbMemoriaMedicao = new RadioButton();
        rbMemoriaMedicao.Text = "🧮 Memória de Cálculo Medição";
        rbMemoriaMedicao.AutoSize = true;
        rbMemoriaMedicao.Font = new Font("Segoe UI", 10);
        tableLayout.Controls.Add(rbMemoriaMedicao, 1, 1);
        
        rbCancelados = new RadioButton();
        rbCancelados.Text = "🔎 ID Cancelados";
        rbCancelados.AutoSize = true;
        rbCancelados.Font = new Font("Segoe UI", 10);
        tableLayout.Controls.Add(rbCancelados, 0, 2);
        
        grupo.Controls.Add(tableLayout);
    }

    private void CriarProgresso()
    {
        var grupo = new GroupBox();
        grupo.Text = "📊 Progresso";
        grupo.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        grupo.Size = new Size(940, 90);
        grupo.Location = new Point(20, 585);
        Controls.Add(grupo);

        lblProgresso = new Label();
        lblProgresso.Text = "Aguardando início...";
        lblProgresso.Location = new Point(20, 25);
        lblProgresso.AutoSize = true;
        lblProgresso.Font = new Font("Segoe UI", 9);
        grupo.Controls.Add(lblProgresso);

        lblEstimativa = new Label();
        lblEstimativa.Text = "⏱️ Estimativa: Aguardando início...";
        lblEstimativa.Location = new Point(280, 25);
        lblEstimativa.Size = new Size(570, 25);
        lblEstimativa.TextAlign = ContentAlignment.TopRight;
        lblEstimativa.Font = new Font("Segoe UI", 9);
        lblEstimativa.ForeColor = Color.Black;
        grupo.Controls.Add(lblEstimativa);

        barraProgresso = new ProgressBar();
        barraProgresso.Location = new Point(20, 50);
        barraProgresso.Size = new Size(900, 25);
        barraProgresso.Minimum = 0;
        barraProgresso.Maximum = 100;
        barraProgresso.Value = 0;
        barraProgresso.Visible = false;
        barraProgresso.Style = ProgressBarStyle.Blocks;
        grupo.Controls.Add(barraProgresso);
    }

    private void CriarLogs()
    {
        var grupo = new GroupBox();
        grupo.Text = "📝 Logs";
        grupo.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        grupo.Size = new Size(940, 210);
        grupo.Location = new Point(20, 685);
        Controls.Add(grupo);

        txtLogs = new RichTextBox();
        txtLogs.Size = new Size(900, 130);
        txtLogs.Location = new Point(20, 25);
        txtLogs.ReadOnly = true;
        txtLogs.BackColor = Color.FromArgb(30, 30, 30);
        txtLogs.ForeColor = Color.FromArgb(0, 255, 0);
        txtLogs.Font = new Font("Consolas", 9);
        txtLogs.BorderStyle = BorderStyle.FixedSingle;
        grupo.Controls.Add(txtLogs);

        btnLimparLogs = new Button();
        btnLimparLogs.Text = "🗑️ Limpar Logs";
        btnLimparLogs.Size = new Size(150, 35);
        btnLimparLogs.Location = new Point(20, 160);
        btnLimparLogs.BackColor = Color.FromArgb(108, 117, 125);
        btnLimparLogs.ForeColor = Color.White;
        btnLimparLogs.FlatStyle = FlatStyle.Flat;
        btnLimparLogs.FlatAppearance.BorderSize = 0;
        btnLimparLogs.Cursor = Cursors.Hand;
        btnLimparLogs.Click += BtnLimparLogs_Click;
        grupo.Controls.Add(btnLimparLogs);

        btnBaixarLogs = new Button();
        btnBaixarLogs.Text = "📥 Baixar Log";
        btnBaixarLogs.Size = new Size(150, 35);
        btnBaixarLogs.Location = new Point(190, 160);
        btnBaixarLogs.BackColor = Color.FromArgb(0, 120, 215);
        btnBaixarLogs.ForeColor = Color.White;
        btnBaixarLogs.FlatStyle = FlatStyle.Flat;
        btnBaixarLogs.FlatAppearance.BorderSize = 0;
        btnBaixarLogs.Cursor = Cursors.Hand;
        btnBaixarLogs.Click += BtnBaixarLogs_Click;
        grupo.Controls.Add(btnBaixarLogs);
        
        // var btnExportarLogs = new Button();
        // btnExportarLogs.Text = "📄 Exportar Relatório";
        // btnExportarLogs.Size = new Size(150, 35);
        // btnExportarLogs.Location = new Point(360, 160);
        // btnExportarLogs.BackColor = Color.FromArgb(40, 167, 69);
        // btnExportarLogs.ForeColor = Color.White;
        // btnExportarLogs.FlatStyle = FlatStyle.Flat;
        // btnExportarLogs.FlatAppearance.BorderSize = 0;
        // btnExportarLogs.Cursor = Cursors.Hand;
        // btnExportarLogs.Click += BtnExportarRelatorio_Click;
        // grupo.Controls.Add(btnExportarLogs);
    }

    private void CriarRodape()
    {
        // Botão Iniciar
        btnIniciar = new Button();
        btnIniciar.Text = "⏳ Aguardando CSV...";
        btnIniciar.BackColor = Color.FromArgb(200, 200, 200);  // Cinza opaco
        btnIniciar.ForeColor = Color.White;
        btnIniciar.FlatStyle = FlatStyle.Flat;
        btnIniciar.Size = new Size(450, 45);
        btnIniciar.Location = new Point(20, 910);
        btnIniciar.Enabled = false;
        btnIniciar.FlatAppearance.BorderSize = 0;
        btnIniciar.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        btnIniciar.Cursor = Cursors.Hand;
        Controls.Add(btnIniciar);
        btnIniciar.Click += BtnIniciar_Click;

        // Botão Parar
        btnParar = new Button();
        btnParar.Text = "⏹️ Parar";
        btnParar.BackColor = Color.FromArgb(200, 200, 200);  // Cinza opaco (inativo)
        btnParar.ForeColor = Color.White;
        btnParar.FlatStyle = FlatStyle.Flat;
        btnParar.Size = new Size(450, 45);
        btnParar.Location = new Point(490, 910);
        btnParar.Enabled = false;
        btnParar.FlatAppearance.BorderSize = 0;
        btnParar.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        btnParar.Cursor = Cursors.Hand;
        btnParar.Click += BtnParar_Click;
        Controls.Add(btnParar);
    }
    
    private void CriarStatusBar()
    {
        statusStrip = new StatusStrip();
        
        // Label para mensagens (ESQUERDA)
        statusLabel = new ToolStripStatusLabel("✅ Pronto");
        statusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        statusLabel.Spring = true;  // Ocupa espaço, empurrando o relógio para a direita
        
        // Label para o relógio (DIREITA)
        clockLabel = new ToolStripStatusLabel();
        clockLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
        clockLabel.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        
        statusStrip.Items.Add(statusLabel);
        statusStrip.Items.Add(clockLabel);
        this.Controls.Add(statusStrip);
        
        // Iniciar o timer do relógio
        IniciarRelogio();
    }

    private void IniciarRelogio()
    {
        clockTimer = new System.Windows.Forms.Timer();
        clockTimer.Interval = 1000;  // 1 segundo
        clockTimer.Tick += (s, e) => AtualizarRelogio();
        clockTimer.Start();
        AtualizarRelogio();  // Atualizar imediatamente
    }

    private void AtualizarRelogio()
    {
        if (InvokeRequired)
        {
            Invoke(() => AtualizarRelogio());
            return;
        }
        
        if (clockLabel != null)
        {
            // Formato: 14:30:45
            clockLabel.Text = DateTime.Now.ToString("HH:mm:ss");
        }
    }


    // =========================================
    // EVENT HANDLERS
    // =========================================
    
    private void BtnMostrarSenha_Click(object? sender, EventArgs e)
    {
        txtSenha.UseSystemPasswordChar = !txtSenha.UseSystemPasswordChar;
        btnMostrarSenha.Text = txtSenha.UseSystemPasswordChar ? "👁" : "🙈";
    }

    private void BtnSalvar_Click(object? sender, EventArgs e)
    {
        var config = new AppConfig
        {
            Usuario = txtUsuario.Text,
            Senha = txtSenha.Text,
            Headless = chkHeadless.Checked,
            LastCsvPath = _caminhoCsv,
            LastMode = GetSelectedMode()
        };
        
        _controller.SaveConfig(config);
        
        // 🔑 Validar campos após salvar
        ValidarCamposObrigatorios();
        
        MessageBox.Show($"Configurações salvas em:\n{_configService.ConfigDir}", 
            "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    
    private void BtnLimparSessao_Click(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "Tem certeza que deseja limpar a sessão?\n\nIsso removerá cookies, autenticação e storage state.\nVocê precisará fazer login novamente.",
            "Confirmar Limpeza de Sessão",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        
        if (result == DialogResult.Yes)
        {
            _sessionService.LimparSessao();
            AdicionarLog("🗑️ Sessão limpa com sucesso!");
            MessageBox.Show("Sessão limpa com sucesso!", "Sucesso",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void CarregarConfiguracoes()
    {
        var config = _controller.LoadConfig();
        txtUsuario.Text = config.Usuario;
        txtSenha.Text = config.Senha;
        chkHeadless.Checked = config.Headless;

        // 🔑 Adicionar eventos para validação em tempo real
        txtUsuario.TextChanged += (s, e) => ValidarCamposObrigatorios();
        txtSenha.TextChanged += (s, e) => ValidarCamposObrigatorios();
        
        if (!string.IsNullOrEmpty(config.LastCsvPath) && File.Exists(config.LastCsvPath))
        {
            _caminhoCsv = config.LastCsvPath;
            lblArquivo.Text = $"📄 {_caminhoCsv}";
            btnIniciar.Enabled = true;
            btnIniciar.BackColor = Color.FromArgb(40, 167, 69);
            btnIniciar.Text = "🚀 Iniciar Scraping";  // 🔑 ADICIONE ESTA LINHA
        }
        else
        {
            _caminhoCsv = null;
            lblArquivo.Text = "📄 Nenhum arquivo selecionado";
            btnIniciar.Enabled = false;
            btnIniciar.BackColor = Color.FromArgb(200, 200, 200);
            btnIniciar.Text = "⏳ Aguardando CSV...";  // 🔑 ADICIONE ESTA LINHA
        }
        
        // 🔑 Validar campos obrigatórios
        ValidarCamposObrigatorios();

        SetModeFromInt(config.LastMode);
    }
    
    private void SetModeFromInt(int mode)
    {
        switch (mode)
        {
            case 2: rbMedicao.Checked = true; break;
            case 3: rbCancelados.Checked = true; break;
            case 4: rbMemoriaDraft.Checked = true; break;
            case 5: rbMemoriaMedicao.Checked = true; break;
            default: rbDraft.Checked = true; break;
        }
    }

    private void BtnSelecionarCsv_Click(object? sender, EventArgs e)
    {
        if (_controller.IsRunning)
        {
            AdicionarLog("⚠️ Aguarde o término do processo atual.");
            return;
        }
        
        using var dialog = new OpenFileDialog();
        dialog.Title = "Selecionar arquivo CSV";
        dialog.Filter = "Arquivo CSV (*.csv)|*.csv";
        dialog.Multiselect = false;

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        try
        {
            _caminhoCsv = dialog.FileName;
            lblArquivo.Text = $"📄 {_caminhoCsv}";
            
            var ids = _controller.GetCsvIds(_caminhoCsv);
            AdicionarLog($"✅ CSV carregado: {_caminhoCsv}");
            AdicionarLog($"📊 Total de IDs: {ids.Count}");
            
            // 🔑 Validar campos após selecionar CSV
            ValidarCamposObrigatorios();
            
            // Salvar caminho do CSV
            var config = _controller.LoadConfig();
            config.LastCsvPath = _caminhoCsv;
            _controller.SaveConfig(config);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao ler CSV:\n{ex.Message}", "Erro",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            AdicionarLog($"❌ Erro CSV: {ex.Message}");
        }
    }

    private void BtnLimparCsv_Click(object? sender, EventArgs e)
    {
        if (_controller.IsRunning)
        {
            AdicionarLog("⚠️ Não é possível limpar durante execução.");
            return;
        }
        
        var result = MessageBox.Show(
            "Tem certeza que deseja limpar o caminho do arquivo CSV?",
            "Confirmar Limpeza",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        
        if (result == DialogResult.Yes)
        {
            _caminhoCsv = null;
            lblArquivo.Text = "📄 Nenhum arquivo selecionado";
            
            // 🔑 Validar campos após limpar CSV
            ValidarCamposObrigatorios();
            
            AdicionarLog("🗑️ CSV removido.");
        }
    }

    private void BtnLimparLogs_Click(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "Tem certeza que deseja limpar o log?",
            "Confirmar Limpeza",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        
        if (result == DialogResult.Yes)
        {
            txtLogs.Clear();
            AdicionarLog("🗑️ Logs limpos.");
        }
    }

    private void BtnBaixarLogs_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog();
        dialog.Title = "Salvar logs";
        dialog.Filter = "Arquivo TXT (*.txt)|*.txt";
        dialog.FileName = $"log_osp_scraper_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        try
        {
            File.WriteAllText(dialog.FileName, txtLogs.Text);
            MessageBox.Show("Logs salvos com sucesso.", "Sucesso",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            AdicionarLog($"✅ Logs exportados: {dialog.FileName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao salvar logs:\n{ex.Message}", "Erro",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            AdicionarLog($"❌ Erro ao exportar logs: {ex.Message}");
        }
    }
    
    private async void BtnExportarRelatorio_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog();
        dialog.Title = "Salvar relatório";
        dialog.Filter = "Arquivo TXT (*.txt)|*.txt";
        dialog.FileName = $"relatorio_osp_scraper_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        try
        {
            var report = _logService.GenerateReport();
            await File.WriteAllTextAsync(dialog.FileName, report);
            MessageBox.Show("Relatório salvo com sucesso.", "Sucesso",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            AdicionarLog($"✅ Relatório exportado: {dialog.FileName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao salvar relatório:\n{ex.Message}", "Erro",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            AdicionarLog($"❌ Erro ao exportar relatório: {ex.Message}");
        }
    }

    private async void BtnIniciar_Click(object? sender, EventArgs e)
    {
        // 🔑 Validação extra antes de iniciar
        if (string.IsNullOrEmpty(_caminhoCsv))
        {
            MessageBox.Show("Selecione um arquivo CSV primeiro!", "Atenção",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        
        if (string.IsNullOrWhiteSpace(txtUsuario.Text))
        {
            MessageBox.Show("Preencha o campo USUÁRIO!", "Atenção",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtUsuario.Focus();
            return;
        }
        
        if (string.IsNullOrWhiteSpace(txtSenha.Text))
        {
            MessageBox.Show("Preencha o campo SENHA!", "Atenção",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtSenha.Focus();
            return;
        }

        _cts = new CancellationTokenSource();
        _tempoInicio = DateTime.Now;
        
        try
        {
            SetUIControlsEnabled(false);
            
            var ids = _controller.GetCsvIds(_caminhoCsv);
            _totalIds = ids.Count;
            
            await _controller.StartScrapingAsync(
                _caminhoCsv,
                (ScrapingMode)GetSelectedMode(),
                txtUsuario.Text,
                txtSenha.Text,
                chkHeadless.Checked,
                _cts.Token
            );
        }
        catch (OperationCanceledException)
        {
            AdicionarLog("⚠️ Processo cancelado pelo usuário.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro: {ex.Message}", "Erro",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            AdicionarLog($"❌ Erro: {ex.Message}");
        }
        finally
        {
            SetUIControlsEnabled(true);
            ValidarCamposObrigatorios();  // 🔑 Revalidar ao finalizar
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void BtnParar_Click(object? sender, EventArgs e)
    {
        if (_controller.IsRunning)
        {
            var result = MessageBox.Show(
                "Tem certeza que deseja parar o processo?\n\nOs dados já extraídos serão salvos em um arquivo PARCIAL.", 
                "Confirmar Parada", 
                MessageBoxButtons.YesNo, 
                MessageBoxIcon.Question);
            
            if (result == DialogResult.Yes)
            {
                _controller.StopScraping();
                
                // Desabilitar botão Parar após solicitar parada
                btnParar.Enabled = false;
                btnParar.BackColor = Color.FromArgb(200, 200, 200);  // Cinza opaco
                btnParar.Text = "⏹️ Parando...";
                
                AdicionarLog("🛑 Parada solicitada. Finalizando ID atual e salvando dados parciais...");
            }
        }
    }

    private void OnRunningStateChanged(bool isRunning)
    {
        if (InvokeRequired)
        {
            Invoke(() => OnRunningStateChanged(isRunning));
            return;
        }
        
        if (!isRunning)
        {
            // Processo finalizado
            barraProgresso.Visible = false;
            barraProgresso.Value = 0;
            lblProgresso.Text = "Aguardando início...";
            lblEstimativa.Text = "⏱️ Estimativa: Aguardando início...";
            
            // 🔑 Restaurar botão Parar
            btnParar.Enabled = false;
            btnParar.BackColor = Color.FromArgb(200, 200, 200);
            btnParar.Text = "⏹️ Parar";
            
            // 🔑 Restaurar botão Iniciar conforme estado do CSV
            if (!string.IsNullOrEmpty(_caminhoCsv))
            {
                btnIniciar.Enabled = true;
                btnIniciar.BackColor = Color.FromArgb(40, 167, 69);
                btnIniciar.Text = "🚀 Iniciar Scraping";
            }
            else
            {
                btnIniciar.Enabled = false;
                btnIniciar.BackColor = Color.FromArgb(200, 200, 200);
                btnIniciar.Text = "⏳ Aguardando CSV...";
            }
            
            // Reativar todos os controles
            SetUIControlsEnabled(true);
            
            if (statusLabel != null && !statusLabel.Text.Contains("Erro"))
            {
                AtualizarStatusBar("✅ Pronto");
            }
        }
        else
        {
            barraProgresso.Visible = true;
            barraProgresso.Value = 0;
            AtualizarStatusBar("🔄 Processando...");
        }
    }

    private void AdicionarLog(string mensagem)
    {
        if (InvokeRequired)
        {
            Invoke(() => AdicionarLog(mensagem));
            return;
        }
        
        txtLogs.AppendText($"{mensagem}\n");
        txtLogs.ScrollToCaret();
        
        // Extrair a mensagem sem o timestamp para a StatusBar
        var mensagemStatus = mensagem;
        if (mensagem.Contains("]"))
        {
            var partes = mensagem.Split(']');
            if (partes.Length > 1)
                mensagemStatus = partes[1].Trim();
        }
        
        // Verificar se é erro
        bool isErro = mensagem.Contains("❌") || mensagem.Contains("Erro") || mensagem.Contains("falha");
        
        // Atualizar StatusBar dinamicamente
        AtualizarStatusBar(mensagemStatus, isErro);
    }

    private void AdicionarTooltips()
    {
        ToolTip toolTip = new ToolTip();
        toolTip.SetToolTip(txtUsuario, "Digite seu usuário do OSP Control");
        toolTip.SetToolTip(txtSenha, "Digite sua senha do OSP Control");
        toolTip.SetToolTip(btnIniciar, "Inicia o processo de scraping");
        toolTip.SetToolTip(btnParar, "Para o processo em execução");
        toolTip.SetToolTip(chkHeadless, "Executa o navegador em segundo plano");
        
        // Verificar se btnLimparSessao não é nulo antes de adicionar tooltip
        if (btnLimparSessao != null)
        {
            toolTip.SetToolTip(btnLimparSessao, "Limpa cookies, autenticação e storage state salvos");
        }
    }

    private int GetSelectedMode()
    {
        if (rbMedicao.Checked) return 2;
        if (rbCancelados.Checked) return 3;
        if (rbMemoriaDraft.Checked) return 4;
        if (rbMemoriaMedicao.Checked) return 5;
        return 1;
    }
    
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_controller.IsRunning)
        {
            var result = MessageBox.Show("O scraping está em execução. Deseja realmente sair?",
                "Confirmar saída", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            
            if (result == DialogResult.Yes)
            {
                _controller.StopScraping();
                Thread.Sleep(1000);
            }
            else
            {
                e.Cancel = true;
            }
        }
        
        base.OnFormClosing(e);
    }

    private void OnDataSaved(string filePath)
    {
        if (InvokeRequired)
        {
            Invoke(() => OnDataSaved(filePath));
            return;
        }
        
        // Tocar som de notificação (beep)
        System.Media.SystemSounds.Asterisk.Play();
        
        // Forçar o formulário a ficar em primeiro plano
        this.TopMost = true;
        this.TopMost = false;
        this.Activate();
        
        // Mostrar mensagem de conclusão
        var result = MessageBox.Show(
            $"✅ Processo finalizado com sucesso!\n\n📁 Arquivo gerado:\n{Path.GetFileName(filePath)}\n\nDeseja abrir a pasta do arquivo?",
            "Processo Concluído",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);
        
        if (result == DialogResult.Yes)
        {
            // Abrir pasta e selecionar o arquivo
            if (File.Exists(filePath))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
            else
            {
                var downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                System.Diagnostics.Process.Start("explorer.exe", downloadPath);
            }
        }
        
        AdicionarLog($"Arquivo salvo: {filePath}");
    }

    private void OnPartialDataSaved(string filePath)
    {
        if (InvokeRequired)
        {
            Invoke(() => OnPartialDataSaved(filePath));
            return;
        }
        
        // Tocar som de notificação
        System.Media.SystemSounds.Asterisk.Play();
        
        // Forçar o formulário a ficar em primeiro plano
        this.TopMost = true;
        this.TopMost = false;
        this.Activate();
        
        // Mostrar mensagem de conclusão parcial
        var result = MessageBox.Show(
            $"⚠️ Processo interrompido!\n\n📁 Arquivo PARCIAL gerado:\n{Path.GetFileName(filePath)}\n\nRegistros extraídos até o momento.\n\nDeseja abrir a pasta do arquivo?",
            "Processo Interrompido - Arquivo Parcial",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        
        if (result == DialogResult.Yes)
        {
            if (File.Exists(filePath))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
            else
            {
                var downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                System.Diagnostics.Process.Start("explorer.exe", downloadPath);
            }
        }
        
        AdicionarLog($"Arquivo parcial salvo: {filePath}");
    }

    private void AtualizarStatusBar(string mensagem, bool isErro = false)
    {
        if (InvokeRequired)
        {
            Invoke(() => AtualizarStatusBar(mensagem, isErro));
            return;
        }
        
        if (statusLabel != null)
        {
            // Limitar tamanho da mensagem
            var texto = mensagem.Length > 60 ? mensagem[..57] + "..." : mensagem;
            statusLabel.Text = texto;
            
            // Mudar cor conforme o tipo de mensagem
            if (isErro)
            {
                statusLabel.ForeColor = Color.Red;
            }
            else if (mensagem.Contains("✅") || mensagem.Contains("sucesso") || mensagem.Contains("concluído"))
            {
                statusLabel.ForeColor = Color.Green;
            }
            else if (mensagem.Contains("⚠️") || mensagem.Contains("atenção") || mensagem.Contains("Aguarde"))
            {
                statusLabel.ForeColor = Color.Orange;
            }
            else if (mensagem.Contains("🔄") || mensagem.Contains("Processando") || mensagem.Contains("Iniciando"))
            {
                statusLabel.ForeColor = Color.Blue;
            }
            else
            {
                statusLabel.ForeColor = Color.Black;
            }
        }
    }

    private void OnTimeUpdate(TimeSpan elapsed, TimeSpan? remaining)
    {
        if (InvokeRequired)
        {
            Invoke(() => OnTimeUpdate(elapsed, remaining));
            return;
        }
        
        // DEBUG: Verificar se o método está sendo chamado
        //System.Diagnostics.Debug.WriteLine($"OnTimeUpdate chamado: elapsed={elapsed}, remaining={remaining}");
        
        var tempoDecorrido = FormatTimeSpan(elapsed);
        
        if (lblEstimativa == null) 
        {
            System.Diagnostics.Debug.WriteLine("lblEstimativa é NULL!");
            return;
        }
        
        if (remaining.HasValue && remaining.Value.TotalSeconds > 0)
        {
            var tempoRestante = FormatTimeSpan(remaining.Value);
            lblEstimativa.Text = $"⏱️ Decorrido: {tempoDecorrido} | Restante: ~{tempoRestante}";
            AtualizarStatusBar($"Processando... Decorrido: {tempoDecorrido} | Previsão: {tempoRestante}");
        }
        else if (remaining.HasValue && remaining.Value.TotalSeconds == 0)
        {
            lblEstimativa.Text = $"⏱️ Processo finalizado! Tempo total: {tempoDecorrido}";
            AtualizarStatusBar($"✅ Processo finalizado! Duração: {tempoDecorrido}");
        }
        else
        {
            lblEstimativa.Text = $"⏱️ Decorrido: {tempoDecorrido} | Calculando tempo restante...";
        }
        
        // Forçar atualização da tela
        lblEstimativa.Refresh();
    }

    private string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{ts.Hours}h {ts.Minutes}min {ts.Seconds}s";
        if (ts.TotalMinutes >= 1)
            return $"{ts.Minutes}min {ts.Seconds}s";
        return $"{ts.Seconds}s";
    }

    private void IniciarTimerEstimativa()
    {
        _estimativaTimer = new System.Windows.Forms.Timer();
        _estimativaTimer.Interval = 1000; // Atualiza a cada segundo
        _estimativaTimer.Tick += (s, e) => {
            if (_controller.IsRunning && _tempoInicio.HasValue && _totalIds > 0)
            {
                var elapsed = DateTime.Now - _tempoInicio.Value;
                OnTimeUpdate(elapsed, null);
            }
        };
        _estimativaTimer.Start();
    }

    private void PararTimerEstimativa()
    {
        _estimativaTimer?.Stop();
        _estimativaTimer?.Dispose();
        _estimativaTimer = null;
    }

    private void SetUIControlsEnabled(bool habilitado)
    {
        // Cor opaca padrão (cinza)
        Color corOpaca = Color.FromArgb(200, 200, 200);
        
        // Botões de controle
        btnSelecionarCsv.Enabled = habilitado;
        btnSelecionarCsv.BackColor = habilitado ? Color.FromArgb(0, 120, 215) : corOpaca;
        
        btnLimparCsv.Enabled = habilitado;
        btnLimparCsv.BackColor = habilitado ? Color.FromArgb(108, 117, 125) : corOpaca;
        
        btnLimparLogs.Enabled = habilitado;
        btnLimparLogs.BackColor = habilitado ? Color.FromArgb(108, 117, 125) : corOpaca;
        
        btnBaixarLogs.Enabled = habilitado;
        btnBaixarLogs.BackColor = habilitado ? Color.FromArgb(0, 120, 215) : corOpaca;
        
        // 🔑 Botão Limpar Sessão - ADICIONAR AQUI
        if (btnLimparSessao != null)
        {
            btnLimparSessao.Enabled = habilitado;
            btnLimparSessao.BackColor = habilitado ? Color.FromArgb(108, 117, 125) : corOpaca;
        }
        
        // Botão Salvar Credenciais
        if (btnSalvar != null)
        {
            btnSalvar.Enabled = habilitado;
            btnSalvar.BackColor = habilitado ? Color.FromArgb(0, 120, 215) : corOpaca;
        }
        
        // Campos de texto
        txtUsuario.Enabled = habilitado;
        txtSenha.Enabled = habilitado;
        btnMostrarSenha.Enabled = habilitado;
        chkHeadless.Enabled = habilitado;
        
        // Radio buttons
        rbDraft.Enabled = habilitado;
        rbMedicao.Enabled = habilitado;
        rbCancelados.Enabled = habilitado;
        rbMemoriaDraft.Enabled = habilitado;
        rbMemoriaMedicao.Enabled = habilitado;
        
        if (!habilitado)
        {
            // Durante processamento: botão Parar fica vermelho e ativo
            btnParar.Enabled = true;
            btnParar.BackColor = Color.FromArgb(220, 53, 69);
            btnParar.Text = "⏹️ Parar";
            
            btnIniciar.Enabled = false;
            btnIniciar.BackColor = corOpaca;
            btnIniciar.Text = "🔄 Processando...";
        }
        else
        {
            // Após finalizar: botão Parar volta a ficar inativo
            btnParar.Enabled = false;
            btnParar.BackColor = corOpaca;
            btnParar.Text = "⏹️ Parar";
            
            // Verificar estado do CSV para definir texto correto
            if (!string.IsNullOrEmpty(_caminhoCsv))
            {
                btnIniciar.Enabled = true;
                btnIniciar.BackColor = Color.FromArgb(40, 167, 69);
                btnIniciar.Text = "🚀 Iniciar Scraping";
            }
            else
            {
                btnIniciar.Enabled = false;
                btnIniciar.BackColor = corOpaca;
                btnIniciar.Text = "⏳ Aguardando CSV...";
            }
        }
    }

    private void AtualizarProgresso(int percentual, string mensagem)
    {
        if (InvokeRequired)
        {
            Invoke(() => AtualizarProgresso(percentual, mensagem));
            return;
        }
        
        barraProgresso.Value = percentual;
        lblProgresso.Text = mensagem;
        
        // Calcular estimativa
        if (_tempoInicio.HasValue && _totalIds > 0 && percentual > 0)
        {
            var decorrido = DateTime.Now - _tempoInicio.Value;
            var processados = (_totalIds * percentual) / 100;
            if (processados > 0)
            {
                var tempoPorId = decorrido.TotalSeconds / processados;
                var restantes = _totalIds - processados;
                var segundosRestantes = tempoPorId * restantes;
                var t = TimeSpan.FromSeconds(segundosRestantes);
                
                string tempoFormatado = t.TotalHours >= 1 
                    ? $"{(int)t.TotalHours}h {t.Minutes}m" 
                    : t.TotalMinutes >= 1
                        ? $"{t.Minutes}m {t.Seconds}s"
                        : $"{t.Seconds}s";
                
                lblEstimativa.Text = $"⏱️ Estimativa: {processados}/{_totalIds} ({percentual}%) | Restante: ~{tempoFormatado}";
            }
        }
        
        if (percentual == 100)
        {
            lblEstimativa.Text = "⏱️ Processo finalizado!";
        }
    }
   
    private void ValidarCamposObrigatorios()
    {
        // Verificar se campos estão vazios
        bool usuarioVazio = string.IsNullOrWhiteSpace(txtUsuario.Text);
        bool senhaVazia = string.IsNullOrWhiteSpace(txtSenha.Text);
        bool csvVazio = string.IsNullOrEmpty(_caminhoCsv);
        
        // 🔑 Aplicar estilo de campo obrigatório (borda vermelha)
        if (usuarioVazio)
        {
            txtUsuario.BackColor = Color.FromArgb(255, 240, 240);  // Vermelho claro
            txtUsuario.PlaceholderText = "⚠️ Campo obrigatório";
        }
        else
        {
            txtUsuario.BackColor = SystemColors.Window;
            txtUsuario.PlaceholderText = "Usuário do OSP Control";
        }
        
        if (senhaVazia)
        {
            txtSenha.BackColor = Color.FromArgb(255, 240, 240);  // Vermelho claro
            txtSenha.PlaceholderText = "⚠️ Campo obrigatório";
        }
        else
        {
            txtSenha.BackColor = SystemColors.Window;
            txtSenha.PlaceholderText = "Senha do OSP Control";
        }
        
        // 🔑 Habilitar/desabilitar botão Iniciar
        bool podeIniciar = !usuarioVazio && !senhaVazia && !csvVazio && !_controller.IsRunning;
        
        btnIniciar.Enabled = podeIniciar;
        
        if (podeIniciar)
        {
            btnIniciar.BackColor = Color.FromArgb(40, 167, 69);  // Verde
            btnIniciar.Text = "🚀 Iniciar Scraping";
        }
        else
        {
            btnIniciar.BackColor = Color.FromArgb(200, 200, 200);  // Cinza
            if (csvVazio)
                btnIniciar.Text = "⏳ Aguardando CSV...";
            else
                btnIniciar.Text = "⚠️ Preencha usuário e senha";
        }
    }

    private void IniciarVerificacaoAtualizacoes()
    {
        _updateService = new UpdateService(_logService);
        _updateService.OnUpdateAvailable += OnUpdateAvailable;
        _updateService.OnError += (error) => _logService.Warning($"Erro ao verificar: {error}");
        
        // Verificar ao iniciar (após 5 segundos)
        Task.Run(async () =>
        {
            await Task.Delay(5000);
            await _updateService.VerificarAtualizacaoAsync();
        });
        
        // Verificar a cada 24 horas
        _updateCheckTimer = new Timer();  // Agora não tem ambiguidade
        _updateCheckTimer.Interval = 24 * 60 * 60 * 1000; // 24 horas
        _updateCheckTimer.Tick += async (s, e) =>
        {
            await _updateService.VerificarAtualizacaoAsync();
        };
        _updateCheckTimer.Start();
    }

    // Modificar o método OnUpdateAvailable:
    private void OnUpdateAvailable(UpdateInfo updateInfo)
    {
        if (InvokeRequired)
        {
            Invoke(() => OnUpdateAvailable(updateInfo));
            return;
        }
        
        // Verificações de segurança
        if (updateInfo == null)
        {
            _logService?.Warning("UpdateInfo é nulo, não é possível mostrar notificação");
            return;
        }
        
        if (_updateService == null)
        {
            _logService?.Warning("UpdateService não inicializado");
            return;
        }
        
        // Verificar se o usuário ignorou esta versão
        var ignoreFile = Path.Combine(Application.StartupPath, "ignored_version.txt");
        string? ignoredVersion = null;
        
        if (File.Exists(ignoreFile))
        {
            ignoredVersion = File.ReadAllText(ignoreFile);
        }
        
        if (ignoredVersion == updateInfo.VersaoNova)
            return;
        
        // Criar e mostrar o formulário de notificação
        var dialog = new UpdateNotificationForm(updateInfo, _updateService);
        dialog.ShowDialog(this);
    }

    private void MostrarNotificacaoTray(UpdateInfo updateInfo)
    {
        // Criar notificação de sistema (Windows)
        var notifyIcon = new NotifyIcon();
        notifyIcon.Icon = SystemIcons.Information;
        notifyIcon.Visible = true;
        notifyIcon.BalloonTipTitle = "Atualização Disponível";
        notifyIcon.BalloonTipText = $"Versão {updateInfo.VersaoNova} disponível!\nClique para atualizar.";
        notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
        notifyIcon.BalloonTipClosed += (s, e) => notifyIcon.Dispose();
        notifyIcon.Click += (s, e) =>
        {
            var dialog = new UpdateNotificationForm(updateInfo, _updateService);
            dialog.ShowDialog(this);
        };
        notifyIcon.ShowBalloonTip(5000);
    }

    private void CriarBotaoSuporte()
    {
        // Botão de Suporte Técnico
        var btnSuporte = new Button();
        btnSuporte.Text = "🆘 Suporte Técnico";
        btnSuporte.Size = new Size(120, 30);
        btnSuporte.Location = new Point(20, 15);
        btnSuporte.BackColor = Color.FromArgb(0, 120, 215);
        btnSuporte.ForeColor = Color.White;
        btnSuporte.FlatStyle = FlatStyle.Flat;
        btnSuporte.FlatAppearance.BorderSize = 0;
        btnSuporte.Cursor = Cursors.Hand;
        btnSuporte.Click += BtnSuporte_Click;
        Controls.Add(btnSuporte);
    }

    private void BtnSuporte_Click(object? sender, EventArgs e)
    {
        // Criar formulário de diálogo personalizado
        var suporteForm = new Form();
        suporteForm.Text = "🆘 Suporte Técnico";
        suporteForm.Size = new Size(500, 380);
        suporteForm.StartPosition = FormStartPosition.CenterParent;
        suporteForm.FormBorderStyle = FormBorderStyle.FixedDialog;
        suporteForm.MaximizeBox = false;
        suporteForm.MinimizeBox = false;
        suporteForm.BackColor = Color.White;
        
        // Criar layout
        var mainLayout = new TableLayoutPanel();
        mainLayout.Dock = DockStyle.Fill;
        mainLayout.Padding = new Padding(20);
        mainLayout.RowCount = 6;
        mainLayout.ColumnCount = 1;
        
        // Ícone de suporte
        var lblIcon = new Label();
        lblIcon.Text = "🆘";
        lblIcon.Font = new Font("Segoe UI", 48);
        lblIcon.TextAlign = ContentAlignment.MiddleCenter;
        lblIcon.Dock = DockStyle.Fill;
        lblIcon.Height = 80;
        
        // Título
        var lblTitle = new Label();
        lblTitle.Text = "Precisa de ajuda?";
        lblTitle.Font = new Font("Segoe UI", 16, FontStyle.Bold);
        lblTitle.ForeColor = Color.FromArgb(0, 120, 215);
        lblTitle.TextAlign = ContentAlignment.MiddleCenter;
        lblTitle.Dock = DockStyle.Fill;
        
        // Instruções
        var lblInstrucoes = new Label();
        lblInstrucoes.Text = "Para melhor atendimento, siga os passos abaixo:";
        lblInstrucoes.Font = new Font("Segoe UI", 10);
        lblInstrucoes.TextAlign = ContentAlignment.MiddleLeft;
        lblInstrucoes.Dock = DockStyle.Fill;
        
        // Passos
        var lblPassos = new Label();
        lblPassos.Text = "1️⃣ Clique em 'Baixar Log'\n2️⃣ Salve o arquivo .txt e prints do problema\n3️⃣ Descreva o problema\n4️⃣ Envio de logs e evidências anexado ao email ";
        lblPassos.Font = new Font("Segoe UI", 9);
        lblPassos.TextAlign = ContentAlignment.MiddleLeft;
        lblPassos.Dock = DockStyle.Fill;
        
        // Contato
        var lblContato = new Label();
        lblContato.Text = "📧 Envie para: geovanehacker.io@gmail.com";
        lblContato.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        lblContato.ForeColor = Color.FromArgb(40, 167, 69);
        lblContato.TextAlign = ContentAlignment.MiddleLeft;
        lblContato.Dock = DockStyle.Fill;
        
        // Botões
        var btnPanel = new FlowLayoutPanel();
        btnPanel.Dock = DockStyle.Fill;
        btnPanel.FlowDirection = FlowDirection.LeftToRight;
        btnPanel.Padding = new Padding(0, 10, 0, 0);
        
        var btnBaixarLog = new Button();
        btnBaixarLog.Text = "📥 Baixar Log Agora";
        btnBaixarLog.Size = new Size(150, 40);
        btnBaixarLog.BackColor = Color.FromArgb(0, 120, 215);
        btnBaixarLog.ForeColor = Color.White;
        btnBaixarLog.FlatStyle = FlatStyle.Flat;
        btnBaixarLog.Cursor = Cursors.Hand;
        btnBaixarLog.Click += (s, args) =>
        {
            // Chamar o método de download de log existente
            BtnBaixarLogs_Click(s, args);
            MessageBox.Show("Log salvo com sucesso!\n\nAnexe este arquivo no e-mail de suporte.", 
                "Log Salvo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        
        var btnFechar = new Button();
        btnFechar.Text = "Fechar";
        btnFechar.Size = new Size(100, 40);
        btnFechar.BackColor = Color.FromArgb(108, 117, 125);
        btnFechar.ForeColor = Color.White;
        btnFechar.FlatStyle = FlatStyle.Flat;
        btnFechar.Cursor = Cursors.Hand;
        btnFechar.Click += (s, args) => suporteForm.Close();
        
        btnPanel.Controls.Add(btnBaixarLog);
        btnPanel.Controls.Add(btnFechar);
        
        // Adicionar controles ao layout
        mainLayout.Controls.Add(lblIcon, 0, 0);
        mainLayout.Controls.Add(lblTitle, 0, 1);
        mainLayout.Controls.Add(lblInstrucoes, 0, 2);
        mainLayout.Controls.Add(lblPassos, 0, 3);
        mainLayout.Controls.Add(lblContato, 0, 4);
        mainLayout.Controls.Add(btnPanel, 0, 5);
        
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        
        suporteForm.Controls.Add(mainLayout);
        suporteForm.ShowDialog(this);
    }
}