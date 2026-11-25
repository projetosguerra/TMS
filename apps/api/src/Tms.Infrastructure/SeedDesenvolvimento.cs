using Microsoft.EntityFrameworkCore;
using Tms.Domain;
using static Tms.Domain.StatusFrete;

namespace Tms.Infrastructure;

internal static class IdentidadesDemo
{
    // Apenas identificadores locais. O seed não provisiona contas no Supabase Auth.
    internal static readonly Guid UsuarioPrincipal = new("00000000-0000-4000-8000-000000000001");
    internal static readonly Guid UsuarioSecundario = new("00000000-0000-4000-8000-000000000002");
}

public static class SeedDesenvolvimento
{
    public static async Task ExecutarAsync(TmsDbContext db)
    {
        await using var transacao = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(20406731)");
        await InserirEmpresa(db, true);
        await InserirEmpresa(db, false);
        await transacao.CommitAsync();
    }

    private static async Task InserirEmpresa(TmsDbContext db, bool principal)
    {
        string cnpj = principal ? "99000001000101" : "99000002000148";
        if (await db.Empresas.IgnoreQueryFilters().AnyAsync(e => e.Cnpj == cnpj)) return;
        var empresa = new Empresa(principal ? "Embarcadora Fictícia Alfa" : "Embarcadora Fictícia Beta", cnpj);
        string nome = principal ? "alfa" : "beta";
        var usuario = new Usuario(principal ? IdentidadesDemo.UsuarioPrincipal : IdentidadesDemo.UsuarioSecundario,
            empresa.Id, "Operador fictício " + nome, nome + "@example.invalid", Perfil.ADMIN);
        db.AddRange(empresa, usuario);
        string[] destinos = ["SP", "RJ", "MG", "PR"];
        string[] documentos = ["99000003000192", "99000004000137", "99000005000181", "90000000175"];
        var parceiros = new List<Transportadora>();
        for (int i = 0; i < (principal ? 4 : 1); i++)
        {
            var parceiro = new Transportadora(empresa.Id, i == 3 ? TipoParceiro.AUTONOMO : TipoParceiro.TRANSPORTADORA,
                $"Parceiro fictício {nome} {i + 1}", documentos[i]);
            parceiros.Add(parceiro);
            db.AddRange(parceiro, new TabelaFrete(parceiro, "SP", destinos[i], 40 + i, 1.5m, i + 1));
        }

        StatusFrete[] estados = [ABERTO, EM_COTACAO, CONTRATADO, COLETADO, EM_TRANSITO, ENTREGUE, CANCELADO,
            ABERTO, CONTRATADO, ENTREGUE, EM_COTACAO, EM_TRANSITO];
        var inicio = DateTimeOffset.UtcNow.AddDays(-20);
        for (int i = 0; i < (principal ? estados.Length : 1); i++)
        {
            int rota = i % parceiros.Count;
            var dados = new DadosFrete("01000000", "Cidade fictícia A", "SP", "02000000", "Cidade fictícia B",
                destinos[rota], "Destinatário fictício", 20 + i);
            var data = inicio.AddDays(i);
            var frete = new Frete(usuario, $"FR-2026-{(principal ? i + 1 : 900001):D6}", dados, data);
            db.Add(frete);
            var estado = estados[i];
            if (estado == CANCELADO) frete.AlterarStatus(CANCELADO, usuario, data.AddHours(1), "Pedido fictício cancelado");
            else if (estado != ABERTO)
            {
                var proposta = frete.RegistrarManual(parceiros[rota], 50 + i, 2, null, usuario, data.AddHours(1));
                // A FK da proposta referencia o frete; a seleção referencia a proposta já persistida.
                await db.SaveChangesAsync();
                if (estado != EM_COTACAO) frete.Selecionar(proposta.Id, usuario, data.AddHours(2));
                if (estado is COLETADO or EM_TRANSITO or ENTREGUE) frete.AlterarStatus(COLETADO, usuario, data.AddDays(1));
                if (estado == EM_TRANSITO) frete.AlterarStatus(EM_TRANSITO, usuario, data.AddDays(2));
                if (estado == ENTREGUE) frete.AlterarStatus(ENTREGUE, usuario, data.AddDays(2));
            }
            await db.SaveChangesAsync();
        }
    }
}
