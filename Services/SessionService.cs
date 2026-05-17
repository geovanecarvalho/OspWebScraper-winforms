using System.Text.Json;

namespace OSPVivoScraper.Services;

public class SessionService
{
    private readonly string _configDir;
    private readonly string _sessionFile;
    private readonly string _authFile;
    private readonly string _cookiesFile;
    private readonly string _storageFile;
    
    public SessionService()
    {
        _configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
            ".osp_web_scraper"
        );
        _sessionFile = Path.Combine(_configDir, "playwright_session.json");
        _authFile = Path.Combine(_configDir, "auth.json");
        _cookiesFile = Path.Combine(_configDir, "cookies.json");
        _storageFile = Path.Combine(_configDir, "storage_state.json");
        
        // Criar diretório se não existir
        if (!Directory.Exists(_configDir))
        {
            Directory.CreateDirectory(_configDir);
        }
    }
    
    /// <summary>
    /// Diretório das sessões
    /// </summary>
    public string SessionDir => _configDir;
    
    /// <summary>
    /// Arquivo de sessão do Playwright
    /// </summary>
    public string SessionFile => _sessionFile;
    
    /// <summary>
    /// Arquivo de autenticação
    /// </summary>
    public string AuthFile => _authFile;
    
    /// <summary>
    /// Arquivo de cookies
    /// </summary>
    public string CookiesFile => _cookiesFile;
    
    /// <summary>
    /// Arquivo de storage state (recomendado pelo Playwright)
    /// </summary>
    public string StorageFile => _storageFile;
    
    // ===========================================================
    // MÉTODOS PARA SESSÃO DO PLAYWRIGHT
    // ===========================================================
    
    /// <summary>
    /// Salva a sessão do Playwright (storage state)
    /// </summary>
    public void SalvarSessao(string sessionData)
    {
        try
        {
            File.WriteAllText(_sessionFile, sessionData);
        }
        catch (Exception ex)
        {
            throw new Exception($"Erro ao salvar sessão: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Carrega a sessão do Playwright
    /// </summary>
    public string? CarregarSessao()
    {
        try
        {
            if (File.Exists(_sessionFile))
            {
                return File.ReadAllText(_sessionFile);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao carregar sessão: {ex.Message}");
        }
        
        return null;
    }
    
    /// <summary>
    /// Salva o storage state (recomendado para Playwright)
    /// </summary>
    public void SalvarStorageState(string storageStateJson)
    {
        try
        {
            File.WriteAllText(_storageFile, storageStateJson);
        }
        catch (Exception ex)
        {
            throw new Exception($"Erro ao salvar storage state: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Carrega o storage state
    /// </summary>
    public string? CarregarStorageState()
    {
        try
        {
            if (File.Exists(_storageFile))
            {
                return File.ReadAllText(_storageFile);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao carregar storage state: {ex.Message}");
        }
        
        return null;
    }
    
    // ===========================================================
    // MÉTODOS PARA AUTENTICAÇÃO
    // ===========================================================
    
    /// <summary>
    /// Salva dados de autenticação
    /// </summary>
    public void SalvarAuth(string authData)
    {
        try
        {
            File.WriteAllText(_authFile, authData);
        }
        catch (Exception ex)
        {
            throw new Exception($"Erro ao salvar autenticação: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Carrega dados de autenticação
    /// </summary>
    public string? CarregarAuth()
    {
        try
        {
            if (File.Exists(_authFile))
            {
                return File.ReadAllText(_authFile);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao carregar autenticação: {ex.Message}");
        }
        
        return null;
    }
    
    /// <summary>
    /// Salva autenticação como objeto tipado
    /// </summary>
    public void SalvarAuthObject<T>(T authObject)
    {
        try
        {
            var json = JsonSerializer.Serialize(authObject, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_authFile, json);
        }
        catch (Exception ex)
        {
            throw new Exception($"Erro ao salvar autenticação: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Carrega autenticação como objeto tipado
    /// </summary>
    public T? CarregarAuthObject<T>() where T : new()
    {
        try
        {
            if (File.Exists(_authFile))
            {
                var json = File.ReadAllText(_authFile);
                return JsonSerializer.Deserialize<T>(json);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao carregar autenticação: {ex.Message}");
        }
        
        return new T();
    }
    
    // ===========================================================
    // MÉTODOS PARA COOKIES
    // ===========================================================
    
    /// <summary>
    /// Salva cookies
    /// </summary>
    public void SalvarCookies(string cookiesJson)
    {
        try
        {
            File.WriteAllText(_cookiesFile, cookiesJson);
        }
        catch (Exception ex)
        {
            throw new Exception($"Erro ao salvar cookies: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Carrega cookies
    /// </summary>
    public string? CarregarCookies()
    {
        try
        {
            if (File.Exists(_cookiesFile))
            {
                return File.ReadAllText(_cookiesFile);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao carregar cookies: {ex.Message}");
        }
        
        return null;
    }
    
    /// <summary>
    /// Salva cookies como lista de objetos
    /// </summary>
    public void SalvarCookiesObject<T>(List<T> cookies)
    {
        try
        {
            var json = JsonSerializer.Serialize(cookies, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_cookiesFile, json);
        }
        catch (Exception ex)
        {
            throw new Exception($"Erro ao salvar cookies: {ex.Message}");
        }
    }
    
    // ===========================================================
    // MÉTODOS DE GERENCIAMENTO
    // ===========================================================
    
    /// <summary>
    /// Verifica se existe sessão salva
    /// </summary>
    public bool HasSession()
    {
        return File.Exists(_sessionFile) || File.Exists(_storageFile);
    }
    
    /// <summary>
    /// Verifica se existe autenticação salva
    /// </summary>
    public bool HasAuth()
    {
        return File.Exists(_authFile);
    }
    
    /// <summary>
    /// Verifica se existe cookies salvos
    /// </summary>
    public bool HasCookies()
    {
        return File.Exists(_cookiesFile);
    }
    
    /// <summary>
    /// Limpa toda a sessão (remove todos os arquivos)
    /// </summary>
    public void LimparSessao()
    {
        try
        {
            if (File.Exists(_sessionFile))
                File.Delete(_sessionFile);
            if (File.Exists(_authFile))
                File.Delete(_authFile);
            if (File.Exists(_cookiesFile))
                File.Delete(_cookiesFile);
            if (File.Exists(_storageFile))
                File.Delete(_storageFile);
        }
        catch (Exception ex)
        {
            throw new Exception($"Erro ao limpar sessão: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Limpa apenas a sessão do Playwright (mantém autenticação)
    /// </summary>
    public void LimparPlaywrightSession()
    {
        try
        {
            if (File.Exists(_sessionFile))
                File.Delete(_sessionFile);
            if (File.Exists(_storageFile))
                File.Delete(_storageFile);
        }
        catch (Exception ex)
        {
            throw new Exception($"Erro ao limpar sessão do Playwright: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Limpa apenas a autenticação (mantém sessão)
    /// </summary>
    public void LimparAuth()
    {
        try
        {
            if (File.Exists(_authFile))
                File.Delete(_authFile);
        }
        catch (Exception ex)
        {
            throw new Exception($"Erro ao limpar autenticação: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Limpa apenas os cookies
    /// </summary>
    public void LimparCookies()
    {
        try
        {
            if (File.Exists(_cookiesFile))
                File.Delete(_cookiesFile);
        }
        catch (Exception ex)
        {
            throw new Exception($"Erro ao limpar cookies: {ex.Message}");
        }
    }
    
    // ===========================================================
    // MÉTODOS DE BACKUP
    // ===========================================================
    
    /// <summary>
    /// Cria backup da sessão completa
    /// </summary>
    public string CriarBackupSessao()
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupDir = Path.Combine(_configDir, "backups");
        
        if (!Directory.Exists(backupDir))
            Directory.CreateDirectory(backupDir);
        
        var backupFile = Path.Combine(backupDir, $"session_backup_{timestamp}.zip");
        
        // Aqui poderia implementar compressão dos arquivos
        // Por simplicidade, vamos apenas copiar os arquivos para uma pasta
        var backupFolder = Path.Combine(backupDir, $"session_backup_{timestamp}");
        Directory.CreateDirectory(backupFolder);
        
        if (File.Exists(_sessionFile))
            File.Copy(_sessionFile, Path.Combine(backupFolder, "playwright_session.json"));
        if (File.Exists(_authFile))
            File.Copy(_authFile, Path.Combine(backupFolder, "auth.json"));
        if (File.Exists(_cookiesFile))
            File.Copy(_cookiesFile, Path.Combine(backupFolder, "cookies.json"));
        if (File.Exists(_storageFile))
            File.Copy(_storageFile, Path.Combine(backupFolder, "storage_state.json"));
        
        return backupFolder;
    }
    
    /// <summary>
    /// Obtém informações da sessão (tamanho, última modificação, etc)
    /// </summary>
    public Dictionary<string, object> GetSessionInfo()
    {
        var info = new Dictionary<string, object>();
        
        info["SessionDir"] = _configDir;
        info["HasSession"] = HasSession();
        info["HasAuth"] = HasAuth();
        info["HasCookies"] = HasCookies();
        
        if (File.Exists(_sessionFile))
        {
            var fileInfo = new FileInfo(_sessionFile);
            info["SessionFileSize"] = fileInfo.Length;
            info["SessionLastModified"] = fileInfo.LastWriteTime;
        }
        
        if (File.Exists(_authFile))
        {
            var fileInfo = new FileInfo(_authFile);
            info["AuthFileSize"] = fileInfo.Length;
            info["AuthLastModified"] = fileInfo.LastWriteTime;
        }
        
        return info;
    }
    
    // ===========================================================
    // MÉTODOS PARA VALIDAÇÃO
    // ===========================================================
    
    /// <summary>
    /// Verifica se a sessão é válida (arquivo existe e não está vazio)
    /// </summary>
    public bool IsSessionValid()
    {
        try
        {
            if (File.Exists(_sessionFile))
            {
                var content = File.ReadAllText(_sessionFile);
                return !string.IsNullOrWhiteSpace(content);
            }
            if (File.Exists(_storageFile))
            {
                var content = File.ReadAllText(_storageFile);
                return !string.IsNullOrWhiteSpace(content);
            }
        }
        catch { }
        
        return false;
    }
    
    /// <summary>
    /// Verifica se a autenticação é válida
    /// </summary>
    public bool IsAuthValid()
    {
        try
        {
            if (File.Exists(_authFile))
            {
                var content = File.ReadAllText(_authFile);
                return !string.IsNullOrWhiteSpace(content);
            }
        }
        catch { }
        
        return false;
    }
    
    // ===========================================================
    // MÉTODOS DE UTILIDADE
    // ===========================================================
    
    /// <summary>
    /// Obtém o tempo de vida da sessão em horas
    /// </summary>
    public double GetSessionAgeHours()
    {
        try
        {
            if (File.Exists(_storageFile))
            {
                var fileInfo = new FileInfo(_storageFile);
                return (DateTime.Now - fileInfo.LastWriteTime).TotalHours;
            }
            if (File.Exists(_sessionFile))
            {
                var fileInfo = new FileInfo(_sessionFile);
                return (DateTime.Now - fileInfo.LastWriteTime).TotalHours;
            }
        }
        catch { }
        
        return 0;
    }
    
    /// <summary>
    /// Verifica se a sessão precisa ser renovada (mais de X horas)
    /// </summary>
    public bool NeedsSessionRenewal(int maxHours = 24)
    {
        return GetSessionAgeHours() > maxHours;
    }
}