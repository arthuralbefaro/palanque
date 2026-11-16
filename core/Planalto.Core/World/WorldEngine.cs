using Planalto.Core.Indicators;
using Planalto.Core.Municipios;
using Planalto.Core.Renewal;
using Planalto.Core.State;

namespace Planalto.Core.World;

/// <summary>
/// O mundo que anda sozinho no município em foco (GDD 17.1, princípio b): indicadores em unidade real (17.17) e
/// renovação do mundo político (17.16). Um só ponto de entrada por semana, que também recupera as semanas que o
/// calendário saltou; não depende de o jogador ter cargo nem de comando dele.
/// </summary>
public sealed class WorldEngine
{
    public WorldEngine(GameState state, IndicatorEvolution? indicators, RenewalEngine? renewal)
    {
        State = state;
        Indicators = indicators;
        Renewal = renewal;
    }

    public GameState State { get; }

    public IndicatorEvolution? Indicators { get; }

    public RenewalEngine? Renewal { get; }

    /// <summary>Cria no save os indicadores do município em foco, se ainda não houver (ou forem de outro município).</summary>
    public MunicipalIndicatorsState? EnsureIndicators(IndicadoresUfFile uf, MunicipiosDataSet municipios)
    {
        if (Indicators is null)
        {
            return State.Indicators;
        }

        Municipio m = Indicators.Municipio;
        if (State.Indicators?.Municipio != m.CodigoIbge)
        {
            State.Indicators = IndicatorSetup.Create(Indicators.Content, m, uf, municipios, State.Calendar.WeekStart, State.World?.Seed ?? State.Rng.Seed);
        }

        return State.Indicators;
    }

    /// <summary>Primeiro dia do mandato municipal em curso em <paramref name="date"/> (1º de janeiro depois da eleição).</summary>
    private static DateOnly TermStart(DateOnly date)
    {
        int year = date.Year;
        while (Calendar.ElectionKindOf(year - 1) != ElectionKind.Municipal)
        {
            year--;
        }

        return new DateOnly(year, 1, 1);
    }

    /// <summary>Leva o mundo até a semana do calendário: primeiro os indicadores, depois a política.</summary>
    public void AdvanceTo(DateOnly date)
    {
        if (Indicators is not null && State.Indicators is { } indicators && indicators.Municipio == Indicators.Municipio.CodigoIbge)
        {
            if (State.Executive is null)
            {
                // Sem o jogador na Prefeitura, o orçamento e as políticas do governo dele não seguem empurrando os indicadores.
                indicators.Pressoes.RemoveAll(p => p.Ate is null
                    && (p.Origem.StartsWith(Executive.ExecutiveEngine.BudgetOrigin, StringComparison.Ordinal) || p.Origem.StartsWith(Executive.ExecutiveEngine.PolicyOrigin, StringComparison.Ordinal)));
            }

            if (State.Governance?.Municipio != indicators.Municipio)
            {
                // Sem governo acompanhado de perto, não há secretário conhecido: a velocidade volta à do modelo.
                indicators.VelocidadeFator.Clear();
            }

            if (Renewal is null && State.Executive is null && TermStart(date) is var term && indicators.MandatoDesde < term)
            {
                // Posse sem o jogador na Prefeitura e sem a renovação ligada (que marcaria a posse): a variação que o
                // eleitor julga recomeça no primeiro dia do mandato.
                Indicators.AdvanceTo(indicators, term);
                IndicatorEvolution.MarkMandateStart(indicators, term);
            }

            Indicators.AdvanceTo(indicators, date);
        }

        Renewal?.AdvanceTo(date);
    }
}
