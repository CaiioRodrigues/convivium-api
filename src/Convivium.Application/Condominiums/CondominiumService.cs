namespace Convivium.Application.Condominiums;

using Convivium.Application.Abstractions;
using Convivium.Domain.Common;
using Convivium.Domain.Condominiums;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Dados e parâmetros de cobrança do condomínio.
/// </summary>
public sealed class CondominiumService(IApplicationDbContext db)
{
    /// <summary>
    /// Multa máxima por atraso permitida pelo Código Civil, art. 1.336, § 1º.
    /// </summary>
    private const decimal MaxLateFeeRate = 0.02m;

    /// <summary>
    /// Dia máximo de vencimento. Acima de 28 a data não existe em fevereiro,
    /// e o vencimento acabaria mudando de dia dependendo do mês.
    /// </summary>
    private const int MaxDueDay = 28;

    public async Task<CondominiumDto> GetAsync(CancellationToken cancellationToken = default)
    {
        Condominium condominium = await db.Condominiums
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new DomainException("Nenhum condomínio ativo no contexto da requisição.");

        var unidades = await db.Units
            .AsNoTracking()
            .GroupBy(u => u.IsActive)
            .Select(g => new { Ativa = g.Key, Quantidade = g.Count(), Fracao = g.Sum(u => u.IdealFraction) })
            .ToListAsync(cancellationToken);

        return ToDto(
            condominium,
            unitCount: unidades.Sum(u => u.Quantidade),
            activeUnitCount: unidades.Where(u => u.Ativa).Sum(u => u.Quantidade),
            idealFractionSum: unidades.Where(u => u.Ativa).Sum(u => u.Fracao));
    }

    public async Task<CondominiumDto> UpdateAsync(
        UpdateCondominiumRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Condominium condominium = await db.Condominiums.FirstOrDefaultAsync(cancellationToken)
            ?? throw new DomainException("Nenhum condomínio ativo no contexto da requisição.");

        DomainException.ThrowIf(
            string.IsNullOrWhiteSpace(request.Name),
            "Informe o nome do condomínio.");

        condominium.Name = request.Name.Trim();
        condominium.LegalName = Trim(request.LegalName);
        condominium.Cnpj = NormalizeCnpj(request.Cnpj);
        condominium.Address = ToAddress(request.Address);
        condominium.Billing = ToBillingSettings(request.Billing);

        ApplyPix(condominium, request);

        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(cancellationToken);
    }

    private static string? NormalizeCnpj(string? cnpj)
    {
        string? digits = BrazilianDocument.OnlyDigits(cnpj);

        if (digits is null)
        {
            return null;
        }

        DomainException.ThrowIf(
            !BrazilianDocument.IsValidCnpj(digits),
            "CNPJ inválido: confira os dígitos.");

        return digits;
    }

    private static Address ToAddress(AddressDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        string uf = (dto.State ?? string.Empty).Trim().ToUpperInvariant();
        DomainException.ThrowIf(uf.Length is not (0 or 2), "A UF deve ter duas letras, como MG.");

        string cep = BrazilianDocument.OnlyDigits(dto.ZipCode) ?? string.Empty;
        DomainException.ThrowIf(cep.Length is not (0 or 8), "O CEP deve ter 8 dígitos.");

        return new Address
        {
            Street = Trim(dto.Street) ?? string.Empty,
            Number = Trim(dto.Number) ?? string.Empty,
            Complement = Trim(dto.Complement),
            District = Trim(dto.District) ?? string.Empty,
            City = Trim(dto.City) ?? string.Empty,
            State = uf,
            ZipCode = cep,
        };
    }

    private static BillingSettings ToBillingSettings(BillingSettingsDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        DomainException.ThrowIf(
            dto.DueDay is < 1 or > MaxDueDay,
            $"O dia de vencimento deve ficar entre 1 e {MaxDueDay}. " +
            "Acima disso a data não existe em fevereiro.");

        DomainException.ThrowIf(
            dto.ReserveFundRate is < 0 or > 0.5m,
            "O fundo de reserva deve ficar entre 0% e 50% da cota.");

        // O limite de 2% é legal, não uma escolha do produto: quem cobrar mais
        // está sujeito a devolver em dobro.
        DomainException.ThrowIf(
            dto.LateFeeRate is < 0 or > MaxLateFeeRate,
            "A multa por atraso não pode passar de 2%, conforme o Código Civil, " +
            "art. 1.336, § 1º.");

        DomainException.ThrowIf(
            dto.MonthlyInterestRate is < 0 or > 0.1m,
            "Os juros de mora devem ficar entre 0% e 10% ao mês. O usual é 1%.");

        return new BillingSettings
        {
            DueDay = dto.DueDay,
            ReserveFundRate = dto.ReserveFundRate,
            LateFeeRate = dto.LateFeeRate,
            MonthlyInterestRate = dto.MonthlyInterestRate,
            DefaultApportionmentMethod = dto.DefaultApportionmentMethod,
        };
    }

    private static void ApplyPix(Condominium condominium, UpdateCondominiumRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PixKey))
        {
            // Sem chave, a cobrança sai sem QR Code — o condomínio ainda pode
            // receber por transferência. Limpa tudo junto para não sobrar um
            // tipo apontando para chave nenhuma.
            condominium.PixKey = null;
            condominium.PixKeyType = null;
            condominium.PixReceiverName = null;
            condominium.PixReceiverCity = null;
            return;
        }

        DomainException.ThrowIf(
            request.PixKeyType is null,
            "Informe o tipo da chave PIX (CPF, CNPJ, e-mail, telefone ou aleatória).");

        condominium.PixKey = PixKey.Normalize(request.PixKey, request.PixKeyType.Value);
        condominium.PixKeyType = request.PixKeyType;

        // Os dois campos entram no QR Code com limite de tamanho do padrão EMV.
        condominium.PixReceiverName = Truncate(
            Trim(request.PixReceiverName) ?? condominium.Name, 25);

        condominium.PixReceiverCity = Truncate(
            Trim(request.PixReceiverCity) ?? condominium.Address.City, 15);
    }

    private static CondominiumDto ToDto(
        Condominium condominium,
        int unitCount,
        int activeUnitCount,
        decimal idealFractionSum) => new(
        condominium.Id,
        condominium.Name,
        condominium.LegalName,
        condominium.Cnpj,
        new AddressDto(
            condominium.Address.Street,
            condominium.Address.Number,
            condominium.Address.Complement,
            condominium.Address.District,
            condominium.Address.City,
            condominium.Address.State,
            condominium.Address.ZipCode),
        new BillingSettingsDto(
            condominium.Billing.DueDay,
            condominium.Billing.ReserveFundRate,
            condominium.Billing.LateFeeRate,
            condominium.Billing.MonthlyInterestRate,
            condominium.Billing.DefaultApportionmentMethod),
        condominium.PixKey,
        condominium.PixKeyType,
        condominium.PixReceiverName,
        condominium.PixReceiverCity,
        condominium.IsActive,
        unitCount,
        activeUnitCount,
        idealFractionSum);

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max].TrimEnd();
}
