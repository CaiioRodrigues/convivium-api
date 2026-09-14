namespace Convivium.Domain.People;

public enum OccupancyRelation
{
    /// <summary>Proprietario. Responde pelo debito condominial perante o condominio.</summary>
    Owner = 1,

    /// <summary>Inquilino. Pode ser o responsavel pela cobranca conforme o contrato de locacao.</summary>
    Tenant = 2,

    /// <summary>Morador sem titularidade (familiar, dependente).</summary>
    Occupant = 3,
}
