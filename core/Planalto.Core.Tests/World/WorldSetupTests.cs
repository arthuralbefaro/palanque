using Planalto.Core.Ideology;
using Planalto.Core.Parties;
using Planalto.Core.State;
using Planalto.Core.Tests.Parties;
using Planalto.Core.World;

namespace Planalto.Core.Tests.World;

public class WorldSetupTests
{
    private static WorldContent Content => TestContent.World;

    private static WorldSetup NewWorld(string preset = "multipartidario_moderado", ulong seed = 1) =>
        WorldSetup.Create(Content, preset, new Rng(seed)).Setup!;

    public static TheoryData<string, int, int> AvailablePresets => new()
    {
        { "brasil_realista", 28, 32 },
        { "multipartidario_moderado", 12, 16 },
        { "poucos_partidos", 4, 6 },
        { "bipartidario", 2, 2 },
        { "personalizado", 12, 16 },
    };

    [Theory]
    [MemberData(nameof(AvailablePresets))]
    public void Predefinicao_GeraQuantidadeNaFaixaTodosValidosEEspectroCoberto(string presetId, int min, int max)
    {
        for (ulong seed = 1; seed <= 40; seed++)
        {
            WorldSetupCreation creation = WorldSetup.Create(Content, presetId, new Rng(seed));
            WorldSetup setup = creation.Setup!;

            Assert.InRange(setup.Parties.Count, min, max);
            Assert.True(creation.Report.IsValid, $"{presetId}/{seed}: {string.Join(", ", creation.Report.Errors.Select(e => $"{e.Code} {e.PartyId} {e.Detail}"))}");
            Assert.Empty(creation.Report.Warnings);
            Assert.Equal(setup.Parties.Count, setup.Parties.Select(p => p.Number).Distinct().Count());
            Assert.All(setup.Parties, p => Assert.InRange(p.Number, 10, 99));
            if (setup.Parties.Count >= 2)
            {
                Assert.Contains(setup.Parties, p => p.Position.Economic < 0);
                Assert.Contains(setup.Parties, p => p.Position.Economic > 0);
            }
        }
    }

    [Fact]
    public void BrasilRealista_CobreTodasAsFamiliasComCentroPragmaticoForte()
    {
        var counts = new Dictionary<string, int>();
        var large = new Dictionary<string, int>();
        for (ulong seed = 1; seed <= 50; seed++)
        {
            WorldSetup setup = NewWorld("brasil_realista", seed);
            Assert.All(Content.Parties.Families.Families, f => Assert.Contains(setup.Parties, p => p.FamilyId == f.Id));
            foreach (PartyDraft party in setup.Parties)
            {
                counts[party.FamilyId] = counts.GetValueOrDefault(party.FamilyId) + 1;
                if (party.Size >= PartySize.Grande)
                {
                    large[party.FamilyId] = large.GetValueOrDefault(party.FamilyId) + 1;
                }
            }
        }

        // Em média, o centro pragmático é a família mais numerosa e a que mais tem partidos grandes.
        Assert.Equal("centro_pragmatico", counts.MaxBy(c => c.Value).Key);
        Assert.Equal("centro_pragmatico", large.MaxBy(c => c.Value).Key);
    }

    [Fact]
    public void Bipartidario_TemUmPartidoDeCadaLadoEDoisGrandes()
    {
        WorldSetup setup = NewWorld("bipartidario", 3);

        Assert.Equal(2, setup.Parties.Count);
        Assert.All(setup.Parties, p => Assert.True(p.Size >= PartySize.Grande));
    }

    [Fact]
    public void Brasil2026_CarregaOsPartidosDoCenarioEValida()
    {
        WorldSetupCreation creation = WorldSetup.Create(Content, "brasil_2026", new Rng(1));
        WorldSetup setup = creation.Setup!;

        Assert.True(creation.Report.IsValid, string.Join(", ", creation.Report.Errors.Select(e => $"{e.Code} {e.PartyId} {e.Detail}")));
        Assert.True(setup.IsScenario);
        Assert.Equal(Content.Brasil2026!.Parties.Select(p => p.Id), setup.Parties.Select(p => p.Id));
        Assert.Equal(Content.Brasil2026.Parties.Select(p => p.Name), setup.Parties.Select(p => p.Name));
    }

    [Fact]
    public void Brasil2026_EditaCamposMasNaoAdicionaNemRemovePartidos()
    {
        WorldSetup setup = WorldSetup.Create(Content, "brasil_2026", new Rng(2)).Setup!;
        var rng = new Rng(3);
        string first = setup.Parties[0].Id;

        Assert.Null(setup.AddParty(rng));
        Assert.False(setup.RemoveParty(first));
        Assert.Null(setup.DuplicateParty(first, rng));
        Assert.True(setup.RandomizeField(first, PartyField.Name, rng));

        setup.Parties[0].Name = "";
        setup.RestorePreset(rng);
        Assert.Equal(Content.Brasil2026!.Parties[0].Name, setup.Parties[0].Name);
        Assert.Equal("brasil_2026", setup.PresetId);
    }

    [Fact]
    public void PredefinicaoDesconhecida_RetornaErroSemExcecao()
    {
        WorldSetupCreation creation = WorldSetup.Create(Content, "nao_existe", new Rng(1));

        Assert.Null(creation.Setup);
        Assert.True(creation.Report.Has(WorldIssueCode.PresetDesconhecida));
    }

    [Fact]
    public void MesmaSemente_GeraOMesmoMundo()
    {
        Assert.Equal(WorldSetupSerializer.Serialize(NewWorld(seed: 9)), WorldSetupSerializer.Serialize(NewWorld(seed: 9)));
        Assert.NotEqual(WorldSetupSerializer.Serialize(NewWorld(seed: 9)), WorldSetupSerializer.Serialize(NewWorld(seed: 10)));
    }

    [Fact]
    public void SorteioGeral_NaoAlteraCamposTravados()
    {
        WorldSetup setup = NewWorld("brasil_realista", 11);
        var rng = new Rng(12);
        PartyField[] fields = Enum.GetValues<PartyField>();

        // Cada partido trava um subconjunto diferente de campos.
        for (int i = 0; i < setup.Parties.Count; i++)
        {
            foreach (PartyField field in fields.Where((_, f) => ((i + 1) & (1 << f)) != 0))
            {
                setup.SetLocked(setup.Parties[i].Id, field, true);
            }
        }

        var before = setup.Parties.Select(Snapshot).ToList();
        for (int round = 0; round < 5; round++)
        {
            setup.RandomizeAll(rng);
        }

        var after = setup.Parties.Select(Snapshot).ToList();
        for (int i = 0; i < before.Count; i++)
        {
            foreach (PartyField field in setup.Parties[i].Locks)
            {
                Assert.Equal(before[i][field], after[i][field]);
            }
        }

        // E os destravados mudam.
        Assert.Contains(Enumerable.Range(0, before.Count), i => !setup.Parties[i].IsLocked(PartyField.Name) && before[i][PartyField.Name] != after[i][PartyField.Name]);
        Assert.True(setup.Validate().IsValid, string.Join(", ", setup.Validate().Errors.Select(e => $"{e.Code} {e.PartyId} {e.Detail}")));
    }

    [Fact]
    public void SorteioDeLinhaECampo_RespeitamTravas()
    {
        WorldSetup setup = NewWorld(seed: 13);
        var rng = new Rng(14);
        PartyDraft party = setup.Parties[0];
        party.SetLocked(PartyField.Name, true);
        party.SetLocked(PartyField.Number, true);
        string name = party.Name;
        int number = party.Number;

        Assert.False(setup.RandomizeField(party.Id, PartyField.Name, rng));
        for (int i = 0; i < 10; i++)
        {
            setup.RandomizeParty(party.Id, rng);
        }

        Assert.Equal(name, party.Name);
        Assert.Equal(number, party.Number);
        Assert.True(setup.RandomizeField(party.Id, PartyField.Visual, rng));
    }

    [Fact]
    public void PosicaoTravada_SorteiaFamiliaQueContemAPosicao()
    {
        WorldSetup setup = NewWorld(seed: 15);
        var rng = new Rng(16);
        PartyDraft party = setup.Parties[0];
        party.Position = new IdeologicalPosition(-90, -80);
        party.SetLocked(PartyField.Position, true);

        for (int i = 0; i < 20; i++)
        {
            setup.RandomizeField(party.Id, PartyField.Family, rng);
            Assert.True(Content.Parties.Families.Get(party.FamilyId).Contains(party.Position));
        }
    }

    [Fact]
    public void NomeTravado_SiglaDerivadaEvitaBloqueioERepeticao()
    {
        WorldSetup setup = NewWorld(seed: 17);
        PartyDraft party = setup.Parties[0];
        party.Name = "Frente Liberal Brasileira";
        party.SetLocked(PartyField.Name, true);
        setup.Parties[1].Sigla = "FLB";
        setup.Parties[1].SetLocked(PartyField.Sigla, true);

        setup.RandomizeField(party.Id, PartyField.Sigla, new Rng(18));

        Assert.Equal("FLBr", party.Sigla);
        Assert.True(setup.Validate().IsValid);
    }

    [Fact]
    public void Adicionar_RemoverEDuplicar()
    {
        WorldSetup setup = NewWorld("poucos_partidos", 19);
        var rng = new Rng(20);
        int count = setup.Parties.Count;

        PartyDraft original = setup.Parties[0];
        PartyDraft copy = setup.DuplicateParty(original.Id, rng)!;
        Assert.Equal(count + 1, setup.Parties.Count);
        Assert.Equal(original.FamilyId, copy.FamilyId);
        Assert.Equal(original.Position, copy.Position);
        Assert.Equal(original.Size, copy.Size);
        Assert.NotEqual(original.Sigla, copy.Sigla);
        Assert.NotEqual(original.Number, copy.Number);
        Assert.NotEqual(original.Id, copy.Id);

        PartyDraft added = setup.AddParty(rng)!;
        Assert.Contains(added, setup.Parties);
        Assert.True(setup.RemoveParty(added.Id));
        Assert.True(setup.RemoveParty(copy.Id));
        Assert.Equal(count, setup.Parties.Count);
        Assert.True(setup.Validate().IsValid);
    }

    [Fact]
    public void AcimaDaFaixaDaPredefinicao_ViraPersonalizadoComLimiteDeTrintaECinco()
    {
        WorldSetup setup = NewWorld("bipartidario", 21);
        var rng = new Rng(22);

        setup.AddParty(rng);
        Assert.Equal("personalizado", setup.PresetId);

        while (setup.AddParty(rng) is not null)
        {
        }

        Assert.Equal(35, setup.Parties.Count);
        Assert.True(setup.Validate().IsValid, string.Join(", ", setup.Validate().Errors.Select(e => $"{e.Code} {e.PartyId} {e.Detail}")));

        while (setup.RemoveParty(setup.Parties[^1].Id))
        {
        }

        Assert.Equal(2, setup.Parties.Count);
    }

    [Fact]
    public void Restaurar_DescartaEdicoesETravas()
    {
        WorldSetup setup = NewWorld(seed: 23);
        setup.Parties[0].Name = "";
        setup.SetLocked(setup.Parties[0].Id, PartyField.Name, true);

        setup.RestorePreset(new Rng(24));

        Assert.True(setup.Validate().IsValid);
        Assert.All(setup.Parties, p => Assert.Empty(p.Locks));
    }

    [Fact]
    public void Validacao_ReportaRepetidosEBloqueadosComoErro()
    {
        WorldSetup setup = NewWorld(seed: 25);
        PartyDraft a = setup.Parties[0];
        PartyDraft b = setup.Parties[1];
        PartyDraft c = setup.Parties[2];
        PartyDraft d = setup.Parties[3];
        b.Sigla = a.Sigla;
        b.Number = a.Number;
        b.Name = a.Name.ToUpperInvariant();
        c.Sigla = "TP";
        c.Name = "Partido Verde";
        d.Sigla = "PSDB";
        d.Number = 9;

        WorldValidationReport report = setup.Validate();

        Assert.False(report.IsValid);
        Assert.Contains(report.Errors, e => e.Code == WorldIssueCode.SiglaRepetida && e.PartyId == a.Id && e.OtherPartyId == b.Id);
        Assert.Contains(report.Errors, e => e.Code == WorldIssueCode.NumeroRepetido);
        Assert.Contains(report.Errors, e => e.Code == WorldIssueCode.NomeRepetido);
        Assert.Contains(report.Errors, e => e.Code == WorldIssueCode.SiglaBloqueadaPorOrdemDasLetras && e.PartyId == c.Id);
        Assert.Contains(report.Errors, e => e.Code == WorldIssueCode.NomeBloqueado && e.PartyId == c.Id);
        Assert.Contains(report.Errors, e => e.Code == WorldIssueCode.SiglaBloqueada && e.PartyId == d.Id);
        Assert.Contains(report.Errors, e => e.Code == WorldIssueCode.NumeroForaDaFaixa && e.PartyId == d.Id);
    }

    [Theory]
    [InlineData("P", WorldIssueCode.SiglaComTamanhoInvalido)]
    [InlineData("ABCDEFGHI", WorldIssueCode.SiglaComTamanhoInvalido)]
    [InlineData("AB1", WorldIssueCode.SiglaComCaracteresInvalidos)]
    [InlineData("A-B", WorldIssueCode.SiglaComCaracteresInvalidos)]
    public void Validacao_SiglaForaDoFormato(string sigla, WorldIssueCode expected)
    {
        WorldSetup setup = NewWorld(seed: 26);
        setup.Parties[0].Sigla = sigla;

        Assert.Contains(setup.Validate().Errors, e => e.Code == expected);
    }

    [Fact]
    public void Validacao_EspectroDescobertoETamanhosIncoerentesSaoAvisos()
    {
        WorldSetup setup = NewWorld("poucos_partidos", 27);
        foreach (PartyDraft party in setup.Parties)
        {
            party.FamilyId = "direita_liberal";
            party.Position = new IdeologicalPosition(60, 0);
            party.Size = PartySize.Nanico;
        }

        setup.Parties[0].Size = PartySize.Dominante;
        setup.Parties[1].Size = PartySize.Dominante;
        setup.Parties[2].Size = PartySize.Dominante;

        WorldValidationReport report = setup.Validate();

        Assert.True(report.IsValid);
        Assert.Contains(report.Warnings, w => w.Code == WorldIssueCode.EspectroSemEsquerdaEconomica);
        Assert.Contains(report.Warnings, w => w.Code == WorldIssueCode.DominantesDemais);

        foreach (PartyDraft party in setup.Parties)
        {
            party.Size = PartySize.Nanico;
        }

        report = setup.Validate();
        Assert.Contains(report.Warnings, w => w.Code == WorldIssueCode.NenhumPartidoPrincipal);
        Assert.Contains(report.Warnings, w => w.Code == WorldIssueCode.NanicoGrandeDemais);
    }

    [Fact]
    public void Validacao_EspectroSoEExigidoComQuatroOuMaisPartidos()
    {
        WorldSetup setup = NewWorld("bipartidario", 28);
        foreach (PartyDraft party in setup.Parties)
        {
            party.FamilyId = "direita_liberal";
            party.Position = new IdeologicalPosition(60, 0);
        }

        Assert.DoesNotContain(setup.Validate().Warnings, w => w.Code == WorldIssueCode.EspectroSemEsquerdaEconomica);
    }

    [Fact]
    public void Validacao_PosicaoForaDaFaixaDaFamiliaEAviso()
    {
        WorldSetup setup = NewWorld(seed: 29);
        setup.Parties[0].FamilyId = "esquerda_socialista";
        setup.Parties[0].Position = new IdeologicalPosition(0, 0);
        setup.Parties[1].Position = new IdeologicalPosition(150, 0);

        WorldValidationReport report = setup.Validate();

        Assert.Contains(report.Warnings, w => w.Code == WorldIssueCode.PosicaoForaDaFaixaDaFamilia && w.PartyId == setup.Parties[0].Id);
        Assert.Contains(report.Errors, e => e.Code == WorldIssueCode.PosicaoForaDosEixos && e.PartyId == setup.Parties[1].Id);
    }

    [Theory]
    [InlineData(2028, true)]
    [InlineData(2030, true)]
    [InlineData(2040, true)]
    [InlineData(2029, false)]
    [InlineData(2026, false)]
    public void Opcoes_AnoInicialPrecisaSerDeEleicaoAPartirDe2028(int year, bool valid)
    {
        WorldSetup setup = NewWorld(seed: 30);
        setup.Options.StartYear = year;

        Assert.Equal(valid, !setup.Validate().Has(WorldIssueCode.AnoInicialInvalido));
    }

    [Fact]
    public void Opcoes_PadroesEDificuldadeDesconhecida()
    {
        WorldSetup setup = NewWorld(seed: 31);

        Assert.Equal(2028, setup.Options.StartYear);
        Assert.Equal("normal", setup.Options.Difficulty);
        Assert.Equal(EventFrequency.Normal, setup.Options.EventFrequency);
        Assert.False(setup.Options.Ironman);

        setup.Options.Difficulty = "impossivel";
        Assert.True(setup.Validate().Has(WorldIssueCode.DificuldadeDesconhecida));
    }

    private static Dictionary<PartyField, string> Snapshot(PartyDraft p) => new()
    {
        [PartyField.Name] = p.Name,
        [PartyField.Sigla] = p.Sigla,
        [PartyField.Number] = p.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
        [PartyField.Family] = p.FamilyId,
        [PartyField.Position] = p.Position.ToString(),
        [PartyField.Size] = p.Size.ToString(),
        [PartyField.Visual] = $"{p.Colors.Primary}/{p.Colors.Secondary}/{p.LogoIcon}",
        [PartyField.Leadership] = p.Leadership.ToString(),
    };
}
