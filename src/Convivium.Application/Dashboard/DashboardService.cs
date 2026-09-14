namespace Convivium.Application.Dashboard;

using Convivium.Application.Abstractions;
using Convivium.Domain.Billing;
using Convivium.Domain.Common;
using Convivium.Domain.Condominiums;
using Convivium.Domain.Expenses;
using Convivium.Domain.Finance;
using Convivium.Domain.Notifications;
using Convivium.Domain.Utilities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Agregacoes prontas para os graficos do convivium-web.
/// </summary>
/// <remarks>
/// Tudo aqui e somente leitura e agregado no banco. Os formatos sao pensados
/// para alimentar um grafico direto, sem o front ter que recalcular nada.
/// </remarks>
public sealed class DashboardService(IApplicationDbContext db, IClock clock)
{
    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        DateOnly today = clock.Today;
        Competence current = Competence.From(today);

        Condominium condominium = await db.Condominiums.AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new DomainException("Nenhum condomínio ativo no contexto da requisição.");

        // --- Caixa ---
        var accounts = await db.BankAccounts
            .AsNoTracking()
            .Where(a => a.IsActive)
            .Select(a => new
            {
                a.IsReserveFund,
                Balance = a.OpeningBalance + a.Entries.Sum(e =>
                    e.Direction == EntryDirection.In ? e.Amount : -e.Amount),
            })
            .ToListAsync(cancellationToken);

        decimal reserve = accounts.Where(a => a.IsReserveFund).Sum(a => a.Balance);
        decimal operating = accounts.Where(a => !a.IsReserveFund).Sum(a => a.Balance);

        // --- Resultado da competencia corrente ---
        var monthTotals = await db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.Competence == current)
            .GroupBy(e => e.Direction)
            .Select(g => new { Direction = g.Key, Total = g.Sum(e => e.Amount) })
            .ToListAsync(cancellationToken);

        decimal revenue = monthTotals.FirstOrDefault(t => t.Direction == EntryDirection.In)?.Total ?? 0m;
        decimal expense = monthTotals.FirstOrDefault(t => t.Direction == EntryDirection.Out)?.Total ?? 0m;

        // --- Contas a pagar ---
        var pending = await db.Expenses
            .AsNoTracking()
            .Where(e => e.Status == ExpenseStatus.Pending)
            .GroupBy(e => e.DueDate < today)
            .Select(g => new { Overdue = g.Key, Total = g.Sum(e => e.Amount), Count = g.Count() })
            .ToListAsync(cancellationToken);

        // --- Contas a receber ---
        var openCharges = await db.Charges
            .AsNoTracking()
            .Where(c => c.Status != ChargeStatus.Paid && c.Status != ChargeStatus.Cancelled)
            .Select(c => new { c.UnitId, c.TotalAmount, c.PaidAmount, c.DueDate })
            .ToListAsync(cancellationToken);

        decimal openReceivables = openCharges.Sum(c => c.TotalAmount - c.PaidAmount);

        var overdue = openCharges.Where(c => c.DueDate < today).ToList();
        decimal overdueReceivables = overdue.Sum(c => c.TotalAmount - c.PaidAmount);

        decimal lateCharges = overdue.Sum(c => LateChargeCalculator.Compute(
            c.TotalAmount - c.PaidAmount,
            c.DueDate,
            today,
            condominium.Billing.LateFeeRate,
            condominium.Billing.MonthlyInterestRate).Total);

        int delinquentUnits = overdue.Select(c => c.UnitId).Distinct().Count();
        int activeUnits = await db.Units.CountAsync(u => u.IsActive, cancellationToken);

        // --- Operacional ---
        var emails = await db.EmailMessages
            .AsNoTracking()
            .Where(m => m.Status == EmailStatus.Pending || m.Status == EmailStatus.Failed)
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int billsToReview = await db.UtilityBills
            .CountAsync(b => b.Status == UtilityBillStatus.NeedsReview, cancellationToken);

        return new DashboardSummary(
            Competence: current.ToString(),
            TotalBalance: operating + reserve,
            OperatingBalance: operating,
            ReserveFundBalance: reserve,
            MonthRevenue: revenue,
            MonthExpense: expense,
            MonthResult: revenue - expense,
            PendingExpenses: pending.Sum(p => p.Total),
            OverdueExpenses: pending.Where(p => p.Overdue).Sum(p => p.Total),
            PendingExpenseCount: pending.Sum(p => p.Count),
            OpenReceivables: openReceivables,
            OverdueReceivables: overdueReceivables,
            OverdueLateCharges: Math.Round(lateCharges, 2),
            DelinquentUnits: delinquentUnits,
            ActiveUnits: activeUnits,
            DelinquencyRate: activeUnits == 0 ? 0m : Math.Round((decimal)delinquentUnits / activeUnits, 4),
            PendingEmails: emails.FirstOrDefault(e => e.Status == EmailStatus.Pending)?.Count ?? 0,
            FailedEmails: emails.FirstOrDefault(e => e.Status == EmailStatus.Failed)?.Count ?? 0,
            BillsAwaitingReview: billsToReview);
    }

    /// <summary>
    /// Gasto por categoria numa competencia, ou no periodo de N meses.
    /// </summary>
    /// <remarks>
    /// Por padrao agrupa no segundo nivel do plano de contas ("5.2
    /// Concessionarias"), que e a granularidade que cabe num grafico de pizza.
    /// Com <paramref name="detailed"/> devolve a conta analitica, para a tela
    /// de detalhe.
    /// </remarks>
    public async Task<IReadOnlyList<CategorySlice>> GetExpensesByCategoryAsync(
        Competence? competence = null,
        int months = 1,
        bool detailed = false,
        CancellationToken cancellationToken = default)
    {
        var range = BuildRange(competence ?? Competence.From(clock.Today), months);

        var rows = await db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.Direction == EntryDirection.Out)
            .Where(e => range.Contains(e.Competence))
            .GroupBy(e => new { e.LedgerAccount.Code, e.LedgerAccount.Name })
            .Select(g => new
            {
                g.Key.Code,
                g.Key.Name,
                Amount = g.Sum(e => e.Amount),
                Count = g.Count(),
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return [];
        }

        // Precisa dos nomes dos grupos para rotular "5.2" como "Concessionárias".
        var groupNames = await db.LedgerAccounts
            .AsNoTracking()
            .Where(a => a.Nature == AccountNature.Expense)
            .Select(a => new { a.Code, a.Name })
            .ToDictionaryAsync(a => a.Code, a => a.Name, cancellationToken);

        var grouped = rows
            .GroupBy(r => detailed ? r.Code : GroupCode(r.Code))
            .Select(g => new
            {
                Code = g.Key,
                Name = groupNames.GetValueOrDefault(g.Key) ?? g.First().Name,
                Amount = g.Sum(r => r.Amount),
                Count = g.Sum(r => r.Count),
            })
            .ToList();

        decimal total = grouped.Sum(g => g.Amount);

        return grouped
            .Select(g => new CategorySlice(
                g.Code,
                g.Name,
                g.Amount,
                total == 0 ? 0m : Math.Round(g.Amount / total, 4),
                g.Count))
            .OrderByDescending(s => s.Amount)
            .ToList();
    }

    /// <summary>
    /// Serie mensal de receita, despesa e saldo de fechamento.
    /// </summary>
    /// <remarks>
    /// Meses sem movimento entram com zero, para o grafico nao pular o eixo.
    /// O saldo de fechamento parte do saldo real anterior ao periodo, entao a
    /// linha do saldo bate com a posicao de caixa no ultimo ponto.
    /// </remarks>
    public async Task<IReadOnlyList<MonthlyPoint>> GetMonthlySeriesAsync(
        int months = 12,
        CancellationToken cancellationToken = default)
    {
        months = Math.Clamp(months, 1, 60);

        Competence last = Competence.From(clock.Today);
        var range = BuildRange(last, months);
        Competence first = range[0];

        var totals = await db.LedgerEntries
            .AsNoTracking()
            .Where(e => range.Contains(e.Competence))
            .GroupBy(e => new { e.Competence, e.Direction })
            .Select(g => new { g.Key.Competence, g.Key.Direction, Total = g.Sum(e => e.Amount) })
            .ToListAsync(cancellationToken);

        decimal openingBalance = await db.BankAccounts
            .Where(a => a.IsActive)
            .SumAsync(a => a.OpeningBalance, cancellationToken);

        // Tudo que se movimentou antes do periodo entra no saldo inicial da serie.
        decimal before = await db.LedgerEntries
            .Where(e => !range.Contains(e.Competence))
            .Where(e => e.Competence < first)
            .SumAsync(e => e.Direction == EntryDirection.In ? e.Amount : -e.Amount, cancellationToken);

        decimal running = openingBalance + before;
        var points = new List<MonthlyPoint>(range.Count);

        foreach (Competence competence in range)
        {
            decimal revenue = totals
                .Where(t => t.Competence == competence && t.Direction == EntryDirection.In)
                .Sum(t => t.Total);

            decimal expense = totals
                .Where(t => t.Competence == competence && t.Direction == EntryDirection.Out)
                .Sum(t => t.Total);

            running += revenue - expense;

            points.Add(new MonthlyPoint(
                competence.ToString(),
                competence.Year,
                competence.Month,
                revenue,
                expense,
                revenue - expense,
                running));
        }

        return points;
    }

    /// <summary>Ranking de gasto por fornecedor no periodo.</summary>
    public async Task<IReadOnlyList<SupplierSpending>> GetTopSuppliersAsync(
        int months = 6,
        int take = 10,
        CancellationToken cancellationToken = default)
    {
        var range = BuildRange(Competence.From(clock.Today), Math.Clamp(months, 1, 60));

        var rows = await db.Expenses
            .AsNoTracking()
            .Where(e => e.Status != ExpenseStatus.Cancelled)
            .Where(e => range.Contains(e.Competence))
            .GroupBy(e => new { e.SupplierId, Name = e.Supplier != null ? e.Supplier.Name : null })
            .Select(g => new
            {
                g.Key.SupplierId,
                g.Key.Name,
                Amount = g.Sum(e => e.Amount),
                Count = g.Count(),
            })
            .ToListAsync(cancellationToken);

        decimal total = rows.Sum(r => r.Amount);

        return rows
            .OrderByDescending(r => r.Amount)
            .Take(Math.Clamp(take, 1, 50))
            .Select(r => new SupplierSpending(
                r.SupplierId,
                r.Name ?? "Sem fornecedor",
                r.Amount,
                r.Count,
                total == 0 ? 0m : Math.Round(r.Amount / total, 4)))
            .ToList();
    }

    /// <summary>
    /// Consumo de uma concessionaria ao longo do tempo, a partir das faturas
    /// lidas em PDF. E aqui que a leitura automatica vira informacao util:
    /// um salto no consumo de agua costuma ser vazamento.
    /// </summary>
    public async Task<ConsumptionSeries> GetConsumptionAsync(
        UtilityProvider provider,
        int months = 12,
        CancellationToken cancellationToken = default)
    {
        months = Math.Clamp(months, 1, 60);
        var range = BuildRange(Competence.From(clock.Today), months);

        var bills = await db.UtilityBills
            .AsNoTracking()
            .Where(b => b.Provider == provider)
            .Where(b => b.Status != UtilityBillStatus.Failed)
            .Where(b => b.ReferenceMonth != null)
            .Select(b => new
            {
                Competence = b.ReferenceMonth!.Value,
                b.Amount,
                b.ConsumptionKwh,
                b.ConsumptionCubicMeters,
            })
            .ToListAsync(cancellationToken);

        bool isElectric = provider == UtilityProvider.Cemig;
        string unit = isElectric ? "kWh" : "m3";

        var points = range
            .Select(competence =>
            {
                var bill = bills.FirstOrDefault(b => b.Competence == competence);

                decimal? consumption = isElectric
                    ? bill?.ConsumptionKwh
                    : bill?.ConsumptionCubicMeters;

                return new ConsumptionPoint(
                    competence.ToString(),
                    consumption,
                    unit,
                    bill?.Amount,
                    consumption is > 0 && bill?.Amount is { } amount
                        ? Math.Round(amount / consumption.Value, 4)
                        : null);
            })
            .ToList();

        var measured = points.Where(p => p.Consumption is > 0).ToList();
        var billed = points.Where(p => p.Amount is > 0).ToList();

        return new ConsumptionSeries(
            provider,
            unit,
            points,
            measured.Count == 0 ? null : Math.Round(measured.Average(p => p.Consumption!.Value), 2),
            billed.Count == 0 ? null : Math.Round(billed.Average(p => p.Amount!.Value), 2));
    }

    /// <summary>
    /// "5.2.01" -> "5.2". Um codigo de dois niveis fica nele mesmo.
    /// </summary>
    private static string GroupCode(string code)
    {
        string[] parts = code.Split('.');
        return parts.Length <= 2 ? code : string.Join('.', parts[..2]);
    }

    /// <summary>
    /// As N competencias que terminam em <paramref name="last"/>, em ordem.
    /// Vira uma clausula IN de inteiros na consulta.
    /// </summary>
    private static List<Competence> BuildRange(Competence last, int months)
    {
        months = Math.Max(1, months);

        return Enumerable
            .Range(0, months)
            .Select(offset => last.AddMonths(offset - months + 1))
            .ToList();
    }
}
