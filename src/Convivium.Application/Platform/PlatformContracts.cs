namespace Convivium.Application.Platform;

/// <summary>Resumo de um condominio na tela de quem administra a plataforma.</summary>
public sealed record CondominiumSummaryDto(
    Guid Id,
    string Name,
    string? Cnpj,
    string City,
    bool IsActive,
    int UnitCount,
    int PersonCount,
    /// <summary>Quem responde pelo condominio hoje. Nulo enquanto ninguem aceitou.</summary>
    string? ManagerName,
    string? ManagerEmail,
    /// <summary>Verdadeiro enquanto o sindico ainda nao escolheu a senha.</summary>
    bool ManagerPending);

public sealed record CreateCondominiumRequest(
    string Name,
    /// <summary>Cidade entra no QR Code do PIX, entao vale pedir desde o inicio.</summary>
    string? City,
    string? State,
    string? Cnpj,
    /// <summary>Nome de quem vai administrar o condominio.</summary>
    string ManagerName,
    /// <summary>E-mail do sindico. E por ele que o convite de acesso sai.</summary>
    string ManagerEmail);

/// <summary>O condominio recem-criado e o convite do sindico.</summary>
public sealed record CreateCondominiumResult(
    Guid CondominiumId,
    string Name,
    Guid ManagerId,
    string ManagerEmail,
    /// <summary>
    /// Link de primeiro acesso do sindico. Devolvido para quem cria poder
    /// repassar por outro meio quando o e-mail nao chegar.
    /// </summary>
    string InviteUrl,
    DateTimeOffset InviteExpiresAt);
