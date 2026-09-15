namespace Convivium.Infrastructure.Persistence.Configurations;

using Convivium.Domain.Metering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class MeterReadingConfiguration : IEntityTypeConfiguration<MeterReading>
{
    public void Configure(EntityTypeBuilder<MeterReading> builder)
    {
        // Tres casas: o medidor de gas marca em litros dentro do metro cubico,
        // e 0,001 m3 ja e centavo quando o m3 passa de vinte reais.
        builder.Property(r => r.PreviousReading).HasPrecision(14, 3);
        builder.Property(r => r.CurrentReading).HasPrecision(14, 3);
        builder.Property(r => r.UnitPrice).HasPrecision(12, 4);

        builder.HasOne(r => r.Unit)
            .WithMany()
            .HasForeignKey(r => r.UnitId)
            .OnDelete(DeleteBehavior.Cascade);

        // Uma leitura por unidade, por utilidade, por competencia.
        builder.HasIndex(r => new { r.CondominiumId, r.Utility, r.Competence, r.UnitId })
            .IsUnique();

        // Serve a busca da leitura anterior: a ultima competencia daquela
        // unidade, para a proxima ja vir com o "leitura anterior" preenchido.
        builder.HasIndex(r => new { r.UnitId, r.Utility, r.Competence });

        builder.Ignore(r => r.Consumption);
        builder.Ignore(r => r.Amount);
    }
}
