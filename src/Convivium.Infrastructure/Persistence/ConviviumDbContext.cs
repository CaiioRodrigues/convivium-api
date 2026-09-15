namespace Convivium.Infrastructure.Persistence;

using System.Linq.Expressions;
using Convivium.Application.Abstractions;
using Convivium.Domain.Billing;
using Convivium.Domain.Common;
using Convivium.Domain.Condominiums;
using Convivium.Domain.Expenses;
using Convivium.Domain.Finance;
using Convivium.Domain.Notifications;
using Convivium.Domain.People;
using Convivium.Domain.Utilities;
using Convivium.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;

public class ConviviumDbContext : DbContext, IApplicationDbContext
{
    private readonly Guid _tenantId;
    private readonly bool _filterByTenant;

    public ConviviumDbContext(DbContextOptions<ConviviumDbContext> options, ITenantContext tenant)
        : base(options)
    {
        Tenant = tenant;

        // Lido uma unica vez na construcao: o contexto vive uma requisicao,
        // e o condominio ativo nao muda no meio dela.
        _tenantId = tenant.CondominiumId ?? Guid.Empty;

        // Havendo condominio ativo, filtra — inclusive para quem administra a
        // plataforma. Ser super admin da o direito de entrar em qualquer
        // condominio, nao o de ver todos misturados: sem isto o painel somaria
        // o caixa de predios diferentes no mesmo grafico, e o numero errado
        // nao avisa que esta errado.
        //
        // As consultas que realmente perguntam "quais condominios existem"
        // pedem IgnoreQueryFilters na cara, e continuam enxergando tudo.
        //
        // Sem condominio ativo, o filtro casa com Guid.Empty — ou seja, com
        // nada. Falhar fechado e mais seguro do que devolver a base inteira por
        // engano. A excecao e o contexto de sistema (seed, despachante de
        // e-mail), que nao tem condominio e precisa varrer todos.
        _filterByTenant = tenant.CondominiumId is not null || !tenant.IsSuperAdmin;
    }

    protected ITenantContext Tenant { get; }

    public DbSet<Condominium> Condominiums => Set<Condominium>();

    public DbSet<Block> Blocks => Set<Block>();

    public DbSet<Unit> Units => Set<Unit>();

    public DbSet<Person> People => Set<Person>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<UnitOccupancy> UnitOccupancies => Set<UnitOccupancy>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();

    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();

    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<BillingCycle> BillingCycles => Set<BillingCycle>();

    public DbSet<Charge> Charges => Set<Charge>();

    public DbSet<ChargeItem> ChargeItems => Set<ChargeItem>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<UtilityBill> UtilityBills => Set<UtilityBill>();

    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConviviumDbContext).Assembly);

        ApplyEnumsAsText(modelBuilder);
        ApplyTenantFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Competence>().HaveConversion<CompetenceConverter>();

        // Valores monetarios: 2 casas, sem ponto flutuante binario.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);

        base.ConfigureConventions(configurationBuilder);
    }

    /// <summary>
    /// Grava enums como texto em vez de inteiro. Custa alguns bytes e entrega
    /// um banco que se le sozinho: "Paid" diz mais do que "2" num extrato.
    /// </summary>
    private static void ApplyEnumsAsText(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                Type type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

                if (type.IsEnum)
                {
                    property.SetProviderClrType(typeof(string));
                    property.SetMaxLength(40);
                }
            }
        }
    }

    /// <summary>
    /// Aplica o filtro por condominio em toda entidade que pertence a um,
    /// para que nenhuma consulta precise lembrar disso.
    /// </summary>
    /// <remarks>
    /// Tres formas de pertencer, e as tres entram:
    /// <list type="bullet">
    /// <item><see cref="ITenantScoped"/>, o caso comum, filtrado por CondominiumId.</item>
    /// <item>O proprio <see cref="Condominium"/>, filtrado pelo Id: ele nao tem
    /// CondominiumId porque ele <em>e</em> o condominio. Sem isso,
    /// "db.Condominiums.FirstOrDefaultAsync()" devolvia uma linha qualquer da
    /// tabela — o que passou despercebido enquanto so existia um condominio, e
    /// virou erro de dinheiro assim que passaram a existir varios: a chave PIX
    /// do boleto sai dai.</item>
    /// <item><see cref="EmailMessage"/>, cujo CondominiumId e anulavel porque
    /// existem mensagens da plataforma. Ter a coluna sem implementar a interface
    /// deixava a fila de avisos inteira aberta: qualquer sindico listava, em
    /// "/api/notificacoes", o assunto e o e-mail dos moradores de todos os
    /// outros predios — e podia reenvia-los.</item>
    /// </list>
    /// </remarks>
    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            if (CampoDoFiltro(entity.ClrType) is not { } campo)
            {
                continue;
            }

            var parameter = Expression.Parameter(entity.ClrType, "e");
            var propriedade = Expression.Property(parameter, campo);

            // Os dois campos viram parametros da consulta, entao o modelo compilado
            // e reaproveitado entre requisicoes de condominios diferentes.
            Expression ativo = Expression.Field(Expression.Constant(this), nameof(_filterByTenant));
            Expression alvo = Expression.Field(Expression.Constant(this), nameof(_tenantId));

            // Coluna anulavel exige os dois lados no mesmo tipo. liftToNull: false
            // mantem o resultado em bool, e nao bool?, para caber no OrElse; em SQL
            // vira "condominium_id = @alvo", que nunca casa com NULL — ou seja,
            // mensagem da plataforma nao aparece dentro de condominio nenhum.
            if (propriedade.Type != alvo.Type)
            {
                alvo = Expression.Convert(alvo, propriedade.Type);
            }

            // e => !_filterByTenant || e.<campo> == _tenantId
            var body = Expression.OrElse(
                Expression.Not(ativo),
                Expression.Equal(propriedade, alvo, liftToNull: false, method: null));

            modelBuilder.Entity(entity.ClrType)
                .HasQueryFilter(Expression.Lambda(body, parameter));
        }
    }

    /// <summary>Por qual coluna esta entidade se prende ao condominio, se e que se prende.</summary>
    private static string? CampoDoFiltro(Type tipo)
    {
        if (tipo == typeof(Condominium))
        {
            return nameof(Entity.Id);
        }

        if (typeof(ITenantScoped).IsAssignableFrom(tipo) || tipo == typeof(EmailMessage))
        {
            return nameof(ITenantScoped.CondominiumId);
        }

        return null;
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditFields();
        StampTenant();

        return base.SaveChangesAsync(cancellationToken);
    }

    private void StampAuditFields()
    {
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
    }

    /// <summary>
    /// Carimba o condominio ativo em entidades novas e recusa gravacao cruzada.
    /// Esquecer de setar o CondominiumId cria um registro orfao, invisivel para
    /// todo mundo — e melhor o servico nem ter que lembrar disso.
    /// </summary>
    private void StampTenant()
    {
        if (Tenant.CondominiumId is not { } activeTenant)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
        {
            switch (entry.State)
            {
                case EntityState.Added when entry.Entity.CondominiumId == Guid.Empty:
                    entry.Entity.CondominiumId = activeTenant;
                    break;

                case EntityState.Added or EntityState.Modified
                    when !Tenant.IsSuperAdmin && entry.Entity.CondominiumId != activeTenant:
                    throw new DomainException(
                        $"Tentativa de gravar {entry.Entity.GetType().Name} em outro condomínio.");
            }
        }
    }
}
