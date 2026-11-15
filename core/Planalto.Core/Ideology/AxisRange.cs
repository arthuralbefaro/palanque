namespace Planalto.Core.Ideology;

/// <summary>Faixa fechada num eixo ideológico (GDD 5.2: de -100 a +100).</summary>
public sealed record AxisRange(int Min, int Max)
{
    public const int AxisMin = -100;
    public const int AxisMax = 100;

    public static AxisRange Full { get; } = new(AxisMin, AxisMax);

    public bool Contains(int value) => value >= Min && value <= Max;

    public IEnumerable<string> Validate(string where)
    {
        if (Min < AxisMin || Max > AxisMax)
        {
            yield return $"{where}: faixa deve ficar entre {AxisMin} e {AxisMax} (valor: {Min} a {Max}).";
        }

        if (Min > Max)
        {
            yield return $"{where}: 'min' maior que 'max' ({Min} > {Max}).";
        }
    }
}

/// <summary>Posição nos dois eixos: Econômico (estatista ↔ liberal) e Costumes (progressista ↔ conservador).</summary>
public readonly record struct IdeologicalPosition(int Economic, int Social)
{
    public bool IsValid => AxisRange.Full.Contains(Economic) && AxisRange.Full.Contains(Social);
}
