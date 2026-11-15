using Planalto.Core.Data;
using Planalto.Core.Parties;

namespace Planalto.Core.Tests.Parties;

public class PartyBlocklistTests
{
    private static readonly Lazy<PartyBlocklist> ProjectBlocklist = new(() =>
    {
        string data = DataPaths.FindDataDirectory(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException("Pasta data/ não encontrada a partir dos testes.");
        return new PartyBlocklist(JsonData.LoadFile<BlocklistFile>(Path.Combine(data, PartyPaths.Blocklist)).GetValueOrThrow());
    });

    private static PartyBlocklist Blocklist => ProjectBlocklist.Value;

    [Fact]
    public void Arquivo_TemOsTrintaPartidosRegistradosEAsFederacoes()
    {
        Assert.Equal(30, Blocklist.File.Entries.Count(e => e.Group == BlocklistGroup.Registrado && e.Sigla is not null));
        Assert.All(Blocklist.File.Entries.Where(e => e.Group == BlocklistGroup.Registrado), e => Assert.True(e.Confirmed));
        Assert.NotNull(Blocklist.CheckName("Democrata"));
        Assert.NotNull(Blocklist.CheckName("Mobilização Nacional"));
        Assert.Equal(5, Blocklist.File.Entries.Count(e => e.Group == BlocklistGroup.Federacao));
        Assert.All(Blocklist.File.Sources, s => Assert.False(string.IsNullOrWhiteSpace(s.Result)));
    }

    [Theory]
    [InlineData("PT")]
    [InlineData("pt")]
    [InlineData("P.T.")]
    [InlineData("PCdoB")]
    [InlineData("PCDOB")]
    [InlineData("Missao")]
    [InlineData("UNIAO")]
    public void SiglaReal_EBloqueadaIgnorandoCaixaAcentoEPontuacao(string sigla)
    {
        Assert.Equal(BlockReason.SiglaIgual, Blocklist.CheckSigla(sigla)?.Reason);
    }

    [Theory]
    [InlineData("TP")]
    [InlineData("DSP")]
    [InlineData("BDSP")]
    [InlineData("BOPCD")]
    public void SiglaQueSoMudaAOrdemDasLetras_EBloqueada(string sigla)
    {
        Assert.Equal(BlockReason.SiglaComLetrasReordenadas, Blocklist.CheckSigla(sigla)?.Reason);
    }

    [Theory]
    [InlineData("PXT")]
    [InlineData("MVR")]
    [InlineData("P")]
    public void SiglaSemCorrespondencia_NaoEBloqueada(string sigla)
    {
        Assert.Null(Blocklist.CheckSigla(sigla));
    }

    [Theory]
    [InlineData("Partido dos Trabalhadores")]
    [InlineData("PARTIDO DOS TRABALHADORES")]
    [InlineData("Partido Trabalhadores")]
    [InlineData("Partido de Trabalhadores")]
    [InlineData("partido   da   mulher   brasileira")]
    [InlineData("Partido Democratico Trabalhista")]
    [InlineData("União Democrática Nacional")]
    [InlineData("Uniao Progressista")]
    [InlineData("Partido Socialismo Liberdade")]
    public void NomeReal_EBloqueadoDepoisDeNormalizar(string name)
    {
        Assert.Equal(BlockReason.NomeIgual, Blocklist.CheckName(name)?.Reason);
    }

    [Theory]
    [InlineData("Partido Liberal Brasileiro", "Partido Liberal")]
    [InlineData("Movimento Solidariedade Nacional", "Solidariedade")]
    [InlineData("Partido dos Trabalhadores Unidos", "Partido dos Trabalhadores")]
    [InlineData("Aliança Renovadora Nacional do Brasil", "Aliança Renovadora Nacional")]
    [InlineData("Frente Cidadania Ativa", "Cidadania")]
    [InlineData("Nova União Democrática Nacional", "União Democrática Nacional")]
    [InlineData("Movimento PARTIDO   VERDE", "Partido Verde")]
    public void NomeQueContemNomeRealInteiro_EBloqueado(string name, string real)
    {
        BlockMatch? match = Blocklist.CheckName(name);

        Assert.Equal(BlockReason.NomeContemNomeBloqueado, match?.Reason);
        Assert.Equal(Blocklist.NormalizeName(real), Blocklist.NormalizeName(match!.Entry.Name));
    }

    [Theory]
    [InlineData("Movimento Democrático Popular")]
    [InlineData("Aliança Verde")]
    [InlineData("Frente Republicana")]
    [InlineData("União Brasileira Cívica")]
    [InlineData("Partido Trabalhista Unido")]
    public void NomeDiferente_NaoEBloqueado(string name)
    {
        Assert.Null(Blocklist.CheckName(name));
    }

    [Fact]
    public void EntradaComFonteNaoDeclarada_EReportada()
    {
        const string json = """
            {
              "sources": [ { "id": "a", "consulted_on": "2026-11-15", "result": "r" } ],
              "ignored_words": [ "de" ],
              "entries": [ { "sigla": "XY", "name": "Partido X", "group": "registrado", "sources": [ "b" ], "confirmed": false } ]
            }
            """;

        DataLoadResult<BlocklistFile> result = JsonData.Parse<BlocklistFile>(json, "teste.json");

        Assert.Contains(result.Errors, e => e.Contains("fonte 'b' não declarada"));
    }

    [Fact]
    public void EntradaRepetida_EReportada()
    {
        const string json = """
            {
              "sources": [ { "id": "a", "consulted_on": "2026-11-15", "result": "r" } ],
              "ignored_words": [ "de" ],
              "entries": [
                { "sigla": "XY", "name": "Partido de X", "group": "registrado", "sources": [ "a" ], "confirmed": false },
                { "sigla": "xy", "name": "Partido X", "group": "historico", "sources": [ "a" ], "confirmed": false }
              ]
            }
            """;

        DataLoadResult<BlocklistFile> result = JsonData.Parse<BlocklistFile>(json, "teste.json");

        Assert.Contains(result.Errors, e => e.Contains("entrada repetida"));
    }
}
