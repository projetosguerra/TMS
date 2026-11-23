namespace Tms.Domain;

public static class Validacao
{
    private static readonly HashSet<string> Ufs = new(
        "AC AL AP AM BA CE DF ES GO MA MT MS MG PA PB PR PE PI RJ RN RS RO RR SC SP SE TO".Split());

    public static bool Digitos(string? valor, int tamanho) =>
        valor?.Length == tamanho && valor.All(char.IsAsciiDigit);

    public static bool Cpf(string? valor) => DocumentoValido(valor, 11);
    public static bool Cnpj(string? valor) => DocumentoValido(valor, 14);
    public static bool Cep(string? valor) => Digitos(valor, 8);
    public static bool Uf(string valor) => Ufs.Contains(valor);

    private static bool DocumentoValido(string? valor, int tamanho)
    {
        if (valor is null || !Digitos(valor, tamanho) || valor.All(d => d == valor[0])) return false;
        for (int fim = tamanho - 2; fim < tamanho; fim++)
        {
            int soma = 0;
            for (int i = 0; i < fim; i++)
            {
                int peso = tamanho == 11 ? fim + 1 - i : (fim - 1 - i) % 8 + 2;
                soma += (valor[i] - '0') * peso;
            }
            int resto = soma % 11;
            if (valor[fim] - '0' != (resto < 2 ? 0 : 11 - resto)) return false;
        }
        return true;
    }

    internal static void Exigir(bool valido, string mensagem)
    {
        if (!valido) throw new RegraViolada(mensagem);
    }

    internal static void DecimalNoIntervalo(decimal valor, int casas, decimal maximo, bool permiteZero)
    {
        Exigir((permiteZero ? valor >= 0 : valor > 0) && valor <= maximo &&
            decimal.Round(valor, casas) == valor, "Valor fora do intervalo ou precisão permitidos.");
    }
}
