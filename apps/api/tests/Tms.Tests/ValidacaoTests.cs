using Tms.Domain;
using Xunit;

namespace Tms.Tests;

public sealed class ValidacaoTests
{
    [Theory]
    [InlineData("99000001000101", true)]
    [InlineData("99000001000102", false)]
    [InlineData("00000000000000", false)]
    [InlineData("99.000.001/0001-01", false)]
    [InlineData(null, false)]
    public void VerificaCnpj(string? valor, bool valido) => Assert.Equal(valido, Validacao.Cnpj(valor));

    [Theory]
    [InlineData("90000000175", true)]
    [InlineData("90000000176", false)]
    [InlineData("11111111111", false)]
    [InlineData("9000000017", false)]
    public void VerificaCpf(string valor, bool valido) => Assert.Equal(valido, Validacao.Cpf(valor));

    [Theory]
    [InlineData("01000000", "SP", true)]
    [InlineData("01000-000", "SP", false)]
    [InlineData("０１００００００", "SP", false)]
    [InlineData("01000000", "XX", false)]
    public void VerificaEndereco(string cep, string uf, bool valido) =>
        Assert.Equal(valido, Validacao.Cep(cep) && Validacao.Uf(uf));

    [Theory]
    [InlineData(500, 1, 300, 500)]
    [InlineData(200, 1, 300, 300)]
    [InlineData(300, 1, 300, 300)]
    [InlineData(10, 0, 300, 10)]
    [InlineData(10, 1, 100, 100)]
    public void CalculaPesoTaxado(int peso, int volume, int fator, int esperado) =>
        Assert.Equal(esperado, TabelaFrete.PesoTaxado(peso, volume, fator));

    [Theory]
    [InlineData(0, 0, 300)]
    [InlineData(-1, 0, 300)]
    [InlineData(1, -1, 300)]
    [InlineData(1, 1, 0)]
    public void RecusaMedidasInvalidas(int peso, int volume, int fator) =>
        Assert.Throws<RegraViolada>(() => TabelaFrete.PesoTaxado(peso, volume, fator));

    [Fact]
    public void CotacaoAplicaMinimoPesoRealCubagemEArredondamento()
    {
        var p = new Transportadora(Guid.NewGuid(), TipoParceiro.AUTONOMO, "Parceiro", "90000000175");
        var tabela = new TabelaFrete(p, "SP", "RJ", 10, 1.2345m, 2);
        Assert.Equal(10m, tabela.Cotar(1, 0));
        Assert.Equal(12.35m, tabela.Cotar(10, 0));
        Assert.Equal(370.35m, tabela.Cotar(10, 1));
    }
}
