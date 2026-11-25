namespace Tms.Domain;

public abstract class Entidade
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTimeOffset CriadoEm { get; protected set; } = DateTimeOffset.UtcNow;
}

public enum StatusFrete { ABERTO, EM_COTACAO, CONTRATADO, COLETADO, EM_TRANSITO, ENTREGUE, CANCELADO }
public enum TipoParceiro { TRANSPORTADORA, AUTONOMO }
public enum Perfil { ADMIN, OPERADOR }
public enum OrigemProposta { TABELA, MANUAL }
public enum TipoDocumento { NFE, CTE, OUTRO }

public class RegraViolada(string mensagem) : Exception(mensagem);
public sealed class RotaSemAtendimento() : RegraViolada("Nenhuma transportadora atende esta rota");
