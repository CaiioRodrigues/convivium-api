namespace Convivium.Application.Auth;

/// <summary>
/// Nomes das claims do token. Ficam centralizados porque quem emite
/// (a fabrica de tokens) e quem le (o contexto de tenant) precisam concordar.
/// </summary>
public static class ConviviumClaims
{
    /// <summary>Id da pessoa autenticada.</summary>
    public const string Subject = "sub";

    public const string Email = "email";

    public const string Name = "name";

    /// <summary>Condominio ativo na sessao.</summary>
    public const string Condominium = "cond";

    /// <summary>Papel da pessoa no condominio ativo.</summary>
    public const string Role = "role";

    /// <summary>Operador da plataforma.</summary>
    public const string SuperAdmin = "sa";
}
