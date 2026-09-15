namespace Convivium.Api.Auth;

using Convivium.Domain.People;
using Microsoft.AspNetCore.Authorization;

/// <summary>Nomes das politicas de autorizacao usadas nos controllers.</summary>
public static class ConviviumPolicies
{
    /// <summary>Qualquer pessoa com vinculo ativo no condominio.</summary>
    public const string Member = "Member";

    /// <summary>Conselho fiscal para cima: leitura completa da prestacao de contas.</summary>
    public const string Council = "Council";

    /// <summary>Subsindico para cima: pode lancar despesas e movimentar o caixa.</summary>
    public const string Finance = "Finance";

    /// <summary>Sindico ou administradora: fecha rateio e altera dados do condominio.</summary>
    public const string Manager = "Manager";

    /// <summary>
    /// Operacao da plataforma: cria condominios e enxerga todos eles.
    /// </summary>
    /// <remarks>
    /// Separada de Manager de proposito. As politicas por papel concedem tudo
    /// ao super admin, mas a reciproca nao vale: sindico manda no predio dele e
    /// nao pode criar outros nem ver os dos vizinhos.
    /// </remarks>
    public const string SuperAdmin = "SuperAdmin";
}

/// <summary>Exige um papel igual ou superior ao informado.</summary>
public sealed class MinimumRoleRequirement(MembershipRole minimum) : IAuthorizationRequirement
{
    public MembershipRole Minimum { get; } = minimum;
}

public sealed class MinimumRoleHandler : AuthorizationHandler<MinimumRoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MinimumRoleRequirement requirement)
    {
        if (context.User.FindFirst(Application.Auth.ConviviumClaims.SuperAdmin)?.Value == "1")
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        string? claim = context.User.FindFirst(Application.Auth.ConviviumClaims.Role)?.Value;

        // Os valores de MembershipRole sao crescentes em poder, entao a
        // hierarquia e uma comparacao simples.
        if (Enum.TryParse(claim, out MembershipRole role) && role >= requirement.Minimum)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
