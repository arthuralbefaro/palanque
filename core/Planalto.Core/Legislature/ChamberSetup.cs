using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Electorate;
using Planalto.Core.Elections;
using Planalto.Core.Ideology;
using Planalto.Core.Municipios;
using Planalto.Core.State;

namespace Planalto.Core.Legislature;

/// <summary>Tudo o que o mandato precisa, derivado do estado e do conteúdo (como o CampaignContext).</summary>
public sealed class LegislatureContext
{
    public required GameState State { get; init; }

    public required Municipio Municipio { get; init; }

    public required LegislatureContent Content { get; init; }

    public required PeopleContent People { get; init; }

    /// <summary>Eleitorado do município: dá o peso de cada grupo na reação da cidade a um voto.</summary>
    public required MunicipalElectorate Electorate { get; init; }

    /// <summary>Escala de reconhecimento do porte (a mesma da campanha).</summary>
    public required double RecognitionScale { get; init; }

    public LegislatureConfig Config => Content.Config;
}

/// <summary>
/// Posse: monta a Câmara a partir da apuração (eleitos nomeados, anônimos que viram NPCs e o jogador), o prefeito
/// eleito, a base do governo por proximidade ideológica, os líderes de bancada e a comissão do jogador.
/// </summary>
public static class ChamberSetup
{
    /// <summary>Política local depois de uma eleição municipal jogada, com ou sem o jogador eleito.</summary>
    public static LocalPolitics RecordLocalPolitics(LegislatureContext ctx, CampaignState campaign, Rng rng)
    {
        ElectionOutcome outcome = campaign.Outcome ?? throw new InvalidOperationException("Eleição sem resultado.");
        var votes = new Dictionary<string, long>(StringComparer.Ordinal);
        var councillors = new List<string>();
        foreach (string electedId in outcome.Vereador.Elected)
        {
            string characterId = CouncillorCharacter(ctx, campaign, electedId, rng);
            councillors.Add(characterId);
            votes[characterId] = ElectedVotes(outcome, campaign, electedId);
        }

        Candidate mayor = campaign.Find(outcome.Prefeito.Elected[0])!;
        LocalPolitics? previous = ctx.State.Local;
        // Reeleito: era o prefeito em exercício (da eleição jogada antes ou, na primeira, do cenário).
        bool reelected = mayor.Incumbent && (previous is null || previous.MayorId == mayor.CharacterId);
        Candidate player = campaign.Player;
        var local = new LocalPolitics
        {
            Municipio = campaign.Municipio,
            ElectionYear = outcome.Date.Year,
            ChamberSeats = new Dictionary<string, int>(outcome.Vereador.SeatsByParty, StringComparer.Ordinal),
            Councillors = councillors,
            CouncillorVotes = votes,
            MayorId = mayor.IsPlayer ? RaceSetup.PlayerId : mayor.CharacterId,
            MayorPartyId = mayor.PartyId,
            MayorReelected = reelected,
            PlayerImage = player.Image,
            PlayerSupport = new Dictionary<string, double>(player.Support, StringComparer.Ordinal),
        };
        ctx.State.Local = local;
        return local;
    }

    /// <summary>Começa o mandato de vereador em 1º de janeiro do ano seguinte à eleição.</summary>
    public static MandateState StartMandate(LegislatureContext ctx, CampaignState campaign, Rng rng)
    {
        GameState state = ctx.State;
        LegislatureConfig config = ctx.Config;
        ElectionOutcome outcome = campaign.Outcome!;
        LocalPolitics local = state.Local ?? RecordLocalPolitics(ctx, campaign, rng);
        int year = outcome.Date.Year;
        List<ChamberMember> members = BuildMembers(state, campaign, local, rng);

        Candidate mayor = campaign.Find(outcome.Prefeito.Elected[0])!;
        var baseParties = BuildBase(state, members, mayor.PartyId, config.Base);
        foreach (var group in members.Where(m => m.PartyId is not null).GroupBy(m => m.PartyId))
        {
            group.OrderByDescending(m => m.Votes).ThenBy(m => m.Id, StringComparer.Ordinal).First().Leader = true;
        }

        ChamberMember player = members.First(m => m.IsPlayer);
        foreach (ChamberMember m in members.Where(m => !m.IsPlayer))
        {
            double relation = rng.NextDouble(-10, 10);
            relation += m.PartyId == player.PartyId ? 20 : 0;
            relation += baseParties.Contains(m.PartyId ?? "") == baseParties.Contains(player.PartyId ?? "") ? 5 : -5;
            m.Relation = Math.Clamp(relation, -100, 100);
        }

        Candidate playerCandidate = campaign.Player;
        var mandate = new MandateState
        {
            Office = Office.Vereador,
            Municipio = campaign.Municipio,
            Start = new DateOnly(year + 1, 1, 1),
            End = new DateOnly(year + 4, 12, 31),
            Members = members,
            MayorId = mayor.CharacterId ?? mayor.Id,
            MayorName = mayor.Name,
            MayorPartyId = mayor.PartyId,
            BaseParties = baseParties,
            PresidentId = members.Where(m => !m.IsPlayer && baseParties.Contains(m.PartyId ?? "")).OrderByDescending(m => m.Votes).FirstOrDefault()?.Id,
            Committee = rng.Pick(config.Committees).Id,
            MayorRelation = baseParties.Contains(player.PartyId ?? "") ? 15 : -10,
            Fidelity = config.Fidelity.Start,
            EnergyLeft = config.WeeklyEnergy,
            EmendasLeft = config.EmendasPerYear,
            EmendasYear = year + 1,
            Recognition = playerCandidate.Recognition,
            Image = playerCandidate.Image,
            Support = new Dictionary<string, double>(playerCandidate.Support, StringComparer.Ordinal),
        };
        state.Player.Resources.PoliticalCapital = Math.Max(state.Player.Resources.PoliticalCapital, config.Capital.Start);
        return mandate;
    }

    /// <summary>Vereadores empossados (o jogador, se eleito, e os NPCs), com posição e votos.</summary>
    public static List<ChamberMember> BuildMembers(GameState state, CampaignState campaign, LocalPolitics local, Rng rng)
    {
        var members = new List<ChamberMember>();
        foreach (string characterId in local.Councillors)
        {
            if (characterId == RaceSetup.PlayerId)
            {
                members.Add(new ChamberMember
                {
                    Id = RaceSetup.PlayerId,
                    Name = state.Player.BallotName,
                    PartyId = state.Player.PartyId,
                    Position = state.Party(state.Player.PartyId)?.Position ?? new IdeologicalPosition(0, 0),
                    IsPlayer = true,
                    Votes = local.CouncillorVotes.GetValueOrDefault(RaceSetup.PlayerId),
                });
                continue;
            }

            Character npc = state.Npcs.First(n => n.Id == characterId);
            Candidate? candidate = campaign.Candidates.FirstOrDefault(c => c.CharacterId == characterId);
            members.Add(new ChamberMember
            {
                Id = npc.Id,
                Name = npc.BallotName,
                PartyId = npc.PartyId,
                Position = candidate?.Position ?? Jitter(state.Party(npc.PartyId)?.Position ?? new IdeologicalPosition(0, 0), rng),
                Votes = local.CouncillorVotes.GetValueOrDefault(npc.Id),
            });
        }

        return members;
    }

    /// <summary>
    /// Base do prefeito: o partido dele e os mais próximos ideologicamente (até a distância máxima), adicionados
    /// do mais perto para o mais longe até somar maioria com folga; governo minoritário se não chegar lá.
    /// </summary>
    public static List<string> BuildBase(GameState state, IReadOnlyList<ChamberMember> members, string mayorPartyId, BaseConfig config)
    {
        var seats = members.Where(m => m.PartyId is not null).GroupBy(m => m.PartyId!).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        IdeologicalPosition mayorPosition = state.Party(mayorPartyId)?.Position ?? new IdeologicalPosition(0, 0);
        int needed = (members.Count / 2) + config.MajorityMargin;
        var result = new List<string> { mayorPartyId };
        int total = seats.GetValueOrDefault(mayorPartyId);
        foreach (string party in seats.Keys
            .Where(p => p != mayorPartyId)
            .Select(p => (Id: p, Distance: Distance(state.Party(p)?.Position ?? mayorPosition, mayorPosition)))
            .Where(p => p.Distance <= config.MaxDistance)
            .OrderBy(p => p.Distance)
            .ThenBy(p => p.Id, StringComparer.Ordinal)
            .Select(p => p.Id))
        {
            if (total >= needed)
            {
                break;
            }

            result.Add(party);
            total += seats[party];
        }

        return result;
    }

    public static double Distance(IdeologicalPosition a, IdeologicalPosition b) =>
        Math.Sqrt(Math.Pow(a.Economic - b.Economic, 2) + Math.Pow(a.Social - b.Social, 2));

    /// <summary>Personagem de um eleito: o jogador, o NPC do candidato nomeado ou um NPC novo para um anônimo.</summary>
    private static string CouncillorCharacter(LegislatureContext ctx, CampaignState campaign, string electedId, Rng rng)
    {
        if (electedId == RaceSetup.PlayerId)
        {
            return RaceSetup.PlayerId;
        }

        if (campaign.Find(electedId)?.CharacterId is { } characterId)
        {
            return characterId;
        }

        // Anônimo do candidato coletivo ("demais_{partido}_{n}"): vira um NPC de verdade ao tomar posse.
        string partyId = campaign.Candidates.First(c => c.IsBulk && electedId.StartsWith(c.Id + "_", StringComparison.Ordinal)).PartyId;
        Municipio m = ctx.Municipio;
        Character npc = PersonGenerator.Generate(rng, ctx.People, new PersonRequest(
            $"npc_{m.CodigoIbge}_{ctx.State.Npcs.Count}_{electedId[(electedId.LastIndexOf('_') + 1)..]}", m.Uf, m.CodigoIbge, ctx.State.Calendar.WeekStart, PartyId: partyId));
        ctx.State.Npcs.Add(npc);
        return npc.Id;
    }

    /// <summary>Votos de um eleito; o anônimo "demais_{partido}_{n}" tem os votos da n-ésima parte do coletivo.</summary>
    private static long ElectedVotes(ElectionOutcome outcome, CampaignState campaign, string electedId)
    {
        if (outcome.Vereador.Votes.TryGetValue(electedId, out long votes))
        {
            return votes;
        }

        Candidate bulk = campaign.Candidates.First(c => c.IsBulk && electedId.StartsWith(c.Id + "_", StringComparison.Ordinal));
        return outcome.BulkVotes.TryGetValue(bulk.PartyId, out List<long>? split)
            && int.TryParse(electedId[(bulk.Id.Length + 1)..], out int index) && index >= 1 && index <= split.Count
            ? split[index - 1]
            : 0;
    }

    private static IdeologicalPosition Jitter(IdeologicalPosition p, Rng rng) => new(
        Math.Clamp(p.Economic + rng.NextInt(-8, 9), AxisRange.AxisMin, AxisRange.AxisMax),
        Math.Clamp(p.Social + rng.NextInt(-8, 9), AxisRange.AxisMin, AxisRange.AxisMax));
}
