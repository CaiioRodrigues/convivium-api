namespace Convivium.Infrastructure.Persistence.Configurations;

using Convivium.Domain.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(160).IsRequired();
        builder.Property(p => p.Email).HasMaxLength(160);
        builder.Property(p => p.Cpf).HasMaxLength(11);
        builder.Property(p => p.Phone).HasMaxLength(20);
        builder.Property(p => p.PasswordHash).HasMaxLength(100);

        // O e-mail e o login, entao precisa ser unico na plataforma inteira —
        // a mesma pessoa usa uma credencial so em todos os condominios dela.
        builder.HasIndex(p => p.Email).IsUnique().HasFilter("email IS NOT NULL");
        builder.HasIndex(p => p.Cpf).IsUnique().HasFilter("cpf IS NOT NULL");
    }
}

public sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.HasOne(m => m.Condominium)
            .WithMany()
            .HasForeignKey(m => m.CondominiumId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Person)
            .WithMany(p => p.Memberships)
            .HasForeignKey(m => m.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.CondominiumId, m.PersonId }).IsUnique();
        builder.HasIndex(m => m.PersonId);

        builder.Ignore(m => m.IsActive);
    }
}

public sealed class UnitOccupancyConfiguration : IEntityTypeConfiguration<UnitOccupancy>
{
    public void Configure(EntityTypeBuilder<UnitOccupancy> builder)
    {
        builder.HasOne(o => o.Unit)
            .WithMany(u => u.Occupancies)
            .HasForeignKey(o => o.UnitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.Person)
            .WithMany(p => p.Occupancies)
            .HasForeignKey(o => o.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => new { o.UnitId, o.PersonId, o.Relation });
        builder.HasIndex(o => o.CondominiumId);

        builder.Ignore(o => o.IsActive);
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(t => t.ReplacedByTokenHash).HasMaxLength(64);
        builder.Property(t => t.CreatedByIp).HasMaxLength(45);

        builder.HasOne(t => t.Person)
            .WithMany()
            .HasForeignKey(t => t.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => new { t.PersonId, t.ExpiresAt });
    }
}
