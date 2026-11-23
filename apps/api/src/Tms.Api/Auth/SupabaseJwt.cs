using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Tms.Api.Auth;

internal static class SupabaseJwt
{
    internal static void Configurar(JwtBearerOptions options, IConfiguration config)
    {
        options.MapInboundClaims = false;
        options.IncludeErrorDetails = false;
        string? url = config["Supabase:Url"]?.TrimEnd('/');
        string? issuer = url is null ? null : $"{url}/auth/v1";
        options.TokenValidationParameters = new()
        {
            ValidIssuer = issuer,
            ValidAudience = "authenticated",
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.EcdsaSha256],
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events.OnTokenValidated = context =>
        {
            if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var sub) || sub == Guid.Empty ||
                context.Principal?.FindFirst("role")?.Value != "authenticated")
                context.Fail("Token de usuário inválido.");
            return Task.CompletedTask;
        };
        if (issuer is null) return; // Sem emissor e chaves, nenhum token pode ser aceito.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var projeto) || projeto.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Supabase__Url deve ser HTTPS.");
        var endereco = config["Supabase:JwksUrl"] ?? $"{issuer}/.well-known/jwks.json";
        options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            endereco, new ChavesDoProjeto(issuer), new HttpDocumentRetriever { RequireHttps = true })
        {
            AutomaticRefreshInterval = TimeSpan.FromMinutes(10)
        };
    }

    private sealed class ChavesDoProjeto(string issuer) : IConfigurationRetriever<OpenIdConnectConfiguration>
    {
        public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(string address,
            IDocumentRetriever retriever, CancellationToken cancel)
        {
            var jwks = new JsonWebKeySet(await retriever.GetDocumentAsync(address, cancel));
            var config = new OpenIdConnectConfiguration { Issuer = issuer };
            foreach (var key in jwks.GetSigningKeys()) config.SigningKeys.Add(key);
            return config;
        }
    }
}
