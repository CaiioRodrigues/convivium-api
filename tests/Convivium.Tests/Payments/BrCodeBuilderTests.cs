namespace Convivium.Tests.Payments;

using System.Text;
using Convivium.Domain.Payments;

/// <summary>
/// O BR Code precisa estar exatamente no padrao do Banco Central: um byte
/// errado e o aplicativo do banco recusa o QR Code e o morador nao paga.
/// </summary>
public class BrCodeBuilderTests
{
    private static PixCharge Charge(decimal? amount = 1153.15m, string? txid = "COBRANCA123") => new()
    {
        Key = "12345678000195",
        ReceiverName = "COND RESID CONVIVIUM",
        ReceiverCity = "BELO HORIZONTE",
        Amount = amount,
        TransactionId = txid,
    };

    /// <summary>
    /// CRC-16/CCITT-FALSE reimplementado aqui de proposito: se o teste
    /// chamasse o mesmo codigo do builder, um erro no algoritmo passaria.
    /// </summary>
    private static string Crc16(string payload)
    {
        ushort crc = 0xFFFF;

        foreach (byte b in Encoding.UTF8.GetBytes(payload))
        {
            crc ^= (ushort)(b << 8);

            for (int i = 0; i < 8; i++)
            {
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
            }
        }

        return crc.ToString("X4");
    }

    /// <summary>Percorre a estrutura TLV e devolve os campos de primeiro nivel.</summary>
    private static Dictionary<string, string> ParseTlv(string payload)
    {
        var fields = new Dictionary<string, string>();

        for (int i = 0; i < payload.Length;)
        {
            string tag = payload.Substring(i, 2);
            int length = int.Parse(payload.Substring(i + 2, 2));
            fields[tag] = payload.Substring(i + 4, length);
            i += 4 + length;
        }

        return fields;
    }

    [Fact]
    public void Termina_com_um_CRC_valido()
    {
        string payload = BrCodeBuilder.Build(Charge());

        string body = payload[..^4];
        string checksum = payload[^4..];

        payload[^8..^4].ShouldBe("6304", "o campo do CRC tem id 63 e tamanho 04");
        checksum.ShouldBe(Crc16(body));
    }

    [Fact]
    public void Todos_os_campos_declaram_o_proprio_tamanho_corretamente()
    {
        string payload = BrCodeBuilder.Build(Charge());

        // ParseTlv estoura se algum tamanho nao bater com o conteudo.
        var fields = ParseTlv(payload);

        fields["00"].ShouldBe("01");
        fields["53"].ShouldBe("986", "986 e o codigo ISO do real");
        fields["58"].ShouldBe("BR");
        fields.ShouldContainKey("26");
        fields.ShouldContainKey("63");
    }

    [Fact]
    public void Leva_a_chave_pix_dentro_do_campo_da_conta()
    {
        string payload = BrCodeBuilder.Build(Charge());

        var merchant = ParseTlv(ParseTlv(payload)["26"]);

        merchant["00"].ShouldBe("br.gov.bcb.pix");
        merchant["01"].ShouldBe("12345678000195");
    }

    [Fact]
    public void Formata_o_valor_com_ponto_decimal()
    {
        // O padrao exige ponto, nao a virgula do formato brasileiro.
        ParseTlv(BrCodeBuilder.Build(Charge(amount: 1153.15m)))["54"].ShouldBe("1153.15");
        ParseTlv(BrCodeBuilder.Build(Charge(amount: 1000m)))["54"].ShouldBe("1000.00");
        ParseTlv(BrCodeBuilder.Build(Charge(amount: 0.99m)))["54"].ShouldBe("0.99");
    }

    [Fact]
    public void Sem_valor_vira_QR_estatico_e_omite_o_campo_de_valor()
    {
        var fields = ParseTlv(BrCodeBuilder.Build(Charge(amount: null)));

        fields.ShouldNotContainKey("54");
        fields["01"].ShouldBe("11", "11 marca QR estatico reutilizavel");
    }

    [Fact]
    public void Com_valor_vira_QR_de_uso_unico()
    {
        ParseTlv(BrCodeBuilder.Build(Charge()))["01"].ShouldBe("12");
    }

    [Fact]
    public void Remove_acento_do_nome_e_da_cidade()
    {
        var charge = Charge() with
        {
            ReceiverName = "CONDOMÍNIO JOÃO AÇÚCAR",
            ReceiverCity = "SÃO PAULO",
        };

        var fields = ParseTlv(BrCodeBuilder.Build(charge));

        fields["59"].ShouldBe("CONDOMINIO JOAO ACUCAR");
        fields["60"].ShouldBe("SAO PAULO");
    }

    [Fact]
    public void Trunca_nome_em_25_e_cidade_em_15_caracteres()
    {
        var charge = Charge() with
        {
            ReceiverName = "CONDOMINIO RESIDENCIAL EDIFICIO PARQUE DAS FLORES",
            ReceiverCity = "SAO JOSE DO RIO PRETO",
        };

        var fields = ParseTlv(BrCodeBuilder.Build(charge));

        fields["59"].Length.ShouldBeLessThanOrEqualTo(25);
        fields["60"].Length.ShouldBeLessThanOrEqualTo(15);
    }

    [Fact]
    public void Limpa_o_txid_para_alfanumerico()
    {
        var fields = ParseTlv(BrCodeBuilder.Build(Charge(txid: "COB-2026/09 #101")));
        var additional = ParseTlv(fields["62"]);

        additional["05"].ShouldBe("COB202609101");
    }

    [Fact]
    public void Usa_o_marcador_padrao_quando_nao_ha_txid()
    {
        var additional = ParseTlv(ParseTlv(BrCodeBuilder.Build(Charge(txid: null)))["62"]);

        additional["05"].ShouldBe("***");
    }

    [Fact]
    public void Trunca_txid_em_25_caracteres()
    {
        var additional = ParseTlv(ParseTlv(
            BrCodeBuilder.Build(Charge(txid: new string('A', 40))))["62"]);

        additional["05"].Length.ShouldBe(25);
    }

    [Fact]
    public void Recusa_cobranca_sem_chave_pix()
    {
        Should.Throw<ArgumentException>(() =>
            BrCodeBuilder.Build(Charge() with { Key = "" }));
    }

    [Fact]
    public void Muda_o_CRC_quando_o_valor_muda()
    {
        // Um CRC que nao acompanha o conteudo nao protege contra adulteracao.
        string a = BrCodeBuilder.Build(Charge(amount: 100m));
        string b = BrCodeBuilder.Build(Charge(amount: 100.01m));

        a[^4..].ShouldNotBe(b[^4..]);
    }
}
