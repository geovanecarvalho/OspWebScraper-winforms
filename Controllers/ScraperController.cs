using OSPVivoScraper.Models;
using OSPVivoScraper.Services;
using ClosedXML.Excel;
using CsvHelper;
using System.Globalization;

namespace OSPVivoScraper.Controllers;

public class ScraperController
{
    private readonly ConfigService _configService;
    private readonly SessionService _sessionService;
    private readonly LogService _logService;
    private PlaywrightScraperService? _scraperService;
    
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private bool _stopRequested = false;
    private List<object> _resultadosParciais = new();
    private ScrapingMode _currentMode;
    
    // Eventos
    public event Action<string>? OnLog;
    public event Action<int, string>? OnProgress;
    public event Action<bool>? OnRunningStateChanged;
    public event Action<string>? OnDataSaved;
    public event Action<string>? OnPartialDataSaved;
    
    public bool IsRunning => _isRunning;
    
    private DateTime _startTime;
    private int _totalIds;
    private int _processedIds;
    
    public event Action<TimeSpan, TimeSpan?>? OnTimeUpdate; // (tempoDecorrido, tempoRestante)

    public ScraperController(ConfigService configService, SessionService sessionService, LogService logService)
    {
        _configService = configService;
        _sessionService = sessionService;
        _logService = logService;
        _logService.OnNewLog += msg => OnLog?.Invoke(msg);
    }
    
    public AppConfig LoadConfig()
    {
        return _configService.Carregar();
    }
    
    public void SaveConfig(AppConfig config)
    {
        _configService.Salvar(config);
        _logService.Success("Configurações salvas");
    }
    
    public string GetConfigDir()
    {
        return _configService.ConfigDir;
    }
    
    public List<string> GetCsvIds(string csvPath)
    {
        var ids = new List<string>();
        
        try
        {
            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            
            csv.Read();
            csv.ReadHeader();
            
            var hasIdColumn = csv.HeaderRecord?.Any(h => h.Equals("ID", StringComparison.OrdinalIgnoreCase)) == true;
            
            if (!hasIdColumn)
            {
                throw new Exception("CSV deve conter uma coluna 'ID'");
            }
            
            while (csv.Read())
            {
                var id = csv.GetField("ID");
                if (!string.IsNullOrEmpty(id))
                {
                    ids.Add(id);
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Error($"Erro ao ler CSV: {ex.Message}");
            throw;
        }
        
        return ids;
    }
    
    public async Task StartScrapingAsync(
    string csvPath, 
    ScrapingMode mode, 
    string username, 
    string password, 
    bool headless,
    CancellationToken cancellationToken)
    {
        if (_isRunning)
        {
            _logService.Warning("Já existe um processo em execução");
            return;
        }
        
        _isRunning = true;
        _stopRequested = false;
        _resultadosParciais.Clear();
        _currentMode = mode;
        OnRunningStateChanged?.Invoke(true);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        string? arquivoGerado = null;
        _startTime = DateTime.Now;  // Marcar início
        
        try
        {
            _logService.LogProcessStart("Web Scraper");
            _logService.Info($"Modo: {mode}");
            _logService.Info($"CSV: {csvPath}");
            _logService.Info($"Headless: {headless}");
            
            _scraperService = new PlaywrightScraperService(_logService, _sessionService);
            
            OnProgress?.Invoke(5, "Inicializando navegador...");
            await _scraperService.InitializeAsync(headless, username, password);
            
            OnProgress?.Invoke(10, "Navegador inicializado. Processando dados...");
            
            var ids = GetCsvIds(csvPath);
            _totalIds = ids.Count;
            _processedIds = 0;
            _logService.Info($"Total de IDs a processar: {_totalIds}");
            
            int total = ids.Count;
            
            for (int i = 0; i < total; i++)
            {
                if (_stopRequested || _cts.Token.IsCancellationRequested)
                {
                    _logService.Warning("Parada solicitada. Salvando dados parciais...");
                    
                    if (_resultadosParciais.Count > 0)
                    {
                        arquivoGerado = await SalvarResultadosParciaisAsync(_resultadosParciais, mode);
                        OnPartialDataSaved?.Invoke(arquivoGerado);
                        _logService.Success($"Dados parciais salvos: {_resultadosParciais.Count} registros");
                    }
                    else
                    {
                        _logService.Warning("Nenhum dado parcial para salvar");
                    }
                    break;
                }
                
                _cts.Token.ThrowIfCancellationRequested();
                
                var id = int.Parse(ids[i]);
                _processedIds = i + 1;
                
                // Calcular progresso e tempo
                var progress = (int)((double)_processedIds / _totalIds * 100);
                var percentComplete = 10 + (progress * 85 / 100);
                
                var elapsed = DateTime.Now - _startTime;
                var avgTimePerId = elapsed.TotalSeconds / _processedIds;
                var remainingIds = _totalIds - _processedIds;
                var remaining = TimeSpan.FromSeconds(avgTimePerId * remainingIds);
                
                // Disparar evento de tempo
                System.Diagnostics.Debug.WriteLine($"Disparando OnTimeUpdate: elapsed={elapsed}, remaining={remaining}");
                OnTimeUpdate?.Invoke(elapsed, remaining);
                

                OnProgress?.Invoke(percentComplete, $"Processando ID {id} ({_processedIds}/{_totalIds})... - Estimativa: {FormatTimeSpan(remaining)}");
                _logService.LogIdProcessing(id, _processedIds, _totalIds);
                
                try
                {
                    switch (mode)
                    {
                        case ScrapingMode.Draft:
                            var draftResults = await _scraperService.ScrapDraftAsync(id);
                            foreach (var r in draftResults) _resultadosParciais.Add(r);
                            _logService.LogIdSuccess(id, draftResults.Count);
                            break;
                        case ScrapingMode.Medicao:
                            var medicaoResults = await _scraperService.ScrapMedicaoAsync(id);
                            foreach (var r in medicaoResults) _resultadosParciais.Add(r);
                            _logService.LogIdSuccess(id, medicaoResults.Count);
                            break;
                        case ScrapingMode.IdCancelados:
                            var canceladosResults = await _scraperService.ScrapIdCanceladoAsync(id);
                            foreach (var r in canceladosResults) _resultadosParciais.Add(r);
                            _logService.LogIdSuccess(id, canceladosResults.Count);
                            break;
                        case ScrapingMode.MemoriaCalculoDraft:
                            var memoriaResults = await _scraperService.ScrapMemoriaCalculoDraftAsync(id);
                            foreach (var r in memoriaResults) _resultadosParciais.Add(r);
                            _logService.LogIdSuccess(id, memoriaResults.Count);
                            break;
                        case ScrapingMode.MemoriaCalculoMedicao:
                            var memoriaMedicaoResults = await _scraperService.ScrapMemoriaCalculoMedicaoAsync(id);
                            foreach (var r in memoriaMedicaoResults) _resultadosParciais.Add(r);
                            _logService.LogIdSuccess(id, memoriaMedicaoResults.Count);
                            break;
                        default:
                            _logService.Warning($"Modo {mode} não implementado");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogIdError(id, ex.Message);
                }
            }
            
            var totalElapsed = DateTime.Now - _startTime;
            
            if (!_stopRequested && !_cts.Token.IsCancellationRequested)
            {
                OnProgress?.Invoke(95, "Salvando resultados...");
                arquivoGerado = await SalvarResultadosAsync(_resultadosParciais, mode);
                
                OnProgress?.Invoke(100, "Processo concluído!");
                _logService.LogProcessEnd("Web Scraper", _startTime);
                _logService.Success($"Arquivo salvo em: {arquivoGerado}");
                _logService.Info($"Tempo total de processamento: {FormatTimeSpan(totalElapsed)}");
                
                // Disparar evento com tempo final
                OnTimeUpdate?.Invoke(totalElapsed, TimeSpan.Zero);
                
                _logService.Info("Fechando navegador...");
                if (_scraperService != null)
                {
                    await _scraperService.DisposeAsync();
                    _scraperService = null;
                }
                
                OnDataSaved?.Invoke(arquivoGerado);
            }
        }
        catch (OperationCanceledException)
        {
            _logService.Warning("Processo cancelado pelo usuário");
            
            if (_resultadosParciais.Count > 0)
            {
                arquivoGerado = await SalvarResultadosParciaisAsync(_resultadosParciais, mode);
                OnPartialDataSaved?.Invoke(arquivoGerado);
            }
        }
        catch (Exception ex)
        {
            _logService.Error($"Erro no scraping: {ex.Message}");
            
            if (_resultadosParciais.Count > 0)
            {
                arquivoGerado = await SalvarResultadosParciaisAsync(_resultadosParciais, mode);
                OnPartialDataSaved?.Invoke(arquivoGerado);
            }
            throw;
        }
        finally
        {
            if (_scraperService != null)
            {
                await _scraperService.DisposeAsync();
                _scraperService = null;
            }
            
            _isRunning = false;
            OnRunningStateChanged?.Invoke(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    // Método auxiliar para formatar TimeSpan
    private string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{ts.Hours}h {ts.Minutes}min {ts.Seconds}s";
        if (ts.TotalMinutes >= 1)
            return $"{ts.Minutes}min {ts.Seconds}s";
        return $"{ts.Seconds}s";
    }
    
    public void StopScraping()
    {
        if (_isRunning && _cts != null)
        {
            _logService.Warning("Solicitando parada... Salvando dados parciais...");
            _stopRequested = true;
            _cts.Cancel();
            _scraperService?.Stop();
        }
    }
    
    private string GerarNomeArquivo(ScrapingMode mode, bool isPartial = false)
    {
        var downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        var timestamp = DateTime.Now.ToString("dd-MM-yyyy_HH'h'MM'm'ss's'");
        var parcial = isPartial ? "_parcial" : "";
        
        string nomeArquivo = mode switch
        {
            ScrapingMode.Draft => $"osp_vivo_draft{parcial}_{timestamp}.xlsx",
            ScrapingMode.Medicao => $"osp_vivo_medicao{parcial}_{timestamp}.xlsx",
            ScrapingMode.IdCancelados => $"osp_id_cancelado{parcial}_{timestamp}.xlsx",
            ScrapingMode.MemoriaCalculoDraft => $"osp_memoria_calculo_draft{parcial}_{timestamp}.xlsx",
            ScrapingMode.MemoriaCalculoMedicao => $"osp_memoria_calculo_medicao{parcial}_{timestamp}.xlsx",
            _ => $"osp_vivo_export{parcial}_{timestamp}.xlsx"
        };
        
        return Path.Combine(downloadPath, nomeArquivo);
    }
    
    private async Task<string> SalvarResultadosAsync(List<object> resultados, ScrapingMode mode)
    {
        var filePath = GerarNomeArquivo(mode, false);
        
        try
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Dados");
            
            if (mode == ScrapingMode.Draft || mode == ScrapingMode.Medicao)
            {
                worksheet.Cell(1, 1).Value = "ID";
                worksheet.Cell(1, 2).Value = "TIPO DE REGISTRO";
                worksheet.Cell(1, 3).Value = "CÓDIGO";
                worksheet.Cell(1, 4).Value = "DESCRIÇÃO";
                worksheet.Cell(1, 5).Value = "QUANTIDADE";
                worksheet.Cell(1, 6).Value = "PREÇO UNITÁRIO";
                worksheet.Cell(1, 7).Value = "UNIDADE";
                worksheet.Cell(1, 8).Value = "PREÇO TOTAL";
                worksheet.Cell(1, 9).Value = "CATEGORIA";
                worksheet.Cell(1, 10).Value = "STATUS";
                
                var headerRange = worksheet.Range(1, 1, 1, 10);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                
                var results = resultados.Cast<ScrapingResult>().ToList();
                for (int i = 0; i < results.Count; i++)
                {
                    var row = i + 2;
                    worksheet.Cell(row, 1).Value = results[i].Id;
                    worksheet.Cell(row, 2).Value = results[i].TipoRegistro;
                    worksheet.Cell(row, 3).Value = results[i].Codigo;
                    worksheet.Cell(row, 4).Value = results[i].Descricao;
                    worksheet.Cell(row, 5).Value = results[i].Quantidade;
                    worksheet.Cell(row, 6).Value = results[i].PrecoUnitario;
                    worksheet.Cell(row, 7).Value = results[i].Unidade;
                    worksheet.Cell(row, 8).Value = results[i].PrecoTotal;
                    worksheet.Cell(row, 9).Value = results[i].Categoria;
                    worksheet.Cell(row, 10).Value = results[i].Status;
                }
            }
            else if (mode == ScrapingMode.MemoriaCalculoDraft || mode == ScrapingMode.MemoriaCalculoMedicao)
            {
                worksheet.Cell(1, 1).Value = "ID";
                worksheet.Cell(1, 2).Value = "CLASSE";
                worksheet.Cell(1, 3).Value = "CODIGO";
                worksheet.Cell(1, 4).Value = "DESCRIÇÃO DO SERVIÇO";
                worksheet.Cell(1, 5).Value = "UNIDADE";
                worksheet.Cell(1, 6).Value = "PONTOS";
                worksheet.Cell(1, 7).Value = "CUSTO UNITÁRIO (R$)";
                worksheet.Cell(1, 8).Value = "QUANTIDADE EXECUTADA";
                worksheet.Cell(1, 9).Value = "PONTOS TOTAIS";
                worksheet.Cell(1, 10).Value = "CUSTO TOTAL (R$)";
                worksheet.Cell(1, 11).Value = "STATUS";
                
                var headerRange = worksheet.Range(1, 1, 1, 11);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                
                var results = resultados.Cast<MemoriaCalculoResult>().ToList();
                for (int i = 0; i < results.Count; i++)
                {
                    var row = i + 2;
                    worksheet.Cell(row, 1).Value = results[i].Id;
                    worksheet.Cell(row, 2).Value = results[i].Classe;
                    worksheet.Cell(row, 3).Value = results[i].Codigo;
                    worksheet.Cell(row, 4).Value = results[i].DescricaoServico;
                    worksheet.Cell(row, 5).Value = results[i].Unidade;
                    worksheet.Cell(row, 6).Value = results[i].Pontos;
                    worksheet.Cell(row, 7).Value = results[i].CustoUnitario;
                    worksheet.Cell(row, 8).Value = results[i].QuantidadeExecutada;
                    worksheet.Cell(row, 9).Value = results[i].PontosTotais;
                    worksheet.Cell(row, 10).Value = results[i].CustoTotal;
                    worksheet.Cell(row, 11).Value = results[i].Status;
                }
            }
            else if (mode == ScrapingMode.IdCancelados)
            {
                worksheet.Cell(1, 1).Value = "ID";
                worksheet.Cell(1, 2).Value = "CONTRATO";
                worksheet.Cell(1, 3).Value = "OSP";
                worksheet.Cell(1, 4).Value = "STATUS";
                
                var headerRange = worksheet.Range(1, 1, 1, 4);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                
                var results = resultados.Cast<IdCanceladoResult>().ToList();
                for (int i = 0; i < results.Count; i++)
                {
                    var row = i + 2;
                    worksheet.Cell(row, 1).Value = results[i].Id;
                    worksheet.Cell(row, 2).Value = results[i].Contrato;
                    worksheet.Cell(row, 3).Value = results[i].Osp;
                    worksheet.Cell(row, 4).Value = results[i].Status;
                }
            }
            
            worksheet.Columns().AdjustToContents();
            await Task.Run(() => workbook.SaveAs(filePath));
            _logService.Success($"Arquivo salvo: {filePath}");
        }
        catch (Exception ex)
        {
            _logService.Error($"Erro ao salvar arquivo: {ex.Message}");
            throw;
        }
        
        return filePath;
    }
    
    private async Task<string> SalvarResultadosParciaisAsync(List<object> resultados, ScrapingMode mode)
    {
        var filePath = GerarNomeArquivo(mode, true);
        
        try
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Dados (Parcial)");
            
            if (mode == ScrapingMode.Draft || mode == ScrapingMode.Medicao)
            {
                worksheet.Cell(1, 1).Value = "ID";
                worksheet.Cell(1, 2).Value = "TIPO DE REGISTRO";
                worksheet.Cell(1, 3).Value = "CÓDIGO";
                worksheet.Cell(1, 4).Value = "DESCRIÇÃO";
                worksheet.Cell(1, 5).Value = "QUANTIDADE";
                worksheet.Cell(1, 6).Value = "PREÇO UNITÁRIO";
                worksheet.Cell(1, 7).Value = "UNIDADE";
                worksheet.Cell(1, 8).Value = "PREÇO TOTAL";
                worksheet.Cell(1, 9).Value = "CATEGORIA";
                worksheet.Cell(1, 10).Value = "STATUS";
                
                var headerRange = worksheet.Range(1, 1, 1, 10);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                
                var results = resultados.Cast<ScrapingResult>().ToList();
                for (int i = 0; i < results.Count; i++)
                {
                    var row = i + 2;
                    worksheet.Cell(row, 1).Value = results[i].Id;
                    worksheet.Cell(row, 2).Value = results[i].TipoRegistro;
                    worksheet.Cell(row, 3).Value = results[i].Codigo;
                    worksheet.Cell(row, 4).Value = results[i].Descricao;
                    worksheet.Cell(row, 5).Value = results[i].Quantidade;
                    worksheet.Cell(row, 6).Value = results[i].PrecoUnitario;
                    worksheet.Cell(row, 7).Value = results[i].Unidade;
                    worksheet.Cell(row, 8).Value = results[i].PrecoTotal;
                    worksheet.Cell(row, 9).Value = results[i].Categoria;
                    worksheet.Cell(row, 10).Value = results[i].Status;
                }
                
                // Adicionar nota informando que é um arquivo parcial
                var lastRow = results.Count + 2;
                worksheet.Cell(lastRow + 2, 1).Value = "ARQUIVO PARCIAL - Processo foi interrompido antes da conclusão completa";
                worksheet.Cell(lastRow + 2, 1).Style.Font.Bold = true;
                worksheet.Cell(lastRow + 2, 1).Style.Font.FontColor = XLColor.Red;
            }
            else if (mode == ScrapingMode.MemoriaCalculoDraft || mode == ScrapingMode.MemoriaCalculoMedicao)
            {
                worksheet.Cell(1, 1).Value = "ID";
                worksheet.Cell(1, 2).Value = "CLASSE";
                worksheet.Cell(1, 3).Value = "CODIGO";
                worksheet.Cell(1, 4).Value = "DESCRIÇÃO DO SERVIÇO";
                worksheet.Cell(1, 5).Value = "UNIDADE";
                worksheet.Cell(1, 6).Value = "PONTOS";
                worksheet.Cell(1, 7).Value = "CUSTO UNITÁRIO (R$)";
                worksheet.Cell(1, 8).Value = "QUANTIDADE EXECUTADA";
                worksheet.Cell(1, 9).Value = "PONTOS TOTAIS";
                worksheet.Cell(1, 10).Value = "CUSTO TOTAL (R$)";
                worksheet.Cell(1, 11).Value = "STATUS";
                
                var headerRange = worksheet.Range(1, 1, 1, 11);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                
                var results = resultados.Cast<MemoriaCalculoResult>().ToList();
                for (int i = 0; i < results.Count; i++)
                {
                    var row = i + 2;
                    worksheet.Cell(row, 1).Value = results[i].Id;
                    worksheet.Cell(row, 2).Value = results[i].Classe;
                    worksheet.Cell(row, 3).Value = results[i].Codigo;
                    worksheet.Cell(row, 4).Value = results[i].DescricaoServico;
                    worksheet.Cell(row, 5).Value = results[i].Unidade;
                    worksheet.Cell(row, 6).Value = results[i].Pontos;
                    worksheet.Cell(row, 7).Value = results[i].CustoUnitario;
                    worksheet.Cell(row, 8).Value = results[i].QuantidadeExecutada;
                    worksheet.Cell(row, 9).Value = results[i].PontosTotais;
                    worksheet.Cell(row, 10).Value = results[i].CustoTotal;
                    worksheet.Cell(row, 11).Value = results[i].Status;
                }
                
                // Adicionar nota informando que é um arquivo parcial
                var lastRow = results.Count + 2;
                worksheet.Cell(lastRow + 2, 1).Value = "ARQUIVO PARCIAL - Processo foi interrompido antes da conclusão completa";
                worksheet.Cell(lastRow + 2, 1).Style.Font.Bold = true;
                worksheet.Cell(lastRow + 2, 1).Style.Font.FontColor = XLColor.Red;
            }
            else if (mode == ScrapingMode.IdCancelados)
            {
                worksheet.Cell(1, 1).Value = "ID";
                worksheet.Cell(1, 2).Value = "CONTRATO";
                worksheet.Cell(1, 3).Value = "OSP";
                worksheet.Cell(1, 4).Value = "STATUS";
                
                var headerRange = worksheet.Range(1, 1, 1, 4);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                
                var results = resultados.Cast<IdCanceladoResult>().ToList();
                for (int i = 0; i < results.Count; i++)
                {
                    var row = i + 2;
                    worksheet.Cell(row, 1).Value = results[i].Id;
                    worksheet.Cell(row, 2).Value = results[i].Contrato;
                    worksheet.Cell(row, 3).Value = results[i].Osp;
                    worksheet.Cell(row, 4).Value = results[i].Status;
                }
                
                // Adicionar nota informando que é um arquivo parcial
                var lastRow = results.Count + 2;
                worksheet.Cell(lastRow + 2, 1).Value = "ARQUIVO PARCIAL - Processo foi interrompido antes da conclusão completa";
                worksheet.Cell(lastRow + 2, 1).Style.Font.Bold = true;
                worksheet.Cell(lastRow + 2, 1).Style.Font.FontColor = XLColor.Red;
            }
            
            worksheet.Columns().AdjustToContents();
            
            await Task.Run(() => workbook.SaveAs(filePath));
            _logService.Success($"Arquivo parcial salvo: {filePath}");
        }
        catch (Exception ex)
        {
            _logService.Error($"Erro ao salvar arquivo parcial: {ex.Message}");
            throw;
        }
        
        return filePath;
    }
}