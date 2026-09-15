namespace Convivium.Tests.Notifications;

using Convivium.Application.Notifications;

/// <summary>
/// O e-mail de redefinição é o único ponto do sistema onde um link vale uma
/// senha. Se ele sair sem o link, ninguém entra; se sair com o nome da pessoa
/// cru, quem cadastrou o nome escreve HTML na caixa de entrada alheia.
/// </summary>
public class EmailComposerTests
{
    private const string Link = "https://portal.exemplo.br/definir-senha/abc-123";

    private static readonly EmailComposer Composer = new();

    [Fact]
    public void Redefinicao_leva_o_link_no_html_e_no_texto()
    {
        EmailContent email = Composer.ComposePasswordReset(
            "Residencial Camila", "Ana", Link, TimeSpan.FromHours(1));

        // Nos dois formatos: cliente que bloqueia HTML mostra o texto puro,
        // e é por ele que a pessoa vai copiar o endereço na mão.
        email.HtmlBody.ShouldContain(Link);
        email.TextBody.ShouldContain(Link);
    }

    [Fact]
    public void Redefinicao_diz_o_prazo_e_que_o_link_e_de_uso_unico()
    {
        EmailContent email = Composer.ComposePasswordReset(
            "Residencial Camila", "Ana", Link, TimeSpan.FromHours(1));

        email.HtmlBody.ShouldContain("1 hora");
        email.HtmlBody.ShouldContain("uma vez");
    }

    [Theory]
    [InlineData(1, "1 hora")]
    [InlineData(2, "2 horas")]
    [InlineData(24, "1 dia")]
    [InlineData(168, "7 dias")]
    public void Prazo_sai_escrito_como_se_fala(int horas, string esperado)
    {
        EmailContent email = Composer.ComposePasswordReset(
            "Residencial Camila", "Ana", Link, TimeSpan.FromHours(horas));

        email.TextBody.ShouldContain(esperado);
    }

    [Fact]
    public void Redefinicao_explica_o_que_fazer_quem_nao_pediu()
    {
        // Quem recebe isto sem ter pedido precisa saber que a senha atual
        // continua valendo — senão troca a senha por susto, ou pior, acha
        // que a conta foi invadida.
        EmailContent email = Composer.ComposePasswordReset(
            "Residencial Camila", "Ana", Link, TimeSpan.FromHours(1));

        email.HtmlBody.ShouldContain("não pediu");
        email.HtmlBody.ShouldContain("continua valendo");
    }

    [Fact]
    public void Nome_da_pessoa_e_escapado_antes_de_entrar_no_html()
    {
        EmailContent email = Composer.ComposePasswordReset(
            "Residencial Camila",
            "<script>alert(1)</script>",
            Link,
            TimeSpan.FromHours(1));

        email.HtmlBody.ShouldNotContain("<script>");
        email.HtmlBody.ShouldContain("&lt;script&gt;");
    }

    [Fact]
    public void Nome_do_condominio_e_escapado_no_corpo_e_no_assunto()
    {
        EmailContent email = Composer.ComposePasswordReset(
            "Ed. <b>Central</b>", "Ana", Link, TimeSpan.FromHours(1));

        email.HtmlBody.ShouldNotContain("<b>Central</b>");
        email.HtmlBody.ShouldContain("&lt;b&gt;Central&lt;/b&gt;");
    }

    [Fact]
    public void Convite_e_redefinicao_nao_se_confundem_no_assunto()
    {
        // Os dois caem na mesma caixa de entrada e levam a mesma tela. O
        // assunto é o que diz à pessoa qual dos dois ela pediu.
        EmailContent convite = Composer.ComposeWelcome("Residencial Camila", "Ana", Link);
        EmailContent redefinicao = Composer.ComposePasswordReset(
            "Residencial Camila", "Ana", Link, TimeSpan.FromHours(1));

        redefinicao.Subject.ShouldNotBe(convite.Subject);
        redefinicao.Subject.ShouldContain("Redefinir");
    }
}
