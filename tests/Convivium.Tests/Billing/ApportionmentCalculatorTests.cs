namespace Convivium.Tests.Billing;

using Convivium.Domain.Billing;
using Convivium.Domain.Common;

/// <summary>
/// O rateio e onde um erro de centavo vira reclamacao em assembleia:
/// a soma das cotas tem que bater exatamente com o valor distribuido.
/// </summary>
public class ApportionmentCalculatorTests
{
    private static ApportionmentUnit Unit(decimal fraction, decimal? area = null) =>
        new(Guid.NewGuid(), fraction, area);

    [Fact]
    public void Distribui_em_partes_iguais_quando_as_fracoes_sao_iguais()
    {
        var units = Enumerable.Range(0, 4).Select(_ => Unit(0.25m)).ToList();

        var shares = ApportionmentCalculator.Distribute(1000m, units, ApportionmentMethod.IdealFraction);

        shares.ShouldAllBe(s => s.Amount == 250m);
        shares.Sum(s => s.Amount).ShouldBe(1000m);
    }

    [Fact]
    public void Soma_das_cotas_bate_com_o_total_quando_a_divisao_nao_e_exata()
    {
        // R$ 1.000 entre 3 unidades da 333,333... por unidade.
        var units = Enumerable.Range(0, 3).Select(_ => Unit(1m / 3m)).ToList();

        var shares = ApportionmentCalculator.Distribute(1000m, units, ApportionmentMethod.IdealFraction);

        shares.Sum(s => s.Amount).ShouldBe(1000m);

        // O centavo que sobra vai para uma unidade so, nao some nem duplica.
        shares.Count(s => s.Amount == 333.34m).ShouldBe(1);
        shares.Count(s => s.Amount == 333.33m).ShouldBe(2);
    }

    [Fact]
    public void Soma_bate_com_o_total_para_qualquer_quantidade_de_unidades()
    {
        // Varre tamanhos e valores que produzem dizimas em varios pontos.
        foreach (int count in new[] { 2, 3, 7, 11, 24, 37, 100 })
        {
            var units = Enumerable.Range(0, count).Select(_ => Unit(1m / count)).ToList();

            foreach (decimal total in new[] { 0.01m, 1m, 999.99m, 12_345.67m, 1_000_000m })
            {
                var shares = ApportionmentCalculator.Distribute(
                    total, units, ApportionmentMethod.IdealFraction);

                shares.Sum(s => s.Amount).ShouldBe(
                    total,
                    $"a soma deveria fechar com {total} dividido entre {count} unidades");
            }
        }
    }

    [Fact]
    public void Unidade_com_fracao_maior_paga_proporcionalmente_mais()
    {
        var big = Unit(0.60m);
        var small = Unit(0.40m);

        var shares = ApportionmentCalculator.Distribute(
            1000m, [big, small], ApportionmentMethod.IdealFraction);

        shares.Single(s => s.UnitId == big.UnitId).Amount.ShouldBe(600m);
        shares.Single(s => s.UnitId == small.UnitId).Amount.ShouldBe(400m);
    }

    [Fact]
    public void Metodo_igualitario_ignora_a_fracao_ideal()
    {
        var units = new[] { Unit(0.90m), Unit(0.10m) };

        var shares = ApportionmentCalculator.Distribute(1000m, units, ApportionmentMethod.Equal);

        shares.ShouldAllBe(s => s.Amount == 500m);
    }

    [Fact]
    public void Metodo_por_area_usa_a_area_privativa()
    {
        var large = Unit(0.5m, area: 90m);
        var smallUnit = Unit(0.5m, area: 60m);

        var shares = ApportionmentCalculator.Distribute(
            1500m, [large, smallUnit], ApportionmentMethod.Area);

        shares.Single(s => s.UnitId == large.UnitId).Amount.ShouldBe(900m);
        shares.Single(s => s.UnitId == smallUnit.UnitId).Amount.ShouldBe(600m);
    }

    [Fact]
    public void Cai_para_divisao_igualitaria_quando_nenhuma_fracao_foi_cadastrada()
    {
        // Condominio recem-cadastrado, sem fracao ideal preenchida: dividir
        // igual e melhor do que dividir por zero ou cobrar zero de todos.
        var units = Enumerable.Range(0, 4).Select(_ => Unit(0m)).ToList();

        var shares = ApportionmentCalculator.Distribute(400m, units, ApportionmentMethod.IdealFraction);

        shares.ShouldAllBe(s => s.Amount == 100m);
        shares.Sum(s => s.Amount).ShouldBe(400m);
    }

    [Fact]
    public void Distribuir_zero_devolve_zero_para_todos()
    {
        var units = Enumerable.Range(0, 5).Select(_ => Unit(0.2m)).ToList();

        var shares = ApportionmentCalculator.Distribute(0m, units, ApportionmentMethod.IdealFraction);

        shares.ShouldAllBe(s => s.Amount == 0m);
    }

    [Fact]
    public void Recusa_rateio_sem_unidades()
    {
        Should.Throw<DomainException>(() =>
            ApportionmentCalculator.Distribute(100m, [], ApportionmentMethod.IdealFraction));
    }

    [Fact]
    public void Recusa_valor_negativo()
    {
        Should.Throw<DomainException>(() =>
            ApportionmentCalculator.Distribute(-1m, [Unit(1m)], ApportionmentMethod.IdealFraction));
    }

    [Fact]
    public void Resultado_e_estavel_entre_execucoes()
    {
        // Reprocessar um rateio nao pode mudar quem recebeu o centavo da sobra.
        var units = Enumerable.Range(0, 7).Select(_ => Unit(1m / 7m)).ToList();

        var first = ApportionmentCalculator.Distribute(100m, units, ApportionmentMethod.IdealFraction);
        var second = ApportionmentCalculator.Distribute(100m, units, ApportionmentMethod.IdealFraction);

        first.OrderBy(s => s.UnitId).Select(s => s.Amount)
            .ShouldBe(second.OrderBy(s => s.UnitId).Select(s => s.Amount));
    }
}
