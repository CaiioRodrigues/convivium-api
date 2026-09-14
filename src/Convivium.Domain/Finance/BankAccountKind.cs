namespace Convivium.Domain.Finance;

public enum BankAccountKind
{
    /// <summary>Conta corrente do condominio.</summary>
    Checking = 1,

    Savings = 2,

    /// <summary>Aplicacao financeira, onde costuma ficar o fundo de reserva.</summary>
    Investment = 3,

    /// <summary>Dinheiro em especie sob guarda do sindico ("caixinha").</summary>
    Cash = 4,
}
