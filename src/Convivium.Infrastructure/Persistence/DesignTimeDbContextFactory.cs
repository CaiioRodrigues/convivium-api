namespace Convivium.Infrastructure.Persistence;

using Convivium.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Usado apenas pelas ferramentas de linha de comando ("dotnet ef migrations add").
/// Em tempo de design nao existe requisicao HTTP nem token, entao o contexto
/// e construido com um tenant de sistema.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ConviviumDbContext>
{
    private const string FallbackConnection =
        "Host=localhost;Port=5432;Database=convivium;Username=convivium;Password=convivium";

    public ConviviumDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? FallbackConnection;

        var options = new DbContextOptionsBuilder<ConviviumDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ConviviumDbContext(options, new SystemTenantContext());
    }
}
