namespace Convivium.Domain.People;

/// <summary>
/// Papel de uma pessoa dentro de um condominio.
/// Os valores sao crescentes em poder, entao comparacoes do tipo
/// <c>role &gt;= MembershipRole.CouncilMember</c> funcionam como hierarquia.
/// </summary>
public enum MembershipRole
{
    /// <summary>Morador ou proprietario: ve apenas as proprias cobrancas e os documentos publicos.</summary>
    Resident = 1,

    /// <summary>Zelador: lanca despesas e anexa notas, sem acesso ao caixa consolidado.</summary>
    Caretaker = 2,

    /// <summary>Conselho fiscal: leitura completa da prestacao de contas.</summary>
    CouncilMember = 3,

    /// <summary>Subsindico: mesmas permissoes do sindico, exceto encerrar exercicio.</summary>
    AssistantManager = 4,

    /// <summary>Sindico: controle total do condominio.</summary>
    Manager = 5,

    /// <summary>Administradora contratada: controle total, normalmente sobre varios condominios.</summary>
    Administrator = 6,
}
