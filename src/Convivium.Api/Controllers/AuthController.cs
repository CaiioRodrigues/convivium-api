namespace Convivium.Api.Controllers;

using Convivium.Api.Auth;
using Convivium.Api.Common;
using Convivium.Application.Abstractions;
using Convivium.Application.Auth;
using Convivium.Application.People;
using Convivium.Domain.People;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

/// <summary>Login, renovacao de sessao e troca de condominio ativo.</summary>
public sealed class AuthController(
    AuthService auth,
    PeopleService pessoas,
    IApplicationDbContext db) : ApiControllerBase
{
    /// <summary>Autentica por e-mail e senha.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<AuthResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResult>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
        => Ok(await auth.LoginAsync(request, ClientIp, cancellationToken));

    /// <summary>Troca um refresh token valido por um novo par de tokens.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Session)]
    [ProducesResponseType<AuthResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResult>> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
        => Ok(await auth.RefreshAsync(request.RefreshToken, ClientIp, cancellationToken));

    /// <summary>Revoga o refresh token da sessao atual.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        await auth.LogoutAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Emite um token novo apontando para outro condominio da pessoa.
    /// </summary>
    /// <remarks>
    /// E aqui que um sindico com dois predios troca de contexto. O vinculo e
    /// conferido no servidor: pedir um condominio onde nao ha vinculo devolve 422.
    /// </remarks>
    [HttpPost("switch/{condominiumId:guid}")]
    [Authorize]
    [ProducesResponseType<AuthResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AuthResult>> Switch(
        Guid condominiumId,
        CancellationToken cancellationToken)
        => Ok(await auth.SwitchCondominiumAsync(
            Tenant.RequirePersonId(), condominiumId, ClientIp, cancellationToken));

    /// <summary>
    /// Define a senha a partir do token recebido por e-mail, seja ele de
    /// convite de primeiro acesso ou de redefinição.
    /// </summary>
    /// <remarks>
    /// Rota pública: quem abre o link ainda não tem sessão. A autorização é o
    /// próprio token, de uso único — 7 dias no convite, 1 hora na redefinição.
    /// </remarks>
    [HttpPost("definir-senha")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SetPassword(
        [FromBody] SetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await pessoas.SetPasswordAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Pede um link de redefinição de senha por e-mail.
    /// </summary>
    /// <remarks>
    /// Responde 202 sempre, inclusive para e-mail que não existe. A resposta
    /// não pode distinguir os dois casos, senão a tela de login vira uma
    /// consulta de quem está cadastrado no condomínio.
    /// </remarks>
    [HttpPost("esqueci-senha")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await pessoas.RequestPasswordResetAsync(request, cancellationToken);
        return Accepted();
    }

    /// <summary>Dados da pessoa autenticada e o contexto ativo da sessão.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken cancellationToken)
    {
        Guid personId = Tenant.RequirePersonId();

        Person? person = await db.People
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == personId, cancellationToken);

        if (person is null)
        {
            return NotFoundProblem("Usuário não encontrado.");
        }

        return Ok(new CurrentUserResponse(
            person.Id,
            person.Name,
            person.Email,
            person.IsSuperAdmin,
            Tenant.CondominiumId,
            Tenant.Role));
    }
}

public sealed record CurrentUserResponse(
    Guid Id,
    string Name,
    string? Email,
    bool IsSuperAdmin,
    Guid? ActiveCondominiumId,
    MembershipRole? ActiveRole);
