using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Planalto.Core.Data;
using Planalto.Core.Economy;
using Planalto.Core.Indicators;
using Planalto.Core.Municipios;

namespace Planalto.DataImporter.Rd;

/// <summary>Contagens de saúde por município de residência (código de 6 dígitos), somadas nos anos lidos.</summary>
public sealed record SaudeInput(
    IReadOnlyList<int> Anos,
    IReadOnlyDictionary<int, long> ObitosInfantis,
    IReadOnlyDictionary<int, long> NascidosVivos,
    IReadOnlyDictionary<int, long> ObitosAgressao,
    int LeitosCompetencia,
    IReadOnlyDictionary<int, long> Leitos,
    IReadOnlyList<(string Campo, string Fonte, string Url, DateTimeOffset BaixadoEm)> Fontes);

/// <summary>IDEB dos anos iniciais da rede municipal: município (7 dígitos) → edição → nota publicada.</summary>
public sealed record IdebInput(int Edicao, IReadOnlyDictionary<int, Dictionary<int, decimal>> PorMunicipio, CachedFile File);

/// <summary>
/// Modo "rd" do importador (GDD 17.17): monta data/indicadores_reais/{UF}.json, _ufs.json e _fontes.json e
/// data/economy/start.json a partir das fontes oficiais no cache. Lacunas seguem a regra de substituição de cada
/// indicador e ficam registradas; nada é gravado se alguma conferência falhar.
/// </summary>
public static class RdImport
{
    public const string PibPeriodo = "2023";
    public const string CensoPeriodo = "2022";
    public const string DesocupacaoPeriodo = "202602";
    public const int DistritoFederalMunicipio = 5300108;
    public const int DistritoFederalUf = 53;

    public sealed record Lacuna(int CodigoIbge, string Nome, string Uf, string Indicador, string Regra);

    /// <summary>Lê saúde (DATASUS) e IDEB (INEP) e monta os arquivos.</summary>
    public static async Task<int> RunAsync(SourceCache cache, string dataDirectory)
    {
        Console.WriteLine("Ministério da Saúde — SIM, SINASC e CNES");
        SaudeAgregados agregados = await SaudeImport.ReadAsync(cache);
        Dictionary<int, long> Soma(Dictionary<int, Dictionary<string, int>> porAno) =>
            porAno.Values.SelectMany(d => d).GroupBy(p => int.Parse(p.Key, CultureInfo.InvariantCulture)).ToDictionary(g => g.Key, g => g.Sum(p => (long)p.Value));
        DateTimeOffset baixado = agregados.Arquivos.Max(f => f.DownloadedAt);
        var saude = new SaudeInput(
            agregados.Anos,
            Soma(agregados.ObitosMenoresDeUmAno),
            Soma(agregados.NascidosVivos),
            Soma(agregados.ObitosPorAgressao),
            int.Parse(agregados.CompetenciaLeitos[2..], CultureInfo.InvariantCulture),
            agregados.Leitos.ToDictionary(p => int.Parse(p.Key, CultureInfo.InvariantCulture), p => p.Value.Existentes),
            [
                ("saude.obitos_infantis, saude.obitos_agressao", "Ministério da Saúde, DATASUS, Sistema de Informações sobre Mortalidade (SIM), declarações de óbito por município de residência", string.Format(CultureInfo.InvariantCulture, SaudeImport.SimUrl, "{UF}", "{ano}"), baixado),
                ("saude.nascidos_vivos, saude.mortalidade_infantil", "Ministério da Saúde, DATASUS, Sistema de Informações sobre Nascidos Vivos (SINASC), por município de residência", string.Format(CultureInfo.InvariantCulture, SaudeImport.SinascUrl, "{UF}", "{ano}"), baixado),
                ("saude.leitos", "Ministério da Saúde, DATASUS, Cadastro Nacional de Estabelecimentos de Saúde (CNES), leitos existentes", string.Format(CultureInfo.InvariantCulture, SaudeImport.CnesLeitosUrl, "{UF}", "{AAMM}"), baixado),
            ]);

        Console.WriteLine("INEP — IDEB");
        IdebInput ideb = await IdebImport.ReadAsync(cache);
        return await RunAsync(cache, dataDirectory, saude, ideb);
    }

    public static async Task<int> RunAsync(SourceCache cache, string dataDirectory, SaudeInput saude, IdebInput ideb)
    {
        PorteCatalog portes = JsonData.LoadFile<PorteCatalog>(Path.Combine(dataDirectory, "portes.json")).GetValueOrThrow();
        var (dataSet, loadErrors) = MunicipiosDataSet.Load(dataDirectory, portes);
        if (dataSet is null)
        {
            throw new ImportException("data/municipios inválido: " + string.Join("; ", loadErrors.Take(5)));
        }

        IndicatorDefinitions definitions = JsonData.LoadFile<IndicatorDefinitions>(Path.Combine(dataDirectory, IndicatorDefinitions.File)).GetValueOrThrow();
        List<Municipio> municipios = dataSet.All.OrderBy(m => m.CodigoIbge).ToList();
        CachedFile localidades = await cache.GetAsync("ibge_localidades_municipios.json", Ibge.LocalidadesUrl);
        Dictionary<string, int> ufIds = Ibge.ReadLocalidades(localidades.Path).GroupBy(m => m.UfSigla).ToDictionary(g => g.Key, g => g.First().UfId);

        var fontes = new List<object>();
        var lacunas = new List<Lacuna>();
        var errors = new List<string>();
        void Fonte(string campo, string fonte, int ano, CachedFile file) =>
            fontes.Add(new { Campo = campo, Fonte = fonte, Ano = ano, Url = file.Url, BaixadoEm = file.DownloadedAt });

        // PIB dos Municípios.
        Console.WriteLine("IBGE — PIB dos Municípios");
        string pibVars = string.Join("|", new[] { SidraRd.PibVar, SidraRd.PibAgroVar, SidraRd.PibIndustriaVar, SidraRd.PibServicosVar, SidraRd.PibAdmVar });
        CachedFile pibFile = await cache.GetAsync($"rd/sidra_{SidraRd.PibTable}_{PibPeriodo}.json", SidraRd.Url(SidraRd.PibTable, PibPeriodo, pibVars, "N6[all]|N3[all]"));
        Fonte("pib", $"IBGE, Produto Interno Bruto dos Municípios, SIDRA {SidraRd.PibTable}", int.Parse(PibPeriodo, CultureInfo.InvariantCulture), pibFile);
        var pib = SidraRd.Read(pibFile.Path, PibPeriodo, null);

        // Saneamento do Censo 2022.
        Console.WriteLine("IBGE — Censo 2022, saneamento");
        string aguaCats = string.Join(",", AguaCategories());
        CachedFile aguaFile = await cache.GetAsync($"rd/sidra_{SidraRd.AguaTable}.json",
            SidraRd.Url(SidraRd.AguaTable, CensoPeriodo, SidraRd.DomiciliosVar.ToString(CultureInfo.InvariantCulture), "N6[all]|N3[all]", $"{SidraRd.AguaClass}[{aguaCats}]"));
        CachedFile esgotoFile = await cache.GetAsync($"rd/sidra_{SidraRd.EsgotoTable}.json",
            SidraRd.Url(SidraRd.EsgotoTable, CensoPeriodo, SidraRd.DomiciliosVar.ToString(CultureInfo.InvariantCulture), "N6[all]|N3[all]", $"{SidraRd.EsgotoClass}[{SidraRd.EsgotoTotal},{SidraRd.EsgotoRede}]"));
        Fonte("saneamento.rede_agua", $"IBGE, Censo Demográfico 2022, SIDRA {SidraRd.AguaTable}", 2022, aguaFile);
        Fonte("saneamento.rede_esgoto, saneamento.domicilios", $"IBGE, Censo Demográfico 2022, SIDRA {SidraRd.EsgotoTable}", 2022, esgotoFile);
        var agua = SidraRd.Read(aguaFile.Path, CensoPeriodo, SidraRd.AguaClass);
        var esgoto = SidraRd.Read(esgotoFile.Path, CensoPeriodo, SidraRd.EsgotoClass);

        // Emprego formal.
        Console.WriteLine("MTE — RAIS, estabelecimentos");
        RaisImport.Result rais = await RaisImport.ReadAsync(cache);
        Fonte("emprego", "Ministério do Trabalho e Emprego, RAIS, microdados de estabelecimentos (vínculos ativos em 31/12)", RaisImport.Ano, rais.Fonte);

        // Economia nacional e desocupação por UF.
        Console.WriteLine("IBGE e Banco Central — séries nacionais");
        NacionalImport.Result nacional = await NacionalImport.ReadAsync(cache, DesocupacaoPeriodo);
        var desocupacao = SidraRd.Read(nacional.Fontes.First(f => f.Campo == "desocupacao").File.Path, DesocupacaoPeriodo, null)[(SidraRd.DesocupacaoVar, 0)];

        foreach (var f in saude.Fontes)
        {
            fontes.Add(new { f.Campo, f.Fonte, Ano = saude.Anos.Max(), f.Url, f.BaixadoEm });
        }

        Fonte("ideb", "INEP, IDEB, anos iniciais do ensino fundamental, resultados por município e rede", ideb.Edicao, ideb.File);
        fontes.Add(new
        {
            Campo = "financas",
            Fonte = "Tesouro Nacional, SICONFI: DCA (Anexos I-C e I-E) e RGF (Anexo 01), API do Data Lake",
            Ano = SiconfiDownload.Exercicios[0],
            Url = SiconfiDownload.BaseUrl,
            BaixadoEm = Directory.EnumerateFiles(SiconfiDownload.Folder(cache.Directory), "*.json").Select(File.GetLastWriteTimeUtc).DefaultIfEmpty(DateTime.UtcNow).Max(),
        });

        // Finanças declaradas (o DF declara como ente estadual).
        Console.WriteLine("Tesouro Nacional — SICONFI");
        var financas = new Dictionary<int, FinancasEnte>();
        foreach (Municipio m in municipios)
        {
            int ente = m.CodigoIbge == DistritoFederalMunicipio ? DistritoFederalUf : m.CodigoIbge;
            if (!SiconfiRead.Downloaded(cache.Directory, ente))
            {
                errors.Add($"SICONFI: o ente {ente} ainda não está no cache; rode 'rd siconfi-baixar' até o fim.");
                continue;
            }

            if (SiconfiRead.Read(cache.Directory, ente) is { } f)
            {
                financas[m.CodigoIbge] = f;
            }
        }

        if (errors.Count > 0)
        {
            errors.Take(5).ToList().ForEach(e => Console.Error.WriteLine($"  ERRO {e}"));
            throw new ImportException($"{errors.Count} entes sem download do SICONFI; nada foi gravado.");
        }

        // Médias e regiões para as regras de substituição.
        var idebUf = municipios.GroupBy(m => m.Uf).ToDictionary(g => g.Key, g =>
        {
            var notas = g.Select(m => ideb.PorMunicipio.TryGetValue(m.CodigoIbge, out var s) && s.TryGetValue(ideb.Edicao, out decimal v) ? v : (decimal?)null).Where(v => v is not null).ToList();
            return notas.Count == 0 ? (decimal?)null : Math.Round(notas.Average(v => v!.Value), 2);
        });
        var regiao = municipios.GroupBy(m => m.RegiaoImediata.Id).ToDictionary(
            g => g.Key,
            g => (Obitos: g.Sum(m => saude.ObitosInfantis.GetValueOrDefault(m.CodigoIbge / 10)), Nascidos: g.Sum(m => saude.NascidosVivos.GetValueOrDefault(m.CodigoIbge / 10))));

        var files = new SortedDictionary<string, List<IndicadoresMunicipio>>(StringComparer.Ordinal);
        foreach (Municipio m in municipios)
        {
            string key = m.CodigoIbge.ToString(CultureInfo.InvariantCulture);
            int six = m.CodigoIbge / 10;
            void Gap(string indicador, string regra) => lacunas.Add(new Lacuna(m.CodigoIbge, m.Nome, m.Uf, indicador, regra));

            PibMunicipal? pibM = null;
            if (pib[(SidraRd.PibVar, 0)].TryGetValue(key, out string? pibRaw) && SidraRd.Whole(pibRaw) is long pibValue)
            {
                long Var(int v) => SidraRd.Whole(pib[(v, 0)].GetValueOrDefault(key, "...")) ?? 0;
                pibM = new PibMunicipal
                {
                    Ano = int.Parse(PibPeriodo, CultureInfo.InvariantCulture),
                    ValorMil = pibValue,
                    AgropecuariaMil = Var(SidraRd.PibAgroVar),
                    IndustriaMil = Var(SidraRd.PibIndustriaVar),
                    ServicosMil = Var(SidraRd.PibServicosVar),
                    AdministracaoPublicaMil = Var(SidraRd.PibAdmVar),
                };
            }
            else
            {
                Gap("pib", "sem valor na fonte (município criado depois do ano de referência); fica sem PIB próprio");
            }

            SaneamentoMunicipal? saneamento = null;
            long? domicilios = Whole(esgoto, SidraRd.EsgotoTotal, key);
            long? redeEsgoto = Whole(esgoto, SidraRd.EsgotoRede, key);
            long? redeAgua = SidraRd.AguaLigada.Select(c => Whole(agua, c, key)).Aggregate((long?)0, (a, b) => a is null || b is null ? null : a + b);
            if (domicilios is not null && redeEsgoto is not null && redeAgua is not null)
            {
                saneamento = new SaneamentoMunicipal { Domicilios = domicilios.Value, RedeAgua = redeAgua.Value, RedeEsgoto = redeEsgoto.Value };
            }
            else
            {
                Gap("saneamento", "sem valor no Censo 2022 (município criado depois); fica sem saneamento próprio");
            }

            if (!financas.TryGetValue(m.CodigoIbge, out FinancasEnte? fin))
            {
                fin = EstimateFinancas(m, municipios, financas, out string grupo);
                Gap("financas", $"sem DCA de {string.Join(", ", SiconfiDownload.Exercicios)} no SICONFI; mediana por habitante dos municípios de {grupo}, marcada como estimativa");
            }
            else if (fin.Fonte == FinancasEnte.ExercicioAnterior)
            {
                Gap("financas", $"sem DCA de {SiconfiDownload.Exercicios[0]}; usa a de {fin.Exercicio}");
            }

            if (fin.PessoalPctRcl is null)
            {
                Gap("financas.pessoal_pct_rcl", "sem RGF do Executivo no SICONFI nos exercícios lidos; fica sem o percentual");
            }

            IdebMunicipal idebM;
            Dictionary<int, decimal> serie = ideb.PorMunicipio.GetValueOrDefault(m.CodigoIbge) ?? [];
            if (serie.TryGetValue(ideb.Edicao, out decimal nota))
            {
                idebM = new IdebMunicipal { Fonte = IdebMunicipal.DoMunicipio, Ano = ideb.Edicao, Valor = nota, Serie = serie };
            }
            else if (serie.Count > 0)
            {
                int ano = serie.Keys.Max();
                idebM = new IdebMunicipal { Fonte = IdebMunicipal.EdicaoAnterior, Ano = ano, Valor = serie[ano], Serie = serie };
                Gap("ideb", $"sem nota da rede municipal em {ideb.Edicao}; usa a edição de {ano}");
            }
            else
            {
                decimal media = idebUf[m.Uf] ?? throw new ImportException($"IDEB: nenhuma nota municipal em {m.Uf}.");
                idebM = new IdebMunicipal { Fonte = IdebMunicipal.DaUf, Ano = ideb.Edicao, Valor = media, Serie = serie };
                Gap("ideb", $"sem nota da rede municipal em nenhuma edição; usa a média da rede municipal em {m.Uf}");
            }

            long obitos = saude.ObitosInfantis.GetValueOrDefault(six);
            long nascidos = saude.NascidosVivos.GetValueOrDefault(six);
            bool proprio = nascidos >= definitions.MinNascidosMortalidadeInfantil;
            var (regObitos, regNascidos) = regiao[m.RegiaoImediata.Id];
            if (!proprio)
            {
                Gap("mortalidade_infantil", $"menos de {definitions.MinNascidosMortalidadeInfantil} nascidos vivos em {saude.Anos.Min()}-{saude.Anos.Max()}; usa a taxa da região imediata {m.RegiaoImediata.Nome}");
            }

            decimal taxa = proprio ? obitos * 1000m / nascidos : regNascidos > 0 ? regObitos * 1000m / regNascidos : 0;
            var saudeM = new SaudeMunicipal
            {
                Anos = saude.Anos.ToList(),
                ObitosInfantis = obitos,
                NascidosVivos = nascidos,
                MortalidadeInfantil = Math.Round(taxa, 2),
                MortalidadeInfantilFonte = proprio ? SaudeMunicipal.DoMunicipio : SaudeMunicipal.DaRegiaoImediata,
                ObitosAgressao = saude.ObitosAgressao.GetValueOrDefault(six),
                LeitosCompetencia = saude.LeitosCompetencia,
                Leitos = saude.Leitos.GetValueOrDefault(six),
            };

            var item = new IndicadoresMunicipio
            {
                CodigoIbge = m.CodigoIbge,
                Pib = pibM,
                Emprego = new EmpregoFormal { Ano = RaisImport.Ano, Vinculos = rais.VinculosPorMunicipio6.GetValueOrDefault(six) },
                Financas = fin,
                Ideb = idebM,
                Saude = saudeM,
                Saneamento = saneamento,
            };

            foreach (var (id, value) in IndicatorDefinitions.RealValues(item, m))
            {
                RealIndicatorDefinition d = definitions.Get(id);
                if (value < d.Min || value > d.Max)
                {
                    errors.Add($"{m.CodigoIbge} {m.Nome}/{m.Uf}: {id} = {value:0.##} fora da faixa plausível [{d.Min}, {d.Max}].");
                }
            }

            if (!files.TryGetValue(m.Uf, out List<IndicadoresMunicipio>? list))
            {
                files[m.Uf] = list = [];
            }

            list.Add(item);
        }

        // Totais por UF iguais aos oficiais, onde a fonte publica o total da UF.
        var totaisUf = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var (uf, list) in files)
        {
            string ufKey = ufIds[uf].ToString(CultureInfo.InvariantCulture);
            long pibOficial = SidraRd.Whole(pib[(SidraRd.PibVar, 0)][ufKey]) ?? throw new ImportException($"PIB de {uf} ausente.");
            long pibSoma = list.Sum(i => i.Pib?.ValorMil ?? 0);

            // O PIB é publicado em mil reais arredondados: a soma dos municípios difere do total da UF só pelo arredondamento.
            if (Math.Abs(pibSoma - pibOficial) > list.Count)
            {
                errors.Add($"{uf}: soma do PIB dos municípios {pibSoma} diferente do total da UF {pibOficial}.");
            }

            long domOficial = Whole(esgoto, SidraRd.EsgotoTotal, ufKey) ?? throw new ImportException($"Domicílios de {uf} ausentes.");
            long domSoma = list.Sum(i => i.Saneamento?.Domicilios ?? 0);
            if (domSoma != domOficial)
            {
                errors.Add($"{uf}: soma dos domicílios dos municípios {domSoma} diferente do total da UF {domOficial}.");
            }

            totaisUf[uf] = new { PibMil = pibOficial, PibMilSomaMunicipios = pibSoma, Domicilios = domOficial };
        }

        if (errors.Count > 0)
        {
            errors.Take(60).ToList().ForEach(e => Console.Error.WriteLine($"  ERRO {e}"));
            throw new ImportException($"{errors.Count} erros; nada foi gravado.");
        }

        // UFs.
        var ufs = files.Keys.Select(uf => new IndicadoresUf
        {
            Uf = uf,
            Financas = SiconfiRead.Read(cache.Directory, ufIds[uf]),
            Desocupacao = SidraRd.Number(desocupacao[ufIds[uf].ToString(CultureInfo.InvariantCulture)]) ?? throw new ImportException($"Desocupação de {uf} ausente."),
            DesocupacaoPeriodo = int.Parse(DesocupacaoPeriodo, CultureInfo.InvariantCulture),
            IdebMedioRedeMunicipal = idebUf[uf] ?? 0,
        }).ToList();

        var options = new JsonSerializerOptions(JsonData.Options) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var compact = new JsonSerializerOptions(options) { WriteIndented = false };
        string directory = Path.Combine(dataDirectory, IndicadoresPaths.Directory);
        Directory.CreateDirectory(directory);
        var utf8 = new UTF8Encoding(false);
        foreach (var (uf, list) in files)
        {
            var file = new IndicadoresUfFile { Uf = uf, Municipios = list };
            List<string> invalid = file.Validate().ToList();
            if (invalid.Count > 0)
            {
                throw new ImportException($"{uf}: {string.Join("; ", invalid.Take(5))}");
            }

            // Um município por linha, para o arquivo ficar legível e os diffs, pequenos.
            var sb = new StringBuilder("{\n");
            sb.Append($"  \"uf\": {JsonSerializer.Serialize(uf, compact)},\n  \"municipios\": [\n");
            for (int i = 0; i < list.Count; i++)
            {
                sb.Append("    ").Append(JsonSerializer.Serialize(list[i], compact)).Append(i < list.Count - 1 ? ",\n" : "\n");
            }

            sb.Append("  ]\n}\n");
            await File.WriteAllTextAsync(IndicadoresPaths.File(dataDirectory, uf), sb.ToString(), utf8);
        }

        await File.WriteAllTextAsync(Path.Combine(directory, IndicadoresPaths.UfsFileName),
            JsonSerializer.Serialize(new IndicadoresUfsFile { Ufs = ufs }, options).ReplaceLineEndings("\n") + "\n", utf8);

        var fontesFile = new
        {
            GeradoEm = DateTimeOffset.UtcNow,
            Fontes = fontes.Concat(nacional.Fontes.Select(f => (object)new { f.Campo, f.Fonte, Ano = DateTime.UtcNow.Year, f.File.Url, BaixadoEm = f.File.DownloadedAt })).ToList(),
            Regras = new
            {
                Financas = $"DCA do exercício mais recente entre {string.Join(", ", SiconfiDownload.Exercicios)}; sem nenhuma, mediana por habitante dos municípios do mesmo porte na UF (ou, sem nenhum, do mesmo porte no país), marcada como estimativa. O Distrito Federal declara como ente estadual ({DistritoFederalUf}).",
                Ideb = "Nota da rede municipal na edição mais recente; sem ela, a edição anterior mais recente; sem nenhuma, a média simples da rede municipal na UF.",
                MortalidadeInfantil = $"Óbitos de menores de 1 ano por mil nascidos vivos, por município de residência, na soma de {saude.Anos.Min()} a {saude.Anos.Max()}; com menos de {definitions.MinNascidosMortalidadeInfantil} nascidos vivos no período, a taxa da região geográfica imediata.",
                Homicidios = $"Óbitos por agressão (CID-10, X85 a Y09), por município de residência, soma de {saude.Anos.Min()} a {saude.Anos.Max()}.",
                Emprego = "Soma dos vínculos ativos em 31/12 dos estabelecimentos do município; município sem estabelecimento na RAIS: zero.",
                Leitos = "Soma dos leitos existentes dos estabelecimentos do município na competência; município sem estabelecimento com leito: zero.",
                Pib = "PIB a preços correntes, em mil reais; a soma dos municípios de cada UF confere com o total publicado da UF (diferença só de arredondamento).",
                Saneamento = "Domicílios particulares permanentes ocupados; rede de água = com ligação à rede geral; rede de esgoto = rede geral, rede pluvial ou fossa ligada à rede. A soma dos domicílios de cada UF é igual ao total publicado.",
            },
            TotaisUf = totaisUf,
            Resumo = new
            {
                Municipios = municipios.Count,
                FinancasDeclaradas = files.Values.Sum(l => l.Count(i => i.Financas.Fonte == FinancasEnte.Declarado)),
                FinancasExercicioAnterior = files.Values.Sum(l => l.Count(i => i.Financas.Fonte == FinancasEnte.ExercicioAnterior)),
                FinancasEstimadas = files.Values.Sum(l => l.Count(i => i.Financas.Fonte == FinancasEnte.Estimativa)),
                IdebDoMunicipio = files.Values.Sum(l => l.Count(i => i.Ideb.Fonte == IdebMunicipal.DoMunicipio)),
                IdebEdicaoAnterior = files.Values.Sum(l => l.Count(i => i.Ideb.Fonte == IdebMunicipal.EdicaoAnterior)),
                IdebDaUf = files.Values.Sum(l => l.Count(i => i.Ideb.Fonte == IdebMunicipal.DaUf)),
                MortalidadeInfantilDaRegiao = files.Values.Sum(l => l.Count(i => i.Saude.MortalidadeInfantilFonte == SaudeMunicipal.DaRegiaoImediata)),
            },
            Lacunas = lacunas,
        };
        await File.WriteAllTextAsync(Path.Combine(directory, IndicadoresPaths.FontesFileName),
            JsonSerializer.Serialize(fontesFile, options).ReplaceLineEndings("\n") + "\n", utf8);

        string economy = Path.Combine(dataDirectory, EconomyStart.File);
        Directory.CreateDirectory(Path.GetDirectoryName(economy)!);
        await File.WriteAllTextAsync(economy, JsonSerializer.Serialize(nacional.Start, options).ReplaceLineEndings("\n") + "\n", utf8);

        long bytes = Directory.EnumerateFiles(directory, "*.json").Sum(f => new FileInfo(f).Length);
        Console.WriteLine($"Gravado: data/{IndicadoresPaths.Directory}/ ({bytes / 1024.0 / 1024.0:0.0} MB, {lacunas.Count} lacunas registradas) e data/{EconomyStart.File}.");
        return 0;
    }

    private static IEnumerable<int> AguaCategories() => SidraRd.AguaLigada.Prepend(SidraRd.AguaTotal);

    private static long? Whole(Dictionary<(int Variable, int Category), Dictionary<string, string>> table, int category, string localidade) =>
        table.TryGetValue((SidraRd.DomiciliosVar, category), out var byLocal) && byLocal.TryGetValue(localidade, out string? raw) ? SidraRd.Whole(raw) : null;

    /// <summary>
    /// Regra de substituição das finanças (GDD 17.17): mediana por habitante dos municípios do mesmo porte na UF com DCA
    /// entregue; se a UF não tiver nenhum desse porte, do mesmo porte no país. Não é dado do ente.
    /// </summary>
    private static FinancasEnte EstimateFinancas(Municipio m, List<Municipio> all, Dictionary<int, FinancasEnte> declared, out string grupo)
    {
        List<Municipio> peers = all.Where(o => o.Porte == m.Porte && o.Uf == m.Uf && declared.ContainsKey(o.CodigoIbge) && o.Populacao > 0).ToList();
        grupo = $"porte {m.Porte} em {m.Uf}";
        if (peers.Count == 0)
        {
            peers = all.Where(o => o.Porte == m.Porte && declared.ContainsKey(o.CodigoIbge) && o.Populacao > 0).ToList();
            grupo = $"porte {m.Porte} no país";
        }

        if (peers.Count == 0)
        {
            throw new ImportException($"SICONFI: nenhum município de porte {m.Porte} com DCA para estimar {m.CodigoIbge}.");
        }

        decimal Median(IEnumerable<decimal> values)
        {
            List<decimal> sorted = values.Order().ToList();
            return sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2m;
        }

        var funcoes = peers.SelectMany(p => declared[p.CodigoIbge].DespesaPorFuncao.Keys).Distinct().Order(StringComparer.Ordinal)
            .ToDictionary(
                f => f,
                f => Math.Round(Median(peers.Select(p => declared[p.CodigoIbge].DespesaPorFuncao.GetValueOrDefault(f) / p.Populacao)) * m.Populacao, 2),
                StringComparer.Ordinal);
        return new FinancasEnte
        {
            Fonte = FinancasEnte.Estimativa,
            Exercicio = SiconfiDownload.Exercicios[0],
            ReceitaCorrente = Math.Round(Median(peers.Select(p => declared[p.CodigoIbge].ReceitaCorrente / p.Populacao)) * m.Populacao, 2),
            DespesaPorFuncao = funcoes.Where(p => p.Value > 0).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal),
        };
    }
}
