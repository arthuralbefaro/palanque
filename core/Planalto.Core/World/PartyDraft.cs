using Planalto.Core.Ideology;
using Planalto.Core.Parties;

namespace Planalto.Core.World;

/// <summary>Campos da lista de partidos que podem ser travados antes de um sorteio (GDD 16.1).</summary>
public enum PartyField
{
    Name,
    Sigla,
    Number,
    Family,
    Position,
    Size,

    /// <summary>Cores e logotipo.</summary>
    Visual,
    Leadership,
}

/// <summary>Uma linha da lista de partidos da tela "Novo Mundo".</summary>
public sealed class PartyDraft
{
    public required string Id { get; init; }

    public required string Name { get; set; }

    public required string Sigla { get; set; }

    public required int Number { get; set; }

    public required string FamilyId { get; set; }

    public required IdeologicalPosition Position { get; set; }

    public required PartySize Size { get; set; }

    public required PartyColors Colors { get; set; }

    public required string LogoIcon { get; set; }

    public required LeadershipStyle Leadership { get; set; }

    public HashSet<PartyField> Locks { get; init; } = [];

    /// <summary>Estatuto, só no partido criado pelo jogador.</summary>
    public PartyCharter? Charter { get; set; }

    public bool IsLocked(PartyField field) => Locks.Contains(field);

    public void SetLocked(PartyField field, bool locked)
    {
        if (locked)
        {
            Locks.Add(field);
        }
        else
        {
            Locks.Remove(field);
        }
    }

    public PartyDraft Clone(string id) => new()
    {
        Id = id,
        Name = Name,
        Sigla = Sigla,
        Number = Number,
        FamilyId = FamilyId,
        Position = Position,
        Size = Size,
        Colors = Colors,
        LogoIcon = LogoIcon,
        Leadership = Leadership,
        Charter = Charter,
        Locks = [.. Locks],
    };
}
