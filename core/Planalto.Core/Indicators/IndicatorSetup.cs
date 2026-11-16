using Planalto.Core.Economy;
using Planalto.Core.Municipios;
using Planalto.Core.State;

namespace Planalto.Core.Indicators;

/// <summary>
/// Monta os indicadores do município em foco a partir do dado importado (GDD 17.17): cada valor nasce igual ao
/// publicado; onde o dado é lacuna, vale a regra de preenchimento de data/indicators/model.json, marcada como estimativa.
/// </summary>
public static class IndicatorSetup
{
    public const string RegraMedianaPorteUf = "Mediana dos municípios do mesmo porte na UF";
    public const string RegraMedianaUf = "Mediana dos municípios da UF";
    public const string RegraMeioDaFaixa = "Meio da faixa plausível (sem dado na UF)";
    public const string RegraIndiceDoJogo = "Índice do jogo, sem fonte oficial";

    /// <param name="content">Definições, modelo e ciclo.</param>
    /// <param name="m">Município em foco.</param>
    /// <param name="uf">Arquivo de data/indicadores_reais/ da UF do município.</param>
    /// <param name="municipios">Todos os municípios (população e porte dos vizinhos de comparação).</param>
    /// <param name="start">Dia em que a simulação começa.</param>
    /// <param name="seed">Semente do mundo.</param>
    public static MunicipalIndicatorsState Create(IndicatorContent content, Municipio m, IndicadoresUfFile uf, MunicipiosDataSet municipios, DateOnly start, ulong seed)
    {
        IndicadoresMunicipio own = uf.Municipios.First(i => i.CodigoIbge == m.CodigoIbge);
        Dictionary<string, decimal> values = IndicatorDefinitions.RealValues(own, m);

        // Valores dos outros municípios da UF, para a mediana de comparação e para preencher lacunas.
        var byCode = municipios.All.Where(x => x.Uf == m.Uf).ToDictionary(x => x.CodigoIbge);
        var peers = uf.Municipios
            .Where(i => byCode.ContainsKey(i.CodigoIbge))
            .Select(i => (Municipio: byCode[i.CodigoIbge], Values: IndicatorDefinitions.RealValues(i, byCode[i.CodigoIbge])))
            .ToList();

        var list = new List<IndicatorValue>();
        foreach (string id in IndicatorDefinitions.Ids)
        {
            RealIndicatorDefinition d = content.Definitions.Get(id);
            IndicatorModelEntry e = content.Model.Of(id);
            var (reference, count, rule) = Reference(content, d, e, id, m, peers);

            double partida;
            bool estimate;
            string? estimateRule;
            if (values.TryGetValue(id, out decimal imported))
            {
                partida = (double)imported;
                estimateRule = ImportedSubstitution(id, own);
                estimate = estimateRule is not null;
            }
            else
            {
                partida = reference;
                estimate = true;
                estimateRule = rule;
            }

            list.Add(new IndicatorValue
            {
                Id = id,
                Valor = partida,
                Partida = partida,
                Estimativa = estimate,
                RegraEstimativa = estimateRule,
                AnoFonte = SourceYear(id, own),
                TendenciaAnual = InitialTrend(d, e, id, own),
                InicioMandato = partida,
                Referencia = reference,
                ReferenciaPartida = reference,
                ReferenciaMunicipios = count,
            });
        }

        return new MunicipalIndicatorsState
        {
            Municipio = m.CodigoIbge,
            Uf = m.Uf,
            Porte = m.Porte,
            Inicio = start,
            UltimoPasso = Calendar.ForDate(start).WeekStart,
            MandatoDesde = start,
            Populacao = m.Populacao,
            PopulacaoPartida = m.Populacao,
            // Gerador próprio, derivado da semente do mundo e do município.
            Rng = new Rng(unchecked((seed * 0x9E3779B97F4A7C15UL) ^ (ulong)m.CodigoIbge ^ 0x1D1CADDEUL)),
            Ciclo = EconomyCycle.Start(content.Cycle, content.EconomyStart),
            Indicadores = list,
        };
    }

    /// <summary>Referência de comparação e de preenchimento: mediana do mesmo porte na UF; com poucos, a UF inteira.</summary>
    private static (double Value, int Count, string Rule) Reference(
        IndicatorContent content, RealIndicatorDefinition d, IndicatorModelEntry e, string id, Municipio m, List<(Municipio Municipio, Dictionary<string, decimal> Values)> peers)
    {
        if (e.Preenchimento == IndicatorModelEntry.ValorFixo)
        {
            return (e.Valor ?? (d.Piso + d.Teto) / 2, 0, RegraIndiceDoJogo);
        }

        List<double> samePorte = peers.Where(p => p.Municipio.Porte == m.Porte && p.Values.ContainsKey(id)).Select(p => (double)p.Values[id]).ToList();
        if (samePorte.Count >= content.Model.MinMunicipiosReferencia)
        {
            return (Median(samePorte), samePorte.Count, RegraMedianaPorteUf);
        }

        List<double> all = peers.Where(p => p.Values.ContainsKey(id)).Select(p => (double)p.Values[id]).ToList();
        return all.Count > 0 ? (Median(all), all.Count, RegraMedianaUf) : ((d.Piso + d.Teto) / 2, 0, RegraMeioDaFaixa);
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        int n = values.Count;
        return n % 2 == 1 ? values[n / 2] : (values[(n / 2) - 1] + values[n / 2]) / 2;
    }

    /// <summary>Regra da substituição já feita pelo importador (o valor existe, mas não é dado do município).</summary>
    private static string? ImportedSubstitution(string id, IndicadoresMunicipio i) => id switch
    {
        IndicatorDefinitions.Ideb => i.Ideb.Fonte switch
        {
            IdebMunicipal.EdicaoAnterior => "Nota da edição anterior do IDEB",
            IdebMunicipal.DaUf => "Média da rede municipal na UF",
            IdebMunicipal.RedeEstadual => "Nota da rede estadual do próprio município",
            _ => null,
        },
        IndicatorDefinitions.MortalidadeInfantil => i.Saude.MortalidadeInfantilFonte == SaudeMunicipal.DaRegiaoImediata ? "Taxa da região geográfica imediata" : null,
        IndicatorDefinitions.ReceitaPerCapita => i.Financas.Fonte == FinancasEnte.Estimativa ? "Mediana por habitante dos municípios do mesmo porte na UF" : null,
        _ => null,
    };

    private static int SourceYear(string id, IndicadoresMunicipio i) => id switch
    {
        IndicatorDefinitions.Ideb => i.Ideb.Ano,
        IndicatorDefinitions.MortalidadeInfantil or IndicatorDefinitions.Homicidios => i.Saude.Anos.Count == 0 ? 0 : i.Saude.Anos.Max(),
        // Competência do CNES no formato AAMM.
        IndicatorDefinitions.Leitos => 2000 + (i.Saude.LeitosCompetencia / 100),
        IndicatorDefinitions.RedeAgua or IndicatorDefinitions.RedeEsgoto => PopulationProjection.CensusYear,
        IndicatorDefinitions.EmpregoFormal => i.Emprego.Ano,
        IndicatorDefinitions.PessoalRcl => i.Financas.RgfExercicio ?? i.Financas.Exercicio,
        IndicatorDefinitions.ReceitaPerCapita => i.Financas.Exercicio,
        IndicatorDefinitions.PibPerCapita => i.Pib?.Ano ?? 0,
        _ => 0,
    };

    /// <summary>
    /// Inclinação inicial em pontos de jogo por ano. No IDEB vem das três últimas edições publicadas do próprio
    /// município (a única herança da série histórica); nos demais, do parâmetro do modelo.
    /// </summary>
    private static double InitialTrend(RealIndicatorDefinition d, IndicatorModelEntry e, string id, IndicadoresMunicipio i)
    {
        if (id == IndicatorDefinitions.Ideb && i.Ideb.Fonte is IdebMunicipal.DoMunicipio or IdebMunicipal.EdicaoAnterior)
        {
            var last = i.Ideb.Serie.OrderBy(s => s.Key).TakeLast(3).ToList();
            if (last.Count >= 2 && last[^1].Key > last[0].Key)
            {
                double slope = (IndicatorScale.ToGame(d, (double)last[^1].Value) - IndicatorScale.ToGame(d, (double)last[0].Value)) / (last[^1].Key - last[0].Key);
                return Math.Clamp(slope, -e.TendenciaMaxAnual, e.TendenciaMaxAnual);
            }
        }

        return e.TendenciaAnual;
    }
}
