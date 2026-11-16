using Planalto.Core.Data;

namespace Planalto.Core.People;

/// <summary>Temperamento de uma pessoa (GDD 17.2): decide como ela reage a recusas, ataques e promessas quebradas.</summary>
public enum Temperament
{
    Conciliador,
    Combativo,
    Calculista,
    Leal,
    Voluvel,
}

/// <summary>Papel da pessoa em relação à carreira do jogador. Uma pessoa pode ter mais de um.</summary>
public enum PersonRole
{
    Rival,
    Lideranca,
    Equipe,
    Doador,
    Jornalista,
    Assessor,
    Secretario,
    Vereador,
    Dirigente,
}

/// <summary>O que a pessoa quer: um tema, uma zona ou um objetivo próprio.</summary>
public sealed class Interest
{
    public const string Tema = "tema";
    public const string ZonaKind = "zona";
    public const string Objetivo = "objetivo";

    public required string Kind { get; init; }

    /// <summary>Id do tema ou da zona; para objetivo, uma chave de texto do conteúdo.</summary>
    public required string Target { get; init; }
}

/// <summary>
/// Lembrança datada (GDD 17.2). <see cref="Peso"/> vai de −3 a +3 e define se ela se apaga: pesos 1 e 2 perdem
/// metade do efeito a cada 52 semanas; peso 3 não se apaga. <see cref="Pontos"/> é o efeito na relação.
/// </summary>
public sealed class Memory
{
    public required DateOnly Date { get; init; }

    /// <summary>Tipo do fato (chave estável, usada por testes e cadeias): "visita", "promessa_aceita", "recusa"…</summary>
    public required string Fact { get; init; }

    public required int Peso { get; init; }

    public required int Pontos { get; init; }

    /// <summary>Texto mostrado na ficha ("Veio à festa da comunidade").</summary>
    public required string Text { get; init; }
}

/// <summary>Parâmetros da memória e da relação (data/people/config.json).</summary>
public sealed class PeopleConfig : IValidatable
{
    public const string File = "people/config.json";

    public required string Note { get; init; }

    public required int MaxMemories { get; init; }

    /// <summary>Semanas para uma lembrança de peso 1 ou 2 perder metade do efeito.</summary>
    public required int HalfLifeWeeks { get; init; }

    public IEnumerable<string> Validate()
    {
        if (MaxMemories < 1 || HalfLifeWeeks < 1)
        {
            yield return "max_memories e half_life_weeks devem ser positivos.";
        }
    }
}

/// <summary>
/// Pessoa com rosto e memória (GDD 17.2): um só modelo para rival, liderança, equipe, doador, jornalista, assessor,
/// secretário e vereador. Nome, nascimento e aparência ficam no <see cref="Characters.Character"/> de mesmo id em
/// <see cref="State.GameState.Npcs"/>; aqui fica o que ela quer e o que ela lembra do jogador.
/// </summary>
public sealed class Person
{
    public const int MinRelation = -100;
    public const int MaxRelation = 100;

    /// <summary>Id do personagem em <see cref="State.GameState.Npcs"/>.</summary>
    public required string Id { get; init; }

    public required List<PersonRole> Roles { get; init; }

    public required List<Interest> Interests { get; init; }

    public required Temperament Temperament { get; init; }

    /// <summary>Afinidade de partida com o jogador (partido, ideologia, vínculos), antes de qualquer lembrança.</summary>
    public double Affinity { get; set; }

    public List<Memory> Memories { get; init; } = [];

    /// <summary>Última vez em que o jogador esteve com a pessoa.</summary>
    public DateOnly? LastContact { get; set; }

    /// <summary>Relação com o jogador hoje (−100 a 100): afinidade mais as lembranças, com o esquecimento de cada uma.</summary>
    public double Relation(DateOnly today, PeopleConfig config) =>
        Math.Clamp(Affinity + Memories.Sum(m => Weight(m, today, config)), MinRelation, MaxRelation);

    /// <summary>Efeito atual de uma lembrança na relação.</summary>
    public static double Weight(Memory memory, DateOnly today, PeopleConfig config)
    {
        if (Math.Abs(memory.Peso) >= 3)
        {
            return memory.Pontos;
        }

        double weeks = Math.Max(0, today.DayNumber - memory.Date.DayNumber) / 7.0;
        return memory.Pontos * Math.Pow(0.5, weeks / config.HalfLifeWeeks);
    }

    /// <summary>
    /// Grava uma lembrança. Com a lista cheia, sai a mais fraca hoje entre as que se apagam; as de peso 3 só saem se
    /// todas forem de peso 3 (aí sai a mais antiga).
    /// </summary>
    public Memory Remember(DateOnly date, string fact, int peso, int pontos, string text, PeopleConfig config)
    {
        var memory = new Memory { Date = date, Fact = fact, Peso = Math.Clamp(peso, -3, 3), Pontos = pontos, Text = text };
        Memories.Add(memory);
        while (Memories.Count > config.MaxMemories)
        {
            Memory drop = Memories.Where(m => !ReferenceEquals(m, memory) && Math.Abs(m.Peso) < 3)
                    .OrderBy(m => Math.Abs(Weight(m, date, config))).ThenBy(m => m.Date).FirstOrDefault()
                ?? Memories.Where(m => !ReferenceEquals(m, memory)).OrderBy(m => m.Date).First();
            Memories.Remove(drop);
        }

        return memory;
    }

    public bool Has(PersonRole role) => Roles.Contains(role);
}
