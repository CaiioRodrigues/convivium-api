namespace Convivium.Application.Dashboard;

using Convivium.Domain.Utilities;

/// <summary>
/// Visao geral do condominio na competencia corrente, para o topo do painel.
/// </summary>
public sealed record DashboardSummary(
    string Competence,

    // Caixa
    decimal TotalBalance,
    decimal OperatingBalance,
    decimal ReserveFundBalance,

    // Resultado do mes
    decimal MonthRevenue,
    decimal MonthExpense,
    decimal MonthResult,

    // Contas a pagar
    decimal PendingExpenses,
    decimal OverdueExpenses,
    int PendingExpenseCount,

    // Contas a receber
    decimal OpenReceivables,
    decimal OverdueReceivables,
    decimal OverdueLateCharges,

    // Inadimplencia
    int DelinquentUnits,
    int ActiveUnits,
    decimal DelinquencyRate,

    // Fila de avisos
    int PendingEmails,
    int FailedEmails,

    // Faturas de PDF aguardando conferencia
    int BillsAwaitingReview);

/// <summary>Uma fatia do grafico de gastos por categoria.</summary>
public sealed record CategorySlice(
    string Code,
    string Name,
    decimal Amount,
    decimal Share,
    int EntryCount);

/// <summary>Um ponto da serie mensal de receita, despesa e saldo.</summary>
public sealed record MonthlyPoint(
    string Competence,
    int Year,
    int Month,
    decimal Revenue,
    decimal Expense,
    decimal Result,
    decimal ClosingBalance);

/// <summary>Fornecedor no ranking de gasto do periodo.</summary>
public sealed record SupplierSpending(
    Guid? SupplierId,
    string Name,
    decimal Amount,
    int ExpenseCount,
    decimal Share);

/// <summary>
/// Consumo de uma concessionaria ao longo dos meses, montado a partir das
/// faturas lidas em PDF.
/// </summary>
public sealed record ConsumptionPoint(
    string Competence,
    decimal? Consumption,
    string Unit,
    decimal? Amount,
    decimal? UnitPrice);

public sealed record ConsumptionSeries(
    UtilityProvider Provider,
    string Unit,
    IReadOnlyList<ConsumptionPoint> Points,
    decimal? AverageConsumption,
    decimal? AverageAmount);
