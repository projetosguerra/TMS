using System.Text.RegularExpressions;
using static Tms.Domain.StatusFrete;

namespace Tms.Domain;

public sealed record DadosFrete(string OrigemCep, string OrigemCidade, string OrigemUf,
    string DestinoCep, string DestinoCidade, string DestinoUf, string DestinatarioNome,
    decimal PesoKg, decimal VolumeM3 = 0, decimal? ValorMercadoria = null, string? Observacao = null);

public sealed class Frete : Entidade
{
    private readonly List<Proposta> _propostas = [];
    private readonly List<HistoricoStatus> _historico = [];
    public IReadOnlyCollection<Proposta> Propostas => _propostas.AsReadOnly();
    public IReadOnlyCollection<HistoricoStatus> Historico => _historico.AsReadOnly();

    public Guid EmpresaId { get; private set; }
    public string Codigo { get; private set; } = null!;
    public StatusFrete Status { get; private set; }
    public DadosFrete Dados { get; private set; } = null!;
    public Guid CriadoPor { get; private set; }
    public Guid? TransportadoraId { get; private set; }
    public Guid? PropostaSelecionadaId { get; private set; }
    public decimal? ValorContratado { get; private set; }
    public DateOnly? PrazoPrevisto { get; private set; }
    public DateTimeOffset? EntregueEm { get; private set; }
    public DateTimeOffset AtualizadoEm { get; private set; }

    private Frete() { }
    public Frete(Usuario autor, string codigo, DadosFrete dados, DateTimeOffset instante)
    {
        Validacao.Exigir(Regex.IsMatch(codigo, @"\AFR-[0-9]{4}-[0-9]{6}\z"), "Código de frete inválido.");
        EmpresaId = autor.EmpresaId;
        CriadoPor = autor.Id;
        Codigo = codigo;
        CriadoEm = instante.ToUniversalTime();
        Editar(dados, instante);
        _historico.Add(new(this, null, ABERTO, autor, instante, null));
    }

    private void ExigirEditavel() =>
        Validacao.Exigir(Status is ABERTO or EM_COTACAO, "O status atual não permite esta alteração.");

    public void Editar(DadosFrete dados, DateTimeOffset instante)
    {
        ExigirEditavel();
        Validacao.Exigir(Validacao.Cep(dados.OrigemCep) && Validacao.Cep(dados.DestinoCep), "CEP inválido.");
        Validacao.Exigir(Validacao.Uf(dados.OrigemUf) && Validacao.Uf(dados.DestinoUf), "UF inválida.");
        Validacao.Exigir(!string.IsNullOrWhiteSpace(dados.OrigemCidade) &&
            !string.IsNullOrWhiteSpace(dados.DestinoCidade) && !string.IsNullOrWhiteSpace(dados.DestinatarioNome),
            "Informe as cidades e o destinatário.");
        Validacao.DecimalNoIntervalo(dados.PesoKg, 2, 99999999.99m, false);
        Validacao.DecimalNoIntervalo(dados.VolumeM3, 3, 9999999.999m, true);
        if (dados.ValorMercadoria is decimal valor) Validacao.DecimalNoIntervalo(valor, 2, 9999999999.99m, false);
        Dados = dados;
        AtualizadoEm = instante.ToUniversalTime();
    }

    public void Simular(IEnumerable<(Transportadora Parceiro, TabelaFrete Tabela)> tabelas,
        Usuario autor, DateTimeOffset instante, decimal fator = TabelaFrete.FatorCubagemPadrao)
    {
        ExigirEditavel();
        ExigirAutor(autor);
        var cotacoes = tabelas.Where(x => x.Parceiro.EmpresaId == EmpresaId && x.Parceiro.Ativo &&
            x.Tabela.Ativa && x.Tabela.EmpresaId == EmpresaId && x.Tabela.TransportadoraId == x.Parceiro.Id &&
            x.Tabela.UfOrigem == Dados.OrigemUf && x.Tabela.UfDestino == Dados.DestinoUf)
            .Select(x => (x.Parceiro, Valor: x.Tabela.Cotar(Dados.PesoKg, Dados.VolumeM3, fator), x.Tabela.PrazoDias))
            .ToList();
        if (cotacoes.Count == 0) throw new RotaSemAtendimento();
        foreach (var item in cotacoes) Validacao.DecimalNoIntervalo(item.Valor, 2, 9999999999.99m, false);
        foreach (var item in cotacoes)
            Registrar(item.Parceiro, OrigemProposta.TABELA, item.Valor, item.PrazoDias, null, autor, instante);
    }

    public Proposta RegistrarManual(Transportadora parceiro, decimal valor, int dias, string? observacao,
        Usuario autor, DateTimeOffset instante) =>
        Registrar(parceiro, OrigemProposta.MANUAL, valor, dias, observacao, autor, instante);

    private Proposta Registrar(Transportadora parceiro, OrigemProposta origem, decimal valor, int dias,
        string? observacao, Usuario autor, DateTimeOffset instante)
    {
        ExigirEditavel();
        ExigirAutor(autor);
        Validacao.Exigir(parceiro.EmpresaId == EmpresaId && parceiro.Ativo, "Transportadora indisponível.");
        var proposta = _propostas.SingleOrDefault(p => p.TransportadoraId == parceiro.Id);
        bool nova = proposta == null;
        proposta ??= new(this, parceiro, instante);
        proposta.Atualizar(origem, valor, dias, observacao);
        if (nova) _propostas.Add(proposta);
        if (Status == ABERTO) Transitar(EM_COTACAO, autor, instante, null);
        AtualizadoEm = instante.ToUniversalTime();
        return proposta;
    }

    public void RemoverProposta(Guid id, DateTimeOffset instante)
    {
        ExigirEditavel();
        Validacao.Exigir(_propostas.RemoveAll(p => p.Id == id) == 1, "Proposta não encontrada.");
        AtualizadoEm = instante.ToUniversalTime();
    }

    public void Selecionar(Guid propostaId, Usuario autor, DateTimeOffset instante)
    {
        ExigirAutor(autor);
        Validacao.Exigir(Status is EM_COTACAO or CONTRATADO, "O status atual não permite selecionar proposta.");
        var proposta = _propostas.SingleOrDefault(p => p.Id == propostaId)
            ?? throw new RegraViolada("Proposta não encontrada.");
        var data = DateOnly.FromDateTime(instante.UtcDateTime);
        Validacao.Exigir(proposta.PrazoDias <= DateOnly.MaxValue.DayNumber - data.DayNumber, "Prazo fora do calendário.");
        foreach (var item in _propostas) item.Selecionada = item == proposta;
        PropostaSelecionadaId = proposta.Id;
        TransportadoraId = proposta.TransportadoraId;
        ValorContratado = proposta.Valor;
        PrazoPrevisto = data.AddDays(proposta.PrazoDias);
        if (Status == EM_COTACAO) Transitar(CONTRATADO, autor, instante, null);
        AtualizadoEm = instante.ToUniversalTime();
    }

    public void AlterarStatus(StatusFrete novo, Usuario autor, DateTimeOffset instante, string? observacao = null)
    {
        ExigirAutor(autor);
        bool permitida = (Status, novo) switch
        {
            (ABERTO or EM_COTACAO or CONTRATADO, CANCELADO) => !string.IsNullOrWhiteSpace(observacao),
            (CONTRATADO, COLETADO) or (COLETADO, EM_TRANSITO or ENTREGUE) or (EM_TRANSITO, ENTREGUE) => true,
            _ => false
        };
        Validacao.Exigir(permitida, "Transição não permitida ou motivo do cancelamento ausente.");
        Transitar(novo, autor, instante, observacao);
        if (novo == ENTREGUE) EntregueEm = AtualizadoEm;
    }

    private void ExigirAutor(Usuario autor) =>
        Validacao.Exigir(autor.EmpresaId == EmpresaId && autor.Ativo, "Usuário sem acesso ao frete.");

    private void Transitar(StatusFrete novo, Usuario autor, DateTimeOffset instante, string? observacao)
    {
        _historico.Add(new(this, Status, novo, autor, instante, observacao));
        Status = novo;
        AtualizadoEm = instante.ToUniversalTime();
    }

    public bool Atrasado(DateOnly hoje) => Status is not (ENTREGUE or CANCELADO) && PrazoPrevisto < hoje;
    public bool EntregueNoPrazo() => EntregueEm is { } entrega && DateOnly.FromDateTime(entrega.UtcDateTime) <= PrazoPrevisto;
}
