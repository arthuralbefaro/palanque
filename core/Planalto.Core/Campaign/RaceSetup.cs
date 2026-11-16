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

    public PorteCatalog? Portes { get; init; }

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
            Recognition = player.Resources.GetRecognition(constituency),
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
            int incumbentSeats = ctx.Local?.Camara.GetValueOrDefault(partyId) ?? 0;
            for (int i = 0; i < rivalsPerParty[partyId]; i++)
            {
                Character npc = NewNpc(ctx, rng, ref serial, partyId, age: null);
                bool incumbent = i < incumbentSeats;
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
            bulk.Multiplicity = Math.Max(1, m.VagasVereador + 1 - named);
        }

        candidates.AddRange(MayorCandidates(ctx, rng, ref serial, numbers, constituency, recognitionScale));

        return new CampaignState
        {
            Municipio = m.CodigoIbge,
            ElectionDate = election,
            CampaignStart = config.DateIn(year, config.CampaignStart),
            Candidates = candidates,
            EnergyLeft = config.WeeklyEnergy,
            Phase = state.Calendar.WeekStart >= config.DateIn(year, config.CampaignStart) ? CampaignPhase.Campanha : CampaignPhase.PreCampanha,
        };
    }

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

        // O prefeito em exercício disputa a reeleição, salvo se já está no 2º mandato (D-007).
        string? incumbentParty = ctx.Local is { Prefeito: not null, PrefeitoReeleito: false } local ? local.Prefeito : null;
        if (incumbentParty is not null && !parties.Contains(incumbentParty) && ctx.State.Party(incumbentParty) is not null)
        {
            parties[^1] = incumbentParty;
        }

        var result = new List<Candidate>();
        foreach (string partyId in parties)
        {
            GameParty party = ctx.State.Party(partyId)!;
            Character npc = NewNpc(ctx, rng, ref serial, partyId, age: rng.NextInt(35, 66));
            numbers.TryReserve(constituency, Office.Prefeito, party.Number, 0, out CandidateNumber number);
            bool incumbent = partyId == incumbentParty;
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
