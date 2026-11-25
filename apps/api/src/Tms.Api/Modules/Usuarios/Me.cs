using Microsoft.EntityFrameworkCore;
using Tms.Api.Auth;
using Tms.Infrastructure;

namespace Tms.Api.Modules.Usuarios;

internal static class Me
{
    internal static void Mapear(RouteGroupBuilder grupo) =>
        grupo.MapGet("/me", async (UsuarioDaRequisicao sessao, TmsDbContext db, CancellationToken cancel) =>
        {
            var u = sessao.Usuario;
            var empresa = await db.Empresas.Select(e => new { e.Id, e.Nome }).SingleAsync(cancel);
            return Results.Ok(new { u.Id, u.Nome, u.Email, Perfil = u.Perfil.ToString(), Empresa = empresa });
        }).ProducesProblem(401).ProducesProblem(403);
}
