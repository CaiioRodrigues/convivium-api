namespace Convivium.Api.Controllers;

using Convivium.Api.Auth;
using Convivium.Application.People;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Moradores, papéis e vínculo com as unidades.</summary>
[Route("api/pessoas")]
[Authorize(Policy = ConviviumPolicies.Council)]
public sealed class PeopleController(PeopleService pessoas) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PersonDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PersonDto>>> List(
        [FromQuery] PersonFilter filter,
        CancellationToken cancellationToken)
        => Ok(await pessoas.ListAsync(filter, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PersonDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PersonDto>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await pessoas.GetAsync(id, cancellationToken));

    /// <summary>
    /// Cadastra a pessoa e a vincula ao condomínio.
    /// </summary>
    /// <remarks>
    /// Quando o e-mail ou o CPF já existem de outro condomínio, o cadastro é
    /// reaproveitado em vez de duplicado — senão a pessoa perderia o login.
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = ConviviumPolicies.Manager)]
    [ProducesResponseType<PersonDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonDto>> Create(
        [FromBody] SavePersonRequest request,
        CancellationToken cancellationToken)
    {
        PersonDto pessoa = await pessoas.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = pessoa.Id }, pessoa);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = ConviviumPolicies.Manager)]
    [ProducesResponseType<PersonDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonDto>> Update(
        Guid id,
        [FromBody] SavePersonRequest request,
        CancellationToken cancellationToken)
        => Ok(await pessoas.UpdateAsync(id, request, cancellationToken));

    [HttpPut("{id:guid}/papel")]
    [Authorize(Policy = ConviviumPolicies.Manager)]
    [ProducesResponseType<PersonDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonDto>> ChangeRole(
        Guid id,
        [FromBody] ChangeRoleRequest request,
        CancellationToken cancellationToken)
        => Ok(await pessoas.ChangeRoleAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/unidades")]
    [Authorize(Policy = ConviviumPolicies.Manager)]
    [ProducesResponseType<PersonDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonDto>> LinkUnit(
        Guid id,
        [FromBody] LinkUnitRequest request,
        CancellationToken cancellationToken)
        => Ok(await pessoas.LinkUnitAsync(id, request, cancellationToken));

    /// <summary>Define quem recebe a cobrança da unidade.</summary>
    [HttpPost("vinculos/{occupancyId:guid}/responsavel")]
    [Authorize(Policy = ConviviumPolicies.Manager)]
    [ProducesResponseType<PersonDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PersonDto>> SetBillingResponsible(
        Guid occupancyId,
        CancellationToken cancellationToken)
        => Ok(await pessoas.SetBillingResponsibleAsync(occupancyId, cancellationToken));

    /// <summary>Encerra o vínculo, preservando o histórico de quem morava ali.</summary>
    [HttpDelete("vinculos/{occupancyId:guid}")]
    [Authorize(Policy = ConviviumPolicies.Manager)]
    [ProducesResponseType<PersonDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PersonDto>> UnlinkUnit(
        Guid occupancyId,
        CancellationToken cancellationToken)
        => Ok(await pessoas.UnlinkUnitAsync(occupancyId, cancellationToken));

    /// <summary>
    /// Envia o convite de primeiro acesso por e-mail.
    /// </summary>
    /// <remarks>
    /// O link vale 7 dias e é de uso único. A pessoa escolhe a própria senha:
    /// o sistema nunca envia senha por e-mail.
    /// </remarks>
    [HttpPost("{id:guid}/convite")]
    [Authorize(Policy = ConviviumPolicies.Manager)]
    [ProducesResponseType<InviteResult>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<InviteResult>> Invite(Guid id, CancellationToken cancellationToken)
        => Accepted(await pessoas.InviteAsync(id, cancellationToken));

    [HttpPost("{id:guid}/desativar")]
    [Authorize(Policy = ConviviumPolicies.Manager)]
    [ProducesResponseType<PersonDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonDto>> Deactivate(Guid id, CancellationToken cancellationToken)
        => Ok(await pessoas.DeactivateAsync(id, cancellationToken));
}
