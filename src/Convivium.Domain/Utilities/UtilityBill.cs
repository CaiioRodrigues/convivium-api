namespace Convivium.Domain.Utilities;

using Convivium.Domain.Common;

/// <summary>
/// Uma fatura de concessionaria importada em PDF. Guarda tanto o que foi
/// extraido quanto o texto bruto, para que o leitor possa ser reprocessado
/// depois sem precisar do arquivo original.
/// </summary>
public class UtilityBill : Entity, ITenantScoped
{
    public Guid CondominiumId { get; set; }

    public UtilityProvider Provider { get; set; } = UtilityProvider.Unknown;

    public string SourceFileName { get; set; } = string.Empty;

    /// <summary>SHA-256 do arquivo. Evita importar a mesma fatura duas vezes.</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>Texto extraido do PDF, preservado para reprocessar ou depurar o leitor.</summary>
    public string RawText { get; set; } = string.Empty;

    public UtilityBillStatus Status { get; set; } = UtilityBillStatus.NeedsReview;

    // --- Campos extraidos ---

    public decimal? Amount { get; set; }

    public DateOnly? DueDate { get; set; }

    public Competence? ReferenceMonth { get; set; }

    /// <summary>Numero da instalacao (CEMIG) ou matricula (COPASA) que identifica o ponto de consumo.</summary>
    public string? InstallationCode { get; set; }

    public string? CustomerName { get; set; }

    /// <summary>Consumo de energia no periodo, em kWh.</summary>
    public decimal? ConsumptionKwh { get; set; }

    /// <summary>Consumo de agua ou gas no periodo, em metros cubicos.</summary>
    public decimal? ConsumptionCubicMeters { get; set; }

    /// <summary>Linha digitavel de 47 ou 48 posicoes, quando presente no PDF.</summary>
    public string? BarcodeLine { get; set; }

    /// <summary>O que o leitor nao conseguiu resolver sozinho, uma mensagem por linha.</summary>
    public string? ParseWarnings { get; set; }

    /// <summary>Despesa criada a partir desta fatura.</summary>
    public Guid? ExpenseId { get; set; }

    public Guid? ImportedByPersonId { get; set; }

    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Verdadeiro quando tudo que a despesa precisa foi extraido.</summary>
    public bool IsComplete => Amount is > 0 && DueDate is not null && ReferenceMonth is not null;
}
