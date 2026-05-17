using OSPVivoScraper.Services;


namespace OSPVivoScraper.Views;

public class UpdateNotificationForm : Form
{
    private Label lblTitle = null!;
    private Label lblCurrentVersion = null!;
    private Label lblNewVersion = null!;
    private Label lblDescription = null!;
    private Label lblSize = null!;
    private RichTextBox txtChangelog = null!;
    private Button btnUpdate = null!;
    private Button btnLater = null!;
    private Button btnIgnore = null!;
    private ProgressBar progressBar = null!;
    private Label lblProgress = null!;
    
    private readonly UpdateInfo _updateInfo;
    private readonly UpdateService _updateService;
    private bool _isDownloading = false;
    
    public UpdateNotificationForm(UpdateInfo updateInfo, UpdateService updateService)
    {
        _updateInfo = updateInfo;
        _updateService = updateService;
        InitializeComponent();
        LoadUpdateInfo();
        
        // Assinar eventos
        _updateService.OnDownloadProgress += OnDownloadProgress;
        _updateService.OnError += OnError;
    }
    
    private void InitializeComponent()
    {
        this.Text = "Atualização Disponível";
        this.Size = new Size(550, 500);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = Color.White;
        
        // Título
        lblTitle = new Label();
        lblTitle.Text = "🔄 Nova Versão Disponível!";
        lblTitle.Font = new Font("Segoe UI", 16, FontStyle.Bold);
        lblTitle.ForeColor = Color.FromArgb(0, 120, 215);
        lblTitle.Location = new Point(20, 20);
        lblTitle.Size = new Size(500, 40);
        lblTitle.TextAlign = ContentAlignment.MiddleLeft;
        
        // Versão Atual
        lblCurrentVersion = new Label();
        lblCurrentVersion.Location = new Point(20, 70);
        lblCurrentVersion.Size = new Size(500, 25);
        lblCurrentVersion.Font = new Font("Segoe UI", 10);
        
        // Nova Versão
        lblNewVersion = new Label();
        lblNewVersion.Location = new Point(20, 95);
        lblNewVersion.Size = new Size(500, 25);
        lblNewVersion.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        lblNewVersion.ForeColor = Color.FromArgb(40, 167, 69);
        
        // Tamanho
        lblSize = new Label();
        lblSize.Location = new Point(20, 120);
        lblSize.Size = new Size(500, 25);
        lblSize.Font = new Font("Segoe UI", 9);
        lblSize.ForeColor = Color.Gray;
        
        // Descrição
        lblDescription = new Label();
        lblDescription.Text = "📝 Novidades desta versão:";
        lblDescription.Location = new Point(20, 155);
        lblDescription.Size = new Size(500, 25);
        lblDescription.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        
        // Changelog
        txtChangelog = new RichTextBox();
        txtChangelog.Location = new Point(20, 185);
        txtChangelog.Size = new Size(500, 150);
        txtChangelog.ReadOnly = true;
        txtChangelog.BorderStyle = BorderStyle.FixedSingle;
        txtChangelog.Font = new Font("Consolas", 9);
        
        // Progresso (inicialmente invisível)
        progressBar = new ProgressBar();
        progressBar.Location = new Point(20, 185);
        progressBar.Size = new Size(500, 25);
        progressBar.Visible = false;
        
        lblProgress = new Label();
        lblProgress.Location = new Point(20, 215);
        lblProgress.Size = new Size(500, 25);
        lblProgress.TextAlign = ContentAlignment.MiddleCenter;
        lblProgress.Visible = false;
        
        // Botões
        btnUpdate = new Button();
        btnUpdate.Text = "📥 Atualizar Agora";
        btnUpdate.Size = new Size(140, 40);
        btnUpdate.Location = new Point(20, 410);
        btnUpdate.BackColor = Color.FromArgb(40, 167, 69);
        btnUpdate.ForeColor = Color.White;
        btnUpdate.FlatStyle = FlatStyle.Flat;
        btnUpdate.FlatAppearance.BorderSize = 0;
        btnUpdate.Cursor = Cursors.Hand;
        btnUpdate.Click += BtnUpdate_Click;
        
        btnLater = new Button();
        btnLater.Text = "⏰ Lembrar Depois";
        btnLater.Size = new Size(140, 40);
        btnLater.Location = new Point(170, 410);
        btnLater.BackColor = Color.FromArgb(108, 117, 125);
        btnLater.ForeColor = Color.White;
        btnLater.FlatStyle = FlatStyle.Flat;
        btnLater.FlatAppearance.BorderSize = 0;
        btnLater.Cursor = Cursors.Hand;
        btnLater.Click += (s, e) => this.Close();
        
        btnIgnore = new Button();
        btnIgnore.Text = "❌ Ignorar Esta Versão";
        btnIgnore.Size = new Size(140, 40);
        btnIgnore.Location = new Point(320, 410);
        btnIgnore.BackColor = Color.FromArgb(220, 53, 69);
        btnIgnore.ForeColor = Color.White;
        btnIgnore.FlatStyle = FlatStyle.Flat;
        btnIgnore.FlatAppearance.BorderSize = 0;
        btnIgnore.Cursor = Cursors.Hand;
        btnIgnore.Click += BtnIgnore_Click;
        
        this.Controls.AddRange(new Control[] {
            lblTitle, lblCurrentVersion, lblNewVersion, lblSize,
            lblDescription, txtChangelog, progressBar, lblProgress,
            btnUpdate, btnLater, btnIgnore
        });
    }
    
    private void LoadUpdateInfo()
    {
        lblCurrentVersion.Text = $"📌 Versão atual: {_updateInfo.VersaoAtual}";
        lblNewVersion.Text = $"✨ Nova versão: {_updateInfo.VersaoNova}";
        lblSize.Text = $"📦 Tamanho: {FormatFileSize(_updateInfo.Tamanho)}";
        
        txtChangelog.Text = string.IsNullOrEmpty(_updateInfo.Descricao) 
            ? "• Melhorias de desempenho\n• Correções de bugs\n• Novas funcionalidades" 
            : _updateInfo.Descricao;
    }
    
    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
    
    private void OnDownloadProgress(string message, int percent)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(() => OnDownloadProgress(message, percent));
            return;
        }
        
        progressBar.Value = percent;
        lblProgress.Text = message;
        btnUpdate.Text = percent < 100 ? "📥 Baixando..." : "✅ Download concluído!";
    }
    
    private void OnError(string error)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(() => OnError(error));
            return;
        }
        
        MessageBox.Show($"Erro na atualização:\n{error}", "Erro",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
    
    private async void BtnUpdate_Click(object? sender, EventArgs e)
    {
        if (_isDownloading) return;
        
        _isDownloading = true;
        
        // Trocar interface para modo de download
        txtChangelog.Visible = false;
        progressBar.Visible = true;
        lblProgress.Visible = true;
        progressBar.Value = 0;
        
        btnUpdate.Enabled = false;
        btnLater.Enabled = false;
        btnIgnore.Enabled = false;
        
        var success = await _updateService.BaixarAtualizacaoAsync(_updateInfo.DownloadUrl);
        
        if (success)
        {
            // A aplicação será fechada pelo UpdateService
        }
    }
    
    private void BtnIgnore_Click(object? sender, EventArgs e)
    {
        // Salvar versão ignorada (usando arquivo simples)
        var ignoreFile = Path.Combine(Application.StartupPath, "ignored_version.txt");
        File.WriteAllText(ignoreFile, _updateInfo.VersaoNova);
        this.Close();
    }
}