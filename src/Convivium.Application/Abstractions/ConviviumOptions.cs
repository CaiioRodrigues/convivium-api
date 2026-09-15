namespace Convivium.Application.Abstractions;

/// <summary>Configuracao geral da aplicacao, lida da secao "App".</summary>
public sealed class ConviviumOptions
{
    public const string SectionName = "App";

    /// <summary>
    /// Endereco publico do convivium-web. E a base do link que vai no e-mail
    /// para o morador abrir o boleto sem precisar de login.
    /// </summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:3000";

    /// <summary>Caminho da rota do boleto publico no front.</summary>
    public string PublicChargePath { get; set; } = "/boleto";

    /// <summary>
    /// E-mail de quem administra a plataforma e pode criar condominios.
    /// </summary>
    /// <remarks>
    /// Conferido a cada subida: se a pessoa nao existir, e criada sem senha e
    /// com um convite de primeiro acesso cujo link sai no console. Trocar este
    /// valor promove outra pessoa; nao rebaixa a anterior.
    /// </remarks>
    public string? SuperAdminEmail { get; set; }

    public string BuildChargeUrl(string publicToken) =>
        $"{PublicBaseUrl.TrimEnd('/')}{PublicChargePath}/{publicToken}";
}
