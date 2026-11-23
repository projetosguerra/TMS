# Executar localmente no PowerShell. Configura o armazenamento de desenvolvimento da API.
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[a-zA-Z0-9-]+\.pooler\.supabase\.com$')]
    [string] $PoolerHost,
    [string] $CertificadoRaiz
)

$ErrorActionPreference = 'Stop'
$projectRef = 'bdyyutxdrxsjrpztlxow'
$projetoApi = Join-Path $PSScriptRoot 'src/Tms.Api/Tms.Api.csproj'
$conexao = New-Object System.Data.Common.DbConnectionStringBuilder
$conexao['Host'] = $PoolerHost
$conexao['Port'] = '5432'
$conexao['Database'] = 'postgres'
$conexao['Username'] = "postgres.$projectRef"
$conexao['SSL Mode'] = 'VerifyFull'
if ($CertificadoRaiz) {
    $conexao['Root Certificate'] = (Resolve-Path -LiteralPath $CertificadoRaiz).Path
}

$senha = Read-Host 'Senha do banco do projeto TMS de desenvolvimento' -AsSecureString
$codificacaoAnterior = $OutputEncoding
try {
    if ($senha.Length -eq 0) { throw 'Informe a senha do banco.' }
    $conexao['Password'] = [System.Net.NetworkCredential]::new('', $senha).Password
    $OutputEncoding = [System.Text.UTF8Encoding]::new($false)
    @{
        'ConnectionStrings:Default' = $conexao.get_ConnectionString()
        'Supabase:Url' = "https://$projectRef.supabase.co"
    } | ConvertTo-Json -Compress | dotnet user-secrets set --project $projetoApi | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao salvar a configuração local.' }
    Write-Host 'Configuração salva em User Secrets para a API em Development.'
} finally {
    $OutputEncoding = $codificacaoAnterior
    $conexao.Clear()
    $senha.Dispose()
}
