namespace Planalto.Core.Characters;

/// <summary>Atributos do personagem, sempre entre 0 e 100.</summary>
public sealed class Attributes
{
    public const int Min = 0;
    public const int Max = 100;

    private int _carisma;
    private int _oratoria;
    private int _articulacao;
    private int _gestao;
    private int _integridade;

    public int Carisma { get => _carisma; set => _carisma = Clamp(value); }

    public int Oratoria { get => _oratoria; set => _oratoria = Clamp(value); }

    public int Articulacao { get => _articulacao; set => _articulacao = Clamp(value); }

    public int Gestao { get => _gestao; set => _gestao = Clamp(value); }

    public int Integridade { get => _integridade; set => _integridade = Clamp(value); }

    public int Get(AttributeId id) => id switch
    {
        AttributeId.Carisma => Carisma,
        AttributeId.Oratoria => Oratoria,
        AttributeId.Articulacao => Articulacao,
        AttributeId.Gestao => Gestao,
        AttributeId.Integridade => Integridade,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null),
    };

    public void Set(AttributeId id, int value)
    {
        switch (id)
        {
            case AttributeId.Carisma: Carisma = value; break;
            case AttributeId.Oratoria: Oratoria = value; break;
            case AttributeId.Articulacao: Articulacao = value; break;
            case AttributeId.Gestao: Gestao = value; break;
            case AttributeId.Integridade: Integridade = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(id), id, null);
        }
    }

    public void Add(AttributeId id, int delta) => Set(id, Get(id) + delta);

    /// <summary>Aplica modificadores de traços ou profissão (valores vêm dos JSON).</summary>
    public void Apply(IReadOnlyDictionary<AttributeId, int> modifiers)
    {
        foreach (var (id, delta) in modifiers)
        {
            Add(id, delta);
        }
    }

    private static int Clamp(int value) => Math.Clamp(value, Min, Max);
}
