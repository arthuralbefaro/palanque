using Planalto.Core.Data;

namespace Planalto.Core.Municipios;

/// <summary>Os 27 arquivos de data/municipios/ e o _fontes.json, carregados e conferidos juntos.</summary>
public sealed class MunicipiosDataSet
{
    public const string DirectoryName = "municipios";
    public const string FontesFileName = "_fontes.json";
    public const int UfCount = 27;

    private MunicipiosDataSet(FontesMunicipios fontes, IReadOnlyList<MunicipioUfFile> files)
    {
        Fontes = fontes;
        Files = files;
    }

    public FontesMunicipios Fontes { get; }

    public IReadOnlyList<MunicipioUfFile> Files { get; }

    public IEnumerable<Municipio> All => Files.SelectMany(f => f.Municipios);

    /// <summary>Carrega e valida tudo; devolve null no conjunto quando algo não pôde ser lido.</summary>
    public static (MunicipiosDataSet? DataSet, IReadOnlyList<string> Errors) Load(string dataDirectory, PorteCatalog portes)
    {
        var errors = new List<string>();
        string directory = Path.Combine(dataDirectory, DirectoryName);
        if (!Directory.Exists(directory))
        {
            return (null, [$"{DirectoryName}/: pasta não encontrada. Rode o Planalto.DataImporter."]);
        }

        DataLoadResult<FontesMunicipios> fontes = JsonData.LoadFile<FontesMunicipios>(Path.Combine(directory, FontesFileName));
        errors.AddRange(fontes.Errors);

        var files = new List<MunicipioUfFile>();
        foreach (string path in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            if (Path.GetFileName(path) == FontesFileName)
            {
                continue;
            }

            DataLoadResult<MunicipioUfFile> file = JsonData.LoadFile<MunicipioUfFile>(path);
            errors.AddRange(file.Errors);
            if (file.Value is null)
            {
                continue;
            }

            if (Path.GetFileNameWithoutExtension(path) != file.Value.Uf)
            {
                errors.Add($"{Path.GetFileName(path)}: nome do arquivo diferente da UF '{file.Value.Uf}'.");
            }

            errors.AddRange(file.Value.ValidatePortes(portes).Select(e => $"{Path.GetFileName(path)}: {e}"));
            files.Add(file.Value);
        }

        if (fontes.Value is null || errors.Count > 0)
        {
            return (null, errors);
        }

        var dataSet = new MunicipiosDataSet(fontes.Value, files);
        errors.AddRange(dataSet.CrossCheck());
        return (dataSet, errors);
    }

    /// <summary>Municípios cujas vagas reais passam do teto calculado com a população atual.</summary>
    public IEnumerable<Municipio> VagasAcimaDoTeto() =>
        All.Where(m => m.EleicoesMunicipais && m.VagasVereador > m.VagasVereadorMax);

    private IEnumerable<string> CrossCheck()
    {
        string prefix = $"{DirectoryName}/";
        if (Files.Count != UfCount)
        {
            yield return $"{prefix} esperados {UfCount} arquivos de UF, encontrados {Files.Count}.";
        }

        List<Municipio> all = All.ToList();
        foreach (var duplicate in all.GroupBy(m => m.CodigoIbge).Where(g => g.Count() > 1))
        {
            yield return $"{prefix} código IBGE {duplicate.Key} aparece em mais de um arquivo.";
        }

        TotaisOficiais totais = Fontes.TotaisOficiais;
        if (all.Count != totais.TotalMunicipios)
        {
            yield return $"{prefix} {all.Count} municípios, mas a lista do IBGE tem {totais.TotalMunicipios}.";
        }

        var populacaoPorUf = all.GroupBy(m => m.Uf).ToDictionary(g => g.Key, g => g.Sum(m => m.Populacao));
        foreach (var (uf, oficial) in totais.PopulacaoPorUf)
        {
            long soma = populacaoPorUf.GetValueOrDefault(uf);
            if (soma != oficial)
            {
                yield return $"{prefix} população de {uf}: soma dos municípios {soma}, total oficial {oficial}.";
            }
        }

        long vagasTse = all.Where(m => m.VagasFonte == Municipio.VagasFonteTse2024).Sum(m => (long)m.VagasVereador);
        if (vagasTse != totais.VagasVereadorTse2024)
        {
            yield return $"{prefix} vagas de vereador do TSE: soma {vagasTse}, total do arquivo {totais.VagasVereadorTse2024}.";
        }

        var excecoes = VagasAcimaDoTeto().Select(m => m.CodigoIbge).ToHashSet();
        if (!excecoes.SetEquals(Fontes.ExcecoesVagas.Select(e => e.CodigoIbge)))
        {
            yield return $"{prefix} a lista excecoes_vagas de {FontesFileName} não corresponde aos municípios com vagas acima do teto.";
        }
    }
}
