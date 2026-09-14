namespace Convivium.Infrastructure.Auth;

using System.Security.Claims;
using System.Text;
using Convivium.Application.Auth;
using Convivium.Domain.People;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Emite o JWT de acesso assinado em HS256.
/// </summary>
/// <remarks>
/// O condominio ativo vai dentro do token. E isso que faz o filtro global do
/// DbContext funcionar sem que nenhuma consulta precise passar o id adiante —
/// e tambem o que impede o cliente de simplesmente pedir outro condominio
/// pela query string.
/// </remarks>
public sealed class JwtAccessTokenFactory : IAccessTokenFactory
{
    private readonly JwtOptions _options;
    private readonly SigningCredentials _credentials;
    private readonly JsonWebTokenHandler _handler = new();

    public JwtAccessTokenFactory(IOptions<JwtOptions> options)
    {
        _options = options.Value;

        if (Encoding.UTF8.GetByteCount(_options.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey precisa de pelo menos 32 bytes para assinar em HS256.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public AccessToken Create(Person person, Guid? condominiumId, MembershipRole? role)
    {
        ArgumentNullException.ThrowIfNull(person);

        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(ConviviumClaims.Subject, person.Id.ToString()),
            new(ConviviumClaims.Name, person.Name),
        };

        if (!string.IsNullOrWhiteSpace(person.Email))
        {
            claims.Add(new Claim(ConviviumClaims.Email, person.Email));
        }

        if (condominiumId is { } tenant)
        {
            claims.Add(new Claim(ConviviumClaims.Condominium, tenant.ToString()));
        }

        if (role is { } membershipRole)
        {
            claims.Add(new Claim(ConviviumClaims.Role, membershipRole.ToString()));
        }

        if (person.IsSuperAdmin)
        {
            claims.Add(new Claim(ConviviumClaims.SuperAdmin, "1"));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _credentials,
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
