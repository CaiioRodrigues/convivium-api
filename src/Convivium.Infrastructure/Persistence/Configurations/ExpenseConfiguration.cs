namespace Convivium.Infrastructure.Persistence.Configurations;

using Convivium.Domain.Expenses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.Property(s => s.Name).HasMaxLength(160).IsRequired();
        builder.Property(s => s.Document).HasMaxLength(14);
        builder.Property(s => s.Email).HasMaxLength(160);
        builder.Property(s => s.Phone).HasMaxLength(20);
        builder.Property(s => s.Notes).HasMaxLength(1000);

        builder.HasIndex(s => new { s.CondominiumId, s.Name });
        builder.HasIndex(s => new { s.CondominiumId, s.Document })
            .IsUnique()
            .HasFilter("document IS NOT NULL");
    }
}

public sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.Property(e => e.Description).HasMaxLength(300).IsRequired();
        builder.Property(e => e.DocumentNumber).HasMaxLength(60);
        builder.Property(e => e.Notes).HasMaxLength(2000);

        builder.HasOne(e => e.Supplier)
            .WithMany(s => s.Expenses)
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.LedgerAccount)
            .WithMany()
            .HasForeignKey(e => e.LedgerAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Serve o fechamento do rateio: despesas rateaveis de uma competencia.
        builder.HasIndex(e => new { e.CondominiumId, e.Competence, e.Status });
        builder.HasIndex(e => new { e.CondominiumId, e.DueDate, e.Status });
        builder.HasIndex(e => e.UtilityBillId).HasFilter("utility_bill_id IS NOT NULL");

        builder.Ignore(e => e.IsOverdue);
    }
}
