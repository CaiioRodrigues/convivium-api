namespace Convivium.Application.Units;

using Convivium.Application.Abstractions;
using Convivium.Domain.Billing;
using Convivium.Domain.Common;
using Convivium.Domain.Condominiums;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Cadastro de blocos e unidades.
/// </summary>
public sealed class UnitService(IApplicationDbContext db, IClock clock)
{
    /// <summary>
    /// Folga aceita na soma das frações. Com 8 casas decimais, arredondamentos
    /// legítimos cabem aqui; acima disso é erro de cadastro.
    /// </summary>
    private const decimal FractionTolerance = 0.0001m;

    public async Task<UnitListDto> ListAsync(
        bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        DateOnly hoje = clock.Today;

        var unidades = await db.Units
            .AsNoTracking()
            .Include(u => u.Block)
            .Include(u => u.Occupancies.Where(o => o.EndedOn == null || o.EndedOn >= hoje))
                .ThenInclude(o => o.Person)
            .Where(u => includeInactive || u.IsActive)
            .OrderBy(u => u.Block!.Name)
            .ThenBy(u => u.Identifier)
            .ToListAsync(cancellationToken);

        var idsDasUnidades = unidades.Select(u => u.Id).ToList();

        // Cobranças em aberto por unidade: é o que impede excluir sem perceber.
        var emAberto = await db.Charges
            .AsNoTracking()
            .Where(c => idsDasUnidades.Contains(c.UnitId))
            .Where(c => c.Status != ChargeStatus.Paid && c.Status != ChargeStatus.Cancelled)
            .GroupBy(c => c.UnitId)
            .Select(g => new
            {
                UnitId = g.Key,
                Quantidade = g.Count(),
                Saldo = g.Sum(c => c.TotalAmount - c.PaidAmount),
            })
            .ToDictionaryAsync(x => x.UnitId, cancellationToken);

        var blocos = await db.Blocks
            .AsNoTracking()
            .OrderBy(b => b.Name)
            .Select(b => new BlockDto(b.Id, b.Name, b.Units.Count))
            .ToListAsync(cancellationToken);

        var itens = unidades
            .Select(u =>
            {
                emAberto.TryGetValue(u.Id, out var pendencia);

                return new UnitDto(
                    u.Id,
                    u.BlockId,
                    u.Block?.Name,
                    u.Identifier,
                    u.FullIdentifier,
                    u.Floor,
                    u.Kind,
                    u.AreaM2,
                    u.IdealFraction,
                    u.IsActive,
                    u.Occupancies
                        .OrderByDescending(o => o.IsBillingResponsible)
                        .ThenBy(o => o.Relation)
                        .Select(o => new UnitOccupantDto(
                            o.Id, o.PersonId, o.Person.Name, o.Person.Email,
                            o.Relation, o.IsBillingResponsible))
                        .ToList(),
                    pendencia?.Quantidade ?? 0,
                    pendencia?.Saldo ?? 0m);
            })
            .ToList();

        decimal soma = itens.Where(u => u.IsActive).Sum(u => u.IdealFraction);

        return new UnitListDto(
            itens,
            blocos,
            soma,
            IsBalanced(soma),
            BuildWarnings(itens, soma));
    }

    private static bool IsBalanced(decimal soma) => Math.Abs(soma - 1m) <= FractionTolerance;

    private static List<string> BuildWarnings(List<UnitDto> unidades, decimal soma)
    {
        var avisos = new List<string>();
        var ativas = unidades.Where(u => u.IsActive).ToList();

        if (ativas.Count == 0)
        {
            avisos.Add("Nenhuma unidade ativa cadastrada. O rateio não tem como ser fechado.");
            return avisos;
        }

        if (!IsBalanced(soma))
        {
            string direcao = soma > 1m ? "acima" : "abaixo";

            avisos.Add(
                $"A soma das frações ideais é {soma:N8}, {direcao} de 1. " +
                "Enquanto não fechar, o rateio cobra a mais ou a menos de todas as unidades.");
        }

        int semFracao = ativas.Count(u => u.IdealFraction <= 0);
        if (semFracao > 0)
        {
            avisos.Add($"{semFracao} unidade(s) ativas estão sem fração ideal definida.");
        }

        int semArea = ativas.Count(u => u.AreaM2 is null or <= 0);
        if (semArea > 0 && semArea < ativas.Count)
        {
            avisos.Add(
                $"{semArea} unidade(s) estão sem área privativa. " +
                "Sem ela não dá para recalcular as frações pela área.");
        }

        int semResponsavel = ativas.Count(u => u.Occupants.Count == 0);
        if (semResponsavel > 0)
        {
            avisos.Add(
                $"{semResponsavel} unidade(s) ativas não têm morador vinculado. " +
                "A cobrança delas sai sem destinatário de e-mail.");
        }

        return avisos;
    }

    public async Task<UnitDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        UnitListDto lista = await ListAsync(includeInactive: true, cancellationToken);

        return lista.Units.FirstOrDefault(u => u.Id == id)
            ?? throw new KeyNotFoundException("Unidade não encontrada.");
    }

    public async Task<UnitDto> CreateAsync(
        SaveUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string identificador = NormalizeIdentifier(request.Identifier);
        Guid? blocoId = await ResolveBlockAsync(request, cancellationToken);

        bool duplicada = await db.Units.AnyAsync(
            u => u.BlockId == blocoId && u.Identifier == identificador, cancellationToken);

        DomainException.ThrowIf(
            duplicada,
            $"Já existe a unidade {identificador} neste bloco.");

        var unidade = new Unit
        {
            Identifier = identificador,
            BlockId = blocoId,
            Floor = request.Floor,
            Kind = request.Kind,
            AreaM2 = ValidateArea(request.AreaM2),
            IdealFraction = ValidateFraction(request.IdealFraction) ?? 0m,
            IsActive = request.IsActive,
        };

        db.Units.Add(unidade);
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(unidade.Id, cancellationToken);
    }

    public async Task<UnitDto> UpdateAsync(
        Guid id,
        SaveUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Unit unidade = await db.Units.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Unidade não encontrada.");

        string identificador = NormalizeIdentifier(request.Identifier);
        Guid? blocoId = await ResolveBlockAsync(request, cancellationToken);

        bool duplicada = await db.Units.AnyAsync(
            u => u.Id != id && u.BlockId == blocoId && u.Identifier == identificador,
            cancellationToken);

        DomainException.ThrowIf(duplicada, $"Já existe a unidade {identificador} neste bloco.");

        unidade.Identifier = identificador;
        unidade.BlockId = blocoId;
        unidade.Floor = request.Floor;
        unidade.Kind = request.Kind;
        unidade.AreaM2 = ValidateArea(request.AreaM2);
        unidade.IsActive = request.IsActive;

        if (request.IdealFraction is { } fracao)
        {
            unidade.IdealFraction = ValidateFraction(fracao) ?? 0m;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(unidade.Id, cancellationToken);
    }

    /// <summary>
    /// Desativa a unidade. Ela some do rateio e continua no histórico.
    /// </summary>
    public async Task<UnitDto> DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Unit unidade = await db.Units.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Unidade não encontrada.");

        bool temPendencia = await db.Charges.AnyAsync(
            c => c.UnitId == id && c.Status != ChargeStatus.Paid && c.Status != ChargeStatus.Cancelled,
            cancellationToken);

        DomainException.ThrowIf(
            temPendencia,
            "Esta unidade tem cobrança em aberto. Receba ou cancele antes de desativar.");

        unidade.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(unidade.Id, cancellationToken);
    }

    /// <summary>
    /// Exclui a unidade de vez. Só funciona enquanto ela nunca foi cobrada:
    /// apagar uma unidade com histórico deixaria lançamentos órfãos na
    /// prestação de contas dos anos anteriores.
    /// </summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Unit unidade = await db.Units.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Unidade não encontrada.");

        bool jaFoiCobrada = await db.Charges.AnyAsync(c => c.UnitId == id, cancellationToken);

        DomainException.ThrowIf(
            jaFoiCobrada,
            "Esta unidade já tem cobranças no histórico e não pode ser excluída. " +
            "Desative em vez de excluir.");

        db.Units.Remove(unidade);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Recalcula a fração ideal de todas as unidades ativas proporcionalmente
    /// à área privativa.
    /// </summary>
    /// <remarks>
    /// A última unidade absorve a diferença de arredondamento, de modo que a
    /// soma feche exatamente em 1 — que é o que a convenção de condomínio
    /// exige e o que o rateio pressupõe.
    /// </remarks>
    public async Task<RedistributeResult> RedistributeByAreaAsync(
        CancellationToken cancellationToken = default)
    {
        var ativas = await db.Units
            .Where(u => u.IsActive)
            .OrderBy(u => u.Identifier)
            .ToListAsync(cancellationToken);

        DomainException.ThrowIf(ativas.Count == 0, "Não há unidades ativas para recalcular.");

        var semArea = ativas.Where(u => u.AreaM2 is null or <= 0).ToList();

        DomainException.ThrowIf(
            semArea.Count > 0,
            $"{semArea.Count} unidade(s) estão sem área privativa: " +
            $"{string.Join(", ", semArea.Take(5).Select(u => u.Identifier))}" +
            (semArea.Count > 5 ? "…" : "") +
            ". Preencha a área antes de recalcular.");

        decimal areaTotal = ativas.Sum(u => u.AreaM2!.Value);
        decimal acumulado = 0m;

        for (int i = 0; i < ativas.Count; i++)
        {
            if (i == ativas.Count - 1)
            {
                ativas[i].IdealFraction = 1m - acumulado;
                break;
            }

            decimal fracao = Math.Round(
                ativas[i].AreaM2!.Value / areaTotal, 8, MidpointRounding.ToZero);

            ativas[i].IdealFraction = fracao;
            acumulado += fracao;
        }

        await db.SaveChangesAsync(cancellationToken);

        UnitListDto lista = await ListAsync(includeInactive: true, cancellationToken);

        return new RedistributeResult(ativas.Count, lista.IdealFractionSum, lista.Units);
    }

    /// <summary>
    /// Ajusta as fracoes ja cadastradas para somarem exatamente 1, mantendo a
    /// proporcao entre elas.
    /// </summary>
    /// <remarks>
    /// Resolve dois casos que aparecem sempre. O primeiro e a convencao que
    /// traz as fracoes arredondadas e nao fecha em 1 por alguns decimos. O
    /// segundo e a digitacao em unidade errada — porcentagem no lugar de
    /// fracao, ou o contrario — que deixa a soma cem vezes maior ou menor sem
    /// alterar a proporcao entre as unidades.
    ///
    /// Nos dois casos o que cada unidade paga em relacao as outras e o que a
    /// convencao mandou; falta so a escala. Diferente do recalculo pela area,
    /// aqui nenhuma proporcao e inventada: as fracoes da convencao mandam, e a
    /// operacao e recusada se alguma unidade ativa estiver sem fracao.
    /// </remarks>
    public async Task<RedistributeResult> NormalizeFractionsAsync(
        CancellationToken cancellationToken = default)
    {
        var ativas = await db.Units
            .Where(u => u.IsActive)
            .OrderBy(u => u.Identifier)
            .ToListAsync(cancellationToken);

        DomainException.ThrowIf(ativas.Count == 0, "Não há unidades ativas para ajustar.");

        var semFracao = ativas.Where(u => u.IdealFraction <= 0).ToList();

        DomainException.ThrowIf(
            semFracao.Count > 0,
            $"{semFracao.Count} unidade(s) estão sem fração ideal: " +
            $"{string.Join(", ", semFracao.Take(5).Select(u => u.Identifier))}" +
            (semFracao.Count > 5 ? "…" : "") +
            ". Preencha a fração de todas antes de ajustar.");

        decimal somaAtual = ativas.Sum(u => u.IdealFraction);
        decimal acumulado = 0m;

        // A ultima absorve a diferenca de arredondamento, como no recalculo
        // pela area, para a soma fechar exatamente em 1.
        for (int i = 0; i < ativas.Count; i++)
        {
            if (i == ativas.Count - 1)
            {
                ativas[i].IdealFraction = 1m - acumulado;
                break;
            }

            decimal fracao = Math.Round(
                ativas[i].IdealFraction / somaAtual, 8, MidpointRounding.ToZero);

            ativas[i].IdealFraction = fracao;
            acumulado += fracao;
        }

        await db.SaveChangesAsync(cancellationToken);

        UnitListDto lista = await ListAsync(includeInactive: true, cancellationToken);

        return new RedistributeResult(ativas.Count, lista.IdealFractionSum, lista.Units);
    }

    // --- Blocos ---

    public async Task<BlockDto> CreateBlockAsync(
        SaveBlockRequest request,
        CancellationToken cancellationToken = default)
    {
        string nome = (request.Name ?? string.Empty).Trim();
        DomainException.ThrowIf(nome.Length == 0, "Informe o nome do bloco.");

        bool duplicado = await db.Blocks.AnyAsync(b => b.Name == nome, cancellationToken);
        DomainException.ThrowIf(duplicado, $"Já existe um bloco chamado '{nome}'.");

        var bloco = new Block { Name = nome };
        db.Blocks.Add(bloco);
        await db.SaveChangesAsync(cancellationToken);

        return new BlockDto(bloco.Id, bloco.Name, 0);
    }

    public async Task DeleteBlockAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Block bloco = await db.Blocks.FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Bloco não encontrado.");

        bool temUnidades = await db.Units.AnyAsync(u => u.BlockId == id, cancellationToken);

        DomainException.ThrowIf(
            temUnidades,
            "Este bloco ainda tem unidades. Mova ou exclua as unidades antes.");

        db.Blocks.Remove(bloco);
        await db.SaveChangesAsync(cancellationToken);
    }

    // --- Apoio ---

    private async Task<Guid?> ResolveBlockAsync(
        SaveUnitRequest request,
        CancellationToken cancellationToken)
    {
        if (request.BlockId is { } id)
        {
            bool existe = await db.Blocks.AnyAsync(b => b.Id == id, cancellationToken);
            DomainException.ThrowIf(!existe, "Bloco não encontrado.");
            return id;
        }

        if (string.IsNullOrWhiteSpace(request.NewBlockName))
        {
            return null;
        }

        // Cria o bloco na hora: cadastrar unidade e bloco em dois passos
        // separados é atrito sem ganho nenhum.
        string nome = request.NewBlockName.Trim();

        Guid? existente = await db.Blocks
            .Where(b => b.Name == nome)
            .Select(b => (Guid?)b.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existente is not null)
        {
            return existente;
        }

        var bloco = new Block { Name = nome };
        db.Blocks.Add(bloco);

        return bloco.Id;
    }

    private static string NormalizeIdentifier(string? identifier)
    {
        string valor = (identifier ?? string.Empty).Trim();
        DomainException.ThrowIf(valor.Length == 0, "Informe a identificação da unidade.");

        return valor;
    }

    private static decimal? ValidateArea(decimal? area)
    {
        if (area is null)
        {
            return null;
        }

        DomainException.ThrowIf(area <= 0, "A área privativa deve ser maior que zero.");
        DomainException.ThrowIf(area > 100_000, "Área privativa fora de qualquer escala plausível.");

        return area;
    }

    private static decimal? ValidateFraction(decimal? fraction)
    {
        if (fraction is null)
        {
            return null;
        }

        DomainException.ThrowIf(fraction < 0, "A fração ideal não pode ser negativa.");

        DomainException.ThrowIf(
            fraction > 1,
            "A fração ideal é uma parcela de 1. Para 1,25%, informe 0,0125.");

        return fraction;
    }
}
