using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.State;

namespace Planalto.Core.Renewal;

/// <summary>Uma cadeira da Câmara como a interface mostra.</summary>
public sealed record SeatView(
    int Numero, string PartyId, string? TitularId, string Nome, int? Idade, bool Presidente, bool Vaga, DateOnly? VagaDesde, DateOnly? VagaPrazo, CouncilSuccession? Destino);

/// <summary>A Prefeitura como a interface mostra: titular, vice, ou quem responde por ela na vacância e a eleição marcada.</summary>
public sealed record ExecutiveView(
    string? PrefeitoId, string PrefeitoNome, string? PartyId, int Mandatos, string? ViceId, string ViceNome, bool Vaga, string? RespondeId, string RespondeNome, PendingElection? Eleicao);

/// <summary>Consultas de leitura da renovação para a interface. Não mudam o estado.</summary>
public static class RenewalQueries
{
    public static List<SeatView> Chamber(GameState state, DateOnly today)
    {
        RenewalState r = state.Renewal ?? throw new InvalidOperationException("Sem estado de renovação.");
        return r.Cadeiras.Select(c =>
        {
            Character? npc = Find(state, c.TitularId);
            return new SeatView(
                c.Numero, c.PartyId, c.TitularId, npc?.BallotName ?? "", npc?.AgeOn(today), c.TitularId is not null && c.TitularId == r.PresidenteCamaraId,
                c.TitularId is null, c.VagaDesde, c.VagaPrazo, c.VagaDestino);
        }).ToList();
    }

    public static ExecutiveView Executive(GameState state)
    {
        RenewalState r = state.Renewal ?? throw new InvalidOperationException("Sem estado de renovação.");
        bool vacant = r.PrefeitoId is null;
        string? acting = vacant ? r.PresidenteCamaraId : null;
        return new ExecutiveView(
            r.PrefeitoId, Find(state, r.PrefeitoId)?.BallotName ?? "", r.PrefeitoPartyId, r.PrefeitoMandatos, r.ViceId, Find(state, r.ViceId)?.BallotName ?? "",
            vacant, acting, Find(state, acting)?.BallotName ?? "", r.EleicoesPendentes.FirstOrDefault(e => e.Cargo == CivicRole.Prefeito));
    }

    /// <summary>Idade média, em anos, de quem ocupa cadeira hoje; 0 com a Câmara vazia.</summary>
    public static double AverageAge(GameState state, DateOnly today)
    {
        RenewalState r = state.Renewal ?? throw new InvalidOperationException("Sem estado de renovação.");
        var ages = r.Titulares.Select(id => Find(state, id)).Where(c => c is not null).Select(c => (today.DayNumber - c!.BirthDate.DayNumber) / 365.25).ToList();
        return ages.Count == 0 ? 0 : ages.Average();
    }

    /// <summary>Suplentes do partido, na ordem em que são chamados.</summary>
    public static IReadOnlyList<string> Substitutes(RenewalState r, string partyId) => r.Suplentes.TryGetValue(partyId, out List<string>? list) ? list : [];

    /// <summary>Fatos do histórico, do mais recente para o mais antigo, opcionalmente só de alguns tipos.</summary>
    public static List<RenewalEvent> History(RenewalState r, params RenewalEventKind[] kinds) =>
        r.Historico.Where(e => kinds.Length == 0 || kinds.Contains(e.Tipo)).OrderByDescending(e => e.Data).ThenByDescending(e => e.Id).ToList();

    /// <summary>A Câmara está em luto oficial (sem sessão) nesta data.</summary>
    public static bool InMourning(RenewalState r, DateOnly today) => r.LutoAte is { } until && today <= until;

    /// <summary>Como a pessoa saiu da política local; null se segue nela ou nunca foi acompanhada.</summary>
    public static ExitKind? ExitOf(RenewalState r, string personId) => r.Status(personId)?.Saida;

    private static Character? Find(GameState state, string? id) =>
        id is null ? null : id == RaceSetup.PlayerId ? state.Player : state.Npcs.FirstOrDefault(n => n.Id == id);
}
