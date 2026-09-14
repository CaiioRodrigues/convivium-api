namespace Convivium.Domain.Finance;

public enum AccountNature
{
    /// <summary>Entra dinheiro: taxa condominial, multa, aluguel de salao.</summary>
    Revenue = 1,

    /// <summary>Sai dinheiro: energia, folha, manutencao.</summary>
    Expense = 2,
}
