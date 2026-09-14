namespace Convivium.Tests.Common;

using Convivium.Domain.Common;

/// <summary>
/// Conferir o dígito verificador pega o erro de digitação no cadastro, que é
/// onde ele custa barato. Depois vira chave PIX inválida.
/// </summary>
public class BrazilianDocumentTests
{
    [Theory]
    [InlineData("11144477735")]
    [InlineData("111.444.777-35")]
    [InlineData("52998224725")]
    public void Aceita_cpf_valido_com_ou_sem_mascara(string cpf)
    {
        BrazilianDocument.IsValidCpf(cpf).ShouldBeTrue();
    }

    [Theory]
    [InlineData("11144477736", "dígito verificador errado")]
    [InlineData("1114447773", "dígitos de menos")]
    [InlineData("111444777350", "dígitos de mais")]
    [InlineData("", "vazio")]
    [InlineData(null, "nulo")]
    public void Recusa_cpf_invalido(string? cpf, string motivo)
    {
        BrazilianDocument.IsValidCpf(cpf).ShouldBeFalse(motivo);
    }

    [Theory]
    [InlineData("11111111111")]
    [InlineData("00000000000")]
    [InlineData("99999999999")]
    public void Recusa_cpf_de_digitos_repetidos(string cpf)
    {
        // Passam na conta do dígito verificador, mas são o resultado típico
        // de um campo preenchido de qualquer jeito.
        BrazilianDocument.IsValidCpf(cpf).ShouldBeFalse();
    }

    [Theory]
    [InlineData("12345678000195")]
    [InlineData("12.345.678/0001-95")]
    [InlineData("06981180000116")]
    public void Aceita_cnpj_valido_com_ou_sem_mascara(string cnpj)
    {
        BrazilianDocument.IsValidCnpj(cnpj).ShouldBeTrue();
    }

    [Theory]
    [InlineData("12345678000199")]
    [InlineData("1234567800019")]
    [InlineData("11111111111111")]
    public void Recusa_cnpj_invalido(string cnpj)
    {
        BrazilianDocument.IsValidCnpj(cnpj).ShouldBeFalse();
    }

    [Fact]
    public void Decide_entre_cpf_e_cnpj_pelo_tamanho()
    {
        BrazilianDocument.IsValidCpfOrCnpj("11144477735").ShouldBeTrue();
        BrazilianDocument.IsValidCpfOrCnpj("12345678000195").ShouldBeTrue();

        // 12 ou 13 dígitos não é nenhum dos dois.
        BrazilianDocument.IsValidCpfOrCnpj("123456780001").ShouldBeFalse();
    }

    [Fact]
    public void Extrai_somente_os_digitos()
    {
        BrazilianDocument.OnlyDigits("12.345.678/0001-95").ShouldBe("12345678000195");
        BrazilianDocument.OnlyDigits("  ").ShouldBeNull();
        BrazilianDocument.OnlyDigits("sem numero").ShouldBeNull();
    }
}
