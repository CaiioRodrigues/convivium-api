namespace Convivium.Domain.Common;

/// <summary>
/// Base de toda entidade persistida. Usa GUID v7 (sequencial no tempo), que indexa
/// bem no Postgres e nao expoe contagem de registros como um int auto-incremento faria.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>
/// Marca entidades que pertencem a um condominio especifico.
/// O <c>DbContext</c> aplica um filtro global por <see cref="CondominiumId"/>,
/// entao uma consulta nunca vaza dados de um condominio para outro.
/// </summary>
public interface ITenantScoped
{
    Guid CondominiumId { get; set; }
}
