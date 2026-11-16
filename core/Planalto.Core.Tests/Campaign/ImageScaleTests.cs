using System.Text.Json;
using System.Text.Json.Nodes;
using Planalto.Core.Campaign;
using Planalto.Core.Data;

namespace Planalto.Core.Tests.Campaign;

/// <summary>Régua de imagem: o número de −1 a +1 vira faixa nomeada, com nomes e limites em data/campaign/config.json.</summary>
public class ImageScaleTests
{
    private static CampaignConfig Config => CampaignKit.C.Campaign.Config;

    [Fact]
    public void A_regua_de_data_cobre_de_menos_um_a_mais_um_com_uma_faixa_em_torno_do_zero()
    {
        CampaignConfig config = Config;

        Assert.Empty(config.Validate());
        Assert.True(config.ImageScale.Count >= 3);
        Assert.Equal(-1, config.ImageBandRange(0).From);
        Assert.Equal(1, config.ImageBandRange(config.ImageScale.Count - 1).To);
        Assert.Equal(0, config.ImageBandSide(config.ImageBandIndex(0)));
        Assert.Equal(-1, config.ImageBandSide(config.ImageBandIndex(-1)));
        Assert.Equal(1, config.ImageBandSide(config.ImageBandIndex(1)));
        Assert.All(config.ImageScale, b => Assert.InRange(b.Name.Length, 1, CampaignConfig.ImageBandNameMax));
    }

    [Fact]
    public void Cada_valor_cai_na_faixa_que_comeca_nele_ou_antes_dele()
    {
        CampaignConfig config = Config;
        for (int i = 0; i < config.ImageScale.Count; i++)
        {
            var (from, to) = config.ImageBandRange(i);
            Assert.Equal(i, config.ImageBandIndex(from));
            Assert.Equal(i, config.ImageBandIndex((from + to) / 2));
            Assert.Equal(config.ImageScale[i].Name, config.ImageBandOf((from + to) / 2).Name);
            if (i > 0)
            {
                Assert.Equal(i - 1, config.ImageBandIndex(from - 0.0001));
            }
        }

        // Fora da régua, vale a faixa da ponta.
        Assert.Equal(0, config.ImageBandIndex(-7));
        Assert.Equal(config.ImageScale.Count - 1, config.ImageBandIndex(7));

        // A carreira começa com imagem zero: a faixa do meio, sem sinal.
        Assert.Equal(0, config.ImageBandSide(config.ImageBandIndex(0.0)));
    }

    [Theory]
    [InlineData("""[{ "from": -1, "name": "mal" }, { "from": 0.1, "name": "bem" }]""", "ao menos 3")]
    [InlineData("""[{ "from": -0.9, "name": "mal" }, { "from": -0.1, "name": "meio" }, { "from": 0.1, "name": "bem" }]""", "começa em -1")]
    [InlineData("""[{ "from": -1, "name": "mal" }, { "from": 0.3, "name": "meio" }, { "from": 0.1, "name": "bem" }]""", "sobem")]
    [InlineData("""[{ "from": -1, "name": "mal" }, { "from": 0.1, "name": "meio" }, { "from": 0.5, "name": "bem" }]""", "contém o zero")]
    [InlineData("""[{ "from": -1, "name": "mal" }, { "from": -0.1, "name": "" }, { "from": 0.1, "name": "bem" }]""", "precisa de nome")]
    [InlineData("""[{ "from": -1, "name": "mal" }, { "from": -0.1, "name": "um nome de faixa comprido demais" }, { "from": 0.1, "name": "bem" }]""", "precisa de nome")]
    [InlineData("""[{ "from": -1, "name": "bem" }, { "from": -0.1, "name": "meio" }, { "from": 0.1, "name": "Bem" }]""", "repetidos")]
    public void Regua_invalida_e_reportada(string scale, string expected)
    {
        JsonNode node = JsonSerializer.SerializeToNode(Config, JsonData.Options)!;
        node["image_scale"] = JsonNode.Parse(scale);
        CampaignConfig broken = node.Deserialize<CampaignConfig>(JsonData.Options)!;

        Assert.Contains(broken.Validate(), e => e.Contains("image_scale", StringComparison.Ordinal) && e.Contains(expected, StringComparison.Ordinal));
    }
}
