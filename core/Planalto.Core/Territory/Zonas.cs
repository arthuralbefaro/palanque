using Planalto.Core.Data;
using Planalto.Core.Geo;
using Planalto.Core.Municipios;

namespace Planalto.Core.Territory;

/// <summary>
/// Zonas do município (GDD 17.5): grupos de setores censitários do Censo 2022, um arquivo por UF em data/zonas/.
/// Os contornos ficam à parte, em data/geo/zonas/, porque só a tela do mapa precisa deles.
/// </summary>
public sealed class ZonasUf : IValidatable
{
    public required string Uf { get; init; }

    public required string Fonte { get; init; }

    public required string Metodo { get; init; }

    public required List<ZonasMunicipio> Municipios { get; init; }

    public IEnumerable<string> Validate()
    {
        foreach (ZonasMunicipio m in Municipios)
        {
            if (m.Zonas.Count == 0)
            {
                yield return $"{Uf}/{m.CodigoIbge}: sem zonas.";
            }

            var ids = m.Zonas.Select(z => z.Id).ToHashSet(StringComparer.Ordinal);
            if (ids.Count != m.Zonas.Count)
            {
                yield return $"{Uf}/{m.CodigoIbge}: id de zona repetido.";
            }

            foreach (Zona z in m.Zonas)
            {
                if (string.IsNullOrWhiteSpace(z.Nome) || z.Populacao < 0 || z.Setores <= 0)
                {
                    yield return $"{z.Id}: nome, população ou setores inválidos.";
                }

                if (z.Situacao is not (Zona.Urbana or Zona.Rural))
                {
                    yield return $"{z.Id}: situação '{z.Situacao}' desconhecida.";
                }

                if (z.NomeFonte is not (Zona.NomeBairro or Zona.NomeDistrito or Zona.NomeGerado))
                {
                    yield return $"{z.Id}: fonte do nome '{z.NomeFonte}' desconhecida.";
                }

                foreach (string v in z.Vizinhas.Where(v => !ids.Contains(v) || v == z.Id))
                {
                    yield return $"{z.Id}: vizinha '{v}' inválida.";
                }

                foreach (string error in z.Perfil.Validate(z))
                {
                    yield return error;
                }
            }
        }
    }
}

public sealed class ZonasMunicipio
{
    public required int CodigoIbge { get; init; }

    public required List<Zona> Zonas { get; init; }

    public long Populacao => Zonas.Sum(z => z.Populacao);
}

public sealed class Zona
{
    public const string Urbana = "urbana";
    public const string Rural = "rural";

    /// <summary>Nome do bairro mais populoso da zona, pela malha de bairros do IBGE.</summary>
    public const string NomeBairro = "bairro_ibge";

    /// <summary>Nome do distrito do IBGE.</summary>
    public const string NomeDistrito = "distrito_ibge";

    /// <summary>Nome descritivo do jogo ("Centro", "Zona Norte"), não é dado oficial.</summary>
    public const string NomeGerado = "gerado";

    /// <summary>Código IBGE do município, hífen e número da zona com dois dígitos ("3550308-03").</summary>
    public required string Id { get; init; }

    public required string Nome { get; init; }

    public required string NomeFonte { get; init; }

    public required string Situacao { get; init; }

    /// <summary>População residente (agregado básico do Censo 2022, V0001, sem supressão).</summary>
    public required long Populacao { get; init; }

    /// <summary>Domicílios particulares permanentes ocupados (agregado básico).</summary>
    public required long Domicilios { get; init; }

    public required int Setores { get; init; }

    public required double AreaKm2 { get; init; }

    /// <summary>Ponto interior da zona, para rótulos e distâncias.</summary>
    public required double Lat { get; init; }

    public required double Lon { get; init; }

    public required List<string> Vizinhas { get; init; }

    public required PerfilZona Perfil { get; init; }

    public bool IsRural => Situacao == Rural;
}

/// <summary>
/// Perfil da zona pelos agregados por setor. Células suprimidas por sigilo não são estimadas: cada bloco guarda a
/// cobertura (população dos setores com dado ÷ população da zona) e, abaixo do mínimo, usa o perfil do município
/// (GDD 17.5, regra de sigilo).
/// </summary>
public sealed class PerfilZona
{
    public const string FonteZona = "zona";
    public const string FonteMunicipio = "municipio";

    /// <summary>Pessoas por faixa etária (mesmas faixas de <see cref="PerfilCategorias.Idade"/>), soma dos setores com dado.</summary>
    public required Dictionary<string, long> Idade { get; init; }

    public required double IdadeCobertura { get; init; }

    public required string IdadeFonte { get; init; }

    /// <summary>Rendimento nominal médio mensal do responsável pelo domicílio (R$), média dos setores pesada pelos domicílios.</summary>
    public double? RendaResponsavel { get; init; }

    public required double RendaCobertura { get; init; }

    public required string RendaFonte { get; init; }

    public IEnumerable<string> Validate(Zona zona)
    {
        if (IdadeCobertura is < 0 or > 1 || RendaCobertura is < 0 or > 1)
        {
            yield return $"{zona.Id}: cobertura fora de 0 a 1.";
        }

        if (IdadeFonte is not (FonteZona or FonteMunicipio) || RendaFonte is not (FonteZona or FonteMunicipio))
        {
            yield return $"{zona.Id}: fonte do perfil desconhecida.";
        }

        if (IdadeFonte == FonteZona && Idade.Values.Sum() > zona.Populacao)
        {
            yield return $"{zona.Id}: soma das faixas etárias maior que a população.";
        }

        if (RendaResponsavel is < 0)
        {
            yield return $"{zona.Id}: renda negativa.";
        }
    }
}

/// <summary>Contornos das zonas de uma UF (data/geo/zonas/{UF}.json), no mesmo formato dos municípios.</summary>
public sealed class GeoZonasUf : IValidatable
{
    public required string Uf { get; init; }

    public required string Source { get; init; }

    public required string Method { get; init; }

    public required int Scale { get; init; }

    public required List<GeoZona> Zonas { get; init; }

    public IEnumerable<string> Validate()
    {
        if (Scale <= 0)
        {
            yield return $"{Uf}: escala inválida.";
        }

        if (Zonas.Select(z => z.Id).Distinct(StringComparer.Ordinal).Count() != Zonas.Count)
        {
            yield return $"{Uf}: id de zona repetido nos contornos.";
        }

        foreach (GeoZona z in Zonas.Where(z => z.Polygons.Count == 0 || z.Polygons.Any(p => p.Outer.Count < 6 || p.Triangles.Count < 3)))
        {
            yield return $"{z.Id}: contorno vazio ou sem triângulos.";
        }
    }
}

public sealed class GeoZona
{
    public required string Id { get; init; }

    public required List<GeoPolygon> Polygons { get; init; }
}

public static class ZonasPaths
{
    public const string Directory = "zonas";

    /// <summary>Teto de tamanho de data/geo/zonas/ no repositório (decisão do autor, GDD 17.20, item 2).</summary>
    public const long MaxGeoBytes = 30L * 1024 * 1024;

    /// <summary>Teto de tamanho de data/zonas/ (atributos e _fontes.json) no repositório (decisão do autor, D-037).</summary>
    public const long MaxZonasBytes = 20L * 1024 * 1024;

    public static string File(string dataDirectory, string uf) => Path.Combine(dataDirectory, Directory, $"{uf}.json");

    public static string GeoDirectory(string dataDirectory) => Path.Combine(dataDirectory, GeoPaths.Directory, Directory);

    public static string GeoFile(string dataDirectory, string uf) => Path.Combine(GeoDirectory(dataDirectory), $"{uf}.json");

    public static DataLoadResult<ZonasUf> Load(string dataDirectory, string uf) => JsonData.LoadFile<ZonasUf>(File(dataDirectory, uf));

    public static DataLoadResult<GeoZonasUf> LoadGeo(string dataDirectory, string uf) => JsonData.LoadFile<GeoZonasUf>(GeoFile(dataDirectory, uf));
}
