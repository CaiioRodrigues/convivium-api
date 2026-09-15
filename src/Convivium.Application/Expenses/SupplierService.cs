namespace Convivium.Application.Expenses;

using Convivium.Application.Abstractions;
using Convivium.Domain.Common;
using Convivium.Domain.Expenses;
using Microsoft.EntityFrameworkCore;

/// <summary>Fornecedores e concessionarias do condominio.</summary>
public sealed class SupplierService(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<SupplierDto>> ListAsync(
        string? search = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Supplier> query = db.Suppliers.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToLowerInvariant();
            query = query.Where(s =>
                s.Name.ToLower().Contains(term) ||
                (s.Document != null && s.Document.Contains(term)));
        }

        return await query
            .OrderBy(s => s.Name)
            .Select(s => new SupplierDto(
                s.Id,
                s.Name,
                s.Document,
                s.Email,
                s.Phone,
                s.IsActive,
                s.Expenses.Count(e => e.Status != ExpenseStatus.Cancelled),
                s.Notes))
            .ToListAsync(cancellationToken);
    }

    public async Task<SupplierDto> CreateAsync(
        SaveSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        DomainException.ThrowIf(string.IsNullOrWhiteSpace(request.Name), "Informe o nome do fornecedor.");

        string? document = ValidatedDocument(request.Document);

        if (document is not null)
        {
            bool duplicated = await db.Suppliers.AnyAsync(s => s.Document == document, cancellationToken);
            DomainException.ThrowIf(duplicated, "Já existe um fornecedor com esse CNPJ/CPF.");
        }

        var supplier = new Supplier
        {
            Name = request.Name.Trim(),
            Document = document,
            Email = request.Email?.Trim().ToLowerInvariant(),
            Phone = request.Phone?.Trim(),
            Notes = request.Notes?.Trim(),
        };

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(cancellationToken);

        return new SupplierDto(
            supplier.Id, supplier.Name, supplier.Document,
            supplier.Email, supplier.Phone, supplier.IsActive, 0, supplier.Notes);
    }

    public async Task<SupplierDto> UpdateAsync(
        Guid id,
        SaveSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        Supplier supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Fornecedor não encontrado.");

        DomainException.ThrowIf(string.IsNullOrWhiteSpace(request.Name), "Informe o nome do fornecedor.");

        string? document = ValidatedDocument(request.Document);

        if (document is not null)
        {
            bool duplicated = await db.Suppliers
                .AnyAsync(s => s.Id != id && s.Document == document, cancellationToken);
            DomainException.ThrowIf(duplicated, "Já existe um fornecedor com esse CNPJ/CPF.");
        }

        supplier.Name = request.Name.Trim();
        supplier.Document = document;
        supplier.Email = request.Email?.Trim().ToLowerInvariant();
        supplier.Phone = request.Phone?.Trim();
        supplier.Notes = request.Notes?.Trim();

        await db.SaveChangesAsync(cancellationToken);

        int expenseCount = await db.Expenses
            .CountAsync(e => e.SupplierId == id && e.Status != ExpenseStatus.Cancelled, cancellationToken);

        return new SupplierDto(
            supplier.Id, supplier.Name, supplier.Document,
            supplier.Email, supplier.Phone, supplier.IsActive, expenseCount, supplier.Notes);
    }

    /// <summary>
    /// Desativa o fornecedor. Nao apaga: o historico de despesas dele precisa
    /// continuar legivel na prestacao de contas dos anos anteriores.
    /// </summary>
    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Supplier supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Fornecedor não encontrado.");

        supplier.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Confere o documento e devolve so os digitos.
    /// </summary>
    /// <remarks>
    /// Fornecedor costuma ser empresa, mas prestador autonomo entra com CPF —
    /// por isso os dois formatos valem. Guardar sem mascara faz a busca e a
    /// checagem de duplicata pararem de depender de como foi digitado.
    /// </remarks>
    private static string? ValidatedDocument(string? value)
    {
        string? digits = BrazilianDocument.OnlyDigits(value);

        if (digits is null)
        {
            return null;
        }

        DomainException.ThrowIf(
            !BrazilianDocument.IsValidCpfOrCnpj(digits),
            "CNPJ ou CPF inválido. Confira os números digitados.");

        return digits;
    }
}
