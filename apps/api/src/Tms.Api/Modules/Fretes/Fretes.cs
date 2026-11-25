using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tms.Api.Auth;
using Tms.Domain;
using Tms.Infrastructure;

namespace Tms.Api.Modules.Fretes;

internal static class Fretes
{
    internal static void Mapear(RouteGroupBuilder api)
    {
        api.MapGet("/fretes", Listar);
        api.MapGet("/fretes/{id:guid}", Obter);
        api.MapPost("/fretes", Criar);
        api.MapPut("/fretes/{id:guid}", Atualizar);
        api.MapPost("/fretes/{id:guid}/cotacao/simular", Simular);
        api.MapPost("/fretes/{id:guid}/propostas", RegistrarProposta);
        api.MapDelete("/fretes/{id:guid}/propostas/{propostaId:guid}", RemoverProposta);
        api.MapPost("/fretes/{id:guid}/propostas/{propostaId:guid}/selecionar", Selecionar);
        api.MapPost("/fretes/{id:guid}/status", AlterarStatus);
        api.MapGet("/fretes/{id:guid}/documentos", ListarDocumentos);
        api.MapPost("/fretes/{id:guid}/documentos", CriarDocumento);
    }

    private static async Task<IResult> Listar(string? status, string? busca, bool? atrasados,
        TmsDbContext db, CancellationToken cancel, int pagina = 1, int tamanho = 20)
    {
        pagina = Math.Max(1, pagina);
        tamanho = Math.Clamp(tamanho, 1, 100);
        var consulta = db.Fretes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<StatusFrete>(status, true, out var estado) || !Enum.IsDefined(estado))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Selecione um status válido."] });
            consulta = consulta.Where(x => x.Status == estado);
        }
        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim().ToLowerInvariant();
            consulta = consulta.Where(x => x.Codigo.ToLower().Contains(termo) ||
                x.Dados.DestinatarioNome.ToLower().Contains(termo) ||
                x.Dados.OrigemCidade.ToLower().Contains(termo) || x.Dados.DestinoCidade.ToLower().Contains(termo));
        }
        if (atrasados == true)
        {
            var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
            consulta = consulta.Where(x => x.PrazoPrevisto < hoje && x.Status != StatusFrete.ENTREGUE && x.Status != StatusFrete.CANCELADO);
        }

        var total = await consulta.CountAsync(cancel);
        var itens = await consulta.OrderByDescending(x => x.CriadoEm).Skip((pagina - 1) * tamanho).Take(tamanho)
            .Select(x => new FreteListaDto(x.Id, x.Codigo, x.Status.ToString(), x.Dados.OrigemCidade,
                x.Dados.OrigemUf, x.Dados.DestinoCidade, x.Dados.DestinoUf, x.Dados.DestinatarioNome,
                x.TransportadoraId, x.ValorContratado, x.PrazoPrevisto, x.Atrasado(DateOnly.FromDateTime(DateTime.UtcNow)), x.CriadoEm))
            .ToListAsync(cancel);
        return Results.Ok(new PaginaDto(itens, pagina, tamanho, total));
    }

    private static async Task<IResult> Obter(Guid id, TmsDbContext db, CancellationToken cancel)
    {
        var frete = await Agregado(db).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancel);
        if (frete is null) return Results.NotFound();
        var nomes = await db.Transportadoras.AsNoTracking().Where(x => frete.Propostas.Select(p => p.TransportadoraId).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Nome, cancel);
        var propostas = frete.Propostas.OrderBy(x => x.Valor).Select(x => new PropostaDto(x.Id, x.TransportadoraId,
            nomes.GetValueOrDefault(x.TransportadoraId, "Cadastro indisponível"), x.Origem.ToString(), x.Valor,
            x.PrazoDias, x.Observacao, x.Selecionada)).ToList();
        var documentos = await db.Documentos.AsNoTracking().Where(x => x.FreteId == id).OrderBy(x => x.CriadoEm)
            .Select(x => ParaDocumento(x)).ToListAsync(cancel);
        var historico = frete.Historico.OrderBy(x => x.OcorridoEm).Select(x => new HistoricoDto(
            x.StatusAnterior.HasValue ? x.StatusAnterior.Value.ToString() : null, x.StatusNovo.ToString(),
            x.Observacao, x.UsuarioId, x.OcorridoEm)).ToList();
        return Results.Ok(new FreteDetalheDto(frete.Id, frete.Codigo, frete.Status.ToString(), frete.Dados,
            frete.CriadoPor, frete.TransportadoraId, frete.PropostaSelecionadaId, frete.ValorContratado,
            frete.PrazoPrevisto, frete.EntregueEm, frete.CriadoEm, frete.AtualizadoEm, propostas, historico, documentos));
    }

    private static async Task<IResult> Criar(EntradaFrete entrada, UsuarioDaRequisicao sessao,
        TmsDbContext db, CancellationToken cancel)
    {
        var erros = Validar(entrada);
        if (erros.Count > 0) return Results.ValidationProblem(erros);
        var agora = DateTimeOffset.UtcNow;
        string codigo = await ProximoCodigo(db, agora.Year, cancel);
        try
        {
            var item = new Frete(sessao.Usuario, codigo, ParaDados(entrada), agora);
            db.Fretes.Add(item);
            await db.SaveChangesAsync(cancel);
            return Results.Created($"/api/fretes/{item.Id}", Resumo(item));
        }
        catch (RegraViolada ex) { return Conflito(ex.Message); }
        catch (DbUpdateException ex) when (EhUnicidade(ex)) { return Conflito("O código do frete já foi utilizado. Tente novamente."); }
    }

    private static async Task<IResult> Atualizar(Guid id, EntradaFrete entrada, TmsDbContext db, CancellationToken cancel)
    {
        var erros = Validar(entrada);
        if (erros.Count > 0) return Results.ValidationProblem(erros);
        var item = await Agregado(db).SingleOrDefaultAsync(x => x.Id == id, cancel);
        if (item is null) return Results.NotFound();
        try
        {
            item.Editar(ParaDados(entrada), DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(cancel);
            return Results.Ok(Resumo(item));
        }
        catch (RegraViolada ex) { return Conflito(ex.Message); }
        catch (DbUpdateConcurrencyException) { return Conflito("O frete foi alterado por outra pessoa. Atualize a tela e tente novamente."); }
    }

    private static async Task<IResult> Simular(Guid id, UsuarioDaRequisicao sessao, TmsDbContext db, CancellationToken cancel)
    {
        var item = await Agregado(db).SingleOrDefaultAsync(x => x.Id == id, cancel);
        if (item is null) return Results.NotFound();
        var tabelas = await (from tabela in db.TabelasFrete.AsNoTracking()
                             join parceiro in db.Transportadoras.AsNoTracking() on tabela.TransportadoraId equals parceiro.Id
                             where tabela.Ativa && parceiro.Ativo && tabela.UfOrigem == item.Dados.OrigemUf && tabela.UfDestino == item.Dados.DestinoUf
                             select new { tabela, parceiro }).ToListAsync(cancel);
        try
        {
            item.Simular(tabelas.Select(x => (x.parceiro, x.tabela)), sessao.Usuario, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(cancel);
            return Results.Ok(item.Propostas.OrderBy(x => x.Valor).Select(x =>
                new { x.Id, x.TransportadoraId, origem = x.Origem.ToString(), x.Valor, x.PrazoDias, x.Selecionada }));
        }
        catch (RotaSemAtendimento ex) { return Results.Problem(statusCode: 422, title: ex.Message); }
        catch (RegraViolada ex) { return Conflito(ex.Message); }
        catch (DbUpdateConcurrencyException) { return Conflito("O frete foi alterado por outra pessoa. Atualize a tela e tente novamente."); }
    }

    private static async Task<IResult> RegistrarProposta(Guid id, EntradaProposta entrada,
        UsuarioDaRequisicao sessao, TmsDbContext db, CancellationToken cancel)
    {
        if (entrada.TransportadoraId == Guid.Empty || entrada.Valor <= 0 || entrada.PrazoDias < 1)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["proposta"] = ["Informe transportadora, valor positivo e prazo de pelo menos um dia."] });
        var frete = await Agregado(db).SingleOrDefaultAsync(x => x.Id == id, cancel);
        if (frete is null) return Results.NotFound();
        var parceiro = await db.Transportadoras.SingleOrDefaultAsync(x => x.Id == entrada.TransportadoraId, cancel);
        if (parceiro is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["transportadoraId"] = ["Selecione uma transportadora ativa desta empresa."] });
        try
        {
            frete.RegistrarManual(parceiro, entrada.Valor, entrada.PrazoDias, entrada.Observacao?.Trim(), sessao.Usuario, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(cancel);
            return Results.Ok(frete.Propostas.Single(x => x.TransportadoraId == parceiro.Id));
        }
        catch (RegraViolada ex) { return Conflito(ex.Message); }
        catch (DbUpdateConcurrencyException) { return Conflito("O frete foi alterado por outra pessoa. Atualize a tela e tente novamente."); }
    }

    private static async Task<IResult> RemoverProposta(Guid id, Guid propostaId, TmsDbContext db, CancellationToken cancel)
    {
        var frete = await Agregado(db).SingleOrDefaultAsync(x => x.Id == id, cancel);
        if (frete is null) return Results.NotFound();
        try
        {
            frete.RemoverProposta(propostaId, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(cancel);
            return Results.NoContent();
        }
        catch (RegraViolada ex) { return Conflito(ex.Message); }
        catch (DbUpdateConcurrencyException) { return Conflito("O frete foi alterado por outra pessoa. Atualize a tela e tente novamente."); }
    }

    private static async Task<IResult> Selecionar(Guid id, Guid propostaId, UsuarioDaRequisicao sessao,
        TmsDbContext db, CancellationToken cancel)
    {
        var frete = await Agregado(db).SingleOrDefaultAsync(x => x.Id == id, cancel);
        if (frete is null) return Results.NotFound();
        try
        {
            frete.Selecionar(propostaId, sessao.Usuario, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(cancel);
            return Results.Ok(Resumo(frete));
        }
        catch (RegraViolada ex) { return Conflito(ex.Message); }
        catch (DbUpdateConcurrencyException) { return Conflito("O frete foi alterado por outra pessoa. Atualize a tela e tente novamente."); }
    }

    private static async Task<IResult> AlterarStatus(Guid id, EntradaStatus entrada, UsuarioDaRequisicao sessao,
        TmsDbContext db, CancellationToken cancel)
    {
        if (!Enum.TryParse<StatusFrete>(entrada.NovoStatus, true, out var novo) || !Enum.IsDefined(novo))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["novoStatus"] = ["Selecione um status válido."] });
        var frete = await Agregado(db).SingleOrDefaultAsync(x => x.Id == id, cancel);
        if (frete is null) return Results.NotFound();
        try
        {
            frete.AlterarStatus(novo, sessao.Usuario, DateTimeOffset.UtcNow, entrada.Observacao?.Trim());
            await db.SaveChangesAsync(cancel);
            return Results.Ok(Resumo(frete));
        }
        catch (RegraViolada ex) { return Conflito(ex.Message); }
        catch (DbUpdateConcurrencyException) { return Conflito("O frete foi alterado por outra pessoa. Atualize a tela e tente novamente."); }
    }

    private static async Task<IResult> ListarDocumentos(Guid id, TmsDbContext db, CancellationToken cancel)
    {
        if (!await db.Fretes.AnyAsync(x => x.Id == id, cancel)) return Results.NotFound();
        var itens = await db.Documentos.AsNoTracking().Where(x => x.FreteId == id).OrderBy(x => x.CriadoEm)
            .Select(x => ParaDocumento(x)).ToListAsync(cancel);
        return Results.Ok(itens);
    }

    private static async Task<IResult> CriarDocumento(Guid id, EntradaDocumento entrada, TmsDbContext db, CancellationToken cancel)
    {
        if (!Enum.TryParse<TipoDocumento>(entrada.Tipo, true, out var tipo) || !Enum.IsDefined(tipo) ||
            string.IsNullOrWhiteSpace(entrada.Numero) || entrada.Numero.Trim().Length > 60)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tipo"] = ["Informe o tipo e o número do documento."] });
        var frete = await db.Fretes.SingleOrDefaultAsync(x => x.Id == id, cancel);
        if (frete is null) return Results.NotFound();
        try
        {
            var documento = new DocumentoFiscal(frete, tipo, entrada.Numero.Trim(),
                string.IsNullOrWhiteSpace(entrada.ChaveAcesso) ? null : entrada.ChaveAcesso.Trim(), entrada.Valor);
            db.Documentos.Add(documento);
            await db.SaveChangesAsync(cancel);
            return Results.Created($"/api/fretes/{id}/documentos", ParaDocumento(documento));
        }
        catch (RegraViolada ex) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["chaveAcesso"] = [ex.Message] }); }
    }

    private static IQueryable<Frete> Agregado(TmsDbContext db) =>
        db.Fretes.AsSplitQuery().Include(x => x.Propostas).Include(x => x.Historico);

    private static async Task<string> ProximoCodigo(TmsDbContext db, int ano, CancellationToken cancel)
    {
        string prefixo = $"FR-{ano}-";
        var codigos = await db.Fretes.AsNoTracking().Where(x => x.Codigo.StartsWith(prefixo)).Select(x => x.Codigo).ToListAsync(cancel);
        int ultimo = codigos.Select(x => int.TryParse(x.AsSpan(prefixo.Length), out var n) ? n : 0).DefaultIfEmpty().Max();
        if (ultimo >= 999999) throw new InvalidOperationException("Não há mais códigos disponíveis para este ano.");
        return prefixo + (ultimo + 1).ToString("D6");
    }

    private static Dictionary<string, string[]> Validar(EntradaFrete entrada)
    {
        var erros = new Dictionary<string, string[]>();
        ValidarCep(entrada.OrigemCep, "origemCep", erros);
        ValidarCep(entrada.DestinoCep, "destinoCep", erros);
        if (!Validacao.Uf((entrada.OrigemUf ?? "").Trim().ToUpperInvariant())) erros["origemUf"] = ["Informe uma UF de origem válida."];
        if (!Validacao.Uf((entrada.DestinoUf ?? "").Trim().ToUpperInvariant())) erros["destinoUf"] = ["Informe uma UF de destino válida."];
        if (string.IsNullOrWhiteSpace(entrada.OrigemCidade)) erros["origemCidade"] = ["Informe a cidade de origem."];
        if (string.IsNullOrWhiteSpace(entrada.DestinoCidade)) erros["destinoCidade"] = ["Informe a cidade de destino."];
        if (string.IsNullOrWhiteSpace(entrada.DestinatarioNome)) erros["destinatarioNome"] = ["Informe o destinatário."];
        if (entrada.PesoKg <= 0 || entrada.PesoKg > 99999999.99m || decimal.Round(entrada.PesoKg, 2) != entrada.PesoKg)
            erros["pesoKg"] = ["Informe um peso positivo com até duas casas decimais."];
        if (entrada.VolumeM3 < 0 || entrada.VolumeM3 > 9999999.999m || decimal.Round(entrada.VolumeM3, 3) != entrada.VolumeM3)
            erros["volumeM3"] = ["Informe um volume igual ou maior que zero com até três casas decimais."];
        if (entrada.ValorMercadoria is decimal valor &&
            (valor <= 0 || valor > 9999999999.99m || decimal.Round(valor, 2) != valor))
            erros["valorMercadoria"] = ["Informe um valor positivo com até duas casas decimais."];
        return erros;
    }

    private static void ValidarCep(string? cep, string campo, Dictionary<string, string[]> erros)
    {
        string valor = (cep ?? "").Trim();
        string digitos = new(valor.Where(char.IsAsciiDigit).ToArray());
        bool formatoPermitido = valor.All(c => char.IsAsciiDigit(c) || c is '-' or '.' || char.IsWhiteSpace(c));
        if (!formatoPermitido || !Validacao.Cep(digitos)) erros[campo] = ["Informe um CEP com oito números."];
    }

    private static DadosFrete ParaDados(EntradaFrete x) => new(
        new string(x.OrigemCep!.Where(char.IsAsciiDigit).ToArray()), x.OrigemCidade!.Trim(), x.OrigemUf!.Trim().ToUpperInvariant(),
        new string(x.DestinoCep!.Where(char.IsAsciiDigit).ToArray()), x.DestinoCidade!.Trim(), x.DestinoUf!.Trim().ToUpperInvariant(),
        x.DestinatarioNome!.Trim(), x.PesoKg, x.VolumeM3, x.ValorMercadoria, x.Observacao?.Trim());

    private static FreteResumoDto Resumo(Frete x) => new(x.Id, x.Codigo, x.Status.ToString(), x.Dados,
        x.TransportadoraId, x.ValorContratado, x.PrazoPrevisto, x.CriadoEm);

    private static DocumentoDto ParaDocumento(DocumentoFiscal x) =>
        new(x.Id, x.FreteId, x.Tipo.ToString(), x.Numero, x.ChaveAcesso?.Trim(), x.Valor, x.CriadoEm);

    private static bool EhUnicidade(DbUpdateException ex) => ex.GetBaseException() is PostgresException
    { SqlState: PostgresErrorCodes.UniqueViolation };
    private static IResult Conflito(string titulo) => Results.Problem(statusCode: 409, title: titulo);

    private sealed record EntradaFrete(string? OrigemCep, string? OrigemCidade, string? OrigemUf,
        string? DestinoCep, string? DestinoCidade, string? DestinoUf, string? DestinatarioNome,
        decimal PesoKg, decimal VolumeM3, decimal? ValorMercadoria, string? Observacao);
    private sealed record EntradaProposta(Guid TransportadoraId, decimal Valor, int PrazoDias, string? Observacao);
    private sealed record EntradaStatus(string? NovoStatus, string? Observacao);
    private sealed record EntradaDocumento(string? Tipo, string? Numero, string? ChaveAcesso, decimal? Valor);
    private sealed record FreteListaDto(Guid Id, string Codigo, string Status, string OrigemCidade, string OrigemUf,
        string DestinoCidade, string DestinoUf, string DestinatarioNome, Guid? TransportadoraId,
        decimal? ValorContratado, DateOnly? PrazoPrevisto, bool Atrasado, DateTimeOffset CriadoEm);
    private sealed record PaginaDto(IReadOnlyList<FreteListaDto> Itens, int Pagina, int Tamanho, int Total);
    private sealed record FreteResumoDto(Guid Id, string Codigo, string Status, DadosFrete Dados, Guid? TransportadoraId,
        decimal? ValorContratado, DateOnly? PrazoPrevisto, DateTimeOffset CriadoEm);
    private sealed record FreteDetalheDto(Guid Id, string Codigo, string Status, DadosFrete Dados, Guid CriadoPor,
        Guid? TransportadoraId, Guid? PropostaSelecionadaId, decimal? ValorContratado, DateOnly? PrazoPrevisto,
        DateTimeOffset? EntregueEm, DateTimeOffset CriadoEm, DateTimeOffset AtualizadoEm,
        IReadOnlyList<PropostaDto> Propostas, IReadOnlyList<HistoricoDto> Historico, IReadOnlyList<DocumentoDto> Documentos);
    private sealed record PropostaDto(Guid Id, Guid TransportadoraId, string Transportadora, string Origem,
        decimal Valor, int PrazoDias, string? Observacao, bool Selecionada);
    private sealed record HistoricoDto(string? StatusAnterior, string StatusNovo, string? Observacao,
        Guid UsuarioId, DateTimeOffset OcorridoEm);
    private sealed record DocumentoDto(Guid Id, Guid FreteId, string Tipo, string Numero, string? ChaveAcesso,
        decimal? Valor, DateTimeOffset CriadoEm);
}
