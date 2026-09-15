namespace Convivium.Application.Metering;

using Convivium.Application.Abstractions;
using Convivium.Domain.Billing;
using Convivium.Domain.Common;
using Convivium.Domain.Condominiums;
using Convivium.Domain.Metering;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// A folha de leitura dos medidores individuais.
/// </summary>
/// <remarks>
/// Consumo medido nao e rateio: quem gastou paga o que gastou, e por isso ele
/// entra no boleto como uma linha propria, somada depois da cota condominial.
/// </remarks>
public sealed class MeteringService(IApplicationDbContext db, IClock clock)
{
    /// <summary>
    /// Monta a folha da competencia, com a leitura anterior de cada unidade ja
    /// preenchida a partir do ultimo fechamento.
    /// </summary>
    public async Task<MeterReadingSheet> GetSheetAsync(
        Competence competence,
        MeteredUtility utility = MeteredUtility.Gas,
        CancellationToken cancellationToken = default)
    {
        var unidades = await db.Units
            .AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.Identifier)
            .Select(u => new { u.Id, u.Identifier })
            .ToListAsync(cancellationToken);

        var salvas = await db.MeterReadings
            .AsNoTracking()
            .Where(r => r.Utility == utility && r.Competence == competence)
            .ToListAsync(cancellationToken);

        // A leitura anterior de quem ainda nao foi digitado sai da competencia
        // mais recente antes desta — nao da imediatamente anterior, porque um
        // mes pode ter ficado sem medicao.
        var anteriores = await db.MeterReadings
            .AsNoTracking()
            .Where(r => r.Utility == utility && r.Competence < competence)
            .GroupBy(r => r.UnitId)
            .Select(g => new
            {
                UnitId = g.Key,
                Ultima = g.OrderByDescending(r => r.Competence)
                    .Select(r => r.CurrentReading)
                    .First(),
            })
            .ToDictionaryAsync(x => x.UnitId, x => x.Ultima, cancellationToken);

        decimal preco = salvas.Count > 0 ? salvas[0].UnitPrice : 0m;
        DateOnly? lida = salvas.Count > 0 ? salvas[0].ReadOn : null;

        var linhas = new List<MeterReadingLine>(unidades.Count);

        foreach (var unidade in unidades)
        {
            MeterReading? salva = salvas.FirstOrDefault(r => r.UnitId == unidade.Id);

            bool herdada = salva is null && anteriores.ContainsKey(unidade.Id);
            decimal anterior = salva?.PreviousReading
                ?? (anteriores.TryGetValue(unidade.Id, out decimal ultima) ? ultima : 0m);

            linhas.Add(new MeterReadingLine(
                unidade.Id,
                unidade.Identifier,
                anterior,
                salva?.CurrentReading,
                salva?.Consumption ?? 0m,
                salva?.Amount ?? 0m,
                herdada));
        }

        bool editavel = !await db.BillingCycles.AnyAsync(
            c => c.Competence == competence
                && c.Status != BillingCycleStatus.Draft
                && c.Status != BillingCycleStatus.Cancelled,
            cancellationToken);

        return new MeterReadingSheet(
            competence.ToString(),
            utility,
            preco,
            lida,
            linhas,
            linhas.Sum(l => l.Consumption),
            linhas.Sum(l => l.Amount),
            linhas.Count(l => l.CurrentReading is null),
            editavel);
    }

    /// <summary>Grava a folha inteira de uma vez, como ela e preenchida.</summary>
    public async Task<MeterReadingSheet> SaveAsync(
        SaveMeterReadingsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Competence competence = Competence.Parse(request.Competence);

        DomainException.ThrowIf(
            request.UnitPrice <= 0m,
            "Informe o preço do metro cúbico antes de salvar as leituras.");

        bool jaCobrada = await db.BillingCycles.AnyAsync(
            c => c.Competence == competence
                && c.Status != BillingCycleStatus.Draft
                && c.Status != BillingCycleStatus.Cancelled,
            cancellationToken);

        DomainException.ThrowIf(
            jaCobrada,
            $"A competência {competence} já foi fechada. As leituras dela foram para os boletos " +
            "e mudá-las agora não mudaria o que foi cobrado.");

        var ativas = await db.Units
            .Where(u => u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        var existentes = await db.MeterReadings
            .Where(r => r.Utility == request.Utility && r.Competence == competence)
            .ToListAsync(cancellationToken);

        DateOnly lida = request.ReadOn ?? clock.Today;

        foreach (UnitReadingInput entrada in request.Readings ?? [])
        {
            DomainException.ThrowIf(
                !ativas.Contains(entrada.UnitId),
                "Uma das leituras aponta para uma unidade que não está ativa neste condomínio.");

            MeterReading? leitura = existentes.FirstOrDefault(r => r.UnitId == entrada.UnitId);

            // Sem leitura atual a linha nao vira registro: unidade sem medicao
            // no mes nao deve aparecer no boleto cobrando zero, deve ficar de
            // fora ate alguem ir la ler o medidor.
            if (entrada.CurrentReading is not { } atual)
            {
                if (leitura is not null)
                {
                    db.MeterReadings.Remove(leitura);
                }

                continue;
            }

            decimal anterior = entrada.PreviousReading ?? leitura?.PreviousReading ?? 0m;

            DomainException.ThrowIf(
                atual < anterior,
                $"A leitura atual da unidade está menor que a anterior ({atual:N3} < {anterior:N3}). " +
                "Confira o número antes de salvar.");

            if (leitura is null)
            {
                leitura = new MeterReading
                {
                    UnitId = entrada.UnitId,
                    Competence = competence,
                    Utility = request.Utility,
                };

                db.MeterReadings.Add(leitura);
            }

            leitura.PreviousReading = anterior;
            leitura.CurrentReading = atual;
            leitura.UnitPrice = request.UnitPrice;
            leitura.ReadOn = lida;
        }

        await db.SaveChangesAsync(cancellationToken);

        return await GetSheetAsync(competence, request.Utility, cancellationToken);
    }

    /// <summary>
    /// O que cada unidade deve de consumo medido na competencia, pronto para
    /// virar linha de boleto.
    /// </summary>
    public async Task<IReadOnlyList<MeterReading>> ForCompetenceAsync(
        Competence competence,
        CancellationToken cancellationToken = default)
        => await db.MeterReadings
            .AsNoTracking()
            .Where(r => r.Competence == competence)
            .ToListAsync(cancellationToken);
}
