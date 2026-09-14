namespace Convivium.Infrastructure.Auth;

/// <summary>Configuracao do token de acesso, lida da secao "Jwt".</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "convivium-api";

    public string Audience { get; set; } = "convivium-web";

    /// <summary>
    /// Chave de assinatura HMAC. Precisa de pelo menos 32 bytes para HS256 e
    /// nunca deve ficar no appsettings versionado — use variavel de ambiente
    /// ou o gerenciador de segredos.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Vida do token de acesso. Curta de proposito: a renovacao silenciosa
    /// usa o refresh token, que pode ser revogado.
    /// </summary>
    public int AccessTokenMinutes { get; set; } = 30;
}
