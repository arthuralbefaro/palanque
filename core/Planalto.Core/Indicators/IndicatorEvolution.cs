using Planalto.Core.Economy;
using Planalto.Core.Municipios;
using Planalto.Core.State;

namespace Planalto.Core.Indicators;

/// <summary>
/// Evolução dos indicadores do município em foco (GDD 17.17), semana a semana:
/// valor(t+1) = valor(t) + velocidade × (alvo − valor) + choque, dentro dos limites plausíveis, com
/// alvo = partida + tendência + pressões + contexto. A conta é feita na escala de jogo e o resultado é guardado na
/// unidade real. Toda aleatoriedade vem do gerador guardado no próprio estado.
/// </summary>
public sealed class IndicatorEvolution
{
    private const double DaysPerYear = 365.25;

    public IndicatorEvolution(IndicatorContent content, Municipio municipio, PopulationProjection? projection = null)
    {
        Content = content;
        Municipio = municipio;
        Projection = projection;
    }

    public IndicatorContent Content { get; }

    public Municipio Municipio { get; }

    /// <summary>Projeção da população (IBGE, por UF); sem ela, a população fica a de partida.</summary>
    public PopulationProjection? Projection { get; }

    private IndicatorDefinitions Definitions => Content.Definitions;

    private IndicatorModelConfig Model => Content.Model;

    // ---------------------------------------------------------------- pontos de extensão

    /// <summary>
    /// Aplica uma pressão de <paramref name="pontos"/> (escala de jogo; positivo melhora) no alvo do indicador real
    /// <paramref name="indicatorId"/> por <paramref name="semanas"/> semanas a partir de <paramref name="desde"/>;
    /// <paramref name="semanas"/> null = sem prazo, até <see cref="RemovePressure"/>. O indicador caminha para o alvo
    /// na velocidade dele e volta quando a pressão acaba. <paramref name="origem"/> identifica quem aplicou
    /// ("politica:creche", "obra:12") e <paramref name="texto"/> aparece na explicação da variação.
    /// </summary>
    public IndicatorPressure ApplyPressure(MunicipalIndicatorsState state, string indicatorId, double pontos, int? semanas, DateOnly desde, string origem, string texto)
    {
        if (Definitions.Find(indicatorId) is null)
        {
            throw new ArgumentException($"Indicador '{indicatorId}' inexistente.", nameof(indicatorId));
        }

        if (semanas is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(semanas), semanas, "O prazo deve ser de 1 semana ou mais, ou null para sem prazo.");
        }

        var pressure = new IndicatorPressure
        {
            Id = state.NextPressureId++,
            IndicatorId = indicatorId,
            Pontos = pontos,
            Desde = desde,
            Ate = semanas is int weeks ? desde.AddDays((7 * weeks) - 1) : null,
            Origem = origem,
            Texto = texto,
        };
        state.Pressoes.Add(pressure);
        return pressure;
    }

    /// <summary>
    /// O mesmo, para um indicador da prefeitura (saúde, educação…): a pressão vai para cada indicador real que o
    /// compõe, pelo mapa de data/indicators/model.json.
    /// </summary>
    public List<IndicatorPressure> ApplyGamePressure(MunicipalIndicatorsState state, string gameIndicatorId, double pontos, int? semanas, DateOnly desde, string origem, string texto) =>
        Components(gameIndicatorId).Select(id => ApplyPressure(state, id, pontos, semanas, desde, origem, texto)).ToList();

    /// <summary>
    /// Pressão permanente com valor que muda (o orçamento da semana, uma política em vigor): cria, atualiza ou, com
    /// zero pontos, retira a pressão sem prazo daquela origem sobre o indicador.
    /// </summary>
    public void SetStandingPressure(MunicipalIndicatorsState state, string indicatorId, double pontos, DateOnly desde, string origem, string texto)
    {
        IndicatorPressure? current = state.Pressoes.FirstOrDefault(p => p.Ate is null && p.Origem == origem && p.IndicatorId == indicatorId);
        if (Math.Abs(pontos) < 1e-12)
        {
            if (current is not null)
            {
                state.Pressoes.Remove(current);
            }

            return;
        }

        if (current is null)
        {
            ApplyPressure(state, indicatorId, pontos, null, desde, origem, texto);
        }
        else
        {
            current.Pontos = pontos;
            current.Texto = texto;
        }
    }

    /// <summary>Retira todas as pressões de uma origem; devolve quantas saíram.</summary>
    public static int RemovePressure(MunicipalIndicatorsState state, string origem) => state.Pressoes.RemoveAll(p => p.Origem == origem);

    /// <summary>
    /// Choque imediato de <paramref name="pontos"/> (escala de jogo) no valor do indicador real: crise, fábrica que
    /// fecha, enchente. Entra na explicação da última variação.
    /// </summary>
    public void ApplyShock(MunicipalIndicatorsState state, string indicatorId, double pontos, DateOnly date, string origem, string texto)
    {
        RealIndicatorDefinition d = Definitions.Get(indicatorId);
        IndicatorValue v = state.Get(indicatorId);
        double before = v.Valor;
        var (min, max) = IndicatorScale.Bounds(d, v.Partida);
        v.Valor = Math.Clamp(IndicatorScale.FromGame(d, IndicatorScale.ToGame(d, before) + pontos), min, max);
        var factor = new IndicatorFactor(IndicatorFactor.Choque, origem, texto, pontos, v.Valor - before);
        v.UltimaVariacao = new IndicatorVariation { De = date, Ate = date, Antes = before, Depois = v.Valor, Fatores = { factor } };
        Accumulate(v, date, before, [factor]);
    }

    /// <summary>Choque num indicador da prefeitura: vai para cada indicador real que o compõe.</summary>
    public void ApplyGameShock(MunicipalIndicatorsState state, string gameIndicatorId, double pontos, DateOnly date, string origem, string texto)
    {
        foreach (string id in Components(gameIndicatorId))
        {
            ApplyShock(state, id, pontos, date, origem, texto);
        }
    }

    /// <summary>Começo de mandato: guarda os valores de hoje como base da "variação no mandato".</summary>
    public static void MarkMandateStart(MunicipalIndicatorsState state, DateOnly date)
    {
        state.MandatoDesde = date;
        foreach (IndicatorValue v in state.Indicadores)
        {
            v.InicioMandato = v.Valor;
        }
    }

    /// <summary>Indicadores reais que compõem um indicador da prefeitura; vazio se não houver mapa.</summary>
    public IEnumerable<string> Components(string gameIndicatorId) =>
        Model.Jogo.TryGetValue(gameIndicatorId, out Dictionary<string, double>? parts) ? parts.Keys : [];

    // ---------------------------------------------------------------- tempo

    /// <summary>
    /// Leva os indicadores até a semana de <paramref name="date"/>, uma semana por vez (inclusive as que o calendário
    /// saltou). Chamar duas vezes para a mesma semana não anda nada.
    /// </summary>
    public void AdvanceTo(MunicipalIndicatorsState state, DateOnly date)
    {
        DateOnly target = Calendar.ForDate(date).WeekStart;
        while (state.UltimoPasso < target)
        {
            StepWeek(state);
        }
    }

    /// <summary>Simula a semana seguinte à última simulada.</summary>
    public void StepWeek(MunicipalIndicatorsState state)
    {
        DateOnly week = state.UltimoPasso.AddDays(7);
        if (week.Year != state.UltimoPasso.Year)
        {
            foreach (IndicatorValue v in state.Indicadores)
            {
                v.VariacaoAnoAnterior = v.VariacaoNoAno;
                v.VariacaoNoAno = null;
            }

            if (Projection is not null)
            {
                state.Populacao = Projection.Population(Municipio, week.Year);
            }
        }

        EconomyCycle.StepWeek(state.Ciclo, Content.Cycle, state.Rng);
        double years = Math.Max(0, week.DayNumber - state.Inicio.DayNumber) / DaysPerYear;
        int horizon = Model.AnosTendencia;

        // Tendência acumulada de uma inclinação de 1 ponto por ano que perde força em linha reta até zerar.
        double trendShape = years < horizon ? years - (years * years / (2 * horizon)) : horizon / 2.0;

        // Os vizinhos são lidos antes do passo, para a ordem dos indicadores não mudar o resultado.
        var gameBefore = state.Indicadores.ToDictionary(
            v => v.Id,
            v => (Now: IndicatorScale.ToGame(Definitions.Get(v.Id), v.Valor), Start: IndicatorScale.ToGame(Definitions.Get(v.Id), v.Partida)),
            StringComparer.Ordinal);

        foreach (IndicatorValue v in state.Indicadores)
        {
            RealIndicatorDefinition d = Definitions.Get(v.Id);
            IndicatorModelEntry e = Model.Of(v.Id);
            var (g, g0) = gameBefore[v.Id];
            double speed = e.VelocidadeSemanal;
            double cycle = e.SensibilidadeCiclo * state.Ciclo.Hiato;
            var factors = new List<(string Tipo, string Origem, string Texto, double Pontos)>
            {
                (IndicatorFactor.Retorno, "", "", speed * (g0 - g)),
                (IndicatorFactor.Tendencia, "", "", speed * v.TendenciaAnual * trendShape),
                (IndicatorFactor.Ciclo, "", "", speed * cycle),
            };
            foreach (IndicatorPressure p in state.Pressoes.Where(p => p.IndicatorId == v.Id && p.ActiveOn(week)))
            {
                factors.Add((IndicatorFactor.Pressao, p.Origem, p.Texto, speed * p.Pontos));
            }

            foreach (var (neighbour, weight) in e.Vizinhos)
            {
                if (gameBefore.TryGetValue(neighbour, out var n))
                {
                    factors.Add((IndicatorFactor.Vizinho, neighbour, Definitions.Get(neighbour).Nome, speed * weight * (n.Now - n.Start)));
                }
            }

            if (e.ChoqueChanceSemanal > 0 && state.Rng.Chance(e.ChoqueChanceSemanal))
            {
                factors.Add((IndicatorFactor.Choque, "sorteio", "", e.ChoqueDesvio * EconomyCycle.Normal(state.Rng)));
            }

            double points = factors.Sum(f => f.Pontos);
            double before = v.Valor;
            double free = IndicatorScale.FromGame(d, g + points);
            var (min, max) = IndicatorScale.Bounds(d, v.Partida);
            v.Valor = Math.Clamp(free, min, max);

            // Cada fator responde pela parte da variação proporcional aos seus pontos; o corte do limite é fator à parte.
            double perPoint = Math.Abs(points) < 1e-15 ? 0 : (free - before) / points;
            var explained = factors.Where(f => Math.Abs(f.Pontos) > 1e-12).Select(f => new IndicatorFactor(f.Tipo, f.Origem, f.Texto, f.Pontos, f.Pontos * perPoint)).ToList();
            if (v.Valor != free)
            {
                explained.Add(new IndicatorFactor(IndicatorFactor.Limite, "", "", IndicatorScale.ToGame(d, v.Valor) - (g + points), v.Valor - free));
            }

            v.UltimaVariacao = new IndicatorVariation { De = state.UltimoPasso, Ate = week, Antes = before, Depois = v.Valor };
            v.UltimaVariacao.Fatores.AddRange(explained);
            Accumulate(v, week, before, explained);

            // A referência de comparação anda só pela tendência do modelo e pelo ciclo: não tem prefeito nem choque.
            double r = IndicatorScale.ToGame(d, v.Referencia);
            double r0 = IndicatorScale.ToGame(d, v.ReferenciaPartida);
            double next = r + (speed * (r0 + (e.TendenciaAnual * trendShape) + cycle - r));
            var (rMin, rMax) = IndicatorScale.Bounds(d, v.ReferenciaPartida);
            v.Referencia = Math.Clamp(IndicatorScale.FromGame(d, next), rMin, rMax);
        }

        state.Pressoes.RemoveAll(p => p.Ate is { } until && until < week);
        state.UltimoPasso = week;
    }

    private static void Accumulate(IndicatorValue v, DateOnly date, double before, List<IndicatorFactor> factors)
    {
        v.VariacaoNoAno ??= new IndicatorVariation { De = date, Ate = date, Antes = before, Depois = before };
        v.VariacaoNoAno.Ate = date;
        v.VariacaoNoAno.Depois = v.Valor;
        foreach (IndicatorFactor f in factors)
        {
            v.VariacaoNoAno.Add(f);
        }
    }
}
