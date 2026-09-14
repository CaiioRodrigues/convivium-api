namespace Convivium.Domain.Utilities;

public enum UtilityBillStatus
{
    /// <summary>Todos os campos obrigatorios foram lidos com confianca.</summary>
    Parsed = 1,

    /// <summary>Leu em parte. Precisa de um humano confirmar antes de virar despesa.</summary>
    NeedsReview = 2,

    /// <summary>Ja virou uma despesa no caixa.</summary>
    Converted = 3,

    /// <summary>Nao foi possivel extrair nada util do arquivo.</summary>
    Failed = 4,
}
