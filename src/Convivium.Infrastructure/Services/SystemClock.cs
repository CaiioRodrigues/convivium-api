namespace Convivium.Infrastructure.Services;

using Convivium.Application.Abstractions;

/// <summary>Relogio real. Sempre em UTC — a conversao para horario local e do front.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
}
