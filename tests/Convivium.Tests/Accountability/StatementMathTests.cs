namespace Convivium.Tests.Accountability;

using Convivium.Application.Accountability;

/// <summary>
/// O balancete é conferido em assembleia, de papel na mão. Se a soma por conta
/// não bater com o total, a discussão deixa de ser sobre o condomínio e passa
/// a ser sobre o sistema.
/// </summary>
public class StatementMathTests
{
    private static StatementEntry Lancamento(string codigo, string nome, decimal valor) =>
        new(new DateOnly(2026, 8, 15), $"Pagamento {nome}", codigo, nome,
            "Conta Corrente", null, IsIncome: false, valor, Reconciled: true);

    [Fact]
    public void Soma_os_lancamentos_da_mesma_conta()
    {
        var linhas = StatementMath.GroupByAccount(
            [
                Lancamento("5.2.01", "Energia Elétrica", 1_200m),
                Lancamento("5.2.01", "Energia Elétrica", 800m),
            ],
            total: 2_000m);

        linhas.Count.ShouldBe(1);
        linhas[0].Amount.ShouldBe(2_000m);
        linhas[0].Count.ShouldBe(2, "as duas contas de luz do mês viraram uma linha só");
    }

    [Fact]
    public void Ordena_da_maior_despesa_para_a_menor()
    {
        // A primeira pergunta numa prestação de contas é sempre "no que foi o
        // grosso do dinheiro". A ordem da tabela precisa responder sozinha.
        var linhas = StatementMath.GroupByAccount(
            [
                Lancamento("5.2.01", "Energia Elétrica", 500m),
                Lancamento("5.1.01", "Salários", 8_000m),
                Lancamento("5.3.01", "Elevadores", 1_000m),
            ],
            total: 9_500m);

        linhas.Select(l => l.Name).ShouldBe(["Salários", "Elevadores", "Energia Elétrica"]);
    }

    [Fact]
    public void Desempata_pelo_codigo_para_a_ordem_nao_mudar_de_mes_para_mes()
    {
        var linhas = StatementMath.GroupByAccount(
            [
                Lancamento("5.9.02", "Outra", 300m),
                Lancamento("5.9.01", "Uma", 300m),
            ],
            total: 600m);

        linhas.Select(l => l.Code).ShouldBe(["5.9.01", "5.9.02"]);
    }

    [Fact]
    public void A_fatia_e_sobre_o_total_do_proprio_lado()
    {
        var linhas = StatementMath.GroupByAccount(
            [
                Lancamento("5.1.01", "Salários", 7_500m),
                Lancamento("5.2.01", "Energia Elétrica", 2_500m),
            ],
            total: 10_000m);

        linhas[0].Share.ShouldBe(0.75m);
        linhas[1].Share.ShouldBe(0.25m);
    }

    [Fact]
    public void As_fatias_somam_o_inteiro()
    {
        // Três valores que não dividem redondo: se a soma das fatias escapasse
        // de 1, a coluna de porcentagem fecharia em 99% ou 101% no papel.
        decimal[] valores = [1_000m, 333.33m, 666.67m];
        decimal total = valores.Sum();

        var linhas = StatementMath.GroupByAccount(
            valores.Select((v, i) => Lancamento($"5.{i}", $"Conta {i}", v)),
            total);

        linhas.Sum(l => l.Share).ShouldBe(1m, tolerance: 0.0000001m);
    }

    [Fact]
    public void Mes_sem_movimento_de_um_lado_nao_estoura_a_divisao()
    {
        // Condomínio que não teve receita no mês não pode derrubar o balancete.
        var linhas = StatementMath.GroupByAccount([], total: 0m);

        linhas.ShouldBeEmpty();
    }

    [Fact]
    public void Total_zerado_com_lancamentos_devolve_fatia_zero()
    {
        // Não deveria acontecer, mas um total desencontrado dos lançamentos não
        // pode virar DivideByZeroException no meio da assembleia.
        var linhas = StatementMath.GroupByAccount([Lancamento("5.1", "Salários", 100m)], total: 0m);

        linhas[0].Share.ShouldBe(0m);
        linhas[0].Amount.ShouldBe(100m);
    }

    [Fact]
    public void Contas_diferentes_com_o_mesmo_nome_nao_se_misturam()
    {
        var linhas = StatementMath.GroupByAccount(
            [
                Lancamento("5.2.01", "Água e Esgoto", 400m),
                Lancamento("4.6", "Água e Esgoto", 100m),
            ],
            total: 500m);

        linhas.Count.ShouldBe(2, "o código da conta é o que separa as duas");
    }
}
