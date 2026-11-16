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

    /// <summary>Leva o mundo até a semana do calendário: primeiro os indicadores, depois a política.</summary>
    public void AdvanceTo(DateOnly date)
    {
        if (Indicators is not null && State.Indicators is { } indicators && indicators.Municipio == Indicators.Municipio.CodigoIbge)
        {
            Indicators.AdvanceTo(indicators, date);
        }

        Renewal?.AdvanceTo(date);
    }
}
