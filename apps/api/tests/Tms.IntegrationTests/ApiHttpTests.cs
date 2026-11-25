using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Tms.Domain;
using Tms.Infrastructure;
using Xunit;

namespace Tms.IntegrationTests;

// Chaves efêmeras exclusivas do host de teste. O pipeline JwtBearer e o filtro de usuário são os da API.
public sealed class ApiHttpTests : IDisposable
{
    private const string Emissor = "https://teste.invalid/auth/v1";
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly WebApplicationFactory<Program> _app;
    private readonly HttpClient _client;
    private readonly Usuario _alfa;
    private readonly Usuario _beta;
    private readonly Usuario _inativo;

    public ApiHttpTests()
    {
        var nomeBanco = Guid.NewGuid().ToString();
        _app = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Development");
            host.UseSetting("Supabase:Url", "https://teste.invalid");
            host.UseSetting("Cors:Origin", "https://web.invalid");
            host.ConfigureServices(services =>
            {
                services.AddScoped(_ => new TmsDbContext(new DbContextOptionsBuilder<TmsDbContext>()
                    .UseInMemoryDatabase(nomeBanco).Options));
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    var chaves = new OpenIdConnectConfiguration { Issuer = Emissor };
                    chaves.SigningKeys.Add(new RsaSecurityKey(_rsa));
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(chaves);
                });
            });
        });
        _client = _app.CreateClient();
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TmsDbContext>();
        var alfa = new Empresa("Fictícia Alfa", "99000001000101");
        var beta = new Empresa("Fictícia Beta", "99000002000148");
        _alfa = Novo(alfa, "alfa");
        _beta = Novo(beta, "beta");
        _inativo = Novo(alfa, "inativo");
        db.AddRange(alfa, beta, _alfa, _beta, _inativo);
        db.Entry(_inativo).Property(u => u.Ativo).CurrentValue = false;
        db.SaveChanges();
    }

    private static Usuario Novo(Empresa empresa, string nome) =>
        new(Guid.NewGuid(), empresa.Id, nome, nome + "@example.invalid", Perfil.OPERADOR);

    private string Token(Guid sub, string audiencia = "authenticated", string issuer = Emissor, RSA? chave = null)
    {
        var jwt = new JwtSecurityToken(issuer, audiencia, [new Claim("sub", sub.ToString()), new Claim("role", "authenticated")],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(
                new RsaSecurityKey(chave ?? _rsa), SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private Task<HttpResponseMessage> Me(string? token, string query = "")
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/me" + query);
        if (token != null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(req);
    }

    private Task<HttpResponseMessage> Transportadoras(HttpMethod metodo, string caminho,
        object? corpo = null, Usuario? usuario = null)
    {
        var req = new HttpRequestMessage(metodo, caminho);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token((usuario ?? _alfa).Id));
        if (corpo is not null) req.Content = JsonContent.Create(corpo);
        return _client.SendAsync(req);
    }

    private Task<HttpResponseMessage> Api(HttpMethod metodo, string caminho, object? corpo = null, Usuario? usuario = null)
    {
        var req = new HttpRequestMessage(metodo, caminho);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token((usuario ?? _alfa).Id));
        if (corpo is not null) req.Content = JsonContent.Create(corpo);
        return _client.SendAsync(req);
    }

    [Fact]
    public async Task HealthPublicoMinimo()
    {
        var json = await _client.GetFromJsonAsync<JsonElement>("/api/health");
        Assert.Equal("ok", Assert.Single(json.EnumerateObject()).Value.GetString());
    }

    [Theory]
    [InlineData("ausente")]
    [InlineData("malformado")]
    [InlineData("assinatura")]
    [InlineData("audiencia")]
    [InlineData("emissor")]
    public async Task CredencialRecusadaRetornaProblema401(string caso)
    {
        using var outraChave = RSA.Create(2048);
        var token = caso switch
        {
            "ausente" => null,
            "assinatura" => Token(_alfa.Id, chave: outraChave),
            "audiencia" => Token(_alfa.Id, audiencia: "outra"),
            "emissor" => Token(_alfa.Id, issuer: "https://outro.invalid"),
            _ => "invalido"
        };
        using var resposta = await Me(token);
        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SemCadastroAtivoRetorna403(bool cadastrado)
    {
        using var resposta = await Me(Token(cadastrado ? _inativo.Id : Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task MeUsaEmpresaDoCadastroEmRequisicoesIndependentes()
    {
        foreach (var (usuario, outra) in new[] { (_alfa, _beta), (_beta, _alfa) })
        {
            using var resposta = await Me(Token(usuario.Id), "?empresa_id=" + outra.EmpresaId);
            resposta.EnsureSuccessStatusCode();
            var json = await resposta.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(usuario.Id, json.GetProperty("id").GetGuid());
            Assert.Equal(usuario.EmpresaId, json.GetProperty("empresa").GetProperty("id").GetGuid());
        }
    }

    [Fact]
    public async Task ListaSomenteTransportadorasDaEmpresaAutenticada()
    {
        var alfa = new Transportadora(_alfa.EmpresaId, TipoParceiro.TRANSPORTADORA,
            "Fictícia Alfa Logística", "99000003000192");
        var beta = new Transportadora(_beta.EmpresaId, TipoParceiro.TRANSPORTADORA,
            "Fictícia Beta Logística", "99000004000137");
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TmsDbContext>();
            db.AddRange(alfa, beta);
            await db.SaveChangesAsync();
        }

        using var resposta = await Transportadoras(HttpMethod.Get,
            "/api/transportadoras?ativo=true&empresa_id=" + _beta.EmpresaId);
        resposta.EnsureSuccessStatusCode();
        var json = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        var unico = Assert.Single(json.EnumerateArray());
        Assert.Equal(alfa.Id, unico.GetProperty("id").GetGuid());
        Assert.Equal(_alfa.EmpresaId, unico.GetProperty("empresaId").GetGuid());
    }

    [Fact]
    public async Task CriacaoUsaEmpresaAutenticadaEGuardaDocumentoSomenteComDigitos()
    {
        using var resposta = await Transportadoras(HttpMethod.Post, "/api/transportadoras", new
        {
            tipo = "TRANSPORTADORA", nome = "Parceiro de Demonstração", documento = "99.000.003/0001-92",
            empresa_id = _beta.EmpresaId
        });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var json = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(_alfa.EmpresaId, json.GetProperty("empresaId").GetGuid());
        Assert.Equal("99000003000192", json.GetProperty("documento").GetString());
        Assert.Equal("Parceiro de Demonstração", json.GetProperty("nome").GetString());
    }

    [Fact]
    public async Task RecusaDocumentoInvalidoComErroDeCampo()
    {
        using var resposta = await Transportadoras(HttpMethod.Post, "/api/transportadoras", new
        {
            tipo = "AUTONOMO", nome = "Motorista de teste", documento = "00000000000"
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        var json = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("errors").TryGetProperty("documento", out _));
    }

    [Fact]
    public async Task DocumentoDuplicadoRetorna409()
    {
        var existente = new Transportadora(_alfa.EmpresaId, TipoParceiro.TRANSPORTADORA,
            "Parceiro já cadastrado", "99000003000192");
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TmsDbContext>();
            db.Add(existente);
            await db.SaveChangesAsync();
        }

        using var resposta = await Transportadoras(HttpMethod.Post, "/api/transportadoras", new
        {
            tipo = "TRANSPORTADORA", nome = "Duplicado", documento = "99000003000192"
        });
        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Fact]
    public async Task AtualizaEDesativaCadastroSemExcluir()
    {
        var existente = new Transportadora(_alfa.EmpresaId, TipoParceiro.TRANSPORTADORA,
            "Parceiro antigo", "99000003000192");
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TmsDbContext>();
            db.Add(existente);
            await db.SaveChangesAsync();
        }

        using var atualizacao = await Transportadoras(HttpMethod.Put,
            $"/api/transportadoras/{existente.Id}", new
            {
                tipo = "TRANSPORTADORA", nome = "Parceiro atualizado", documento = "99000004000137"
            });
        Assert.Equal(HttpStatusCode.OK, atualizacao.StatusCode);

        using var desativacao = await Transportadoras(HttpMethod.Patch,
            $"/api/transportadoras/{existente.Id}/ativo", new { ativo = false });
        Assert.Equal(HttpStatusCode.OK, desativacao.StatusCode);
        var json = await desativacao.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(json.GetProperty("ativo").GetBoolean());

        using var leitura = await Transportadoras(HttpMethod.Get, $"/api/transportadoras/{existente.Id}");
        Assert.Equal(HttpStatusCode.OK, leitura.StatusCode);
        var lido = await leitura.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Parceiro atualizado", lido.GetProperty("nome").GetString());
        Assert.False(lido.GetProperty("ativo").GetBoolean());
    }

    [Fact]
    public async Task NaoAtualizaCadastroDeOutraEmpresa()
    {
        var beta = new Transportadora(_beta.EmpresaId, TipoParceiro.TRANSPORTADORA,
            "Parceiro Beta", "99000003000192");
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TmsDbContext>();
            db.Add(beta);
            await db.SaveChangesAsync();
        }

        using var resposta = await Transportadoras(HttpMethod.Put, $"/api/transportadoras/{beta.Id}", new
        {
            tipo = "TRANSPORTADORA", nome = "Tentativa", documento = "99000004000137"
        });
        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task TabelasDeFreteSaoIsoladasEImpedemDuasRotasAtivasIguais()
    {
        var parceiro = new Transportadora(_alfa.EmpresaId, TipoParceiro.TRANSPORTADORA,
            "Parceiro de teste", "99000003000192");
        var beta = new Transportadora(_beta.EmpresaId, TipoParceiro.TRANSPORTADORA,
            "Parceiro Beta", "99000004000137");
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TmsDbContext>();
            db.AddRange(parceiro, beta);
            await db.SaveChangesAsync();
        }

        var entrada = new { ufOrigem = "sp", ufDestino = "rj", valorMinimo = 50m, valorPorKg = 1.25m, prazoDias = 3, ativa = true };
        using var criada = await Api(HttpMethod.Post, $"/api/transportadoras/{parceiro.Id}/tabelas", entrada);
        Assert.Equal(HttpStatusCode.Created, criada.StatusCode);
        var jsonCriada = await criada.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("SP", jsonCriada.GetProperty("ufOrigem").GetString());
        Guid tabelaId = jsonCriada.GetProperty("id").GetGuid();

        using var duplicada = await Api(HttpMethod.Post, $"/api/transportadoras/{parceiro.Id}/tabelas", entrada);
        Assert.Equal(HttpStatusCode.Conflict, duplicada.StatusCode);
        using var atualizada = await Api(HttpMethod.Put, $"/api/tabelas-frete/{tabelaId}", new
        { ufOrigem = "MG", ufDestino = "PR", valorMinimo = 80m, valorPorKg = 2m, prazoDias = 4, ativa = false });
        Assert.Equal(HttpStatusCode.OK, atualizada.StatusCode);
        using var leitura = await Api(HttpMethod.Get, $"/api/transportadoras/{parceiro.Id}/tabelas?empresa_id={_beta.EmpresaId}");
        leitura.EnsureSuccessStatusCode();
        var tabelaLida = await leitura.Content.ReadFromJsonAsync<JsonElement>();
        var linha = Assert.Single(tabelaLida.EnumerateArray());
        Assert.Equal("MG", linha.GetProperty("ufOrigem").GetString());
        Assert.False(linha.GetProperty("ativa").GetBoolean());
        using var leituraOutraEmpresa = await Api(HttpMethod.Get, $"/api/transportadoras/{beta.Id}/tabelas?empresa_id={_beta.EmpresaId}");
        Assert.Equal(HttpStatusCode.NotFound, leituraOutraEmpresa.StatusCode);
        using var atualizacaoOutraEmpresa = await Api(HttpMethod.Put, $"/api/tabelas-frete/{tabelaId}",
            new { ufOrigem = "SP", ufDestino = "PR", valorMinimo = 80m, valorPorKg = 2m, prazoDias = 4, ativa = true }, _beta);
        Assert.Equal(HttpStatusCode.NotFound, atualizacaoOutraEmpresa.StatusCode);
    }

    [Fact]
    public async Task FluxoFreteCotacaoSelecaoHistoricoDocumentoEDashboardUsaEmpresaAutenticada()
    {
        var parceiro = new Transportadora(_alfa.EmpresaId, TipoParceiro.TRANSPORTADORA,
            "Parceiro de teste", "99000003000192");
        var outroParceiro = new Transportadora(_beta.EmpresaId, TipoParceiro.TRANSPORTADORA,
            "Parceiro Beta", "99000004000137");
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TmsDbContext>();
            db.AddRange(parceiro, outroParceiro);
            db.Add(new TabelaFrete(parceiro, "SP", "RJ", 50m, 1.25m, 3));
            db.Add(new TabelaFrete(outroParceiro, "SP", "RJ", 10m, 0.5m, 1));
            await db.SaveChangesAsync();
        }

        using var criada = await Api(HttpMethod.Post, "/api/fretes", new
        {
            origemCep = "01000000", origemCidade = "Cidade A", origemUf = "SP",
            destinoCep = "20000000", destinoCidade = "Cidade B", destinoUf = "RJ",
            destinatarioNome = "Destinatario de teste", pesoKg = 20m, volumeM3 = 0m,
            valorMercadoria = 1000m, observacao = (string?)null, empresaId = _beta.EmpresaId
        });
        Assert.Equal(HttpStatusCode.Created, criada.StatusCode);
        var freteCriado = await criada.Content.ReadFromJsonAsync<JsonElement>();
        Guid id = freteCriado.GetProperty("id").GetGuid();

        using var lista = await Api(HttpMethod.Get, "/api/fretes?empresa_id=" + _beta.EmpresaId);
        lista.EnsureSuccessStatusCode();
        var pagina = await lista.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, pagina.GetProperty("total").GetInt32());

        using var transicaoInvalida = await Api(HttpMethod.Post, $"/api/fretes/{id}/status", new { novoStatus = "ENTREGUE" });
        Assert.Equal(HttpStatusCode.Conflict, transicaoInvalida.StatusCode);

        using var manual = await Api(HttpMethod.Post, $"/api/fretes/{id}/propostas", new
        {
            transportadoraId = parceiro.Id, valor = 120m, prazoDias = 5, observacao = "Proposta ficticia"
        });
        Assert.Equal(HttpStatusCode.OK, manual.StatusCode);
        var propostaManual = await manual.Content.ReadFromJsonAsync<JsonElement>();
        Guid propostaManualId = propostaManual.GetProperty("id").GetGuid();
        using var removida = await Api(HttpMethod.Delete, $"/api/fretes/{id}/propostas/{propostaManualId}");
        Assert.Equal(HttpStatusCode.NoContent, removida.StatusCode);

        using var cotacao = await Api(HttpMethod.Post, $"/api/fretes/{id}/cotacao/simular");
        Assert.Equal(HttpStatusCode.OK, cotacao.StatusCode);
        var ofertas = await cotacao.Content.ReadFromJsonAsync<JsonElement>();
        var oferta = Assert.Single(ofertas.EnumerateArray());
        Assert.Equal(parceiro.Id, oferta.GetProperty("transportadoraId").GetGuid());
        Guid propostaId = oferta.GetProperty("id").GetGuid();

        using var selecao = await Api(HttpMethod.Post, $"/api/fretes/{id}/propostas/{propostaId}/selecionar");
        Assert.Equal(HttpStatusCode.OK, selecao.StatusCode);
        foreach (string status in new[] { "COLETADO", "ENTREGUE" })
        {
            using var alteracao = await Api(HttpMethod.Post, $"/api/fretes/{id}/status", new { novoStatus = status });
            Assert.Equal(HttpStatusCode.OK, alteracao.StatusCode);
        }

        using var documento = await Api(HttpMethod.Post, $"/api/fretes/{id}/documentos", new
        {
            tipo = "NFE", numero = "NF-100", chaveAcesso = new string('1', 44), valor = 1000m
        });
        Assert.Equal(HttpStatusCode.Created, documento.StatusCode);

        using var detalhe = await Api(HttpMethod.Get, $"/api/fretes/{id}");
        detalhe.EnsureSuccessStatusCode();
        var detalheJson = await detalhe.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ENTREGUE", detalheJson.GetProperty("status").GetString());
        Assert.Equal(5, detalheJson.GetProperty("historico").GetArrayLength());
        Assert.Single(detalheJson.GetProperty("documentos").EnumerateArray());

        string hoje = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        using var dashboard = await Api(HttpMethod.Get, $"/api/dashboard?de={hoje}&ate={hoje}");
        dashboard.EnsureSuccessStatusCode();
        var painel = await dashboard.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, painel.GetProperty("entreguesNoPeriodo").GetInt32());
        Assert.Single(painel.GetProperty("rankingTransportadoras").EnumerateArray());
    }

    [Fact]
    public async Task SwaggerDescreveRotasEmDevelopment()
    {
        var json = await _client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        Assert.True(json.GetProperty("paths").TryGetProperty("/api/me", out _));
    }

    [Theory]
    [InlineData("http://localhost:5173", true)]
    [InlineData("https://web.invalid", false)]
    [InlineData("https://outra.invalid", false)]
    public async Task CorsPermiteSomenteOrigemConfigurada(string origem, bool permitido)
    {
        using var req = new HttpRequestMessage(HttpMethod.Options, "/api/me");
        req.Headers.Add("Origin", origem);
        req.Headers.Add("Access-Control-Request-Method", "GET");
        using var res = await _client.SendAsync(req);
        Assert.Equal(permitido, res.Headers.Contains("Access-Control-Allow-Origin"));
    }

    public void Dispose()
    {
        _client.Dispose();
        _app.Dispose();
        _rsa.Dispose();
    }
}
