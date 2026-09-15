namespace Convivium.Tests.Condominiums;

using Convivium.Domain.Common;
using Convivium.Domain.Condominiums;

/// <summary>
/// Chave PIX inválida gera um QR Code que o aplicativo do banco recusa, e o
/// síndico só descobre quando os moradores reclamam que não conseguem pagar.
/// </summary>
public class PixKeyTests
{
    [Fact]
    public void Normaliza_cpf_removendo_a_mascara()
    {
        PixKey.Normalize("111.444.777-35", PixKeyType.Cpf).ShouldBe("11144477735");
    }

    [Fact]
    public void Normaliza_cnpj_removendo_a_mascara()
    {
        PixKey.Normalize("12.345.678/0001-95", PixKeyType.Cnpj).ShouldBe("12345678000195");
    }

    [Fact]
    public void Recusa_documento_com_digito_errado()
    {
        Should.Throw<DomainException>(() => PixKey.Normalize("11144477736", PixKeyType.Cpf));
        Should.Throw<DomainException>(() => PixKey.Normalize("12345678000199", PixKeyType.Cnpj));
    }

    [Fact]
    public void Recusa_documento_com_tamanho_errado_para_o_tipo()
    {
        // CNPJ declarado, CPF informado.
        Should.Throw<DomainException>(() => PixKey.Normalize("11144477735", PixKeyType.Cnpj));
    }

    [Theory]
    [InlineData("Sindico@Convivium.Local", "sindico@convivium.local")]
    [InlineData("  contato@predio.com.br  ", "contato@predio.com.br")]
    public void Normaliza_email_para_minusculas(string entrada, string esperado)
    {
        PixKey.Normalize(entrada, PixKeyType.Email).ShouldBe(esperado);
    }

    [Theory]
    [InlineData("sem-arroba")]
    [InlineData("dois@@arrobas.com")]
    [InlineData("sem@dominio")]
    public void Recusa_email_invalido(string chave)
    {
        Should.Throw<DomainException>(() => PixKey.Normalize(chave, PixKeyType.Email));
    }

    [Theory]
    [InlineData("31999998888", "+5531999998888")]
    [InlineData("(31) 99999-8888", "+5531999998888")]
    [InlineData("+55 31 99999-8888", "+5531999998888")]
    [InlineData("3133334444", "+553133334444")]
    public void Coloca_telefone_no_padrao_internacional(string entrada, string esperado)
    {
        // O padrão do Banco Central exige o código do país.
        PixKey.Normalize(entrada, PixKeyType.Phone).ShouldBe(esperado);
    }

    [Theory]
    [InlineData("999")]
    [InlineData("1234567890123456")]
    public void Recusa_telefone_com_tamanho_impossivel(string chave)
    {
        Should.Throw<DomainException>(() => PixKey.Normalize(chave, PixKeyType.Phone));
    }

    [Fact]
    public void Normaliza_chave_aleatoria_para_minusculas_com_hifens()
    {
        PixKey.Normalize("7B1E5A0C-3D2F-4A6B-9C8D-1E2F3A4B5C6D", PixKeyType.Random)
            .ShouldBe("7b1e5a0c-3d2f-4a6b-9c8d-1e2f3a4b5c6d");
    }

    [Fact]
    public void Recusa_chave_aleatoria_que_nao_e_identificador()
    {
        Should.Throw<DomainException>(() => PixKey.Normalize("chave-qualquer", PixKeyType.Random));
    }

    [Fact]
    public void Recusa_chave_vazia()
    {
        Should.Throw<DomainException>(() => PixKey.Normalize("", PixKeyType.Cpf));
        Should.Throw<DomainException>(() => PixKey.Normalize(null, PixKeyType.Email));
    }
}
