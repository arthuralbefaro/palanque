using System.Globalization;
using Planalto.Core.Characters;
using Planalto.Core.Electorate;
using Planalto.Core.Elections;
using Planalto.Core.Ideology;
using Planalto.Core.Municipios;
using Planalto.Core.Scenarios;
using Planalto.Core.State;
using Calendar = Planalto.Core.State.Calendar;

namespace Planalto.Core.Campaign;

/// <summary>Tudo o que a simulação detalhada do município em foco precisa (GDD 12.5), derivado do estado e do conteúdo.</summary>
public sealed class CampaignContext
{
    public required GameState State { get; init; }

    public required Municipio Municipio { get; init; }

    public required MunicipalElectorate Electorate { get; init; }

    public required MunicipalScenario? Local { get; init; }

    public required PeopleContent People { get; init; }

    public required ElectorateContent ElectorateContent { get; init; }

    public required CampaignContent Campaign { get; init; }

    public required ThemeCatalog Themes { get; init; }

    public Events.EventContent? Events { get; init; }

    /// <summary>Regras do debate; sem elas, não há debate na campanha.</summary>
    public Debate.DebateContent? Debate { get; init; }

    /// <summary>Projeção da população; sem ela, as vagas são as de data/municipios/.</summary>
    public PopulationProjection? Projection { get; init; }

    /// <summary>Vagas de vereador na eleição do ano (Fase 6: população projetada e teto do art. 29, IV).</summary>
    public int SeatsIn(int electionYear) => Projection?.Seats(Municipio, electionYear) ?? Municipio.VagasVereador;

    public PorteCatalog? Portes { get; init; }

    /// <summary>Zonas do município em foco (data/zonas/, GDD 17.5).</summary>
    public required Territory.ZonasMunicipio Zonas { get; init; }

    private IReadOnlyList<Territory.ZoneElectorate>? _zones;

    /// <summary>Os segmentos do município repartidos pelas zonas (montado uma vez por contexto).</summary>
    public IReadOnlyList<Territory.ZoneElectorate> ZoneElectorates =>
        _zones ??= Planalto.Core.Territory.ZoneElectorateBuilder.Build(Electorate, Zonas, Municipio, ElectorateContent.Segments, Campaign.Territory);

    /// <summary>Zonas do município pelo arquivo da UF.</summary>
    public static Territory.ZonasMunicipio LoadZonas(string dataDirectory, Municipio municipio) =>
        Planalto.Core.Territory.ZonasPaths.Load(dataDirectory, municipio.Uf).GetValueOrThrow().Municipios.First(z => z.CodigoIbge == municipio.CodigoIbge);

    public CampaignConfig Config => Campaign.Config;

    /// <summary>Eleitorado do município pelo mesmo procedimento em qualquer momento (determinístico pela semente do mundo).</summary>
    public static MunicipalElectorate BuildElectorate(GameState state, Municipio municipio, ElectorateContent electorate, ThemeCatalog themes, IReadOnlyDictionary<Parties.PartySize, int> sizeWeights, MunicipalScenario? local) =>
        ElectorateBuilder.Build(municipio, electorate.Segments, electorate.VoteModel, themes, ElectoralParties(state, sizeWeights), local?.ForcaVereador, ElectorateRng(WorldSeed(state), municipio.CodigoIbge));

    /// <summary>Força dos partidos no município, idêntica à de <see cref="BuildElectorate"/>, sem montar os segmentos.</summary>
    public static IReadOnlyDictionary<string, double> PartyStrength(GameState state, int codigoIbge, ElectorateContent electorate, IReadOnlyList<ElectoralParty> parties, MunicipalScenario? local) =>
        PartyStrength(WorldSeed(state), codigoIbge, electorate, parties, local);

    /// <summary>
    /// A mesma força antes de a carreira existir (criação de personagem), a partir da semente que o mundo vai
    /// receber em <see cref="NewGame.Create"/>.
    /// </summary>
    public static IReadOnlyDictionary<string, double> PartyStrength(ulong worldSeed, int codigoIbge, ElectorateContent electorate, IReadOnlyList<ElectoralParty> parties, MunicipalScenario? local) =>
        ElectorateBuilder.Strength(parties, electorate.VoteModel, local?.ForcaVereador, ElectorateRng(worldSeed, codigoIbge)).Strength;

    public static List<ElectoralParty> ElectoralParties(GameState state, IReadOnlyDictionary<Parties.PartySize, int> sizeWeights) =>
        ElectoralParties(state.Parties, sizeWeights);

    public static List<ElectoralParty> ElectoralParties(IEnumerable<GameParty> parties, IReadOnlyDictionary<Parties.PartySize, int> sizeWeights) =>
        parties.Select(p => new ElectoralParty(p.Id, p.Position, sizeWeights[p.Size])).ToList();

    private static ulong WorldSeed(GameState state) => state.World?.Seed ?? 1;

    /// <summary>O eleitorado de cada município tem semente própria, derivada da semente do mundo.</summary>
    private static Rng ElectorateRng(ulong worldSeed, int codigoIbge) => new(worldSeed ^ (ulong)codigoIbge);
}

/// <summary>
/// Monta a disputa municipal quando o município entra em foco (GDD 12.5 e Fase 3): partidos na disputa,
/// jogador, rivais nomeados (gerados e persistidos como NPCs), candidato coletivo de cada partido e
/// candidatos a prefeito. Determinístico pelo Rng.
/// </summary>
public enum CandidacyIssue
{
    IdadeMinima,
    ForaDoPrazo,
}

public static class RaceSetup
{
    public const string PlayerId = "jogador";

    public static string BulkId(string partyId) => $"demais_{partyId}";

    public static CampaignState Create(CampaignContext ctx, Rng rng)
    {
        GameState state = ctx.State;
        Character player = state.Player;
        CampaignConfig config = ctx.Config;
        Municipio m = ctx.Municipio;
        int year = NextMunicipalYear(state.Calendar.Year);
        DateOnly election = Calendar.ElectionFor(year).FirstRound;
        double recognitionScale = config.PorteRecognitionMultiplier.GetValueOrDefault(m.Porte, 1);

        // Partidos na disputa: os mais fortes no município, sempre com o do jogador.
        var inRace = ctx.Electorate.PartyStrength.OrderByDescending(p => p.Value).Select(p => p.Key)
            .Take(config.PartiesInRace).ToList();
        if (player.PartyId is not null && !inRace.Contains(player.PartyId))
        {
            inRace[^1] = player.PartyId;
        }

        var numbers = new CandidateNumberBook();
        string constituency = m.CodigoIbge.ToString(CultureInfo.InvariantCulture);
        var candidates = new List<Candidate>();
        GameParty PartyOf(string id) => state.Party(id)!;

        // Câmara e prefeitura de uma eleição já jogada aqui: os incumbentes vêm dela, não do cenário.
        Legislature.LocalPolitics? played = state.Local?.Municipio == m.CodigoIbge ? state.Local : null;
        Legislature.MandateState? mandate = state.Mandate?.Municipio == m.CodigoIbge ? state.Mandate : null;

        // Jogador.
        GameParty playerParty = PartyOf(player.PartyId!);
        int playerSuffix = player.Campaign?.BallotNumber is { Length: 5 } n ? int.Parse(n[2..], CultureInfo.InvariantCulture) : rng.NextInt(1000);
        numbers.TryReserve(constituency, Office.Vereador, playerParty.Number, playerSuffix, out CandidateNumber playerNumber);
        candidates.Add(new Candidate
        {
            Id = PlayerId,
            Name = player.BallotName,
            PartyId = playerParty.Id,
            Office = Office.Vereador,
            Position = playerParty.Position,
            BirthDate = player.BirthDate,
            Gender = player.Gender,
            Number = playerNumber.Text,
            Recognition = mandate?.Recognition ?? player.Resources.GetRecognition(constituency),
            Image = mandate?.Image ?? played?.PlayerImage ?? 0,
            Support = new Dictionary<string, double>(mandate?.Support ?? played?.PlayerSupport ?? [], StringComparer.Ordinal),
            Incumbent = played?.Councillors.Contains(PlayerId) == true,
            Money = player.Resources.Money,
            IsPlayer = true,
        });

        // Candidato coletivo de cada partido: os demais candidatos sem nome próprio.
        foreach (string partyId in inRace)
        {
            GameParty party = PartyOf(partyId);
            candidates.Add(new Candidate
            {
                Id = BulkId(partyId),
                Name = party.Sigla,
                PartyId = partyId,
                Office = Office.Vereador,
                Position = party.Position,
                BirthDate = new DateOnly(1980, 1, 1),
                Number = party.Number.ToString(CultureInfo.InvariantCulture),
                Recognition = config.BulkRecognition * recognitionScale,
                IsBulk = true,
                Drive = config.BulkDrive,
            });
        }

        // Rivais nomeados repartidos pela força dos partidos (maiores restos).
        var strength = inRace.ToDictionary(p => p, p => ctx.Electorate.PartyStrength.GetValueOrDefault(p));
        var rivalsPerParty = Apportion(config.NamedRivals, strength);
        int serial = state.Npcs.Count;
        foreach (string partyId in inRace)
        {
            GameParty party = PartyOf(partyId);
            int incumbentSeats = played is null ? ctx.Local?.Camara.GetValueOrDefault(partyId) ?? 0 : 0;
            var sitting = played?.Councillors
                .Where(id => id != PlayerId)
                .Select(id => state.Npcs.FirstOrDefault(n => n.Id == id))
                .Where(n => n is not null && n.PartyId == partyId)
                .Select(n => n!)
                .ToList() ?? [];
            for (int i = 0; i < Math.Max(rivalsPerParty[partyId], sitting.Count); i++)
            {
                // Vereadores em exercício disputam a reeleição antes de surgirem nomes novos.
                Character npc = i < sitting.Count ? sitting[i] : NewNpc(ctx, rng, ref serial, partyId, age: null);
                bool incumbent = i < sitting.Count || i < incumbentSeats;
                CandidateNumber number = numbers.ReserveRandom(rng, constituency, Office.Vereador, party.Number)!.Value;
                candidates.Add(new Candidate
                {
                    Id = npc.Id,
                    Name = npc.BallotName,
                    PartyId = partyId,
                    Office = Office.Vereador,
                    Position = Jitter(party.Position, rng, 8),
                    BirthDate = npc.BirthDate,
                    Gender = npc.Gender,
                    Number = number.Text,
                    Recognition = (rng.NextInt(config.NpcRecognition.Min, config.NpcRecognition.Max + 1) + (incumbent ? config.IncumbentRecognitionBonus : 0)) * recognitionScale,
                    Incumbent = incumbent,
                    CharacterId = npc.Id,
                    Drive = rng.NextDouble(config.NpcDrive.Min, config.NpcDrive.Max),
                });
            }
        }

        // Lista cheia de vereador: vagas + 1 por partido (Lei 9.504, art. 10); o coletivo representa os não nomeados.
        foreach (Candidate bulk in candidates.Where(c => c.IsBulk))
        {
            int named = candidates.Count(c => !c.IsBulk && c.PartyId == bulk.PartyId);
            bulk.Multiplicity = Math.Max(1, ctx.SeatsIn(year) + 1 - named);
        }

        candidates.AddRange(MayorCandidates(ctx, rng, ref serial, numbers, constituency, recognitionScale));

        var campaignState = new CampaignState
        {
            Municipio = m.CodigoIbge,
            ElectionDate = election,
            CampaignStart = config.DateIn(year, config.CampaignStart),
            Candidates = candidates,
            Territory = BuildTerritory(ctx, candidates, rng),
            Phase = state.Calendar.WeekStart >= config.DateIn(year, config.CampaignStart) ? CampaignPhase.Campanha : CampaignPhase.PreCampanha,
        };

        // A cidade com gente (lideranças) nasce quando o município entra em foco e fica no estado do jogo.
        CityBuilder.Ensure(ctx, campaignState, rng);
        StaffBuilder.Ensure(ctx, campaignState);

        // Prefeito em exercício no 1º mandato: começa candidato à reeleição (pode trocar na pré-campanha).
        if (played?.MayorId == PlayerId && !played.MayorReelected)
        {
            SwitchOffice(ctx, campaignState, Office.Prefeito, rng, incumbent: true);
        }

        return campaignState;
    }

    /// <summary>
    /// Presença de partida por zona (GDD 17.5): o jogador é mais conhecido na zona em que mora (a zona urbana mais
    /// populosa); cada rival nomeado a vereador tem um ou dois redutos; candidatos a prefeito e o candidato coletivo
    /// de cada partido começam iguais em toda a cidade. A média pela população reproduz o reconhecimento sorteado.
    /// </summary>
    private static Territory.TerritoryState BuildTerritory(CampaignContext ctx, List<Candidate> candidates, Rng rng)
    {
        List<Territory.Zona> zonas = ctx.Zonas.Zonas;
        Territory.TerritorySimulation sim = ctx.Campaign.Territory.Simulacao;
        var territory = new Territory.TerritoryState
        {
            Zones = zonas.Select(z => z.Id).ToList(),
            Population = zonas.Select(z => z.Populacao).ToList(),
            HomeZone = zonas.IndexOf(zonas.Where(z => !z.IsRural).DefaultIfEmpty(zonas[0]).MaxBy(z => z.Populacao)!),
        };
        territory.MeetingSupport.AddRange(zonas.Select(_ => new Dictionary<string, double>(StringComparer.Ordinal)));

        foreach (Candidate c in candidates)
        {
            List<int> strong = [];
            double factor = 1;
            if (c.IsPlayer)
            {
                strong = [territory.HomeZone];
                factor = sim.ZonaNatalFator;
            }
            else if (!c.IsBulk && c.Office == Office.Vereador)
            {
                int count = zonas.Count >= 4 && rng.Chance(0.5) ? 2 : 1;
                while (strong.Count < Math.Min(count, zonas.Count))
                {
                    int pick = rng.PickWeighted(Enumerable.Range(0, zonas.Count).ToList(), i => strong.Contains(i) ? 0 : Math.Max(1, zonas[i].Populacao));
                    strong.Add(pick);
                }

                factor = sim.RedutoFator;
                territory.Redutos[c.Id] = strong;
            }

            territory.Seed(c.Id, c.Recognition, strong, factor);
            c.Recognition = territory.Recognition(c.Id);
        }

        return territory;
    }

    /// <summary>Por que o jogador não pode disputar o cargo agora, ou null.</summary>
    public static CandidacyIssue? CanRunFor(CampaignContext ctx, Office office, CandidacyRulesConfig? rules = null)
    {
        CampaignState campaign = ctx.State.Campaign!;
        if (campaign.Phase != CampaignPhase.PreCampanha)
        {
            return CandidacyIssue.ForaDoPrazo;
        }

        // Idade mínima na posse (1º de janeiro seguinte à eleição); com mínimo de 18 anos, no prazo do registro.
        rules ??= CandidacyRulesConfig.Default;
        var posse = new DateOnly(campaign.ElectionDate.Year + 1, 1, 1);
        DateOnly reference = rules.AgeReferenceDate(office, campaign.ElectionDate.Year, posse);
        return ctx.State.Player.AgeOn(reference) < rules.MinimumAgeFor(office) ? CandidacyIssue.IdadeMinima : null;
    }

    /// <summary>
    /// Muda o cargo que o jogador disputa (vereador ou prefeito) na pré-campanha. Disputando a prefeitura, ele
    /// ocupa a vaga de candidato do próprio partido; o NPC que estava nela passa a disputar a Câmara.
    /// </summary>
    public static CandidacyIssue? SwitchPlayerOffice(CampaignContext ctx, Office office, Rng rng)
    {
        if (ctx.State.Campaign!.Player.Office == office)
        {
            return null;
        }

        if (CanRunFor(ctx, office) is { } issue)
        {
            return issue;
        }

        SwitchOffice(ctx, ctx.State.Campaign, office, rng, incumbent: false);
        return null;
    }

    private static void SwitchOffice(CampaignContext ctx, CampaignState campaign, Office office, Rng rng, bool incumbent)
    {
        Candidate player = campaign.Player;

        GameParty party = ctx.State.Party(player.PartyId)!;
        var used = campaign.Candidates.Where(c => c.Office == Office.Vereador).Select(c => c.Number).ToHashSet(StringComparer.Ordinal);
        string VereadorNumber()
        {
            string number;
            do
            {
                number = $"{party.Number}{rng.NextInt(1000):000}";
            }
            while (used.Contains(number));
            used.Add(number);
            return number;
        }

        if (office == Office.Prefeito)
        {
            if (campaign.Candidates.FirstOrDefault(c => c.Office == Office.Prefeito && c.PartyId == player.PartyId) is { } own)
            {
                Replace(campaign, own, Copy(own, Office.Vereador, VereadorNumber()));
            }

            Replace(campaign, player, Copy(player, Office.Prefeito, party.Number.ToString(CultureInfo.InvariantCulture), incumbent));
        }
        else
        {
            Replace(campaign, player, Copy(player, Office.Vereador, VereadorNumber(), incumbent));
        }

        // A lista de vereadores do partido mudou: o coletivo representa os que sobraram.
        int seats = ctx.SeatsIn(campaign.ElectionDate.Year);
        foreach (Candidate bulk in campaign.Candidates.Where(c => c.IsBulk))
        {
            int named = campaign.Candidates.Count(c => !c.IsBulk && c.Office == Office.Vereador && c.PartyId == bulk.PartyId);
            bulk.Multiplicity = Math.Max(1, seats + 1 - named);
        }
    }

    private static void Replace(CampaignState campaign, Candidate old, Candidate replacement) =>
        campaign.Candidates[campaign.Candidates.IndexOf(old)] = replacement;

    private static Candidate Copy(Candidate c, Office office, string number, bool incumbent = false) => new()
    {
        Id = c.Id,
        Name = c.Name,
        PartyId = c.PartyId,
        Office = office,
        Position = c.Position,
        BirthDate = c.BirthDate,
        Gender = c.Gender,
        Number = number,
        Recognition = c.Recognition,
        Image = c.Image,
        Support = c.Support,
        Money = c.Money,
        IsPlayer = c.IsPlayer,
        IsBulk = c.IsBulk,
        Multiplicity = c.Multiplicity,
        Incumbent = incumbent,
        CharacterId = c.CharacterId,
        Drive = c.Drive,
    };

    /// <summary>A próxima eleição municipal no ano informado ou depois.</summary>
    public static int NextMunicipalYear(int year)
    {
        while (Calendar.ElectionKindOf(year) != ElectionKind.Municipal)
        {
            year++;
        }

        return year;
    }

    private static IEnumerable<Candidate> MayorCandidates(CampaignContext ctx, Rng rng, ref int serial, CandidateNumberBook numbers, string constituency, double recognitionScale)
    {
        CampaignConfig config = ctx.Config;
        int count = rng.NextInt(config.MayorCandidates.Min, config.MayorCandidates.Max + 1);
        var strength = ctx.Local?.ForcaPrefeito is { Count: > 0 } prefeito
            ? prefeito.ToDictionary(p => p.Key, p => (double)p.Value)
            : ctx.Electorate.PartyStrength.ToDictionary(p => p.Key, p => p.Value);
        var parties = strength.OrderByDescending(p => p.Value).Select(p => p.Key).Where(p => ctx.State.Party(p) is not null).Take(count).ToList();

        // O prefeito em exercício disputa a reeleição, salvo se já está no 2º mandato (D-007). Depois de uma
        // eleição jogada no município, o prefeito em exercício é o NPC eleito nela.
        Legislature.LocalPolitics? played = ctx.State.Local?.Municipio == ctx.Municipio.CodigoIbge ? ctx.State.Local : null;
        string? incumbentParty = played is not null
            ? played.MayorReelected || played.MayorId == PlayerId ? null : played.MayorPartyId
            : ctx.Local is { Prefeito: not null, PrefeitoReeleito: false } local ? local.Prefeito : null;
        Character? sittingMayor = played?.MayorId is { } mayorId && !played.MayorReelected ? ctx.State.Npcs.FirstOrDefault(n => n.Id == mayorId) : null;
        if (incumbentParty is not null && !parties.Contains(incumbentParty) && ctx.State.Party(incumbentParty) is not null)
        {
            parties[^1] = incumbentParty;
        }

        var result = new List<Candidate>();
        foreach (string partyId in parties)
        {
            GameParty party = ctx.State.Party(partyId)!;
            bool incumbent = partyId == incumbentParty;
            Character npc = incumbent && sittingMayor is not null ? sittingMayor : NewNpc(ctx, rng, ref serial, partyId, age: rng.NextInt(35, 66));
            numbers.TryReserve(constituency, Office.Prefeito, party.Number, 0, out CandidateNumber number);
            result.Add(new Candidate
            {
                Id = npc.Id,
                Name = npc.BallotName,
                PartyId = partyId,
                Office = Office.Prefeito,
                Position = Jitter(party.Position, rng, 6),
                BirthDate = npc.BirthDate,
                Gender = npc.Gender,
                Number = number.Text,
                Recognition = (rng.NextInt(config.MayorRecognition.Min, config.MayorRecognition.Max + 1) + (incumbent ? config.IncumbentRecognitionBonus : 0)) * Math.Min(1, recognitionScale * 1.5),
                Incumbent = incumbent,
                CharacterId = npc.Id,
                Drive = rng.NextDouble(config.MayorDrive.Min, config.MayorDrive.Max),
            });
        }

        return result;
    }

    private static Character NewNpc(CampaignContext ctx, Rng rng, ref int serial, string partyId, int? age)
    {
        Municipio m = ctx.Municipio;
        Character npc = PersonGenerator.Generate(rng, ctx.People, new PersonRequest(
            $"npc_{m.CodigoIbge}_{serial++}", m.Uf, m.CodigoIbge, ctx.State.Calendar.WeekStart, Age: age, PartyId: partyId));
        ctx.State.Npcs.Add(npc);
        return npc;
    }

    private static IdeologicalPosition Jitter(IdeologicalPosition p, Rng rng, int amount) => new(
        Math.Clamp(p.Economic + rng.NextInt(-amount, amount + 1), AxisRange.AxisMin, AxisRange.AxisMax),
        Math.Clamp(p.Social + rng.NextInt(-amount, amount + 1), AxisRange.AxisMin, AxisRange.AxisMax));

    private static Dictionary<string, int> Apportion(int total, Dictionary<string, double> weights)
    {
        double sum = weights.Values.Sum();
        var quotas = weights.ToDictionary(w => w.Key, w => sum <= 0 ? (double)total / weights.Count : total * w.Value / sum);
        var counts = quotas.ToDictionary(q => q.Key, q => (int)Math.Floor(q.Value));
        foreach (string key in quotas.OrderByDescending(q => q.Value - Math.Floor(q.Value)).ThenBy(q => q.Key, StringComparer.Ordinal).Take(total - counts.Values.Sum()).Select(q => q.Key))
        {
            counts[key]++;
        }

        return counts;
    }
}
