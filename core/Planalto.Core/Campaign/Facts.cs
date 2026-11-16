using Planalto.Core.State;

namespace Planalto.Core.Campaign;

/// <summary>Tipos de fato que os sistemas emitem (GDD 17.2, peça 5). As chaves são as mesmas do feed.</summary>
public static class FactKind
{
    public const string Compromisso = "compromisso";
    public const string RivalZona = "rival_zona";
    public const string RivalLideranca = "rival_lideranca";
    public const string Lideranca = "lideranca";
    public const string LiderancaPerdida = "lideranca_perdida";
    public const string Promessa = "promessa";
    public const string Posicao = "posicao";
    public const string Pesquisa = "pesquisa";
    public const string Evento = "evento";
    public const string Equipe = "equipe";
    public const string Doacao = "doacao";
    public const string Fundo = "fundo";
    public const string Contas = "contas";
    public const string Ataque = "ataque";
    public const string Resposta = "resposta";
    public const string Boato = "boato";
    public const string Pacto = "pacto";
    public const string Plano = "plano_rival";
    public const string Economia = "economia";
    public const string Cidade = "cidade";
    public const string TemaEmAlta = "tema_em_alta";
    public const string Esqueleto = "esqueleto";
    public const string Justica = "justica";
    public const string Anuncio = "anuncio";
    public const string HorarioGratuito = "horario_gratuito";
    public const string Investigacao = "investigacao";

    /// <summary>Morte de uma pessoa do mundo político (GDD 17.16): a notícia sai sempre em tom neutro.</summary>
    public const string Morte = "morte";
}

/// <summary>
/// Fato (GDD 17.2, peça 5): quem fez o quê, onde, quando, com visibilidade de 0 a 3 e tema quando houver. Todo
/// sistema emite fatos; a mídia local transforma os de visibilidade 1 ou mais em notícias. Fato de visibilidade 0 só
/// é conhecido por quem estava presente.
/// </summary>
public sealed class Fact
{
    public required string Id { get; init; }

    /// <summary>Número de ordem (para os veículos saberem o que já viram).</summary>
    public required int Seq { get; init; }

    public required DateOnly Date { get; init; }

    public required string Kind { get; init; }

    /// <summary>Subtipo (ex.: o tipo do compromisso, a fonte da doação).</summary>
    public string? Sub { get; init; }

    /// <summary>Quem fez: id de candidato ou de pessoa; null quando o fato não tem autor (economia, cidade).</summary>
    public string? ActorId { get; init; }

    /// <summary>Contra quem ou com quem (id de candidato ou de pessoa).</summary>
    public string? TargetId { get; init; }

    public string? ZoneId { get; init; }

    /// <summary>Pessoa envolvida que não é o autor (a liderança visitada, o membro da equipe).</summary>
    public string? PersonId { get; init; }

    public string? Theme { get; init; }

    public required int Visibility { get; init; }

    /// <summary>O que aconteceu, por extenso.</summary>
    public required string Text { get; init; }

    /// <summary>Circula como boato de rede.</summary>
    public bool Rumor { get; init; }

    /// <summary>O boato é falso.</summary>
    public bool False { get; init; }
}

/// <summary>Registro dos fatos da campanha, com teto de tamanho.</summary>
public sealed class FactLog
{
    public const int Max = 800;

    public List<Fact> Items { get; init; } = [];

    public int NextSeq { get; set; } = 1;

    public Fact? Find(string id) => Items.FirstOrDefault(f => f.Id == id);

    public void Add(Fact fact)
    {
        Items.Add(fact);
        if (Items.Count > Max)
        {
            Items.RemoveRange(0, Items.Count - Max);
        }
    }
}

/// <summary>O que os sistemas do mundo já fizeram nesta semana (para o dia e a virada darem o mesmo resultado).</summary>
public sealed class WorldClock
{
    /// <summary>Último dia em que o mundo (rivais, mídia, doadores) já agiu.</summary>
    public DateOnly? LastDay { get; set; }
}

public sealed partial class CampaignEngine
{
    public FactLog Facts => Campaign.Facts;

    private Media.MediaContent MediaContent => Context.Campaign.Media;

    /// <summary>
    /// Emite um fato. Com <paramref name="feed"/>, ele também entra no feed do jogador (a linha do feed aponta o
    /// fato de origem). A visibilidade padrão de cada tipo vem de data/media/templates.json.
    /// </summary>
    public Fact Emit(string kind, string text, string? actor = null, string? zone = null, string? person = null, string? theme = null,
        string? target = null, string? sub = null, int? visibility = null, DateOnly? date = null, bool feed = true, bool rumor = false, bool falso = false)
    {
        int seq = Facts.NextSeq++;
        var fact = new Fact
        {
            Id = $"f{seq}",
            Seq = seq,
            Date = date ?? Today,
            Kind = kind,
            Sub = sub,
            ActorId = actor,
            TargetId = target,
            ZoneId = zone,
            PersonId = person,
            Theme = theme,
            Visibility = Math.Clamp(visibility ?? MediaContent.Templates.Visibility(kind, sub), 0, 3),
            Text = text,
            Rumor = rumor,
            False = falso,
        };
        Facts.Add(fact);
        if (feed)
        {
            Agenda.AddFeed(new FeedItem(fact.Date, kind, text, zone, person, actor, fact.Id));
        }

        DynamicEvents(fact);
        return fact;
    }

    /// <summary>Linha de feed que nasce de um fato (os mesmos campos do feed antigo).</summary>
    private Fact EmitFeed(DateOnly date, string kind, string text, string? zoneId = null, string? personId = null, string? candidateId = null) =>
        Emit(kind, text, candidateId, zoneId, personId, date: date);

    /// <summary>
    /// O mundo num dia: rivais cumprem a agenda deles, doadores e boatos aparecem, a mídia cobre os fatos do dia e os
    /// prazos correm. Roda uma vez por dia, tanto no avanço dia a dia quanto na virada direta da semana.
    /// </summary>
    private void WorldDay(DateOnly day)
    {
        if (Campaign.World.LastDay is { } last && day <= last)
        {
            return;
        }

        Campaign.World.LastDay = day;
        if (!CampaignRunning)
        {
            return;
        }

        RivalsDay(day);
        FinanceDay(day);
        AttacksDay(day);
        MediaDay(day);
    }

    /// <summary>
    /// Eventos dinâmicos (GDD 15.4): um fato pode disparar um evento escrito para ele, que envolve as pessoas, a zona
    /// e o tema do fato. Vale a chance do evento, o cooldown e o teto de eventos pendentes.
    /// </summary>
    private void DynamicEvents(Fact fact)
    {
        if (Context.Events is not { } content || !CampaignRunning || Campaign.Events.Pending.Count >= content.Config.MaxPerWeek)
        {
            return;
        }

        string specific = $"{fact.Kind}:{fact.Sub}";
        foreach (Events.EventDefinition e in content.Events)
        {
            if (e.Conditions.Trigger is not { } trigger || (trigger != fact.Kind && trigger != specific) || Campaign.Events.Pending.Any(p => p.EventId == e.Id)
                || !Events.EventEngine.Fits(Context, e, State.Calendar.WeekStart) || !Rng.Chance(e.Conditions.Chance))
            {
                continue;
            }

            Events.EventEngine.FireFromFact(Context, e, Rng, fact);
            return;
        }
    }
}
