namespace Tms.Domain;

public sealed class Proposta : Entidade
{
    public Guid EmpresaId { get; private set; }
    public Guid FreteId { get; private set; }
    public Guid TransportadoraId { get; private set; }
    public OrigemProposta Origem { get; private set; }
    public decimal Valor { get; private set; }
    public int PrazoDias { get; private set; }
    public string? Observacao { get; private set; }
    public bool Selecionada { get; internal set; }

    private Proposta() { }

    internal Proposta(Frete frete, Transportadora parceiro, DateTimeOffset instante)
    {
        EmpresaId = frete.EmpresaId;
        FreteId = frete.Id;
        TransportadoraId = parceiro.Id;
        CriadoEm = instante.ToUniversalTime();
    }

    internal void Atualizar(OrigemProposta origem, decimal valor, int dias, string? observacao)
    {
        Validacao.DecimalNoIntervalo(valor, 2, 9999999999.99m, false);
        Validacao.Exigir(dias >= 1, "Prazo mínimo de um dia.");
        Origem = origem;
        Valor = valor;
        PrazoDias = dias;
        Observacao = observacao;
    }
}

public sealed class HistoricoStatus : Entidade
{
    public Guid EmpresaId { get; private set; }
    public Guid FreteId { get; private set; }
    public StatusFrete? StatusAnterior { get; private set; }
    public StatusFrete StatusNovo { get; private set; }
    public string? Observacao { get; private set; }
    public Guid UsuarioId { get; private set; }
    public DateTimeOffset OcorridoEm { get; private set; }

    private HistoricoStatus() { }

    internal HistoricoStatus(Frete frete, StatusFrete? anterior, StatusFrete novo, Usuario autor,
        DateTimeOffset instante, string? observacao)
    {
        EmpresaId = frete.EmpresaId;
        FreteId = frete.Id;
        StatusAnterior = anterior;
        StatusNovo = novo;
        Observacao = observacao;
        UsuarioId = autor.Id;
        OcorridoEm = CriadoEm = instante.ToUniversalTime();
    }
}

public sealed class DocumentoFiscal : Entidade
{
    public Guid EmpresaId { get; private set; }
    public Guid FreteId { get; private set; }
    public TipoDocumento Tipo { get; private set; }
    public string Numero { get; private set; } = null!;
    public string? ChaveAcesso { get; private set; }
    public decimal? Valor { get; private set; }

    private DocumentoFiscal() { }
    public DocumentoFiscal(Frete frete, TipoDocumento tipo, string numero, string? chaveAcesso, decimal? valor)
    {
        Validacao.Exigir(Enum.IsDefined(tipo) && !string.IsNullOrWhiteSpace(numero), "Documento fiscal inválido.");
        Validacao.Exigir(chaveAcesso == null ? tipo != TipoDocumento.NFE : Validacao.Digitos(chaveAcesso, 44),
            "Informe uma chave de acesso de 44 dígitos.");
        if (valor.HasValue) Validacao.DecimalNoIntervalo(valor.Value, 2, 9999999999.99m, false);
        EmpresaId = frete.EmpresaId;
        FreteId = frete.Id;
        Tipo = tipo;
        Numero = numero;
        ChaveAcesso = chaveAcesso;
        Valor = valor;
    }
}
