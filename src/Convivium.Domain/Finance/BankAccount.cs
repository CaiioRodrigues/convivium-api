namespace Convivium.Domain.Finance;

using Convivium.Domain.Common;
using Convivium.Domain.Condominiums;

/// <summary>
/// Uma conta onde o dinheiro do condominio fica. O saldo nunca e armazenado:
/// ele e sempre <see cref="OpeningBalance"/> mais a soma dos lancamentos,
/// para que nao exista divergencia entre o saldo e o extrato.
/// </summary>
public class BankAccount : Entity, ITenantScoped
{
    public Guid CondominiumId { get; set; }

    public Condominium Condominium { get; set; } = null!;

    /// <summary>Ex.: "Conta Corrente Itau", "Fundo de Reserva".</summary>
    public string Name { get; set; } = string.Empty;

    public BankAccountKind Kind { get; set; } = BankAccountKind.Checking;

    /// <summary>Codigo COMPE do banco, ex.: "341" (Itau), "001" (BB), "756" (Sicoob).</summary>
    public string? BankCode { get; set; }

    public string? Agency { get; set; }

    public string? AccountNumber { get; set; }

    /// <summary>
    /// Saldo no momento em que a conta entrou no sistema. Serve para migrar
    /// um condominio que ja existia sem precisar importar o historico inteiro.
    /// </summary>
    public decimal OpeningBalance { get; set; }

    public DateOnly OpeningDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// Marca a conta que guarda o fundo de reserva. Esse dinheiro tem destinacao
    /// vinculada (obras e emergencias) e por isso aparece separado no dashboard.
    /// </summary>
    public bool IsReserveFund { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<LedgerEntry> Entries { get; set; } = [];
}
