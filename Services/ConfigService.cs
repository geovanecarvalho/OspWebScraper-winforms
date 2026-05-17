using System.Text.Json;
using OSPVivoScraper.Models;

namespace OSPVivoScraper.Services;

public class ConfigService
{
    private readonly string _configDir;
    private readonly string _configFile;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public ConfigService()
    {
        _configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
            ".osp_web_scraper"
        );
        _configFile = Path.Combine(_configDir, "config.json");
        
        _jsonOptions = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
        
        // Criar diretório se não existir
        if (!Directory.Exists(_configDir))
        {
            Directory.CreateDirectory(_configDir);
        }
    }
    
    /// <summary>
    /// Retorna o diretório onde as configurações são armazenadas
    /// </summary>
    public string ConfigDir => _configDir;
    
    /// <summary>
    /// Retorna o caminho completo do arquivo de configuração
    /// </summary>
    public string ConfigFile => _configFile;
    
    /// <summary>
    /// Salva as configurações no arquivo JSON (com senha criptografada)
    /// </summary>
    public void Salvar(AppConfig config)
    {
        try
        {
            // Criar uma cópia para criptografar a senha
            var configToSave = new AppConfig
            {
                Usuario = config.Usuario,
                Senha = CryptoService.Encrypt(config.Senha),  // 🔑 CRIPTOGRAFAR SENHA
                Headless = config.Headless,
                LastCsvPath = config.LastCsvPath,
                LastMode = config.LastMode
            };
            
            var json = JsonSerializer.Serialize(configToSave, _jsonOptions);
            File.WriteAllText(_configFile, json);
        }
        catch (Exception ex)
        {
            throw new Exception($"Erro ao salvar configuração: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Carrega as configurações do arquivo JSON (com senha descriptografada)
    /// </summary>
    public AppConfig Carregar()
    {
        try
        {
            if (File.Exists(_configFile))
            {
                var json = File.ReadAllText(_configFile);
                var config = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions);
                if (config != null)
                {
                    // 🔑 DESCRIPTOGRAFAR SENHA
                    config.Senha = CryptoService.Decrypt(config.Senha);
                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            // Log de erro pode ser adicionado aqui
            Console.WriteLine($"Erro ao carregar configuração: {ex.Message}");
        }
        
        return new AppConfig();
    }
    
    /// <summary>
    /// Verifica se o arquivo de configuração existe
    /// </summary>
    public bool ConfigExists()
    {
        return File.Exists(_configFile);
    }
    
    /// <summary>
    /// Carrega uma configuração específica por chave
    /// </summary>
    public T? CarregarValor<T>(string key, T? defaultValue = default)
    {
        try
        {
            var config = Carregar();
            var property = typeof(AppConfig).GetProperty(key);
            if (property != null)
            {
                var value = property.GetValue(config);
                if (value != null)
                {
                    return (T)value;
                }
            }
        }
        catch { }
        
        return defaultValue;
    }
    
    /// <summary>
    /// Salva um valor específico na configuração
    /// </summary>
    public void SalvarValor<T>(string key, T value)
    {
        var config = Carregar();
        var property = typeof(AppConfig).GetProperty(key);
        if (property != null && property.CanWrite)
        {
            property.SetValue(config, value);
            Salvar(config);
        }
    }
    
    /// <summary>
    /// Limpa todas as configurações (remove o arquivo)
    /// </summary>
    public void Limpar()
    {
        try
        {
            if (File.Exists(_configFile))
            {
                File.Delete(_configFile);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Erro ao limpar configurações: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Exporta as configurações para um arquivo específico
    /// </summary>
    public void Exportar(string filePath)
    {
        try
        {
            var config = Carregar();
            // 🔑 Para exportar, mantemos a senha descriptografada? Ou criptografada?
            // Recomendo exportar sem criptografar para facilitar leitura externa
            var json = JsonSerializer.Serialize(config, _jsonOptions);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            throw new Exception($"Erro ao exportar configuração: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Importa configurações de um arquivo
    /// </summary>
    public void Importar(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions);
                if (config != null)
                {
                    Salvar(config);
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Erro ao importar configuração: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Cria um backup das configurações atuais (senha criptografada)
    /// </summary>
    public string CriarBackup()
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupFile = Path.Combine(_configDir, $"config_backup_{timestamp}.json");
        Exportar(backupFile);
        return backupFile;
    }
    
    /// <summary>
    /// Restaura o último backup
    /// </summary>
    public bool RestaurarUltimoBackup()
    {
        var backups = Directory.GetFiles(_configDir, "config_backup_*.json");
        if (backups.Length > 0)
        {
            var lastBackup = backups.OrderByDescending(f => f).First();
            Importar(lastBackup);
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Obtém o tamanho do arquivo de configuração em bytes
    /// </summary>
    public long GetConfigFileSize()
    {
        try
        {
            if (File.Exists(_configFile))
            {
                return new FileInfo(_configFile).Length;
            }
        }
        catch { }
        return 0;
    }
    
    /// <summary>
    /// Verifica se o arquivo de configuração é válido (JSON bem formatado)
    /// </summary>
    public bool IsConfigValid()
    {
        try
        {
            if (!File.Exists(_configFile))
                return false;
            
            var json = File.ReadAllText(_configFile);
            var config = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions);
            return config != null;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Recria o arquivo de configuração com valores padrão
    /// </summary>
    public void ResetToDefault()
    {
        var defaultConfig = new AppConfig();
        Salvar(defaultConfig);
    }
}