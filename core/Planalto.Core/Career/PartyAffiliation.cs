using Planalto.Core.Elections;
using Planalto.Core.Legislature;
using Planalto.Core.State;
using Calendar = Planalto.Core.State.Calendar;

namespace Planalto.Core.Career;

/// <summary>
/// Filiação e troca de partido (GDD 4.1 e 5.5): só disputa quem está filiado até 6 meses antes da eleição
/// (Lei 9.504, art. 9º). Com mandato proporcional, trocar de partido fora da janela partidária custa o mandato
/// (Lei 9.096, art. 22-A); expulso ou sem mandato, a troca é livre.
/// </summary>
public static class PartyAffiliation
{
    /// <summary>Dias da janela partidária antes do prazo de filiação (art. 22-A, III).</summary>
    public const int WindowDays = 30;

    /// <summary>
    /// Janela partidária aberta para o mandato proporcional em curso: só ao término do mandato vigente (art. 22-A,
    /// III), isto é, no ano da eleição em que ele acaba (municipal para vereador, geral para deputados), nos 30 dias
    /// antes do prazo de filiação. Sem mandato proporcional, não há janela (a troca é livre).
    /// </summary>
    public static bool InWindow(GameState state)
    {
        if (state.Mandate is not { } mandate)
        {
            return false;
        }

        int year = state.Calendar.Year;
        if (year != mandate.End.Year || Calendar.ElectionKindOf(year) != ElectionKindFor(mandate.Office))
        {
            return false;
        }

        DateOnly deadline = Calendar.ElectionFor(year).PartyAffiliationDeadline;
        return state.Calendar.WeekEnd >= deadline.AddDays(-WindowDays) && state.Calendar.WeekStart <= deadline;
    }

    /// <summary>Eleição que renova o cargo: municipal para vereador; geral para deputados.</summary>
    public static ElectionKind ElectionKindFor(Office office) =>
        office == Office.Vereador ? ElectionKind.Municipal : ElectionKind.General;

    /// <summary>Se a troca agora custaria o mandato.</summary>
    public static bool LosesMandate(GameState state) =>
        state.Mandate is not null && state.Player.PartyId is not null && !InWindow(state);

    /// <summary>Filia o jogador ao partido. Com mandato e fora da janela, o mandato acaba (a vaga fica com o partido).</summary>
    public static void Join(GameState state, string partyId)
    {
        if (state.Party(partyId) is null)
        {
            throw new ArgumentException($"Partido '{partyId}' inexistente.", nameof(partyId));
        }

        if (state.Player.PartyId == partyId)
        {
            return;
        }

        bool losesMandate = LosesMandate(state);
        state.Player.PartyId = partyId;
        state.Player.AffiliatedOn = state.Calendar.WeekStart;

        // Prefeito (cargo majoritário) não perde o mandato ao trocar de partido; as secretarias do partido dele vão junto.
        if (state.Executive is { } executive)
        {
            int own = executive.Secretarias.GetValueOrDefault(executive.PartyId);
            executive.Secretarias.Remove(executive.PartyId);
            executive.Secretarias[partyId] = executive.Secretarias.GetValueOrDefault(partyId) + own;
            executive.PartyId = partyId;
        }

        if (state.Mandate is not { } mandate)
        {
            return;
        }

        if (losesMandate)
        {
            state.Mandate = null;
            return;
        }

        ChamberMember player = mandate.Player;
        player.PartyId = partyId;
        player.Leader = mandate.Members.Count(m => m.PartyId == partyId) == 1;
        mandate.Expelled = false;
        mandate.Sanctioned = false;
        mandate.Warnings = 0;
        mandate.Fidelity = Math.Max(mandate.Fidelity, 60);
        mandate.News.Add(new MandateNews(state.Calendar.WeekStart, MandateNewsKind.MudancaDePartido, state.Party(partyId)!.Sigla));
    }

    /// <summary>
    /// Pode disputar a eleição do ano: filiado até o prazo de filiação, a um partido registrado no TSE até 6 meses
    /// antes da eleição (Lei 9.504, art. 4º; o mesmo prazo, na prática).
    /// </summary>
    public static bool CanRun(GameState state, int electionYear)
    {
        DateOnly deadline = Calendar.ElectionFor(electionYear).PartyAffiliationDeadline;
        return state.Party(state.Player.PartyId) is { } party
            && (party.RegisteredOn is null || party.RegisteredOn <= deadline)
            && (state.Player.AffiliatedOn is null || state.Player.AffiliatedOn <= deadline);
    }
}
