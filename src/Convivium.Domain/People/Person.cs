namespace Convivium.Domain.People;

using Convivium.Domain.Common;

/// <summary>
/// Uma pessoa fisica no sistema: morador, proprietario, sindico ou zelador.
/// Nem toda pessoa faz login — um proprietario que nunca acessou o sistema
/// ainda precisa existir para receber cobranca por e-mail.
/// </summary>
public class Person : Entity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>E-mail em minusculas. Unico no sistema quando informado, pois e o login.</summary>
    public string? Email { get; set; }

    /// <summary>CPF somente com digitos.</summary>
    public string? Cpf { get; set; }

    public string? Phone { get; set; }

    /// <summary>Hash BCrypt. Nulo quando a pessoa ainda nao ativou o acesso.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>Acesso irrestrito a todos os condominios. Reservado a operacao da plataforma.</summary>
    public bool IsSuperAdmin { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>
    /// SHA-256 do token de primeiro acesso. O valor original só existe no link
    /// enviado por e-mail.
    /// </summary>
    /// <remarks>
    /// É assim que alguém entra no sistema pela primeira vez sem que ninguém
    /// precise transmitir uma senha: o síndico dispara o convite, a pessoa
    /// escolhe a própria senha. Mandar senha por e-mail deixaria ela em texto
    /// puro na caixa de entrada para sempre.
    /// </remarks>
    public string? InviteTokenHash { get; set; }

    public DateTimeOffset? InviteTokenExpiresAt { get; set; }

    /// <summary>Convite ainda válido e não usado.</summary>
    public bool HasPendingInvite(DateTimeOffset now) =>
        InviteTokenHash is not null && InviteTokenExpiresAt > now;

    public bool IsActive { get; set; } = true;

    public ICollection<Membership> Memberships { get; set; } = [];

    public ICollection<UnitOccupancy> Occupancies { get; set; } = [];

    public bool CanSignIn => IsActive && !string.IsNullOrEmpty(PasswordHash) && !string.IsNullOrWhiteSpace(Email);
}
