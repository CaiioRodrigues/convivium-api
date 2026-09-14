namespace Convivium.Domain.Billing;

using Convivium.Domain.Common;

/// <summary>Unidade participante do rateio, com os pesos possiveis.</summary>
public sealed record ApportionmentUnit(Guid UnitId, decimal IdealFraction, decimal? AreaM2);

/// <summary>Quanto coube a uma unidade e com que peso.</summary>
public sealed record ApportionmentShare(Guid UnitId, decimal Amount, decimal Weight);

/// <summary>
/// Divide um valor entre as unidades sem perder nem inventar centavos.
/// </summary>
/// <remarks>
/// Arredondar cada cota isoladamente faz a soma nao bater com o total —
/// o classico "sobrou R$ 0,03 no rateio". Aqui o calculo e feito em centavos
/// inteiros e a sobra vai, um centavo por vez, para as unidades cujo valor exato
/// tinha a maior parte fracionaria (metodo das maiores sobras). A soma das cotas
/// e sempre exatamente igual ao total distribuido.
/// </remarks>
public static class ApportionmentCalculator
{
    public static IReadOnlyList<ApportionmentShare> Distribute(
        decimal total,
        IReadOnlyCollection<ApportionmentUnit> units,
        ApportionmentMethod method)
    {
        ArgumentNullException.ThrowIfNull(units);
        DomainException.ThrowIf(units.Count == 0, "Nao ha unidades ativas para ratear a despesa.");
        DomainException.ThrowIf(total < 0, "O valor a ratear nao pode ser negativo.");

        // Trabalhar em centavos inteiros elimina o erro de arredondamento acumulado.
        long totalCents = (long)Math.Round(total * 100m, MidpointRounding.AwayFromZero);

        var weighted = units
            .Select(unit => new { Unit = unit, Weight = WeightOf(unit, method) })
            .ToList();

        decimal totalWeight = weighted.Sum(x => x.Weight);

        // Sem fracao ideal cadastrada nao da para ponderar: cai para divisao igualitaria,
        // que e melhor do que dividir por zero ou cobrar zero de todo mundo.
        if (totalWeight <= 0m)
        {
            weighted = units.Select(unit => new { Unit = unit, Weight = 1m }).ToList();
            totalWeight = weighted.Count;
        }

        var draft = weighted
            .Select(x =>
            {
                decimal exact = totalCents * x.Weight / totalWeight;
                long floor = (long)Math.Floor(exact);
                return new { x.Unit, x.Weight, Floor = floor, Remainder = exact - floor };
            })
            .ToList();

        long leftover = totalCents - draft.Sum(d => d.Floor);

        // Desempate por Id mantem o resultado estavel entre execucoes,
        // o que importa para os testes e para reprocessar um rateio.
        var order = draft
            .OrderByDescending(d => d.Remainder)
            .ThenBy(d => d.Unit.UnitId)
            .ToList();

        var extraCents = new Dictionary<Guid, long>();
        for (int i = 0; i < leftover; i++)
        {
            Guid unitId = order[i % order.Count].Unit.UnitId;
            extraCents[unitId] = extraCents.GetValueOrDefault(unitId) + 1;
        }

        return draft
            .Select(d => new ApportionmentShare(
                d.Unit.UnitId,
                (d.Floor + extraCents.GetValueOrDefault(d.Unit.UnitId)) / 100m,
                d.Weight))
            .ToList();
    }

    private static decimal WeightOf(ApportionmentUnit unit, ApportionmentMethod method) => method switch
    {
        ApportionmentMethod.IdealFraction => Math.Max(0m, unit.IdealFraction),
        ApportionmentMethod.Area => Math.Max(0m, unit.AreaM2 ?? 0m),
        ApportionmentMethod.Equal => 1m,
        _ => throw new DomainException($"Metodo de rateio nao suportado: {method}."),
    };
}
