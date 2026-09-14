namespace Convivium.Api.Common;

using Convivium.Application.Auth;
using Convivium.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Traduz excecoes conhecidas em respostas ProblemDetails (RFC 9457).
/// </summary>
/// <remarks>
/// Erro de regra de negocio e 422 e nao 500: o pedido chegou bem formado,
/// o que o sistema recusou foi a operacao. Isso permite ao front mostrar a
/// mensagem para o usuario, o que nunca se deve fazer com um erro inesperado.
/// </remarks>
public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        (int status, string title) = exception switch
        {
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Operação não permitida"),
            AuthenticationFailedException => (StatusCodes.Status401Unauthorized, "Não autenticado"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Recurso não encontrado"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Requisição inválida"),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno"),
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Erro não tratado em {Path}", httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                // Detalhe de erro interno nao vaza para o cliente: pode conter
                // nome de tabela, caminho de arquivo ou string de conexao.
                Detail = status == StatusCodes.Status500InternalServerError
                    ? "Não foi possível concluir a operação."
                    : exception.Message,
            },
        });
    }
}
