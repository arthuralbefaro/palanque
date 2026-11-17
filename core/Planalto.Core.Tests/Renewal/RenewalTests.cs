using Planalto.Core.Campaign;
using Planalto.Core.Career;
using Planalto.Core.Characters;
using Planalto.Core.Legislature;
using Planalto.Core.Municipios;
using Planalto.Core.People;
using Planalto.Core.Renewal;
using Planalto.Core.Save;
using Planalto.Core.Scenarios;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;
using Planalto.Core.Tests.Executive;
using Planalto.Core.Tests.Indicators;
using Planalto.Core.Tests.Legislature;
using Planalto.Core.Tests.Parties;
using Planalto.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace Planalto.Core.Tests.Renewal;

/// <summary>Conteúdo da renovação e montagem de mundos para os testes.</summary>
internal static class RenewalKit
{
    private static readonly Lazy<RenewalContent> LazyContent = new(() => RenewalContent.LoadOrThrow(TestContent.DataDirectory));

    public static RenewalContent Content => LazyContent.Value;

    public static RenewalContext Context(GameState state, SuccessionRulesConfig? rules = null)
    {
        Municipio m = CampaignKit.Municipio(state.FocusMunicipio);
        MunicipalScenario? scenario = state.World?.ScenarioId == "brasil_2026" ? CampaignKit.C.Brasil2026.Municipio(m.CodigoIbge) : null;
        return new RenewalContext
        {
            State = state,
            Municipio = m,
            People = CampaignKit.C.People,
            Content = Content,
            Rules = rules ?? SuccessionRulesConfig.Default,
            Projection = CampaignKit.C.Projection,
            Themes = CampaignKit.C.World.Parties.Themes.Themes.Select(t => t.Id).ToList(),
            Staff = CampaignKit.C.Campaign.Staff,
            InitialSeats = scenario?.Camara,
            InitialMayorParty = scenario?.Prefeito,
        };
    }

    /// <summary>Mundo com a cidade gerada (lideranças e equipe) e o jogador parado: sem campanha, sem cargo, sem comando.</summary>
    public static (GameState State, RenewalEngine Engine) Idle(int ibge, ulong seed, SuccessionRulesConfig? rules = null)
    {
        GameState state = CampaignKit.NewCareer(ibge, seed).Context.State;
        state.Campaign = null;
        var engine = new RenewalEngine(Context(state, rules));
        engine.Ensure();
        return (state, engine);
    }

    public static int Town(string porte, int skip) =>
        CampaignKit.C.Municipios.All.Where(m => m.EleicoesMunicipais && m.Porte == porte).OrderBy(m => m.CodigoIbge).Skip(skip).First().CodigoIbge;

    public static Character Npc(GameState state, string id) => state.Npcs.First(n => n.Id == id);
}

/// <summary>Renovação do mundo político no município (GDD 17.16) e substituição pela regra (GDD 17.20, item 12).</summary>
public class RenewalTests(ITestOutputHelper output)
{
    private static readonly SuccessionRulesConfig Rules = SuccessionRulesConfig.Default;

    // ---------------------------------------------------------------- 40 anos

    public static TheoryData<string, int, ulong> FortyYearCases() => new()
    {
        { "pequeno", 40, 1UL }, { "pequeno", 900, 2UL }, { "medio", 60, 3UL }, { "medio", 300, 4UL },
        { "grande", 20, 5UL }, { "grande", 150, 6UL }, { "metropole", 5, 7UL }, { "metropole", 30, 8UL },
    };

    private sealed record Run(string Nome, int Cadeiras, List<LegislatureRecord> Legislaturas, int Mortes, int MortesTitulares, int Vagas, int MaxDiasVaga, int Suplentes, int Vices, int Diretas, int Indiretas);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(string, int, ulong), Run> Runs = new();

    /// <summary>Simula 40 anos, semana a semana, sem nenhum comando, conferindo as regras a cada semana.</summary>
    private static Run Simulate(string porte, int skip, ulong seed) => Runs.GetOrAdd((porte, skip, seed), _ =>
    {
        var (state, engine) = RenewalKit.Idle(RenewalKit.Town(porte, skip), seed);
        RenewalState r = state.Renewal!;
        var world = new WorldEngine(state, null, engine);
        DateOnly end = state.Calendar.WeekStart.AddYears(40);
        var events = new List<RenewalEvent>();
        int lastEvent = 0, maxVacantDays = 0, vacancies = 0;
        while (state.Calendar.WeekStart < end)
        {
            CareerFlow.AdvanceWeek(state, null, null, world: world);
            DateOnly week = r.UltimoPasso;
            events.AddRange(r.Historico.Where(e => e.Id > lastEvent));
            lastEvent = r.NextEventId - 1;

            // A Câmara nunca fica com cadeira vaga por mais tempo do que a regra permite.
            foreach (Seat seat in r.Cadeiras.Where(c => c.TitularId is null))
            {
                Assert.NotNull(seat.VagaDesde);
                Assert.NotNull(seat.VagaPrazo);
                Assert.True(week <= seat.VagaPrazo.Value.AddDays(6), $"{week}: cadeira {seat.Numero} vaga desde {seat.VagaDesde} além do prazo {seat.VagaPrazo} ({seat.VagaDestino})");
                Assert.NotEqual(CouncilSuccession.SuplenteAssume, seat.VagaDestino);
                maxVacantDays = Math.Max(maxVacantDays, week.DayNumber - seat.VagaDesde.Value.DayNumber);
            }

            // Ninguém morto ou fora da política ocupa cargo, e ninguém ocupa duas cadeiras.
            var holders = r.Titulares.ToList();
            Assert.Equal(holders.Count, holders.Distinct().Count());
            Assert.All(holders, id => Assert.Null(r.Status(id)?.Saida));
            Assert.True(r.PrefeitoId is null || r.Status(r.PrefeitoId)?.Saida is null);
            Assert.True(r.ViceId is null || r.Status(r.ViceId)?.Saida is null);
            Assert.All(r.Suplentes.Values.SelectMany(l => l), id => Assert.Null(r.SeatOf(id)));

            // Prefeitura vaga só com o presidente da Câmara respondendo e eleição marcada, ou a menos do prazo do fim do mandato.
            if (r.PrefeitoId is null)
            {
                Assert.NotNull(r.ExecutivoVagoDesde);
                Assert.NotNull(r.PresidenteCamaraId);
                Assert.True(
                    r.EleicoesPendentes.Any(e => e.Cargo == CivicRole.Prefeito) || r.ExecutivoVagoDesde.Value.AddDays(Rules.PrazoEleicaoDiretaDias) > r.MandatoFim,
                    $"{week}: Prefeitura vaga desde {r.ExecutivoVagoDesde} sem eleição marcada");
            }
        }

        // Toda morte de titular resulta em posse de suplente ou de vice, ou em eleição, conforme a regra configurada.
        var deaths = events.Where(e => e.Tipo == RenewalEventKind.Morte).ToList();
        foreach (RenewalEvent death in deaths)
        {
            switch (death.Papel)
            {
                case CivicRole.Vereador:
                    Assert.Contains(death.Desfecho, new[] { SuccessionOutcome.SuplenteAssumiu, SuccessionOutcome.EleicaoParaACadeira, SuccessionOutcome.CadeiraVagaAteOFim });
                    Assert.Equal(Rules.BaseVereador.Artigo, death.Regra);
                    if (death.Desfecho == SuccessionOutcome.SuplenteAssumiu)
                    {
                        RenewalEvent posse = Assert.Single(events, e => e.Tipo == RenewalEventKind.PosseSuplente && e.Data == death.Data && e.Cadeira == death.Cadeira && e.PessoaId == death.PessoaId);
                        Assert.Equal(death.SucessorId, posse.SucessorId);
                        Assert.Equal(state.Npcs.First(n => n.Id == death.PessoaId).PartyId, state.Npcs.First(n => n.Id == posse.SucessorId).PartyId);
                    }

                    break;
                case CivicRole.Prefeito:
                    Assert.Contains(death.Desfecho, new[] { SuccessionOutcome.ViceAssumiu, SuccessionOutcome.EleicaoDireta, SuccessionOutcome.EleicaoIndireta, SuccessionOutcome.InterinoAteOFim });
                    Assert.Equal(Rules.BaseDuplaVacancia.Artigo, death.Regra);
                    break;
                case CivicRole.VicePrefeito:
                    Assert.Equal(SuccessionOutcome.ViceFicaVago, death.Desfecho);
                    break;
                default:
                    Assert.Equal(SuccessionOutcome.Nenhum, death.Desfecho);
                    break;
            }

            // Morte sempre com notícia sóbria, sem exclamação.
            Assert.DoesNotContain('!', death.Texto);
        }

        vacancies = events.Count(e => e.Papel == CivicRole.Vereador && e.Tipo is RenewalEventKind.Morte or RenewalEventKind.Renuncia or RenewalEventKind.Cassacao);
        return new Run(
            CampaignKit.Municipio(state.FocusMunicipio).Nome, r.Cadeiras.Count, r.Legislaturas.ToList(), deaths.Count,
            deaths.Count(d => d.Papel is CivicRole.Vereador or CivicRole.Prefeito or CivicRole.VicePrefeito), vacancies, maxVacantDays,
            events.Count(e => e.Tipo == RenewalEventKind.PosseSuplente), events.Count(e => e.Tipo == RenewalEventKind.PosseVice),
            events.Count(e => e.Tipo == RenewalEventKind.EleicaoResolvida && e.Desfecho == SuccessionOutcome.EleicaoDireta),
            events.Count(e => e.Tipo == RenewalEventKind.EleicaoResolvida && e.Desfecho == SuccessionOutcome.EleicaoIndireta));
    });

    [Theory]
    [MemberData(nameof(FortyYearCases))]
    public void Renovacao_municipal_em_40_anos_segue_a_regra_e_mantem_a_idade_media(string porte, int skip, ulong seed)
    {
        Run run = Simulate(porte, skip, seed);
        Assert.True(run.Legislaturas.Count >= 11, $"{run.Legislaturas.Count} legislaturas");
        LegislatureRecord first = run.Legislaturas[0], tenth = run.Legislaturas[9];
        output.WriteLine(
            $"{run.Nome} ({porte}, {run.Cadeiras} cadeiras, semente {seed}): idade média na 1ª legislatura {first.IdadeMedia:0.0}, na 10ª {tenth.IdadeMedia:0.0}; " +
            $"todas: {string.Join(" ", run.Legislaturas.Select(l => l.IdadeMedia.ToString("0.0")))}; renovação: {string.Join(" ", run.Legislaturas.Skip(1).Select(l => l.RenovacaoPct.ToString("0")))}%; " +
            $"mortes {run.Mortes} (de titulares {run.MortesTitulares}); vagas na Câmara {run.Vagas}; posses de suplente {run.Suplentes}; posses de vice {run.Vices}; " +
            $"eleições diretas {run.Diretas}, indiretas {run.Indiretas}; maior tempo de cadeira vaga {run.MaxDiasVaga} dias");
        Assert.All(run.Legislaturas, l => Assert.InRange(l.IdadeMedia, 30, 65));

        // Câmara com 13 cadeiras ou mais: a média de uma legislatura já é estável por si (decisão do autor, D-051).
        // Nas de 9 a 11, uma só troca mexe a média em mais de um ano: lá o critério é a média de 20 sementes (teste abaixo).
        if (run.Cadeiras >= 13)
        {
            Assert.True(Math.Abs(tenth.IdadeMedia - first.IdadeMedia) < 5, $"{run.Nome}: {first.IdadeMedia:0.0} → {tenth.IdadeMedia:0.0}");
        }
    }

    [Fact]
    public void Idade_media_dos_vereadores_varia_menos_de_5_anos_entre_a_1a_e_a_10a_legislatura()
    {
        var runs = FortyYearCases().Select(c => Simulate((string)c[0], (int)c[1], (ulong)c[2])).ToList();
        double Mean(int legislature) => runs.Sum(r => r.Legislaturas[legislature].IdadeMedia * r.Legislaturas[legislature].Cadeiras) / runs.Sum(r => r.Legislaturas[legislature].Cadeiras);
        double first = Mean(0), tenth = Mean(9);
        double renewal = runs.SelectMany(r => r.Legislaturas.Skip(1)).Average(l => l.RenovacaoPct);
        output.WriteLine($"Idade média dos vereadores em {runs.Count} municípios: 1ª legislatura {first:0.0}, 10ª {tenth:0.0} (diferença {tenth - first:+0.0;-0.0}).");
        output.WriteLine($"Por legislatura: {string.Join(" ", Enumerable.Range(0, 11).Select(i => Mean(i).ToString("0.0")))}");
        output.WriteLine($"Renovação média da Câmara por eleição (não reeleitos ÷ cadeiras; só informativo): {renewal:0}%");
        output.WriteLine($"Mortes: {runs.Sum(r => r.Mortes)}; de titulares: {runs.Sum(r => r.MortesTitulares)}; vagas na Câmara: {runs.Sum(r => r.Vagas)}; maior tempo de cadeira vaga: {runs.Max(r => r.MaxDiasVaga)} dias.");
        Assert.True(Math.Abs(tenth - first) < 5, $"{first:0.0} → {tenth:0.0}");
        Assert.True(runs.Sum(r => r.MortesTitulares) > 0, "nenhuma morte de titular em 40 anos em oito municípios");
    }

    /// <summary>
    /// Câmaras de 9 a 11 cadeiras (decisão do autor, D-051): o que se mede é a deriva da idade média, não o acaso
    /// de uma semente. O critério de menos de 5 anos entre a 1ª e a 10ª legislatura vale para a média de 20 sementes;
    /// uma semente isolada pode passar de 5 anos.
    /// </summary>
    [Fact]
    [Trait("Categoria", "Lento")]
    public void Idade_media_nas_Camaras_de_9_a_11_cadeiras_deriva_menos_de_5_anos_na_media_de_20_sementes()
    {
        const int Sementes = 20;
        var towns = FortyYearCases().Select(c => ((string)c[0], (int)c[1], (ulong)c[2]))
            .Where(c => Simulate(c.Item1, c.Item2, c.Item3).Cadeiras is >= 9 and <= 11)
            .Select(c => (Porte: c.Item1, Skip: c.Item2))
            .ToList();
        Assert.NotEmpty(towns);

        foreach (var (porte, skip) in towns)
        {
            var drifts = new double[Sementes];
            string name = "";
            int seats = 0;
            System.Threading.Tasks.Parallel.For(0, Sementes, i =>
            {
                Run run = Simulate(porte, skip, 1000UL + (ulong)i);
                drifts[i] = run.Legislaturas[9].IdadeMedia - run.Legislaturas[0].IdadeMedia;
                name = run.Nome;
                seats = run.Cadeiras;
            });
            double mean = drifts.Average();
            output.WriteLine($"{name} ({seats} cadeiras): deriva média em {Sementes} sementes {mean:+0.0;-0.0} anos; por semente: {string.Join(" ", drifts.Select(d => d.ToString("+0.0;-0.0")))}");
            Assert.True(Math.Abs(mean) < 5, $"{name}: deriva média de {mean:0.0} anos em {Sementes} sementes");
        }
    }

    // ---------------------------------------------------------------- regra, em objeto de configuração

    [Fact]
    public void Regras_de_substituicao_citam_o_artigo_e_dizem_o_que_nao_foi_conferido()
    {
        Assert.Contains("art. 224", Rules.BaseJusticaEleitoral.Artigo);
        Assert.Contains("§§ 3º e 4º", Rules.BaseJusticaEleitoral.Artigo);
        Assert.Contains("arts. 79 a 81", Rules.BaseDuplaVacancia.Artigo);
        Assert.Contains("art. 56", Rules.BaseVereador.Artigo);
        Assert.Contains("art. 112", Rules.BaseVereador.Artigo);

        // As três bases constam de docs/referencias/LEIS_CONFERIDAS.md (segunda leitura); a diferença do art. 113 fica dita.
        Assert.All(new[] { Rules.BaseVereador, Rules.BaseDuplaVacancia, Rules.BaseJusticaEleitoral }, b => Assert.True(b.Conferido));
        Assert.Contains("art. 113", Rules.BaseVereador.Nota);
        Assert.Contains("9 meses", Rules.BaseVereador.Nota);
        Assert.Contains("ADI 5525", Rules.BaseJusticaEleitoral.Nota);
        Assert.All(new[] { Rules.BaseVereador, Rules.BaseDuplaVacancia, Rules.BaseJusticaEleitoral }, b => Assert.False(string.IsNullOrWhiteSpace(b.Nota)));
        Assert.Equal(15, Rules.MesesParaEleicaoSemSuplente);
        Assert.Equal(6, Rules.MesesFinaisComEleicaoIndireta);
        Assert.Equal(0.5, Rules.FracaoDoMandatoComEleicaoDireta);
        Assert.Equal(90, Rules.PrazoEleicaoDiretaDias);
        Assert.Equal(30, Rules.PrazoEleicaoIndiretaDias);
        Assert.Throws<ArgumentOutOfRangeException>(() => new RenewalEngine(RenewalKit.Context(CampaignKit.NewCareer(RenewalKit.Town("pequeno", 40), 1).Context.State, Rules with { FracaoDoMandatoComEleicaoDireta = 2 })));
    }

    [Fact]
    public void Regra_da_cadeira_vaga_suplente_eleicao_ou_vaga_ate_o_fim()
    {
        var fim = new DateOnly(2032, 12, 31);
        Assert.Equal(CouncilSuccession.SuplenteAssume, Rules.Council(true, fim, new DateOnly(2032, 12, 1)));

        // Sem suplente: eleição só se faltarem mais de 15 meses (até 30/9/2031 faltam; em 1º/10/2031 faltam exatos 15).
        Assert.Equal(CouncilSuccession.NovaEleicao, Rules.Council(false, fim, new DateOnly(2031, 9, 30)));
        Assert.Equal(CouncilSuccession.VagaAteOFimDoMandato, Rules.Council(false, fim, new DateOnly(2031, 10, 1)));
        Assert.Equal(CouncilSuccession.VagaAteOFimDoMandato, Rules.Council(false, fim, new DateOnly(2032, 6, 1)));
        Assert.Equal(CouncilSuccession.NovaEleicao, (Rules with { MesesParaEleicaoSemSuplente = 3 }).Council(false, fim, new DateOnly(2032, 6, 1)));
    }

    [Theory]
    [InlineData(VacancyCause.Morte, true, "2029-03-10", ExecutiveSuccession.ViceAssume)]
    [InlineData(VacancyCause.Renuncia, true, "2032-11-10", ExecutiveSuccession.ViceAssume)]
    [InlineData(VacancyCause.Cassacao, true, "2031-05-10", ExecutiveSuccession.ViceAssume)]
    [InlineData(VacancyCause.Morte, false, "2029-03-10", ExecutiveSuccession.EleicaoDireta)]
    [InlineData(VacancyCause.Renuncia, false, "2030-12-31", ExecutiveSuccession.EleicaoDireta)]
    [InlineData(VacancyCause.Morte, false, "2031-01-01", ExecutiveSuccession.EleicaoIndireta)]
    [InlineData(VacancyCause.Renuncia, false, "2032-08-15", ExecutiveSuccession.EleicaoIndireta)]
    [InlineData(VacancyCause.JusticaEleitoral, true, "2029-03-10", ExecutiveSuccession.EleicaoDireta)]
    [InlineData(VacancyCause.JusticaEleitoral, true, "2031-06-10", ExecutiveSuccession.EleicaoDireta)]
    [InlineData(VacancyCause.JusticaEleitoral, false, "2032-06-30", ExecutiveSuccession.EleicaoDireta)]
    [InlineData(VacancyCause.JusticaEleitoral, true, "2032-07-01", ExecutiveSuccession.EleicaoIndireta)]
    [InlineData(VacancyCause.JusticaEleitoral, false, "2032-12-20", ExecutiveSuccession.EleicaoIndireta)]
    public void Regra_da_vacancia_no_executivo(VacancyCause cause, bool vice, string date, ExecutiveSuccession expected)
    {
        Assert.Equal(expected, Rules.Executive(cause, vice, new DateOnly(2029, 1, 1), new DateOnly(2032, 12, 31), DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Regra_do_executivo_e_configuravel()
    {
        var inicio = new DateOnly(2029, 1, 1);
        var fim = new DateOnly(2032, 12, 31);
        var meio = new DateOnly(2030, 6, 1);

        // Lei Orgânica que mande eleição indireta em qualquer momento, ou que não dê a Prefeitura ao vice.
        Assert.Equal(ExecutiveSuccession.EleicaoIndireta, (Rules with { FracaoDoMandatoComEleicaoDireta = 0 }).Executive(VacancyCause.Morte, false, inicio, fim, meio));
        Assert.Equal(ExecutiveSuccession.EleicaoDireta, (Rules with { ViceAssume = false }).Executive(VacancyCause.Morte, true, inicio, fim, meio));
        Assert.Equal(ExecutiveSuccession.EleicaoIndireta, (Rules with { MesesFinaisComEleicaoIndireta = 36 }).Executive(VacancyCause.JusticaEleitoral, true, inicio, fim, meio));
        Assert.Equal(new DateOnly(2031, 1, 1), Rules.EndOfDirectPeriod(inicio, fim));
        Assert.Equal(meio.AddDays(90), Rules.ElectionDate(ExecutiveSuccession.EleicaoDireta, meio));
        Assert.Equal(meio.AddDays(30), Rules.ElectionDate(ExecutiveSuccession.EleicaoIndireta, meio));
    }

    // ---------------------------------------------------------------- um teste para cada caminho de vacância no Executivo

    /// <summary>Mundo levado até a data, com prefeito e vice no cargo e sem eleição pendente.</summary>
    private static (GameState State, RenewalEngine Engine, RenewalState R) WorldAt(DateOnly date, string porte = "medio", Func<RenewalState, bool>? also = null)
    {
        for (ulong seed = 11; seed < 40; seed++)
        {
            var (state, engine) = RenewalKit.Idle(RenewalKit.Town(porte, 25), seed);
            state.Calendar.JumpTo(date);
            engine.AdvanceTo(date);
            RenewalState r = state.Renewal!;
            if (r.PrefeitoId is not null && r.ViceId is not null && r.EleicoesPendentes.Count == 0 && r.Cadeiras.All(c => c.TitularId is not null) && also?.Invoke(r) != false)
            {
                return (state, engine, r);
            }
        }

        throw new InvalidOperationException("Nenhuma semente chegou à data com prefeito, vice e Câmara completa.");
    }

    private static void Advance(GameState state, RenewalEngine engine, DateOnly date)
    {
        state.Calendar.JumpTo(date);
        engine.AdvanceTo(date);
    }

    [Fact]
    public void Morte_do_prefeito_da_posse_ao_vice()
    {
        var (state, engine, r) = WorldAt(new DateOnly(2029, 6, 4));
        string mayor = r.PrefeitoId!, vice = r.ViceId!;
        DateOnly date = r.UltimoPasso;
        engine.Die(mayor, date);

        Assert.Equal(vice, r.PrefeitoId);
        Assert.Null(r.ViceId);
        Assert.Null(r.ExecutivoVagoDesde);
        Assert.Empty(r.EleicoesPendentes);
        Assert.Equal(ExitKind.Morte, RenewalQueries.ExitOf(r, mayor));
        RenewalEvent death = Assert.Single(r.Historico, e => e.Tipo == RenewalEventKind.Morte && e.PessoaId == mayor);
        Assert.Equal(SuccessionOutcome.ViceAssumiu, death.Desfecho);
        Assert.Equal(vice, death.SucessorId);
        Assert.Equal(Rules.BaseDuplaVacancia.Artigo, death.Regra);
        Assert.Single(r.Historico, e => e.Tipo == RenewalEventKind.PosseVice && e.SucessorId == vice);
        Assert.True(RenewalQueries.InMourning(r, date.AddDays(3)));
        Assert.False(RenewalQueries.InMourning(r, date.AddDays(4)));
        Assert.Equal(vice, state.Local!.MayorId);
        Assert.Contains(RenewalKit.Npc(state, mayor).BallotName, death.Texto);
    }

    [Theory]
    [InlineData(VacancyCause.Morte)]
    [InlineData(VacancyCause.Renuncia)]
    public void Dupla_vacancia_na_primeira_metade_do_mandato_leva_a_eleicao_direta(VacancyCause cause)
    {
        var (state, engine, r) = WorldAt(new DateOnly(2030, 3, 4));
        string mayor = r.PrefeitoId!, vice = r.ViceId!;
        DateOnly date = r.UltimoPasso;
        RenewalEvent fact = Vacate(engine, r, cause, vice, mayor, date);

        Assert.Equal(SuccessionOutcome.EleicaoDireta, fact.Desfecho);
        Assert.Equal(date.AddDays(90), fact.EleicaoEm);
        Assert.Equal(Rules.BaseDuplaVacancia.Artigo, fact.Regra);
        PendingElection election = Assert.Single(r.EleicoesPendentes);
        Assert.False(election.Indireta);
        Assert.Equal(date.AddDays(90), election.Data);

        // Enquanto a eleição não vem, o presidente da Câmara responde pela Prefeitura, sem deixar a cadeira.
        Assert.Null(r.PrefeitoId);
        Assert.Equal(date, r.ExecutivoVagoDesde);
        ExecutiveView view = RenewalQueries.Executive(state);
        Assert.True(view.Vaga);
        Assert.Equal(r.PresidenteCamaraId, view.RespondeId);
        Assert.NotNull(r.SeatOf(r.PresidenteCamaraId!));
        Assert.Single(r.Historico, e => e.Tipo == RenewalEventKind.Interino && e.PessoaId == r.PresidenteCamaraId);

        // A simulação anda por semanas: a eleição se resolve na semana em que cai o dia marcado.
        Advance(state, engine, date.AddDays(83));
        Assert.Null(r.PrefeitoId);
        Advance(state, engine, date.AddDays(97));
        Assert.NotNull(r.PrefeitoId);
        Assert.NotNull(r.ViceId);
        Assert.NotEqual(mayor, r.PrefeitoId);
        Assert.Null(r.ExecutivoVagoDesde);
        Assert.Empty(r.EleicoesPendentes);
        RenewalEvent result = Assert.Single(r.Historico, e => e.Tipo == RenewalEventKind.EleicaoResolvida && e.Papel == CivicRole.Prefeito);
        Assert.Equal(SuccessionOutcome.EleicaoDireta, result.Desfecho);
        Assert.Equal(r.PrefeitoId, result.PessoaId);
        Assert.Equal(new DateOnly(2032, 12, 31), r.MandatoFim);
        Assert.True(RenewalKit.Npc(state, r.PrefeitoId!).AgeOn(result.Data) >= 21);
        Assert.All(r.Cadeiras, c => Assert.NotNull(c.TitularId));
    }

    [Theory]
    [InlineData(VacancyCause.Morte)]
    [InlineData(VacancyCause.Renuncia)]
    public void Dupla_vacancia_na_segunda_metade_do_mandato_leva_a_eleicao_indireta_pela_camara(VacancyCause cause)
    {
        var (state, engine, r) = WorldAt(new DateOnly(2031, 3, 3));
        string mayor = r.PrefeitoId!, vice = r.ViceId!;
        DateOnly date = r.UltimoPasso;
        int seats = r.Cadeiras.Count;
        RenewalEvent fact = Vacate(engine, r, cause, vice, mayor, date);

        Assert.Equal(SuccessionOutcome.EleicaoIndireta, fact.Desfecho);
        Assert.Equal(date.AddDays(30), fact.EleicaoEm);
        PendingElection election = Assert.Single(r.EleicoesPendentes);
        Assert.True(election.Indireta);

        Advance(state, engine, date.AddDays(35));
        RenewalEvent result = Assert.Single(r.Historico, e => e.Tipo == RenewalEventKind.EleicaoResolvida && e.Papel == CivicRole.Prefeito);
        Assert.Equal(SuccessionOutcome.EleicaoIndireta, result.Desfecho);
        Assert.Equal(r.PrefeitoId, result.PessoaId);

        // Quem a Câmara escolheu era vereador: deixou a cadeira, e o suplente do partido dele tomou posse.
        RenewalEvent left = Assert.Single(r.Historico, e => e.Tipo == RenewalEventKind.Investidura);
        Assert.Equal(r.PrefeitoId, left.PessoaId);
        Assert.Equal(SuccessionOutcome.SuplenteAssumiu, left.Desfecho);
        Assert.Null(r.SeatOf(r.PrefeitoId!));
        Assert.Null(RenewalQueries.ExitOf(r, r.PrefeitoId!));
        Assert.Equal(seats, r.Cadeiras.Count(c => c.TitularId is not null));
        Assert.Equal(new DateOnly(2032, 12, 31), r.MandatoFim);
    }

    private static RenewalEvent Vacate(RenewalEngine engine, RenewalState r, VacancyCause cause, string vice, string mayor, DateOnly date)
    {
        if (cause == VacancyCause.Morte)
        {
            engine.Die(vice, date);
            Assert.Equal(SuccessionOutcome.ViceFicaVago, Assert.Single(r.Historico, e => e.Tipo == RenewalEventKind.Morte && e.PessoaId == vice).Desfecho);
            engine.Die(mayor, date);
            return Assert.Single(r.Historico, e => e.Tipo == RenewalEventKind.Morte && e.PessoaId == mayor);
        }

        Assert.Equal(SuccessionOutcome.ViceFicaVago, engine.VacateVice(cause, date)!.Desfecho);
        return engine.VacateExecutive(cause, date)!;
    }

    [Fact]
    public void Vacancia_por_decisao_da_justica_eleitoral_leva_a_eleicao_direta_e_o_vice_nao_assume()
    {
        // Segunda metade do mandato, a mais de 6 meses do fim: pela regra da morte seria indireta; aqui é direta.
        var (state, engine, r) = WorldAt(new DateOnly(2031, 9, 1));
        string mayor = r.PrefeitoId!, vice = r.ViceId!;
        DateOnly date = r.UltimoPasso;
        RenewalEvent fact = engine.VacateExecutive(VacancyCause.JusticaEleitoral, date)!;

        Assert.Equal(SuccessionOutcome.EleicaoDireta, fact.Desfecho);
        Assert.Equal(Rules.BaseJusticaEleitoral.Artigo, fact.Regra);
        Assert.Null(r.PrefeitoId);
        Assert.Null(r.ViceId);
        Assert.Equal(ExitKind.JusticaEleitoral, RenewalQueries.ExitOf(r, mayor));
        Assert.Equal(ExitKind.JusticaEleitoral, RenewalQueries.ExitOf(r, vice));
        Assert.False(Assert.Single(r.EleicoesPendentes).Indireta);

        Advance(state, engine, date.AddDays(97));
        RenewalEvent result = Assert.Single(r.Historico, e => e.Tipo == RenewalEventKind.EleicaoResolvida && e.Papel == CivicRole.Prefeito);
        Assert.Equal(SuccessionOutcome.EleicaoDireta, result.Desfecho);
        Assert.Equal(Rules.BaseJusticaEleitoral.Artigo, result.Regra);
        Assert.NotNull(r.PrefeitoId);
        Assert.NotEqual(vice, r.PrefeitoId);
    }

    [Fact]
    public void Vacancia_por_decisao_da_justica_eleitoral_a_menos_de_6_meses_do_fim_leva_a_eleicao_indireta()
    {
        var (state, engine, r) = WorldAt(new DateOnly(2032, 7, 5));
        DateOnly date = r.UltimoPasso;
        RenewalEvent fact = engine.VacateExecutive(VacancyCause.JusticaEleitoral, date)!;

        Assert.Equal(SuccessionOutcome.EleicaoIndireta, fact.Desfecho);
        Assert.True(Assert.Single(r.EleicoesPendentes).Indireta);
        Assert.Null(r.ViceId);
        Advance(state, engine, date.AddDays(35));
        RenewalEvent result = Assert.Single(r.Historico, e => e.Tipo == RenewalEventKind.EleicaoResolvida && e.Papel == CivicRole.Prefeito);
        Assert.Equal(SuccessionOutcome.EleicaoIndireta, result.Desfecho);
        Assert.NotNull(r.PrefeitoId);

        // A eleição municipal de outubro segue normal, e em 1º de janeiro toma posse quem ela escolheu.
        Advance(state, engine, new DateOnly(2033, 1, 10));
        Assert.Equal(new DateOnly(2033, 1, 1), r.MandatoInicio);
        Assert.NotNull(r.PrefeitoId);
    }

    [Fact]
    public void Dupla_vacancia_a_dias_do_fim_do_mandato_fica_com_o_presidente_da_camara_ate_a_posse()
    {
        // Um mundo em que quem sai em dezembro não é quem venceu a eleição de outubro.
        var (state, engine, r) = WorldAt(new DateOnly(2032, 12, 13), also: x => x.Eleitos is { } e && e.PrefeitoId != x.PrefeitoId && e.PrefeitoId != x.ViceId);
        DateOnly date = r.UltimoPasso;
        engine.VacateVice(VacancyCause.Renuncia, date);
        RenewalEvent fact = engine.VacateExecutive(VacancyCause.Renuncia, date)!;
        Assert.Equal(SuccessionOutcome.InterinoAteOFim, fact.Desfecho);
        Assert.Empty(r.EleicoesPendentes);
        Assert.Equal(r.PresidenteCamaraId, RenewalQueries.Executive(state).RespondeId);
        Advance(state, engine, new DateOnly(2033, 1, 10));
        Assert.NotNull(r.PrefeitoId);
        Assert.Null(r.ExecutivoVagoDesde);
    }

    // ---------------------------------------------------------------- Câmara

    [Fact]
    public void Morte_renuncia_ou_cassacao_de_vereador_da_posse_ao_suplente_da_mesma_lista()
    {
        var (state, engine, r) = WorldAt(new DateOnly(2029, 6, 4));
        DateOnly date = r.UltimoPasso;
        foreach (VacancyCause cause in new[] { VacancyCause.Morte, VacancyCause.Renuncia, VacancyCause.Cassacao })
        {
            Seat seat = r.Cadeiras.First(c => RenewalQueries.Substitutes(r, c.PartyId).Count > 0 && c.TitularId != r.PresidenteCamaraId);
            string holder = seat.TitularId!;
            string expected = RenewalQueries.Substitutes(r, seat.PartyId)[0];
            RenewalEvent fact;
            if (cause == VacancyCause.Morte)
            {
                engine.Die(holder, date);
                fact = Assert.Single(r.Historico, e => e.Tipo == RenewalEventKind.Morte && e.PessoaId == holder);
            }
            else
            {
                fact = engine.VacateSeat(holder, cause, date)!;
            }

            Assert.Equal(SuccessionOutcome.SuplenteAssumiu, fact.Desfecho);
            Assert.Equal(expected, fact.SucessorId);
            Assert.Equal(expected, seat.TitularId);
            Assert.Equal(RenewalKit.Npc(state, holder).PartyId, RenewalKit.Npc(state, expected).PartyId);
            Assert.DoesNotContain(expected, RenewalQueries.Substitutes(r, seat.PartyId));
            Assert.Null(seat.VagaDesde);
            Assert.NotNull(RenewalQueries.ExitOf(r, holder));
            Assert.Contains(expected, state.Local!.Councillors);
            Assert.DoesNotContain(holder, state.Local.Councillors);
        }

        Assert.Null(engine.VacateSeat("ninguem", VacancyCause.Renuncia, date));
    }

    [Fact]
    public void Cadeira_sem_suplente_tem_eleicao_se_faltam_mais_de_15_meses_e_fica_vaga_se_faltam_menos()
    {
        var (state, engine, r) = WorldAt(new DateOnly(2029, 6, 4));
        DateOnly date = r.UltimoPasso;
        Seat seat = r.Cadeiras[0];
        r.Suplentes[seat.PartyId] = [];
        string holder = seat.TitularId!;
        engine.Die(holder, date);
        RenewalEvent death = Assert.Single(r.Historico, e => e.Tipo == RenewalEventKind.Morte && e.PessoaId == holder);
        Assert.Equal(SuccessionOutcome.EleicaoParaACadeira, death.Desfecho);
        Assert.Null(seat.TitularId);
        Assert.Equal(CouncilSuccession.NovaEleicao, seat.VagaDestino);
        Assert.Equal(date.AddDays(RenewalKit.Content.Config.Prazos.EleicaoCadeiraDias), seat.VagaPrazo);
        Assert.True(RenewalQueries.Chamber(state, date).Single(v => v.Numero == seat.Numero).Vaga);
        Advance(state, engine, seat.VagaPrazo!.Value.AddDays(7));
        Assert.NotNull(seat.TitularId);
        Assert.Single(r.Historico, e => e.Tipo == RenewalEventKind.EleicaoResolvida && e.Cadeira == seat.Numero);

        // A menos de 15 meses do fim, sem suplente: a cadeira fica vaga até a próxima legislatura, que nasce completa.
        Advance(state, engine, new DateOnly(2032, 2, 2));
        Seat other = r.Cadeiras.First(c => c.TitularId is not null);
        r.Suplentes[other.PartyId] = [];
        RenewalEvent fact = engine.VacateSeat(other.TitularId!, VacancyCause.Renuncia, r.UltimoPasso)!;
        Assert.Equal(SuccessionOutcome.CadeiraVagaAteOFim, fact.Desfecho);
        Assert.Equal(r.MandatoFim, other.VagaPrazo);
        int seats = r.Cadeiras.Count;
        Advance(state, engine, new DateOnly(2032, 12, 20));
        Assert.Null(other.TitularId);
        Advance(state, engine, new DateOnly(2033, 1, 10));
        Assert.All(r.Cadeiras, c => Assert.NotNull(c.TitularId));
        Assert.True(r.Cadeiras.Count >= 1 && r.Cadeiras.Count <= seats);
    }

    // ---------------------------------------------------------------- luto, lideranças e equipe

    [Fact]
    public void Morte_deixa_lembranca_em_quem_tinha_vinculo_e_noticia_no_feed()
    {
        var (state, engine, r) = WorldAt(new DateOnly(2029, 6, 4), "grande");
        DateOnly date = r.UltimoPasso;
        Seat seat = r.Cadeiras.GroupBy(c => c.PartyId).OrderByDescending(g => g.Count()).First().First();
        string holder = seat.TitularId!;
        var colleagues = r.Cadeiras.Where(c => c.PartyId == seat.PartyId && c.TitularId != holder).Select(c => c.TitularId!).ToList();
        Assert.NotEmpty(colleagues);
        engine.Die(holder, date);

        RenewalEvent death = Assert.Single(r.Historico, e => e.Tipo == RenewalEventKind.Morte && e.PessoaId == holder);
        Assert.All(colleagues, id => Assert.Contains(id, death.Enlutados));
        foreach (string id in colleagues)
        {
            Memory memory = Assert.Single(state.City!.Person(id)!.Memories, m => m.Fact == "luto");
            Assert.Equal(date, memory.Date);
            Assert.Equal(0, memory.Pontos);
            Assert.Contains(RenewalKit.Npc(state, holder).BallotName, memory.Text);
        }

        FeedItem news = Assert.Single(r.Noticias, n => n.Kind == "renovacao_morte_vereador");
        Assert.Equal(death.Texto, news.Text);
        Assert.Contains("luto oficial de 3 dias", news.Text);
        Assert.Contains(RenewalKit.Npc(state, holder).AgeOn(date).ToString(System.Globalization.CultureInfo.InvariantCulture) + " anos", news.Text);
        Assert.DoesNotContain("{", news.Text);
        Assert.True(RenewalQueries.InMourning(r, date));

        // Morto não morre duas vezes nem volta a ocupar nada.
        engine.Die(holder, date);
        Assert.Single(r.Historico, e => e.Tipo == RenewalEventKind.Morte && e.PessoaId == holder);
    }

    [Fact]
    public void Liderancas_e_equipe_tambem_saem_e_dao_lugar_a_gente_nova()
    {
        var (state, engine) = RenewalKit.Idle(RenewalKit.Town("medio", 60), 21);
        RenewalState r = state.Renewal!;
        CityState city = state.City!;
        int leaders = city.Leaders.Count;
        Assert.True(leaders > 0 && city.Staff.Count > 0);
        Leader leader = city.Leaders.OrderByDescending(l => l.Rivals.Count).First();
        string leaderId = leader.PersonId;
        var rivals = leader.Rivals.ToList();
        StaffMember member = city.Staff[0];
        DateOnly date = r.UltimoPasso;
        engine.Die(leaderId, date);
        engine.Die(member.PersonId, date);

        Assert.Null(city.Leader(leaderId));
        Assert.All(city.Leaders, l => Assert.DoesNotContain(leaderId, l.Rivals));
        Assert.Single(r.LiderancasVagas, v => v.AntecessorId == leaderId);
        Assert.True(member.Left);
        Assert.Single(r.Noticias, n => n.Kind == "renovacao_morte_lideranca");
        Assert.Single(r.Noticias, n => n.Kind == "renovacao_morte_equipe");
        RenewalEvent death = r.Historico.First(e => e.Tipo == RenewalEventKind.Morte && e.PessoaId == leaderId);
        Assert.All(rivals, id => Assert.Contains(id, death.Enlutados));

        // Em alguns anos alguém assume o lugar da liderança, e a equipe disponível se recompõe.
        Advance(state, engine, date.AddYears(3));
        Assert.DoesNotContain(r.LiderancasVagas, v => v.AntecessorId == leaderId);
        RenewalEvent successor = Assert.Single(r.Historico, e => e.Tipo == RenewalEventKind.NovaLideranca && e.PessoaId == leaderId);
        Leader heir = city.Leader(successor.SucessorId!) ?? city.Leaders.First();
        Assert.NotNull(state.Npcs.FirstOrDefault(n => n.Id == successor.SucessorId));
        Assert.NotNull(city.Person(successor.SucessorId!));
        Assert.False(heir.Known && heir.PersonId == successor.SucessorId);
        Assert.Contains(r.Historico, e => e.Tipo == RenewalEventKind.NovoNaEquipe);

        // Todo mundo acompanhado usa a tábua da própria UF.
        Assert.All(r.Pessoas.Where(p => p.TabuaUsada is not null), p => Assert.Equal(r.Uf, p.TabuaUsada));
        Assert.Contains(r.Pessoas, p => p.TabuaUsada is not null);
    }

    // ---------------------------------------------------------------- com o jogador num cargo

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Com_o_jogador_vereador_a_camara_do_mandato_acompanha_a_sucessao()
    {
        var (state, legislature) = LegislatureTests.ElectedCouncillor();
        var engine = new RenewalEngine(RenewalKit.Context(state));
        RenewalState r = engine.Ensure();
        MandateState mandate = state.Mandate!;
        Assert.NotNull(r.SeatOf(RaceSetup.PlayerId));
        Assert.Equal(mandate.Members.Count, r.Cadeiras.Count);
        Assert.Equal(mandate.Start, r.MandatoInicio);
        Assert.Equal(mandate.End, r.MandatoFim);
        DateOnly date = state.Calendar.WeekStart;

        // Morre um colega: sai da Câmara do mandato e entra o suplente, do mesmo partido.
        ChamberMember colleague = mandate.Members.First(m => !m.IsPlayer && m.Id != r.PresidenteCamaraId && RenewalQueries.Substitutes(r, m.PartyId!).Count > 0);
        string substitute = RenewalQueries.Substitutes(r, colleague.PartyId!)[0];
        int members = mandate.Members.Count;
        engine.Die(colleague.Id, date);
        Assert.Null(mandate.Member(colleague.Id));
        ChamberMember entered = mandate.Member(substitute)!;
        Assert.Equal(colleague.PartyId, entered.PartyId);
        Assert.Equal(members, mandate.Members.Count);
        Assert.Single(mandate.Members, m => m.PartyId == colleague.PartyId && m.Leader);

        // Morre o prefeito: o vice assume e o mandato passa a apontar para ele.
        string vice = r.ViceId!;
        engine.Die(r.PrefeitoId!, date);
        Assert.Equal(vice, mandate.MayorId);
        Assert.Equal(RenewalKit.Npc(state, vice).BallotName, mandate.MayorName);

        // O jogador não é sorteado: segue na cadeira por todo o mandato, que continua votando com a Câmara nova.
        var world = new WorldEngine(state, null, engine);
        var policy = new Rng(9);
        for (int week = 0; week < 60; week++)
        {
            CareerAutoplayTests.PlayMandateWeek(legislature, policy);
            world.AdvanceTo(state.Calendar.WeekStart);
            Assert.Equal(mandate.Members.Select(m => m.Id).Order(), r.Titulares.Order());
        }

        Assert.NotNull(r.SeatOf(RaceSetup.PlayerId));
        Assert.Null(r.Status(RaceSetup.PlayerId));
    }

    [Fact]
    public void Com_o_jogador_prefeito_a_camara_acompanha_e_a_renuncia_dele_da_posse_ao_vice()
    {
        var (state, _) = ExecutiveTests.ElectedMayor();
        var engine = new RenewalEngine(RenewalKit.Context(state));
        RenewalState r = engine.Ensure();
        Assert.Equal(RaceSetup.PlayerId, r.PrefeitoId);
        Assert.NotNull(r.ViceId);
        DateOnly date = state.Calendar.WeekStart;
        ChamberMember member = state.Executive!.Chamber.First(m => RenewalQueries.Substitutes(r, m.PartyId!).Count > 0);
        string substitute = RenewalQueries.Substitutes(r, member.PartyId!)[0];
        engine.VacateSeat(member.Id, VacancyCause.Cassacao, date);
        Assert.DoesNotContain(state.Executive.Chamber, m => m.Id == member.Id);
        Assert.Contains(state.Executive.Chamber, m => m.Id == substitute);

        // Meses sem comando: o jogador segue prefeito.
        state.Calendar.JumpTo(date.AddDays(7 * 30));
        engine.AdvanceTo(state.Calendar.WeekStart);
        Assert.Equal(RaceSetup.PlayerId, r.PrefeitoId);

        // Renúncia (o Executivo sai do estado, como na desincompatibilização): o vice assume.
        string? vice = r.ViceId;
        state.Executive = null;
        state.Calendar.AdvanceWeek();
        engine.AdvanceTo(state.Calendar.WeekStart);
        if (vice is not null)
        {
            Assert.Equal(vice, r.PrefeitoId);
            Assert.Single(r.Historico, e => e.Tipo == RenewalEventKind.PosseVice);
        }
        else
        {
            Assert.Null(r.PrefeitoId);
            Assert.Single(r.EleicoesPendentes);
        }
    }

    /// <summary>Com o gancho da apuração (suplentes pela ordem de votação) e sem ele (lido da política local na posse).</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Eleicao_jogada_entrega_eleitos_e_suplentes_para_a_posse(bool comGancho)
    {
        CampaignEngine campaign = CampaignKit.NewCareer(RenewalKit.Town("pequeno", 7), 3, strongestParty: true);
        GameState state = campaign.Context.State;
        var engine = new RenewalEngine(RenewalKit.Context(state));
        var world = new WorldEngine(state, null, engine);
        RenewalState r = engine.Ensure();
        int before = r.Legislatura;
        CampaignAutoplayTests.Play(campaign, 3);
        var losers = state.Campaign!.Candidates.Where(c => c.Office == Planalto.Core.Elections.Office.Vereador && c.CharacterId is not null && !c.IsPlayer).Select(c => c.CharacterId!).ToList();
        CareerFlow.ConcludeElection(campaign.Context, CampaignKit.LegislatureContext(state), CampaignKit.ExecutiveContext(state), renewal: comGancho ? engine : null);
        Assert.Equal(comGancho, r.Eleitos is { Jogada: true });
        world.AdvanceTo(state.Calendar.WeekStart);
        if (comGancho)
        {
            // Os candidatos nomeados que não se elegeram ficam de suplentes do próprio partido, à frente dos nomes novos.
            foreach (var (party, list) in r.Suplentes)
            {
                var named = losers.Where(id => !r.Titulares.Contains(id) && RenewalKit.Npc(state, id).PartyId == party).ToList();
                Assert.Equal(named.Order(), list.Take(named.Count).Order());
            }
        }

        Assert.Null(r.Eleitos);
        Assert.Equal(before + 1, r.Legislatura);
        Assert.Equal(new DateOnly(state.Local!.ElectionYear + 1, 1, 1), r.MandatoInicio);
        Assert.Equal(state.Local.Councillors.Order(), r.Titulares.Order());
        Assert.Equal(state.Local.MayorId, r.PrefeitoId);
        Assert.NotNull(r.ViceId);
        Assert.False(r.Legislaturas[^1].Automatica);
        foreach (var group in r.Cadeiras.GroupBy(c => c.PartyId))
        {
            Assert.True(RenewalQueries.Substitutes(r, group.Key).Count >= RenewalKit.Content.Config.Suplentes.MinimoPorPartido, group.Key);
            Assert.All(RenewalQueries.Substitutes(r, group.Key), id => Assert.Equal(group.Key, RenewalKit.Npc(state, id).PartyId));
        }

        if (state.Mandate is { } mandate)
        {
            Assert.Equal(mandate.Members.Select(m => m.Id).Order(), r.Titulares.Order());
        }
    }

    // ---------------------------------------------------------------- determinismo e save

    [Fact]
    public void Save_e_load_no_meio_terminam_iguais()
    {
        var (state, engine) = RenewalKit.Idle(RenewalKit.Town("medio", 60), 31);
        Municipio m = CampaignKit.Municipio(state.FocusMunicipio);
        var evolution = new Planalto.Core.Indicators.IndicatorEvolution(IndicatorKit.Content, m, CampaignKit.C.Projection);
        var world = new WorldEngine(state, evolution, engine);
        world.EnsureIndicators(IndicatorKit.Uf(m.Uf), CampaignKit.C.Municipios);
        DateOnly start = state.Calendar.WeekStart;
        state.Calendar.JumpTo(start.AddYears(9));
        world.AdvanceTo(state.Calendar.WeekStart);

        GameState copy = GameStateSerializer.Deserialize(GameStateSerializer.Serialize(state));
        Assert.Equal(CampaignKit.Canonical(state), CampaignKit.Canonical(copy));
        var otherWorld = new WorldEngine(copy, new Planalto.Core.Indicators.IndicatorEvolution(IndicatorKit.Content, m, CampaignKit.C.Projection), new RenewalEngine(RenewalKit.Context(copy)));
        foreach (int years in new[] { 13, 21 })
        {
            state.Calendar.JumpTo(start.AddYears(years));
            world.AdvanceTo(state.Calendar.WeekStart);
            copy.Calendar.JumpTo(start.AddYears(years));
            otherWorld.AdvanceTo(copy.Calendar.WeekStart);
        }

        Assert.Equal(CampaignKit.Canonical(state), CampaignKit.Canonical(copy));
        Assert.True(state.Renewal!.Legislatura >= 6);
        Assert.Contains(state.Renewal.Historico, e => e.Tipo == RenewalEventKind.Morte);
    }

    [Fact]
    public void Dados_da_renovacao_sao_validos_e_as_origens_nao_escolhem_partido()
    {
        Assert.Empty(RenewalContent.Validate(TestContent.DataDirectory).SelectMany(r => r.Errors));
        Assert.Equal(EntriesCatalog.Known.Order(), RenewalKit.Content.Entries.Origens.Select(o => o.Id).Order());

        // Em 40 anos numa cidade, estreantes de várias origens, em mais de um partido cada uma.
        var (state, engine) = RenewalKit.Idle(RenewalKit.Town("grande", 20), 5);
        state.Calendar.JumpTo(state.Calendar.WeekStart.AddYears(40));
        engine.AdvanceTo(state.Calendar.WeekStart);
        var byOrigin = state.Renewal!.Pessoas.Where(p => p.Origem is not null && p.Mandatos > 0)
            .GroupBy(p => p.Origem!).ToDictionary(g => g.Key, g => g.Select(p => RenewalKit.Npc(state, p.Id).PartyId).Distinct().Count());
        output.WriteLine(string.Join(", ", byOrigin.Select(o => $"{o.Key}: {o.Value} partidos")));
        Assert.True(byOrigin.Count >= 4, string.Join(", ", byOrigin.Keys));
        Assert.All(byOrigin.Where(o => o.Key != EntryOrigin.Equipe), o => Assert.True(o.Value >= 2, o.Key));
        Assert.Contains(state.Renewal.Pessoas, p => p.ParenteDe is not null);
    }
}
