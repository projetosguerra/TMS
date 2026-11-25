namespace Tms.Domain;

public sealed class Empresa : Entidade
{
    public string Nome { get; private set; } = null!;
    public string Cnpj { get; private set; } = null!;

    public Empresa(string nome, string cnpj)
    {
        Validacao.Exigir(Validacao.Cnpj(cnpj), "CNPJ inválido.");
        Nome = nome;
        Cnpj = cnpj;
    }
}

public sealed class Usuario : Entidade
{
    public Guid EmpresaId { get; private set; }
    public string Nome { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public Perfil Perfil { get; private set; }
    public bool Ativo { get; private set; } = true;

    public Usuario(Guid id, Guid empresaId, string nome, string email, Perfil perfil)
    {
        Validacao.Exigir(id != Guid.Empty && empresaId != Guid.Empty && Enum.IsDefined(perfil), "Usuário inválido.");
        Id = id;
        EmpresaId = empresaId;
        Nome = nome;
        Email = email.Trim().ToLowerInvariant();
        Perfil = perfil;
    }
}

public sealed class Transportadora : Entidade
{
    public Guid EmpresaId { get; private set; }
    public TipoParceiro Tipo { get; private set; }
    public string Nome { get; private set; } = null!;
    public string Documento { get; private set; } = null!;
    public string? Telefone { get; private set; }
    public string? Email { get; private set; }
    public bool Ativo { get; private set; } = true;

    public Transportadora(Guid empresaId, TipoParceiro tipo, string nome, string documento,
        string? telefone = null, string? email = null)
    {
        Validar(empresaId, tipo, nome, documento);
        EmpresaId = empresaId;
        Tipo = tipo;
        Nome = nome.Trim();
        Documento = documento;
        Telefone = NormalizarOpcional(telefone);
        Email = NormalizarOpcional(email)?.ToLowerInvariant();
    }

    public void Atualizar(TipoParceiro tipo, string nome, string documento, string? telefone, string? email)
    {
        Validar(EmpresaId, tipo, nome, documento);
        Tipo = tipo;
        Nome = nome.Trim();
        Documento = documento;
        Telefone = NormalizarOpcional(telefone);
        Email = NormalizarOpcional(email)?.ToLowerInvariant();
    }

    public void Ativar() => Ativo = true;
    public void Inativar() => Ativo = false;

    private static void Validar(Guid empresaId, TipoParceiro tipo, string nome, string documento)
    {
        Validacao.Exigir(empresaId != Guid.Empty && Enum.IsDefined(tipo) && !string.IsNullOrWhiteSpace(nome),
            "Informe um tipo e nome válidos para a transportadora.");
        Validacao.Exigir(tipo == TipoParceiro.AUTONOMO ? Validacao.Cpf(documento) : Validacao.Cnpj(documento),
            "Documento da transportadora inválido.");
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
