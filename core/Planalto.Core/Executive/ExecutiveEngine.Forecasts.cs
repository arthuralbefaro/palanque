using System.Globalization;
using Planalto.Core.Campaign;
using Planalto.Core.Governance;
using Planalto.Core.Indicators;
using Planalto.Core.Legislature;

namespace Planalto.Core.Executive;

/// <summary>
/// Previsão dos comandos da prefeitura antes do clique (GDD 17.1, princípio f): política por decreto ou por lei,
/// revogação, orçamento por área, resposta à crise e atos do gabinete. Cada previsão é calculada pelas mesmas regras
/// que o comando aplica (mesmos cortes, mesma votação, mesmo teste de atributo), sem mexer no estado nem no Rng.
/// </summary>
public sealed partial class ExecutiveEngine
{
    /// <summary>Fração das votações simuladas que a faixa de votos a favor cobre (de 5% a 95% da distribuição).</summary>
    private const double VoteTail = 0.05;

    private string Text(string key, params (string Key, string Value)[] values) => Texts.Fill(Context.Content.Texts.Texto(key), values);

    /// <summary>Por que o comando não pode ser dado, em texto de data/executive/texts.json.</summary>
    public string FailureText(ExecutiveFailure failure) => Text(failure switch
    {
        ExecutiveFailure.SemEnergia => "falha_sem_energia",
        ExecutiveFailure.SemReserva => "falha_sem_reserva",
        ExecutiveFailure.JaAdotada => "falha_ja_adotada",
        ExecutiveFailure.CrisePendente => "falha_crise_pendente",
        ExecutiveFailure.SemSecretarias => "falha_sem_secretarias",
        ExecutiveFailure.OrcamentoInvalido => "falha_orcamento_invalido",
        ExecutiveFailure.CamaraEmLuto => "falha_camara_em_luto",
        _ => "falha_alvo_invalido",
    });

    private GovForecast Blocked(ExecutiveFailure failure) => GovForecast.Block(failure switch
    {
        ExecutiveFailure.SemEnergia => GovFailure.SemTurno,
        ExecutiveFailure.JaAdotada => GovFailure.JaFeito,
        ExecutiveFailure.AlvoInvalido => GovFailure.AlvoInvalido,
        _ => GovFailure.Bloqueado,
    }, FailureText(failure));

    private static ForecastLine Certain(string kind, string? target, double value) => new(kind, target, value, value, value);

    /// <summary>Linha de um efeito que só acontece com a chance <paramref name="chance"/> (senão, zero).</summary>
    private static ForecastLine Maybe(string kind, string? target, double value, double chance) =>
        chance >= 1 - 1e-12 ? Certain(kind, target, value) : new(kind, target, Math.Min(0, value), Math.Max(0, value), chance * value);

    private string Share(double fraction) => Texts.Number(Math.Abs(fraction) * 100, "0.0") + "%";

    private string Reais(double fraction) => Texts.Money(AnnualRevenue * (decimal)Math.Abs(fraction));

    // ---------------------------------------------------------------- reação dos grupos

    /// <summary>O que <see cref="Opinion"/> mudaria na imagem e no apoio de cada grupo, com os mesmos cortes.</summary>
    private (double Image, Dictionary<string, double> Support) OpinionDelta(IReadOnlyDictionary<string, int> popularity, double salience, double sign)
    {
        ExecutiveState e = Executive;
        double net = 0;
        var support = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (group, value) in popularity)
        {
            double before = e.Support.GetValueOrDefault(group);
            support[group] = Math.Clamp(before + (sign * value * salience * Config.SupportPerPoint), 0, 1) - before;
            net += value * _groupShares.GetValueOrDefault(group);
        }

        double image = Math.Clamp(e.Image + (sign * net * salience * Config.ImagePerPoint), -1, 1) - e.Image;
        return (image, support);
    }

    private static void AddOpinion(List<ForecastLine> lines, (double Image, Dictionary<string, double> Support) delta, double chance)
    {
        if (Math.Abs(delta.Image) > 1e-12)
        {
            lines.Add(Maybe(GovForecast.Imagem, null, delta.Image, chance));
        }

        foreach (var (group, value) in delta.Support.Where(s => Math.Abs(s.Value) > 1e-12).OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            lines.Add(Maybe(GovForecast.Apoio, group, value, chance));
        }
    }

    // ---------------------------------------------------------------- políticas

    /// <summary>
    /// Distribuição dos votos da Câmara numa política, pela mesma regra de <see cref="Enact"/>: cada vereador vota sim,
    /// não ou se abstém pela chance que o logito com ruído dá. Devolve a chance de aprovar e a distribuição dos votos a favor.
    /// </summary>
    public (double Approve, double[] Yes) VoteDistribution(PolicyDefinition policy)
    {
        ExecutiveState e = Executive;
        LegislatureVoteConfig w = Context.Legislature.Vote;
        int n = e.Chamber.Count;
        // dist[sim, não]: probabilidade de cada placar depois dos vereadores já somados.
        var dist = new double[n + 1, n + 1];
        dist[0, 0] = 1;
        int seen = 0;
        foreach (ChamberMember m in e.Chamber)
        {
            int side = e.InBase(m.PartyId) ? 1 : -1;
            int orientation = m.PartyId == e.PartyId ? 1 : ChamberVote.PartyOrientation(Context.Legislature, State, m.PartyId, policy.Position, side);
            double logit = ChamberVote.Logit(Context.Legislature, State, m, policy.Position, orientation, side, m.Relation) + (State.Governance?.DealLogit(policy.Id, m.Id) ?? 0);
            double sd = Math.Max(1e-9, w.NoiseSd);
            double pYes = 1 - GovernanceEngine.Phi((w.AbstainBand - logit) / sd);
            double pNo = GovernanceEngine.Phi((-w.AbstainBand - logit) / sd);
            double pAbstain = Math.Max(0, 1 - pYes - pNo);
            var next = new double[n + 1, n + 1];
            for (int y = 0; y <= seen; y++)
            {
                for (int x = 0; x + y <= seen; x++)
                {
                    double p = dist[y, x];
                    if (p <= 0)
                    {
                        continue;
                    }

                    next[y + 1, x] += p * pYes;
                    next[y, x + 1] += p * pNo;
                    next[y, x] += p * pAbstain;
                }
            }

            dist = next;
            seen++;
        }

        double approve = 0;
        var yes = new double[n + 1];
        for (int y = 0; y <= n; y++)
        {
            for (int x = 0; x + y <= n; x++)
            {
                yes[y] += dist[y, x];
                if (LegislatureEngine.Approved(policy.Quorum, y, x, n))
                {
                    approve += dist[y, x];
                }
            }
        }

        return (Math.Clamp(approve, 0, 1), yes);
    }

    private static int Quantile(double[] distribution, double q)
    {
        double sum = 0;
        for (int k = 0; k < distribution.Length; k++)
        {
            sum += distribution[k];
            if (sum >= q - 1e-12)
            {
                return k;
            }
        }

        return distribution.Length - 1;
    }

    /// <summary>
    /// Previsão de adotar uma política: a energia, os votos a favor e a chance de aprovar (quando precisa de lei), a
    /// reação dos grupos e a imagem (só se for adotada), o que sai da reserva e o que ela empurra quando funcionar.
    /// </summary>
    public GovForecast ForecastEnact(string policyId)
    {
        PolicyDefinition? policy = Context.Content.Policies.Find(policyId);
        if (policy is null)
        {
            return GovForecast.Block(GovFailure.NaoEncontrado, "");
        }

        if (CanEnact(policy) is { } failure)
        {
            return Blocked(failure);
        }

        var lines = new List<ForecastLine> { Certain(GovForecast.Energia, null, -Config.EnactEnergy) };
        var outcomes = new List<ForecastOutcome>();
        var explanation = new List<string>();
        string weeks = policy.ImplementationWeeks.ToString(CultureInfo.InvariantCulture);
        double pass = 1;
        if (policy.RequiresLaw)
        {
            (pass, double[] yes) = VoteDistribution(policy);
            int lo = Quantile(yes, VoteTail), hi = Quantile(yes, 1 - VoteTail);
            double expected = yes.Select((p, k) => p * k).Sum();
            lines.Add(new ForecastLine(GovForecast.VotosSim, null, lo, hi, expected));
            explanation.Add(Text("previsao_lei", ("chance", Texts.Percent(pass)), ("semanas", weeks)));
            explanation.Add(Text("previsao_votos", ("min", lo.ToString(CultureInfo.InvariantCulture)), ("max", hi.ToString(CultureInfo.InvariantCulture)),
                ("total", Executive.Chamber.Count.ToString(CultureInfo.InvariantCulture)), ("esperado", Texts.Number(expected, "0"))));
            outcomes.Add(new ForecastOutcome(pass, Text("desfecho_aprovada")));
            outcomes.Add(new ForecastOutcome(1 - pass, Text("desfecho_rejeitada")));
        }
        else
        {
            explanation.Add(Text("previsao_decreto", ("semanas", weeks)));
        }

        if (Math.Abs(policy.Cost) > 1e-12)
        {
            lines.Add(Maybe(GovForecast.Reserva, null, -policy.Cost, pass));
            explanation.Add(Text(policy.Cost > 0 ? "previsao_custo" : "previsao_libera", ("pct", Share(policy.Cost)), ("reais", Reais(policy.Cost))));
        }

        AddOpinion(lines, OpinionDelta(policy.Popularity, policy.Salience, 1), pass);
        if (policy.Popularity.Count > 0)
        {
            explanation.Add(Text("previsao_reacao"));
        }

        explanation.Add(policy.Boosts.Count == 0 ? Text("previsao_sem_empurrao")
            : Text("previsao_empurra", ("efeitos", string.Join(", ", policy.Boosts.Select(b => $"{IndicatorName(b.Key)} {Signed(b.Value)}")))));
        return new GovForecast(null, null, lines, outcomes.Where(o => o.Probability is > 0 and < 1).ToList(), explanation);
    }

    /// <summary>Previsão de revogar: o custo volta para a reserva e a reação dos grupos se inverte pela metade.</summary>
    public GovForecast ForecastRevoke(string policyId)
    {
        PolicyDefinition? policy = Context.Content.Policies.Find(policyId);
        if (policy is null || !Executive.Policies.Any(p => p.PolicyId == policyId && p.Counts))
        {
            return GovForecast.Block(GovFailure.NaoEncontrado, Text("falha_sem_politica"));
        }

        var lines = new List<ForecastLine>();
        if (Math.Abs(policy.Cost) > 1e-12)
        {
            lines.Add(Certain(GovForecast.Reserva, null, policy.Cost));
        }

        AddOpinion(lines, OpinionDelta(policy.Popularity, policy.Salience, -0.5), 1);
        return new GovForecast(null, null, lines, [], [Text("previsao_revogar"), Text("previsao_revogar_reacao")]);
    }

    // ---------------------------------------------------------------- orçamento

    private static bool ValidAllocation(ExecutiveConfig config, IReadOnlyDictionary<string, double> allocation) =>
        config.Areas.All(a => allocation.ContainsKey(a.Id)) && allocation.Values.All(v => v >= 0) && Math.Abs(allocation.Values.Sum() - 1) <= 0.001;

    /// <summary>
    /// Previsão de aplicar o rascunho do orçamento: quanto muda o alvo de cada indicador (para onde ele caminha semana a
    /// semana), a reserva livre e as áreas que ficariam abaixo do mínimo.
    /// </summary>
    public GovForecast ForecastAllocation(IReadOnlyDictionary<string, double> allocation)
    {
        if (!ValidAllocation(Config, allocation))
        {
            return Blocked(ExecutiveFailure.OrcamentoInvalido);
        }

        ExecutiveState e = Executive;
        var lines = new List<ForecastLine>();
        Dictionary<string, double> before = TargetShift(e.Allocation), after = TargetShift(allocation);
        foreach (IndicatorDefinition indicator in Config.Indicators)
        {
            double delta = after.GetValueOrDefault(indicator.Id) - before.GetValueOrDefault(indicator.Id);
            if (Math.Abs(delta) > 1e-9)
            {
                lines.Add(Certain(GovForecast.IndicadorAlvo, indicator.Id, delta));
            }
        }

        double reserve = allocation.GetValueOrDefault(ExecutiveConfig.ReserveArea) - e.Allocation.GetValueOrDefault(ExecutiveConfig.ReserveArea);
        if (Math.Abs(reserve) > 1e-9)
        {
            lines.Add(Certain(GovForecast.Reserva, null, reserve));
        }

        var explanation = new List<string>
        {
            lines.Count == 0 ? Text("previsao_orcamento_igual") : Text("previsao_orcamento", ("velocidade", Texts.Percent(Config.IndicatorSpeedPerWeek))),
        };
        List<BudgetArea> below = Config.Areas.Where(a => allocation.GetValueOrDefault(a.Id) + 1e-9 < MinimumShare(a)).ToList();
        if (below.Count > 0)
        {
            explanation.Add(Text("previsao_orcamento_minimo", ("areas", string.Join(", ", below.Select(a => a.Name))), ("imagem", Signed(Config.MinimumViolationImage * 100))));
        }

        return new GovForecast(null, null, lines, [], explanation);
    }

    /// <summary>
    /// Para onde o orçamento empurra cada indicador: com os indicadores reais, a pressão do orçamento (a mesma da
    /// semana); sem eles, o alvo da regra da D-031 com o corte nos limites.
    /// </summary>
    private Dictionary<string, double> TargetShift(IReadOnlyDictionary<string, double> allocation)
    {
        Dictionary<string, double> targets = TargetsFor(allocation);
        if (Real is null)
        {
            return targets;
        }

        Dictionary<string, double> pressure = BudgetPressure(allocation);
        return Config.Indicators.ToDictionary(i => i.Id, i => Context.IndicatorEvolution!.Components(i.Id).Any() ? pressure.GetValueOrDefault(i.Id) : targets[i.Id], StringComparer.Ordinal);
    }

    // ---------------------------------------------------------------- crises

    /// <summary>Previsão de responder à crise pendente com a opção <paramref name="choiceIndex"/>: indicadores (na hora), imagem e o peso nas contas.</summary>
    public GovForecast ForecastCrisis(int choiceIndex)
    {
        ExecutiveState e = Executive;
        if (e.Crisis is not { } pending || Context.Content.Crises.Find(pending.CrisisId) is not { } crisis)
        {
            return GovForecast.Block(GovFailure.NaoEncontrado, Text("falha_sem_crise"));
        }

        CrisisChoice choice = crisis.Choices[Math.Clamp(choiceIndex, 0, crisis.Choices.Count - 1)];
        var shocks = choice.Indicators.Select(x => (x.Key, x.Value)).Append(("contas", -choice.Cost * Config.UnusedReserveToAccounts)).ToList();
        Dictionary<string, double> after = PredictShocks(shocks);
        var lines = new List<ForecastLine>();
        foreach (IndicatorDefinition indicator in Config.Indicators)
        {
            double delta = after.GetValueOrDefault(indicator.Id) - e.Indicators.GetValueOrDefault(indicator.Id);
            if (Math.Abs(delta) > 1e-9)
            {
                lines.Add(Certain(GovForecast.Indicador, indicator.Id, delta));
            }
        }

        double image = Math.Clamp(e.Image + choice.Image, -1, 1) - e.Image;
        if (Math.Abs(image) > 1e-12)
        {
            lines.Add(Certain(GovForecast.Imagem, null, image));
        }

        var explanation = new List<string>
        {
            Text("previsao_crise_imediato"),
            choice.Cost > 0 ? Text("previsao_crise_custo", ("pct", Share(choice.Cost)), ("reais", Reais(choice.Cost))) : Text("previsao_crise_sem_custo"),
        };
        return new GovForecast(null, null, lines, [], explanation);
    }

    /// <summary>
    /// Valor de cada indicador da prefeitura depois dos choques, um depois do outro, como <see cref="Shift"/> faria:
    /// nos indicadores reais, o choque em cada componente com os limites dele e a escala de jogo relida; nos demais,
    /// a soma com o corte nos limites. Não muda nada.
    /// </summary>
    private Dictionary<string, double> PredictShocks(IReadOnlyList<(string Indicator, double Delta)> shocks)
    {
        ExecutiveState e = Executive;
        var after = new Dictionary<string, double>(e.Indicators, StringComparer.Ordinal);
        MunicipalIndicatorsState? real = Real;
        IndicatorEvolution? evolution = Context.IndicatorEvolution;
        Dictionary<string, double>? values = real?.Indicadores.ToDictionary(v => v.Id, v => v.Valor, StringComparer.Ordinal);
        foreach (var (indicator, delta) in shocks)
        {
            if (real is not null && evolution!.Components(indicator).Any())
            {
                foreach (string id in evolution.Components(indicator))
                {
                    RealIndicatorDefinition d = evolution.Content.Definitions.Get(id);
                    var (min, max) = IndicatorScale.Bounds(d, real.Get(id).Partida);
                    values![id] = Math.Clamp(IndicatorScale.FromGame(d, IndicatorScale.ToGame(d, values[id]) + delta), min, max);
                }

                foreach (IndicatorDefinition game in Config.Indicators)
                {
                    if (Score(real, evolution, values!, game.Id) is double score)
                    {
                        after[game.Id] = Math.Clamp(score, Config.IndicatorBounds.Min, Config.IndicatorBounds.Max);
                    }
                }
            }
            else if (after.ContainsKey(indicator))
            {
                after[indicator] = Math.Clamp(after[indicator] + delta, Config.IndicatorBounds.Min, Config.IndicatorBounds.Max);
            }
        }

        return after;
    }

    /// <summary>A mesma conta de <see cref="IndicatorQueries.GameScore"/>, com valores reais hipotéticos.</summary>
    private static double? Score(MunicipalIndicatorsState real, IndicatorEvolution evolution, IReadOnlyDictionary<string, double> values, string gameIndicatorId)
    {
        IndicatorModelConfig model = evolution.Content.Model;
        if (!model.Jogo.TryGetValue(gameIndicatorId, out Dictionary<string, double>? parts))
        {
            return null;
        }

        double Judged(string id)
        {
            RealIndicatorDefinition d = evolution.Content.Definitions.Get(id);
            IndicatorValue v = real.Get(id);
            double now = IndicatorScale.ToGame(d, values[id]);
            return model.Julgamento.Neutro
                + (model.Julgamento.PesoVariacao * (now - IndicatorScale.ToGame(d, v.InicioMandato)))
                + (model.Julgamento.PesoComparacao * (now - IndicatorScale.ToGame(d, v.Referencia)));
        }

        return parts.Sum(p => p.Value * Judged(p.Key)) / parts.Values.Sum();
    }

    // ---------------------------------------------------------------- atos do gabinete

    /// <summary>
    /// Previsão de um ato do gabinete: energia, reconhecimento, imagem e apoio (com o teste de atributo, quando há, nos
    /// dois desfechos), o apoio do público escolhido e a relação com o vereador recebido.
    /// </summary>
    public GovForecast ForecastAction(string actionId, string? target)
    {
        MandateActionDefinition? action = Context.Content.Actions.Find(actionId);
        if (action is null)
        {
            return GovForecast.Block(GovFailure.NaoEncontrado, "");
        }

        if (CanExecute(action, target) is { } failure)
        {
            return Blocked(failure);
        }

        ExecutiveState e = Executive;
        var start = new ActSnapshot(e.Recognition, e.Image, new Dictionary<string, double>(e.Support, StringComparer.Ordinal));
        ActSnapshot certain = Act(start, action.Effects);
        var branches = new List<(double Chance, ActSnapshot State)>();
        var outcomes = new List<ForecastOutcome>();
        var explanation = new List<string> { action.Description };
        if (action.Test is { } test)
        {
            double chance = Math.Clamp(State.Player.Attributes.Get(test.Attribute) / 100.0, 0.05, 0.95);
            branches.Add((chance, Act(certain, test.Success)));
            branches.Add((1 - chance, Act(certain, test.Failure)));
            outcomes.Add(new ForecastOutcome(chance, test.SuccessText));
            outcomes.Add(new ForecastOutcome(1 - chance, test.FailureText));
            explanation.Add(Text("previsao_ato_teste", ("chance", Texts.Percent(chance))));
        }
        else
        {
            branches.Add((1, certain));
        }

        if (action.Targets.FirstOrDefault(t => t.Id == target) is { } chosen)
        {
            branches = branches.Select(b =>
            {
                var support = new Dictionary<string, double>(b.State.Support, StringComparer.Ordinal);
                support[chosen.Group] = Math.Clamp(support.GetValueOrDefault(chosen.Group) + chosen.Support, 0, 1);
                return (b.Chance, b.State with { Support = support });
            }).ToList();
        }

        var lines = new List<ForecastLine> { Certain(GovForecast.Energia, null, -action.Energy) };
        void Add(string kind, string? key, Func<ActSnapshot, double> value)
        {
            var deltas = branches.Select(b => (b.Chance, D: value(b.State) - value(start))).ToList();
            if (deltas.All(d => Math.Abs(d.D) < 1e-12))
            {
                return;
            }

            lines.Add(new ForecastLine(kind, key, deltas.Min(d => d.D), deltas.Max(d => d.D), deltas.Sum(d => d.Chance * d.D)));
        }

        Add(GovForecast.Reconhecimento, null, s => s.Recognition);
        Add(GovForecast.Imagem, null, s => s.Image);
        foreach (string group in branches.SelectMany(b => b.State.Support.Keys).Distinct().Order(StringComparer.Ordinal))
        {
            Add(GovForecast.Apoio, group, s => s.Support.GetValueOrDefault(group));
        }

        var remembered = new List<string>();
        if (action.Kind == MandateActionKind.Articular && e.Chamber.FirstOrDefault(m => m.Id == target) is { } member)
        {
            lines.Add(Certain(GovForecast.Relacao, member.Id, Math.Clamp(member.Relation + action.Relation, -100, 100) - member.Relation));
            explanation.Add(Text("previsao_ato_relacao", ("nome", member.Name), ("deriva", Texts.Number(Config.RelationDriftPerWeek, "0.0#"))));
            remembered.Add(member.Name);
        }

        return new GovForecast(null, null, lines, outcomes, explanation) { Remembered = remembered };
    }

    private sealed record ActSnapshot(double Recognition, double Image, Dictionary<string, double> Support);

    /// <summary>O que <see cref="Apply"/> faria com o estado <paramref name="s"/>, sem aplicar.</summary>
    private ActSnapshot Act(ActSnapshot s, Effects effects)
    {
        var support = new Dictionary<string, double>(s.Support, StringComparer.Ordinal);
        foreach (var (group, delta) in effects.Support)
        {
            support[group] = Math.Clamp(support.GetValueOrDefault(group) + delta, 0, 1);
        }

        return new ActSnapshot(EffectApplier.Gain(s.Recognition, effects.Recognition * Context.RecognitionScale), Math.Clamp(s.Image + effects.Image, -1, 1), support);
    }

    // ---------------------------------------------------------------- apoio

    private string IndicatorName(string id) => Config.Indicators.FirstOrDefault(i => i.Id == id)?.Name.ToLowerInvariant() ?? id;

    private static string Signed(double value) => (value < 0 ? "−" : "+") + Texts.Number(Math.Abs(value), "0.#");
}
