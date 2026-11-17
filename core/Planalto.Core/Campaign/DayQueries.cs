using Planalto.Core.Electorate;
using Planalto.Core.State;
using Planalto.Core.Territory;

namespace Planalto.Core.Campaign;

/// <summary>Como está um turno na linha do dia (RL): o que a barra de cima e o painel Você mostram.</summary>
public enum TurnoSituacao
{
    /// <summary>Sem compromisso pessoal e ainda por vir (ou em curso): dá para marcar.</summary>
    Livre,

    /// <summary>Há um compromisso pessoal marcado e o relógio ainda não chegou nele.</summary>
    Marcado,

    /// <summary>O relógio está dentro do turno do compromisso marcado: ele acontece quando o turno terminar.</summary>
    EmAndamento,

    /// <summary>O compromisso do turno já aconteceu.</summary>
    Feito,

    /// <summary>O turno passou sem compromisso pessoal: não volta.</summary>
    Passou,
}

/// <summary>Um turno de um dia, com o compromisso que o ocupa (marcado ou feito) e o evento da cidade que cai nele.</summary>
public sealed record TurnoSlot(
    DateOnly Date,
    Turno Turno,
    int HoraInicio,
    int HoraFim,
    TurnoSituacao Situacao,
    Commitment? Commitment,
    CommitmentResult? Result,
    CityEventInstance? CityEvent,
    Invite? Invite);

/// <summary>Relógio do dia e linha do dia (parte RL): só leem o estado.</summary>
public sealed partial class CampaignEngine
{
    private TimeConfig? Time => Context.Time;

    /// <summary>O turno de hoje já passou no relógio do jogo (sem relógio do dia, nenhum passou).</summary>
    public bool TurnoPassed(DateOnly date, Turno turno) => Time is not null && date == Today && Clock.TurnoEnded(Time, State, turno);

    private Turno LastTurnOf(Commitment c) => (Turno)Math.Min(2, (int)c.Turno + Commitments.Get(c.Type).Turnos - 1);

    /// <summary>
    /// Compromissos de hoje cujo turno o relógio já fechou (GDD 17.4 com o relógio do dia): acontecem agora, na ordem
    /// dos turnos, e devolvem os resultados explicados. O que sobrar do dia acontece na virada, como antes.
    /// </summary>
    public List<CommitmentResult> RunDue()
    {
        var results = new List<CommitmentResult>();
        if (Time is null || Campaign.Phase is not (CampaignPhase.PreCampanha or CampaignPhase.Campanha))
        {
            return results;
        }

        foreach (Commitment c in Agenda.Scheduled.Where(c => c.Date == Today && Clock.TurnoEnded(Time, State, LastTurnOf(c))).OrderBy(c => c.Turno).ThenBy(c => c.Id, StringComparer.Ordinal).ToList())
        {
            if (Agenda.PendingRequest is not null)
            {
                // Uma decisão ficou esperando: o resto espera o jogador.
                break;
            }

            Agenda.Scheduled.Remove(c);
            results.Add(Run(c));
        }

        return results;
    }

    /// <summary>Os três turnos de um dia: o que ocupa cada um e se já passou (linha do dia da barra de cima).</summary>
    public IReadOnlyList<TurnoSlot> DayLine(DateOnly date)
    {
        var slots = new List<TurnoSlot>();
        foreach (Turno turno in Enum.GetValues<Turno>())
        {
            int start = Time?.Dia.Of(turno).Inicio ?? 0;
            int end = Time?.Dia.Of(turno).Fim ?? 0;
            CityEventInstance? cityEvent = Agenda.CityEvents.FirstOrDefault(e => e.Date == date && e.Turno == turno);
            Invite? invite = Agenda.Invites.FirstOrDefault(i => !i.Answered && i.Date == date && i.Turno == turno);
            if (Occupying(date, turno) is { } c)
            {
                bool now = Time is not null && date == Today && State.Time.Minute >= Time.Dia.StartOf(c.Turno) && State.Time.Minute < Time.Dia.EndOf(LastTurnOf(c));
                slots.Add(new TurnoSlot(date, turno, start, end, now ? TurnoSituacao.EmAndamento : TurnoSituacao.Marcado, c, null, cityEvent, invite));
            }
            else if (DoneIn(date, turno) is { } done)
            {
                slots.Add(new TurnoSlot(date, turno, start, end, TurnoSituacao.Feito, done.Commitment, done, cityEvent, invite));
            }
            else
            {
                bool past = date < Today || TurnoPassed(date, turno);
                slots.Add(new TurnoSlot(date, turno, start, end, past ? TurnoSituacao.Passou : TurnoSituacao.Livre, null, null, cityEvent, invite));
            }
        }

        return slots;
    }

    /// <summary>Compromisso pessoal já feito que ocupou o turno.</summary>
    private CommitmentResult? DoneIn(DateOnly date, Turno turno) =>
        Agenda.Log.LastOrDefault(r => !r.Commitment.Delegado && r.Commitment.Date == date
            && (int)turno >= (int)r.Commitment.Turno && (int)turno < (int)r.Commitment.Turno + Commitments.Get(r.Commitment.Type).Turnos);

    /// <summary>Próximos compromissos pessoais a partir de agora (o em andamento primeiro).</summary>
    public IReadOnlyList<Commitment> UpcomingCommitments(int count) =>
        Agenda.Scheduled.Where(c => !c.Delegado && c.Date >= Today).OrderBy(c => c.Date).ThenBy(c => c.Turno).Take(count).ToList();
}
