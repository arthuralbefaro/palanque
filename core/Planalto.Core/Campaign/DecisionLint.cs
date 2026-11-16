using Planalto.Core.Events;

namespace Planalto.Core.Campaign;

/// <summary>O que uma resposta ganha e o que custa, por natureza (GDD 17.1, princípio e).</summary>
public sealed class DecisionTrade
{
    public required List<string> Ganho { get; init; }

    public required List<string> Custo { get; init; }
}

/// <summary>
/// Lint de conteúdo das decisões (GDD 17.1, princípio e): em toda decisão — evento da equipe, pedido de liderança,
/// escolha de evento marcada como relevante — cada opção tem pelo menos um ganho e um custo de naturezas diferentes
/// (dinheiro, tempo, relação, zona, risco, posição), e o que o conteúdo declara tem de bater com o efeito que a
/// opção de fato produz. Opção relevante sem rastro (Livro ou lembrança) também reprova (princípio d).
/// </summary>
public static partial class DecisionLint
{
    public static IReadOnlyList<string> Run(CampaignContent campaign, EventContent? events)
    {
        var errors = new List<string>();
        errors.AddRange(Staff(campaign.Staff));
        errors.AddRange(Leaders(campaign.Leaders));
        if (events is not null)
        {
            errors.AddRange(Events(events));
        }

        return errors;
    }

    /// <summary>Quantas opções de decisão o lint cobre (para o relatório do validador).</summary>
    public static int Count(CampaignContent campaign, EventContent? events) =>
        campaign.Staff.Eventos.Sum(e => e.Opcoes.Count)
        + (campaign.Leaders.Pedidos.Count * LeaderCatalog.AnswerKeys.Count)
        + (events?.Events.Sum(e => e.Choices.Count(c => c.Relevant)) ?? 0);

    public static IEnumerable<string> Staff(StaffCatalog staff)
    {
        foreach (StaffEventDefinition e in staff.Eventos)
        {
            bool othersCostMoney = e.Opcoes.Any(o => o.Efeitos.SalarioFator > 1 || o.Efeitos.Dinheiro < 0);
            bool othersBind = e.Opcoes.Any(o => o.Efeitos.Divida || o.Efeitos.Promessa);
            foreach (StaffEventOption o in e.Opcoes)
            {
                string where = $"equipe '{e.Id}', opção '{o.Id}'";
                foreach (string error in DecisionNature.Check(o.Ganho, o.Custo, where))
                {
                    yield return error;
                }

                StaffOptionEffects fx = o.Efeitos;
                var gains = new HashSet<string>(StringComparer.Ordinal);
                var costs = new HashSet<string>(StringComparer.Ordinal);
                if (fx.Lealdade > 0 || fx.LealdadeOutro > 0 || fx.ApresentaLideranca)
                {
                    gains.Add(DecisionNature.Relacao);
                }

                if (fx.Lealdade < 0 || fx.LealdadeOutro < 0)
                {
                    costs.Add(DecisionNature.Relacao);
                }

                if (fx.SalarioFator > 1 || fx.Dinheiro < 0)
                {
                    costs.Add(DecisionNature.Dinheiro);
                }

                // Não pagar o que outra opção pagaria é ganho em dinheiro; receber, também.
                if (fx.Dinheiro > 0 || (othersCostMoney && fx.SalarioFator <= 1 && fx.Dinheiro >= 0))
                {
                    gains.Add(DecisionNature.Dinheiro);
                }

                if (fx.SaiChance > 0 || fx.OutroSaiChance > 0 || fx.FicaPelaLealdade || fx.Promessa || fx.Divida)
                {
                    costs.Add(DecisionNature.Risco);
                }

                if (fx.ReforcaRival)
                {
                    costs.Add(DecisionNature.Zona);
                }

                if (fx.Estresse < 0)
                {
                    gains.Add(DecisionNature.Tempo);
                }

                if (fx.Estresse > 0)
                {
                    costs.Add(DecisionNature.Tempo);
                }

                // Não ficar devendo o que outra opção deixaria no Livro é manter a posição livre.
                if (othersBind && !fx.Divida && !fx.Promessa)
                {
                    gains.Add(DecisionNature.Posicao);
                }

                foreach (string nature in o.Ganho.Where(g => !gains.Contains(g)))
                {
                    yield return $"{where}: ganho '{nature}' declarado sem efeito que o sustente.";
                }

                foreach (string nature in o.Custo.Where(c => !costs.Contains(c)))
                {
                    yield return $"{where}: custo '{nature}' declarado sem efeito que o sustente.";
                }

                if (fx.Lembranca is null)
                {
                    yield return $"{where}: decisão sem rastro (nenhuma lembrança em quem estava envolvido).";
                }
            }
        }
    }

    public static IEnumerable<string> Leaders(LeaderCatalog leaders)
    {
        LeaderRules r = leaders.Regras;
        foreach (LeaderRequestDefinition request in leaders.Pedidos)
        {
            foreach (string answer in LeaderCatalog.AnswerKeys)
            {
                string where = $"pedido '{request.Id}', resposta '{answer}'";
                DecisionTrade? trade = leaders.TradeOf(request, answer);
                foreach (string error in DecisionNature.Check(trade?.Ganho, trade?.Custo, where))
                {
                    yield return error;
                }

                if (trade is null)
                {
                    continue;
                }

                bool money = request.Kind == LeaderRequestDefinition.Dinheiro;
                var gains = new HashSet<string>(StringComparer.Ordinal);
                var costs = new HashSet<string>(StringComparer.Ordinal);
                switch (answer)
                {
                    case LeaderCatalog.Aceitar:
                        // Relação e o apoio do público dela na zona; em troca, uma promessa no Livro (ou o gasto irregular).
                        if (r.PontosPromessa > 0)
                        {
                            gains.Add(DecisionNature.Relacao);
                            gains.Add(DecisionNature.Zona);
                        }

                        costs.Add(DecisionNature.Risco);
                        if (money)
                        {
                            costs.Add(DecisionNature.Dinheiro);
                        }

                        if (request.Kind == LeaderRequestDefinition.Posicao)
                        {
                            costs.Add(DecisionNature.Posicao);
                        }

                        if (request.Kind == LeaderRequestDefinition.Presenca)
                        {
                            costs.Add(DecisionNature.Tempo);
                        }

                        break;
                    case LeaderCatalog.Negociar:
                        if (r.PontosNegociacao > 0)
                        {
                            gains.Add(DecisionNature.Relacao);
                            gains.Add(DecisionNature.Zona);
                        }

                        if (r.PontosNegociacaoFalha < 0)
                        {
                            costs.Add(DecisionNature.Risco);
                        }

                        break;
                    case LeaderCatalog.NaoSeComprometer:
                        // Fica sem a promessa no Livro e ainda rende um pouco de relação; o turno rendeu menos que fechando.
                        gains.Add(DecisionNature.Posicao);
                        if (r.PontosVisitaSemCompromisso > 0)
                        {
                            gains.Add(DecisionNature.Relacao);
                        }

                        if (r.PontosVisitaSemCompromisso < r.PontosPromessa)
                        {
                            costs.Add(DecisionNature.Tempo);
                        }

                        break;
                    default:
                        gains.Add(DecisionNature.Posicao);
                        if (money)
                        {
                            gains.Add(DecisionNature.Dinheiro);
                        }

                        if (r.PontosRecusa < 0)
                        {
                            costs.Add(DecisionNature.Relacao);
                        }

                        break;
                }

                foreach (string nature in trade.Ganho.Where(g => !gains.Contains(g)))
                {
                    yield return $"{where}: ganho '{nature}' declarado sem efeito que o sustente.";
                }

                foreach (string nature in trade.Custo.Where(c => !costs.Contains(c)))
                {
                    yield return $"{where}: custo '{nature}' declarado sem efeito que o sustente.";
                }
            }
        }
    }

    public static IEnumerable<string> Events(EventContent events)
    {
        foreach (EventDefinition e in events.Events)
        {
            for (int i = 0; i < e.Choices.Count; i++)
            {
                EventChoice choice = e.Choices[i];
                string where = $"evento '{e.Id}', escolha {i + 1}";
                List<Effects> branches = choice.Test is null ? [choice.Effects] : [choice.Effects, choice.Test.Success, choice.Test.Failure];
                if (!choice.Relevant)
                {
                    // O que fica depois do evento (Livro ou cadeia) não pode passar por escolha comum: tem de ser marcado
                    // como relevante, para cair no lint de troca e de rastro.
                    if (branches.Any(fx => fx.AddSkeleton is not null || fx.Promise is not null || fx.Debt is not null || fx.Schedule.Count > 0))
                    {
                        yield return $"{where}: escolha com consequência que fica (Livro ou cadeia) não está marcada como relevante.";
                    }

                    continue;
                }

                foreach (string error in DecisionNature.Check(choice.Gain, choice.Cost, where))
                {
                    yield return error;
                }

                List<Effects> all = choice.Test is null ? [choice.Effects] : [choice.Effects, choice.Test.Success, choice.Test.Failure];
                var gains = new HashSet<string>(StringComparer.Ordinal);
                var costs = new HashSet<string>(StringComparer.Ordinal);
                foreach (Effects fx in all)
                {
                    Add(fx.Money > 0, gains, DecisionNature.Dinheiro);
                    Add(fx.Money < 0, costs, DecisionNature.Dinheiro);
                    Add(fx.Relation > 0 || fx.Support.Values.Any(v => v > 0) || fx.Memories.Any(m => m.Pontos > 0), gains, DecisionNature.Relacao);
                    Add(fx.Relation < 0 || fx.Support.Values.Any(v => v < 0) || fx.Memories.Any(m => m.Pontos < 0), costs, DecisionNature.Relacao);
                    Add(fx.Recognition > 0 || fx.ZonePresence > 0, gains, DecisionNature.Zona);
                    Add(fx.Recognition < 0 || fx.ZonePresence < 0, costs, DecisionNature.Zona);
                    Add(fx.Image > 0, gains, DecisionNature.Posicao);
                    Add(fx.Image < 0, costs, DecisionNature.Posicao);
                    Add(fx.Energy > 0 || fx.Stress < 0, gains, DecisionNature.Tempo);
                    Add(fx.Energy < 0 || fx.Stress > 0 || fx.CancelCommitment is not null, costs, DecisionNature.Tempo);
                    Add(fx.AddSkeleton is not null || fx.Schedule.Count > 0 || fx.Promise is not null || fx.Debt is not null, costs, DecisionNature.Risco);
                }

                Add(choice.Test is not null, costs, DecisionNature.Risco);
                foreach (string nature in (choice.Gain ?? []).Where(g => !gains.Contains(g)))
                {
                    yield return $"{where}: ganho '{nature}' declarado sem efeito que o sustente.";
                }

                foreach (string nature in (choice.Cost ?? []).Where(c => !costs.Contains(c)))
                {
                    yield return $"{where}: custo '{nature}' declarado sem efeito que o sustente.";
                }

                foreach (string error in Trace(choice, where))
                {
                    yield return error;
                }
            }
        }
    }

    /// <summary>
    /// Lint de rastro (GDD 17.1, princípio d): a escolha relevante grava uma entrada no Livro de compromissos
    /// (esqueleto, promessa ou dívida) ou uma lembrança em alguém, qualquer que seja o desfecho sorteado. Agendar uma
    /// continuação não basta: o que fica tem de estar no save.
    /// </summary>
    public static IEnumerable<string> Trace(EventChoice choice, string where)
    {
        bool traced = choice.Effects.LeavesTrace || (choice.Test is { } test && test.Success.LeavesTrace && test.Failure.LeavesTrace);
        if (choice.Relevant && !traced)
        {
            yield return $"{where}: escolha relevante sem rastro (nem entrada no Livro, nem lembrança em alguém).";
        }
    }

    /// <summary>O lint de rastro em todos os eventos (para o validador e os testes).</summary>
    public static IEnumerable<string> Trace(EventContent events)
    {
        foreach (EventDefinition e in events.Events)
        {
            for (int i = 0; i < e.Choices.Count; i++)
            {
                foreach (string error in Trace(e.Choices[i], $"evento '{e.Id}', escolha {i + 1}"))
                {
                    yield return error;
                }
            }
        }
    }

    private static void Add(bool condition, HashSet<string> set, string nature)
    {
        if (condition)
        {
            set.Add(nature);
        }
    }
}
