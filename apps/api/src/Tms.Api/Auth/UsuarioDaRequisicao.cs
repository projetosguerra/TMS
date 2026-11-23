using Microsoft.EntityFrameworkCore;
using Tms.Domain;
using Tms.Infrastructure;

namespace Tms.Api.Auth;

public sealed class UsuarioDaRequisicao(TmsDbContext db)
{
    public Usuario Usuario { get; private set; } = null!;

    public async Task<bool> ResolverAsync(Guid sub, CancellationToken cancel)
    {
        // Única consulta anterior ao escopo de empresa. O identificador vem do JWT validado.
        var usuario = await db.Usuarios.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == sub && u.Ativo, cancel);
        if (usuario is null) return false;
        db.UsarEmpresa(usuario);
        Usuario = usuario;
        return true;
    }
}

internal sealed class FiltroUsuario : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var sessao = http.RequestServices.GetRequiredService<UsuarioDaRequisicao>();
        if (!Guid.TryParse(http.User.FindFirst("sub")?.Value, out var sub) ||
            !await sessao.ResolverAsync(sub, http.RequestAborted))
            return Results.Problem(statusCode: 403, title: "Acesso não permitido.");
        return await next(context);
    }
}
