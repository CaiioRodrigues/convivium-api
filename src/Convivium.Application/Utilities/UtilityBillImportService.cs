namespace Convivium.Application.Utilities;

using System.Security.Cryptography;
using Convivium.Application.Abstractions;
using Convivium.Application.Common;
using Convivium.Application.Expenses;
using Convivium.Domain.Common;
using Convivium.Domain.Expenses;
using Convivium.Domain.Finance;
using Convivium.Domain.Utilities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Importa faturas de concessionaria em PDF e as transforma em despesas.
/// </summary>
/// <remarks>
/// O fluxo e deliberadamente em dois passos: importar le e guarda, converter
/// gera a despesa. Um leitor de PDF erra, e lancar no caixa automaticamente
/// o que uma expressao regular achou seria confiar demais — o valor errado
/// entraria no rateio de todo mundo.
/// </remarks>
public sealed class UtilityBillImportService(
    IApplicationDbContext db,
    IPdfTextExtractor extractor,
    IEnumerable<IUtilityBillParser> parsers,
    ExpenseService expenses,
    IClock clock)
{
    /// <summary>Conta contabil padrao de cada concessionaria no plano de contas.</summary>
    private static readonly Dictionary<UtilityProvider, string> DefaultAccountCodes = new()
    {
        [UtilityProvider.Cemig] = "5.2.01",
        [UtilityProvider.Copasa] = "5.2.02",
        [UtilityProvider.Gasmig] = "5.2.03",
    };

    public async Task<UtilityBillDto> ImportAsync(
        Stream pdf,
        string fileName,
        UtilityProvider? providerHint,
        Guid? importedByPersonId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        DomainException.ThrowIf(string.IsNullOrWhiteSpace(fileName), "Informe o nome do arquivo.");

        // O PdfPig precisa de um stream posicionavel e o hash precisa ler tudo:
        // materializa uma vez em memoria em vez de ler o arquivo duas vezes.
        using var buffer = new MemoryStream();
        await pdf.CopyToAsync(buffer, cancellationToken);
        byte[] content = buffer.ToArray();

        DomainException.ThrowIf(content.Length == 0, "O arquivo enviado esta vazio.");

        string hash = Convert.ToHexString(SHA256.HashData(content));

        UtilityBill? existing = await db.UtilityBills
            .FirstOrDefaultAsync(b => b.ContentHash == hash, cancellationToken);

        if (existing is not null)
        {
            throw new DomainException(
                $"Esta fatura ja foi importada em {existing.ImportedAt:dd/MM/yyyy} " +
                $"({existing.SourceFileName}).");
        }

        string text;
        try
        {
            buffer.Position = 0;
            text = extractor.Extract(buffer);
        }
        catch (Exception ex)
        {
            throw new DomainException(
                $"Nao foi possivel ler o PDF: {ex.Message}. " +
                "Verifique se o arquivo nao esta protegido por senha ou corrompido.");
        }

        DomainException.ThrowIf(
            string.IsNullOrWhiteSpace(text),
            "O PDF nao contem texto extraivel. Se for um documento digitalizado, " +
            "sera preciso lancar a despesa manualmente.");

        IUtilityBillParser parser = SelectParser(text, providerHint);
        UtilityBillReading reading = parser.Parse(text);

        var bill = new UtilityBill
        {
            Provider = reading.Provider,
            SourceFileName = fileName.Trim(),
            ContentHash = hash,
            RawText = text,
            Amount = reading.Amount,
            DueDate = reading.DueDate,
            ReferenceMonth = reading.ReferenceMonth,
            InstallationCode = reading.InstallationCode,
            CustomerName = reading.CustomerName,
            ConsumptionKwh = reading.ConsumptionKwh,
            ConsumptionCubicMeters = reading.ConsumptionCubicMeters,
            BarcodeLine = reading.BarcodeLine,
            ParseWarnings = reading.Warnings.Count > 0 ? string.Join('\n', reading.Warnings) : null,
            Status = reading.IsComplete && reading.Warnings.Count == 0
                ? UtilityBillStatus.Parsed
                : UtilityBillStatus.NeedsReview,
            ImportedByPersonId = importedByPersonId,
            ImportedAt = clock.Now,
        };

        db.UtilityBills.Add(bill);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(bill);
    }

    /// <summary>
    /// Escolhe o leitor: a dica do usuario tem prioridade, depois quem
    /// reconhece o layout, e por ultimo o leitor generico.
    /// </summary>
    private IUtilityBillParser SelectParser(string text, UtilityProvider? hint)
    {
        var all = parsers.ToList();
        DomainException.ThrowIf(all.Count == 0, "Nenhum leitor de fatura registrado.");

        if (hint is { } provider && provider != UtilityProvider.Unknown)
        {
            IUtilityBillParser? hinted = all.FirstOrDefault(p => p.Provider == provider);
            if (hinted is not null)
            {
                return hinted;
            }
        }

        return all.FirstOrDefault(p => p.Provider != UtilityProvider.Unknown && p.CanParse(text))
            ?? all.FirstOrDefault(p => p.Provider == UtilityProvider.Unknown)
            ?? all[0];
    }

    public async Task<PagedResult<UtilityBillDto>> ListAsync(
        UtilityBillFilter filter,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        IQueryable<UtilityBill> query = db.UtilityBills.AsNoTracking();

        if (filter.Provider is { } provider)
        {
            query = query.Where(b => b.Provider == provider);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(b => b.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.InstallationCode))
        {
            string code = filter.InstallationCode.Trim();
            query = query.Where(b => b.InstallationCode == code);
        }

        int total = await query.CountAsync(cancellationToken);

        var bills = await query
            .OrderByDescending(b => b.ImportedAt)
            .Skip(page.Skip)
            .Take(page.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<UtilityBillDto>(
            bills.Select(ToDto).ToList(), total, page.NormalizedPage, page.NormalizedPageSize);
    }

    public async Task<UtilityBillDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => ToDto(await LoadAsync(id, cancellationToken));

    /// <summary>Texto bruto extraido do PDF, para conferir o que o leitor viu.</summary>
    public async Task<string> GetRawTextAsync(Guid id, CancellationToken cancellationToken = default)
        => (await LoadAsync(id, cancellationToken)).RawText;

    /// <summary>Corrige manualmente os campos que o leitor errou ou nao achou.</summary>
    public async Task<UtilityBillDto> ReviewAsync(
        Guid id,
        ReviewUtilityBillRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        UtilityBill bill = await LoadAsync(id, cancellationToken);

        DomainException.ThrowIf(
            bill.Status == UtilityBillStatus.Converted,
            "Esta fatura ja virou despesa. Corrija diretamente na despesa.");

        if (request.Amount is { } amount)
        {
            DomainException.ThrowIf(amount <= 0, "O valor deve ser maior que zero.");
            bill.Amount = amount;
        }

        if (request.DueDate is { } dueDate)
        {
            bill.DueDate = dueDate;
        }

        if (request.ReferenceMonth is { Length: > 0 } reference)
        {
            bill.ReferenceMonth = Competence.Parse(reference);
        }

        if (request.InstallationCode is { Length: > 0 } installation)
        {
            bill.InstallationCode = installation.Trim();
        }

        if (request.ConsumptionKwh is { } kwh)
        {
            bill.ConsumptionKwh = kwh;
        }

        if (request.ConsumptionCubicMeters is { } cubic)
        {
            bill.ConsumptionCubicMeters = cubic;
        }

        // Revisada por uma pessoa, os avisos do leitor deixam de valer.
        if (bill.IsComplete)
        {
            bill.Status = UtilityBillStatus.Parsed;
            bill.ParseWarnings = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(bill);
    }

    /// <summary>
    /// Cria a despesa correspondente a fatura e marca a fatura como convertida.
    /// </summary>
    public async Task<ExpenseDto> ConvertToExpenseAsync(
        Guid id,
        ConvertBillToExpenseRequest request,
        Guid? createdByPersonId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        UtilityBill bill = await LoadAsync(id, cancellationToken);

        DomainException.ThrowIf(
            bill.Status == UtilityBillStatus.Converted,
            "Esta fatura ja gerou uma despesa.");

        DomainException.ThrowIf(
            bill.Amount is not > 0,
            "A fatura nao tem valor. Corrija a leitura antes de gerar a despesa.");

        DomainException.ThrowIf(
            bill.DueDate is null,
            "A fatura nao tem vencimento. Corrija a leitura antes de gerar a despesa.");

        Guid accountId = request.LedgerAccountId
            ?? await ResolveDefaultAccountAsync(bill.Provider, cancellationToken)
            ?? throw new DomainException(
                "Nao foi possivel deduzir a conta contabil desta fatura. Informe qual usar.");

        var createRequest = new CreateExpenseRequest(
            Description: request.Description?.Trim() ?? BuildDescription(bill),
            LedgerAccountId: accountId,
            Amount: bill.Amount!.Value,
            DueDate: bill.DueDate!.Value,
            Competence: (bill.ReferenceMonth ?? Competence.From(bill.DueDate.Value).Previous()).ToString(),
            SupplierId: request.SupplierId ?? await ResolveSupplierAsync(bill.Provider, cancellationToken),
            IsApportionable: request.IsApportionable,
            DocumentNumber: bill.InstallationCode,
            Notes: BuildNotes(bill));

        ExpenseDto expense = await expenses.CreateAsync(createRequest, createdByPersonId, cancellationToken);

        // Marca a origem nos dois sentidos: a despesa aponta para a fatura e
        // a fatura para a despesa, para a tela mostrar o PDF de onde o valor veio.
        Expense? created = await db.Expenses.FirstOrDefaultAsync(e => e.Id == expense.Id, cancellationToken);
        if (created is not null)
        {
            created.UtilityBillId = bill.Id;
        }

        bill.ExpenseId = expense.Id;
        bill.Status = UtilityBillStatus.Converted;

        await db.SaveChangesAsync(cancellationToken);

        return expense with { UtilityBillId = bill.Id };
    }

    private async Task<Guid?> ResolveDefaultAccountAsync(
        UtilityProvider provider,
        CancellationToken cancellationToken)
    {
        if (!DefaultAccountCodes.TryGetValue(provider, out string? code))
        {
            return null;
        }

        return await db.LedgerAccounts
            .Where(a => a.Code == code && a.Nature == AccountNature.Expense && !a.IsGroup)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Casa a concessionaria com um fornecedor ja cadastrado, pelo nome.</summary>
    private async Task<Guid?> ResolveSupplierAsync(
        UtilityProvider provider,
        CancellationToken cancellationToken)
    {
        if (provider == UtilityProvider.Unknown)
        {
            return null;
        }

        string name = provider.ToString().ToLowerInvariant();

        return await db.Suppliers
            .Where(s => s.IsActive && s.Name.ToLower().Contains(name))
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string BuildDescription(UtilityBill bill)
    {
        string what = bill.Provider switch
        {
            UtilityProvider.Cemig => "Energia eletrica",
            UtilityProvider.Copasa => "Agua e esgoto",
            UtilityProvider.Gasmig => "Gas canalizado",
            _ => "Fatura de concessionaria",
        };

        return bill.ReferenceMonth is { } reference ? $"{what} - {reference}" : what;
    }

    private static string? BuildNotes(UtilityBill bill)
    {
        var parts = new List<string> { $"Importado do PDF {bill.SourceFileName}." };

        if (bill.ConsumptionKwh is { } kwh)
        {
            parts.Add($"Consumo: {kwh:N0} kWh.");
        }

        if (bill.ConsumptionCubicMeters is { } cubic)
        {
            parts.Add($"Consumo: {cubic:N2} m3.");
        }

        if (bill.InstallationCode is { Length: > 0 } installation)
        {
            parts.Add($"Instalacao: {installation}.");
        }

        return string.Join(' ', parts);
    }

    private async Task<UtilityBill> LoadAsync(Guid id, CancellationToken cancellationToken)
        => await db.UtilityBills.FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Fatura nao encontrada.");

    private static UtilityBillDto ToDto(UtilityBill bill) => new(
        bill.Id,
        bill.Provider,
        bill.SourceFileName,
        bill.Status,
        bill.Amount,
        bill.DueDate,
        bill.ReferenceMonth?.ToString(),
        bill.InstallationCode,
        bill.CustomerName,
        bill.ConsumptionKwh,
        bill.ConsumptionCubicMeters,
        bill.BarcodeLine,
        string.IsNullOrWhiteSpace(bill.ParseWarnings)
            ? []
            : bill.ParseWarnings.Split('\n', StringSplitOptions.RemoveEmptyEntries),
        bill.ExpenseId,
        bill.ImportedAt);
}
