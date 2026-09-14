namespace Convivium.Api.Controllers;

using Convivium.Api.Auth;
using Convivium.Application.Dashboard;
using Convivium.Domain.Common;
using Convivium.Domain.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Agregacoes prontas para os graficos e o painel do sindico.</summary>
[Route("api/painel")]
[Authorize(Policy = ConviviumPolicies.Council)]
public sealed class DashboardController(DashboardService dashboard) : ApiControllerBase
{
    /// <summary>Visao geral: caixa, resultado do mes, contas a pagar e receber e inadimplencia.</summary>
    [HttpGet]
    [ProducesResponseType<DashboardSummary>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardSummary>> Summary(CancellationToken cancellationToken)
        => Ok(await dashboard.GetSummaryAsync(cancellationToken));

    /// <summary>
    /// Gasto por categoria, para o grafico de pizza.
    /// </summary>
    /// <param name="competence">Competencia final, no formato MM/AAAA. Padrao: mes corrente.</param>
    /// <param name="months">Quantos meses somar, terminando na competencia. Padrao: 1.</param>
    /// <param name="detailed">Quebra na conta analitica em vez do grupo.</param>
    [HttpGet("gastos-por-categoria")]
    [ProducesResponseType<IReadOnlyList<CategorySlice>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategorySlice>>> ExpensesByCategory(
        [FromQuery] string? competence,
        [FromQuery] int months = 1,
        [FromQuery] bool detailed = false,
        CancellationToken cancellationToken = default)
    {
        Competence? parsed = competence is { Length: > 0 } text ? Competence.Parse(text) : null;
        return Ok(await dashboard.GetExpensesByCategoryAsync(parsed, months, detailed, cancellationToken));
    }

    /// <summary>Receita, despesa e saldo mes a mes, para o grafico de linha.</summary>
    [HttpGet("evolucao-mensal")]
    [ProducesResponseType<IReadOnlyList<MonthlyPoint>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MonthlyPoint>>> MonthlySeries(
        [FromQuery] int months = 12,
        CancellationToken cancellationToken = default)
        => Ok(await dashboard.GetMonthlySeriesAsync(months, cancellationToken));

    /// <summary>Ranking de gasto por fornecedor no periodo.</summary>
    [HttpGet("fornecedores")]
    [ProducesResponseType<IReadOnlyList<SupplierSpending>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SupplierSpending>>> TopSuppliers(
        [FromQuery] int months = 6,
        [FromQuery] int take = 10,
        CancellationToken cancellationToken = default)
        => Ok(await dashboard.GetTopSuppliersAsync(months, take, cancellationToken));

    /// <summary>
    /// Consumo de uma concessionaria mes a mes, montado das faturas lidas em
    /// PDF. Um salto no consumo de agua costuma ser vazamento.
    /// </summary>
    [HttpGet("consumo/{provider}")]
    [ProducesResponseType<ConsumptionSeries>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConsumptionSeries>> Consumption(
        UtilityProvider provider,
        [FromQuery] int months = 12,
        CancellationToken cancellationToken = default)
        => Ok(await dashboard.GetConsumptionAsync(provider, months, cancellationToken));
}
