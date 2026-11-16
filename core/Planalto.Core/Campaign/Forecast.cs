using Planalto.Core.Events;

namespace Planalto.Core.Campaign;

/// <summary>Uma linha da previsão: o que muda, para quem, e a faixa de valores possíveis.</summary>
/// <param name="Kind">recognition, image, money ou support.</param>
/// <param name="Target">Grupo de eleitores (apoio); null nos demais.</param>
public sealed record ForecastLine(string Kind, string? Target, double Min, double Max, double Expected);

/// <summary>Um desfecho possível da ação ou escolha, com a chance e o texto que o jogo mostra.</summary>
public sealed record ForecastOutcome(double Probability, string Text);

/// <summary>
/// Previsão antes do clique (GDD 17.1, princípio f): faixa de cada efeito sobre todos os desfechos possíveis,
/// valor esperado e os desfechos sorteados com a chance de cada um. Calculada pelas mesmas regras de
/// <see cref="EffectApplier"/>, por isso o resultado real sempre cai dentro da faixa.
/// </summary>
public sealed record Forecast(IReadOnlyList<ForecastLine> Lines, IReadOnlyList<ForecastOutcome> Outcomes)
{
    /// <summary>O valor cabe na faixa de uma linha (com folga de arredondamento).</summary>
    public bool Contains(string kind, string? target, double value)
    {
        ForecastLine? line = Lines.FirstOrDefault(l => l.Kind == kind && l.Target == target);
        return line is null ? Math.Abs(value) < 1e-6 : value >= line.Min - 1e-6 && value <= line.Max + 1e-6;
    }
}

/// <summary>Uma etapa sorteada: alternativas que se excluem (chance, efeitos, texto).</summary>
public sealed record ForecastStage(IReadOnlyList<(double Chance, Effects Effects, string Text)> Branches);

public static class Forecaster
{
    /// <param name="Presence">Presença do jogador em cada zona (null quando a previsão não tem território).</param>
    private sealed record Snapshot(double Recognition, double Image, decimal Money, IReadOnlyDictionary<string, double> Support, double[]? Presence = null);

    /// <summary>
    /// Enumera todas as combinações das etapas a partir do estado atual do candidato do jogador e devolve as faixas.
    /// A primeira etapa costuma ser a certa (chance 1); depois vêm teste de atributo e riscos.
    /// </summary>
    /// <param name="territory">
    /// Com o território, o reconhecimento é previsto como acontece: a mudança vale em cada zona (com o piso e o teto
    /// de cada uma) e o da cidade é a média pela população (GDD 17.5).
    /// </param>
    /// <param name="zone">Zona em que vale a presença de zona dos efeitos (evento com lugar); −1 se não há.</param>
    /// <param name="zoneFactor">Quanto 1 ponto de presença de zona vale ali (escala do porte × alcance da zona).</param>
    public static Forecast Run(Candidate player, decimal money, double recognitionScale, IReadOnlyList<ForecastStage> stages, Territory.TerritoryState? territory = null, int zone = -1, double zoneFactor = 0)
    {
        double[]? presence = territory is not null && territory.Presence.TryGetValue(player.Id, out double[]? p) ? [.. p] : null;
        var start = new Snapshot(presence is null ? player.Recognition : Average(presence, territory!), player.Image, money, new Dictionary<string, double>(player.Support), presence);
        var results = new List<(double P, Snapshot S)>();
        Walk(start, 1, 0);

        var lines = new List<ForecastLine>();
        void Add(string kind, string? target, Func<Snapshot, double> value)
        {
            var deltas = results.Select(r => (r.P, D: value(r.S) - value(start))).ToList();
            if (deltas.All(d => Math.Abs(d.D) < 1e-9))
            {
                return;
            }

            lines.Add(new ForecastLine(kind, target, deltas.Min(d => d.D), deltas.Max(d => d.D), deltas.Sum(d => d.P * d.D)));
        }

        Add("recognition", null, s => s.Recognition);
        Add("image", null, s => s.Image);
        Add("money", null, s => (double)s.Money);
        foreach (string group in results.SelectMany(r => r.S.Support.Keys).Union(start.Support.Keys).Distinct().Order(StringComparer.Ordinal))
        {
            Add("support", group, s => s.Support.GetValueOrDefault(group));
        }

        var outcomes = stages.SelectMany(stage => stage.Branches.Where(b => b.Chance is > 0 and < 1 && b.Text.Length > 0))
            .Select(b => new ForecastOutcome(b.Chance, b.Text))
            .ToList();
        return new Forecast(lines, outcomes);

        void Walk(Snapshot s, double p, int index)
        {
            if (index == stages.Count)
            {
                results.Add((p, s));
                return;
            }

            foreach (var (chance, effects, _) in stages[index].Branches.Where(b => b.Chance > 0))
            {
                Walk(Apply(s, effects, recognitionScale, territory, zone, zoneFactor), p * chance, index + 1);
            }
        }
    }

    private static double Average(double[] presence, Territory.TerritoryState territory)
    {
        double pop = territory.Population.Sum(x => (double)x);
        return pop <= 0 ? presence.Average() : presence.Select((v, i) => v * territory.Population[i]).Sum() / pop;
    }

    private static Snapshot Apply(Snapshot s, Effects e, double recognitionScale, Territory.TerritoryState? territory, int zone, double zoneFactor)
    {
        var support = new Dictionary<string, double>(s.Support);
        foreach (var (group, delta) in e.Support)
        {
            support[group] = Math.Clamp(support.GetValueOrDefault(group) + delta, 0, 1);
        }

        double[]? presence = s.Presence?.Select(v => EffectApplier.Gain(v, e.Recognition * recognitionScale)).ToArray();
        if (presence is not null && zone >= 0 && zone < presence.Length && Math.Abs(e.ZonePresence) > 1e-12)
        {
            presence[zone] = EffectApplier.Gain(presence[zone], e.ZonePresence * zoneFactor);
        }

        return new Snapshot(
            presence is null ? EffectApplier.Gain(s.Recognition, e.Recognition * recognitionScale) : Average(presence, territory!),
            Math.Clamp(s.Image + e.Image, -1, 1),
            s.Money + e.Money,
            support,
            presence);
    }

    public static ForecastStage Certain(Effects effects) => new([(1, effects, "")]);

    /// <summary>Teste de atributo: passa com a chance do atributo, limitada entre 5% e 95% (mesma regra do motor).</summary>
    public static ForecastStage Test(double chance, Effects success, string successText, Effects failure, string failureText) =>
        new([(chance, success, successText), (1 - chance, failure, failureText)]);

    public static ForecastStage Risk(double chance, Effects effects, string text) => new([(chance, effects, text), (1 - chance, Effects.None, "")]);

    /// <summary>Previsão de uma escolha de evento: efeitos certos mais o teste, se houver.</summary>
    /// <param name="pending">O evento em curso: dá a zona em que vale a presença de zona dos efeitos.</param>
    public static Forecast ForChoice(CampaignContext ctx, EventChoice choice, PendingEvent? pending = null)
    {
        var stages = new List<ForecastStage> { Certain(choice.Effects) };
        if (choice.Test is { } test)
        {
            double chance = Math.Clamp((ctx.State.Player.Attributes.Get(test.Attribute) / 100.0) + test.Bonus, 0.05, 0.95);
            stages.Add(Test(chance, test.Success, test.SuccessText, test.Failure, test.FailureText));
        }

        var (zone, factor) = EffectApplier.EventZone(ctx, pending);
        return Run(ctx.State.Campaign!.Player, ctx.State.Player.Resources.Money, 1, stages, ctx.State.Campaign.Territory, zone, factor);
    }
}
