namespace Convivium.Application.Expenses;

using System.Linq.Expressions;
using Convivium.Application.Abstractions;
using Convivium.Application.Common;
using Convivium.Domain.Common;
using Convivium.Domain.Expenses;
using Convivium.Domain.Finance;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Contas a pagar do condominio e a baixa delas no caixa.
/// </summary>
public sealed class ExpenseService(IApplicationDbContext db, IClock clock)
{
    public async Task<PagedResult<ExpenseDto>> ListAsync(
        ExpenseFilter filter,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Expense> query = ApplyFilter(db.Expenses.AsNoTracking(), filter, clock.Today);

        int total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(e => e.Status == ExpenseStatus.Pending ? 0 : 1)
            .ThenBy(e => e.DueDate)
            .Skip(page.Skip)
            .Take(page.NormalizedPageSize)
            .Select(ToDto(clock.Today))
            .ToListAsync(cancellationToken);

        return new PagedResult<ExpenseDto>(items, total, page.NormalizedPage, page.NormalizedPageSize);
    }

    public async Task<ExpenseTotals> GetTotalsAsync(
        ExpenseFilter filter,
        CancellationToken cancellationToken = default)
    {
        DateOnly today = clock.Today;

        var buckets = await ApplyFilter(db.Expenses.AsNoTracking(), filter, today)
            .Where(e => e.Status != ExpenseStatus.Cancelled)
            .GroupBy(e => new
            {
                e.Status,
                Overdue = e.Status == ExpenseStatus.Pending && e.DueDate < today,
            })
            .Select(g => new { g.Key.Status, g.Key.Overdue, Total = g.Sum(e => e.Amount), Count = g.Count() })
            .ToListAsync(cancellationToken);

        return new ExpenseTotals(
            Pending: buckets.Where(b => b.Status == ExpenseStatus.Pending).Sum(b => b.Total),
            Paid: buckets.Where(b => b.Status == ExpenseStatus.Paid).Sum(b => b.Total),
            Overdue: buckets.Where(b => b.Overdue).Sum(b => b.Total),
            Count: buckets.Sum(b => b.Count));
    }

    public async Task<ExpenseDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => await db.Expenses
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(ToDto(clock.Today))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Despesa não encontrada.");

    public async Task<ExpenseDto> CreateAsync(
        CreateExpenseRequest request,
        Guid? createdByPersonId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        DomainException.ThrowIf(request.Amount <= 0, "O valor da despesa deve ser maior que zero.");
        DomainException.ThrowIf(string.IsNullOrWhiteSpace(request.Description), "Descreva a despesa.");

        LedgerAccount account = await LoadExpenseAccountAsync(request.LedgerAccountId, cancellationToken);

        if (request.SupplierId is { } supplierId)
        {
            bool exists = await db.Suppliers.AnyAsync(s => s.Id == supplierId, cancellationToken);
            DomainException.ThrowIf(!exists, "Fornecedor não encontrado.");
        }

        var expense = new Expense
        {
            Description = request.Description.Trim(),
            LedgerAccountId = account.Id,
            SupplierId = request.SupplierId,
            Amount = request.Amount,
            DueDate = request.DueDate,
            Competence = request.Competence is { Length: > 0 } text
                ? Competence.Parse(text)
                : Competence.From(request.DueDate),
            // Por padrao segue o plano de contas, mas o sindico pode tirar uma
            // despesa especifica do rateio (ex.: obra custeada pelo fundo).
            IsApportionable = request.IsApportionable ?? account.IsApportionable,
            DocumentNumber = request.DocumentNumber?.Trim(),
            Notes = request.Notes?.Trim(),
            CreatedByPersonId = createdByPersonId,
        };

        db.Expenses.Add(expense);
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(expense.Id, cancellationToken);
    }

    public async Task<ExpenseDto> UpdateAsync(
        Guid id,
        CreateExpenseRequest request,
        CancellationToken cancellationToken = default)
    {
        Expense expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Despesa não encontrada.");

        // Alterar o valor de uma despesa ja paga deixaria o lancamento de caixa
        // divergente do que a despesa diz. Estorne, corrija e pague de novo.
        DomainException.ThrowIf(
            expense.Status == ExpenseStatus.Paid,
            "Despesa já paga. Estorne o pagamento antes de alterar.");

        LedgerAccount account = await LoadExpenseAccountAsync(request.LedgerAccountId, cancellationToken);

        expense.Description = request.Description.Trim();
        expense.LedgerAccountId = account.Id;
        expense.SupplierId = request.SupplierId;
        expense.Amount = request.Amount;
        expense.DueDate = request.DueDate;
        expense.Competence = request.Competence is { Length: > 0 } text
            ? Competence.Parse(text)
            : expense.Competence;
        expense.IsApportionable = request.IsApportionable ?? account.IsApportionable;
        expense.DocumentNumber = request.DocumentNumber?.Trim();
        expense.Notes = request.Notes?.Trim();

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(expense.Id, cancellationToken);
    }

    /// <summary>
    /// Da baixa na despesa criando o lancamento de saida no caixa.
    /// </summary>
    /// <remarks>
    /// A despesa e o lancamento nascem na mesma transacao de proposito: uma
    /// despesa marcada como paga sem o dinheiro ter saido do caixa e o tipo de
    /// divergencia que so aparece na hora da prestacao de contas.
    /// </remarks>
    public async Task<ExpenseDto> PayAsync(
        Guid id,
        PayExpenseRequest request,
        Guid? registeredByPersonId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Expense expense = await db.Expenses
            .Include(e => e.LedgerAccount)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Despesa não encontrada.");

        DomainException.ThrowIf(expense.Status == ExpenseStatus.Paid, "Despesa já está paga.");
        DomainException.ThrowIf(expense.Status == ExpenseStatus.Cancelled, "Despesa cancelada.");

        bool accountExists = await db.BankAccounts
            .AnyAsync(a => a.Id == request.BankAccountId && a.IsActive, cancellationToken);
        DomainException.ThrowIf(!accountExists, "Conta bancária não encontrada ou inativa.");

        decimal amount = request.Amount ?? expense.Amount;
        DomainException.ThrowIf(amount <= 0, "O valor pago deve ser maior que zero.");

        DateOnly paidOn = request.PaidOn ?? clock.Today;

        var entry = new LedgerEntry
        {
            CondominiumId = expense.CondominiumId,
            BankAccountId = request.BankAccountId,
            LedgerAccountId = expense.LedgerAccountId,
            Direction = EntryDirection.Out,
            Amount = amount,
            Date = paidOn,
            Competence = expense.Competence,
            Description = expense.Description,
            DocumentNumber = request.DocumentNumber ?? expense.DocumentNumber,
            ExpenseId = expense.Id,
            CreatedByPersonId = registeredByPersonId,
        };

        db.LedgerEntries.Add(entry);

        expense.Status = ExpenseStatus.Paid;
        expense.PaidOn = paidOn;
        expense.LedgerEntryId = entry.Id;

        // Pagou diferente do lancado (juros, desconto): a despesa passa a
        // valer o que de fato saiu, senao o rateio cobraria o valor errado.
        if (amount != expense.Amount)
        {
            expense.Notes = string.IsNullOrWhiteSpace(expense.Notes)
                ? $"Valor original lancado: {expense.Amount:N2}."
                : $"{expense.Notes} | Valor original lancado: {expense.Amount:N2}.";

            expense.Amount = amount;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(expense.Id, cancellationToken);
    }

    /// <summary>Desfaz o pagamento: remove o lancamento e devolve a despesa para pendente.</summary>
    public async Task<ExpenseDto> ReversePaymentAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        Expense expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Despesa não encontrada.");

        DomainException.ThrowIf(expense.Status != ExpenseStatus.Paid, "Esta despesa não está paga.");

        LedgerEntry? entry = await db.LedgerEntries
            .FirstOrDefaultAsync(e => e.ExpenseId == expense.Id, cancellationToken);

        if (entry is not null)
        {
            db.LedgerEntries.Remove(entry);
        }

        expense.Status = ExpenseStatus.Pending;
        expense.PaidOn = null;
        expense.LedgerEntryId = null;

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(expense.Id, cancellationToken);
    }

    public async Task<ExpenseDto> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Expense expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Despesa não encontrada.");

        DomainException.ThrowIf(
            expense.Status == ExpenseStatus.Paid,
            "Despesa paga não pode ser cancelada. Estorne o pagamento primeiro.");

        expense.Status = ExpenseStatus.Cancelled;
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(expense.Id, cancellationToken);
    }

    private async Task<LedgerAccount> LoadExpenseAccountAsync(
        Guid ledgerAccountId,
        CancellationToken cancellationToken)
    {
        LedgerAccount account = await db.LedgerAccounts
            .FirstOrDefaultAsync(a => a.Id == ledgerAccountId, cancellationToken)
            ?? throw new DomainException("Conta contábil não encontrada.");

        DomainException.ThrowIf(
            account.Nature != AccountNature.Expense,
            $"A conta {account.Display} é de receita e não aceita despesa.");

        DomainException.ThrowIf(
            account.IsGroup,
            $"A conta {account.Display} é um grupo e não aceita lançamento direto.");

        return account;
    }

    private static IQueryable<Expense> ApplyFilter(
        IQueryable<Expense> query,
        ExpenseFilter filter,
        DateOnly today)
    {
        if (filter.Competence is { Length: > 0 } competenceText
            && Competence.TryParse(competenceText, out Competence competence))
        {
            query = query.Where(e => e.Competence == competence);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(e => e.Status == status);
        }

        if (filter.LedgerAccountId is { } accountId)
        {
            query = query.Where(e => e.LedgerAccountId == accountId);
        }

        if (filter.SupplierId is { } supplierId)
        {
            query = query.Where(e => e.SupplierId == supplierId);
        }

        if (filter.DueFrom is { } from)
        {
            query = query.Where(e => e.DueDate >= from);
        }

        if (filter.DueTo is { } to)
        {
            query = query.Where(e => e.DueDate <= to);
        }

        if (filter.OnlyOverdue == true)
        {
            query = query.Where(e => e.Status == ExpenseStatus.Pending && e.DueDate < today);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            string term = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(e =>
                e.Description.ToLower().Contains(term) ||
                (e.Supplier != null && e.Supplier.Name.ToLower().Contains(term)) ||
                (e.DocumentNumber != null && e.DocumentNumber.ToLower().Contains(term)));
        }

        return query;
    }

    private static Expression<Func<Expense, ExpenseDto>> ToDto(DateOnly today) =>
        e => new ExpenseDto(
            e.Id,
            e.Description,
            e.Competence.ToString(),
            e.DueDate,
            e.Amount,
            e.Status,
            e.IsApportionable,
            e.Status == ExpenseStatus.Pending && e.DueDate < today,
            e.PaidOn,
            e.LedgerAccountId,
            e.LedgerAccount.Code,
            e.LedgerAccount.Name,
            e.SupplierId,
            e.Supplier != null ? e.Supplier.Name : null,
            e.DocumentNumber,
            e.Notes,
            e.UtilityBillId);
}
