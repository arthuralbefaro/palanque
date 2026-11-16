using Planalto.Core.Electorate;
using Planalto.Core.Ideology;
using Planalto.Core.Parties;
using Planalto.Core.State;

namespace Planalto.Core.Legislature;

/// <summary>
/// Como um vereador NPC vota (DECISOES D-027), usado tanto no mandato de vereador quanto nos projetos que o
/// prefeito manda à Câmara: afinidade ideológica, orientação da bancada × disciplina, governo/oposição e relação.
/// </summary>
public static class ChamberVote
{
    /// <summary>Afinidade (-1 a 1) entre uma posição e o projeto: positiva perto, negativa longe.</summary>
    public static double Alignment(LegislatureVoteConfig w, IdeologicalPosition position, IdeologicalPosition bill) =>
        Math.Clamp((w.NeutralDistance - ChamberSetup.Distance(position, bill)) / w.NeutralDistance, -1, 1);

    public static double Discipline(LegislatureConfig config, GameState state, string? partyId)
    {
        string key = state.Party(partyId)?.Charter?.Discipline switch
        {
            PartyDiscipline.Rigida => "rigida",
            PartyDiscipline.Frouxa => "frouxa",
            _ => "moderada",
        };
        return config.Discipline.GetValueOrDefault(key, 0.7);
    }

    /// <summary>Orientação da bancada (-1 não, 0 liberada, 1 sim), pela posição do partido e pelo lado no governo.</summary>
    public static int PartyOrientation(LegislatureConfig config, GameState state, string? partyId, IdeologicalPosition bill, int governmentSide)
    {
        if (partyId is null || state.Party(partyId) is not { } party)
        {
            return 0;
        }

        double score = Alignment(config.Vote, party.Position, bill)
            + (governmentSide * config.Vote.GovernmentWeight / config.Vote.IdeologyWeight);
        return Math.Abs(score) < config.Vote.OrientationThreshold ? 0 : Math.Sign(score);
    }

    /// <summary>Logito do voto SIM (sem ruído). <paramref name="relation"/> de -100 a 100 com o autor, quando conta.</summary>
    public static double Logit(LegislatureConfig config, GameState state, ChamberMember member, IdeologicalPosition bill, int orientation, int governmentSide, double relation)
    {
        LegislatureVoteConfig w = config.Vote;
        return (w.IdeologyWeight * Alignment(w, member.Position, bill))
            + (w.OrientationWeight * orientation * Discipline(config, state, member.PartyId))
            + (w.GovernmentWeight * governmentSide)
            + (w.RelationWeight * relation / 100);
    }

    /// <summary>Voto com ruído; perto de zero, abstenção.</summary>
    public static VoteChoice Decide(LegislatureConfig config, double logit, Rng rng)
    {
        double noisy = logit + (ElectorateBuilder.Gaussian(rng) * config.Vote.NoiseSd);
        return Math.Abs(noisy) < config.Vote.AbstainBand ? VoteChoice.Abstencao : noisy > 0 ? VoteChoice.Sim : VoteChoice.Nao;
    }
}
