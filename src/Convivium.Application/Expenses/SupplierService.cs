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
                s.Expenses.Count(e => e.Status != ExpenseStatus.Cancelled)))
            .ToListAsync(cancellationToken);
    }

    public async Task<SupplierDto> CreateAsync(
        SaveSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        DomainException.ThrowIf(string.IsNullOrWhiteSpace(request.Name), "Informe o nome do fornecedor.");

        string? document = OnlyDigits(request.Document);

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
            supplier.Email, supplier.Phone, supplier.IsActive, 0);
    }

    public async Task<SupplierDto> UpdateAsync(
        Guid id,
        SaveSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        Supplier supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Fornecedor não encontrado.");

        supplier.Name = request.Name.Trim();
        supplier.Document = OnlyDigits(request.Document);
        supplier.Email = request.Email?.Trim().ToLowerInvariant();
        supplier.Phone = request.Phone?.Trim();
        supplier.Notes = request.Notes?.Trim();

        await db.SaveChangesAsync(cancellationToken);

        int expenseCount = await db.Expenses
            .CountAsync(e => e.SupplierId == id && e.Status != ExpenseStatus.Cancelled, cancellationToken);

        return new SupplierDto(
            supplier.Id, supplier.Name, supplier.Document,
            supplier.Email, supplier.Phone, supplier.IsActive, expenseCount);
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

    /// <summary>CNPJ e CPF sao guardados so com digitos, para a busca nao depender da mascara.</summary>
    private static string? OnlyDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string digits = new(value.Where(char.IsAsciiDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }
}
