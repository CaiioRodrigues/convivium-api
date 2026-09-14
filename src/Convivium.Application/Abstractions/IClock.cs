namespace Convivium.Application.Abstractions;

/// <summary>
/// Fonte de tempo injetavel. Existe para que os testes de vencimento,
/// multa e juros possam fixar "hoje" em vez de depender do relogio real.
/// </summary>
public interface IClock
{
    DateTimeOffset Now { get; }

    DateOnly Today => DateOnly.FromDateTime(Now.UtcDateTime);
}
