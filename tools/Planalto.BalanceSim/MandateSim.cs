using System.Globalization;
using System.Text;
using Planalto.Core.Campaign;
using Planalto.Core.Career;
using Planalto.Core.Elections;
using Planalto.Core.Events;
using Planalto.Core.Executive;
using Planalto.Core.Governance;
using Planalto.Core.Legislature;
using Planalto.Core.Municipios;
using Planalto.Core.People;
using Planalto.Core.State;

namespace Planalto.BalanceSim;

/// <summary>O que o robô fez e viu num mandato de vereador.</summary>
public sealed class MandateTrace
{
    /// <summary>Instrumento da opção de maior ganho previsto vista em cada semana.</summary>
    public List<string> BestByWeek { get; } = [];

    /// <summary>Instrumento usado em cada turno.</summary>
    public List<string> UsedByTurn { get; } = [];

    public int Weeks { get; set; }

    public int IdleTurns { get; set; }

    public int Decisions { get; set; }

    public int Findings { get; set; }

    public int WorksDelivered { get; set; }

    public int Inaugurations { get; set; }

    public int PromisesAtStart { get; set; }

    public int PromisesCollected { get; set; }

    public int PromisesKept { get; set; }

    public int PromisesExpired { get; set; }

    public int Deals { get; set; }

    public int Crises { get; set; }

    public double PresenceStart { get; set; }

    public double PresenceEnd { get; set; }

    public bool CpiOpened { get; set; }
}

/// <summary>
/// BalanceSim de mandato (ROADMAP, R4): um robô cumpre o mandato de vereador escolhendo, em cada turno, o instrumento
/// de maior ganho previsto. Critério (GDD 17.1, princípio a): nenhum instrumento é a melhor escolha em mais de 40% das
/// semanas.
/// </summary>
public static class MandateSim
{
    private const double MaxShare = 0.40;

    /// <summary>
    /// Quanto o robô dá a cada linha da previsão, em pontos de presença na cidade. É o juízo do robô, não regra do
    /// jogo: presença pesa pela fatia da zona na população; o resto é convertido por estes pesos.
    /// </summary>
    private const double ImageWeight = 15;        // 1,0 de imagem (a escala vai de −1 a 1) = 15 pontos de presença
    private const double PromiseWeight = 1.5;     // uma promessa cumprida
    private const double CapitalWeight = 0.08;    // um ponto de capital político
    private const double MayorWeight = 0.02;      // um ponto de relação com o prefeito
    private const double RelationWeight = 0.01;   // um ponto de relação com um colega
    private const double SkeletonWeight = -1.0;   // um ponto de gravidade de esqueleto no Livro

    public static int Run(SimContent content, string[] portes, int mandates, int cities, ulong seed, string? output)
    {
        var culture = CultureInfo.GetCultureInfo("pt-BR");
        string dir = content.DataDirectory;
        var groups = content.Electorate.Segments.Dimensions.SelectMany(d => d.Groups).Select(g => g.Id).ToHashSet(StringComparer.Ordinal);
        var legislature = LegislatureContent.LoadOrThrow(dir, groups, content.World.Parties.Themes.Themes.Select(t => t.Id).ToHashSet(StringComparer.Ordinal));
        var executive = ExecutiveContent.LoadOrThrow(dir, groups);
        var governance = GovernanceContent.LoadOrThrow(dir, executive, content.Campaign.Promises);

        var report = new StringBuilder();
        report.AppendLine("# BalanceSim — mandato de vereador (R4)");
        report.AppendLine();
        report.AppendLine(string.Format(culture, "Robô que cumpre o mandato de vereador escolhendo, em cada um dos {0} turnos da semana, a opção de instrumento de maior ganho previsto (instrumento e alvo), a partir das previsões que o jogo mostra ao jogador. {1} mandatos por porte, em {2} cidades sorteadas por porte (semente {3}).", governance.Instruments.Regras.TurnosPorSemana, mandates, cities, seed));
        report.AppendLine();
        report.AppendLine("Como o robô chega ao mandato: a campanha não é o objeto desta medição. Ela é jogada sem agenda, com a presença do candidato mantida em pelo menos 60 em todas as zonas para que ele se eleja (se não se eleger, a preparação é refeita com outra semente), e com até 3 promessas de obra feitas a zonas (para haver cobrança no mandato).");
        report.AppendLine();
        report.AppendLine(string.Format(culture, "Como o robô compara opções (juízo do robô, não regra do jogo): soma as linhas da previsão em pontos de presença na cidade. Presença na zona vale pela fatia da zona na população, tanto a de agora quanto a esperada depois (obra entregue); imagem vale {0:0} pontos por unidade; promessa cumprida, {1:0.0}; capital político, {2:0.00} por ponto; relação com o prefeito, {3:0.00} por ponto; relação com um colega, {4:0.00}; esqueleto no Livro, {5:0.0} por ponto de gravidade. No gabinete, nomeia primeiro quem tem vaga prometida; nas decisões (eventos e achados da fiscalização), escolhe a opção de maior valor pela mesma conta; inaugura toda obra pronta; vota pela orientação do partido.", ImageWeight, PromiseWeight, CapitalWeight, MayorWeight, RelationWeight, SkeletonWeight));
        report.AppendLine();
        report.AppendLine("Critério (GDD 17.1, princípio a, e ROADMAP R4): nenhum instrumento é a melhor escolha em mais de 40% das semanas. A melhor escolha da semana é a opção de maior ganho previsto vista nela (no primeiro turno, antes de o robô gastar qualquer coisa).");
        report.AppendLine();
        bool allOk = true;

        foreach (string porte in portes)
        {
            List<Municipio> pool = content.Municipios.All.Where(m => m.Porte == porte && m.EleicoesMunicipais).OrderBy(m => m.CodigoIbge).ToList();
            List<Municipio> chosen = new Rng(seed + (ulong)porte.Length).Shuffled(pool).Take(Math.Min(cities, pool.Count)).ToList();
            var traces = new MandateTrace?[mandates];
            var failures = new List<string>();
            DateTime started = DateTime.UtcNow;
            Parallel.For(0, mandates, i =>
            {
                Municipio m = chosen[i % chosen.Count];
                try
                {
                    // A campanha de preparação pode não eleger o candidato: tenta outras sementes na mesma cidade.
                    for (ulong attempt = 0; attempt < 6 && traces[i] is null; attempt++)
                    {
                        traces[i] = Play(content, legislature, executive, governance, m, seed + (ulong)(1000 * (i + 1)) + attempt);
                    }

                    if (traces[i] is null)
                    {
                        throw new InvalidOperationException("o candidato não se elegeu em 6 campanhas de preparação.");
                    }
                }
                catch (Exception ex)
                {
                    lock (failures)
                    {
                        failures.Add($"{m.CodigoIbge} {m.Nome}, mandato {i}: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            });

            List<MandateTrace> done = traces.OfType<MandateTrace>().ToList();
            report.AppendLine($"## Porte {porte}");
            report.AppendLine();
            report.AppendLine(string.Format(culture, "{0} mandatos concluídos, {1} com erro, em {2:0} s. Cidades: {3}.", done.Count, failures.Count, (DateTime.UtcNow - started).TotalSeconds, string.Join(", ", chosen.Select(m => $"{m.Nome}/{m.Uf}"))));
            foreach (string f in failures.Take(5))
            {
                report.AppendLine($"- ERRO {f}");
            }

            if (done.Count == 0)
            {
                allOk = false;
                report.AppendLine();
                continue;
            }

            var best = done.SelectMany(t => t.BestByWeek).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
            var used = done.SelectMany(t => t.UsedByTurn).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
            double weeks = Math.Max(1, best.Values.Sum());
            double turns = Math.Max(1, used.Values.Sum());
            report.AppendLine();
            report.AppendLine("| Instrumento | Semanas em que foi a melhor escolha | Turnos em que foi usado |");
            report.AppendLine("|---|---|---|");
            foreach (InstrumentDefinition def in governance.Instruments.Instrumentos.OrderByDescending(d => best.GetValueOrDefault(d.Id)))
            {
                report.AppendLine(string.Format(culture, "| {0} | {1:P1} | {2:P1} |", def.Nome, best.GetValueOrDefault(def.Id) / weeks, used.GetValueOrDefault(def.Id) / turns));
            }

            double maxShare = best.Count == 0 ? 1 : best.Values.Max() / weeks;
            bool ok = maxShare <= MaxShare && failures.Count == 0;
            allOk &= ok;
            report.AppendLine();
            report.AppendLine(string.Format(culture, "- Instrumento que mais vezes foi a melhor escolha: **{0:P1}** das semanas (limite 40%). Instrumentos diferentes que foram a melhor escolha em alguma semana: {1} de {2}.", maxShare, best.Count, governance.Instruments.Instrumentos.Count));
            report.AppendLine(string.Format(culture, "- Por mandato, em média: {0:0} semanas, {1:0.0} decisões de evento, {2:0.0} achados de fiscalização decididos, {3:0.0} acordos de voto, {4:0.0} obras entregues a pedido do mandato ({5:0.0} inauguradas pelo robô), {6:0.0} crises do governo (todas com causa). Turnos sem opção útil: {7:P1}.",
                done.Average(t => t.Weeks), done.Average(t => t.Decisions), done.Average(t => t.Findings), done.Average(t => t.Deals), done.Average(t => t.WorksDelivered), done.Average(t => t.Inaugurations),
                done.Average(t => t.Crises), done.Sum(t => t.IdleTurns) / Math.Max(1.0, done.Sum(t => t.IdleTurns) + turns)));
            report.AppendLine(string.Format(culture, "- Livro: {0:0.0} promessas abertas na posse por mandato; {1:P0} cobradas ao menos uma vez; {2:0.0} cumpridas e {3:0.0} vencidas por mandato.",
                done.Average(t => t.PromisesAtStart), done.Sum(t => t.PromisesCollected) / Math.Max(1.0, done.Sum(t => t.PromisesAtStart)), done.Average(t => t.PromisesKept), done.Average(t => t.PromisesExpired)));
            report.AppendLine(string.Format(culture, "- Presença na cidade: {0:0.0} na posse, {1:0.0} no fim do mandato. Mandatos com CPI aberta pelo robô: {2} de {3}.",
                done.Average(t => t.PresenceStart), done.Average(t => t.PresenceEnd), done.Count(t => t.CpiOpened), done.Count));
            report.AppendLine($"- **{(ok ? "Dentro do critério" : "FORA do critério")}.**");
            report.AppendLine();
            Console.WriteLine($"{porte}: melhor instrumento em {maxShare:P1} das semanas, {(ok ? "ok" : "FORA")}");
        }

        string text = report.ToString().ReplaceLineEndings("\n");
        if (output is { Length: > 0 })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            File.WriteAllText(output, text, new UTF8Encoding(false));
        }

        Console.WriteLine();
        Console.WriteLine(text);
        return allOk ? 0 : 1;
    }

    private static MandateTrace? Play(SimContent content, LegislatureContent legislature, ExecutiveContent executive, GovernanceContent governance, Municipio m, ulong seed)
    {
        CampaignEngine campaign = content.NewCareer(m, seed, strongestParty: true);
        GameState state = campaign.Context.State;
        CampaignState race = state.Campaign!;

        // Até 3 promessas de obra a zonas, para o Livro chegar ao mandato com o que cobrar.
        int promised = 0;
        foreach (PromiseDefinition definition in content.Campaign.Promises.Promessas.Where(p => p.Tipo == PromiseDefinition.TipoObra && p.Alvos.Contains(Promise.ParaZona)))
        {
            foreach (string zone in race.Territory.Zones)
            {
                if (promised < 3 && campaign.MakePromise(definition.Id, Promise.ParaZona, zone).Failure is null)
                {
                    promised++;
                    break;
                }
            }
        }

        while (race.Phase is CampaignPhase.PreCampanha or CampaignPhase.Campanha)
        {
            double[] presence = race.Territory.Presence[RaceSetup.PlayerId];
            for (int z = 0; z < presence.Length; z++)
            {
                presence[z] = Math.Max(presence[z], 60);
            }

            ResolveCampaignPending(campaign);
            campaign.AdvanceDay();
        }

        ResolveCampaignPending(campaign);
        campaign.CastPlayerVote(new UrnaVote(Office.Vereador, race.Player.Number, false));
        if (!campaign.ResolveElection().PlayerElected)
        {
            return null;
        }

        var legislatureContext = new LegislatureContext
        {
            State = state, Municipio = m, Content = legislature, People = content.People, Electorate = campaign.Context.Electorate,
            RecognitionScale = content.Campaign.Config.PorteRecognitionMultiplier.GetValueOrDefault(m.Porte, 1),
        };
        var governanceContext = new GovernanceContext
        {
            State = state, Municipio = m, Content = governance, Legislature = legislature, Executive = executive, People = content.People, Campaign = content.Campaign, Zonas = campaign.Context.Zonas,
        };
        CareerFlow.ConcludeElection(campaign.Context, legislatureContext, governanceContext: governanceContext);
        var mandate = new LegislatureEngine(legislatureContext);
        var gov = new GovernanceEngine(governanceContext);
        GovernanceState g = state.Governance!;
        var trace = new MandateTrace { PromisesAtStart = g.PromisesAtStart.Count, PresenceStart = g.CityPresence() };
        gov.SetInstruction(VoteInstruction.SeguirOrientacao);

        while (state.Mandate is not null && !CareerFlow.ShouldStartCampaign(state) && trace.Weeks < 260)
        {
            trace.Weeks++;
            if (state.Player.PartyId is null)
            {
                PartyAffiliation.Join(state, state.Parties[0].Id);
            }

            Cabinet(gov, g, state);
            if (g.Decision is not null && gov.DecisionDefinition is { } def)
            {
                TermEventOption pick = def.Opcoes.Where(o => gov.ForecastDecision(o.Id).Blocked is null).DefaultIfEmpty(def.Opcoes.First(o => o.Padrao)).MaxBy(o => Value(g, gov.ForecastDecision(o.Id)))!;
                gov.Decide(pick.Id);
                trace.Decisions++;
            }

            foreach (Inspection inspection in g.Inspections.Where(i => i.Pending).ToList())
            {
                string option = FindingOption.Ids.MaxBy(id => Value(g, gov.ForecastFinding(inspection.Id, id)))!;
                gov.DecideFinding(inspection.Id, option);
                trace.Findings++;
            }

            foreach (WorkView ready in GovernanceQueries.Works(gov).Where(w => w.CanInaugurate))
            {
                if (gov.Inaugurate(ready.Work.Id).Ok)
                {
                    trace.Inaugurations++;
                }
            }

            foreach (Work work in g.Works.Where(w => w is { Irregular: true, Revealed: true, Denounced: true }).ToList())
            {
                if (gov.ForecastCpi(work.Id).Blocked is null && gov.RequestCpi(work.Id).CreatedId is not null)
                {
                    trace.CpiOpened = true;
                }
            }

            bool first = true;
            while (g.TurnsLeft > 0)
            {
                var options = gov.CouncilOptions().Select(o => (Option: o, Value: Value(g, o.Forecast))).ToList();
                if (options.Count == 0)
                {
                    trace.IdleTurns += g.TurnsLeft;
                    break;
                }

                var best = options.MaxBy(o => o.Value);
                if (first)
                {
                    trace.BestByWeek.Add(best.Option.InstrumentId);
                    first = false;
                }

                if (best.Value <= 0)
                {
                    // Nada rende: o robô guarda o turno em vez de gastar capital ou relação à toa.
                    trace.IdleTurns += g.TurnsLeft;
                    break;
                }

                if (!gov.Use(best.Option).Ok)
                {
                    throw new InvalidOperationException($"opção listada não pôde ser usada: {best.Option.InstrumentId} ({best.Option.Label}).");
                }

                trace.UsedByTurn.Add(best.Option.InstrumentId);
                if (best.Option.InstrumentId == InstrumentDefinition.NegociarVoto)
                {
                    trace.Deals++;
                }
            }

            CareerFlow.AdvanceWeek(state, null, mandate, governance: gov);
        }

        var (promises, collected) = gov.CollectionStats();
        trace.PromisesCollected = collected;
        trace.PromisesKept = state.Ledger.Promises.Count(p => g.PromisesAtStart.Contains(p.Id) && p.State == PromiseState.Cumprida);
        trace.PromisesExpired = state.Ledger.Promises.Count(p => g.PromisesAtStart.Contains(p.Id) && p.State == PromiseState.Vencida);
        trace.WorksDelivered = g.Works.Count(w => w.SponsorId == RaceSetup.PlayerId && w.Stage == WorkStage.Entregue);
        trace.Crises = g.Crises.Count;
        trace.PresenceEnd = g.CityPresence();
        _ = promises;
        return trace;
    }

    /// <summary>Gabinete: primeiro quem tem vaga prometida; depois, os de melhor qualidade. Comunicação, base nas zonas da base, chefe e legislativo.</summary>
    private static void Cabinet(GovernanceEngine gov, GovernanceState g, GameState state)
    {
        if (g.Cabinet.Count() >= gov.CabinetSlots)
        {
            return;
        }

        var promisedTo = state.Ledger.Open.Where(p => p.ToKind == Promise.ParaPessoa).Select(p => p.ToId).ToHashSet(StringComparer.Ordinal);
        foreach (Aide candidate in g.Aides.Where(a => !a.Appointed && !a.Left && a.Origin != Aide.Parente)
            .OrderByDescending(a => promisedTo.Contains(a.PersonId) || (a.NomineeOf is not null && promisedTo.Contains(a.NomineeOf))).ThenByDescending(a => a.Quality).ToList())
        {
            if (g.Cabinet.Count() >= gov.CabinetSlots)
            {
                break;
            }

            string? baseZone = g.BaseZones.Select(z => g.Zones[z]).FirstOrDefault(z => !g.Cabinet.Any(a => a.Role == AideRole.Base && a.ZoneId == z));
            string role = g.AideIn(AideRole.Comunicacao) is null ? AideRole.Comunicacao
                : baseZone is not null ? AideRole.Base
                : g.AideIn(AideRole.Chefe) is null ? AideRole.Chefe
                : g.AideIn(AideRole.Legislativo) is null ? AideRole.Legislativo : AideRole.Base;
            string? zone = role == AideRole.Base ? baseZone ?? g.Zones.FirstOrDefault(z => !g.Cabinet.Any(a => a.Role == AideRole.Base && a.ZoneId == z)) : null;
            gov.AppointAide(candidate.PersonId, role, zone);
        }
    }

    /// <summary>Valor de uma previsão para o robô, em pontos de presença na cidade.</summary>
    private static double Value(GovernanceState g, GovForecast forecast)
    {
        if (forecast.Blocked is not null)
        {
            return double.NegativeInfinity;
        }

        double population = Math.Max(1, g.ZonePopulation.Sum(p => (double)p));
        double value = 0;
        foreach (ForecastLine line in forecast.Lines)
        {
            switch (line.Kind)
            {
                case GovForecast.Presenca or GovForecast.PresencaDepois:
                    int zone = line.Target is null ? -1 : g.IndexOf(line.Target);
                    value += line.Expected * (zone < 0 ? 1 : g.ZonePopulation[zone] / population);
                    break;
                case GovForecast.Imagem or GovForecast.Imagem + "_later":
                    value += line.Expected * ImageWeight;
                    break;
                case GovForecast.PromessaCumprida:
                    value += line.Expected * PromiseWeight;
                    break;
                case GovForecast.Capital or GovForecast.Capital + "_later":
                    value += line.Expected * CapitalWeight;
                    break;
                case GovForecast.RelacaoPrefeito:
                    value += line.Expected * MayorWeight;
                    break;
                case GovForecast.Relacao:
                    value += line.Expected * RelationWeight;
                    break;
                case GovForecast.Esqueleto:
                    value += line.Expected * SkeletonWeight;
                    break;
            }
        }

        return value;
    }

    private static void ResolveCampaignPending(CampaignEngine campaign)
    {
        GameState state = campaign.Context.State;
        CampaignState race = state.Campaign!;
        while (race.Events.Pending.Count > 0)
        {
            PendingEvent pending = race.Events.Pending[0];
            EventDefinition definition = campaign.Context.Events!.Find(pending.EventId)!;
            int choice = Enumerable.Range(0, definition.Choices.Count).First(i => EventEngine.Blocked(campaign.Context, definition.Choices[i]) is null);
            EventEngine.Resolve(campaign.Context, pending, choice, state.Rng);
        }

        while (campaign.Agenda.PendingRequest is not null)
        {
            campaign.AnswerLeader(LeaderAnswer.Recusar);
        }
    }
}
