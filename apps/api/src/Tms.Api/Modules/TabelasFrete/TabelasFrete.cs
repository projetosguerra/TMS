using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tms.Domain;
using Tms.Infrastructure;

namespace Tms.Api.Modules.TabelasFrete;

internal static class TabelasFrete
{
    internal static void Mapear(RouteGroupBuilder api)
    {
        api.MapGet("/transportadoras/{transportadoraId:guid}/tabelas", Listar);
        api.MapPost("/transportadoras/{transportadoraId:guid}/tabelas", Criar);
        api.MapPut("/tabelas-frete/{id:guid}", Atualizar);
    }

    private static async Task<IResult> Listar(Guid transportadoraId, TmsDbContext db, CancellationToken cancel)
    {
        if (!await db.Transportadoras.AnyAsync(x => x.Id == transportadoraId, cancel)) return Results.NotFound();
        var itens = await db.TabelasFrete.AsNoTracking().Where(x => x.TransportadoraId == transportadoraId)
            .OrderBy(x => x.UfOrigem).ThenBy(x => x.UfDestino).Select(x => ParaDto(x)).ToListAsync(cancel);
        return Results.Ok(itens);
    }

    private static async Task<IResult> Criar(Guid transportadoraId, Entrada entrada, TmsDbContext db, CancellationToken cancel)
    {
        var erros = Validar(entrada);
        if (erros.Count > 0) return Results.ValidationProblem(erros);
        var parceiro = await db.Transportadoras.SingleOrDefaultAsync(x => x.Id == transportadoraId, cancel);
        if (parceiro is null) return Results.NotFound();
        if (!parceiro.Ativo) return Conflito("Ative o cadastro antes de criar uma tabela de frete.");
        if (entrada.Ativa && await RotaAtivaExiste(db, transportadoraId, entrada, null, cancel))
            return Conflito("JÃ¡ existe uma tabela ativa para esta rota.");

        var item = new TabelaFrete(parceiro, entrada.UfOrigem!.Trim().ToUpperInvariant(), entrada.UfDestino!.Trim().ToUpperInvariant(),
            entrada.ValorMinimo!.Value, entrada.ValorPorKg!.Value, entrada.PrazoDias!.Value, entrada.Ativa);
        db.TabelasFrete.Add(item);
        try { await db.SaveChangesAsync(cancel); }
        catch (DbUpdateException ex) when (EhUnicidade(ex)) { return Conflito("JÃ¡ existe uma tabela ativa para esta rota."); }
        return Results.Created($"/api/tabelas-frete/{item.Id}", ParaDto(item));
    }

    private static async Task<IResult> Atualizar(Guid id, Entrada entrada, TmsDbContext db, CancellationToken cancel)
    {
        var erros = Validar(entrada);
        if (erros.Count > 0) return Results.ValidationProblem(erros);
        var item = await db.TabelasFrete.SingleOrDefaultAsync(x => x.Id == id, cancel);
        if (item is null) return Results.NotFound();
        if (entrada.Ativa && await RotaAtivaExiste(db, item.TransportadoraId, entrada, id, cancel))
            return Conflito("JÃ¡ existe uma tabela ativa para esta rota.");
        item.Atualizar(entrada.UfOrigem!, entrada.UfDestino!, entrada.ValorMinimo!.Value,
            entrada.ValorPorKg!.Value, entrada.PrazoDias!.Value, entrada.Ativa);
        try { await db.SaveChangesAsync(cancel); }
        catch (DbUpdateException ex) when (EhUnicidade(ex)) { return Conflito("JÃ¡ existe uma tabela ativa para esta rota."); }
        return Results.Ok(ParaDto(item));
    }

    private static Task<bool> RotaAtivaExiste(TmsDbContext db, Guid transportadoraId, Entrada entrada,
        Guid? ignorarId, CancellationToken cancel) => db.TabelasFrete.AnyAsync(x =>
        x.TransportadoraId == transportadoraId && x.Ativa && x.UfOrigem == entrada.UfOrigem!.Trim().ToUpperInvariant() &&
        x.UfDestino == entrada.UfDestino!.Trim().ToUpperInvariant() && (!ignorarId.HasValue || x.Id != ignorarId), cancel);

    private static Dictionary<string, string[]> Validar(Entrada entrada)
    {
        var erros = new Dictionary<string, string[]>();
        if (!Validacao.Uf((entrada.UfOrigem ?? "").Trim().ToUpperInvariant())) erros["ufOrigem"] = ["Informe uma UF de origem válida."];
        if (!Validacao.Uf((entrada.UfDestino ?? "").Trim().ToUpperInvariant())) erros["ufDestino"] = ["Informe uma UF de destino válida."];
        if (entrada.ValorMinimo is null || entrada.ValorMinimo <= 0 || entrada.ValorMinimo > 9999999999.99m || decimal.Round(entrada.ValorMinimo.Value, 2) != entrada.ValorMinimo)
            erros["valorMinimo"] = ["Informe um valor mínimo positivo com até duas casas decimais."];
        if (entrada.ValorPorKg is null || entrada.ValorPorKg <= 0 || entrada.ValorPorKg > 99999999.9999m || decimal.Round(entrada.ValorPorKg.Value, 4) != entrada.ValorPorKg)
            erros["valorPorKg"] = ["Informe um valor por quilo positivo com até quatro casas decimais."];
        if (entrada.PrazoDias is null || entrada.PrazoDias < 1) erros["prazoDias"] = ["Informe um prazo de pelo menos um dia."];
        return erros;
    }

    private static bool EhUnicidade(DbUpdateException ex) => ex.GetBaseException() is PostgresException
    { SqlState: PostgresErrorCodes.UniqueViolation };

    private static IResult Conflito(string titulo) => Results.Problem(statusCode: 409, title: titulo);
    private static TabelaDto ParaDto(TabelaFrete x) =>
        new(x.Id, x.TransportadoraId, x.UfOrigem.Trim(), x.UfDestino.Trim(), x.ValorMinimo, x.ValorPorKg, x.PrazoDias, x.Ativa);

    private sealed record Entrada(string? UfOrigem, string? UfDestino, decimal? ValorMinimo,
        decimal? ValorPorKg, int? PrazoDias, bool Ativa = true);
    private sealed record TabelaDto(Guid Id, Guid TransportadoraId, string UfOrigem, string UfDestino,
        decimal ValorMinimo, decimal ValorPorKg, int PrazoDias, bool Ativa);
}
