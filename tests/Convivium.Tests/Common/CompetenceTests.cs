namespace Convivium.Tests.Common;

using Convivium.Domain.Common;

public class CompetenceTests
{
    [Theory]
    [InlineData("09/2026", 2026, 9)]
    [InlineData("9/2026", 2026, 9)]
    [InlineData("2026-09", 2026, 9)]
    [InlineData("202609", 2026, 9)]
    [InlineData("12/2026", 2026, 12)]
    public void Aceita_os_formatos_usados_no_brasil(string input, int year, int month)
    {
        Competence competence = Competence.Parse(input);

        competence.Year.ShouldBe(year);
        competence.Month.ShouldBe(month);
    }

    [Theory]
    [InlineData("13/2026")]
    [InlineData("00/2026")]
    [InlineData("setembro")]
    [InlineData("")]
    public void Recusa_entrada_invalida(string input)
    {
        Competence.TryParse(input, out _).ShouldBeFalse();
    }

    [Fact]
    public void Persiste_como_inteiro_AAAAMM()
    {
        // O formato inteiro e o que permite ordenar e comparar no banco
        // com um indice comum.
        new Competence(2026, 9).ToInt().ShouldBe(202609);
        Competence.FromInt(202609).ShouldBe(new Competence(2026, 9));
    }

    [Fact]
    public void Ordena_cronologicamente()
    {
        var dezembro = new Competence(2026, 12);
        var janeiro = new Competence(2027, 1);

        (dezembro < janeiro).ShouldBeTrue();
        (janeiro > dezembro).ShouldBeTrue();
    }

    [Fact]
    public void Avanca_e_retrocede_atravessando_o_ano()
    {
        new Competence(2026, 12).Next().ShouldBe(new Competence(2027, 1));
        new Competence(2026, 1).Previous().ShouldBe(new Competence(2025, 12));
        new Competence(2026, 9).AddMonths(-14).ShouldBe(new Competence(2025, 7));
    }

    [Fact]
    public void Conhece_o_primeiro_e_o_ultimo_dia_do_mes()
    {
        var fevereiro = new Competence(2028, 2);

        fevereiro.FirstDay.ShouldBe(new DateOnly(2028, 2, 1));

        // 2028 e bissexto.
        fevereiro.LastDay.ShouldBe(new DateOnly(2028, 2, 29));
    }

    [Fact]
    public void Formata_como_o_brasileiro_escreve()
    {
        new Competence(2026, 9).ToString().ShouldBe("09/2026");
    }

    [Fact]
    public void Recusa_mes_fora_do_intervalo()
    {
        Should.Throw<DomainException>(() => new Competence(2026, 13));
        Should.Throw<DomainException>(() => new Competence(2026, 0));
    }
}
