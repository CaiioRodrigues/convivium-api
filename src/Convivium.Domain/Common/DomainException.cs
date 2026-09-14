namespace Convivium.Domain.Common;

/// <summary>
/// Violacao de uma regra de negocio (ex.: fechar um rateio sem despesas).
/// A API traduz isso em HTTP 422, diferente de um erro inesperado (500).
/// </summary>
public class DomainException(string message) : Exception(message)
{
    public static void Throw(string message) => throw new DomainException(message);

    public static void ThrowIf(bool condition, string message)
    {
        if (condition)
        {
            throw new DomainException(message);
        }
    }
}
