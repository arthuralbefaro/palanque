namespace Planalto.Core.State;

public static class RngExtensions
{
    /// <summary>Real em [min, max).</summary>
    public static double NextDouble(this Rng rng, double min, double max) => min + (rng.NextDouble() * (max - min));

    /// <summary>True com a probabilidade informada.</summary>
    public static bool Chance(this Rng rng, double probability) => rng.NextDouble() < probability;

    public static T Pick<T>(this Rng rng, IReadOnlyList<T> items)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("Lista vazia.", nameof(items));
        }

        return items[rng.NextInt(items.Count)];
    }

    /// <summary>Sorteio ponderado; itens com peso zero ou negativo nunca saem.</summary>
    public static T PickWeighted<T>(this Rng rng, IReadOnlyList<T> items, Func<T, double> weight)
    {
        double total = 0;
        foreach (T item in items)
        {
            total += Math.Max(0, weight(item));
        }

        if (total <= 0)
        {
            throw new ArgumentException("Nenhum item com peso positivo.", nameof(items));
        }

        double target = rng.NextDouble() * total;
        T? last = default;
        foreach (T item in items)
        {
            double w = Math.Max(0, weight(item));
            if (w <= 0)
            {
                continue;
            }

            last = item;
            if (target < w)
            {
                return item;
            }

            target -= w;
        }

        // Só por arredondamento de ponto flutuante.
        return last!;
    }

    /// <summary>Embaralha uma cópia da lista (Fisher–Yates).</summary>
    public static List<T> Shuffled<T>(this Rng rng, IEnumerable<T> items)
    {
        var list = items.ToList();
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.NextInt(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }
}
