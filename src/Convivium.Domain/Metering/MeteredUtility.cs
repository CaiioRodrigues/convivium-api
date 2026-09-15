namespace Convivium.Domain.Metering;

/// <summary>O que o medidor de cada unidade mede.</summary>
public enum MeteredUtility
{
    /// <summary>Gas encanado ou de central, cobrado por metro cubico.</summary>
    Gas = 1,

    /// <summary>Agua com hidrometro individual.</summary>
    Water = 2,
}
