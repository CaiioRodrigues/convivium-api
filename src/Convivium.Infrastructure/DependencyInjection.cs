namespace Convivium.Infrastructure;

using Convivium.Application.Abstractions;
using Convivium.Application.Auth;
using Convivium.Infrastructure.Auth;
using Convivium.Infrastructure.Persistence;
using Convivium.Infrastructure.Persistence.Seeding;
using Convivium.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' nao configurada.");

        services.AddDbContext<ConviviumDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsAssembly(typeof(ConviviumDbContext).Assembly.FullName)
                .EnableRetryOnFailure(3))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ConviviumDbContext>());

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IAccessTokenFactory, JwtAccessTokenFactory>();

        services.AddScoped<DemoDataSeeder>();

        return services;
    }
}
