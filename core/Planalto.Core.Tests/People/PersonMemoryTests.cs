using Planalto.Core.Data;
using Planalto.Core.People;
using Planalto.Core.Tests.Parties;

namespace Planalto.Core.Tests.People;

/// <summary>Pessoa com memória (GDD 17.2): teto de lembranças, peso 3 que não se apaga e meia-vida das outras.</summary>
public class PersonMemoryTests
{
    private static readonly PeopleConfig Config = JsonData.LoadFile<PeopleConfig>(Path.Combine(TestContent.DataDirectory, PeopleConfig.File)).GetValueOrThrow();

    private static readonly DateOnly Start = new(2028, 3, 6);

    private static Person NewPerson(double affinity = 0) => new()
    {
        Id = "npc_teste",
        Roles = [PersonRole.Lideranca],
        Interests = [new Interest { Kind = Interest.Tema, Target = "saude" }],
        Temperament = Temperament.Conciliador,
        Affinity = affinity,
    };

    [Fact]
    public void Configuracao_segue_o_GDD()
    {
        Assert.Empty(Config.Validate());
        Assert.Equal(20, Config.MaxMemories);
        Assert.Equal(52, Config.HalfLifeWeeks);
    }

    [Fact]
    public void Guarda_no_maximo_vinte_lembrancas_e_descarta_a_mais_fraca()
    {
        Person person = NewPerson();
        for (int i = 0; i < Config.MaxMemories; i++)
        {
            person.Remember(Start.AddDays(7 * i), $"fato_{i}", 1, 2 + i, $"Lembrança {i}", Config);
        }

        Assert.Equal(Config.MaxMemories, person.Memories.Count);

        // A 21ª entra, e sai a de menor efeito hoje (a mais antiga e de menos pontos), nunca a que acabou de entrar.
        DateOnly today = Start.AddDays(7 * Config.MaxMemories);
        Memory added = person.Remember(today, "novo", 1, 1, "Fato novo e fraco", Config);
        Assert.Equal(Config.MaxMemories, person.Memories.Count);
        Assert.Contains(added, person.Memories);
        Assert.DoesNotContain(person.Memories, m => m.Fact == "fato_0");
        Assert.Contains(person.Memories, m => m.Fact == "fato_1");

        for (int i = 0; i < 100; i++)
        {
            person.Remember(today.AddDays(i), $"extra_{i}", i % 2 == 0 ? 2 : -1, i % 2 == 0 ? 3 : -3, "Mais um", Config);
            Assert.Equal(Config.MaxMemories, person.Memories.Count);
        }
    }

    [Fact]
    public void Lembranca_de_peso_tres_nao_se_apaga_nem_sai_da_lista()
    {
        Person person = NewPerson();
        Memory promise = person.Remember(Start, "promessa", 3, 22, "Prometeu a creche", Config);
        Memory betrayal = person.Remember(Start, "promessa_quebrada", -3, -30, "Não cumpriu", Config);

        // Dez anos depois, o efeito é o mesmo.
        DateOnly later = Start.AddDays(7 * 52 * 10);
        Assert.Equal(22, Person.Weight(promise, later, Config));
        Assert.Equal(-30, Person.Weight(betrayal, later, Config));
        Assert.Equal(-8, person.Relation(later, Config));

        // Cem lembranças comuns depois, as duas continuam lá, mesmo sendo as mais antigas.
        for (int i = 0; i < 100; i++)
        {
            person.Remember(later.AddDays(i), $"visita_{i}", 2, 40, "Visita", Config);
        }

        Assert.Equal(Config.MaxMemories, person.Memories.Count);
        Assert.Contains(promise, person.Memories);
        Assert.Contains(betrayal, person.Memories);

        // Só com a lista inteira de peso 3 é que a mais antiga sai.
        Person stubborn = NewPerson();
        for (int i = 0; i <= Config.MaxMemories; i++)
        {
            stubborn.Remember(Start.AddDays(i), $"marco_{i}", 3, 5, "Marco", Config);
        }

        Assert.Equal(Config.MaxMemories, stubborn.Memories.Count);
        Assert.DoesNotContain(stubborn.Memories, m => m.Fact == "marco_0");
        Assert.Contains(stubborn.Memories, m => m.Fact == $"marco_{Config.MaxMemories}");
    }

    [Theory]
    [InlineData(1, 4)]
    [InlineData(2, 10)]
    [InlineData(-1, -4)]
    [InlineData(-2, -8)]
    public void Lembrancas_de_peso_um_e_dois_perdem_metade_em_52_semanas(int peso, int pontos)
    {
        Person person = NewPerson(affinity: 5);
        Memory memory = person.Remember(Start, "visita", peso, pontos, "Visita", Config);

        Assert.Equal(pontos, Person.Weight(memory, Start, Config), 9);
        Assert.Equal(pontos / 2.0, Person.Weight(memory, Start.AddDays(7 * Config.HalfLifeWeeks), Config), 9);
        Assert.Equal(pontos / 4.0, Person.Weight(memory, Start.AddDays(7 * Config.HalfLifeWeeks * 2), Config), 9);
        Assert.Equal(5 + (pontos / 2.0), person.Relation(Start.AddDays(7 * Config.HalfLifeWeeks), Config), 9);

        // No meio do caminho já perdeu parte; data anterior à lembrança não a aumenta.
        double half = Person.Weight(memory, Start.AddDays(7 * 26), Config);
        Assert.InRange(Math.Abs(half), Math.Abs(pontos) / 2.0, Math.Abs(pontos));
        Assert.Equal(pontos, Person.Weight(memory, Start.AddDays(-30), Config), 9);
    }

    [Fact]
    public void Relacao_e_a_afinidade_mais_as_lembrancas_dentro_dos_limites()
    {
        Person person = NewPerson(affinity: -10);
        Assert.Equal(-10, person.Relation(Start, Config));
        person.Remember(Start, "visita", 1, 4, "Visita", Config);
        person.Remember(Start, "promessa", 3, 22, "Promessa", Config);
        Assert.Equal(16, person.Relation(Start, Config), 9);

        for (int i = 0; i < 10; i++)
        {
            person.Remember(Start, "promessa", 3, 40, "Promessa", Config);
        }

        Assert.Equal(Person.MaxRelation, person.Relation(Start, Config));
        Person enemy = NewPerson(affinity: -90);
        enemy.Remember(Start, "recusa", -3, -50, "Recusa", Config);
        Assert.Equal(Person.MinRelation, enemy.Relation(Start, Config));

        // O peso fica entre -3 e 3.
        Assert.Equal(3, person.Remember(Start, "x", 9, 1, "x", Config).Peso);
        Assert.Equal(-3, person.Remember(Start, "y", -9, -1, "y", Config).Peso);
    }
}
