namespace Convivium.Domain.People;

using Convivium.Domain.Common;

/// <summary>
/// Token de renovacao de sessao. Permite manter o morador logado no app sem
/// que o token de acesso precise ser longevo — se um access token vazar,
/// ele expira em minutos; este aqui pode ser revogado a qualquer momento.
/// </summary>
public class RefreshToken : Entity
{
    public Guid PersonId { get; set; }

    public Person Person { get; set; } = null!;

    /// <summary>
    /// SHA-256 do token. O valor original so existe no cliente: um vazamento
    /// do banco nao entrega sessoes validas.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>
    /// Token que substituiu este na rotacao. Se um token ja rotacionado for
    /// usado de novo, e sinal de roubo e toda a cadeia deve ser revogada.
    /// </summary>
    public string? ReplacedByTokenHash { get; set; }

    public string? CreatedByIp { get; set; }

    /// <summary>Condominio que estava ativo quando a sessao comecou.</summary>
    public Guid? CondominiumId { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
