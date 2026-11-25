using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.RegularExpressions;
using Tms.Domain;

namespace Tms.Infrastructure;

public sealed class TmsDbContext(DbContextOptions<TmsDbContext> options) : DbContext(options)
{
    private Guid? _empresaId;
    public void UsarEmpresa(Usuario autenticado)
    {
        if (!autenticado.Ativo || (_empresaId.HasValue && _empresaId != autenticado.EmpresaId))
            throw new InvalidOperationException("Contexto de empresa inválido.");
        _empresaId = autenticado.EmpresaId;
    }

    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Transportadora> Transportadoras => Set<Transportadora>();
    public DbSet<TabelaFrete> TabelasFrete => Set<TabelaFrete>();
    public DbSet<Frete> Fretes => Set<Frete>();
    public DbSet<Proposta> Propostas => Set<Proposta>();
    public DbSet<HistoricoStatus> Historico => Set<HistoricoStatus>();
    public DbSet<DocumentoFiscal> Documentos => Set<DocumentoFiscal>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var empresa = Raiz<Empresa>(model, "empresas");
        empresa.HasQueryFilter(x => x.Id == _empresaId);
        empresa.Property(x => x.Cnpj).HasMaxLength(14);
        empresa.HasIndex(x => x.Cnpj).IsUnique();
        Check(empresa, "empresas", "cnpj", "cnpj ~ '^[0-9]{14}$'");

        var usuario = Negocio<Usuario>(model, "usuarios");
        usuario.Property(x => x.Id).HasDefaultValueSql(null).ValueGeneratedNever();
        usuario.HasIndex(x => x.Email).IsUnique();
        EnumCheck<Usuario, Perfil>(usuario, "usuarios", "perfil");

        var parceiro = Negocio<Transportadora>(model, "transportadoras");
        parceiro.Property(x => x.Documento).HasMaxLength(14);
        parceiro.HasIndex(x => new { x.EmpresaId, x.Documento }).IsUnique();
        EnumCheck<Transportadora, TipoParceiro>(parceiro, "transportadoras", "tipo");
        Check(parceiro, "transportadoras", "documento", "(tipo = 'AUTONOMO' AND documento ~ '^[0-9]{11}$') OR (tipo = 'TRANSPORTADORA' AND documento ~ '^[0-9]{14}$')");

        var tabela = Negocio<TabelaFrete>(model, "tabelas_frete");
        Vincular<Transportadora, TabelaFrete>(tabela, "TransportadoraId");
        tabela.Property(x => x.UfOrigem).HasColumnType("char(2)");
        tabela.Property(x => x.UfDestino).HasColumnType("char(2)");
        tabela.Property(x => x.ValorMinimo).HasPrecision(12, 2);
        tabela.Property(x => x.ValorPorKg).HasPrecision(12, 4);
        Check(tabela, "tabelas_frete", "tarifa", "valor_minimo > 0 AND valor_por_kg > 0 AND prazo_dias >= 1");
        tabela.HasIndex(x => new { x.EmpresaId, x.TransportadoraId, x.UfOrigem, x.UfDestino })
            .IsUnique().HasFilter("ativa");

        var frete = Negocio<Frete>(model, "fretes");
        frete.Property(x => x.Codigo).HasMaxLength(20);
        frete.HasIndex(x => x.Codigo).IsUnique();
        frete.HasIndex(x => new { x.EmpresaId, x.Status });
        frete.HasIndex(x => new { x.EmpresaId, x.CriadoEm });
        frete.Property(x => x.ValorContratado).HasPrecision(12, 2);
        frete.Property<uint>("xmin").IsRowVersion();
        EnumCheck<Frete, StatusFrete>(frete, "fretes", "status");
        Check(frete, "fretes", "carga", "peso_kg > 0 AND volume_m3 >= 0 AND valor_mercadoria > 0 AND valor_contratado > 0");
        Check(frete, "fretes", "cep", "origem_cep ~ '^[0-9]{8}$' AND destino_cep ~ '^[0-9]{8}$'");
        Check(frete, "fretes", "codigo", "codigo ~ '^FR-[0-9]{4}-[0-9]{6}$'");
        Vincular<Usuario, Frete>(frete, "CriadoPor");
        Vincular<Transportadora, Frete>(frete, "TransportadoraId");
        frete.OwnsOne(x => x.Dados, dados =>
        {
            dados.Property(x => x.PesoKg).HasPrecision(10, 2);
            dados.Property(x => x.VolumeM3).HasPrecision(10, 3).HasDefaultValue(0m);
            dados.Property(x => x.ValorMercadoria).HasPrecision(12, 2);
            dados.Property(x => x.OrigemCep).HasMaxLength(8);
            dados.Property(x => x.DestinoCep).HasMaxLength(8);
            dados.Property(x => x.OrigemUf).HasColumnType("char(2)");
            dados.Property(x => x.DestinoUf).HasColumnType("char(2)");
        });
        frete.Navigation(x => x.Dados).IsRequired();

        var proposta = Negocio<Proposta>(model, "propostas");
        proposta.Property(x => x.Valor).HasPrecision(12, 2);
        proposta.HasIndex(x => new { x.FreteId, x.TransportadoraId }).IsUnique();
        Check(proposta, "propostas", "oferta", "valor > 0 AND prazo_dias >= 1");
        EnumCheck<Proposta, OrigemProposta>(proposta, "propostas", "origem");
        Vincular<Transportadora, Proposta>(proposta, "TransportadoraId");
        frete.HasMany(x => x.Propostas).WithOne().HasForeignKey(x => new { x.EmpresaId, x.FreteId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        frete.HasOne<Proposta>().WithMany()
            .HasForeignKey(x => new { x.EmpresaId, x.Id, x.PropostaSelecionadaId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.FreteId, x.Id }).OnDelete(DeleteBehavior.Restrict);

        var historico = Negocio<HistoricoStatus>(model, "historico_status");
        frete.HasMany(x => x.Historico).WithOne().HasForeignKey(x => new { x.EmpresaId, x.FreteId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        Vincular<Usuario, HistoricoStatus>(historico, "UsuarioId");
        EnumCheck<HistoricoStatus, StatusFrete>(historico, "historico_status", "status_anterior");
        EnumCheck<HistoricoStatus, StatusFrete>(historico, "historico_status", "status_novo");

        var documento = Negocio<DocumentoFiscal>(model, "documentos_fiscais");
        Vincular<Frete, DocumentoFiscal>(documento, "FreteId");
        documento.Property(x => x.Valor).HasPrecision(12, 2);
        documento.Property(x => x.ChaveAcesso).HasColumnType("char(44)");
        EnumCheck<DocumentoFiscal, TipoDocumento>(documento, "documentos_fiscais", "tipo");
        Check(documento, "documentos_fiscais", "valor", "valor > 0");
        Check(documento, "documentos_fiscais", "chave", "(tipo <> 'NFE' OR chave_acesso IS NOT NULL) AND chave_acesso ~ '^[0-9]{44}$'");

        foreach (var entidade in model.Model.GetEntityTypes())
        foreach (var propriedade in entidade.GetProperties())
        {
            if (entidade.IsOwned() && propriedade.IsPrimaryKey()) continue;
            propriedade.SetColumnName(Regex.Replace(propriedade.Name, "(?<=[a-z0-9])(?=[A-Z])", "_").ToLowerInvariant());
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder builder) =>
        builder.Properties<Enum>().HaveConversion<string>();

    private static EntityTypeBuilder<T> Raiz<T>(ModelBuilder model, string tabela) where T : Entidade
    {
        var entidade = model.Entity<T>();
        entidade.ToTable(tabela);
        entidade.HasKey(x => x.Id);
        entidade.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()").ValueGeneratedNever();
        entidade.Property(x => x.CriadoEm).HasDefaultValueSql("now()");
        return entidade;
    }

    private EntityTypeBuilder<T> Negocio<T>(ModelBuilder model, string tabela) where T : Entidade
    {
        var entidade = Raiz<T>(model, tabela);
        entidade.HasOne<Empresa>().WithMany().HasForeignKey("EmpresaId").OnDelete(DeleteBehavior.Restrict);
        entidade.HasQueryFilter(x => EF.Property<Guid>(x, "EmpresaId") == _empresaId);
        return entidade;
    }

    private static void Vincular<TPai, TFilho>(EntityTypeBuilder<TFilho> entidade, string chave)
        where TPai : Entidade where TFilho : Entidade =>
        entidade.HasOne<TPai>().WithMany().HasForeignKey("EmpresaId", chave)
            .HasPrincipalKey("EmpresaId", "Id").OnDelete(DeleteBehavior.Restrict);

    private static void Check<T>(EntityTypeBuilder<T> entidade, string tabela, string nome, string sql)
        where T : Entidade => entidade.ToTable(t => t.HasCheckConstraint($"ck_{tabela}_{nome}", sql));

    private static void EnumCheck<T, TEnum>(EntityTypeBuilder<T> entidade, string tabela, string coluna)
        where T : Entidade where TEnum : struct, Enum =>
        Check(entidade, tabela, coluna, $"{coluna} IN ('{string.Join("','", Enum.GetNames<TEnum>())}')");
}
