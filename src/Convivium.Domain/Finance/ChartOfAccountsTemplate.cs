namespace Convivium.Domain.Finance;

/// <summary>Uma linha do plano de contas padrao.</summary>
public sealed record ChartOfAccountsEntry(
    string Code,
    string Name,
    AccountNature Nature,
    bool IsGroup = false,
    bool IsApportionable = true);

/// <summary>
/// Plano de contas padrao de condominio residencial brasileiro.
/// </summary>
/// <remarks>
/// Todo condominio novo nasce com esta estrutura e pode ajustar depois.
/// A codificacao segue a pratica contabil: grupo 4 para receitas, 5 para
/// despesas, com subgrupos que sao exatamente o eixo dos graficos de gasto.
/// </remarks>
public static class ChartOfAccountsTemplate
{
    public static IReadOnlyList<ChartOfAccountsEntry> Default { get; } =
    [
        // --- Receitas ---
        new("4", "Receitas", AccountNature.Revenue, IsGroup: true, IsApportionable: false),
        new("4.1", "Taxa Condominial", AccountNature.Revenue, IsApportionable: false),
        new("4.2", "Fundo de Reserva", AccountNature.Revenue, IsApportionable: false),
        new("4.3", "Multas e Juros", AccountNature.Revenue, IsApportionable: false),
        new("4.4", "Aluguel de Áreas Comuns", AccountNature.Revenue, IsApportionable: false),
        new("4.5", "Receitas Financeiras", AccountNature.Revenue, IsApportionable: false),
        new("4.9", "Outras Receitas", AccountNature.Revenue, IsApportionable: false),

        // --- Despesas de pessoal ---
        new("5", "Despesas", AccountNature.Expense, IsGroup: true),
        new("5.1", "Pessoal", AccountNature.Expense, IsGroup: true),
        new("5.1.01", "Salários", AccountNature.Expense),
        new("5.1.02", "Encargos Sociais", AccountNature.Expense),
        new("5.1.03", "Benefícios", AccountNature.Expense),
        new("5.1.04", "Férias e Rescisões", AccountNature.Expense),

        // --- Concessionarias: e aqui que caem as faturas lidas de PDF ---
        new("5.2", "Concessionárias", AccountNature.Expense, IsGroup: true),
        new("5.2.01", "Energia Elétrica", AccountNature.Expense),
        new("5.2.02", "Água e Esgoto", AccountNature.Expense),
        new("5.2.03", "Gás", AccountNature.Expense),
        new("5.2.04", "Telefone e Internet", AccountNature.Expense),

        // --- Manutencao ---
        new("5.3", "Manutenção e Conservação", AccountNature.Expense, IsGroup: true),
        new("5.3.01", "Elevadores", AccountNature.Expense),
        new("5.3.02", "Portão, Interfone e CFTV", AccountNature.Expense),
        new("5.3.03", "Jardinagem", AccountNature.Expense),
        new("5.3.04", "Piscina", AccountNature.Expense),
        new("5.3.05", "Hidráulica e Elétrica", AccountNature.Expense),
        new("5.3.06", "Dedetização e Limpeza de Caixa d'Água", AccountNature.Expense),

        // --- Terceirizados ---
        new("5.4", "Serviços Terceirizados", AccountNature.Expense, IsGroup: true),
        new("5.4.01", "Portaria e Segurança", AccountNature.Expense),
        new("5.4.02", "Limpeza e Conservação", AccountNature.Expense),
        new("5.4.03", "Administradora", AccountNature.Expense),
        new("5.4.04", "Contabilidade e Jurídico", AccountNature.Expense),

        // --- Demais despesas ---
        new("5.5", "Material de Consumo", AccountNature.Expense),
        new("5.6", "Seguros e Tributos", AccountNature.Expense, IsGroup: true),
        new("5.6.01", "Seguro Predial", AccountNature.Expense),
        new("5.6.02", "Taxas e Tributos", AccountNature.Expense),
        new("5.7", "Despesas Administrativas", AccountNature.Expense),

        // Obras nao entram no rateio ordinario: saem do fundo de reserva ou de
        // uma cota extraordinaria aprovada em assembleia.
        new("5.8", "Obras e Benfeitorias", AccountNature.Expense, IsApportionable: false),
    ];

    /// <summary>
    /// Materializa o plano para um condominio, ja resolvendo a hierarquia
    /// pelo prefixo do codigo ("5.2.01" tem "5.2" como pai).
    /// </summary>
    public static List<LedgerAccount> BuildFor(Guid condominiumId)
    {
        var byCode = new Dictionary<string, LedgerAccount>(StringComparer.Ordinal);
        var accounts = new List<LedgerAccount>(Default.Count);

        foreach (ChartOfAccountsEntry entry in Default)
        {
            var account = new LedgerAccount
            {
                CondominiumId = condominiumId,
                Code = entry.Code,
                Name = entry.Name,
                Nature = entry.Nature,
                IsGroup = entry.IsGroup,
                IsApportionable = entry.IsApportionable,
            };

            int lastDot = entry.Code.LastIndexOf('.');
            if (lastDot > 0 && byCode.TryGetValue(entry.Code[..lastDot], out LedgerAccount? parent))
            {
                account.ParentId = parent.Id;
                account.Parent = parent;
            }

            byCode[entry.Code] = account;
            accounts.Add(account);
        }

        return accounts;
    }
}
