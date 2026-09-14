namespace Convivium.Domain.Expenses;

using Convivium.Domain.Common;

/// <summary>Prestador de servico ou concessionaria. Ex.: CEMIG, COPASA, a empresa do elevador.</summary>
public class Supplier : Entity, ITenantScoped
{
    public Guid CondominiumId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>CNPJ ou CPF, somente digitos.</summary>
    public string? Document { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Expense> Expenses { get; set; } = [];
}
