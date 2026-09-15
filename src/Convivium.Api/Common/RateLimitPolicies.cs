namespace Convivium.Api.Common;

using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

/// <summary>
/// Limite de tentativas nas rotas que aceitam credencial.
/// </summary>
/// <remarks>
/// <para>
/// Sem isto, descobrir a senha de um síndico é só uma questão de tempo de
/// máquina: o BCrypt encarece cada tentativa, mas não limita quantas cabem
/// num dia. O limite por IP não resolve ataque distribuído — resolve o caso
/// comum, que é um script apontado para o endereço do condomínio.
/// </para>
/// <para>
/// Login e senha andam juntos numa janela apertada. Renovação de sessão tem
/// janela própria e folgada: ela é automática, várias abas abertas renovam
/// cada uma por si, e derrubar isso desloga morador que não fez nada.
/// </para>
/// </remarks>
public static class RateLimitPolicies
{
    /// <summary>Login, redefinição e definição de senha.</summary>
    public const string Auth = "auth";

    /// <summary>Renovação de token, que o navegador dispara sozinho.</summary>
    public const string Session = "session";

    /// <summary>Janela das duas políticas. Muda o limite, não o período.</summary>
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    public static IServiceCollection AddConviviumRateLimiter(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(Auth, PartitionBy(permits: 10));
            options.AddPolicy(Session, PartitionBy(permits: 60));

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                // Retry-After em segundos, como manda a RFC 9110, para o cliente
                // não ficar adivinhando quanto esperar.
                //
                // O limitador só publica o metadado quando a tentativa passou
                // por fila, e aqui a fila é zero — sem o recuo para a janela
                // inteira, o cabeçalho simplesmente não sairia. A janela é o
                // pior caso: quem gastou a cota agora espera tudo isso.
                TimeSpan espera =
                    context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan doLease)
                        ? doLease
                        : Window;

                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)espera.TotalSeconds).ToString(CultureInfo.InvariantCulture);

                // Mesmo formato de erro do resto da API, para o front mostrar
                // a mensagem sem tratar este caso à parte.
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Muitas tentativas",
                        Detail = "Tentativas demais em pouco tempo. Espere alguns minutos e tente de novo.",
                    },
                    cancellationToken);
            };
        });

    /// <summary>
    /// Janela deslizante por IP de origem.
    /// </summary>
    /// <remarks>
    /// Deslizante e não fixa: na janela fixa dá para gastar a cota inteira no
    /// fim de uma e de novo no começo da seguinte, o que dobra o limite real
    /// bem no momento em que ele deveria valer.
    /// </remarks>
    private static Func<HttpContext, RateLimitPartition<string>> PartitionBy(int permits) =>
        context => RateLimitPartition.GetSlidingWindowLimiter(
            ClientKey(context),
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = permits,
                Window = Window,
                SegmentsPerWindow = 5,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            });

    /// <summary>
    /// Chave da partição: o IP de quem chamou.
    /// </summary>
    /// <remarks>
    /// Usa o IP da conexão, e não o X-Forwarded-For cru. Atrás de um proxy é o
    /// ForwardedHeaders, ligado por configuração, que reescreve
    /// <see cref="ConnectionInfo.RemoteIpAddress"/> — confiar no cabeçalho aqui
    /// deixaria qualquer um trocar de partição a cada tentativa e passar por
    /// cima do limite.
    /// </remarks>
    private static string ClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";
}
