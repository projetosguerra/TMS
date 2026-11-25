using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tms.Api.Auth;
using Tms.Domain;
using Tms.Infrastructure;

namespace Tms.Api.Modules.Transportadoras;

internal static class Transportadoras
{
    internal static void Mapear(RouteGroupBuilder grupo)
    {
        var rotas = grupo.MapGroup("/transportadoras");
        rotas.MapGet("", Listar);
        rotas.MapGet("/{id:guid}", Obter);
        rotas.MapPost("", Criar);
        rotas.MapPut("/{id:guid}", Atualizar);
        rotas.MapPatch("/{id:guid}/ativo", AlterarAtivo);
    }

    private static async Task<IResult> Listar(bool? ativo, TmsDbContext db, CancellationToken cancel)
    {
        var consulta = db.Transportadoras.AsNoTracking();
        if (ativo.HasValue) consulta = consulta.Where(x => x.Ativo == ativo.Value);
        var itens = await consulta.OrderBy(x => x.Nome).Select(x => ParaDto(x)).ToListAsync(cancel);
        return Results.Ok(itens);
    }

    private static async Task<IResult> Obter(Guid id, TmsDbContext db, CancellationToken cancel)
    {
        var item = await db.Transportadoras.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancel);
        return item is null ? Results.NotFound() : Results.Ok(ParaDto(item));
    }

    private static async Task<IResult> Criar(Entrada entrada, UsuarioDaRequisicao sessao,
        TmsDbContext db, CancellationToken cancel)
    {
        var erros = Validar(entrada, out var tipo, out var documento);
        if (erros.Count > 0) return Results.ValidationProblem(erros);
        if (await db.Transportadoras.AnyAsync(x => x.Documento == documento, cancel))
            return ConflitoDocumento();

        var item = new Transportadora(sessao.Usuario.EmpresaId, tipo, entrada.Nome!.Trim(), documento,
            entrada.Telefone, entrada.Email);
        db.Transportadoras.Add(item);
        try { await db.SaveChangesAsync(cancel); }
        catch (DbUpdateException ex) when (EhDocumentoDuplicado(ex)) { return ConflitoDocumento(); }
        return Results.Created($"/api/transportadoras/{item.Id}", ParaDto(item));
    }

    private static async Task<IResult> Atualizar(Guid id, Entrada entrada, TmsDbContext db, CancellationToken cancel)
    {
        var item = await db.Transportadoras.SingleOrDefaultAsync(x => x.Id == id, cancel);
        if (item is null) return Results.NotFound();
        var erros = Validar(entrada, out var tipo, out var documento);
        if (erros.Count > 0) return Results.ValidationProblem(erros);
        if (await db.Transportadoras.AnyAsync(x => x.Id != id && x.Documento == documento, cancel))
            return ConflitoDocumento();

        item.Atualizar(tipo, entrada.Nome!.Trim(), documento, entrada.Telefone, entrada.Email);
        try { await db.SaveChangesAsync(cancel); }
        catch (DbUpdateException ex) when (EhDocumentoDuplicado(ex)) { return ConflitoDocumento(); }
        return Results.Ok(ParaDto(item));
    }

    private static async Task<IResult> AlterarAtivo(Guid id, AtivoEntrada entrada,
        TmsDbContext db, CancellationToken cancel)
    {
        if (entrada.Ativo is null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["ativo"] = ["Informe a situação do cadastro."] });
        var item = await db.Transportadoras.SingleOrDefaultAsync(x => x.Id == id, cancel);
        if (item is null) return Results.NotFound();
        if (entrada.Ativo.Value) item.Ativar(); else item.Inativar();
        await db.SaveChangesAsync(cancel);
        return Results.Ok(ParaDto(item));
    }

    private static Dictionary<string, string[]> Validar(Entrada entrada, out TipoParceiro tipo, out string documento)
    {
        var erros = new Dictionary<string, string[]>();
        tipo = default;
        documento = NormalizarDocumento(entrada.Documento, out var documentoValido);
        bool tipoValido = Enum.GetNames<TipoParceiro>().Contains(entrada.Tipo, StringComparer.OrdinalIgnoreCase);
        if (!tipoValido) erros["tipo"] = ["Selecione transportadora ou motorista autônomo."];
        else Enum.TryParse(entrada.Tipo, true, out tipo);
        if (string.IsNullOrWhiteSpace(entrada.Nome)) erros["nome"] = ["Informe o nome."];
        else if (entrada.Nome.Trim().Length > 150) erros["nome"] = ["Use até 150 caracteres."];
        if (!documentoValido || !tipoValido || !(tipo == TipoParceiro.AUTONOMO
                ? Validacao.Cpf(documento) : Validacao.Cnpj(documento)))
            erros["documento"] = [tipo == TipoParceiro.AUTONOMO ? "Informe um CPF válido com 11 dígitos." : "Informe um CNPJ válido com 14 dígitos."];
        if (!string.IsNullOrWhiteSpace(entrada.Telefone) && entrada.Telefone.Trim().Length > 30)
            erros["telefone"] = ["Use até 30 caracteres."];
        if (!string.IsNullOrWhiteSpace(entrada.Email) && !new EmailAddressAttribute().IsValid(entrada.Email.Trim()))
            erros["email"] = ["Informe um e-mail válido."];
        return erros;
    }

    private static string NormalizarDocumento(string? valor, out bool permitido)
    {
        permitido = !string.IsNullOrWhiteSpace(valor);
        if (!permitido) return string.Empty;
        var digitos = new System.Text.StringBuilder();
        foreach (var caractere in valor!.Trim())
        {
            if (char.IsAsciiDigit(caractere)) digitos.Append(caractere);
            else if (caractere is '.' or '/' or '-' || char.IsWhiteSpace(caractere)) continue;
            else permitido = false;
        }
        return digitos.ToString();
    }

    private static bool EhDocumentoDuplicado(DbUpdateException ex) =>
        ex.GetBaseException() is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_transportadoras_empresa_id_documento"
        };

    private static IResult ConflitoDocumento() =>
        Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Já existe um cadastro com este documento.");

    private static TransportadoraDto ParaDto(Transportadora x) =>
        new(x.Id, x.EmpresaId, x.Tipo.ToString(), x.Nome, x.Documento, x.Telefone, x.Email, x.Ativo, x.CriadoEm);

    private sealed record Entrada(string? Tipo, string? Nome, string? Documento, string? Telefone, string? Email);
    private sealed record AtivoEntrada(bool? Ativo);
    private sealed record TransportadoraDto(Guid Id, Guid EmpresaId, string Tipo, string Nome, string Documento,
        string? Telefone, string? Email, bool Ativo, DateTimeOffset CriadoEm);
}
