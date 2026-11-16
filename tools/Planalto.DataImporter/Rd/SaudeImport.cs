using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace Planalto.DataImporter.Rd;

/// <summary>Totais de uma UF num ano, para conferência com os números publicados.</summary>
public sealed record SaudeTotaisUf(long Obitos, long ObitosMenoresDeUmAno, long ObitosPorAgressao, long NascidosVivos);

/// <summary>
/// Leitos existentes (CNES, campo QT_EXIST) de um município ou UF. <see cref="Existentes"/> soma todos os
/// tipos de leito, SUS e não SUS; <see cref="Complementares"/> é a parte de <see cref="Existentes"/> com
/// TP_LEITO = 3 (UTI e unidades intermediárias), que as tabelas de "leitos de internação" do Ministério
/// da Saúde contam à parte.
/// </summary>
public sealed record SaudeLeitos(long Existentes, long Sus, long NaoSus, long Complementares)
{
    public long Internacao => Existentes - Complementares;

    public SaudeLeitos Mais(SaudeLeitos outro) => new(
        Existentes + outro.Existentes, Sus + outro.Sus, NaoSus + outro.NaoSus, Complementares + outro.Complementares);
}

/// <summary>
/// Agregados de saúde por município de residência (código IBGE de 6 dígitos, como vem do DATASUS) e ano.
/// Códigos terminados em 0000 são "município ignorado" da UF e ficam como vieram. Município sem registro
/// num ano não aparece no dicionário daquele ano (vale zero).
/// </summary>
public sealed record SaudeAgregados(
    int Versao,
    IReadOnlyList<int> Anos,
    string CompetenciaLeitos,
    Dictionary<int, Dictionary<string, int>> ObitosMenoresDeUmAno,
    Dictionary<int, Dictionary<string, int>> ObitosPorAgressao,
    Dictionary<int, Dictionary<string, int>> NascidosVivos,
    Dictionary<string, SaudeLeitos> Leitos,
    Dictionary<int, SortedDictionary<string, SaudeTotaisUf>> TotaisPorUf,
    SortedDictionary<string, SaudeLeitos> LeitosPorUf,
    IReadOnlyList<CachedFile> Arquivos)
{
    public SaudeTotaisUf TotalNacional(int ano) => TotaisPorUf[ano].Values.Aggregate(
        new SaudeTotaisUf(0, 0, 0, 0),
        (a, b) => new SaudeTotaisUf(
            a.Obitos + b.Obitos,
            a.ObitosMenoresDeUmAno + b.ObitosMenoresDeUmAno,
            a.ObitosPorAgressao + b.ObitosPorAgressao,
            a.NascidosVivos + b.NascidosVivos));

    public SaudeLeitos TotalNacionalDeLeitos() => LeitosPorUf.Values.Aggregate(new SaudeLeitos(0, 0, 0, 0), (a, b) => a.Mais(b));
}

/// <summary>
/// Saúde e segurança da RD (GDD 17.17), dos microdados do DATASUS (Ministério da Saúde), por município de
/// residência:
/// <list type="bullet">
/// <item>SIM, declarações de óbito (DORES): óbitos de menores de 1 ano e óbitos por agressão;</item>
/// <item>SINASC, declarações de nascido vivo (DNRES): nascidos vivos;</item>
/// <item>CNES, leitos (LT): leitos existentes numa competência.</item>
/// </list>
/// Só agrega; a taxa, a soma de 3 anos e a regra dos municípios pequenos ficam para quem monta os arquivos.
/// </summary>
public static class SaudeImport
{
    /// <summary>Muda quando a regra de agregação muda, para descartar o agregado intermediário antigo.</summary>
    public const int Versao = 1;

    public const string SimUrl = "ftp://ftp.datasus.gov.br/dissemin/publicos/SIM/CID10/DORES/DO{0}{1}.dbc";

    public const string SinascUrl = "ftp://ftp.datasus.gov.br/dissemin/publicos/SINASC/1996_/Dados/DNRES/DN{0}{1}.dbc";

    public const string CnesLeitosUrl = "ftp://ftp.datasus.gov.br/dissemin/publicos/CNES/200508_/Dados/LT/LT{0}{1}.dbc";

    public const string AgregadoFile = "rd/saude_agregado.json";

    /// <summary>Anos definitivos do SIM e do SINASC usados na soma de 3 anos (GDD 17.17).</summary>
    public static readonly IReadOnlyList<int> Anos = [2022, 2023, 2024];

    /// <summary>Competência do CNES, AAAAMM.</summary>
    public const string CompetenciaLeitos = "202609";

    public static readonly IReadOnlyList<string> Ufs =
    [
        "AC", "AL", "AM", "AP", "BA", "CE", "DF", "ES", "GO", "MA", "MG", "MS", "MT", "PA",
        "PB", "PE", "PI", "PR", "RJ", "RN", "RO", "RR", "RS", "SC", "SE", "SP", "TO",
    ];

    /// <summary>Nomes dos arquivos brutos no cache (relativos à pasta do cache), na ordem em que são lidos.</summary>
    public static IReadOnlyList<string> FileNames => Fontes().Select(f => f.FileName).ToList();

    private const string TipoObitoFetal = "1";
    private const string TipoLeitoComplementar = "3";

    // Um download de outro processo deixa um .part ao lado; mais velho que isso, é resto de download interrompido.
    private static readonly TimeSpan DownloadEmAndamento = TimeSpan.FromMinutes(10);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private enum Tipo
    {
        Sim,
        Sinasc,
        Leitos,
    }

    private sealed record Fonte(Tipo Tipo, string Uf, int Ano, string FileName, string Url);

    private sealed class Parcial
    {
        public Dictionary<string, int> A { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, int> B { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, SaudeLeitos> Leitos { get; } = new(StringComparer.Ordinal);

        public long Registros { get; set; }
    }

    public static async Task<SaudeAgregados> ReadAsync(SourceCache cache)
    {
        List<Fonte> fontes = Fontes();
        var arquivos = new List<CachedFile>(fontes.Count);
        foreach (Fonte fonte in fontes)
        {
            arquivos.Add(await GetAsync(cache, fonte));
        }

        string agregadoPath = Path.Combine(cache.Directory, AgregadoFile);
        if (await TryLoadAsync(agregadoPath, arquivos) is { } pronto)
        {
            Console.WriteLine($"  saúde: agregado reaproveitado de {AgregadoFile}");
            return pronto with { Arquivos = arquivos };
        }

        Console.WriteLine($"  saúde: lendo {fontes.Count} arquivos .dbc do DATASUS");
        var parciais = new ConcurrentDictionary<int, Parcial>();
        await Task.Run(() => Parallel.For(0, fontes.Count, i => parciais[i] = Ler(fontes[i], arquivos[i].Path)));

        SaudeAgregados agregados = Montar(fontes, parciais, arquivos);
        await File.WriteAllTextAsync(agregadoPath, JsonSerializer.Serialize(agregados, JsonOptions));
        return agregados;
    }

    /// <summary>
    /// Menor de 1 ano pelo campo IDADE do SIM (3 dígitos: unidade + quantidade). Unidade 0 = minutos,
    /// 1 = horas, 2 = dias, 3 = meses, 4 = anos, 5 = mais de 100 anos; "999" e vazio são idade ignorada.
    /// Nos arquivos de 2022 a 2024 a unidade 0 só aparece com quantidade de 01 a 59 (minutos); "000", que a
    /// documentação dá como idade ignorada, não aparece e fica de fora. "400" (zero ano completo, raríssimo)
    /// conta como menor de 1 ano, como nas tabelas do Ministério da Saúde.
    /// </summary>
    public static bool MenorDeUmAno(string idade) =>
        idade.Length == 3 && (idade == "400" || (idade[0] is >= '0' and <= '3' && idade != "000"));

    /// <summary>Agressão (homicídio) pela causa básica: CID-10 de X85 a Y09, com ou sem o 4º caractere.</summary>
    public static bool Agressao(string causaBasica)
    {
        if (causaBasica.Length < 3 || !char.IsAsciiDigit(causaBasica[1]) || !char.IsAsciiDigit(causaBasica[2]))
        {
            return false;
        }

        int numero = (causaBasica[1] - '0') * 10 + (causaBasica[2] - '0');
        return causaBasica[0] switch
        {
            'X' or 'x' => numero >= 85,
            'Y' or 'y' => numero <= 9,
            _ => false,
        };
    }

    /// <summary>Código do município como vem do DATASUS (6 ou 7 dígitos), reduzido aos 6 primeiros.</summary>
    public static string Municipio6(string codigo, string arquivo)
    {
        if (codigo.Length is not (6 or 7) || !codigo.All(char.IsAsciiDigit))
        {
            throw new ImportException($"{arquivo}: código de município '{codigo}' fora do formato de 6 ou 7 dígitos.");
        }

        return codigo.Length == 6 ? codigo : codigo[..6];
    }

    private static List<Fonte> Fontes()
    {
        var fontes = new List<Fonte>();
        string competencia = CompetenciaLeitos[2..];
        foreach (string uf in Ufs)
        {
            foreach (int ano in Anos)
            {
                fontes.Add(new Fonte(Tipo.Sim, uf, ano, $"rd/DO{uf}{ano}.dbc", string.Format(CultureInfo.InvariantCulture, SimUrl, uf, ano)));
                fontes.Add(new Fonte(Tipo.Sinasc, uf, ano, $"rd/DN{uf}{ano}.dbc", string.Format(CultureInfo.InvariantCulture, SinascUrl, uf, ano)));
            }

            fontes.Add(new Fonte(Tipo.Leitos, uf, 0, $"rd/LT{uf}{competencia}.dbc", string.Format(CultureInfo.InvariantCulture, CnesLeitosUrl, uf, competencia)));
        }

        return fontes;
    }

    private static async Task<CachedFile> GetAsync(SourceCache cache, Fonte fonte)
    {
        string path = Path.Combine(cache.Directory, fonte.FileName);
        string partial = path + ".part";
        if (!File.Exists(path) && File.Exists(partial) && DateTime.UtcNow - File.GetLastWriteTimeUtc(partial) < DownloadEmAndamento)
        {
            throw new ImportException($"{fonte.FileName}: há um download em andamento por outro processo ({partial}). Espere terminar e rode de novo.");
        }

        return await cache.GetAsync(fonte.FileName, fonte.Url);
    }

    private static Parcial Ler(Fonte fonte, string path)
    {
        string nome = Path.GetFileName(path);
        var parcial = new Parcial();
        switch (fonte.Tipo)
        {
            case Tipo.Sim:
                foreach (string[] r in Dbc.ReadColumns(path, "TIPOBITO", "CODMUNRES", "IDADE", "CAUSABAS"))
                {
                    // Os arquivos DORES trazem só óbitos não fetais; o filtro fica por garantia.
                    if (r[0] == TipoObitoFetal)
                    {
                        continue;
                    }

                    parcial.Registros++;
                    string municipio = Municipio6(r[1], nome);
                    if (MenorDeUmAno(r[2]))
                    {
                        Somar(parcial.A, municipio);
                    }

                    if (Agressao(r[3]))
                    {
                        Somar(parcial.B, municipio);
                    }
                }

                break;

            case Tipo.Sinasc:
                foreach (string[] r in Dbc.ReadColumns(path, "CODMUNRES"))
                {
                    parcial.Registros++;
                    Somar(parcial.A, Municipio6(r[0], nome));
                }

                break;

            case Tipo.Leitos:
                foreach (string[] r in Dbc.ReadColumns(path, "CODUFMUN", "TP_LEITO", "QT_EXIST", "QT_SUS", "QT_NSUS", "COMPETEN"))
                {
                    if (r[5] != CompetenciaLeitos)
                    {
                        throw new ImportException($"{nome}: competência '{r[5]}', esperada {CompetenciaLeitos}.");
                    }

                    parcial.Registros++;
                    string municipio = Municipio6(r[0], nome);
                    long existentes = Numero(r[2], nome, "QT_EXIST");
                    var leitos = new SaudeLeitos(
                        existentes,
                        Numero(r[3], nome, "QT_SUS"),
                        Numero(r[4], nome, "QT_NSUS"),
                        r[1].TrimStart('0') == TipoLeitoComplementar ? existentes : 0);
                    parcial.Leitos[municipio] = parcial.Leitos.TryGetValue(municipio, out SaudeLeitos? atual) ? atual.Mais(leitos) : leitos;
                }

                break;
        }

        return parcial;
    }

    private static SaudeAgregados Montar(List<Fonte> fontes, IReadOnlyDictionary<int, Parcial> parciais, List<CachedFile> arquivos)
    {
        var menores = Anos.ToDictionary(a => a, _ => new Dictionary<string, int>(StringComparer.Ordinal));
        var agressoes = Anos.ToDictionary(a => a, _ => new Dictionary<string, int>(StringComparer.Ordinal));
        var nascidos = Anos.ToDictionary(a => a, _ => new Dictionary<string, int>(StringComparer.Ordinal));
        var leitos = new Dictionary<string, SaudeLeitos>(StringComparer.Ordinal);
        var totais = Anos.ToDictionary(a => a, _ => new SortedDictionary<string, SaudeTotaisUf>(StringComparer.Ordinal));
        var leitosPorUf = new SortedDictionary<string, SaudeLeitos>(StringComparer.Ordinal);

        for (int i = 0; i < fontes.Count; i++)
        {
            Fonte fonte = fontes[i];
            Parcial parcial = parciais[i];
            switch (fonte.Tipo)
            {
                case Tipo.Sim:
                    Juntar(menores[fonte.Ano], parcial.A, fonte);
                    Juntar(agressoes[fonte.Ano], parcial.B, fonte);
                    SaudeTotaisUf antesSim = totais[fonte.Ano].GetValueOrDefault(fonte.Uf) ?? new SaudeTotaisUf(0, 0, 0, 0);
                    totais[fonte.Ano][fonte.Uf] = antesSim with
                    {
                        Obitos = parcial.Registros,
                        ObitosMenoresDeUmAno = parcial.A.Values.Sum(v => (long)v),
                        ObitosPorAgressao = parcial.B.Values.Sum(v => (long)v),
                    };
                    break;

                case Tipo.Sinasc:
                    Juntar(nascidos[fonte.Ano], parcial.A, fonte);
                    SaudeTotaisUf antesSinasc = totais[fonte.Ano].GetValueOrDefault(fonte.Uf) ?? new SaudeTotaisUf(0, 0, 0, 0);
                    totais[fonte.Ano][fonte.Uf] = antesSinasc with { NascidosVivos = parcial.Registros };
                    break;

                case Tipo.Leitos:
                    var daUf = new SaudeLeitos(0, 0, 0, 0);
                    foreach ((string municipio, SaudeLeitos valor) in parcial.Leitos)
                    {
                        if (!leitos.TryAdd(municipio, valor))
                        {
                            throw new ImportException($"{fonte.FileName}: município {municipio} já veio no arquivo de outra UF.");
                        }

                        daUf = daUf.Mais(valor);
                    }

                    leitosPorUf[fonte.Uf] = daUf;
                    break;
            }
        }

        return new SaudeAgregados(Versao, Anos, CompetenciaLeitos, menores, agressoes, nascidos, leitos, totais, leitosPorUf, arquivos);
    }

    private static void Juntar(Dictionary<string, int> destino, Dictionary<string, int> origem, Fonte fonte)
    {
        foreach ((string municipio, int valor) in origem)
        {
            if (!destino.TryAdd(municipio, valor))
            {
                throw new ImportException($"{fonte.FileName}: município {municipio} já veio no arquivo de outra UF.");
            }
        }
    }

    private static void Somar(Dictionary<string, int> contagem, string municipio) =>
        contagem[municipio] = contagem.GetValueOrDefault(municipio) + 1;

    private static long Numero(string texto, string arquivo, string coluna) =>
        long.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out long valor) && valor >= 0
            ? valor
            : throw new ImportException($"{arquivo}: valor '{texto}' sem número em {coluna}.");

    /// <summary>Reaproveita o agregado intermediário se for da mesma versão e mais novo que todos os .dbc.</summary>
    private static async Task<SaudeAgregados?> TryLoadAsync(string agregadoPath, List<CachedFile> arquivos)
    {
        if (!File.Exists(agregadoPath))
        {
            return null;
        }

        DateTime gravado = File.GetLastWriteTimeUtc(agregadoPath);
        if (arquivos.Any(a => File.GetLastWriteTimeUtc(a.Path) >= gravado))
        {
            return null;
        }

        try
        {
            SaudeAgregados? lido = JsonSerializer.Deserialize<SaudeAgregados>(await File.ReadAllTextAsync(agregadoPath), JsonOptions);
            return lido is { Versao: Versao, CompetenciaLeitos: CompetenciaLeitos } && lido.Anos.SequenceEqual(Anos) ? lido : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
