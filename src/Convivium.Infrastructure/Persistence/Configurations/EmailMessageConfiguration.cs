namespace Convivium.Infrastructure.Persistence.Configurations;

using Convivium.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class EmailMessageConfiguration : IEntityTypeConfiguration<EmailMessage>
{
    public void Configure(EntityTypeBuilder<EmailMessage> builder)
    {
        builder.Property(m => m.ToAddress).HasMaxLength(160).IsRequired();
        builder.Property(m => m.ToName).HasMaxLength(160);
        builder.Property(m => m.Subject).HasMaxLength(300).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(2000);

        // Indice que o servico de envio varre a cada ciclo: o que esta pendente e ja venceu.
        builder.HasIndex(m => new { m.Status, m.ScheduledFor });
        builder.HasIndex(m => m.ChargeId).HasFilter("charge_id IS NOT NULL");
        builder.HasIndex(m => m.CondominiumId);
    }
}
