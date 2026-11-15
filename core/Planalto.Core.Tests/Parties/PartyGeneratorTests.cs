using Planalto.Core.Ideology;
using Planalto.Core.Parties;
using Planalto.Core.State;

namespace Planalto.Core.Tests.Parties;

public class PartyGeneratorTests
{
    private const int NumberMin = 10;
    private const int NumberMax = 99;

    private static PartyContent Content => TestContent.Parties;

    private static PartyGenerator Generator => new(Content);

    [Fact]
    public void Estresse_DezMilPartidosSemColisaoComAListaESemSiglaRepetidaNoMundo()
    {
        const int partiesPerWorld = 35;
        const int total = 10_000;
        var rng = new Rng(20261115);
        PartyGenerator generator = Generator;
        List<FamilyDefinition> families = Content.Families.Families;
        int generated = 0;

        while (generated < total)
        {
            var taken = new TakenIdentities(Content.Blocklist);
            var siglas = new HashSet<string>(StringComparer.Ordinal);
            var numbers = new HashSet<int>();
            var primaries = new List<OkLab>();
            for (int i = 0; i < partiesPerWorld && generated < total; i++, generated++)
            {
                FamilyDefinition family = rng.PickWeighted(families, f => f.Weight);
                GeneratedParty? party = generator.Generate(rng, family, taken, NumberMin, NumberMax);

                Assert.NotNull(party);
                Assert.Null(Content.Blocklist.CheckSigla(party.Sigla));
                Assert.Null(Content.Blocklist.CheckName(party.Name));
                Assert.True(siglas.Add(PartyBlocklist.NormalizeSigla(party.Sigla)), $"Sigla repetida no mundo: {party.Sigla}");
                Assert.True(numbers.Add(party.Number), $"Número repetido no mundo: {party.Number}");
                Assert.InRange(party.Number, NumberMin, NumberMax);

                Assert.True(OkColor.TryParseHex(party.Colors.Primary, out OkLab primary));
                Assert.All(primaries, other => Assert.True(other.DistanceTo(primary) >= Content.Generator.Colors.MinDistanceFloor));
                primaries.Add(primary);
            }
        }

        Assert.Equal(total, generated);
    }

    [Fact]
    public void MesmaSemente_GeraOsMesmosPartidos()
    {
        Assert.Equal(GenerateWorld(42), GenerateWorld(42));
        Assert.NotEqual(GenerateWorld(42), GenerateWorld(43));
    }

    [Fact]
    public void Posicao_FicaDentroDaFaixaDaFamilia()
    {
        var rng = new Rng(7);
        foreach (FamilyDefinition family in Content.Families.FixedRangeFamilies)
        {
            for (int i = 0; i < 200; i++)
            {
                IdeologicalPosition position = Generator.GeneratePosition(rng, family);
                Assert.True(family.Contains(position), $"{family.Id}: {position}");
            }
        }
    }

    [Fact]
    public void Regionalista_SorteiaAFaixaDeOutraFamilia()
    {
        var rng = new Rng(8);
        FamilyDefinition regionalista = Content.Families.Get("regionalista");
        var usedFamilies = new HashSet<string>();

        for (int i = 0; i < 500; i++)
        {
            IdeologicalPosition position = Generator.GeneratePosition(rng, regionalista);
            var containing = Content.Families.FixedRangeFamilies.Where(f => f.Contains(position)).ToList();
            Assert.NotEmpty(containing);
            usedFamilies.UnionWith(containing.Select(f => f.Id));
        }

        Assert.True(usedFamilies.Count > 5, "A faixa emprestada deveria variar entre famílias.");
    }

    [Theory]
    [InlineData("Frente Liberal do Brasil", "FLB")]
    [InlineData("Aliança Democrática Brasileira", "ADB")]
    [InlineData("União Cívica", "UC")]
    [InlineData("Ação Verde e Sustentável", "AVS")]
    public void Sigla_SaiDasIniciaisSemPreposicoes(string name, string sigla)
    {
        Assert.Equal(sigla, Generator.DeriveSigla(name));
    }

    [Fact]
    public void Nome_ConcordaComOGeneroDoPrimeiroTermo()
    {
        var rng = new Rng(9);
        var feminineHeads = Content.NameTerms.Heads.Where(h => h.Gender == GrammaticalGender.Feminine).Select(h => h.Text).ToHashSet();
        var masculineOnly = Content.NameTerms.Qualifiers.Where(q => q.Masculine != q.Feminine).Select(q => q.Masculine).ToHashSet();

        for (int i = 0; i < 500; i++)
        {
            FamilyDefinition family = rng.Pick(Content.Families.Families);
            GeneratedName name = Generator.GenerateName(rng, family, new TakenIdentities(Content.Blocklist))!;
            string[] words = name.Name.Split(' ');
            if (feminineHeads.Contains(words[0]))
            {
                Assert.DoesNotContain(words.Skip(1), masculineOnly.Contains);
            }
        }
    }

    [Fact]
    public void Nome_EvitaTermosRedundantesComAPrimeiraPalavra()
    {
        var rng = new Rng(13);
        FamilyDefinition centro = Content.Families.Get("centro_pragmatico");

        for (int i = 0; i < 1000; i++)
        {
            GeneratedName name = Generator.GenerateName(rng, centro, new TakenIdentities(Content.Blocklist))!;
            Assert.False(name.Name.StartsWith("União Unida", StringComparison.Ordinal), name.Name);
        }
    }

    [Fact]
    public void Nome_UsaSoTermosGenericosOuDaFamilia()
    {
        var rng = new Rng(10);
        FamilyDefinition ambientalista = Content.Families.Get("ambientalista");
        var forbidden = Content.NameTerms.Qualifiers
            .Where(q => !q.IsGeneric && !q.Families.Contains("ambientalista"))
            .SelectMany(q => new[] { q.Masculine, q.Feminine })
            .ToHashSet();

        for (int i = 0; i < 300; i++)
        {
            GeneratedName name = Generator.GenerateName(rng, ambientalista, new TakenIdentities(Content.Blocklist))!;
            Assert.DoesNotContain(name.Name.Split(' '), forbidden.Contains);
        }
    }

    [Fact]
    public void Cores_SecundariaContrastaEIconeNuncaEExcluido()
    {
        var rng = new Rng(11);
        var excluded = Content.LogoIcons.Excluded.Select(e => e.Id).ToHashSet();
        var taken = new TakenIdentities(Content.Blocklist);

        for (int i = 0; i < 35; i++)
        {
            PartyColors colors = Generator.GenerateColors(rng, taken);
            taken.Add(null, null, null, colors);
            Assert.True(OkColor.TryParseHex(colors.Primary, out OkLab primary));
            Assert.True(OkColor.TryParseHex(colors.Secondary, out OkLab secondary));
            Assert.True(primary.DistanceTo(secondary) >= Content.Generator.Colors.SecondaryMinDistance - 1e-9);
            Assert.DoesNotContain(Generator.PickLogoIcon(rng, rng.Pick(Content.Families.Families).Id), excluded);
        }
    }

    [Fact]
    public void Icone_ComFamiliaSoESorteadoParaElas()
    {
        var rng = new Rng(14);
        var restricted = Content.LogoIcons.Icons.Where(i => i.Families.Count > 0).ToDictionary(i => i.Id, i => i.Families);

        foreach (FamilyDefinition family in Content.Families.Families)
        {
            for (int i = 0; i < 300; i++)
            {
                string icon = Generator.PickLogoIcon(rng, family.Id);
                if (restricted.TryGetValue(icon, out List<string>? families))
                {
                    Assert.Contains(family.Id, families);
                }
            }
        }
    }

    [Fact]
    public void Numero_NuncaRepeteENuloQuandoAcabam()
    {
        var rng = new Rng(12);
        var taken = new TakenIdentities(Content.Blocklist);
        for (int i = NumberMin; i <= NumberMax; i++)
        {
            int number = PartyGenerator.PickNumber(rng, taken, NumberMin, NumberMax)!.Value;
            Assert.DoesNotContain(number, taken.Numbers);
            taken.Add(null, null, number, null);
        }

        Assert.Null(PartyGenerator.PickNumber(rng, taken, NumberMin, NumberMax));
    }

    [Theory]
    [InlineData("#FFFFFF")]
    [InlineData("#000000")]
    [InlineData("#1E90FF")]
    [InlineData("#C0392B")]
    public void OkColor_IdaEVoltaPreservaOHex(string hex)
    {
        Assert.True(OkColor.TryParseHex(hex, out OkLab lab));
        Assert.Equal(hex, OkColor.ToHex(lab));
    }

    private static List<GeneratedParty> GenerateWorld(ulong seed)
    {
        var rng = new Rng(seed);
        var taken = new TakenIdentities(Content.Blocklist);
        return Content.Families.Families.Select(f => Generator.Generate(rng, f, taken, NumberMin, NumberMax)!).ToList();
    }
}
