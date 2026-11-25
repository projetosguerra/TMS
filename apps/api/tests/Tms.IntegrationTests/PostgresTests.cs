using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tms.Api.Auth;
using Tms.Infrastructure;
using Xunit;

namespace Tms.IntegrationTests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TmsTest__Postgres")))
            Skip = "Defina TmsTest__Postgres para um PostgreSQL local dedicado chamado tms_test.";
    }
}

public sealed class BancoDeTeste
{
    private readonly Lazy<Task<DbContextOptions<TmsDbContext>>> _banco = new(Preparar);
    public async Task<TmsDbContext> Abrir() => new(await _banco.Value);

    private static async Task<DbContextOptions<TmsDbContext>> Preparar()
    {
        var cs = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TmsTest__Postgres"));
        if (cs.Database != "tms_test" || cs.Host is not ("localhost" or "127.0.0.1" or "::1"))
            throw new InvalidOperationException("A suíte exige o banco local dedicado tms_test.");
        string schema = "ensaio_" + Guid.NewGuid().ToString("N");
        cs.SearchPath = schema;
        await using (var con = new NpgsqlConnection(cs.ConnectionString))
        {
            await con.OpenAsync();
            await using var criar = new NpgsqlCommand("CREATE SCHEMA " + schema, con);
            await criar.ExecuteNonQueryAsync();
        }
        var opcoes = new DbContextOptionsBuilder<TmsDbContext>()
            .UseNpgsql(cs.ConnectionString, pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", schema)).Options;
        await using var db = new TmsDbContext(opcoes);
        await db.Database.MigrateAsync();
        await SeedDesenvolvimento.ExecutarAsync(db);
        return opcoes;
    }
}

public sealed class PostgresTests(BancoDeTeste banco) : IClassFixture<BancoDeTeste>
{
    private static readonly Guid Alfa = Guid.Parse("00000000-0000-4000-8000-000000000001");
    private static readonly Guid Beta = Guid.Parse("00000000-0000-4000-8000-000000000002");

    [PostgresFact]
    public async Task MigrationAplicadaESeedRepetidoPreservaRegistros()
    {
        await using var db = await banco.Abrir();
        Assert.Contains("20261119165808_Inicial", await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        async Task<int[]> Contar() => [
            await db.Empresas.IgnoreQueryFilters().CountAsync(), await db.Usuarios.IgnoreQueryFilters().CountAsync(),
            await db.Transportadoras.IgnoreQueryFilters().CountAsync(), await db.TabelasFrete.IgnoreQueryFilters().CountAsync(),
            await db.Fretes.IgnoreQueryFilters().CountAsync(), await db.Propostas.IgnoreQueryFilters().CountAsync(),
            await db.Historico.IgnoreQueryFilters().CountAsync(), await db.Documentos.IgnoreQueryFilters().CountAsync()];
        var antes = await Contar();
        Assert.Equal(new[] { 2, 2, 5, 5, 13 }, antes[..5]);
        await SeedDesenvolvimento.ExecutarAsync(db);
        Assert.Equal(antes, await Contar());
    }

    [PostgresFact]
    public async Task EscopoDaApiIsolaFretesETransportadorasPersistidos()
    {
        foreach (var (sub, fretes, parceiros) in new[] { (Alfa, 12, 4), (Beta, 1, 1) })
        {
            await using var db = await banco.Abrir();
            Assert.Empty(await db.Fretes.ToListAsync());
            var sessao = new UsuarioDaRequisicao(db);
            Assert.True(await sessao.ResolverAsync(sub, default));
            var cargas = await db.Fretes.AsNoTracking().Include(f => f.Propostas).ToListAsync();
            var lista = await db.Transportadoras.AsNoTracking().ToListAsync();
            Assert.Equal(fretes, cargas.Count);
            Assert.Equal(parceiros, lista.Count);
            Assert.All(lista, t => Assert.Equal(sessao.Usuario.EmpresaId, t.EmpresaId));
            Assert.All(cargas, f => Assert.Equal(sessao.Usuario.EmpresaId, f.EmpresaId));
            if (sub == Alfa)
            {
                Assert.Contains(cargas, f => f.PropostaSelecionadaId.HasValue);
                foreach (var f in cargas.Where(f => f.PropostaSelecionadaId.HasValue))
                {
                    var selecionada = Assert.Single(f.Propostas, p => p.Selecionada);
                    Assert.Equal(f.PropostaSelecionadaId, selecionada.Id);
                    Assert.Equal(f.TransportadoraId, selecionada.TransportadoraId);
                }
            }
        }
    }

    [PostgresFact]
    public async Task FkRecusaParceiroDeOutraEmpresa()
    {
        await using var db = await banco.Abrir();
        var beta = await db.Usuarios.IgnoreQueryFilters().SingleAsync(u => u.Id == Beta);
        var outro = await db.Transportadoras.IgnoreQueryFilters().FirstAsync(t => t.EmpresaId != beta.EmpresaId);
        await using var tx = await db.Database.BeginTransactionAsync();
        var erro = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"UPDATE fretes SET transportadora_id = {outro.Id} WHERE empresa_id = {beta.EmpresaId}"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, erro.SqlState);
    }

    [PostgresFact]
    public async Task FkRecusaPropostaDeOutroFreteDaMesmaEmpresa()
    {
        await using var db = await banco.Abrir();
        var proposta = await db.Propostas.IgnoreQueryFilters().FirstAsync();
        var frete = await db.Fretes.IgnoreQueryFilters().FirstAsync(f => f.EmpresaId == proposta.EmpresaId && f.Id != proposta.FreteId);
        await using var tx = await db.Database.BeginTransactionAsync();
        var erro = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"UPDATE fretes SET proposta_selecionada_id = {proposta.Id} WHERE id = {frete.Id}"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, erro.SqlState);
    }
}
