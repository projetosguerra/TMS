using Tms.Domain;
using Xunit;

namespace Tms.Tests;

public sealed class TransportadoraTests
{
    [Fact]
    public void AceitaCnpjParaTransportadora()
    {
        var item = new Transportadora(Guid.NewGuid(), TipoParceiro.TRANSPORTADORA,
            "  Parceiro Alfa  ", "99000003000192", " 11999990000 ", " TESTE@EXEMPLO.INVALID ");

        Assert.Equal("Parceiro Alfa", item.Nome);
        Assert.Equal("11999990000", item.Telefone);
        Assert.Equal("teste@exemplo.invalid", item.Email);
    }

    [Fact]
    public void AceitaCpfParaMotoristaAutonomo()
    {
        var item = new Transportadora(Guid.NewGuid(), TipoParceiro.AUTONOMO, "Motorista", "90000000175");

        Assert.Equal(TipoParceiro.AUTONOMO, item.Tipo);
    }

    [Fact]
    public void RejeitaDocumentoIncompativelComOTipo()
    {
        Assert.Throws<RegraViolada>(() => new Transportadora(Guid.NewGuid(), TipoParceiro.AUTONOMO,
            "Motorista", "99000003000192"));
        Assert.Throws<RegraViolada>(() => new Transportadora(Guid.NewGuid(), TipoParceiro.TRANSPORTADORA,
            "Parceiro", "90000000175"));
    }

    [Fact]
    public void AtualizacaoInvalidaNaoAlteraDadosExistentes()
    {
        var item = new Transportadora(Guid.NewGuid(), TipoParceiro.TRANSPORTADORA,
            "Parceiro Alfa", "99000003000192");

        Assert.Throws<RegraViolada>(() => item.Atualizar(TipoParceiro.TRANSPORTADORA,
            "Novo nome", "00000000000000", null, null));

        Assert.Equal("Parceiro Alfa", item.Nome);
        Assert.Equal("99000003000192", item.Documento);
    }
}
