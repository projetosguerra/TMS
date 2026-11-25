namespace Tms.Domain;

public sealed class TabelaFrete : Entidade
{
    // Hipótese do MVP em kg/m³. A simulação pode informar outro fator.
    public const decimal FatorCubagemPadrao = 300m;
    public Guid EmpresaId { get; private set; }
    public Guid TransportadoraId { get; private set; }
    public string UfOrigem { get; private set; } = null!;
    public string UfDestino { get; private set; } = null!;
    public decimal ValorMinimo { get; private set; }
    public decimal ValorPorKg { get; private set; }
    public int PrazoDias { get; private set; }
    public bool Ativa { get; private set; }
    private TabelaFrete() { }

    public TabelaFrete(Transportadora parceiro, string ufOrigem, string ufDestino,
        decimal valorMinimo, decimal valorPorKg, int prazoDias, bool ativa = true)
    {
        Validacao.Exigir(Validacao.Uf(ufOrigem) && Validacao.Uf(ufDestino), "UF inválida.");
        Validacao.Exigir(prazoDias >= 1, "Informe o prazo em dias.");
        Validacao.DecimalNoIntervalo(valorMinimo, 2, 9999999999.99m, false);
        Validacao.DecimalNoIntervalo(valorPorKg, 4, 99999999.9999m, false);
        EmpresaId = parceiro.EmpresaId;
        TransportadoraId = parceiro.Id;
        UfOrigem = ufOrigem;
        UfDestino = ufDestino;
        ValorMinimo = valorMinimo;
        ValorPorKg = valorPorKg;
        PrazoDias = prazoDias;
        Ativa = ativa;
    }

    public void Atualizar(string ufOrigem, string ufDestino, decimal valorMinimo,
        decimal valorPorKg, int prazoDias, bool ativa)
    {
        var origem = ufOrigem.Trim().ToUpperInvariant();
        var destino = ufDestino.Trim().ToUpperInvariant();
        Validacao.Exigir(Validacao.Uf(origem) && Validacao.Uf(destino), "UF invalida.");
        Validacao.Exigir(prazoDias >= 1, "Informe o prazo em dias.");
        Validacao.DecimalNoIntervalo(valorMinimo, 2, 9999999999.99m, false);
        Validacao.DecimalNoIntervalo(valorPorKg, 4, 99999999.9999m, false);
        UfOrigem = origem;
        UfDestino = destino;
        ValorMinimo = valorMinimo;
        ValorPorKg = valorPorKg;
        PrazoDias = prazoDias;
        Ativa = ativa;
    }

    public static decimal PesoTaxado(decimal peso, decimal volume, decimal fator = FatorCubagemPadrao)
    {
        Validacao.Exigir(peso > 0 && volume >= 0 && fator > 0, "Peso, volume ou fator inválido.");
        return Math.Max(peso, volume * fator);
    }

    public decimal Cotar(decimal peso, decimal volume, decimal fator = FatorCubagemPadrao) =>
        decimal.Round(Math.Max(ValorMinimo, PesoTaxado(peso, volume, fator) * ValorPorKg),
            2, MidpointRounding.AwayFromZero);
}
