using System.Globalization;
using Planalto.Core.Campaign;
using Planalto.Core.Governance;

namespace Planalto.Core.Legislature;

/// <summary>
/// Previsão do voto do vereador na pauta (GDD 17.1, princípio f): o que a sessão da semana aplica pelo voto marcado
/// (fidelidade, reação dos eleitores, relação com o prefeito e com o autor, capital), pelas mesmas regras de
/// <see cref="PlayerConsequences"/>, sem mexer no estado.
/// </summary>
public sealed partial class LegislatureEngine
{
    private string Text(string key, params (string Key, string Value)[] values) => Texts.Fill(Context.Content.Texts.Texto(key), values);

    public GovForecast ForecastVote(string billId, VoteChoice choice)
    {
        Bill? bill = Pauta.FirstOrDefault(b => b.Id == billId);
        if (bill is null || choice == VoteChoice.Ausente)
        {
            return GovForecast.Block(GovFailure.NaoEncontrado, "");
        }

        MandateState mandate = Mandate;
        BillTemplate template = Template(bill);
        ChamberMember player = mandate.Player;
        int orientation = PlayerOrientation(bill);
        int sign = choice switch { VoteChoice.Sim => 1, VoteChoice.Nao => -1, _ => 0 };
        var lines = new List<ForecastLine>();
        var explanation = new List<string> { Text("previsao_voto") };
        var remembered = new List<string>();
        void Add(string kind, string? target, double value)
        {
            if (Math.Abs(value) > 1e-12)
            {
                lines.Add(new ForecastLine(kind, target, value, value, value));
            }
        }

        // Fidelidade: só com partido e com orientação (a mesma conta de ChangeFidelity).
        string party = player.PartyId is null ? "" : State.Party(player.PartyId)?.Sigla ?? player.PartyId;
        FidelityConfig f = Config.Fidelity;
        if (player.PartyId is null)
        {
            explanation.Add(Text("previsao_voto_sem_partido"));
        }
        else if (orientation == 0)
        {
            explanation.Add(Text("previsao_voto_liberado", ("partido", party)));
        }
        else
        {
            int delta = sign == orientation ? f.GainPerFollow : sign == 0 ? -f.LossPerAbstention : -f.LossPerDefiance;
            Add(GovForecast.Fidelidade, null, Math.Clamp(mandate.Fidelity + delta, 0, 100) - mandate.Fidelity);
            string pontos = Math.Abs(delta).ToString(CultureInfo.InvariantCulture);
            explanation.Add(Text(sign == orientation ? "previsao_voto_segue" : sign == 0 ? "previsao_voto_abstem" : "previsao_voto_contraria", ("partido", party), ("pontos", pontos)));
            explanation.Add(Text("previsao_voto_limites", ("advertencia", f.WarningBelow.ToString(CultureInfo.InvariantCulture)), ("sancao", f.SanctionBelow.ToString(CultureInfo.InvariantCulture)),
                ("expulsao", f.ExpulsionBelow.ToString(CultureInfo.InvariantCulture)), ("hoje", mandate.Fidelity.ToString(CultureInfo.InvariantCulture))));
        }

        // Reação dos eleitores (Opinion com multiplicador 1).
        if (sign == 0)
        {
            explanation.Add(Text("previsao_voto_abstencao_publico"));
        }
        else
        {
            double net = 0;
            foreach (var (group, popularity) in template.Popularity.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                double before = mandate.Support.GetValueOrDefault(group);
                Add(GovForecast.Apoio, group, Math.Clamp(before + (sign * popularity * template.Salience * Config.Public.SupportPerPoint), 0, 1) - before);
                net += popularity * _groupShares.GetValueOrDefault(group);
            }

            Add(GovForecast.Imagem, null, Math.Clamp(mandate.Image + (sign * net * template.Salience * Config.Public.ImagePerPoint), -1, 1) - mandate.Image);
            if (template.Popularity.Count > 0)
            {
                explanation.Add(Text("previsao_voto_publico"));
            }
        }

        if (bill.AuthorKind == BillAuthorKind.Executivo)
        {
            int mayor = sign > 0 ? Config.Relations.MayorWith : sign < 0 ? Config.Relations.MayorAgainst : 0;
            Add(GovForecast.RelacaoPrefeito, null, Math.Clamp(mandate.MayorRelation + mayor, -100, 100) - mandate.MayorRelation);
            if (sign > 0 && mandate.InBase(player.PartyId))
            {
                Add(GovForecast.Capital, null, Config.Capital.WithGovernmentInBase);
            }

            explanation.Add(Text("previsao_voto_executivo"));
            remembered.Add(mandate.MayorName);
        }
        else if (mandate.Member(bill.AuthorId) is { IsPlayer: false } author)
        {
            int relation = sign > 0 ? Config.Relations.AuthorWith : sign < 0 ? Config.Relations.AuthorAgainst : 0;
            Add(GovForecast.Relacao, author.Id, Math.Clamp(author.Relation + relation, -100, 100) - author.Relation);
            explanation.Add(Text("previsao_voto_autor", ("autor", author.Name)));
            remembered.Add(author.Name);
        }

        // Acordo de voto: prometeu votar a favor do projeto do colega; outro voto quebra a palavra (GDD 17.14).
        if (choice != VoteChoice.Sim && State.Governance is { } g
            && g.Deals.FirstOrDefault(d => d.SupportBillId == bill.Id && State.Ledger.Promises.Any(p => p.Id == d.PromiseId && p.State == People.PromiseState.Aberta)) is { } deal
            && mandate.Member(deal.MemberId) is { } partner)
        {
            explanation.Add(Text("previsao_voto_acordo", ("nome", partner.Name)));
            remembered.Add(partner.Name);
        }

        return new GovForecast(null, null, lines, [], explanation) { Remembered = remembered.Where(n => n.Length > 0).Distinct().ToList() };
    }
}
