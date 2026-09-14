namespace Convivium.Api.Controllers;

using Convivium.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected ITenantContext Tenant => HttpContext.RequestServices.GetRequiredService<ITenantContext>();

    /// <summary>IP de origem, usado para rastrear de onde uma sessao foi criada.</summary>
    protected string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>404 com um corpo ProblemDetails coerente com o resto da API.</summary>
    protected ActionResult NotFoundProblem(string message) =>
        Problem(title: "Recurso nao encontrado", detail: message, statusCode: StatusCodes.Status404NotFound);
}
