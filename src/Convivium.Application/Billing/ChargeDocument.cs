namespace Convivium.Application.Billing;

/// <summary>
/// Tudo que o boleto em PDF precisa mostrar, ja resolvido.
/// </summary>
/// <remarks>
/// O gerador de PDF recebe este modelo pronto e nao consulta o banco:
/// assim ele fica testavel sem infraestrutura e a regra de calculo de
/// multa e juros mora num lugar so.
/// </remarks>
public sealed record ChargeDocument
{
    public required string CondominiumName { get; init; }

    public string? CondominiumCnpj { get; init; }

    public required string CondominiumAddress { get; init; }

    public required string UnitIdentifier { get; init; }

    public string? PayerName { get; init; }

    public required string Competence { get; init; }

    public required DateOnly DueDate { get; init; }

    public required decimal TotalAmount { get; init; }

    public decimal PaidAmount { get; init; }

    public decimal LateFee { get; init; }

    public decimal Interest { get; init; }

    /// <summary>Valor a pagar hoje, ja com multa e juros.</summary>
    public required decimal TotalDue { get; init; }

    public int DaysLate { get; init; }

    public required IReadOnlyList<ChargeItemDto> Items { get; init; }

    /// <summary>Copia-e-cola do PIX. Nulo quando o condominio nao tem chave cadastrada.</summary>
    public string? PixPayload { get; init; }

    /// <summary>Link publico para o morador abrir a cobranca no navegador.</summary>
    public string? PublicUrl { get; init; }

    /// <summary>Observacao do ciclo, ex.: "inclui rateio da pintura da fachada".</summary>
    public string? Notes { get; init; }

    public bool IsPaid { get; init; }

    public bool IsOverdue => DaysLate > 0 && !IsPaid;
}

/// <summary>Gera o boleto de cobranca, em PDF ou em imagem.</summary>
public interface IChargeDocumentRenderer
{
    /// <summary>PDF, para arquivar, imprimir e anexar no e-mail.</summary>
    byte[] Render(ChargeDocument document);

    /// <summary>
    /// PNG da primeira pagina, para mandar no WhatsApp.
    /// </summary>
    /// <remarks>
    /// Existe por causa de como o WhatsApp mostra cada coisa: PDF chega como
    /// cartao de documento, que o morador precisa tocar para abrir, e imagem
    /// aparece aberta na conversa. Com o QR Code do PIX visivel sem abrir
    /// nada, o boleto e pago no mesmo minuto em que chega.
    /// </remarks>
    byte[] RenderImage(ChargeDocument document);
}
