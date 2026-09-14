namespace Convivium.Infrastructure.Persistence.Configurations;

using Convivium.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> builder)
    {
        builder.Property(a => a.Name).HasMaxLength(120).IsRequired();
        builder.Property(a => a.BankCode).HasMaxLength(5);
        builder.Property(a => a.Agency).HasMaxLength(15);
        builder.Property(a => a.AccountNumber).HasMaxLength(25);

        builder.HasOne(a => a.Condominium)
            .WithMany()
            .HasForeignKey(a => a.CondominiumId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.CondominiumId, a.Name }).IsUnique();
    }
}

public sealed class LedgerAccountConfiguration : IEntityTypeConfiguration<LedgerAccount>
{
    public void Configure(EntityTypeBuilder<LedgerAccount> builder)
    {
        builder.Property(a => a.Code).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Name).HasMaxLength(120).IsRequired();

        builder.HasOne(a => a.Parent)
            .WithMany(a => a.Children)
            .HasForeignKey(a => a.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.CondominiumId, a.Code }).IsUnique();

        builder.Ignore(a => a.Display);
    }
}

public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.Property(e => e.Description).HasMaxLength(300).IsRequired();
        builder.Property(e => e.DocumentNumber).HasMaxLength(60);

        builder.HasOne(e => e.BankAccount)
            .WithMany(a => a.Entries)
            .HasForeignKey(e => e.BankAccountId)
            // Restrict: apagar uma conta bancaria nao pode levar o historico junto.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.LedgerAccount)
            .WithMany()
            .HasForeignKey(e => e.LedgerAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indice que serve o extrato e o saldo por conta.
        builder.HasIndex(e => new { e.CondominiumId, e.BankAccountId, e.Date });

        // Indice que serve os graficos: gasto por categoria dentro de uma competencia.
        builder.HasIndex(e => new { e.CondominiumId, e.Competence, e.LedgerAccountId });

        builder.HasIndex(e => e.ExpenseId).HasFilter("expense_id IS NOT NULL");
        builder.HasIndex(e => e.PaymentId).HasFilter("payment_id IS NOT NULL");

        builder.Ignore(e => e.SignedAmount);
    }
}
