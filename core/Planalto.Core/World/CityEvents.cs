using Planalto.Core.Data;
using Planalto.Core.State;

namespace Planalto.Core.World;

/// <summary>
/// Tipo de evento da cidade (GDD 17.6): feira, festa, assembleia, jogo. Só existe no dia; quem vai encontra o
/// público dele. Evento religioso declara a religião (categoria do Censo) e ocorre na proporção dela no município.
/// </summary>
public sealed class CityEventType
{
    public required string Id { get; init; }

    public required string Nome { get; init; }

    public required List<string> Portes { get; init; }

    /// <summary>Meses em que ocorre (1 a 12); vazio = o ano todo.</summary>
    public List<int> Meses { get; init; } = [];

    /// <summary>Dia da semana: 0 = segunda … 6 = domingo.</summary>
    public required int DiaSemana { get; init; }

    public required Turno Turno { get; init; }

    /// <summary>"urbana", "rural" ou "qualquer".</summary>
    public required string Situacao { get; init; }

    /// <summary>Grupo de eleitores presente, ou "geral".</summary>
    public required string Publico { get; init; }

    /// <summary>Chance por semana de acontecer (antes do peso da religião).</summary>
    public required double ChanceSemanal { get; init; }

    /// <summary>Grupo de religião dos segmentos ("catolica", "evangelica", "outras"): a chance é multiplicada pela fatia dele no município.</summary>
    public string? Religiao { get; init; }
}

/// <summary>data/world/city_events.json.</summary>
public sealed class CityEventCatalog : IValidatable
{
    public const string File = "world/city_events.json";

    public required string Note { get; init; }

    /// <summary>Multiplicador da chance dos eventos religiosos, para que a soma deles não dependa do número de tipos.</summary>
    public required double ReligiosoEscala { get; init; }

    public required List<CityEventType> Tipos { get; init; }

    public IEnumerable<string> Validate()
    {
        foreach (string error in DataChecks.UniqueIds(Tipos.Select(t => t.Id), "Evento da cidade"))
        {
            yield return error;
        }

        foreach (CityEventType t in Tipos)
        {
            if (t.DiaSemana is < 0 or > 6 || t.ChanceSemanal is <= 0 or > 1 || t.Portes.Count == 0 || t.Meses.Any(m => m is < 1 or > 12))
            {
                yield return $"evento '{t.Id}': dia da semana de 0 a 6, chance em (0, 1], portes e meses válidos.";
            }

            if (t.Situacao is not ("urbana" or "rural" or "qualquer"))
            {
                yield return $"evento '{t.Id}': situação '{t.Situacao}' desconhecida.";
            }

            // Limite de texto do ART_GUIDE: item de agenda em até 2 linhas.
            if (t.Nome.Length > 40)
            {
                yield return $"evento '{t.Id}': nome com mais de 40 caracteres.";
            }
        }

        // Neutralidade (GDD 17.6): eventos religiosos proporcionais à composição do Censo, isto é, cada religião com
        // a mesma chance de base; a proporção vem do município.
        var religious = Tipos.Where(t => t.Religiao is not null).GroupBy(t => t.Religiao!).Select(g => g.Sum(t => t.ChanceSemanal)).ToList();
        if (religious.Count > 0 && religious.Max() - religious.Min() > 1e-9)
        {
            yield return "eventos religiosos: a soma das chances de base tem de ser igual entre as religiões (a proporção vem do Censo do município).";
        }

        if (ReligiosoEscala <= 0)
        {
            yield return "religioso_escala deve ser positivo.";
        }
    }
}
