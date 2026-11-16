using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Data;
using Planalto.Core.Elections;
using Planalto.Core.State;

namespace Planalto.Core.Debate;

public sealed class CardOutcome
{
    /// <summary>Pontos de quem joga.</summary>
    public required double Self { get; init; }

    /// <summary>Pontos do alvo (cartas de ataque).</summary>
    public required double Target { get; init; }
}

/// <summary>Carta do debate (GDD 5.7): testa um atributo; o resultado vale pontos diante de quem assiste.</summary>
public sealed class DebateCard
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required AttributeId Attribute { get; init; }

    public required bool Targeted { get; init; }

    /// <summary>Defesa: reduz à metade os ataques sofridos na rodada.</summary>
    public required bool Blocks { get; init; }

    /// <summary>Rende mais quando o tema da rodada pesa para o eleitorado.</summary>
    public required bool TopicSensitive { get; init; }

    public required CardOutcome Success { get; init; }

    public required CardOutcome Failure { get; init; }
}

/// <summary>Estilo de um rival no debate: peso de cada carta na escolha da IA.</summary>
public sealed class DebateStyle
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required Dictionary<string, double> Weights { get; init; }
}

/// <summary>Regras e cartas do debate (data/debate/debate.json).</summary>
public sealed class DebateContent : IValidatable
{
    public const string File = "debate/debate.json";

    public required int Rounds { get; init; }

    public required int HandSize { get; init; }

    /// <summary>Semanas antes do 1º turno em que há debate na TV.</summary>
    public required List<int> WeeksBeforeElection { get; init; }

    /// <summary>Portes com debate de prefeito na TV.</summary>
    public required List<string> Portes { get; init; }

    public required double ImagePerPoint { get; init; }

    public required double ExposureRecognition { get; init; }

    public required double ViralChance { get; init; }

    public required double ViralImage { get; init; }

    public required double DeclineImage { get; init; }

    public required double TopicBonus { get; init; }

    public required string HotTemperedTrait { get; init; }

    public required double HotTemperedMultiplier { get; init; }

    public required List<DebateCard> Cards { get; init; }

    public required List<DebateStyle> Styles { get; init; }

    public DebateCard Card(string id) => Cards.First(c => c.Id == id);

    public IEnumerable<string> Validate()
    {
        if (Rounds < 1 || HandSize < 1 || HandSize > Cards.Count || WeeksBeforeElection.Any(w => w < 1))
        {
            yield return "rodadas e mão positivas (mão até o número de cartas); semanas antes da eleição positivas.";
        }

        var ids = Cards.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        if (ids.Count != Cards.Count)
        {
            yield return "cartas com id repetido.";
        }

        foreach (DebateStyle style in Styles)
        {
            foreach (string card in style.Weights.Keys.Where(k => !ids.Contains(k)))
            {
                yield return $"estilo {style.Id}: carta '{card}' inexistente.";
            }
        }

        if (Styles.Count == 0 || ImagePerPoint < 0 || ViralChance is < 0 or > 1)
        {
            yield return "ao menos um estilo; imagem por ponto não negativa; chance de viral entre 0 e 1.";
        }
    }
}

public sealed record DebatePlay(int Round, string ParticipantId, string CardId, bool Success, string? TargetId, double Points);

/// <summary>Debate em curso ou recém-terminado na campanha.</summary>
public sealed class DebateState
{
    public required DateOnly Week { get; init; }

    /// <summary>Ids dos candidatos; o jogador primeiro.</summary>
    public required List<string> Participants { get; init; }

    /// <summary>Estilo de cada rival.</summary>
    public required Dictionary<string, string> Styles { get; init; }

    public required List<string> Topics { get; init; }

    public int Round { get; set; }

    public required Dictionary<string, double> Scores { get; init; }

    public List<string> Hand { get; set; } = [];

    public List<DebatePlay> Log { get; init; } = [];

    public List<string> Viral { get; init; } = [];

    public bool Finished { get; set; }

    public string? Topic => Round < Topics.Count ? Topics[Round] : null;
}

/// <summary>
/// Minigame do debate (GDD 5.7): rodadas temáticas, uma carta por participante em cada rodada, rivais com IA por
/// estilo e resultado convertido em imagem e reconhecimento na campanha. Toda aleatoriedade vem do Rng do estado.
/// </summary>
public static class DebateEngine
{
    /// <summary>Dias em que há debate de prefeito na TV nesta campanha.</summary>
    public static IEnumerable<DateOnly> Dates(CampaignState campaign, DebateContent content) =>
        content.WeeksBeforeElection.Select(w => campaign.ElectionDate.AddDays(-7 * w));

    /// <summary>Há debate nesta semana: candidato a prefeito, porte com TV, campanha oficial e data do calendário.</summary>
    public static bool ShouldSchedule(CampaignContext ctx, DebateContent content)
    {
        CampaignState campaign = ctx.State.Campaign!;
        return campaign.Player.Office == Office.Prefeito
            && campaign.Debate is null
            && campaign.Phase == CampaignPhase.Campanha
            && content.Portes.Contains(ctx.Municipio.Porte)
            && Dates(campaign, content).Any(d => ctx.State.Calendar.Contains(d) && !campaign.DebatesHeld.Contains(d));
    }

    public static DebateState Start(CampaignContext ctx, DebateContent content, Rng rng)
    {
        CampaignState campaign = ctx.State.Campaign!;
        DateOnly date = Dates(campaign, content).First(d => ctx.State.Calendar.Contains(d));
        var rivals = campaign.ForOffice(Office.Prefeito).Where(c => !c.IsPlayer).OrderByDescending(c => c.Recognition).Take(4).ToList();
        var styles = rivals.ToDictionary(
            r => r.Id,
            r => NpcOf(ctx, r)?.TraitIds.Contains(content.HotTemperedTrait) == true ? "agressivo" : rng.Pick(content.Styles).Id,
            StringComparer.Ordinal);

        // Temas sorteados pelo peso que têm para o eleitorado da cidade.
        var priority = Priorities(ctx);
        var topics = new List<string>();
        var pool = priority.Keys.ToList();
        for (int i = 0; i < content.Rounds && pool.Count > 0; i++)
        {
            string topic = rng.PickWeighted(pool, t => priority[t]);
            topics.Add(topic);
            pool.Remove(topic);
        }

        var participants = new List<string> { campaign.Player.Id };
        participants.AddRange(rivals.Select(r => r.Id));
        var state = new DebateState
        {
            Week = date,
            Participants = participants,
            Styles = styles,
            Topics = topics,
            Scores = participants.ToDictionary(p => p, _ => 0.0, StringComparer.Ordinal),
        };
        state.Hand = Draw(content, rng);
        return state;
    }

    /// <summary>Recusa o debate: a cadeira vazia custa imagem.</summary>
    public static void Decline(CampaignContext ctx, DebateContent content)
    {
        CampaignState campaign = ctx.State.Campaign!;
        DebateState debate = campaign.Debate ?? throw new InvalidOperationException("Sem debate marcado.");
        campaign.Player.Image = Math.Clamp(campaign.Player.Image + content.DeclineImage, -1, 1);
        campaign.DebatesHeld.Add(debate.Week);
        campaign.Debate = null;
    }

    /// <summary>Joga a carta do jogador e resolve a rodada inteira; na última, aplica o resultado à campanha.</summary>
    public static IReadOnlyList<DebatePlay> Play(CampaignContext ctx, DebateContent content, string cardId, string? targetId, Rng rng)
    {
        CampaignState campaign = ctx.State.Campaign!;
        DebateState debate = campaign.Debate ?? throw new InvalidOperationException("Sem debate marcado.");
        if (debate.Finished || !debate.Hand.Contains(cardId))
        {
            throw new ArgumentException($"Carta '{cardId}' fora da mão.", nameof(cardId));
        }

        DebateCard playerCard = content.Card(cardId);
        string player = debate.Participants[0];
        if (playerCard.Targeted && (targetId is null || targetId == player || !debate.Participants.Contains(targetId)))
        {
            throw new ArgumentException("Ataque precisa de um adversário.", nameof(targetId));
        }

        // Escolhas da rodada: o jogador e cada rival pelo estilo (ataques miram o mais bem colocado entre os outros).
        var choices = new List<(string Id, DebateCard Card, string? Target)> { (player, playerCard, playerCard.Targeted ? targetId : null) };
        foreach (string rival in debate.Participants.Skip(1))
        {
            DebateStyle style = content.Styles.First(s => s.Id == debate.Styles[rival]);
            DebateCard card = rng.PickWeighted(content.Cards, c => style.Weights.GetValueOrDefault(c.Id));
            string? target = card.Targeted ? debate.Participants.Where(p => p != rival).OrderByDescending(p => debate.Scores[p]).First() : null;
            choices.Add((rival, card, target));
        }

        double topicFactor = TopicFactor(ctx, content, debate.Topic);
        var results = choices.Select(c => (c.Id, c.Card, c.Target, Success: rng.NextDouble() < Chance(ctx, c.Id, c.Card))).ToList();
        var blocking = results.Where(r => r.Card.Blocks && r.Success).Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        var plays = new List<DebatePlay>();
        foreach (var r in results)
        {
            CardOutcome outcome = r.Success ? r.Card.Success : r.Card.Failure;
            double multiplier = (r.Card.TopicSensitive ? topicFactor : 1) * (r.Card.Targeted && HotTempered(ctx, content, r.Id) ? content.HotTemperedMultiplier : 1);
            double self = outcome.Self * multiplier;
            debate.Scores[r.Id] += self;
            if (r.Target is not null && outcome.Target != 0)
            {
                debate.Scores[r.Target] += outcome.Target * multiplier * (blocking.Contains(r.Target) ? 0.5 : 1);
            }

            var play = new DebatePlay(debate.Round, r.Id, r.Card.Id, r.Success, r.Target, self);
            debate.Log.Add(play);
            plays.Add(play);
        }

        debate.Round++;
        if (debate.Round >= debate.Topics.Count)
        {
            Finish(ctx, content, debate, rng);
        }
        else
        {
            debate.Hand = Draw(content, rng);
        }

        return plays;
    }

    /// <summary>Fecha o debate já terminado (a tela de resultado foi vista).</summary>
    public static void Close(CampaignState campaign)
    {
        if (campaign.Debate is { Finished: true } debate)
        {
            campaign.DebatesHeld.Add(debate.Week);
            campaign.Debate = null;
        }
    }

    private static void Finish(CampaignContext ctx, DebateContent content, DebateState debate, Rng rng)
    {
        CampaignState campaign = ctx.State.Campaign!;
        double mean = debate.Scores.Values.Average();
        double scale = ctx.Config.PorteRecognitionMultiplier.GetValueOrDefault(ctx.Municipio.Porte, 1);
        foreach (string id in debate.Participants)
        {
            Candidate candidate = campaign.Find(id)!;
            candidate.Image = Math.Clamp(candidate.Image + ((debate.Scores[id] - mean) * content.ImagePerPoint), -1, 1);
            campaign.Territory.GainEverywhere(id, content.ExposureRecognition * scale);
            candidate.Recognition = campaign.Territory.Recognition(id);

            // Corte viral: um ataque ou uma emoção que deu muito certo (ou muito errado) circula depois do debate.
            var striking = debate.Log.Where(p => p.ParticipantId == id && p.CardId is "ataque" or "emocao").ToList();
            if (striking.Count > 0 && rng.NextDouble() < content.ViralChance)
            {
                bool good = striking.Count(p => p.Success) * 2 >= striking.Count;
                candidate.Image = Math.Clamp(candidate.Image + (good ? content.ViralImage : -content.ViralImage), -1, 1);
                debate.Viral.Add(id);
            }
        }

        debate.Hand = [];
        debate.Finished = true;
    }

    private static List<string> Draw(DebateContent content, Rng rng) =>
        rng.Shuffled(content.Cards.Select(c => c.Id).ToList()).Take(content.HandSize).ToList();

    private static double Chance(CampaignContext ctx, string candidateId, DebateCard card)
    {
        Character? person = candidateId == RaceSetup.PlayerId ? ctx.State.Player : NpcOf(ctx, ctx.State.Campaign!.Find(candidateId)!);
        int attribute = person?.Attributes.Get(card.Attribute) ?? 50;
        return Math.Clamp(attribute / 100.0, 0.1, 0.9);
    }

    private static bool HotTempered(CampaignContext ctx, DebateContent content, string candidateId)
    {
        Character? person = candidateId == RaceSetup.PlayerId ? ctx.State.Player : NpcOf(ctx, ctx.State.Campaign!.Find(candidateId)!);
        return person?.TraitIds.Contains(content.HotTemperedTrait) == true;
    }

    private static Character? NpcOf(CampaignContext ctx, Candidate candidate) =>
        candidate.CharacterId is null ? null : ctx.State.Npcs.FirstOrDefault(n => n.Id == candidate.CharacterId);

    /// <summary>Peso médio de cada tema para o eleitorado da cidade (ponderado pelos eleitores).</summary>
    private static Dictionary<string, double> Priorities(CampaignContext ctx)
    {
        var sums = new Dictionary<string, double>(StringComparer.Ordinal);
        double voters = Math.Max(1, ctx.Electorate.Voters);
        foreach (var segment in ctx.Electorate.Segments)
        {
            foreach (var (theme, priority) in segment.ThemePriority)
            {
                sums[theme] = sums.GetValueOrDefault(theme) + (priority * segment.Voters / voters);
            }
        }

        return sums.Where(s => s.Value > 0).ToDictionary(s => s.Key, s => s.Value, StringComparer.Ordinal);
    }

    private static double TopicFactor(CampaignContext ctx, DebateContent content, string? topic)
    {
        var priority = Priorities(ctx);
        if (topic is null || priority.Count == 0)
        {
            return 1;
        }

        double average = priority.Values.Average();
        return Math.Max(0.5, 1 + (content.TopicBonus * ((priority.GetValueOrDefault(topic) / average) - 1)));
    }
}
