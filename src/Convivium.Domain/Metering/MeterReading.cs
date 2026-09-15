namespace Convivium.Domain.Metering;

using Convivium.Domain.Common;
using Convivium.Domain.Condominiums;

/// <summary>
/// A leitura do medidor de uma unidade numa competencia.
/// </summary>
/// <remarks>
/// Consumo individual nao entra no rateio: quem gastou paga o que gastou. Por
/// isso a leitura vive fora da despesa — a conta do gas que chega para o
/// condominio e uma coisa, o que cada apartamento consumiu e outra, e as duas
/// raramente batem no mesmo mes (o botijao e comprado antes de ser consumido).
///
/// O valor nao e gravado: e leitura atual menos anterior, vezes o preco da
/// competencia. Guardar o produto criaria uma segunda verdade que, mais cedo
/// ou mais tarde, discorda das parcelas que a originaram. O que congela o
/// valor e o fechamento do ciclo, que copia o resultado para o item do boleto.
/// </remarks>
public class MeterReading : Entity, ITenantScoped
{
    public Guid CondominiumId { get; set; }

    public Guid UnitId { get; set; }

    public Unit Unit { get; set; } = null!;

    public Competence Competence { get; set; }

    public MeteredUtility Utility { get; set; } = MeteredUtility.Gas;

    /// <summary>Marcacao do medidor no fim do periodo anterior.</summary>
    public decimal PreviousReading { get; set; }

    /// <summary>Marcacao do medidor nesta leitura.</summary>
    public decimal CurrentReading { get; set; }

    /// <summary>Preco do metro cubico na competencia, como combinado na compra.</summary>
    public decimal UnitPrice { get; set; }

    public DateOnly ReadOn { get; set; }

    /// <summary>
    /// Consumo do periodo. Medidor nao anda para tras: uma diferenca negativa
    /// significa troca de medidor ou digitacao errada, e vira zero em vez de
    /// virar credito.
    /// </summary>
    public decimal Consumption => Math.Max(CurrentReading - PreviousReading, 0m);

    /// <summary>Quanto a unidade paga pelo que consumiu, em reais.</summary>
    public decimal Amount => Math.Round(Consumption * UnitPrice, 2, MidpointRounding.AwayFromZero);
}
