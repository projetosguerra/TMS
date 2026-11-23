using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Tms.Api.Auth;
using Tms.Api.Modules.Usuarios;
using Tms.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<TmsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<UsuarioDaRequisicao>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => SupabaseJwt.Configurar(options, builder.Configuration));
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    var origem = builder.Configuration["Cors:Origin"];
    if (!string.IsNullOrWhiteSpace(origem))
    {
        if (!Uri.TryCreate(origem, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https") || uri.GetLeftPart(UriPartial.Authority) != origem)
            throw new InvalidOperationException("Cors__Origin deve ser uma origem HTTP ou HTTPS sem caminho.");
        policy.WithOrigins(origem).AllowAnyHeader().AllowAnyMethod();
    }
}));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var app = builder.Build();

if (args.Contains("--seed-development"))
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("Seed disponível somente em Development.");
    using var scope = app.Services.CreateScope();
    await SeedDesenvolvimento.ExecutarAsync(scope.ServiceProvider.GetRequiredService<TmsDbContext>());
    return;
}

app.UseExceptionHandler(handler => handler.Run(context =>
    Results.Problem(statusCode: 500, title: "Não foi possível concluir a solicitação.").ExecuteAsync(context)));
app.UseStatusCodePages(async status =>
{
    int codigo = status.HttpContext.Response.StatusCode;
    await Results.Problem(statusCode: codigo, title: codigo switch
    {
        401 => "Autenticação necessária.",
        403 => "Acesso não permitido.",
        404 => "Recurso não encontrado.",
        _ => "Erro na solicitação."
    }).ExecuteAsync(status.HttpContext);
});
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
var protegidos = app.MapGroup("/api").RequireAuthorization().AddEndpointFilter<FiltroUsuario>();
Me.Mapear(protegidos);
app.Run();

public partial class Program { }
