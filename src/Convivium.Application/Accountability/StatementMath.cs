namespace Convivium.Application.Accountability;

/// <summary>
/// As contas do balancete, separadas do acesso ao banco.
/// </summary>
/// <remarks>
/// Fica sozinho aqui porque e a parte que alguem confere na assembleia de
/// papel na mao. Misturada com a consulta, so daria para verificar subindo
/// banco; separada, cada regra tem um teste.
/// </remarks>
public static class StatementMath
{
    /// <summary>
    /// Junta os lancamentos por conta do plano de contas, do maior para o menor.
    /// </summary>
    /// <param name="entries">Lancamentos de um mesmo lado: ou receitas, ou despesas.</param>
    /// <param name="total">
    /// Total do proprio lado. A fatia e sobre ele, e nao sobre o movimento
    /// inteiro: sobre o movimento ela nao responderia a pergunta que se faz
    /// olhando um balancete, que e "quanto do que gastamos foi nisto".
    /// </param>
    public static IReadOnlyList<StatementLine> GroupByAccount(
        IEnumerable<StatementEntry> entries,
        decimal total)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries
            .GroupBy(e => new { e.AccountCode, e.AccountName })
            .Select(g =>
            {
                decimal amount = g.Sum(e => e.Amount);

                // Total zero nao e erro: e mes sem movimento daquele lado. A
                // fatia vira zero em vez de estourar a divisao.
                return new StatementLine(
                    g.Key.AccountCode,
                    g.Key.AccountName,
                    amount,
                    g.Count(),
                    total > 0 ? amount / total : 0m);
            })
            // Maior primeiro: numa prestacao de contas, a primeira pergunta e
            // sempre "no que foi o grosso do dinheiro". Empate desempata pelo
            // codigo, para a ordem nao mudar de um mes para o outro.
            .OrderByDescending(l => l.Amount)
            .ThenBy(l => l.Code, StringComparer.Ordinal)
            .ToList();
    }
}
