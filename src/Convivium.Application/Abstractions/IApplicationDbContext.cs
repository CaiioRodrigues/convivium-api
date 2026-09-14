namespace Convivium.Application.Abstractions;

using Convivium.Domain.Billing;
using Convivium.Domain.Condominiums;
using Convivium.Domain.Expenses;
using Convivium.Domain.Finance;
using Convivium.Domain.Notifications;
using Convivium.Domain.People;
using Convivium.Domain.Utilities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Contrato de acesso a dados visto pela camada de aplicacao.
/// Mantem os servicos livres da implementacao concreta do EF Core.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Condominium> Condominiums { get; }

    DbSet<Block> Blocks { get; }

    DbSet<Unit> Units { get; }

    DbSet<Person> People { get; }

    DbSet<Membership> Memberships { get; }

    DbSet<UnitOccupancy> UnitOccupancies { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<BankAccount> BankAccounts { get; }

    DbSet<LedgerAccount> LedgerAccounts { get; }

    DbSet<LedgerEntry> LedgerEntries { get; }

    DbSet<Supplier> Suppliers { get; }

    DbSet<Expense> Expenses { get; }

    DbSet<BillingCycle> BillingCycles { get; }

    DbSet<Charge> Charges { get; }

    DbSet<ChargeItem> ChargeItems { get; }

    DbSet<Payment> Payments { get; }

    DbSet<UtilityBill> UtilityBills { get; }

    DbSet<EmailMessage> EmailMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
