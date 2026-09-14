namespace Convivium.Application.Finance;

using Convivium.Application.Abstractions;
using Convivium.Application.Common;
using Convivium.Domain.Common;
using Convivium.Domain.Finance;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// O caixa do condominio: contas, plano de contas, lancamentos e saldo.
/// </summary>
/// <remarks>
/// Saldo nunca e coluna. E sempre o saldo de abertura da conta mais a soma
/// dos lancamentos, calculada no banco. Guardar um saldo materializado
/// significa, mais cedo ou mais tarde, ter um saldo que nao bate com o extrato.
/// </remarks>
public sealed class CashBookService(IApplicationDbContext db, IClock clock)
{
    // --- Contas bancarias ---

    public async Task<CashPosition> GetPositionAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await db.BankAccounts
            .AsNoTracking()
            .Where(a => a.IsActive)
            .OrderByDescending(a => a.IsReserveFund == false)
            .ThenBy(a => a.Name)
            .Select(a => new BankAccountSummary(
                a.Id,
                a.Name,
                a.Kind,
                a.BankCode,
                a.Agency,
                a.AccountNumber,
                a.IsReserveFund,
                a.IsActive,
                a.OpeningBalance,
                a.OpeningBalance + a.Entries.Sum(e =>
                    e.Direction == EntryDirection.In ? e.Amount : -e.Amount)))
            .ToListAsync(cancellationToken);

        decimal reserve = accounts.Where(a => a.IsReserveFund).Sum(a => a.CurrentBalance);
        decimal operating = accounts.Where(a => !a.IsReserveFund).Sum(a => a.CurrentBalance);

        return new CashPosition(operating + reserve, operating, reserve, accounts);
    }

    public async Task<BankAccountSummary> CreateBankAccountAsync(
        CreateBankAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        DomainException.ThrowIf(string.IsNullOrWhiteSpace(request.Name), "Informe o nome da conta.");

        string name = request.Name.Trim();

        bool duplicated = await db.BankAccounts.AnyAsync(a => a.Name == name, cancellationToken);
        DomainException.ThrowIf(duplicated, $"Já existe uma conta chamada '{name}'.");

        var account = new BankAccount
        {
            Name = name,
            Kind = request.Kind,
            BankCode = request.BankCode?.Trim(),
            Agency = request.Agency?.Trim(),
            AccountNumber = request.AccountNumber?.Trim(),
            OpeningBalance = request.OpeningBalance,
            OpeningDate = request.OpeningDate ?? clock.Today,
            IsReserveFund = request.IsReserveFund,
        };

        db.BankAccounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);

        return new BankAccountSummary(
            account.Id, account.Name, account.Kind, account.BankCode, account.Agency,
            account.AccountNumber, account.IsReserveFund, account.IsActive,
            account.OpeningBalance, account.OpeningBalance);
    }

    // --- Plano de contas ---

    /// <summary>Plano de contas em arvore, pronto para renderizar no front.</summary>
    public async Task<IReadOnlyList<LedgerAccountNode>> GetChartOfAccountsAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var accounts = await db.LedgerAccounts
            .AsNoTracking()
            .Where(a => includeInactive || a.IsActive)
            .OrderBy(a => a.Code)
            .ToListAsync(cancellationToken);

        var childrenByParent = accounts
            .Where(a => a.ParentId is not null)
            .GroupBy(a => a.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        return accounts
            .Where(a => a.ParentId is null)
            .Select(a => BuildNode(a, childrenByParent))
            .ToList();
    }

    private static LedgerAccountNode BuildNode(
        LedgerAccount account,
        Dictionary<Guid, List<LedgerAccount>> childrenByParent)
    {
        var children = childrenByParent.TryGetValue(account.Id, out var list)
            ? list.Select(c => BuildNode(c, childrenByParent)).ToList()
            : [];

        return new LedgerAccountNode(
            account.Id, account.Code, account.Name, account.Nature,
            account.IsGroup, account.IsApportionable, account.IsActive, children);
    }

    public async Task<LedgerAccountNode> CreateLedgerAccountAsync(
        CreateLedgerAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string code = (request.Code ?? string.Empty).Trim();
        DomainException.ThrowIf(string.IsNullOrWhiteSpace(code), "Informe o código da conta.");
        DomainException.ThrowIf(string.IsNullOrWhiteSpace(request.Name), "Informe o nome da conta.");

        bool duplicated = await db.LedgerAccounts.AnyAsync(a => a.Code == code, cancellationToken);
        DomainException.ThrowIf(duplicated, $"A conta {code} já existe no plano de contas.");

        // O pai sai do proprio codigo: "5.2.01" pendura em "5.2".
        Guid? parentId = null;
        int lastDot = code.LastIndexOf('.');

        if (lastDot > 0)
        {
            string parentCode = code[..lastDot];
            parentId = await db.LedgerAccounts
                .Where(a => a.Code == parentCode)
                .Select(a => (Guid?)a.Id)
                .FirstOrDefaultAsync(cancellationToken);

            DomainException.ThrowIf(
                parentId is null,
                $"A conta {code} precisa que a conta pai {parentCode} exista antes.");
        }

        var account = new LedgerAccount
        {
            Code = code,
            Name = request.Name.Trim(),
            Nature = request.Nature,
            ParentId = parentId,
            IsGroup = request.IsGroup,
            IsApportionable = request.IsApportionable,
        };

        db.LedgerAccounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);

        return new LedgerAccountNode(
            account.Id, account.Code, account.Name, account.Nature,
            account.IsGroup, account.IsApportionable, account.IsActive, []);
    }

    // --- Lancamentos ---

    public async Task<PagedResult<LedgerEntryDto>> ListEntriesAsync(
        LedgerEntryFilter filter,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        IQueryable<LedgerEntry> query = ApplyFilter(db.LedgerEntries.AsNoTracking(), filter);

        int total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.CreatedAt)
            .Skip(page.Skip)
            .Take(page.NormalizedPageSize)
            .Select(ToDto())
            .ToListAsync(cancellationToken);

        return new PagedResult<LedgerEntryDto>(items, total, page.NormalizedPage, page.NormalizedPageSize);
    }

    /// <summary>
    /// Extrato de uma conta no periodo, com o saldo que ela tinha antes do
    /// primeiro dia — que e o que permite conferir o extrato contra o banco.
    /// </summary>
    public async Task<CashStatement> GetStatementAsync(
        Guid bankAccountId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        DomainException.ThrowIf(to < from, "A data final não pode ser anterior à inicial.");

        BankAccount account = await db.BankAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == bankAccountId, cancellationToken)
            ?? throw new KeyNotFoundException("Conta bancária não encontrada.");

        decimal movementBefore = await db.LedgerEntries
            .Where(e => e.BankAccountId == bankAccountId && e.Date < from)
            .SumAsync(e => e.Direction == EntryDirection.In ? e.Amount : -e.Amount, cancellationToken);

        var entries = await db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.BankAccountId == bankAccountId && e.Date >= from && e.Date <= to)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.CreatedAt)
            .Select(ToDto())
            .ToListAsync(cancellationToken);

        decimal opening = account.OpeningBalance + movementBefore;
        decimal totalIn = entries.Where(e => e.Direction == EntryDirection.In).Sum(e => e.Amount);
        decimal totalOut = entries.Where(e => e.Direction == EntryDirection.Out).Sum(e => e.Amount);

        return new CashStatement(from, to, opening, totalIn, totalOut, opening + totalIn - totalOut, entries);
    }

    public async Task<LedgerEntryDto> CreateEntryAsync(
        CreateLedgerEntryRequest request,
        Guid? createdByPersonId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        DomainException.ThrowIf(request.Amount <= 0, "O valor do lançamento deve ser maior que zero.");
        DomainException.ThrowIf(
            string.IsNullOrWhiteSpace(request.Description),
            "Descreva o lançamento.");

        bool accountExists = await db.BankAccounts
            .AnyAsync(a => a.Id == request.BankAccountId && a.IsActive, cancellationToken);
        DomainException.ThrowIf(!accountExists, "Conta bancária não encontrada ou inativa.");

        LedgerAccount ledgerAccount = await db.LedgerAccounts
            .FirstOrDefaultAsync(a => a.Id == request.LedgerAccountId, cancellationToken)
            ?? throw new DomainException("Conta contábil não encontrada.");

        // Contas sinteticas existem so para agrupar: aceitar lancamento nelas
        // faria os totais do grafico contarem o mesmo valor duas vezes.
        DomainException.ThrowIf(
            ledgerAccount.IsGroup,
            $"A conta {ledgerAccount.Display} é um grupo e não aceita lançamento direto.");

        Competence competence = request.Competence is { Length: > 0 } text
            ? Competence.Parse(text)
            : Competence.From(request.Date);

        var entry = new LedgerEntry
        {
            BankAccountId = request.BankAccountId,
            LedgerAccountId = request.LedgerAccountId,
            Direction = request.Direction,
            Amount = request.Amount,
            Date = request.Date,
            Competence = competence,
            Description = request.Description.Trim(),
            DocumentNumber = request.DocumentNumber?.Trim(),
            CreatedByPersonId = createdByPersonId,
        };

        db.LedgerEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);

        return await db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.Id == entry.Id)
            .Select(ToDto())
            .FirstAsync(cancellationToken);
    }

    /// <summary>Marca lancamentos como conferidos contra o extrato bancario.</summary>
    public async Task<int> ReconcileAsync(
        IReadOnlyCollection<Guid> entryIds,
        bool reconciled,
        CancellationToken cancellationToken = default)
    {
        var entries = await db.LedgerEntries
            .Where(e => entryIds.Contains(e.Id))
            .ToListAsync(cancellationToken);

        foreach (LedgerEntry entry in entries)
        {
            entry.ReconciledAt = reconciled ? clock.Now : null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return entries.Count;
    }

    public async Task DeleteEntryAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        LedgerEntry entry = await db.LedgerEntries
            .FirstOrDefaultAsync(e => e.Id == entryId, cancellationToken)
            ?? throw new KeyNotFoundException("Lançamento não encontrado.");

        // Um lancamento que nasceu de uma despesa ou de um pagamento nao pode
        // ser apagado sozinho: estornar a origem e o caminho certo, senao a
        // despesa fica "paga" sem nenhum dinheiro tendo saido.
        DomainException.ThrowIf(
            entry.ExpenseId is not null,
            "Este lançamento pertence a uma despesa. Estorne o pagamento da despesa.");

        DomainException.ThrowIf(
            entry.PaymentId is not null,
            "Este lançamento pertence a um pagamento de cobrança. Estorne o pagamento.");

        DomainException.ThrowIf(
            entry.ReconciledAt is not null,
            "Lançamento já conciliado. Desfaça a conciliação antes de excluir.");

        db.LedgerEntries.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<LedgerEntry> ApplyFilter(IQueryable<LedgerEntry> query, LedgerEntryFilter filter)
    {
        if (filter.BankAccountId is { } bankAccountId)
        {
            query = query.Where(e => e.BankAccountId == bankAccountId);
        }

        if (filter.LedgerAccountId is { } ledgerAccountId)
        {
            query = query.Where(e => e.LedgerAccountId == ledgerAccountId);
        }

        if (filter.From is { } from)
        {
            query = query.Where(e => e.Date >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(e => e.Date <= to);
        }

        if (filter.Direction is { } direction)
        {
            query = query.Where(e => e.Direction == direction);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            // ToLower + Contains vira "lower(coluna) LIKE '%termo%'" no Postgres.
            // Evita depender de ILike, que e especifico do Npgsql e traria o
            // provider de banco para dentro da camada de aplicacao.
            string term = filter.Search.Trim().ToLowerInvariant();

            query = query.Where(e =>
                e.Description.ToLower().Contains(term) ||
                (e.DocumentNumber != null && e.DocumentNumber.ToLower().Contains(term)));
        }

        return query;
    }

    private static System.Linq.Expressions.Expression<Func<LedgerEntry, LedgerEntryDto>> ToDto() =>
        e => new LedgerEntryDto(
            e.Id,
            e.Date,
            e.Competence.ToString(),
            e.Description,
            e.Direction,
            e.Amount,
            e.Direction == EntryDirection.In ? e.Amount : -e.Amount,
            e.BankAccountId,
            e.BankAccount.Name,
            e.LedgerAccountId,
            e.LedgerAccount.Code,
            e.LedgerAccount.Name,
            e.DocumentNumber,
            e.ReconciledAt != null,
            e.ExpenseId,
            e.PaymentId);
}
