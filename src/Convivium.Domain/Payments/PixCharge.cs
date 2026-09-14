namespace Convivium.Domain.Payments;

/// <summary>Dados necessarios para montar um QR Code PIX de cobranca.</summary>
public sealed record PixCharge
{
    /// <summary>Chave PIX do recebedor (CPF, CNPJ, e-mail, telefone ou chave aleatoria).</summary>
    public required string Key { get; init; }

    /// <summary>Nome do beneficiario. Sera normalizado para ASCII e truncado em 25 caracteres.</summary>
    public required string ReceiverName { get; init; }

    /// <summary>Cidade do beneficiario. Normalizada para ASCII e truncada em 15 caracteres.</summary>
    public required string ReceiverCity { get; init; }

    /// <summary>Valor da cobranca. Nulo deixa o pagador digitar o valor.</summary>
    public decimal? Amount { get; init; }

    /// <summary>
    /// Identificador da transacao (txid). Ate 25 caracteres alfanumericos —
    /// e por ele que a conciliacao liga o PIX recebido a cobranca.
    /// </summary>
    public string? TransactionId { get; init; }

    /// <summary>Texto curto que aparece para o pagador. Ex.: "Taxa cond. 09/2026 - Apto 101".</summary>
    public string? Description { get; init; }
}
