using Microsoft.EntityFrameworkCore;
using Tms.Domain;
using Tms.Infrastructure;

namespace Tms.Api.Modules.Dashboard;

internal static class Dashboard
{
    internal static void Mapear(RouteGroupBuilder api) => api.MapGet("/dashboard", Obter);

    private static async Task<IResult> Obter(DateOnly? de, DateOnly? ate, TmsDbContext db, CancellationToken cancel)
    {
        var inicio = de ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var fim = ate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (inicio > fim)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["de"] = ["A data inicial deve ser anterior ou igual a data final."] });

        var fretes = await db.Fretes.AsNoTracking().ToListAsync(cancel);
        var entregues = fretes.Where(x => x.Status == StatusFrete.ENTREGUE && x.EntregueEm.HasValue)
            .Where(x => DateOnly.FromDateTime(x.EntregueEm!.Value.UtcDateTime) >= inicio &&
                DateOnly.FromDateTime(x.EntregueEm.Value.UtcDateTime) <= fim).ToList();
        var custos = entregues.Where(x => x.ValorContratado.HasValue).Select(x => x.ValorContratado!.Value).ToList();
        var idsParceiros = entregues.Where(x => x.TransportadoraId.HasValue).Select(x => x.TransportadoraId!.Value).Distinct().ToList();
        var nomes = await db.Transportadoras.AsNoTracking().Where(x => idsParceiros.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Nome, cancel);
        var ranking = entregues.Where(x => x.TransportadoraId.HasValue).GroupBy(x => x.TransportadoraId!.Value)
            .Select(grupo =>
            {
                var itens = grupo.ToList();
                var noPrazo = itens.Count(x => x.EntregueNoPrazo());
                var valores = itens.Where(x => x.ValorContratado.HasValue).Select(x => x.ValorContratado!.Value).ToList();
                return new RankingDto(grupo.Key, nomes.GetValueOrDefault(grupo.Key, "Cadastro indisponível"), itens.Count,
                    valores.Count == 0 ? 0 : decimal.Round(valores.Average(), 2),
                    itens.Count == 0 ? 0 : decimal.Round(noPrazo * 100m / itens.Count, 1));
            }).OrderByDescending(x => x.Quantidade).ThenBy(x => x.Transportadora).ToList();
        var diasEntrega = entregues.Select(x => (x.EntregueEm!.Value.UtcDateTime - x.CriadoEm.UtcDateTime).TotalDays).ToList();
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var atrasados = fretes.Count(x => x.PrazoPrevisto < hoje && x.Status is not (StatusFrete.ENTREGUE or StatusFrete.CANCELADO));
        var valorTotal = custos.Sum();

        return Results.Ok(new DashboardDto(
            fretes.Count(x => x.Status is not (StatusFrete.ENTREGUE or StatusFrete.CANCELADO)),
            entregues.Count,
            entregues.Count == 0 ? 0 : decimal.Round(entregues.Count(x => x.EntregueNoPrazo()) * 100m / entregues.Count, 1),
            valorTotal,
            custos.Count == 0 ? 0 : decimal.Round(valorTotal / custos.Count, 2),
            diasEntrega.Count == 0 ? 0 : decimal.Round((decimal)diasEntrega.Average(), 1),
            atrasados, ranking, inicio, fim));
    }

    private sealed record DashboardDto(int FretesAtivos, int EntreguesNoPeriodo, decimal PercentualNoPrazo,
        decimal CustoTotal, decimal CustoMedioPorFrete, decimal TempoMedioEntregaDias,
        int FretesAtrasados, IReadOnlyList<RankingDto> RankingTransportadoras, DateOnly De, DateOnly Ate);
    private sealed record RankingDto(Guid TransportadoraId, string Transportadora, int Quantidade,
        decimal CustoMedio, decimal PercentualNoPrazo);
}
