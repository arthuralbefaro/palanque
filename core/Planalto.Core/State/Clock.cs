using Planalto.Core.Data;

namespace Planalto.Core.State;

public enum Turno
{
    Manha,
    Tarde,
    Noite,
}

/// <summary>Uma regra de pausa automática (data/time.json).</summary>
public sealed class PauseRule
{
    public required string Id { get; init; }

    public required string Nome { get; init; }

    /// <summary>Pausa por padrão.</summary>
    public required bool Pausa { get; init; }

    /// <summary>O jogador pode desligar nas Opções.</summary>
    public required bool Desligavel { get; init; }

    /// <summary>
    /// Para as Opções: na desligável, o que acontece quando ela está desligada (onde o acontecimento continua à vista);
    /// na que não se desliga, por que ela é fixa.
    /// </summary>
    public required string Explicacao { get; init; }
}

/// <summary>Ritmo do tempo e pausas automáticas (GDD 17.4, data/time.json).</summary>
public sealed class TimeConfig : IValidatable
{
    public const string File = "time.json";

    public required string Note { get; init; }

    public required List<int> Velocidades { get; init; }

    public required Dictionary<string, List<double>> SegundosPorDia { get; init; }

    public required List<PauseRule> Pausas { get; init; }

    public PauseRule Rule(string id) => Pausas.First(p => p.Id == id);

    public IEnumerable<string> Validate()
    {
        if (Velocidades.Count == 0 || Velocidades.Any(v => v <= 0))
        {
            yield return "velocidades: lista de inteiros positivos.";
        }

        foreach (string mode in new[] { Clock.ModoCampanha, Clock.ModoMandato })
        {
            if (!SegundosPorDia.TryGetValue(mode, out List<double>? seconds) || seconds.Count != Velocidades.Count || seconds.Any(s => s <= 0))
            {
                yield return $"segundos_por_dia.{mode}: um valor positivo por velocidade.";
            }
        }

        foreach (string error in DataChecks.UniqueIds(Pausas.Select(p => p.Id), "Pausa"))
        {
            yield return error;
        }

        foreach (string id in Clock.PauseIds.Where(id => Pausas.All(p => p.Id != id)))
        {
            yield return $"pausas: falta '{id}'.";
        }

        foreach (PauseRule rule in Pausas.Where(p => !p.Desligavel && !p.Pausa))
        {
            yield return $"pausa '{rule.Id}': não desligável precisa pausar.";
        }

        foreach (PauseRule rule in Pausas.Where(p => string.IsNullOrWhiteSpace(p.Nome) || string.IsNullOrWhiteSpace(p.Explicacao)))
        {
            yield return $"pausa '{rule.Id}': precisa de nome e de explicação para a tela de Opções.";
        }
    }
}

/// <summary>Estado do relógio: dia da semana (0 = segunda) dentro da semana do calendário.</summary>
public sealed class TimeState
{
    /// <summary>0 = segunda-feira, 6 = domingo.</summary>
    public int Day { get; set; }
}

/// <summary>
/// Tempo corrente (GDD 17.4): o dia é a unidade; a simulação pesada continua semanal e roda na passagem de domingo
/// para segunda. Sete dias seguidos produzem exatamente o mesmo estado que uma semana do motor antigo.
/// </summary>
public static class Clock
{
    public const string ModoCampanha = "campanha";
    public const string ModoMandato = "mandato";

    public const string DecisaoImediata = "decisao_imediata";
    public const string PesquisaPublicada = "pesquisa_publicada";
    public const string VotacaoNaPauta = "votacao_na_pauta";
    public const string CaixaBaixo = "caixa_baixo";
    public const string SemanaSemCompromisso = "semana_sem_compromisso";
    public const string FaseNova = "fase_nova";
    public const string NoticiaComum = "noticia_comum";

    /// <summary>Uma liderança que apoiava o jogador mudou de lado ou ficou neutra (GDD 17.4).</summary>
    public const string LiderancaPerdida = "lideranca_perdida";

    /// <summary>Um rival foi visto numa zona em que o jogador lidera a presença (GDD 17.4).</summary>
    public const string RivalEmZonaLider = "rival_em_zona_lider";

    /// <summary>Um ataque contra o jogador saiu e o prazo de resposta começou a correr (GDD 17.4 e 17.13).</summary>
    public const string AtaqueContraVoce = "ataque";

    /// <summary>Chegou uma decisão com prazo (oferta de doação): pausa ao chegar; o prazo corre se o jogador retomar (GDD 17.4).</summary>
    public const string DecisaoComPrazo = "decisao_com_prazo";

    /// <summary>Mandato: a fiscalização trouxe um achado que espera decisão (GDD 17.14).</summary>
    public const string AchadoFiscalizacao = "achado_fiscalizacao";

    /// <summary>Prefeitura: abriu a data-base dos servidores, com pedido de reajuste na mesa (GDD 17.15).</summary>
    public const string MesaNegociacao = "mesa_negociacao";

    /// <summary>Mandato e prefeitura: alguém cobra uma promessa ou dívida do Livro de compromissos (GDD 17.2).</summary>
    public const string CobrancaDoLivro = "cobranca_do_livro";

    /// <summary>Mandato: luto oficial sem sessão da Câmara (GDD 17.16).</summary>
    public const string LutoOficial = "luto_oficial";

    /// <summary>Mandato e prefeitura: uma obra sua ficou pronta para a entrega (GDD 17.14 e 17.15).</summary>
    public const string ObraPronta = "obra_pronta";

    public static readonly IReadOnlyList<string> PauseIds =
    [
        DecisaoImediata, PesquisaPublicada, VotacaoNaPauta, CaixaBaixo, SemanaSemCompromisso, FaseNova, NoticiaComum, LiderancaPerdida, RivalEmZonaLider, AtaqueContraVoce, DecisaoComPrazo,
        AchadoFiscalizacao, MesaNegociacao, CobrancaDoLivro, LutoOficial, ObraPronta,
    ];

    public static DateOnly Today(GameState state) => state.Calendar.WeekStart.AddDays(state.Time.Day);

    /// <summary>
    /// Avança um dia. No domingo, chama <paramref name="runWeek"/> (a simulação semanal, que anda o calendário) e
    /// volta para segunda; devolve true quando a semana virou.
    /// </summary>
    public static bool AdvanceDay(GameState state, Action runWeek)
    {
        if (state.Time.Day < 6)
        {
            state.Time.Day++;
            return false;
        }

        DateOnly before = state.Calendar.WeekStart;
        runWeek();
        state.Time.Day = 0;
        if (state.Calendar.WeekStart == before)
        {
            throw new InvalidOperationException("A simulação semanal não andou o calendário.");
        }

        return true;
    }

    /// <summary>Volta para segunda-feira (saltos de calendário, como pular até a próxima eleição).</summary>
    public static void ResetDay(GameState state) => state.Time.Day = 0;

    public static double SecondsPerDay(TimeConfig config, string mode, int speed)
    {
        int index = Math.Max(0, config.Velocidades.IndexOf(speed));
        return config.SegundosPorDia[mode][index];
    }

    /// <summary>Pausa automática para um tipo de acontecimento, com as escolhas do jogador (só nas desligáveis).</summary>
    public static bool ShouldPause(TimeConfig config, string pauseId, IReadOnlyDictionary<string, bool>? overrides)
    {
        PauseRule rule = config.Rule(pauseId);
        return !rule.Desligavel || (overrides is not null && overrides.TryGetValue(pauseId, out bool value) ? value : rule.Pausa);
    }
}
