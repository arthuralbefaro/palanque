using System.Text.Json.Nodes;
using Planalto.Core.Campaign;
using Planalto.Core.Save;
using Planalto.Core.State;
using Planalto.Core.Territory;
using Planalto.Core.Tests.Campaign;

namespace Planalto.Core.Tests.Save;

/// <summary>
/// Save criado com outra divisão em zonas não abre (decisão do autor: os ids de zona não são migrados): a leitura
/// recusa a versão antiga com a mensagem ao jogador, e a conferência de ids pega zona que não existe nos dados.
/// </summary>
public class SaveZoneCheckTests
{
    private const int Mantena = 3139607;

    private static IReadOnlyList<string>? ZonesOf(int ibge)
    {
        var m = CampaignKit.C.Municipios.All.FirstOrDefault(x => x.CodigoIbge == ibge);
        return m is null ? null : CampaignKit.Zonas(m).Zonas.Select(z => z.Id).ToList();
    }

    private static string CampaignSave() => GameStateSerializer.Serialize(CampaignKit.NewCareer(Mantena, 5).Context.State);

    [Fact]
    public void Save_novo_grava_a_versao_atual_e_abre()
    {
        string json = CampaignSave();
        Assert.Contains($"\"zonas_versao\": {ZonasPaths.Versao}", json);

        GameState loaded = GameStateSerializer.Deserialize(json);

        Assert.Equal(ZonasPaths.Versao, loaded.ZonasVersao);
        Assert.Empty(SaveZoneCheck.Problems(loaded, ZonesOf));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(ZonasPaths.Versao + 1)]
    public void Save_de_outra_divisao_de_zonas_nao_abre_e_explica_por_que(int? versao)
    {
        JsonObject root = JsonNode.Parse(CampaignSave())!.AsObject();
        root.Remove("zonas_versao");
        if (versao is int v)
        {
            root["zonas_versao"] = v;
        }

        var ex = Assert.Throws<SaveFormatException>(() => GameStateSerializer.Deserialize(root.ToJsonString()));

        Assert.Equal(SaveZoneCheck.Mensagem, ex.Message);
        Assert.Contains("divisão de zonas anterior", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Zona_que_nao_existe_nos_dados_impede_o_save_de_abrir()
    {
        GameState state = GameStateSerializer.Deserialize(CampaignSave());
        string home = state.Campaign!.Territory.Zones[state.Campaign.Territory.HomeZone];
        state.Ledger.AddPromise(Planalto.Core.People.Promise.ParaZona, $"{Mantena}-99", "obra", "Cascalhar a estrada", $"{Mantena}-99", state.Calendar.WeekStart, state.Calendar.WeekStart.AddDays(60));

        List<string> problems = SaveZoneCheck.Problems(state, ZonesOf);

        Assert.Contains(problems, p => p.Contains($"{Mantena}-99", StringComparison.Ordinal));
        Assert.DoesNotContain(problems, p => p.Contains(home, StringComparison.Ordinal));
    }

    [Fact]
    public void Territorio_com_as_zonas_em_outra_ordem_impede_o_save_de_abrir()
    {
        // Simula dados regenerados sem mudar a versão: os mesmos ids, noutra ordem (os vetores seguem a ordem).
        GameState state = GameStateSerializer.Deserialize(CampaignSave());
        IReadOnlyList<string> real = ZonesOf(Mantena)!;
        var swapped = real.Reverse().ToList();

        List<string> problems = SaveZoneCheck.Problems(state, ibge => ibge == Mantena ? swapped : ZonesOf(ibge));

        Assert.Contains(problems, p => p.Contains("território", StringComparison.Ordinal));
    }

    [Fact]
    public void Posicao_de_zona_fora_da_lista_impede_o_save_de_abrir()
    {
        GameState state = GameStateSerializer.Deserialize(CampaignSave());
        TerritoryState territory = state.Campaign!.Territory;
        territory.Redutos["rival_teste"] = [territory.Zones.Count + 3];

        Assert.Contains(SaveZoneCheck.Problems(state, ZonesOf), p => p.StartsWith("redutos", StringComparison.Ordinal));
    }

    [Fact]
    public void Ids_de_zona_dentro_de_chaves_compostas_tambem_sao_conferidos()
    {
        GameState state = GameStateSerializer.Deserialize(CampaignSave());
        state.Campaign!.Agenda.Fatigue[$"caminhada|{Mantena}-98|geral"] = 1;

        Assert.Contains($"{Mantena}-98", SaveZoneCheck.ZoneIds(state));
        Assert.Contains(SaveZoneCheck.Problems(state, ZonesOf), p => p.Contains($"{Mantena}-98", StringComparison.Ordinal));
    }

    [Fact]
    public void Carreira_sem_campanha_nao_tem_problema_de_zona()
    {
        CampaignEngine engine = CampaignKit.NewCareer(Mantena, 9);
        GameState state = engine.Context.State;
        state.Campaign = null;

        Assert.Empty(SaveZoneCheck.Problems(state, ZonesOf));
    }
}
