namespace Convivium.Api.Common;

using Convivium.Infrastructure.Persistence;
using Convivium.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;

public static class StartupExtensions
{
    /// <summary>
    /// Aplica as migrations pendentes e, em desenvolvimento, popula o
    /// condominio de demonstracao.
    /// </summary>
    /// <remarks>
    /// Migrar no boot e conveniente em dev. Em producao, prefira rodar
    /// "dotnet ef database update" no deploy: duas instancias subindo ao mesmo
    /// tempo disputariam o lock de migration.
    /// </remarks>
    public static async Task MigrateAndSeedAsync(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        var db = services.GetRequiredService<ConviviumDbContext>();
        await db.Database.MigrateAsync();

        bool seedEnabled = app.Configuration.GetValue("Seed:Enabled", false);

        if (app.Environment.IsDevelopment() && seedEnabled)
        {
            var seeder = services.GetRequiredService<DemoDataSeeder>();
            await seeder.SeedAsync();
        }
    }
}
