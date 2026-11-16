using Planalto.Core.Campaign;
using Planalto.Core.Career;
using Planalto.Core.Data;
using Planalto.Core.Events;
using Planalto.Core.Executive;
using Planalto.Core.Legislature;
using Planalto.Core.Renewal;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;
using Planalto.Core.Tests.Parties;
using Xunit;

namespace Planalto.Core.Tests.Governance;

/// <summary>Os textos de morte e de luto da renovação passam pelas regras de tom dos eventos (GDD 17.16, "morte tratada com seriedade").</summary>
public class DeathTextTests
{
    [Fact]
    public void Textos_de_morte_da_renovacao_passam_pelas_regras_de_tom()
    {
        string path = Path.Combine(TestContent.DataDirectory, RenewalConfig.File);
        RenewalConfig config = JsonData.LoadFile<RenewalConfig>(path).GetValueOrThrow();
        var death = config.Textos.Where(t => RenewalConfig.IsDeathText(t.Key, t.Value)).ToList();
        Assert.True(death.Count >= 8, $"só {death.Count} textos de morte ou luto.");
        Assert.All(death, t => Assert.Empty(EventLint.CheckDeathText(t.Value)));
        Assert.Empty(config.Validate());

        // Um texto fora do tom reprova o arquivo, com a chave no erro.
        string bad = Path.Combine(Path.GetTempPath(), $"renewal_{Guid.NewGuid():N}.json");
        try
        {
            string original = config.Textos["morte_lideranca"];
            string json = File.ReadAllText(path);
            Assert.Contains(original, json);
            File.WriteAllText(bad, json.Replace(original, "{nome} teve um fim chocante aos {idade} anos!", StringComparison.Ordinal));
            DataLoadResult<RenewalConfig> loaded = JsonData.LoadFile<RenewalConfig>(bad);
            Assert.Contains(loaded.Errors, e => e.Contains("textos.morte_lideranca", StringComparison.Ordinal) && e.Contains("exclamação", StringComparison.Ordinal));
            Assert.Contains(loaded.Errors, e => e.Contains("sensacionalista", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(bad);
        }

        // Texto que fala de morte fora das chaves "morte_" também é conferido.
        Assert.True(RenewalConfig.IsDeathText("posse_suplente", "Assumiu depois do falecimento do titular."));
        Assert.False(RenewalConfig.IsDeathText("posse_suplente", config.Textos["posse_suplente"]));
    }
}
