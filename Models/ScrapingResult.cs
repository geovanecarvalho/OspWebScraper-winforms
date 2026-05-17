namespace OSPVivoScraper.Models;

public class ScrapingResult
{
    public int Id { get; set; }
    public string TipoRegistro { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string Quantidade { get; set; } = string.Empty;
    public string PrecoUnitario { get; set; } = string.Empty;
    public string Unidade { get; set; } = string.Empty;
    public string PrecoTotal { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class MemoriaCalculoResult
{
    public int Id { get; set; }
    public string Classe { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string DescricaoServico { get; set; } = string.Empty;
    public string Unidade { get; set; } = string.Empty;
    public string Pontos { get; set; } = string.Empty;
    public string CustoUnitario { get; set; } = string.Empty;
    public string QuantidadeExecutada { get; set; } = string.Empty;
    public string PontosTotais { get; set; } = string.Empty;
    public string CustoTotal { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class IdCanceladoResult
{
    public int Id { get; set; }
    public string Contrato { get; set; } = string.Empty;
    public string Osp { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}