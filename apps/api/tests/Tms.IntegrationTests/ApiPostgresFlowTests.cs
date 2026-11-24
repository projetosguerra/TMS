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
using Npgsql;
using Tms.Domain;
using Tms.Infrastructure;
using Xunit;

namespace Tms.IntegrationTests;

public sealed class PostgresApiFactAttribute : FactAttribute
{
    public PostgresApiFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TmsTest__PostgresApi")))
            Skip = "Configure TmsTest__PostgresApi para um banco local descartável PostgreSQL.";
    }
}

public sealed class ApiPostgresFlowTests
{
    private const string Issuer = "https://api-postgres-test.invalid/auth/v1";
    private static readonly Guid Sub = Guid.Parse("00000000-0000-4000-8000-000000000111");

    [PostgresApiFact]
    public async Task FluxoCompletoDaApiPersisteNoPostgresLocalEIsolaAEmpresa()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TmsTest__PostgresApi"));
        if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") || connection.Database != "tms_demo")
            throw new InvalidOperationException("O teste exige exclusivamente o banco local tms_demo em localhost.");

        string schema = "http_flow_" + Guid.NewGuid().ToString("N");
        await using (var createConnection = new NpgsqlConnection(connection.ConnectionString))
        {
            await createConnection.OpenAsync();
            await using var createSchema = new NpgsqlCommand($"CREATE SCHEMA {schema}", createConnection);
            await createSchema.ExecuteNonQueryAsync();
        }
        connection.SearchPath = schema;
        string localConnection = connection.ConnectionString;
        var options = new DbContextOptionsBuilder<TmsDbContext>()
            .UseNpgsql(localConnection, pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", schema)).Options;
        await using (var setup = new TmsDbContext(options))
        {
            await setup.Database.MigrateAsync();
            var empresa = new Empresa("Empresa de Teste Local", "99000001000101");
            var usuario = new Usuario(Sub, empresa.Id, "Usuário de teste", "usuario@local.invalid", Perfil.ADMIN);
            var carrier = new Transportadora(empresa.Id, TipoParceiro.TRANSPORTADORA,
                "Transportadora Fictícia Local", "99000003000192");
            var autonomo = new Transportadora(empresa.Id, TipoParceiro.AUTONOMO,
                "Motorista Autônomo Fictício", "90000000175");
            setup.AddRange(empresa, usuario, carrier, autonomo,
                new TabelaFrete(carrier, "SP", "RJ", 45m, 1.25m, 3));
            await setup.SaveChangesAsync();
        }

        using var rsa = RSA.Create(2048);
        using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Testing");
            host.UseSetting("Supabase:Url", "https://api-postgres-test.invalid");
            host.UseSetting("ConnectionStrings:Default", localConnection);
            host.ConfigureServices(services => services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme, jwt =>
                {
                    var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                    configuration.SigningKeys.Add(new RsaSecurityKey(rsa));
                    jwt.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                }));
        });
        using var client = app.CreateClient();
        string token = CreateToken(rsa);

        using var partners = await Send(client, HttpMethod.Get, "/api/transportadoras", token);
        partners.EnsureSuccessStatusCode();
        var carrierList = await partners.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, carrierList.GetArrayLength());
        Guid carrierId = carrierList.EnumerateArray().Single(x => x.GetProperty("tipo").GetString() == "TRANSPORTADORA").GetProperty("id").GetGuid();

        using var rates = await Send(client, HttpMethod.Get, $"/api/transportadoras/{carrierId}/tabelas", token);
        rates.EnsureSuccessStatusCode();
        Assert.Single((await rates.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());

        using var created = await Send(client, HttpMethod.Post, "/api/fretes?empresa_id=00000000-0000-4000-8000-000000000222", token, new
        {
            origemCep = "01000000", origemCidade = "Cidade A", origemUf = "SP",
            destinoCep = "20000000", destinoCidade = "Cidade B", destinoUf = "RJ",
            destinatarioNome = "Destinatário de teste", pesoKg = 20m, volumeM3 = 0m,
            valorMercadoria = 1000m, observacao = (string?)null, empresaId = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdJson = await created.Content.ReadFromJsonAsync<JsonElement>();
        Guid freightId = createdJson.GetProperty("id").GetGuid();

        using var simulate = await Send(client, HttpMethod.Post, $"/api/fretes/{freightId}/cotacao/simular", token);
        simulate.EnsureSuccessStatusCode();
        Guid proposalId = (await simulate.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()
            .Single().GetProperty("id").GetGuid();

        using var select = await Send(client, HttpMethod.Post,
            $"/api/fretes/{freightId}/propostas/{proposalId}/selecionar", token);
        select.EnsureSuccessStatusCode();
        foreach (string status in new[] { "COLETADO", "EM_TRANSITO", "ENTREGUE" })
        {
            using var change = await Send(client, HttpMethod.Post, $"/api/fretes/{freightId}/status", token,
                new { novoStatus = status });
            change.EnsureSuccessStatusCode();
        }

        using var fiscal = await Send(client, HttpMethod.Post, $"/api/fretes/{freightId}/documentos", token,
            new { tipo = "NFE", numero = "NF-LOCAL-1", chaveAcesso = new string('1', 44), valor = 1000m });
        Assert.Equal(HttpStatusCode.Created, fiscal.StatusCode);

        using var detail = await Send(client, HttpMethod.Get, $"/api/fretes/{freightId}", token);
        detail.EnsureSuccessStatusCode();
        var detailJson = await detail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ENTREGUE", detailJson.GetProperty("status").GetString());
        Assert.Equal(6, detailJson.GetProperty("historico").GetArrayLength());
        Assert.Single(detailJson.GetProperty("documentos").EnumerateArray());

        string today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        using var dashboard = await Send(client, HttpMethod.Get, $"/api/dashboard?de={today}&ate={today}", token);
        dashboard.EnsureSuccessStatusCode();
        var metrics = await dashboard.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, metrics.GetProperty("entreguesNoPeriodo").GetInt32());
        Assert.Equal(0, metrics.GetProperty("fretesAtivos").GetInt32());
        Assert.Single(metrics.GetProperty("rankingTransportadoras").EnumerateArray());

        using var otherCompany = await Send(client, HttpMethod.Get,
            "/api/fretes?empresa_id=00000000-0000-4000-8000-000000000222", token);
        otherCompany.EnsureSuccessStatusCode();
        Assert.Equal(1, (await otherCompany.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetInt32());
    }

    private static string CreateToken(RSA rsa)
    {
        var jwt = new JwtSecurityToken(Issuer, "authenticated",
            [new Claim("sub", Sub.ToString()), new Claim("role", "authenticated")],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(
                new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path,
        string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return client.SendAsync(request);
    }
}
