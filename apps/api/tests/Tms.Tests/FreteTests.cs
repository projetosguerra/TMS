using Tms.Domain;
using Xunit;
using static Tms.Domain.StatusFrete;

namespace Tms.Tests;

public sealed class FreteTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
    private readonly Usuario _autor = new(Guid.NewGuid(), Guid.NewGuid(), "Operador", "teste@example.invalid", Perfil.OPERADOR);
    private readonly DadosFrete _dados = new("01000000", "Origem", "SP", "02000000", "Destino", "RJ", "Destinatário", 10);
    private Transportadora Parceiro() => new(_autor.EmpresaId, TipoParceiro.AUTONOMO, "Parceiro", "90000000175");
    private Frete Criar(StatusFrete status = ABERTO)
    {
        var f = new Frete(_autor, "FR-2026-000001", _dados, Inicio);
        if (status == CANCELADO) f.AlterarStatus(status, _autor, Inicio, "Motivo");
        if (status is ABERTO or CANCELADO) return f;
        var proposta = f.RegistrarManual(Parceiro(), 100, 2, null, _autor, Inicio);
        if (status == EM_COTACAO) return f;
        f.Selecionar(proposta.Id, _autor, Inicio);
        if (status == CONTRATADO) return f;
        f.AlterarStatus(COLETADO, _autor, Inicio);
        if (status != COLETADO) f.AlterarStatus(status, _autor, Inicio);
        return f;
    }

    public static IEnumerable<object[]> Pares() =>
        from origem in Enum.GetValues<StatusFrete>() from destino in Enum.GetValues<StatusFrete>()
        select new object[] { origem, destino };

    [Theory]
    [MemberData(nameof(Pares))]
    public void AlteracaoDiretaRespeitaMatrizEHistorico(StatusFrete origem, StatusFrete destino)
    {
        var frete = Criar(origem);
        var quantidade = frete.Historico.Count;
        var instante = Inicio.AddDays(1);
        bool permitido = destino switch
        {
            CANCELADO => origem is ABERTO or EM_COTACAO or CONTRATADO,
            COLETADO => origem == CONTRATADO,
            EM_TRANSITO => origem == COLETADO,
            ENTREGUE => origem is COLETADO or EM_TRANSITO,
            _ => false
        };
        if (permitido)
        {
            frete.AlterarStatus(destino, _autor, instante, "Registro");
            Assert.Equal(destino, frete.Status);
            var evento = Assert.Single(frete.Historico.Skip(quantidade));
            Assert.Equal((origem, destino, instante, _autor.Id, frete.EmpresaId, frete.Id, "Registro"),
                (evento.StatusAnterior!.Value, evento.StatusNovo, evento.OcorridoEm, evento.UsuarioId,
                    evento.EmpresaId, evento.FreteId, evento.Observacao));
        }
        else
        {
            Assert.Throws<RegraViolada>(() => frete.AlterarStatus(destino, _autor, instante, "Registro"));
            Assert.Equal(origem, frete.Status);
            Assert.Equal(quantidade, frete.Historico.Count);
        }
    }

    [Theory]
    [InlineData(ABERTO)]
    [InlineData(EM_COTACAO)]
    [InlineData(CONTRATADO)]
    public void CancelamentoExigeMotivo(StatusFrete status) =>
        Assert.Throws<RegraViolada>(() => Criar(status).AlterarStatus(CANCELADO, _autor, Inicio, " "));

    [Fact]
    public void SimularFiltraParceiroTabelaRotaEEmpresaEAtualizaSemDuplicar()
    {
        var f = Criar();
        var p = Parceiro();
        var q = Parceiro();
        var inativo = Parceiro();
        inativo.Inativar();
        var terceiro = new Transportadora(Guid.NewGuid(), TipoParceiro.AUTONOMO, "Outro", "90000000175");
        (Transportadora, TabelaFrete)[] tabelas = [
            (inativo, new(inativo, "SP", "RJ", 10, 1, 1)), (q, new(q, "RJ", "SP", 10, 1, 1)),
            (q, new(q, "SP", "RJ", 10, 1, 1, false)), (terceiro, new(terceiro, "SP", "RJ", 10, 1, 1))];
        Assert.Throws<RotaSemAtendimento>(() => f.Simular(tabelas, _autor, Inicio));
        Assert.Equal(ABERTO, f.Status);
        tabelas = [.. tabelas, (p, new(p, "SP", "RJ", 1, 2, 3)), (q, new(q, "SP", "RJ", 30, 1, 1))];
        f.Simular(tabelas, _autor, Inicio);
        f.Simular(tabelas, _autor, Inicio);
        Assert.Equal(2, f.Propostas.Count);
        var proposta = f.Propostas.Single(x => x.TransportadoraId == p.Id);
        Assert.Equal((OrigemProposta.TABELA, 20m, 3), (proposta.Origem, proposta.Valor, proposta.PrazoDias));
        Assert.Equal([ABERTO, EM_COTACAO], f.Historico.Select(x => x.StatusNovo));
        Assert.Null(f.Historico.First().StatusAnterior);
        var manual = f.RegistrarManual(p, 15, 1, "Recebida", _autor, Inicio);
        Assert.Same(proposta, manual);
        Assert.Equal((OrigemProposta.MANUAL, 15m, 1, "Recebida"), (manual.Origem, manual.Valor, manual.PrazoDias, manual.Observacao));
        f.RemoverProposta(manual.Id, Inicio);
        Assert.Single(f.Propostas);
    }

    [Fact]
    public void SelecaoETrocaGravamContratoEPrazoCorrido()
    {
        var f = Criar(EM_COTACAO);
        var primeira = f.Propostas.Single();
        var segunda = f.RegistrarManual(Parceiro(), 80, 1, null, _autor, Inicio);
        f.Selecionar(primeira.Id, _autor, Inicio);
        Assert.True(primeira.Selecionada);
        Assert.Equal(new DateOnly(2026, 11, 22), f.PrazoPrevisto);
        f.Selecionar(segunda.Id, _autor, Inicio.AddDays(2));
        Assert.False(primeira.Selecionada);
        Assert.True(segunda.Selecionada);
        Assert.Equal((CONTRATADO, segunda.Id, segunda.TransportadoraId, 80m, new DateOnly(2026, 11, 23)),
            (f.Status, f.PropostaSelecionadaId, f.TransportadoraId, f.ValorContratado, f.PrazoPrevisto));
        Assert.Equal([ABERTO, EM_COTACAO, CONTRATADO], f.Historico.Select(x => x.StatusNovo));
    }

    [Theory]
    [InlineData(CONTRATADO)]
    [InlineData(COLETADO)]
    [InlineData(EM_TRANSITO)]
    [InlineData(ENTREGUE)]
    [InlineData(CANCELADO)]
    public void BloqueiaEdicaoESubstituicaoForaDaCotacao(StatusFrete status)
    {
        var f = Criar(status);
        Assert.Throws<RegraViolada>(() => f.Editar(_dados, Inicio));
        Assert.Throws<RegraViolada>(() => f.RegistrarManual(Parceiro(), 1, 1, null, _autor, Inicio));
        Assert.Throws<RegraViolada>(() => f.RemoverProposta(f.Propostas.FirstOrDefault()?.Id ?? Guid.Empty, Inicio));
        Assert.Throws<RegraViolada>(() => f.Simular([], _autor, Inicio));
        if (status != CONTRATADO)
            Assert.Throws<RegraViolada>(() => f.Selecionar(f.Propostas.FirstOrDefault()?.Id ?? Guid.Empty, _autor, Inicio));
    }

    [Theory]
    [InlineData(ABERTO)]
    [InlineData(EM_COTACAO)]
    public void PermiteEditarAntesDaContratacao(StatusFrete status)
    {
        var f = Criar(status);
        f.Editar(_dados with { PesoKg = 42 }, Inicio);
        Assert.Equal(42m, f.Dados.PesoKg);
        Assert.Throws<RegraViolada>(() => f.Selecionar(Guid.NewGuid(), _autor, Inicio));
    }

    [Fact]
    public void InativarPreservaPropostaEReativarPermiteAtualizar()
    {
        var p = Parceiro();
        var f = Criar();
        var original = f.RegistrarManual(p, 10, 1, null, _autor, Inicio);
        p.Inativar();
        Assert.Throws<RegraViolada>(() => f.RegistrarManual(p, 9, 1, null, _autor, Inicio));
        Assert.Equal(10m, Assert.Single(f.Propostas).Valor);
        p.Ativar();
        Assert.Same(original, f.RegistrarManual(p, 9, 1, null, _autor, Inicio));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 2)]
    [InlineData(100, 0)]
    public void ManualInvalidaPreservaEstado(int valor, int dias)
    {
        var f = Criar();
        Assert.Throws<RegraViolada>(() => f.RegistrarManual(Parceiro(), valor, dias, null, _autor, Inicio));
        Assert.Empty(f.Propostas);
        Assert.Equal(ABERTO, f.Status);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void AtrasoEEntregaUsamDataDoPrazo(int dias, bool noPrazo)
    {
        var f = Criar(COLETADO);
        var instante = Inicio.AddDays(dias);
        Assert.Equal(!noPrazo, f.Atrasado(DateOnly.FromDateTime(instante.UtcDateTime)));
        f.AlterarStatus(ENTREGUE, _autor, instante);
        Assert.Equal(instante, f.EntregueEm);
        Assert.Equal(noPrazo, f.EntregueNoPrazo());
        Assert.False(f.Atrasado(DateOnly.MaxValue));
    }

    [Fact]
    public void SemPrazoOuCanceladoNaoAtrasa()
    {
        Assert.False(Criar().Atrasado(DateOnly.MaxValue));
        var f = Criar(CONTRATADO);
        f.AlterarStatus(CANCELADO, _autor, Inicio, "Cancelamento");
        Assert.False(f.Atrasado(DateOnly.MaxValue));
    }

    [Fact]
    public void EmpresaDoAtorEParceiroDevemConferir()
    {
        var f = Criar();
        var outro = new Usuario(Guid.NewGuid(), Guid.NewGuid(), "Outro", "outro@example.invalid", Perfil.ADMIN);
        var p = new Transportadora(outro.EmpresaId, TipoParceiro.AUTONOMO, "Outro", "90000000175");
        Assert.Throws<RegraViolada>(() => f.RegistrarManual(p, 10, 1, null, _autor, Inicio));
        Assert.Throws<RegraViolada>(() => f.RegistrarManual(Parceiro(), 10, 1, null, outro, Inicio));
        Assert.Throws<RegraViolada>(() => f.AlterarStatus(CANCELADO, outro, Inicio, "Teste"));
        Assert.Empty(f.Propostas);
    }

    [Theory]
    [InlineData(TipoDocumento.NFE, 44, true)]
    [InlineData(TipoDocumento.NFE, 43, false)]
    [InlineData(TipoDocumento.NFE, 45, false)]
    [InlineData(TipoDocumento.NFE, 0, false)]
    [InlineData(TipoDocumento.CTE, 0, true)]
    [InlineData(TipoDocumento.OUTRO, 0, true)]
    public void ValidaChaveFiscal(TipoDocumento tipo, int tamanho, bool valido)
    {
        var f = Criar();
        string? chave = tamanho == 0 ? null : new string('1', tamanho);
        DocumentoFiscal Novo() => new(f, tipo, "1", chave, null);
        if (valido) Assert.Equal((f.EmpresaId, f.Id), (Novo().EmpresaId, Novo().FreteId));
        else Assert.Throws<RegraViolada>(Novo);
        Assert.Throws<RegraViolada>(() => new DocumentoFiscal(f, tipo, "1", new string('A', 44), null));
    }
}
