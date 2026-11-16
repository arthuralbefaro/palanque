using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Demography;
using Planalto.Core.Economy;
using Planalto.Core.Elections;
using Planalto.Core.Municipios;
using Planalto.Core.State;
using Calendar = Planalto.Core.State.Calendar;

namespace Planalto.Core.Renewal;

/// <summary>Tudo o que a renovação precisa, derivado do estado e do conteúdo.</summary>
public sealed class RenewalContext
{
    public required GameState State { get; init; }

    public required Municipio Municipio { get; init; }

    public required PeopleContent People { get; init; }

    public required RenewalContent Content { get; init; }

    /// <summary>Regras de substituição (lei); o padrão é o do GDD 17.16 e 17.20, item 12.</summary>
    public SuccessionRulesConfig Rules { get; init; } = SuccessionRulesConfig.Default;

    public CandidacyRulesConfig Candidacy { get; init; } = CandidacyRulesConfig.Default;

    /// <summary>Projeção da população: dá as vagas de vereador de cada eleição; sem ela, valem as atuais.</summary>
    public PopulationProjection? Projection { get; init; }

    /// <summary>Ids dos temas, para o interesse de quem ganha ficha de pessoa; pode ficar vazio.</summary>
    public IReadOnlyList<string> Themes { get; init; } = [];

    /// <summary>Catálogo da equipe: com ele, quem sai da equipe dá lugar a gente nova; sem ele, só há saídas.</summary>
    public StaffCatalog? Staff { get; init; }

    /// <summary>Cadeiras por partido na Câmara de partida, quando não há eleição jogada (ex.: o cenário do mundo).</summary>
    public IReadOnlyDictionary<string, int>? InitialSeats { get; init; }

    /// <summary>Partido do prefeito de partida, quando não há eleição jogada.</summary>
    public string? InitialMayorParty { get; init; }

    public RenewalConfig Config => Content.Config;
}

/// <summary>
/// Motor da renovação do mundo político no município (GDD 17.16): envelhecimento e mortalidade pela tábua da UF,
/// saídas e entradas a cada eleição e entre eleições, luto, e substituição pela regra de <see cref="SuccessionRulesConfig"/>.
/// Anda sozinho, semana a semana, com o jogador ocupando ou não um cargo. Toda aleatoriedade vem do gerador do
/// próprio estado da renovação.
/// </summary>
public sealed partial class RenewalEngine
{
    private readonly Dictionary<string, Character> _npcs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PersonStatus> _status = new(StringComparer.Ordinal);
    private int _npcCount = -1;
    private int _statusCount = -1;

    public RenewalEngine(RenewalContext ctx)
    {
        Context = ctx;
        ctx.Rules.Validate();
    }

    public RenewalContext Context { get; }

    public RenewalState Renewal => State.Renewal ?? throw new InvalidOperationException("Sem estado de renovação.");

    private GameState State => Context.State;

    private RenewalConfig Config => Context.Config;

    private SuccessionRulesConfig Rules => Context.Rules;

    private Municipio Municipio => Context.Municipio;

    private Rng Rng => Renewal.Rng;

    private CityState? City => State.City?.Municipio == Municipio.CodigoIbge ? State.City : null;

    // ---------------------------------------------------------------- tempo

    /// <summary>Cria o estado da renovação se o save ainda não tem (ou é de outro município) e devolve-o.</summary>
    public RenewalState Ensure()
    {
        if (State.Renewal?.Municipio != Municipio.CodigoIbge)
        {
            State.Renewal = Create();
        }

        return State.Renewal;
    }

    /// <summary>
    /// Leva a renovação até a semana de <paramref name="date"/>, uma semana por vez (inclusive as que o calendário
    /// saltou). Chamar duas vezes para a mesma semana não anda nada.
    /// </summary>
    public void AdvanceTo(DateOnly date)
    {
        RenewalState r = Ensure();
        DateOnly target = Calendar.ForDate(date).WeekStart;
        while (r.UltimoPasso < target)
        {
            StepWeek(r.UltimoPasso.AddDays(7));
        }
    }

    private void StepWeek(DateOnly week)
    {
        RenewalState r = Renewal;
        if (week > r.MandatoFim)
        {
            Inaugurate(r.MandatoFim.AddDays(1));
        }

        DetectPlayerExit(week);
        int month = (week.Year * 12) + week.Month;
        if (month != r.UltimoMes)
        {
            r.UltimoMes = month;
            MonthlyDraws(week);
        }

        foreach (PendingElection election in r.EleicoesPendentes.Where(e => e.Data <= week.AddDays(6)).OrderBy(e => e.Data).ThenBy(e => e.Id).ToList())
        {
            r.EleicoesPendentes.Remove(election);
            ResolveElection(election);
        }

        if (Calendar.ElectionKindOf(week.Year) == ElectionKind.Municipal && r.UltimaEleicaoAno < week.Year
            && Calendar.ElectionFor(week.Year).FirstRound <= week.AddDays(6))
        {
            // Com o jogador em campanha, a eleição é a jogada: o resultado chega por OnElectionConcluded ou, se ninguém
            // o entregou, é lido da política local gravada na apuração.
            DateOnly electionDay = Calendar.ElectionFor(week.Year).FirstRound;
            if (State.Local is { } local && local.Municipio == Municipio.CodigoIbge && local.ElectionYear == week.Year)
            {
                r.Eleitos = SlateFromLocal(local, null, electionDay);
                r.UltimaEleicaoAno = week.Year;
            }
            else if (State.Campaign?.Municipio != Municipio.CodigoIbge)
            {
                RunAutomaticElection(electionDay, week.Year);
            }
        }

        if (r.Historico.Count > Config.HistoricoMaximo)
        {
            r.Historico.RemoveRange(0, r.Historico.Count - Config.HistoricoMaximo);
        }

        r.UltimoPasso = week;
    }

    // ---------------------------------------------------------------- sorteio mensal: mortalidade e saídas

    /// <summary>
    /// Uma vez por mês, para cada pessoa acompanhada: a saúde cai com a idade, a morte é sorteada pela tábua da UF, do
    /// sexo e da idade vezes o fator pessoal e, para quem segue vivo, as saídas possíveis do papel. Quando mais de uma
    /// saída ocorre no mesmo mês, vale uma só, sorteada. O jogador não entra: segue as regras próprias de saúde e idade.
    /// </summary>
    private void MonthlyDraws(DateOnly date)
    {
        RenewalState r = Renewal;
        AnnualExitsConfig exits = Config.SaidasAnuais;
        foreach (var (id, role) in Tracked())
        {
            if (!IsActive(id) || !HasRole(id, role) || Npc(id) is not { } npc)
            {
                continue;
            }

            PersonStatus status = Status(id);
            int age = npc.AgeOn(date);
            if (date.Month == 1 && age > Config.Saude.IdadeDaPerda)
            {
                status.Saude = Math.Max(0, status.Saude - Config.Saude.PerdaAnual);
            }

            MortalityChance chance = Mortality.Chance(
                Context.Content.Mortality, Context.Content.Demography, r.Uf, npc.Gender, age, (int)Math.Round(status.Saude), Config.EstressePorPapel[RoleKey(role)]);
            status.TabuaUsada = chance.Tabua;
            if (Mortality.Sorteia(Rng, chance))
            {
                Die(id, date);
                continue;
            }

            var triggered = new List<Action>();
            switch (role)
            {
                case CivicRole.Vereador:
                    if (status.Saude < Config.Saude.LimiteRenuncia || Rng.Chance(Monthly(exits.RenunciaVereador)))
                    {
                        triggered.Add(() => VacateSeat(id, VacancyCause.Renuncia, date));
                    }

                    if (Rng.Chance(Monthly(exits.CassacaoVereador)))
                    {
                        triggered.Add(() => VacateSeat(id, VacancyCause.Cassacao, date));
                    }

                    break;
                case CivicRole.Prefeito:
                    if (status.Saude < Config.Saude.LimiteRenuncia || Rng.Chance(Monthly(exits.RenunciaPrefeito)))
                    {
                        triggered.Add(() => VacateExecutive(VacancyCause.Renuncia, date));
                    }

                    if (Rng.Chance(Monthly(exits.CassacaoPrefeito)))
                    {
                        triggered.Add(() => VacateExecutive(VacancyCause.Cassacao, date));
                    }

                    if (Rng.Chance(Monthly(exits.JusticaEleitoralPrefeito)))
                    {
                        triggered.Add(() => VacateExecutive(VacancyCause.JusticaEleitoral, date));
                    }

                    break;
                case CivicRole.Lideranca:
                    if (age >= Config.Lideranca.IdadeAposentadoria && Rng.Chance(Monthly(Config.Lideranca.ChanceAposentadoriaAnual)))
                    {
                        triggered.Add(() => LeaderLeaves(id, ExitKind.Aposentadoria, date));
                    }

                    if (Rng.Chance(Monthly(exits.MudancaLideranca)))
                    {
                        triggered.Add(() => LeaderLeaves(id, ExitKind.Mudanca, date));
                    }

                    break;
                case CivicRole.Equipe:
                    // Quem está contratado numa campanha em curso não some da cidade no meio dela.
                    if (!(State.Campaign is not null && City?.Staff.Any(s => s.PersonId == id && s.Hired) == true) && Rng.Chance(Monthly(exits.MudancaEquipe)))
                    {
                        triggered.Add(() => StaffLeaves(id, ExitKind.Mudanca, date));
                    }

                    break;
            }

            if (triggered.Count > 0)
            {
                triggered[triggered.Count == 1 ? 0 : Rng.NextInt(triggered.Count)]();
            }
        }

        FillLeaderVacancies(date);
        FillStaffVacancies(date);
    }

    /// <summary>Chance mensal equivalente à anual.</summary>
    private static double Monthly(double annual) => Mortality.Mensal(annual);

    /// <summary>Pessoas acompanhadas, com o papel: cadeiras, prefeito, vice, suplentes, lideranças e equipe, nessa ordem.</summary>
    private List<(string Id, CivicRole Role)> Tracked()
    {
        RenewalState r = Renewal;
        var list = new List<(string, CivicRole)>();
        list.AddRange(r.Cadeiras.Where(c => c.TitularId is not null).Select(c => (c.TitularId!, CivicRole.Vereador)));
        if (r.PrefeitoId is not null)
        {
            list.Add((r.PrefeitoId, CivicRole.Prefeito));
        }

        if (r.ViceId is not null)
        {
            list.Add((r.ViceId, CivicRole.VicePrefeito));
        }

        foreach (var (_, ids) in r.Suplentes.OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            list.AddRange(ids.Select(id => (id, CivicRole.Suplente)));
        }

        if (City is { } city)
        {
            list.AddRange(city.Leaders.Select(l => (l.PersonId, CivicRole.Lideranca)));
            list.AddRange(city.Staff.Where(s => !s.Left).Select(s => (s.PersonId, CivicRole.Equipe)));
        }

        return list.Where(x => x.Item1 != RaceSetup.PlayerId).ToList();
    }

    private bool HasRole(string id, CivicRole role)
    {
        RenewalState r = Renewal;
        return role switch
        {
            CivicRole.Vereador => r.SeatOf(id) is not null,
            CivicRole.Prefeito => r.PrefeitoId == id,
            CivicRole.VicePrefeito => r.ViceId == id,
            CivicRole.Suplente => r.Suplentes.Values.Any(l => l.Contains(id)),
            CivicRole.Lideranca => City?.Leader(id) is not null,
            _ => City?.Staff.Any(s => s.PersonId == id && !s.Left) == true,
        };
    }

    // ---------------------------------------------------------------- utilitários

    internal static string RoleKey(CivicRole role) => role switch
    {
        CivicRole.Vereador => "vereador",
        CivicRole.Prefeito => "prefeito",
        CivicRole.VicePrefeito => "vice_prefeito",
        CivicRole.Suplente => "suplente",
        CivicRole.Lideranca => "lideranca",
        _ => "equipe",
    };

    private Character? Npc(string id)
    {
        if (id == RaceSetup.PlayerId)
        {
            return State.Player;
        }

        if (_npcCount != State.Npcs.Count)
        {
            _npcs.Clear();
            foreach (Character c in State.Npcs)
            {
                _npcs[c.Id] = c;
            }

            _npcCount = State.Npcs.Count;
        }

        return _npcs.GetValueOrDefault(id);
    }

    /// <summary>Estado da pessoa na renovação; quem ainda não tem ganha um, com a saúde sorteada.</summary>
    private PersonStatus Status(string id, string? origin = null, string? relativeOf = null)
    {
        RenewalState r = Renewal;
        if (_statusCount != r.Pessoas.Count)
        {
            _status.Clear();
            foreach (PersonStatus p in r.Pessoas)
            {
                _status[p.Id] = p;
            }

            _statusCount = r.Pessoas.Count;
        }

        if (_status.TryGetValue(id, out PersonStatus? status))
        {
            return status;
        }

        status = new PersonStatus
        {
            Id = id,
            Saude = Rng.NextInt(Config.Saude.Inicial.Min, Config.Saude.Inicial.Max + 1),
            Origem = origin,
            ParenteDe = relativeOf,
        };
        r.Pessoas.Add(status);
        _status[id] = status;
        _statusCount = r.Pessoas.Count;
        return status;
    }

    private bool IsActive(string id) => id == RaceSetup.PlayerId || Status(id).Saida is null;

    private string NameOf(string? id) => id is null ? "" : Npc(id)?.BallotName ?? id;

    private string Sigla(string? partyId) => State.Party(partyId)?.Sigla ?? partyId ?? "";

    private string NewNpcId() => $"npc_{Municipio.CodigoIbge}_r{Renewal.NextSerial++}";

    private double LogNormal(double sigma) => Math.Exp(sigma * EconomyCycle.Normal(Rng));

    private RenewalEvent Record(
        DateOnly date, RenewalEventKind kind, string textKey, string? personId, CivicRole? role = null, VacancyCause? cause = null,
        SuccessionOutcome outcome = SuccessionOutcome.Nenhum, string? rule = null, int? seat = null, string? successorId = null,
        DateOnly? electionOn = null, string? partyId = null, params (string Key, string Value)[] extra)
    {
        RenewalState r = Renewal;
        Character? npc = personId is null ? null : Npc(personId);
        var values = new List<(string, string)>
        {
            ("nome", NameOf(personId)),
            ("partido", Sigla(partyId ?? npc?.PartyId)),
            ("idade", npc is null ? "" : npc.AgeOn(date).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("dias", Config.Luto.DiasOficiais.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("antecessor", NameOf(personId)),
            ("data", electionOn is { } d ? Texts.Date(d) : ""),
        };
        if (successorId is not null)
        {
            // Nos textos de posse, {nome} é quem assume e {antecessor} é quem saiu.
            values[0] = ("nome", NameOf(successorId));
            values[1] = ("partido", Sigla(partyId ?? Npc(successorId)?.PartyId));
        }

        values.InsertRange(0, extra);
        var e = new RenewalEvent
        {
            Id = r.NextEventId++,
            Data = date,
            Tipo = kind,
            Papel = role,
            PessoaId = personId,
            SucessorId = successorId,
            Causa = cause,
            Desfecho = outcome,
            Regra = rule,
            Cadeira = seat,
            EleicaoEm = electionOn,
            Texto = Texts.Fill(Config.Texto(textKey), [.. values]),
        };
        r.Historico.Add(e);
        var item = new FeedItem(date, "renovacao_" + textKey, e.Texto, null, successorId ?? personId);
        r.Noticias.Add(item);
        if (r.Noticias.Count > Config.NoticiasMaximo)
        {
            r.Noticias.RemoveRange(0, r.Noticias.Count - Config.NoticiasMaximo);
        }

        // Com campanha em curso, a notícia também vai para o feed da tela.
        if (State.Campaign?.Municipio == Municipio.CodigoIbge)
        {
            State.Campaign.Agenda.AddFeed(item);
        }

        return e;
    }
}
