using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Tms.IntegrationTests;

public sealed class SupabaseFactAttribute : FactAttribute
{
    public SupabaseFactAttribute()
    {
        string[] nomes = ["Url", "Postgres", "TokenAlfa", "TokenBeta", "EmpresaAlfa", "EmpresaBeta"];
        if (nomes.Any(n => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TmsTestSupabase__" + n))))
            Skip = "Requer projeto de teste Supabase, dois access tokens válidos e empresas cadastradas.";
    }
}

public class SupabaseTests
{
    [SupabaseFact]
    public async Task TokensReaisResolvemEmpresasEsperadas()
    {
        static string Config(string nome) => Environment.GetEnvironmentVariable("TmsTestSupabase__" + nome)!;
        using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Testing");
            host.UseSetting("Supabase:Url", Config("Url"));
            host.UseSetting("Supabase:JwksUrl", Config("Url").TrimEnd('/') + "/auth/v1/.well-known/jwks.json");
            host.UseSetting("ConnectionStrings:Default", Config("Postgres"));
        });
        using var client = app.CreateClient();
        var alfa = Guid.Parse(Config("EmpresaAlfa"));
        var beta = Guid.Parse(Config("EmpresaBeta"));
        Assert.NotEqual(alfa, beta);
        foreach (var (nome, esperada, outra) in new[] { ("Alfa", alfa, beta), ("Beta", beta, alfa) })
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/api/me?empresa_id=" + outra);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Config("Token" + nome));
            using var res = await client.SendAsync(req);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(esperada, json.GetProperty("empresa").GetProperty("id").GetGuid());
        }
    }
}
