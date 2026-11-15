using Planalto.Core.Data;
using Planalto.Core.Elections;

namespace Planalto.Core.Municipios;

/// <summary>Conteúdo de data/municipios/{UF}.json.</summary>
public sealed class MunicipioUfFile : IValidatable
{
    public required string Uf { get; init; }

    public required List<Municipio> Municipios { get; init; }

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Uf) || Uf.Length != 2)
        {
            yield return $"sigla de UF inválida: '{Uf}'.";
        }

        if (Municipios is null || Municipios.Count == 0)
        {
            yield return "a lista 'municipios' está vazia.";
            yield break;
        }

        for (int i = 0; i < Municipios.Count; i++)
        {
            Municipio? m = Municipios[i];
            if (m is null)
            {
                yield return $"municipios[{i}]: item null.";
                continue;
            }

            foreach (string error in ValidateMunicipio(m))
            {
                yield return $"{m.CodigoIbge} {m.Nome}: {error}";
            }
        }

        foreach (var duplicate in Municipios.Where(m => m is not null).GroupBy(m => m.CodigoIbge).Where(g => g.Count() > 1))
        {
            yield return $"código IBGE duplicado: {duplicate.Key}.";
        }

        if (Municipios.Where(m => m is not null).Select(m => m.CodigoIbge / 100000).Distinct().Count() > 1)
        {
            yield return "municípios de UFs diferentes no mesmo arquivo (prefixo do código IBGE).";
        }
    }

    /// <summary>Confere se o porte gravado é o que data/portes.json calcula para a população.</summary>
    public IEnumerable<string> ValidatePortes(PorteCatalog portes)
    {
        foreach (Municipio m in Municipios.Where(m => m is not null))
        {
            string expected = portes.Classify(m.Populacao);
            if (m.Porte != expected)
            {
                yield return $"{m.CodigoIbge} {m.Nome}: porte '{m.Porte}', mas data/portes.json dá '{expected}'. Rode o importador de novo.";
            }
        }
    }

    private IEnumerable<string> ValidateMunicipio(Municipio m)
    {
        if (m.CodigoIbge is < 1_000_000 or > 9_999_999)
        {
            yield return "código IBGE deve ter 7 dígitos.";
        }

        if (m.Uf != Uf)
        {
            yield return $"UF '{m.Uf}' diferente da UF do arquivo '{Uf}'.";
        }

        foreach (string error in DataChecks.Text(m.Nome, "nome", "município")
            .Concat(DataChecks.Text(m.Regiao, "regiao", "município"))
            .Concat(DataChecks.Text(m.RegiaoImediata?.Nome, "regiao_imediata.nome", "município")))
        {
            yield return error;
        }

        // Caixa que contém todo o território brasileiro, só para pegar coordenadas trocadas ou zeradas.
        if (m.Lat is < -34 or > 6 || m.Lon is < -74 or > -28)
        {
            yield return $"coordenadas fora do Brasil ({m.Lat}, {m.Lon}).";
        }

        if (m.Populacao <= 0)
        {
            yield return "população deve ser maior que zero.";
        }

        if (m.Eleitorado < 0 || (m.EleicoesMunicipais && m.Eleitorado == 0))
        {
            yield return "eleitorado deve ser maior que zero em município com eleições municipais.";
        }

        if (m.PerfilFonte is not (Municipio.PerfilFonteMunicipio or Municipio.PerfilFonteUf))
        {
            yield return $"perfil_fonte inválido: '{m.PerfilFonte}'.";
        }

        foreach (string error in ValidatePerfil(m.Perfil))
        {
            yield return error;
        }

        if (m.Perfil is not null && m.UrbanaPct != PerfilCenso.UrbanaPct(m.Perfil.Situacao.GetValueOrDefault("urbana"), m.Perfil.PopulacaoCenso))
        {
            yield return "urbana_pct não corresponde ao perfil.";
        }

        foreach (string error in ValidateEleicoes(m))
        {
            yield return error;
        }
    }

    private static IEnumerable<string> ValidateEleicoes(Municipio m)
    {
        if (!m.EleicoesMunicipais)
        {
            if (m.VagasVereador != 0 || m.VagasVereadorMax != 0 || m.SegundoTurnoPrefeito || m.VagasFonte != Municipio.VagasFonteSemEleicao)
            {
                yield return "sem eleições municipais, vagas devem ser 0, sem 2º turno e vagas_fonte 'sem_eleicao_municipal'.";
            }

            yield break;
        }

        if (m.VagasVereadorMax != ElectionRules.VagasVereador(m.Populacao))
        {
            yield return "vagas_vereador_max não corresponde ao teto do art. 29, IV.";
        }

        if (m.VagasVereador <= 0)
        {
            yield return "vagas_vereador deve ser maior que zero.";
        }

        if (m.VagasFonte is not (Municipio.VagasFonteTse2024 or Municipio.VagasFonteTeto))
        {
            yield return $"vagas_fonte inválida: '{m.VagasFonte}'.";
        }
        else if (m.VagasFonte == Municipio.VagasFonteTeto && m.VagasVereador != m.VagasVereadorMax)
        {
            yield return "com vagas_fonte 'teto_constitucional', vagas_vereador deve ser igual ao teto.";
        }

        if (m.SegundoTurnoPrefeito != ElectionRules.TemSegundoTurno(m.Eleitorado))
        {
            yield return "segundo_turno_prefeito não corresponde ao eleitorado.";
        }
    }

    private static IEnumerable<string> ValidatePerfil(PerfilCenso? perfil)
    {
        if (perfil is null)
        {
            yield return "perfil ausente.";
            yield break;
        }

        if (perfil.PopulacaoCenso <= 0)
        {
            yield return "perfil.populacao_censo deve ser maior que zero.";
        }

        var blocks = new (string Name, Dictionary<string, long>? Values, IReadOnlyList<string> Keys, bool SumsToTotal)[]
        {
            ("situacao", perfil.Situacao, PerfilCategorias.Situacao, true),
            ("idade", perfil.Idade, PerfilCategorias.Idade, true),
            ("renda", perfil.Renda, PerfilCategorias.Renda, false),
            ("religiao", perfil.Religiao, PerfilCategorias.Religiao, false),
            ("escolaridade", perfil.Escolaridade, PerfilCategorias.Escolaridade, false),
        };

        foreach (var (name, values, keys, sumsToTotal) in blocks)
        {
            if (values is null || !values.Keys.ToHashSet().SetEquals(keys))
            {
                yield return $"perfil.{name} deve ter exatamente as chaves {string.Join(", ", keys)}.";
                continue;
            }

            if (values.Values.Any(v => v < 0))
            {
                yield return $"perfil.{name} tem contagem negativa.";
            }

            long sum = values.Values.Sum();
            if (sumsToTotal && sum != perfil.PopulacaoCenso)
            {
                yield return $"soma de perfil.{name} ({sum}) diferente de populacao_censo ({perfil.PopulacaoCenso}).";
            }
            else if (!sumsToTotal && (sum <= 0 || sum > perfil.PopulacaoCenso))
            {
                yield return $"soma de perfil.{name} ({sum}) deve estar entre 1 e populacao_censo.";
            }
        }
    }
}
