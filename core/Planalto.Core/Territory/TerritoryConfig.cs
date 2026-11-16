using Planalto.Core.Characters;
using Planalto.Core.Data;

namespace Planalto.Core.Territory;

/// <summary>Parâmetros do território (data/territory/config.json, GDD 17.5).</summary>
public sealed class TerritoryConfig : IValidatable
{
    public const string File = "territory/config.json";

    public required string Note { get; init; }

    public required Dictionary<string, AgeRange> ZonasPorPorte { get; init; }

    public required int VerticesPorZona { get; init; }

    public required double ToleranciaInicialGraus { get; init; }

    public required double ToleranciaMaximaGraus { get; init; }

    /// <summary>Cobertura mínima de um bloco do perfil para a zona usar o próprio dado (regra de sigilo).</summary>
    public required double CoberturaMinima { get; init; }

    /// <summary>Fração da população num só distrito para a zona levar o nome dele.</summary>
    public required double DistritoDominante { get; init; }

    public IEnumerable<string> Validate()
    {
        foreach (string porte in new[] { "pequeno", "medio", "grande", "metropole" })
        {
            if (!ZonasPorPorte.TryGetValue(porte, out AgeRange? range) || range.Min < 1 || range.Max < range.Min)
            {
                yield return $"zonas_por_porte.{porte}: mínimo de 1 e máximo maior ou igual ao mínimo.";
            }
        }

        if (VerticesPorZona < 8 || ToleranciaInicialGraus <= 0 || ToleranciaMaximaGraus < ToleranciaInicialGraus)
        {
            yield return "vertices_por_zona (8 ou mais) e tolerâncias positivas e crescentes.";
        }

        if (CoberturaMinima is <= 0 or > 1 || DistritoDominante is <= 0.5 or > 1)
        {
            yield return "cobertura_minima entre 0 e 1; distrito_dominante entre 0,5 e 1.";
        }
    }
}
