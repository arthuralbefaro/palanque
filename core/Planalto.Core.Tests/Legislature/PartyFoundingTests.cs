using System.Text.Json;
using System.Text.Json.Nodes;
using Planalto.Core.Career;
using Planalto.Core.Data;
using Planalto.Core.Elections;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;
using Planalto.Core.Tests.Parties;
using Xunit;
using Calendar = Planalto.Core.State.Calendar;

namespace Planalto.Core.Tests.Legislature;

public class PartyFoundingTests
{
    private static FoundingConfig Config(double? impugnation = null)
    {
        FoundingConfig config = JsonData.LoadFile<FoundingConfig>(Path.Combine(TestContent.DataDirectory, FoundingConfig.File)).GetValueOrThrow();
        if (impugnation is null)
        {
            return config;
        }

        // Cópia com outra chance de impugnação, para testar os dois desfechos.
        JsonNode node = JsonNode.Parse(JsonSerializer.Serialize(config, JsonData.Options))!;
        node["impugnation_base"] = impugnation.Value;
        return JsonData.Parse<FoundingConfig>(node.ToJsonString(), "teste").GetValueOrThrow();
    }

    private static PartyFoundingEngine Engine(GameState state, FoundingConfig config) =>
        new(state, config, CampaignKit.C.World.Parties, CampaignKit.C.Municipios, CandidateNumberRulesConfig.Default);

    private static GameState OutOfOffice()
    {
        GameState state = CampaignKit.NewCareer(3550308, 8).Context.State;
        state.Campaign = null;
        state.Player.Resources.PoliticalCapital = 100;
        state.Player.Resources.Money = 10_000_000;
        return state;
    }

    [Fact]
    public void Requisitos_seguem_a_lei_sobre_o_eleitorado()
    {
        GameState state = OutOfOffice();
        FoundingConfig config = Config();
        PartyFoundingEngine engine = Engine(state, config);
        long electorate = CampaignKit.C.Municipios.All.Sum(m => m.Eleitorado);

        Assert.InRange(engine.NationalRequired, (long)(electorate * 0.72 * 0.005) - 1, (long)(electorate * 0.72 * 0.005) + 1);
        Assert.True(engine.StateRequired("SP") > engine.StateRequired("RR"));
        Assert.Equal(27, engine.States.Count);
    }

    [Fact]
    public void Meta_do_Brasil_2026_e_meio_por_cento_dos_votos_validos_reais()
    {
        GameState state = OutOfOffice();
        long valid = CampaignKit.C.Brasil2026.National.CamaraValidVotes;
        Assert.Equal(CampaignKit.C.Brasil2026.National.CamaraVotes.Values.Sum(), valid);

        var engine = new PartyFoundingEngine(state, Config(), CampaignKit.C.World.Parties, CampaignKit.C.Municipios, CandidateNumberRulesConfig.Default, valid);
        Assert.True(engine.UsesRealVotes);
        Assert.Equal((long)Math.Ceiling(valid * 0.005), engine.NationalRequired);

        // Sem os votos reais (outros cenários), vale a aproximação pelo eleitorado.
        Assert.False(Engine(state, Config()).UsesRealVotes);
    }

    [Fact]
    public void Proposta_e_valida_e_sigla_repetida_ou_bloqueada_e_recusada()
    {
        GameState state = OutOfOffice();
        PartyFoundingEngine engine = Engine(state, Config());
        GameParty draft = engine.Propose(new Rng(1), state.Parties[0].FamilyId);
        Assert.Null(engine.CheckDraft(draft));

        draft.Sigla = state.Parties[0].Sigla;
        Assert.Equal(FoundingFailure.SiglaInvalida, engine.CheckDraft(draft));

        draft.Sigla = "PNX";
        draft.Name = state.Parties[0].Name;
        Assert.Equal(FoundingFailure.NomeInvalido, engine.CheckDraft(draft));

        draft.Name = "Partido dos Trabalhadores";
        Assert.Equal(FoundingFailure.NomeInvalido, engine.CheckDraft(draft));
    }

    [Fact]
    public void Coleta_registro_e_filiacao_ao_partido_fundado()
    {
        GameState state = OutOfOffice();
        PartyFoundingEngine engine = Engine(state, Config(impugnation: 0));
        GameParty draft = engine.Propose(new Rng(2), state.Parties[0].FamilyId);
        int capital = state.Player.Resources.PoliticalCapital;
        Assert.Null(engine.Start(draft));
        Assert.Equal(capital - engine.Config.CapitalCost, state.Player.Resources.PoliticalCapital);
        Assert.Equal(FoundingFailure.JaEmCurso, engine.CanStart(draft));

        string[] big = ["SP", "MG", "RJ"];
        string[][] rounds = [big, ["BA", "PR", "RS"], ["PE", "CE", "PA"]];
        int weeks = 0;
        while (!engine.Ready)
        {
            Assert.True(++weeks < 104, "A coleta não terminou no prazo.");
            Assert.True(engine.Collect("coletores_pagos", rounds[weeks % 3]) > 0);
            Assert.Equal(FoundingFailure.JaColetouNestaSemana, engine.CanCollect(engine.Config.Intensities[0], big));
            CareerFlow.AdvanceWeek(state, null, null, engine);
        }

        Assert.True(engine.StatesDone >= engine.Config.MinStates);
        Assert.True(state.Founding!.Risk > 0, "Coletores pagos acumulam risco.");
        state.Founding.Risk = 0; // este caso testa o registro; a impugnação tem teste próprio.
        Assert.Null(engine.Submit());
        Assert.Equal(FoundingStage.Registro, state.Founding!.Stage);
        for (int i = 0; i < 30 && state.Founding.Stage == FoundingStage.Registro; i++)
        {
            CareerFlow.AdvanceWeek(state, null, null, engine);
        }

        Assert.Equal(FoundingStage.Registrado, state.Founding.Stage);
        Assert.Contains(state.Parties, p => p.Id == draft.Id && p.RegisteredOn is not null);

        PartyAffiliation.Join(state, draft.Id);
        Assert.Equal(draft.Id, state.Player.PartyId);
        int year = CareerFlow.NextMunicipalElectionYear(state);
        Assert.Equal(draft.RegisteredOn <= Calendar.ElectionFor(year).PartyAffiliationDeadline && state.Player.AffiliatedOn <= Calendar.ElectionFor(year).PartyAffiliationDeadline, PartyAffiliation.CanRun(state, year));
    }

    [Fact]
    public void Impugnacao_derruba_assinaturas_e_o_prazo_expira()
    {
        GameState state = OutOfOffice();
        PartyFoundingEngine engine = Engine(state, Config(impugnation: 1));
        GameParty draft = engine.Propose(new Rng(3), state.Parties[0].FamilyId);
        Assert.Null(engine.Start(draft));
        PartyFoundingState founding = state.Founding!;
        foreach (string uf in engine.States)
        {
            founding.Signatures[uf] = engine.StateRequired(uf) * 40;
        }

        Assert.True(engine.Ready);
        long before = founding.Total;
        Assert.Null(engine.Submit());
        for (int i = 0; i < 30 && founding.Stage == FoundingStage.Registro; i++)
        {
            CareerFlow.AdvanceWeek(state, null, null, engine);
        }

        Assert.Equal(FoundingStage.Assinaturas, founding.Stage);
        Assert.Equal(1, founding.Impugnations);
        Assert.True(founding.Total < before);
        Assert.DoesNotContain(state.Parties, p => p.Id == draft.Id);

        state.Calendar.JumpTo(founding.Started.AddDays(7 * engine.Config.DeadlineWeeks));
        engine.Tick();
        Assert.Equal(FoundingStage.Expirado, founding.Stage);
    }
}
