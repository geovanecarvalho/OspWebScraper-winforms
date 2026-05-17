using System.Text;

namespace OSPVivoScraper.Services;

public enum LogLevel
{
    Info = 0,
    Success = 1,
    Warning = 2,
    Error = 3,
    Progress = 4,
    Debug = 5
}

public class LogEventArgs : EventArgs
{
    public string Message { get; set; } = string.Empty;
    public LogLevel Level { get; set; }
    public DateTime Timestamp { get; set; }
    public string FormattedMessage => $"[{Timestamp:HH:mm:ss}] {GetEmoji()}{Message}";
    
    private string GetEmoji()
    {
        return Level switch
        {
            LogLevel.Success => "✅ ",
            LogLevel.Warning => "⚠️ ",
            LogLevel.Error => "❌ ",
            LogLevel.Progress => "🔄 ",
            LogLevel.Debug => "🐛 ",
            _ => "ℹ️ "
        };
    }
}

public class LogService
{
    // Eventos - CORRIGIDO
    public event EventHandler<LogEventArgs>? OnLog;
    public event Action<string>? OnNewLog;  // Usando Action em vez de EventHandler
    public event Action<List<string>>? OnLogsCleared;
    
    private readonly List<LogEntry> _logs = new();
    private readonly object _lock = new();
    
    private bool _enableDebug = false;
    private bool _persistToFile = true;
    private string _logFile = string.Empty;
    private int _maxLogEntries = 10000;
    
    private readonly Dictionary<LogLevel, ConsoleColor> _consoleColors = new()
    {
        [LogLevel.Info] = ConsoleColor.White,
        [LogLevel.Success] = ConsoleColor.Green,
        [LogLevel.Warning] = ConsoleColor.Yellow,
        [LogLevel.Error] = ConsoleColor.Red,
        [LogLevel.Progress] = ConsoleColor.Cyan,
        [LogLevel.Debug] = ConsoleColor.Magenta
    };
    
    public LogService()
    {
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".osp_web_scraper"
        );
        
        if (!Directory.Exists(configDir))
            Directory.CreateDirectory(configDir);
        
        _logFile = Path.Combine(configDir, "logs", $"scraper_{DateTime.Now:yyyyMMdd}.log");
        
        var logDir = Path.GetDirectoryName(_logFile);
        if (!Directory.Exists(logDir))
            Directory.CreateDirectory(logDir!);
    }
    
    public bool EnableDebug
    {
        get => _enableDebug;
        set => _enableDebug = value;
    }
    
    public bool PersistToFile
    {
        get => _persistToFile;
        set => _persistToFile = value;
    }
    
    public int MaxLogEntries
    {
        get => _maxLogEntries;
        set => _maxLogEntries = value;
    }
    
    public IReadOnlyList<LogEntry> Logs
    {
        get
        {
            lock (_lock)
            {
                return _logs.ToList();
            }
        }
    }
    
    public void Info(string message) => AddLog(LogLevel.Info, message);
    public void Success(string message) => AddLog(LogLevel.Success, message);
    public void Warning(string message) => AddLog(LogLevel.Warning, message);
    public void Error(string message) => AddLog(LogLevel.Error, message);
    public void Progress(string message) => AddLog(LogLevel.Progress, message);
    public void Debug(string message) { if (_enableDebug) AddLog(LogLevel.Debug, message); }
    
    public void LogProcessStart(string processName)
    {
        Info($"Iniciando {processName}...");
        Info($"Data/Hora: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        Info($"Usuário: {Environment.UserName}");
        Info($"Máquina: {Environment.MachineName}");
    }
    
    public void LogProcessEnd(string processName, DateTime startTime)
    {
        var duration = DateTime.Now - startTime;
        Success($"{processName} finalizado!");
        Info($"Duração total: {duration:hh\\:mm\\:ss}");
    }
    
    public void LogProgress(int current, int total, string message = "")
    {
        var percent = (int)((double)current / total * 100);
        var progressMsg = string.IsNullOrEmpty(message) 
            ? $"Progresso: {current}/{total} ({percent}%)"
            : $"{message} - {current}/{total} ({percent}%)";
        Progress(progressMsg);
    }
    
    public void LogIdProcessing(int id, int current, int total)
    {
        var percent = (int)((double)current / total * 100);
        Progress($"Processando ID {id} ({current}/{total} - {percent}%)...");
    }
    
    public void LogIdSuccess(int id, int linesExtracted)
    {
        Success($"ID {id} extraído com sucesso! ({linesExtracted} linhas)");
    }
    
    public void LogIdWarning(int id, string reason)
    {
        Warning($"ID {id}: {reason}");
    }
    
    public void LogIdError(int id, string error)
    {
        Error($"ID {id}: {error}");
    }
    
    private void AddLog(LogLevel level, string message)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message
        };
        
        lock (_lock)
        {
            _logs.Add(entry);
            if (_logs.Count > _maxLogEntries)
            {
                var removeCount = _logs.Count - _maxLogEntries;
                _logs.RemoveRange(0, removeCount);
            }
        }
        
        WriteToConsole(entry);
        if (_persistToFile) WriteToFile(entry);
        
        // Disparar eventos - CORRIGIDO
        var eventArgs = new LogEventArgs
        {
            Message = message,
            Level = level,
            Timestamp = entry.Timestamp
        };
        OnLog?.Invoke(this, eventArgs);
        OnNewLog?.Invoke(eventArgs.FormattedMessage);
    }
    
    private void WriteToConsole(LogEntry entry)
    {
        try
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = _consoleColors.GetValueOrDefault(entry.Level, ConsoleColor.White);
            Console.WriteLine($"[{entry.Timestamp:HH:mm:ss}] {GetLevelEmoji(entry.Level)}{entry.Message}");
            Console.ForegroundColor = originalColor;
        }
        catch { }
    }
    
    private void WriteToFile(LogEntry entry)
    {
        try
        {
            var logLine = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] {GetLevelEmoji(entry.Level)}{entry.Message}";
            
            if (File.Exists(_logFile) && new FileInfo(_logFile).Length > 10 * 1024 * 1024)
            {
                RotateLogFile();
            }
            
            File.AppendAllText(_logFile, logLine + Environment.NewLine, Encoding.UTF8);
        }
        catch { }
    }
    
    private void RotateLogFile()
    {
        try
        {
            var logDir = Path.GetDirectoryName(_logFile);
            var logName = Path.GetFileNameWithoutExtension(_logFile);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var archiveFile = Path.Combine(logDir!, $"{logName}_{timestamp}.log");
            
            if (File.Exists(_logFile))
                File.Move(_logFile, archiveFile);
        }
        catch { }
    }
    
    public List<string> GetLogs(LogLevel? level = null, int limit = 100)
    {
        lock (_lock)
        {
            var query = _logs.AsEnumerable();
            if (level.HasValue) query = query.Where(l => l.Level == level.Value);
            return query.OrderByDescending(l => l.Timestamp).Take(limit).Select(l => l.ToString()).Reverse().ToList();
        }
    }
    
    public List<LogEntry> GetLogEntries(LogLevel? level = null, int limit = 100)
    {
        lock (_lock)
        {
            var query = _logs.AsEnumerable();
            if (level.HasValue) query = query.Where(l => l.Level == level.Value);
            return query.OrderByDescending(l => l.Timestamp).Take(limit).Reverse().ToList();
        }
    }
    
    public int GetLogCount(LogLevel? level = null)
    {
        lock (_lock)
        {
            if (level.HasValue) return _logs.Count(l => l.Level == level.Value);
            return _logs.Count;
        }
    }
    
    public string GetLogsText()
    {
        lock (_lock)
        {
            return string.Join(Environment.NewLine, _logs.Select(l => l.ToString()));
        }
    }
    
    public void Clear()
    {
        lock (_lock) { _logs.Clear(); }
        OnLogsCleared?.Invoke(new List<string>());
        Info("🗑️ Logs limpos");
    }
    
    public Dictionary<LogLevel, int> GetStats()
    {
        lock (_lock)
        {
            return _logs.GroupBy(l => l.Level).ToDictionary(g => g.Key, g => g.Count());
        }
    }
    
    public string GenerateReport()
    {
        var stats = GetStats();
        var sb = new StringBuilder();
        sb.AppendLine("=".PadRight(60, '='));
        sb.AppendLine("RELATÓRIO DE LOGS");
        sb.AppendLine("=".PadRight(60, '='));
        sb.AppendLine($"📅 Data: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($"📊 Total de logs: {_logs.Count}");
        sb.AppendLine();
        sb.AppendLine("📈 ESTATÍSTICAS:");
        sb.AppendLine($"   ✅ Sucessos: {stats.GetValueOrDefault(LogLevel.Success)}");
        sb.AppendLine($"   ⚠️ Alertas: {stats.GetValueOrDefault(LogLevel.Warning)}");
        sb.AppendLine($"   ❌ Erros: {stats.GetValueOrDefault(LogLevel.Error)}");
        sb.AppendLine($"   ℹ️ Informações: {stats.GetValueOrDefault(LogLevel.Info)}");
        sb.AppendLine($"   🔄 Progresso: {stats.GetValueOrDefault(LogLevel.Progress)}");
        if (_enableDebug) sb.AppendLine($"   🐛 Debug: {stats.GetValueOrDefault(LogLevel.Debug)}");
        sb.AppendLine();
        sb.AppendLine("📝 ÚLTIMOS 10 LOGS:");
        sb.AppendLine("-".PadRight(60, '-'));
        var lastLogs = GetLogs(limit: 10);
        foreach (var log in lastLogs) sb.AppendLine(log);
        sb.AppendLine("=".PadRight(60, '='));
        return sb.ToString();
    }
    
    public async Task ExportLogsToFileAsync(string filePath)
    {
        try
        {
            var content = GetLogsText();
            await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
            Success($"Logs exportados para: {filePath}");
        }
        catch (Exception ex)
        {
            Error($"Erro ao exportar logs: {ex.Message}");
        }
    }
    
    private string GetLevelEmoji(LogLevel level)
    {
        return level switch
        {
            LogLevel.Success => "✅ ",
            LogLevel.Warning => "⚠️ ",
            LogLevel.Error => "❌ ",
            LogLevel.Progress => "🔄 ",
            LogLevel.Debug => "🐛 ",
            _ => "ℹ️ "
        };
    }
    
    public void Flush() { }
    public void Dispose() => Flush();
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    
    private string GetLevelEmoji()
    {
        return Level switch
        {
            LogLevel.Success => "✅ ",
            LogLevel.Warning => "⚠️ ",
            LogLevel.Error => "❌ ",
            LogLevel.Progress => "🔄 ",
            LogLevel.Debug => "🐛 ",
            _ => "ℹ️ "
        };
    }
    
    public override string ToString()
    {
        return $"[{Timestamp:HH:mm:ss}] {GetLevelEmoji()}{Message}";
    }
}