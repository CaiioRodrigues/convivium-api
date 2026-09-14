namespace Convivium.Domain.Billing;

public enum BillingCycleStatus
{
    /// <summary>Em montagem: da para incluir despesas e recalcular a vontade.</summary>
    Draft = 1,

    /// <summary>Fechado: os valores foram congelados e as cobrancas por unidade foram geradas.</summary>
    Closed = 2,

    /// <summary>Publicado: os moradores foram notificados e ja acessam seus boletos.</summary>
    Published = 3,

    Cancelled = 4,
}
