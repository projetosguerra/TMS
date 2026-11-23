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
        Validacao.Exigir(Enum.IsDefined(tipo) && (tipo == TipoParceiro.AUTONOMO ?
            Validacao.Cpf(documento) : Validacao.Cnpj(documento)), "Documento da transportadora inválido.");
        EmpresaId = empresaId;
        Tipo = tipo;
        Nome = nome;
        Documento = documento;
        Telefone = telefone;
        Email = email;
    }

    public void Ativar() => Ativo = true;
    public void Inativar() => Ativo = false;
}
