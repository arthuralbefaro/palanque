using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Planalto.Core.Data;
using Planalto.Core.Demography;
using Planalto.Core.Municipios;

namespace Planalto.DataImporter.Rd;

/// <summary>
/// Tábuas de mortalidade e população projetada por UF (GDD 17.16 e 17.17), em data/demography/.
///
/// Estrutura das planilhas do IBGE, como lidas em novembro de 2026:
/// - Tábuas das Projeções (projecoes_2024_tab5_tabuas_mortalidade.xlsx): uma planilha, "5) TÁBUAS DE MORTALIDADE", com
///   títulos nas linhas 1 a 3, o cabeçalho na linha 6 e uma linha por idade, sexo, ano e local: A IDADE (0, 1, 5, 10,
///   …, 85, 90), B INTERVALO DE IDADE (1, 4, 5, …, e "+" no grupo de 90 ou mais), C SEXO (Mulheres, Homens, Ambos),
///   D ANO (2000 a 2070), E CÓD. (0 Brasil, 1 a 5 regiões, 11 a 53 UFs), F SIGLA, G LOCAL, H nMx, I nAx, J nqx
///   (proporção), K lx, L ndx, M nLx, N Sx, O Tx, P ex.
/// - Tábua completa do Brasil (homens.xlsx, mulheres.xlsx, ambos_os_sexos.xlsx): uma planilha ("TVC H 2024", "TVC M
///   2024", "TVC AS 2024"), em dois blocos (0 a 44 e 45 a 90 anos), cada um com três linhas de cabeçalho: A Idades
///   exatas (X), B Q(X, N) por mil, C D(X, N), D l(X), E L(X, N), F T(X), G E(X). A última linha é o grupo aberto, com
///   Q = 1000; a idade vem como "90" na planilha dos homens e "90 ou mais" nas outras duas.
/// - Indicadores das Projeções (projecoes_2024_tab4_indicadores.xlsx): planilhas "4) INDICADORES" e "NOTAS". Na
///   primeira, o cabeçalho com os códigos das colunas fica na linha 7 e há uma linha por ano (2000 a 2070) e local:
///   A ANO, B CÓD., C SIGLA, D LOCAL, E POP_T (população total em 1º de julho), F POP_H, G POP_M e os demais indicadores.
/// </summary>
public static class MortalityImport
{
    public const string ProjecoesUrl = "https://ftp.ibge.gov.br/Projecao_da_Populacao/Projecao_da_Populacao_2024/";
    public const string TabuasFile = "projecoes_2024_tab5_tabuas_mortalidade.xlsx";
    public const string IndicadoresFile = "projecoes_2024_tab4_indicadores.xlsx";
    public const string CompletaUrl = "https://ftp.ibge.gov.br/Tabuas_Completas_de_Mortalidade/Tabuas_Completas_de_Mortalidade_2024/xlsx/";

    /// <summary>Ano das tábuas, fixo (GDD 17.16).</summary>
    public const int Ano = 2024;

    /// <summary>Série da população por UF: do ano do Censo ao fim das Projeções.</summary>
    public const int PrimeiroAnoPopulacao = 2022;
    public const int UltimoAnoPopulacao = 2070;

    /// <summary>Idade inicial do grupo aberto das duas fontes.</summary>
    public const int IdadeGrupoAberto = 90;

    private const string CacheFolder = "rd/";

    /// <summary>Sexo do jogo, rótulo nas Projeções e nome do arquivo da tábua completa.</summary>
    private static readonly (string Id, string Projecoes, string Completa)[] Sexos =
    [
        ("masculino", "Homens", "homens"),
        ("feminino", "Mulheres", "mulheres"),
        ("ambos", "Ambos", "ambos_os_sexos"),
    ];

    /// <summary>
    /// Conferência de leitura do GDD 17.16: Q(X) por mil da tábua completa do Brasil de 2024, com três casas.
    /// Se a leitura não der estes valores, a importação para.
    /// </summary>
    private static readonly (string Sexo, int Idade, double PorMil)[] Conferencia =
    [
        ("masculino", 40, 3.265), ("feminino", 40, 1.535),
        ("masculino", 60, 12.532), ("feminino", 60, 6.888),
        ("masculino", 80, 62.284), ("feminino", 80, 46.531),
    ];

    /// <summary>Tábua completa lida: qx em proporção por idade simples e as colunas do grupo aberto.</summary>
    public sealed record TabuaCompleta(double[] Qx, GrupoAberto GrupoAberto);

    /// <summary>Tábua abreviada de um local e sexo: as faixas fechadas e o grupo aberto.</summary>
    public sealed record TabuaAbreviada(List<FaixaPublicada> Faixas, GrupoAberto GrupoAberto);

    public static async Task<int> RunAsync(SourceCache cache, string dataDirectory)
    {
        CachedFile tabuas = await cache.GetAsync(CacheFolder + TabuasFile, ProjecoesUrl + TabuasFile);
        CachedFile indicadores = await cache.GetAsync(CacheFolder + IndicadoresFile, ProjecoesUrl + IndicadoresFile);
        var completas = new Dictionary<string, (CachedFile File, TabuaCompleta Tabua)>(StringComparer.Ordinal);
        foreach (var (id, _, arquivo) in Sexos)
        {
            CachedFile file = await cache.GetAsync($"{CacheFolder}tabua_completa_{Ano}_{arquivo}.xlsx", $"{CompletaUrl}{arquivo}.xlsx");
            completas[id] = (file, ReadCompleta(Xlsx.ReadRows(file.Path, Xlsx.SheetNames(file.Path)[0]), arquivo));
        }

        var conferencia = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (sexo, idade, porMil) in Conferencia)
        {
            double lido = Math.Round(completas[sexo].Tabua.Qx[idade] * 1000, 3);
            if (lido != porMil)
            {
                throw new ImportException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Tábua completa do Brasil ({sexo}): Q({idade}) lido como {lido} por mil; o esperado é {porMil}. A leitura da planilha está errada."));
            }

            conferencia[$"{sexo}_{idade}_anos_por_mil"] = lido;
        }

        Console.WriteLine("  lendo as tábuas das Projeções (planilha grande)");
        Dictionary<(string Local, string Sexo), TabuaAbreviada> abreviadas =
            ReadAbreviadas(Xlsx.ReadRows(tabuas.Path, Xlsx.SheetNames(tabuas.Path)[0]), Ano);

        var locais = abreviadas.Keys.Select(k => k.Local).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (locais.Count != 28 || !locais.Contains(MortalityTable.Brasil))
        {
            throw new ImportException($"Tábuas das Projeções: esperados o Brasil e 27 UFs em {Ano}; lidos {locais.Count} locais.");
        }

        var tabelas = new Dictionary<string, TabuaLocal>(StringComparer.Ordinal);
        foreach (string local in locais.OrderBy(l => l == MortalityTable.Brasil ? 0 : 1).ThenBy(l => l, StringComparer.Ordinal))
        {
            TabuaSexo Build(string sexo)
            {
                if (!abreviadas.TryGetValue((local, sexo), out TabuaAbreviada? abreviada))
                {
                    throw new ImportException($"Tábuas das Projeções: falta {local}/{sexo} em {Ano}.");
                }

                TabuaCompleta brasil = completas[sexo].Tabua;
                return local == MortalityTable.Brasil
                    ? new TabuaSexo { Qx = [.. brasil.Qx], GrupoAberto = brasil.GrupoAberto, Faixas = abreviada.Faixas }
                    : new TabuaSexo { Qx = [.. Repartir(abreviada.Faixas, brasil.Qx)], GrupoAberto = abreviada.GrupoAberto, Faixas = abreviada.Faixas };
            }

            tabelas[local] = new TabuaLocal { Masculino = Build("masculino"), Feminino = Build("feminino"), Ambos = Build("ambos") };
        }

        var mortality = new MortalityTable
        {
            Note = "Gerado pelo DataImporter; não editar à mão. qx é a probabilidade de morte entre as idades exatas X e X+1, em proporção, "
                + "das idades de 0 a 89 anos; grupo_aberto e faixas são o que o IBGE publica (nqx em proporção). Fonte e método em _fontes.json.",
            Ano = Ano,
            Tabuas = tabelas,
        };

        ProjecaoUf projecao = ReadPopulacao(Xlsx.ReadRows(indicadores.Path, Xlsx.SheetNames(indicadores.Path)[0]), PrimeiroAnoPopulacao, UltimoAnoPopulacao);

        // Maior diferença relativa, por faixa, entre a tábua completa do Brasil e a abreviada das Projeções: fica registrada.
        double diferencaBrasil = tabelas[MortalityTable.Brasil].PorSexo()
            .SelectMany(s => s.Tabua.Faixas.Select(f => Math.Abs(s.Tabua.Recomposta(f) - f.Nqx) / f.Nqx))
            .Max();

        var fontes = new FontesDemografia
        {
            GeradoEm = DateTimeOffset.UtcNow,
            Fontes =
            [
                new FonteCampo
                {
                    Campo = "mortality.json: tabuas das UFs (faixas, grupo_aberto e, repartido, qx); faixas do Brasil",
                    Fonte = "IBGE, Projeções da População do Brasil e das Unidades da Federação, revisão 2024: tábuas de mortalidade por sexo e idade quinquenal",
                    Ano = Ano,
                    Url = ProjecoesUrl + TabuasFile,
                    BaixadoEm = tabuas.DownloadedAt,
                    Observacao = "Planilha \"5) TÁBUAS DE MORTALIDADE\": colunas IDADE, INTERVALO DE IDADE, SEXO, ANO, CÓD., SIGLA, LOCAL, nMx, nAx, nqx, lx, ndx, nLx, Sx, Tx, ex. "
                        + $"Usadas as linhas de {Ano} do Brasil (código 0) e das 27 UFs (códigos 11 a 53): nqx das faixas de 0 a 85 anos; nMx (taxa_central) e ex (expectativa_de_vida) do grupo de 90 anos ou mais.",
                },
                .. Sexos.Select(s => new FonteCampo
                {
                    Campo = $"mortality.json: tabuas.BR.{s.Id} (qx e grupo_aberto); perfil por idade simples das UFs ({s.Id})",
                    Fonte = $"IBGE, Tábuas Completas de Mortalidade para o Brasil, {Ano}: {s.Completa.Replace('_', ' ')}",
                    Ano = Ano,
                    Url = $"{CompletaUrl}{s.Completa}.xlsx",
                    BaixadoEm = completas[s.Id].File.DownloadedAt,
                    Observacao = "Coluna Q(X, N) por mil, N = 1, idades de 0 a 89 anos, gravada em proporção. Do grupo de 90 anos ou mais: E(X) (expectativa_de_vida) e D(X, N) ÷ L(X, N) (taxa_central).",
                }),
                new FonteCampo
                {
                    Campo = "projecao_uf.json: populacao",
                    Fonte = "IBGE, Projeções da População do Brasil e das Unidades da Federação, revisão 2024: indicadores implícitos",
                    Ano = Ano,
                    Url = ProjecoesUrl + IndicadoresFile,
                    BaixadoEm = indicadores.DownloadedAt,
                    Observacao = $"Planilha \"4) INDICADORES\", coluna POP_T (população total em 1º de julho), anos de {PrimeiroAnoPopulacao} a {UltimoAnoPopulacao}, Brasil e 27 UFs.",
                },
            ],
            Metodo = "A tábua do Brasil é a tábua completa publicada (idades simples de 0 a 89 anos). Para cada UF e sexo, a probabilidade de cada faixa etária publicada "
                + "(nqx das faixas 0, 1 a 4, 5 a 9, …, 85 a 89) é repartida pelas idades simples da faixa seguindo o perfil da tábua completa do Brasil do mesmo sexo: "
                + "qx_UF = 1 − (1 − qx_BR)^k, com k = ln(1 − nqx_UF) ÷ ln(1 − nqx_BR), em que nqx_BR é a probabilidade da mesma faixa recomposta pela tábua completa "
                + "(1 − Π(1 − qx_BR)). Assim a faixa recomposta da UF, 1 − Π(1 − qx_UF), é igual à publicada. O grupo aberto de 90 anos ou mais não tem idade simples: "
                + "ficam gravadas a taxa central e a expectativa de vida publicadas, e a curva além dos 89 anos é parâmetro de jogo (config.json).",
            Conferencia = conferencia,
            Observacoes =
            [
                "Ano das tábuas fixo em 2024 (GDD 17.16). A tábua do Brasil vale para a UF que faltar.",
                string.Create(
                    CultureInfo.GetCultureInfo("pt-BR"),
                    $"A tábua completa do Brasil e a tábua abreviada do Brasil nas Projeções são publicações distintas: a recomposição das faixas pela tábua completa difere da abreviada em até {diferencaBrasil * 100:F2}% (relativo). Por isso a igualdade exata entre faixa recomposta e faixa publicada vale para as 27 UFs; para o Brasil, as faixas da abreviada ficam gravadas só para conferência."),
                "config.json não tem fonte: são parâmetros de jogo (fator pessoal, curva depois dos 89 anos, sorteio mensal), decisão 22 do GDD 17.20.",
            ],
        };

        Check(mortality.Validate());
        Check(projecao.Validate());
        Check(fontes.Validate());
        if (diferencaBrasil > MortalityTable.ToleranciaBrasil)
        {
            throw new ImportException($"A tábua completa do Brasil difere da abreviada das Projeções em {diferencaBrasil * 100:F2}%, acima do limite de {MortalityTable.ToleranciaBrasil * 100:F0}%.");
        }

        var options = new JsonSerializerOptions(JsonData.Options) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        Directory.CreateDirectory(Path.Combine(dataDirectory, "demography"));
        foreach (var (file, value) in new (string, object)[] { (MortalityTable.File, mortality), (ProjecaoUf.File, projecao), (FontesDemografia.File, fontes) })
        {
            string path = Path.Combine(dataDirectory, file);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, value.GetType(), options).ReplaceLineEndings("\n") + "\n");
            Console.WriteLine($"  {file}: {new FileInfo(path).Length / 1024.0:F0} KB");
        }

        Console.WriteLine($"{tabelas.Count} tábuas (Brasil e {tabelas.Count - 1} UFs) e população de {PrimeiroAnoPopulacao} a {UltimoAnoPopulacao} gravadas em {Path.Combine(dataDirectory, "demography")}");
        return 0;
    }

    /// <summary>
    /// Reparte cada faixa publicada pelas idades simples, com o perfil da tábua completa: qx = 1 − (1 − qx_perfil)^k,
    /// com k escolhido para a faixa recomposta dar exatamente o nqx publicado.
    /// </summary>
    public static double[] Repartir(IReadOnlyList<FaixaPublicada> faixas, IReadOnlyList<double> perfil)
    {
        var qx = new double[perfil.Count];
        foreach (FaixaPublicada faixa in faixas)
        {
            if (faixa.Amplitude == 1)
            {
                qx[faixa.Idade] = faixa.Nqx;
                continue;
            }

            // Soma dos logaritmos da sobrevivência do perfil na faixa: ln(1 − nqx_BR recomposto).
            double logPerfil = 0;
            for (int age = faixa.Idade; age < faixa.Idade + faixa.Amplitude; age++)
            {
                logPerfil += Math.Log(1 - perfil[age]);
            }

            double k = Math.Log(1 - faixa.Nqx) / logPerfil;
            for (int age = faixa.Idade; age < faixa.Idade + faixa.Amplitude; age++)
            {
                qx[age] = 1 - Math.Pow(1 - perfil[age], k);
            }
        }

        return qx;
    }

    /// <summary>Lê a tábua completa: idades simples de 0 a 89 com Q por mil e a linha do grupo aberto.</summary>
    public static TabuaCompleta ReadCompleta(IEnumerable<string?[]> rows, string nome)
    {
        var qx = new double?[IdadeGrupoAberto];
        GrupoAberto? aberto = null;
        bool header = false;
        foreach (string?[] row in rows)
        {
            if (row.Length > 1 && row[1] is { } title && title.Contains("Q (X, N)", StringComparison.Ordinal) && title.Contains("Por Mil", StringComparison.OrdinalIgnoreCase))
            {
                header = true;
                continue;
            }

            // A idade é o primeiro termo da coluna A ("90 ou mais" no grupo aberto de duas das planilhas).
            if (!header || row.Length < 7 || row[0] is null || row[1] is null
                || !int.TryParse(row[0]!.Trim().Split(' ')[0], NumberStyles.None, CultureInfo.InvariantCulture, out int idade))
            {
                continue;
            }

            if (idade < IdadeGrupoAberto)
            {
                if (qx[idade] is not null)
                {
                    throw new ImportException($"Tábua completa ({nome}): idade {idade} repetida.");
                }

                qx[idade] = Number(row[1], $"tábua completa ({nome}), Q({idade})") / 1000;
            }
            else if (idade == IdadeGrupoAberto && aberto is null)
            {
                string where = $"tábua completa ({nome}), grupo de {IdadeGrupoAberto} anos ou mais";
                if (Number(row[1], where) != 1000)
                {
                    throw new ImportException($"{where}: Q deveria ser 1000 por mil.");
                }

                aberto = new GrupoAberto
                {
                    Idade = IdadeGrupoAberto,
                    TaxaCentral = Number(row[2], where) / Number(row[4], where),
                    ExpectativaDeVida = Number(row[6], where),
                };
            }
            else
            {
                throw new ImportException($"Tábua completa ({nome}): linha inesperada para a idade {idade}.");
            }
        }

        int missing = Array.FindIndex(qx, q => q is null);
        if (!header || missing >= 0 || aberto is null)
        {
            throw new ImportException($"Tábua completa ({nome}): {(header ? $"falta a idade {(missing >= 0 ? missing : IdadeGrupoAberto)}" : "cabeçalho 'Q (X, N) (Por Mil)' não encontrado")}.");
        }

        return new TabuaCompleta(qx.Select(q => q!.Value).ToArray(), aberto);
    }

    /// <summary>Lê as tábuas abreviadas do ano, do Brasil e das UFs (regiões ficam de fora), por sigla e sexo do jogo.</summary>
    public static Dictionary<(string Local, string Sexo), TabuaAbreviada> ReadAbreviadas(IEnumerable<string?[]> rows, int ano)
    {
        string year = ano.ToString(CultureInfo.InvariantCulture);
        Dictionary<string, int>? columns = null;
        var faixas = new Dictionary<(string, string), List<FaixaPublicada>>();
        var abertos = new Dictionary<(string, string), GrupoAberto>();
        foreach (string?[] row in rows)
        {
            if (columns is null)
            {
                columns = Header(row, "IDADE", "INTERVALO DE IDADE", "SEXO", "ANO", "CÓD.", "SIGLA", "nMx", "nqx", "ex");
                continue;
            }

            Dictionary<string, int> at = columns;
            string? Cell(string name) => at[name] < row.Length ? row[at[name]] : null;
            if (Cell("ANO") != year || Cell("SIGLA") is not { } sigla || Cell("SEXO") is not { } sexoFonte)
            {
                continue;
            }

            // Código 0 é o Brasil; 1 a 5, as regiões; dois dígitos, as UFs.
            int codigo = (int)Number(Cell("CÓD."), "tábuas das Projeções, CÓD.");
            if (codigo is >= 1 and <= 5)
            {
                continue;
            }

            string sexo = Sexos.FirstOrDefault(s => s.Projecoes == sexoFonte).Id
                ?? throw new ImportException($"Tábuas das Projeções: sexo '{sexoFonte}' desconhecido.");
            string where = $"tábuas das Projeções, {sigla}/{sexo}";
            int idade = (int)Number(Cell("IDADE"), where);
            var key = (sigla, sexo);
            if (Cell("INTERVALO DE IDADE") == "+")
            {
                abertos.Add(key, new GrupoAberto { Idade = idade, TaxaCentral = Number(Cell("nMx"), where), ExpectativaDeVida = Number(Cell("ex"), where) });
                continue;
            }

            if (!faixas.TryGetValue(key, out List<FaixaPublicada>? list))
            {
                faixas[key] = list = [];
            }

            list.Add(new FaixaPublicada { Idade = idade, Amplitude = (int)Number(Cell("INTERVALO DE IDADE"), where), Nqx = Number(Cell("nqx"), where) });
        }

        if (columns is null)
        {
            throw new ImportException("Tábuas das Projeções: cabeçalho não encontrado.");
        }

        var result = new Dictionary<(string, string), TabuaAbreviada>();
        foreach (var (key, list) in faixas)
        {
            list.Sort((a, b) => a.Idade.CompareTo(b.Idade));
            if (!abertos.TryGetValue(key, out GrupoAberto? aberto) || aberto.Idade != IdadeGrupoAberto || list.Sum(f => f.Amplitude) != IdadeGrupoAberto)
            {
                throw new ImportException($"Tábuas das Projeções: {key.Item1}/{key.Item2} sem as faixas de 0 a {IdadeGrupoAberto - 1} anos e o grupo de {IdadeGrupoAberto} ou mais.");
            }

            result[key] = new TabuaAbreviada(list, aberto);
        }

        return result;
    }

    /// <summary>Lê a população total (POP_T) do Brasil e das UFs nos anos pedidos.</summary>
    public static ProjecaoUf ReadPopulacao(IEnumerable<string?[]> rows, int primeiroAno, int ultimoAno)
    {
        Dictionary<string, int>? columns = null;
        int years = ultimoAno - primeiroAno + 1;
        var series = new SortedDictionary<string, long?[]>(StringComparer.Ordinal);
        foreach (string?[] row in rows)
        {
            if (columns is null)
            {
                columns = Header(row, "ANO", "CÓD.", "SIGLA", "POP_T");
                continue;
            }

            Dictionary<string, int> at = columns;
            string? Cell(string name) => at[name] < row.Length ? row[at[name]] : null;
            if (Cell("SIGLA") is not { } sigla || Cell("ANO") is not { } anoText
                || !int.TryParse(anoText, NumberStyles.None, CultureInfo.InvariantCulture, out int ano) || ano < primeiroAno || ano > ultimoAno)
            {
                continue;
            }

            if ((int)Number(Cell("CÓD."), "indicadores das Projeções, CÓD.") is >= 1 and <= 5)
            {
                continue;
            }

            double pop = Number(Cell("POP_T"), $"indicadores das Projeções, POP_T de {sigla} em {ano}");
            if (pop != Math.Floor(pop) || pop <= 0)
            {
                throw new ImportException($"Indicadores das Projeções: POP_T de {sigla} em {ano} não é um inteiro positivo ({pop}).");
            }

            if (!series.TryGetValue(sigla, out long?[]? serie))
            {
                series[sigla] = serie = new long?[years];
            }

            if (serie[ano - primeiroAno] is not null)
            {
                throw new ImportException($"Indicadores das Projeções: {sigla} em {ano} repetido.");
            }

            serie[ano - primeiroAno] = (long)pop;
        }

        if (columns is null)
        {
            throw new ImportException("Indicadores das Projeções: cabeçalho não encontrado.");
        }

        if (series.Count != 28 || series.Values.Any(s => s.Any(p => p is null)))
        {
            throw new ImportException($"Indicadores das Projeções: esperados o Brasil e 27 UFs com todos os anos de {primeiroAno} a {ultimoAno}; lidos {series.Count} locais.");
        }

        return new ProjecaoUf
        {
            Note = "Gerado pelo DataImporter; não editar à mão. População total projetada em 1º de julho de cada ano, de ano_inicial a ano_final, "
                + "do Brasil (BR) e de cada UF (IBGE, Projeções da População, revisão 2024). Fonte em _fontes.json.",
            AnoInicial = primeiroAno,
            AnoFinal = ultimoAno,
            Populacao = series
                .OrderBy(s => s.Key == ProjecaoUf.Brasil ? 0 : 1).ThenBy(s => s.Key, StringComparer.Ordinal)
                .ToDictionary(s => s.Key, s => s.Value.Select(p => p!.Value).ToList(), StringComparer.Ordinal),
        };
    }

    /// <summary>Posição de cada coluna pedida se a linha for o cabeçalho (tem todas); senão, null.</summary>
    private static Dictionary<string, int>? Header(string?[] row, params string[] names)
    {
        var columns = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string name in names)
        {
            int index = Array.FindIndex(row, cell => cell?.Trim() == name);
            if (index < 0)
            {
                return null;
            }

            columns[name] = index;
        }

        return columns;
    }

    private static double Number(string? text, string where) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : throw new ImportException($"Valor '{text}' sem número em {where}.");

    private static void Check(IEnumerable<string> errors)
    {
        string error = string.Join("; ", errors);
        if (error.Length > 0)
        {
            throw new ImportException(error);
        }
    }
}
