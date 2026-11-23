using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "empresas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    nome = table.Column<string>(type: "text", nullable: false),
                    cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_empresas", x => x.id);
                    table.CheckConstraint("ck_empresas_cnpj", "cnpj ~ '^[0-9]{14}$'");
                });

            migrationBuilder.CreateTable(
                name: "transportadoras",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false),
                    documento = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    telefone = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transportadoras", x => x.id);
                    table.UniqueConstraint("AK_transportadoras_empresa_id_id", x => new { x.empresa_id, x.id });
                    table.CheckConstraint("ck_transportadoras_documento", "(tipo = 'AUTONOMO' AND documento ~ '^[0-9]{11}$') OR (tipo = 'TRANSPORTADORA' AND documento ~ '^[0-9]{14}$')");
                    table.CheckConstraint("ck_transportadoras_tipo", "tipo IN ('TRANSPORTADORA','AUTONOMO')");
                    table.ForeignKey(
                        name: "FK_transportadoras_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    perfil = table.Column<string>(type: "text", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.id);
                    table.UniqueConstraint("AK_usuarios_empresa_id_id", x => new { x.empresa_id, x.id });
                    table.CheckConstraint("ck_usuarios_perfil", "perfil IN ('ADMIN','OPERADOR')");
                    table.ForeignKey(
                        name: "FK_usuarios_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tabelas_frete",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transportadora_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uf_origem = table.Column<string>(type: "char(2)", nullable: false),
                    uf_destino = table.Column<string>(type: "char(2)", nullable: false),
                    valor_minimo = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    valor_por_kg = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    prazo_dias = table.Column<int>(type: "integer", nullable: false),
                    ativa = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tabelas_frete", x => x.id);
                    table.CheckConstraint("ck_tabelas_frete_tarifa", "valor_minimo > 0 AND valor_por_kg > 0 AND prazo_dias >= 1");
                    table.ForeignKey(
                        name: "FK_tabelas_frete_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tabelas_frete_transportadoras_empresa_id_transportadora_id",
                        columns: x => new { x.empresa_id, x.transportadora_id },
                        principalTable: "transportadoras",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documentos_fiscais",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    frete_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    numero = table.Column<string>(type: "text", nullable: false),
                    chave_acesso = table.Column<string>(type: "char(44)", nullable: true),
                    valor = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documentos_fiscais", x => x.id);
                    table.CheckConstraint("ck_documentos_fiscais_chave", "(tipo <> 'NFE' OR chave_acesso IS NOT NULL) AND chave_acesso ~ '^[0-9]{44}$'");
                    table.CheckConstraint("ck_documentos_fiscais_tipo", "tipo IN ('NFE','CTE','OUTRO')");
                    table.CheckConstraint("ck_documentos_fiscais_valor", "valor > 0");
                    table.ForeignKey(
                        name: "FK_documentos_fiscais_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fretes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    origem_cep = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    origem_cidade = table.Column<string>(type: "text", nullable: false),
                    origem_uf = table.Column<string>(type: "char(2)", nullable: false),
                    destino_cep = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    destino_cidade = table.Column<string>(type: "text", nullable: false),
                    destino_uf = table.Column<string>(type: "char(2)", nullable: false),
                    destinatario_nome = table.Column<string>(type: "text", nullable: false),
                    peso_kg = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    volume_m3 = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false, defaultValue: 0m),
                    valor_mercadoria = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    observacao = table.Column<string>(type: "text", nullable: true),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: false),
                    transportadora_id = table.Column<Guid>(type: "uuid", nullable: true),
                    proposta_selecionada_id = table.Column<Guid>(type: "uuid", nullable: true),
                    valor_contratado = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    prazo_previsto = table.Column<DateOnly>(type: "date", nullable: true),
                    entregue_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fretes", x => x.id);
                    table.UniqueConstraint("AK_fretes_empresa_id_id", x => new { x.empresa_id, x.id });
                    table.CheckConstraint("ck_fretes_carga", "peso_kg > 0 AND volume_m3 >= 0 AND valor_mercadoria > 0 AND valor_contratado > 0");
                    table.CheckConstraint("ck_fretes_cep", "origem_cep ~ '^[0-9]{8}$' AND destino_cep ~ '^[0-9]{8}$'");
                    table.CheckConstraint("ck_fretes_codigo", "codigo ~ '^FR-[0-9]{4}-[0-9]{6}$'");
                    table.CheckConstraint("ck_fretes_status", "status IN ('ABERTO','EM_COTACAO','CONTRATADO','COLETADO','EM_TRANSITO','ENTREGUE','CANCELADO')");
                    table.ForeignKey(
                        name: "FK_fretes_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fretes_transportadoras_empresa_id_transportadora_id",
                        columns: x => new { x.empresa_id, x.transportadora_id },
                        principalTable: "transportadoras",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fretes_usuarios_empresa_id_criado_por",
                        columns: x => new { x.empresa_id, x.criado_por },
                        principalTable: "usuarios",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "historico_status",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    frete_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status_anterior = table.Column<string>(type: "text", nullable: true),
                    status_novo = table.Column<string>(type: "text", nullable: false),
                    observacao = table.Column<string>(type: "text", nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ocorrido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historico_status", x => x.id);
                    table.CheckConstraint("ck_historico_status_status_anterior", "status_anterior IN ('ABERTO','EM_COTACAO','CONTRATADO','COLETADO','EM_TRANSITO','ENTREGUE','CANCELADO')");
                    table.CheckConstraint("ck_historico_status_status_novo", "status_novo IN ('ABERTO','EM_COTACAO','CONTRATADO','COLETADO','EM_TRANSITO','ENTREGUE','CANCELADO')");
                    table.ForeignKey(
                        name: "FK_historico_status_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_historico_status_fretes_empresa_id_frete_id",
                        columns: x => new { x.empresa_id, x.frete_id },
                        principalTable: "fretes",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_historico_status_usuarios_empresa_id_usuario_id",
                        columns: x => new { x.empresa_id, x.usuario_id },
                        principalTable: "usuarios",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "propostas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    frete_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transportadora_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origem = table.Column<string>(type: "text", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    prazo_dias = table.Column<int>(type: "integer", nullable: false),
                    observacao = table.Column<string>(type: "text", nullable: true),
                    selecionada = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_propostas", x => x.id);
                    table.UniqueConstraint("AK_propostas_empresa_id_frete_id_id", x => new { x.empresa_id, x.frete_id, x.id });
                    table.CheckConstraint("ck_propostas_oferta", "valor > 0 AND prazo_dias >= 1");
                    table.CheckConstraint("ck_propostas_origem", "origem IN ('TABELA','MANUAL')");
                    table.ForeignKey(
                        name: "FK_propostas_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_propostas_fretes_empresa_id_frete_id",
                        columns: x => new { x.empresa_id, x.frete_id },
                        principalTable: "fretes",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_propostas_transportadoras_empresa_id_transportadora_id",
                        columns: x => new { x.empresa_id, x.transportadora_id },
                        principalTable: "transportadoras",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            // No Supabase, o cliente web acessa estas tabelas somente através da API do TMS.
            migrationBuilder.Sql("""
                ALTER TABLE empresas ENABLE ROW LEVEL SECURITY;
                ALTER TABLE usuarios ENABLE ROW LEVEL SECURITY;
                ALTER TABLE transportadoras ENABLE ROW LEVEL SECURITY;
                ALTER TABLE tabelas_frete ENABLE ROW LEVEL SECURITY;
                ALTER TABLE fretes ENABLE ROW LEVEL SECURITY;
                ALTER TABLE propostas ENABLE ROW LEVEL SECURITY;
                ALTER TABLE historico_status ENABLE ROW LEVEL SECURITY;
                ALTER TABLE documentos_fiscais ENABLE ROW LEVEL SECURITY;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_documentos_fiscais_empresa_id_frete_id",
                table: "documentos_fiscais",
                columns: new[] { "empresa_id", "frete_id" });

            migrationBuilder.CreateIndex(
                name: "IX_empresas_cnpj",
                table: "empresas",
                column: "cnpj",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fretes_codigo",
                table: "fretes",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fretes_empresa_id_criado_em",
                table: "fretes",
                columns: new[] { "empresa_id", "criado_em" });

            migrationBuilder.CreateIndex(
                name: "IX_fretes_empresa_id_criado_por",
                table: "fretes",
                columns: new[] { "empresa_id", "criado_por" });

            migrationBuilder.CreateIndex(
                name: "IX_fretes_empresa_id_id_proposta_selecionada_id",
                table: "fretes",
                columns: new[] { "empresa_id", "id", "proposta_selecionada_id" });

            migrationBuilder.CreateIndex(
                name: "IX_fretes_empresa_id_status",
                table: "fretes",
                columns: new[] { "empresa_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_fretes_empresa_id_transportadora_id",
                table: "fretes",
                columns: new[] { "empresa_id", "transportadora_id" });

            migrationBuilder.CreateIndex(
                name: "IX_historico_status_empresa_id_frete_id",
                table: "historico_status",
                columns: new[] { "empresa_id", "frete_id" });

            migrationBuilder.CreateIndex(
                name: "IX_historico_status_empresa_id_usuario_id",
                table: "historico_status",
                columns: new[] { "empresa_id", "usuario_id" });

            migrationBuilder.CreateIndex(
                name: "IX_propostas_empresa_id_transportadora_id",
                table: "propostas",
                columns: new[] { "empresa_id", "transportadora_id" });

            migrationBuilder.CreateIndex(
                name: "IX_propostas_frete_id_transportadora_id",
                table: "propostas",
                columns: new[] { "frete_id", "transportadora_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tabelas_frete_empresa_id_transportadora_id_uf_origem_uf_des~",
                table: "tabelas_frete",
                columns: new[] { "empresa_id", "transportadora_id", "uf_origem", "uf_destino" },
                unique: true,
                filter: "ativa");

            migrationBuilder.CreateIndex(
                name: "IX_transportadoras_empresa_id_documento",
                table: "transportadoras",
                columns: new[] { "empresa_id", "documento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_email",
                table: "usuarios",
                column: "email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_documentos_fiscais_fretes_empresa_id_frete_id",
                table: "documentos_fiscais",
                columns: new[] { "empresa_id", "frete_id" },
                principalTable: "fretes",
                principalColumns: new[] { "empresa_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_fretes_propostas_empresa_id_id_proposta_selecionada_id",
                table: "fretes",
                columns: new[] { "empresa_id", "id", "proposta_selecionada_id" },
                principalTable: "propostas",
                principalColumns: new[] { "empresa_id", "frete_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_fretes_empresas_empresa_id",
                table: "fretes");

            migrationBuilder.DropForeignKey(
                name: "FK_propostas_empresas_empresa_id",
                table: "propostas");

            migrationBuilder.DropForeignKey(
                name: "FK_transportadoras_empresas_empresa_id",
                table: "transportadoras");

            migrationBuilder.DropForeignKey(
                name: "FK_usuarios_empresas_empresa_id",
                table: "usuarios");

            migrationBuilder.DropForeignKey(
                name: "FK_propostas_fretes_empresa_id_frete_id",
                table: "propostas");

            migrationBuilder.DropTable(
                name: "documentos_fiscais");

            migrationBuilder.DropTable(
                name: "historico_status");

            migrationBuilder.DropTable(
                name: "tabelas_frete");

            migrationBuilder.DropTable(
                name: "empresas");

            migrationBuilder.DropTable(
                name: "fretes");

            migrationBuilder.DropTable(
                name: "propostas");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropTable(
                name: "transportadoras");
        }
    }
}
