namespace OSPVivoScraper.Models;

public class AppConfig
{
    public string Usuario { get; set; } = string.Empty;
    public string Senha { get; set; } = string.Empty;
    public bool Headless { get; set; } = false;
    public string? LastCsvPath { get; set; }
    public int LastMode { get; set; } = 1;
    public bool SalvarSessao { get; set; } = true;
}

public enum ScrapingMode
{
    Draft = 1,
    Medicao = 2,
    IdCancelados = 3,
    MemoriaCalculoDraft = 4,
    MemoriaCalculoMedicao = 5
}