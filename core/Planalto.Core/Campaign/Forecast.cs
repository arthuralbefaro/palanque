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
    private sealed record Snapshot(double Recognition, double Image, decimal Money, IReadOnlyDictionary<string, double> Support);

    /// <summary>
    /// Enumera todas as combinações das etapas a partir do estado atual do candidato do jogador e devolve as faixas.
    /// A primeira etapa costuma ser a certa (chance 1); depois vêm teste de atributo e riscos.
    /// </summary>
    public static Forecast Run(Candidate player, decimal money, double recognitionScale, IReadOnlyList<ForecastStage> stages)
    {
        var start = new Snapshot(player.Recognition, player.Image, money, new Dictionary<string, double>(player.Support));
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
                Walk(Apply(s, effects, recognitionScale), p * chance, index + 1);
            }
        }
    }

    private static Snapshot Apply(Snapshot s, Effects e, double recognitionScale)
    {
        var support = new Dictionary<string, double>(s.Support);
        foreach (var (group, delta) in e.Support)
        {
            support[group] = Math.Clamp(support.GetValueOrDefault(group) + delta, 0, 1);
        }

        return new Snapshot(
            EffectApplier.Gain(s.Recognition, e.Recognition * recognitionScale),
            Math.Clamp(s.Image + e.Image, -1, 1),
            s.Money + e.Money,
            support);
    }

    public static ForecastStage Certain(Effects effects) => new([(1, effects, "")]);

    /// <summary>Teste de atributo: passa com a chance do atributo, limitada entre 5% e 95% (mesma regra do motor).</summary>
    public static ForecastStage Test(double chance, Effects success, string successText, Effects failure, string failureText) =>
        new([(chance, success, successText), (1 - chance, failure, failureText)]);

    public static ForecastStage Risk(double chance, Effects effects, string text) => new([(chance, effects, text), (1 - chance, Effects.None, "")]);

    /// <summary>Previsão de uma escolha de evento: efeitos certos mais o teste, se houver.</summary>
    public static Forecast ForChoice(CampaignContext ctx, EventChoice choice)
    {
        var stages = new List<ForecastStage> { Certain(choice.Effects) };
        if (choice.Test is { } test)
        {
            double chance = Math.Clamp((ctx.State.Player.Attributes.Get(test.Attribute) / 100.0) + test.Bonus, 0.05, 0.95);
            stages.Add(Test(chance, test.Success, test.SuccessText, test.Failure, test.FailureText));
        }

        return Run(ctx.State.Campaign!.Player, ctx.State.Player.Resources.Money, 1, stages);
    }
}
