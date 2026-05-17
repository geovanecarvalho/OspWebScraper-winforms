using Microsoft.Playwright;
using OSPVivoScraper.Models;
using System.Text.RegularExpressions;

namespace OSPVivoScraper.Services;

public class PlaywrightScraperService : IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;
    
    private readonly SessionService _sessionService;
    private readonly LogService _logService;
    
    private readonly string _loginUrl = "https://devopsredes.vivo.com.br/ospcontrol/home";
    private readonly string _loggedSelector = "#ott-username";
    
    private bool _isRunning = true;
    private bool _isLogged = false;
    
    public bool IsLogged => _isLogged;
    
    private bool _modoHeadlessOriginal = false;
    public PlaywrightScraperService(LogService logService, SessionService sessionService)
    {
        _logService = logService;
        _sessionService = sessionService;
    }
    
    public async Task InitializeAsync(bool headless, string? username = null, string? password = null)
    {
        _logService.Info($"Iniciando navegador (Headless: {(headless ? "Sim" : "Não")})...");
        
        _playwright = await Playwright.CreateAsync();

        _modoHeadlessOriginal = headless;
        
        bool startHeadless = headless;
        bool hasSession = _sessionService.HasSession();
        
        // Se estiver em modo headless e não tiver sessão, inicia visível para login
        if (startHeadless && !hasSession)
        {
            _logService.Warning("Modo Headless ativo, mas sem sessão salva. Iniciando visível para login...");
            startHeadless = false;
        }
        
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = startHeadless,
            Channel = "chrome",
            Args = new[] { "--ignore-certificate-errors" }
        });
        
        var contextOptions = new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
        };
        
        // Carregar storage state se existir
        var storageState = _sessionService.CarregarStorageState();
        if (!string.IsNullOrEmpty(storageState))
        {
            _logService.Info("Carregando sessão existente...");
            contextOptions.StorageState = storageState;
        }
        else
        {
            _logService.Info("Criando nova sessão...");
        }
        
        _context = await _browser.NewContextAsync(contextOptions);
        _page = await _context.NewPageAsync();
        
        // Navegar para a página
        await _page.GotoAsync(_loginUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        
        // Verificar login
        _isLogged = await IsLoggedAsync();
        
        if (!_isLogged)
        {
            await PerformLoginAsync(username, password, startHeadless);
        }
        else
        {
            _logService.Success("Já está logado!");
        }
    }
    
    private async Task<bool> IsLoggedAsync()
    {
        try
        {
            await _page!.WaitForSelectorAsync(_loggedSelector, new PageWaitForSelectorOptions { Timeout = 5000 });
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    private async Task PerformLoginAsync(string? username, string? password, bool wasHeadless)
    {
        _logService.Warning("Sessão expirada. Aguardando login manual...");
        
        bool estavaEmHeadless = wasHeadless;
        bool navegadorAbertoParaLogin = false;
        
        // Se estava em headless e precisa de login, reinicia em modo visível
        if (wasHeadless && !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            _logService.Warning("Login necessário! Reiniciando navegador em modo visível para login...");
            
            // Fechar o navegador headless atual
            await _browser!.CloseAsync();
            
            // Reiniciar navegador em modo VISÍVEL
            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                Channel = "chrome",
                Args = new[] { "--ignore-certificate-errors" }
            });
            
            _context = await _browser.NewContextAsync();
            _page = await _context.NewPageAsync();
            await _page.GotoAsync(_loginUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            
            navegadorAbertoParaLogin = true;
            _logService.Info("✅ Navegador visível aberto para login. Faça o login manualmente.");
            
            // Verificar se realmente está visível
            bool isVisible = await IsBrowserVisible();
            _logService.Info($"Navegador está visível: {isVisible}");
        }
        
        // Injetar mensagem de overlay se tiver credenciais
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            await _page!.EvaluateAsync("""
                () => {
                    let div = document.getElementById('scraping-msg-overlay');
                    if (!div) {
                        div = document.createElement('div');
                        div.id = 'scraping-msg-overlay';
                        div.style.position = 'fixed';
                        div.style.top = '0';
                        div.style.left = '0';
                        div.style.width = '100%';
                        div.style.textAlign = 'center';
                        div.style.zIndex = '99999';
                        div.style.padding = '15px';
                        div.style.fontSize = '18px';
                        div.style.fontWeight = 'bold';
                        document.body.prepend(div);
                    }
                    div.style.backgroundColor = '#fff3cd';
                    div.style.color = '#856404';
                    div.style.borderBottom = '2px solid #ffeeba';
                    div.innerText = '🤖 O Robô irá preencher as credenciais... Por favor, aguarde!';
                }
            """);
        }
        
        // Loop até detectar login
        while (_isRunning && !await IsLoggedAsync())
        {
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                await TryAutoFillCredentialsAsync(username, password);
            }
            await Task.Delay(1000);
        }
        
        if (_isRunning)
        {
            _logService.Success("Login detectado! Salvando sessão...");
            await SaveSessionAsync();
            _isLogged = true;
            
            // 🔑 Se o navegador foi aberto visível para login, voltar para headless
            if (navegadorAbertoParaLogin && wasHeadless)
            {
                _logService.Info("Login concluído. Alternando navegador para modo HEADLESS...");
                
                // Salvar o storage state antes de fechar
                var storageState = await _context!.StorageStateAsync();
                var storageStateJson = storageState.ToString();
                _sessionService.SalvarStorageState(storageStateJson);
                
                // Fechar navegador visível
                await _browser!.CloseAsync();
                
                // Reiniciar navegador em modo HEADLESS
                _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true,
                    Channel = "chrome",
                    Args = new[] { "--ignore-certificate-errors" }
                });
                
                // Criar novo contexto com o storage state salvo
                var contextOptions = new BrowserNewContextOptions
                {
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                    ViewportSize = new ViewportSize { Width = 1280, Height = 720 },
                    StorageState = storageStateJson
                };
                
                _context = await _browser.NewContextAsync(contextOptions);
                _page = await _context.NewPageAsync();
                
                // Navegar para a página inicial
                await _page.GotoAsync(_loginUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
                
                // Verificar se voltou para headless
                bool isHeadlessAgain = !await IsBrowserVisible();
                _logService.Info($"Navegador está em modo headless: {isHeadlessAgain}");
                _logService.Success("✅ Navegador agora está em modo HEADLESS!");
            }
        }
    }
            
    private async Task TryAutoFillCredentialsAsync(string username, string password)
    {
        try
        {
            var usernameField = _page!.Locator("#username");
            if (await usernameField.IsVisibleAsync())
            {
                var userValue = await usernameField.InputValueAsync();
                var passValue = await _page.Locator("#password").InputValueAsync();
                
                if (string.IsNullOrEmpty(userValue) && string.IsNullOrEmpty(passValue))
                {
                    _logService.Info("Preenchendo credenciais...");
                    
                    await Task.Delay(3000);
                    
                    await usernameField.FillAsync("");
                    await usernameField.PressSequentiallyAsync(username, new LocatorPressSequentiallyOptions { Delay = 100 });
                    
                    await _page.Locator("#password").FillAsync("");
                    await _page.Locator("#password").PressSequentiallyAsync(password, new LocatorPressSequentiallyOptions { Delay = 100 });
                    
                    await _page.EvaluateAsync("""
                        () => {
                            let div = document.getElementById('scraping-msg-overlay');
                            if(div) {
                                div.style.backgroundColor = '#d4edda';
                                div.style.color = '#155724';
                                div.style.borderBottom = '2px solid #c3e6cb';
                                div.innerText = '⚠️ AÇÃO NECESSÁRIA: Preencha o CAPTCHA e clique em Entrar!';
                            }
                        }
                    """);
                    
                    _logService.Success("Credenciais preenchidas.");
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Debug($"Erro ao preencher credenciais: {ex.Message}");
        }
    }
    
    private async Task SaveSessionAsync()
    {
        try
        {
            var storageState = await _context!.StorageStateAsync();
            _sessionService.SalvarStorageState(storageState.ToString());
            _logService.Success($"Sessão salva em: {_sessionService.StorageFile}");
        }
        catch (Exception ex)
        {
            _logService.Error($"Erro ao salvar sessão: {ex.Message}");
        }
    }
    
    private async Task<bool> IsBrowserVisible()
    {
        try
        {
            // Verificar se o navegador está em modo headless
            var isHeadless = await _page!.EvaluateAsync<bool>("() => navigator.webdriver === true");
            return !isHeadless;
        }
        catch
        {
            return false;
        }
    }

    // ===========================================================
    // MÉTODOS DE SCRAPING (Draft, Memória de Cálculo, etc.)
    // ===========================================================
    
    private string NormalizeText(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = Regex.Replace(s.Trim(), @"\s+", " ");
        return s.Normalize(System.Text.NormalizationForm.FormKD).ToLowerInvariant();
    }
    
    private string DetermineTipoRegistro(string categoria, string unidade = "", string descricao = "")
    {
        var catNorm = NormalizeText(categoria);
        
        if (catNorm.Contains("material") || catNorm.Contains("materiais") || catNorm.Contains("telefonica"))
            return "Material";
        if (catNorm.Contains("custo") || catNorm.Contains("custos"))
            return "Custo";
        if (catNorm.Contains("servico") || catNorm.Contains("servicos") || catNorm.Contains("classe") || catNorm.Contains("valor"))
            return "Serviço";
        
        var unidadeLower = unidade.ToLower();
        var descricaoLower = descricao.ToLower();
        
        if (unidadeLower.Contains("m") || unidadeLower.Contains("u") || unidadeLower.Contains("cj") ||
            unidadeLower.Contains("un") || unidadeLower.Contains("metro") || unidadeLower.Contains("kg"))
            return "Material";
        
        if (descricaoLower.Contains("cfo") || descricaoLower.Contains("chassi") || descricaoLower.Contains("conj") ||
            descricaoLower.Contains("subduto") || descricaoLower.Contains("material") || descricaoLower.Contains("cabos") ||
            descricaoLower.Contains("fibra"))
            return "Material";
        
        return "Serviço";
    }
    
    private async Task<string> ExtractCategoryFromTable(ILocator table)
    {
        try
        {
            var categoria = await table.EvaluateAsync<string>("""
                el => {
                    function findPrevText(e){
                        let node = e.previousElementSibling;
                        while(node){
                            const txt = node.innerText ? node.innerText.trim() : '';
                            if(txt) return txt.replace(/\\s+/g,' ');
                            node = node.previousElementSibling;
                        }
                        return '';
                    }
                    return findPrevText(el);
                }
            """);
            return categoria?.Trim() ?? "";
        }
        catch
        {
            return "";
        }
    }
    
    private async Task<string> ExtractStatusAsync(int idValue)
    {
        try
        {
            await _page!.WaitForSelectorAsync("table tbody tr", new PageWaitForSelectorOptions { Timeout = 8000 });
            
            var table = _page.Locator("table").First;
            if (await _page.Locator(".tab-pane.active table").CountAsync() > 0)
                table = _page.Locator(".tab-pane.active table").First;
            
            var headers = await table.Locator("thead th").AllAsync();
            int statusIndex = -1;
            
            for (int i = 0; i < headers.Count; i++)
            {
                var headerText = await headers[i].TextContentAsync();
                if (headerText?.ToLower().Contains("status") == true ||
                    headerText?.ToLower().Contains("situacao") == true)
                {
                    statusIndex = i;
                    break;
                }
            }
            
            if (statusIndex == -1)
            {
                _logService.Warning($"ID {idValue}: Cabeçalho 'Status' não encontrado, tentando coluna 13.");
                var statusCell = table.Locator("tbody tr:first-child td:nth-child(13)");
                var status = await statusCell.TextContentAsync();
                return status?.Trim() ?? "CÉLULA VAZIA/NÃO ENCONTRADA";
            }
            
            var colIndex = statusIndex + 1;
            var statusLocator = table.Locator($"tbody tr:first-child td:nth-child({colIndex})");
            var statusText = await statusLocator.TextContentAsync();
            
            _logService.Info($"ID {idValue}: Status encontrado: '{statusText?.Trim()}'");
            return statusText?.Trim() ?? "STATUS NÃO ENCONTRADO";
        }
        catch (Exception ex)
        {
            _logService.Error($"Erro ao ler status para ID {idValue}: {ex.Message}");
            return "STATUS NÃO ENCONTRADO";
        }
    }
    
    private async Task NavigateToSearchAsync(int idValue)
    {
        await _page!.ClickAsync("//*[@id=\"ott-sidebar-collapse\"]");
        await Task.Delay(500);
        await _page.ClickAsync("//*[@id=\"ott-sidebar\"]/div[3]/ul/li[3]/a");
        await _page.WaitForSelectorAsync("//*[@id=\"filtroId\"]");
        await _page.FillAsync("//*[@id=\"filtroId\"]", idValue.ToString());
        await _page.Locator("a.btn.btn-primary.btn-sm.btn-block:has-text('Buscar')").ClickAsync();
        await Task.Delay(1000);
    }
    
    private async Task ReturnToMainMenuAsync()
    {
        await _page!.ClickAsync("//*[@id=\"ott-sidebar-collapse\"]");
        await Task.Delay(500);
        await _page.ClickAsync("//*[@id=\"ott-sidebar\"]/div[3]/ul/li[1]/a");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
    
    public async Task<List<ScrapingResult>> ScrapDraftAsync(int idValue)
    {
        var results = new List<ScrapingResult>();
        var status = "";
        
        await NavigateToSearchAsync(idValue);
        
        bool editClicked = false;
        for (int tentativa = 1; tentativa <= 3; tentativa++)
        {
            _logService.Progress($"ID {idValue}: Tentativa {tentativa} de 3 - Verificando botão Editar...");
            
            try
            {
                status = await ExtractStatusAsync(idValue);
                _logService.Info($"ID {idValue}: Status encontrado: '{status}'");
                
                var editButton = _page!.Locator("span.badge.bg-primary").Filter(new LocatorFilterOptions { HasText = "Editar" });
                if (await editButton.CountAsync() > 0)
                {
                    await editButton.ClickAsync();
                    _logService.Success($"ID {idValue}: Botão Editar clicado com sucesso");
                    editClicked = true;
                    break;
                }
                else
                {
                    _logService.Warning($"ID {idValue}: Botão Editar não encontrado");
                    if (tentativa == 3)
                    {
                        results.Add(new ScrapingResult { Id = idValue, Status = $"Botão Editar não encontrado! Status: {status}" });
                        await ReturnToMainMenuAsync();
                        return results;
                    }
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                _logService.Error($"ID {idValue}: Tentativa {tentativa} falhou: {ex.Message}");
                if (tentativa == 3)
                {
                    results.Add(new ScrapingResult { Id = idValue, Status = $"Erro: {ex.Message}" });
                    await ReturnToMainMenuAsync();
                    return results;
                }
                await Task.Delay(1000);
            }
        }
        
        if (!editClicked)
        {
            await ReturnToMainMenuAsync();
            return results;
        }
        
        try
        {
            await Task.Delay(1500);
            
            var draft = _page!.GetByText("Draft", new PageGetByTextOptions { Exact = true });
            await draft.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });
            await draft.ClickAsync();
            await Task.Delay(1500);
            
            var servicos = _page.Locator("a[title='Serviços']");
            int countServicos = await servicos.CountAsync();
            
            _logService.Info($"ID {idValue}: Encontrados {countServicos} serviço(s)");
            
            for (int i = 0; i < countServicos; i++)
            {
                _logService.Progress($"ID {idValue}: Processando serviço {i + 1}/{countServicos}");
                await servicos.Nth(i).ClickAsync();
                await Task.Delay(2000);
                
                var tables = await _page.Locator("table").AllAsync();
                
                foreach (var table in tables)
                {
                    var categoria = await ExtractCategoryFromTable(table);
                    var rows = await table.Locator("tbody tr").AllAsync();
                    
                    foreach (var row in rows)
                    {
                        var cells = await row.Locator("td").AllAsync();
                        var valores = new List<string>();
                        foreach (var cell in cells)
                        {
                            valores.Add((await cell.TextContentAsync())?.Trim() ?? "");
                        }
                        
                        if (valores.Count >= 6)
                        {
                            var tipoRegistro = DetermineTipoRegistro(categoria,
                                valores.Count > 4 ? valores[4] : "",
                                valores.Count > 1 ? valores[1] : "");
                            
                            results.Add(new ScrapingResult
                            {
                                Id = idValue,
                                TipoRegistro = tipoRegistro,
                                Codigo = valores.Count > 0 ? valores[0] : "",
                                Descricao = valores.Count > 1 ? valores[1] : "",
                                Quantidade = valores.Count > 2 ? valores[2] : "",
                                PrecoUnitario = valores.Count > 3 ? valores[3] : "",
                                Unidade = valores.Count > 4 ? valores[4] : "",
                                PrecoTotal = valores.Count > 5 ? valores[5] : "",
                                Categoria = categoria,
                                Status = status
                            });
                        }
                    }
                }
                
                if (i < countServicos - 1)
                {
                    await _page.GoBackAsync();
                    await Task.Delay(1000);
                    await draft.ClickAsync();
                    await Task.Delay(1000);
                }
            }
            
            await ReturnToMainMenuAsync();
        }
        catch (Exception ex)
        {
            _logService.Error($"ID {idValue}: Erro durante extração - {ex.Message}");
        }
        
        _logService.Success($"ID {idValue}: Total extraído: {results.Count} linhas");
        return results;
    }
    
    public async Task<List<MemoriaCalculoResult>> ScrapMemoriaCalculoDraftAsync(int idValue)
    {
        var results = new List<MemoriaCalculoResult>();
        
        await NavigateToSearchAsync(idValue);
        var status = await ExtractStatusAsync(idValue);
        
        try
        {
            var editButton = _page!.Locator("span.badge.bg-primary").Filter(new LocatorFilterOptions { HasText = "Editar" });
            if (await editButton.CountAsync() == 0)
            {
                _logService.Warning($"ID {idValue}: Botão 'Editar' não encontrado. Status: '{status}'.");
                await ReturnToMainMenuAsync();
                return results;
            }
            
            await editButton.ClickAsync();
            await _page.WaitForSelectorAsync("a[title='Serviços']");
            
            var servicos = _page.Locator("a[title='Serviços']");
            int countServicos = await servicos.CountAsync();
            
            for (int servicoIdx = 0; servicoIdx < countServicos; servicoIdx++)
            {
                await servicos.Nth(servicoIdx).ClickAsync();
                
                try
                {
                    var memoriaLink = _page.Locator("//a[text()='Memória de Cálculo']");
                    await memoriaLink.ClickAsync(new LocatorClickOptions { Timeout = 15000 });
                    await Task.Delay(2000);
                    
                    await _page.WaitForSelectorAsync("table.ott-table-sm.ott-table-nowrap");
                    _logService.Success($"ID {idValue}: Serviço {servicoIdx + 1} - Memória de Cálculo");
                    
                    try
                    {
                        await _page.SelectOptionAsync("select.custom-select", new[] { new SelectOptionValue { Label = "50 itens" } });
                        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    }
                    catch { }
                    
                    int pagina = 1;
                    while (true)
                    {
                        var table = _page.Locator("table.ott-table-sm.ott-table-nowrap").First;
                        
                        if (await table.CountAsync() > 0)
                        {
                            var rows = await table.Locator("tbody tr").AllAsync();
                            
                            foreach (var row in rows)
                            {
                                var cells = await row.Locator("td").AllAsync();
                                if (cells.Count >= 9)
                                {
                                    var valores = new List<string>();
                                    foreach (var cell in cells)
                                    {
                                        var text = (await cell.TextContentAsync() ?? "").Replace("R$", "").Trim();
                                        valores.Add(text);
                                    }
                                    
                                    while (valores.Count < 9) valores.Add("");
                                    
                                    results.Add(new MemoriaCalculoResult
                                    {
                                        Id = idValue,
                                        Classe = valores[0],
                                        Codigo = valores[1],
                                        DescricaoServico = valores[2],
                                        Unidade = valores[3],
                                        Pontos = valores[4],
                                        CustoUnitario = valores[5],
                                        QuantidadeExecutada = valores[6],
                                        PontosTotais = valores[7],
                                        CustoTotal = valores[8],
                                        Status = status
                                    });
                                }
                            }
                        }
                        
                        var nextButton = _page.Locator("//li[not(contains(@class, 'disabled'))]//a[@aria-label='Next']");
                        bool hasNext = await nextButton.CountAsync() > 0 && await nextButton.First.IsVisibleAsync();
                        
                        if (!hasNext) break;
                        
                        await nextButton.First.ClickAsync();
                        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                        pagina++;
                    }
                }
                catch
                {
                    _logService.Warning($"ID {idValue}: Serviço {servicoIdx + 1} - Não encontrou Memória de Cálculo");
                    if (countServicos > 1)
                    {
                        await _page.GoBackAsync();
                        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    }
                    continue;
                }
                
                if (servicoIdx < countServicos - 1)
                {
                    await _page.GoBackAsync();
                    await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                }
            }
            
            await ReturnToMainMenuAsync();
        }
        catch (Exception ex)
        {
            _logService.Error($"Erro ao processar ID {idValue}: {ex.Message}");
            await ReturnToMainMenuAsync();
        }
        
        _logService.Success($"ID {idValue}: Finalizado - {results.Count} linhas");
        return results;
    }
    
    public void Stop()
    {
        _isRunning = false;
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_browser != null)
            await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    // ===========================================================
    // SCRAPING MEDIÇÃO
    // ===========================================================

    public async Task<List<ScrapingResult>> ScrapMedicaoAsync(int idValue)
    {
        var results = new List<ScrapingResult>();
        var status = "";
        
        await NavigateToSearchAsync(idValue);
        
        bool editClicked = false;
        for (int tentativa = 1; tentativa <= 3; tentativa++)
        {
            _logService.Progress($"ID {idValue}: Tentativa {tentativa} de 3 - Verificando botão Editar...");
            
            try
            {
                status = await ExtractStatusAsync(idValue);
                _logService.Info($"ID {idValue}: Status encontrado: '{status}'");
                
                var editButton = _page!.Locator("span.badge.bg-primary").Filter(new LocatorFilterOptions { HasText = "Editar" });
                if (await editButton.CountAsync() > 0)
                {
                    await editButton.ClickAsync();
                    _logService.Success($"ID {idValue}: Botão Editar clicado com sucesso");
                    editClicked = true;
                    break;
                }
                else
                {
                    _logService.Warning($"ID {idValue}: Botão Editar não encontrado");
                    if (tentativa == 3)
                    {
                        results.Add(new ScrapingResult { Id = idValue, Status = $"Botão Editar não encontrado! Status: {status}" });
                        await ReturnToMainMenuAsync();
                        return results;
                    }
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                _logService.Error($"ID {idValue}: Tentativa {tentativa} falhou: {ex.Message}");
                if (tentativa == 3)
                {
                    results.Add(new ScrapingResult { Id = idValue, Status = $"Erro: {ex.Message}" });
                    await ReturnToMainMenuAsync();
                    return results;
                }
                await Task.Delay(1000);
            }
        }
        
        if (!editClicked)
        {
            await ReturnToMainMenuAsync();
            return results;
        }
        
        try
        {
            await Task.Delay(1500);
            
            // Clica na aba Medição
            var medicao = _page!.GetByText("Medição", new PageGetByTextOptions { Exact = true });
            await medicao.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });
            await medicao.ClickAsync();
            await Task.Delay(1500);
            
            var servicos = _page.Locator("a[title='Serviços']");
            int countServicos = await servicos.CountAsync();
            
            _logService.Info($"ID {idValue}: Encontrados {countServicos} serviço(s)");
            
            for (int i = 0; i < countServicos; i++)
            {
                _logService.Progress($"ID {idValue}: Processando serviço {i + 1}/{countServicos}");
                await servicos.Nth(i).ClickAsync();
                await Task.Delay(2000);
                
                var tables = await _page.Locator("table").AllAsync();
                
                foreach (var table in tables)
                {
                    var categoria = await ExtractCategoryFromTable(table);
                    var rows = await table.Locator("tbody tr").AllAsync();
                    
                    foreach (var row in rows)
                    {
                        var cells = await row.Locator("td").AllAsync();
                        var valores = new List<string>();
                        foreach (var cell in cells)
                        {
                            valores.Add((await cell.TextContentAsync())?.Trim() ?? "");
                        }
                        
                        if (valores.Count >= 6)
                        {
                            var tipoRegistro = DetermineTipoRegistro(categoria,
                                valores.Count > 4 ? valores[4] : "",
                                valores.Count > 1 ? valores[1] : "");
                            
                            results.Add(new ScrapingResult
                            {
                                Id = idValue,
                                TipoRegistro = tipoRegistro,
                                Codigo = valores.Count > 0 ? valores[0] : "",
                                Descricao = valores.Count > 1 ? valores[1] : "",
                                Quantidade = valores.Count > 2 ? valores[2] : "",
                                PrecoUnitario = valores.Count > 3 ? valores[3] : "",
                                Unidade = valores.Count > 4 ? valores[4] : "",
                                PrecoTotal = valores.Count > 5 ? valores[5] : "",
                                Categoria = categoria,
                                Status = status
                            });
                        }
                    }
                }
                
                if (i < countServicos - 1)
                {
                    await _page.GoBackAsync();
                    await Task.Delay(1000);
                    await medicao.ClickAsync();
                    await Task.Delay(1000);
                }
            }
            
            await ReturnToMainMenuAsync();
        }
        catch (Exception ex)
        {
            _logService.Error($"ID {idValue}: Erro durante extração - {ex.Message}");
        }
        
        _logService.Success($"ID {idValue}: Total extraído: {results.Count} linhas");
        return results;
    }

    // ===========================================================
    // SCRAPING ID CANCELADOS
    // ===========================================================

    public async Task<List<IdCanceladoResult>> ScrapIdCanceladoAsync(int idValue)
    {
        var results = new List<IdCanceladoResult>();
        var status = "";
        var linkServicoEncontrado = false;
        
        try
        {
            await NavigateToSearchAsync(idValue);
            status = await ExtractStatusAsync(idValue);
        }
        catch (Exception ex)
        {
            _logService.Error($"❌ ID {idValue}: Erro na navegação inicial: {ex.Message}");
            status = "ERRO_NAVEGACAO";
        }
        
        try
        {
            var editButton = _page!.Locator("span.badge.bg-primary").Filter(new LocatorFilterOptions { HasText = "Editar" });
            if (await editButton.CountAsync() == 0)
            {
                _logService.Warning($"⚠️ ID {idValue}: Botão 'Editar' não encontrado. Status: '{status}'.");
                results.Add(new IdCanceladoResult { Id = idValue, Contrato = "", Osp = "NAO_ENCONTRADO", Status = status });
                await ReturnToMainMenuAsync();
                return results;
            }
            
            await editButton.ClickAsync();
            await Task.Delay(2000);
            
            // 🔑 Buscar serviço com timeout de 10 segundos
            try
            {
                // Tentar encontrar o link de serviço com timeout
                var servicosLink = _page.Locator("a[title='Serviços']");
                
                // Aguardar o elemento aparecer com timeout de 10 segundos
                await servicosLink.First.WaitForAsync(new LocatorWaitForOptions 
                { 
                    State = WaitForSelectorState.Attached,
                    Timeout = 10000 
                });
                
                linkServicoEncontrado = true;
                _logService.Info($"ID {idValue}: Link de serviço encontrado");
            }
            catch (TimeoutException)
            {
                _logService.Warning($"ID {idValue}: Timeout ao aguardar link de serviço (10 segundos)");
                linkServicoEncontrado = false;
            }
            
            // Se não encontrou o link de serviço por timeout
            if (!linkServicoEncontrado)
            {
                _logService.Warning($"ID {idValue}: Link de serviço não encontrado por timeout");
                results.Add(new IdCanceladoResult 
                { 
                    Id = idValue, 
                    Contrato = "", 
                    Osp = "Link serviço não encontrado", 
                    Status = status 
                });
                await ReturnToMainMenuAsync();
                return results;
            }
            
            // Verificar quantidade de serviços
            var servicos = _page.Locator("a[title='Serviços']");
            int countServicos = await servicos.CountAsync();
            
            if (countServicos == 0)
            {
                _logService.Warning($"ID {idValue}: Nenhum serviço encontrado.");
                results.Add(new IdCanceladoResult 
                { 
                    Id = idValue, 
                    Contrato = "", 
                    Osp = "Link serviço não encontrado", 
                    Status = status 
                });
                await ReturnToMainMenuAsync();
                return results;
            }
            
            _logService.Info($"ID {idValue}: Encontrados {countServicos} serviço(s)");
            
            for (int i = 0; i < countServicos; i++)
            {
                try
                {
                    var servicoBtn = servicos.Nth(i);
                    
                    // Clicar no serviço com timeout
                    try
                    {
                        await servicoBtn.ClickAsync(new LocatorClickOptions { Timeout = 1000 });
                    }
                    catch (TimeoutException)
                    {
                        _logService.Warning($"ID {idValue}: Timeout ao clicar no serviço {i + 1}");
                        results.Add(new IdCanceladoResult
                        {
                            Id = idValue,
                            Contrato = "TIMEOUT",
                            Osp = "Timeout ao clicar",
                            Status = status
                        });
                        continue;
                    }
                    
                    await Task.Delay(1000);
                    
                    // Extrair Contrato com timeout
                    string contrato = "";
                    try
                    {
                        var contratoEl = _page.Locator("xpath=/html/body/app-root/app-requisicoes-servicos/div/div/div/div/div[2]/div[2]/div/div/div[2]/div[2]/span");
                        
                        // Aguardar contrato com timeout curto
                        await contratoEl.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 1000 });
                        
                        if (await contratoEl.CountAsync() > 0)
                        {
                            contrato = await contratoEl.First.TextContentAsync() ?? "";
                        }
                    }
                    catch (TimeoutException)
                    {
                        _logService.Warning($"ID {idValue}: Timeout ao extrair contrato do serviço {i + 1}");
                        contrato = "TIMEOUT_CONTRATO";
                    }
                    catch (Exception ex)
                    {
                        _logService.Warning($"ID {idValue}: Erro ao extrair contrato: {ex.Message}");
                        contrato = "ERRO_CONTRATO";
                    }
                    
                    // Extrair OSP
                    string osp = await ExtrairStatusOspAsync();
                    
                    _logService.Info($"ID {idValue} - Serviço {i + 1}/{countServicos}: Contrato={contrato}, OSP={osp}");
                    
                    results.Add(new IdCanceladoResult
                    {
                        Id = idValue,
                        Contrato = contrato.Trim(),
                        Osp = osp.Trim(),
                        Status = status
                    });
                }
                catch (Exception ex)
                {
                    _logService.Warning($"ID {idValue}: Erro no serviço {i + 1}: {ex.Message}");
                    results.Add(new IdCanceladoResult
                    {
                        Id = idValue,
                        Contrato = "ERRO",
                        Osp = "ERRO",
                        Status = status
                    });
                }
                
                if (i < countServicos - 1)
                {
                    try
                    {
                        await _page.GoBackAsync();
                        await Task.Delay(1000);
                    }
                    catch (Exception ex)
                    {
                        _logService.Warning($"ID {idValue}: Erro ao voltar: {ex.Message}");
                    }
                }
            }
            
            await ReturnToMainMenuAsync();
        }
        catch (Exception ex)
        {
            _logService.Error($"ID {idValue}: Erro na extração - {ex.Message}");
            
            // Se não adicionou nenhum resultado ainda, adiciona com erro
            if (results.Count == 0)
            {
                string erroOsp = ex is TimeoutException ? "Timeout na extração" : "ERRO_GERAL";
                results.Add(new IdCanceladoResult
                {
                    Id = idValue,
                    Contrato = "ERRO",
                    Osp = erroOsp,
                    Status = status
                });
            }
            await ReturnToMainMenuAsync();
        }
        
        _logService.Success($"ID {idValue}: Extraído {results.Count} registro(s)");
        return results;
    }

    // ===========================================================
    // MÉTODOS PRIVADOS AUXILIARES
    // ===========================================================
    
    // 🔑 Método auxiliar para extrair o status do OSP
    
    private async Task<string> ExtrairStatusOspAsync()
    {
        try
        {
            // Tentar encontrar o elemento com timeout de 5 segundos
            var canceladoEl = _page!.Locator("strong.bg-danger-strong");
            
            try
            {
                await canceladoEl.First.WaitForAsync(new LocatorWaitForOptions 
                { 
                    State = WaitForSelectorState.Attached,
                    Timeout = 1000 
                });
                
                var texto = await canceladoEl.First.TextContentAsync();
                if (!string.IsNullOrEmpty(texto))
                {
                    _logService.Debug($"Status OSP encontrado: {texto.Trim()}");
                    return texto.Trim().ToUpperInvariant();
                }
            }
            catch (TimeoutException)
            {
                _logService.Debug("Timeout ao aguardar elemento 'strong.bg-danger-strong'. Assumindo ATIVO.");
            }
            
            return "ATIVO";
        }
        catch (Exception ex)
        {
            _logService.Debug($"Erro ao verificar status OSP: {ex.Message}. Assumindo ATIVO.");
            return "ATIVO";
        }
    }

    // ===========================================================
    // SCRAPING MEMÓRIA DE CÁLCULO MEDIÇÃO
    // ===========================================================

    public async Task<List<MemoriaCalculoResult>> ScrapMemoriaCalculoMedicaoAsync(int idValue)
    {
        var results = new List<MemoriaCalculoResult>();
        
        await NavigateToSearchAsync(idValue);
        var status = await ExtractStatusAsync(idValue);
        
        try
        {
            var editButton = _page!.Locator("span.badge.bg-primary").Filter(new LocatorFilterOptions { HasText = "Editar" });
            if (await editButton.CountAsync() == 0)
            {
                _logService.Warning($"ID {idValue}: Botão 'Editar' não encontrado. Status: '{status}'.");
                await ReturnToMainMenuAsync();
                return results;
            }
            
            await editButton.ClickAsync();
            await _page.WaitForSelectorAsync("a[title='Serviços']");
            
            // Clica na aba Medição
            var medicao = _page.GetByText("Medição", new PageGetByTextOptions { Exact = true });
            await medicao.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });
            await medicao.ClickAsync();
            await Task.Delay(1500);
            
            var servicos = _page.Locator("a[title='Serviços']");
            int countServicos = await servicos.CountAsync();
            
            for (int servicoIdx = 0; servicoIdx < countServicos; servicoIdx++)
            {
                await servicos.Nth(servicoIdx).ClickAsync();
                
                try
                {
                    var memoriaLink = _page.Locator("//a[text()='Memória de Cálculo']");
                    await memoriaLink.ClickAsync(new LocatorClickOptions { Timeout = 15000 });
                    await Task.Delay(2000);
                    
                    await _page.WaitForSelectorAsync("table.ott-table-sm.ott-table-nowrap");
                    _logService.Success($"ID {idValue}: Serviço {servicoIdx + 1} - Memória de Cálculo (Medição)");
                    
                    try
                    {
                        await _page.SelectOptionAsync("select.custom-select", new[] { new SelectOptionValue { Label = "50 itens" } });
                        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    }
                    catch { }
                    
                    int pagina = 1;
                    while (true)
                    {
                        var table = _page.Locator("table.ott-table-sm.ott-table-nowrap").First;
                        
                        if (await table.CountAsync() > 0)
                        {
                            var rows = await table.Locator("tbody tr").AllAsync();
                            
                            foreach (var row in rows)
                            {
                                var cells = await row.Locator("td").AllAsync();
                                if (cells.Count >= 9)
                                {
                                    var valores = new List<string>();
                                    foreach (var cell in cells)
                                    {
                                        var text = (await cell.TextContentAsync() ?? "").Replace("R$", "").Trim();
                                        valores.Add(text);
                                    }
                                    
                                    while (valores.Count < 9) valores.Add("");
                                    
                                    results.Add(new MemoriaCalculoResult
                                    {
                                        Id = idValue,
                                        Classe = valores[0],
                                        Codigo = valores[1],
                                        DescricaoServico = valores[2],
                                        Unidade = valores[3],
                                        Pontos = valores[4],
                                        CustoUnitario = valores[5],
                                        QuantidadeExecutada = valores[6],
                                        PontosTotais = valores[7],
                                        CustoTotal = valores[8],
                                        Status = status
                                    });
                                }
                            }
                        }
                        
                        var nextButton = _page.Locator("//li[not(contains(@class, 'disabled'))]//a[@aria-label='Next']");
                        bool hasNext = await nextButton.CountAsync() > 0 && await nextButton.First.IsVisibleAsync();
                        
                        if (!hasNext) break;
                        
                        await nextButton.First.ClickAsync();
                        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                        pagina++;
                    }
                }
                catch
                {
                    _logService.Warning($"ID {idValue}: Serviço {servicoIdx + 1} - Não encontrou Memória de Cálculo");
                    if (countServicos > 1)
                    {
                        await _page.GoBackAsync();
                        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                        await medicao.ClickAsync();
                    }
                    continue;
                }
                
                if (servicoIdx < countServicos - 1)
                {
                    await _page.GoBackAsync();
                    await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    await medicao.ClickAsync();
                }
            }
            
            await ReturnToMainMenuAsync();
        }
        catch (Exception ex)
        {
            _logService.Error($"Erro ao processar ID {idValue}: {ex.Message}");
            await ReturnToMainMenuAsync();
        }
        
        _logService.Success($"ID {idValue}: Finalizado - {results.Count} linhas");
        return results;
    }
}   