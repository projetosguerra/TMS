using Tms.Domain;
using Xunit;

namespace Tms.Tests;

public sealed class TabelaFreteTests
{
    private static Transportadora Parceiro() => new(Guid.NewGuid(), TipoParceiro.TRANSPORTADORA,
        "Transportadora de teste", "99000003000192");

    [Fact]
    public void AtualizarNormalizaUfEValoresDaRota()
    {
        var tabela = new TabelaFrete(Parceiro(), "SP", "RJ", 50m, 1.25m, 3);

        tabela.Atualizar(" mg ", " pr ", 80m, 2.5m, 5, false);

        Assert.Equal("MG", tabela.UfOrigem);
        Assert.Equal("PR", tabela.UfDestino);
        Assert.Equal(80m, tabela.ValorMinimo);
        Assert.Equal(2.5m, tabela.ValorPorKg);
        Assert.Equal(5, tabela.PrazoDias);
        Assert.False(tabela.Ativa);
    }

    [Fact]
    public void AtualizacaoInvalidaNaoAlteraTabelaExistente()
    {
        var tabela = new TabelaFrete(Parceiro(), "SP", "RJ", 50m, 1.25m, 3);

        Assert.Throws<RegraViolada>(() => tabela.Atualizar("XX", "RJ", 99m, 2m, 7, false));

        Assert.Equal("SP", tabela.UfOrigem);
        Assert.Equal("RJ", tabela.UfDestino);
        Assert.Equal(50m, tabela.ValorMinimo);
        Assert.Equal(1.25m, tabela.ValorPorKg);
        Assert.Equal(3, tabela.PrazoDias);
        Assert.True(tabela.Ativa);
    }
}
