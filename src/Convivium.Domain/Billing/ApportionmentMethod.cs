namespace Convivium.Domain.Billing;

public enum ApportionmentMethod
{
    /// <summary>
    /// Divide proporcionalmente a fracao ideal de cada unidade.
    /// E a regra supletiva do Codigo Civil, art. 1.336, inciso I.
    /// </summary>
    IdealFraction = 1,

    /// <summary>Divide em partes iguais entre as unidades ativas, se a convencao assim definir.</summary>
    Equal = 2,

    /// <summary>Divide proporcionalmente a area privativa.</summary>
    Area = 3,
}
