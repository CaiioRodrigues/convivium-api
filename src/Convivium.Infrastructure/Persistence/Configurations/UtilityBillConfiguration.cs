namespace Convivium.Infrastructure.Persistence.Configurations;

using Convivium.Domain.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class UtilityBillConfiguration : IEntityTypeConfiguration<UtilityBill>
{
    public void Configure(EntityTypeBuilder<UtilityBill> builder)
    {
        builder.Property(b => b.SourceFileName).HasMaxLength(260).IsRequired();
        builder.Property(b => b.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(b => b.InstallationCode).HasMaxLength(40);
        builder.Property(b => b.CustomerName).HasMaxLength(200);
        builder.Property(b => b.BarcodeLine).HasMaxLength(60);
        builder.Property(b => b.ParseWarnings).HasMaxLength(2000);

        builder.Property(b => b.ConsumptionKwh).HasPrecision(12, 3);
        builder.Property(b => b.ConsumptionCubicMeters).HasPrecision(12, 3);

        // O mesmo PDF nao entra duas vezes: evita despesa duplicada por reenvio.
        builder.HasIndex(b => new { b.CondominiumId, b.ContentHash }).IsUnique();
        builder.HasIndex(b => new { b.CondominiumId, b.Provider, b.Status });

        builder.Ignore(b => b.IsComplete);
    }
}
