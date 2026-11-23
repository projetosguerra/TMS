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
    public async Task SwaggerDescreveRotasEmDevelopment()
    {
        var json = await _client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        Assert.True(json.GetProperty("paths").TryGetProperty("/api/me", out _));
    }

    [Theory]
    [InlineData("https://web.invalid", true)]
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
