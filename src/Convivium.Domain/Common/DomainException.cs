namespace Convivium.Domain.Common;

using System.Diagnostics.CodeAnalysis;
/// <summary>
/// Violacao de uma regra de negocio (ex.: fechar um rateio sem despesas).
/// A API traduz isso em HTTP 422, diferente de um erro inesperado (500).
/// </summary>
public class DomainException(string message) : Exception(message)
{
    [DoesNotReturn]
    public static void Throw(string message) => throw new DomainException(message);

    /// <summary>
    /// Interrompe quando a condição for verdadeira.
    /// </summary>
    /// <remarks>
    /// O atributo <c>DoesNotReturnIf</c> faz a análise de nulos do compilador
    /// entender que, passando daqui, a condição é falsa. Sem ele, um guarda
    /// como <c>ThrowIf(x is null, ...)</c> não convence o compilador de que
    /// <c>x</c> deixou de ser nulo, e o código fica cheio de <c>!</c>.
    /// </remarks>
    public static void ThrowIf([DoesNotReturnIf(true)] bool condition, string message)
    {
        if (condition)
        {
            throw new DomainException(message);
        }
    }
}
