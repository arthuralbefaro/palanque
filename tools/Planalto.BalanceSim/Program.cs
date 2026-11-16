using System.Globalization;
using System.Text;
using Planalto.BalanceSim;
using Planalto.Core.Campaign;
using Planalto.Core.Data;
using Planalto.Core.Municipios;
using Planalto.Core.State;

// Uso: dotnet run --project tools/Planalto.BalanceSim -- [--campanhas 200] [--cidades 10] [--portes pequeno,medio,grande,metropole]
//                                                        [--semente 1] [--paradas 0] [--saida docs/balance/r3.md]
// --paradas N: além das campanhas jogadas, N campanhas por porte com o robô parado (a régua "sem campanha" da D-026).
// Joga campanhas de vereador com o robô que escolhe sempre o compromisso de maior ganho previsto de votos e, desde a
// R2b, contrata, reparte a verba pelos canais, assume posições e faz promessas; desde a R3, responde a doadores e a
// ataques, e depois da eleição passa pelo julgamento das contas. Mede o princípio a do GDD 17.1 (nenhum tipo de
// compromisso é a melhor escolha em mais de 40% das semanas, e nenhum par (tipo, zona) em mais de 25%), o da verba
// (nenhum canal é o melhor destino em mais de 40% das semanas), o princípio e (média de 1 ou mais decisões com
// troca por semana) e o das contas (GDD 17.12: mais de 30% do limite "por fora" perde o mandato em 25% a 40% das
// carreiras em 2 anos; campanha limpa, em menos de 1%). Código de saída 0 = tudo dentro dos critérios; 1 = algo fora.

string? dataDirectory = DataPaths.FindDataDirectory(Directory.GetCurrentDirectory()) ?? DataPaths.FindDataDirectory(AppContext.BaseDirectory);
if (dataDirectory is null)
{
    Console.Error.WriteLine("Pasta data/ não encontrada. Rode a partir da raiz do projeto.");
    return 2;
}

string Arg(string name, string fallback)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
}

int campaigns = int.Parse(Arg("--campanhas", "200"), CultureInfo.InvariantCulture);
int cities = int.Parse(Arg("--cidades", "10"), CultureInfo.InvariantCulture);
ulong seed = ulong.Parse(Arg("--semente", "1"), CultureInfo.InvariantCulture);
int idleCampaigns = int.Parse(Arg("--paradas", "0"), CultureInfo.InvariantCulture);
string[] portes = Arg("--portes", "pequeno,medio,grande,metropole").Split(',', StringSplitOptions.RemoveEmptyEntries);
string? output = Array.IndexOf(args, "--saida") >= 0 ? Arg("--saida", "") : null;

const double MaxTypeShare = 0.40;
const double MaxPairShare = 0.25;
const double MaxChannelShare = 0.40;
const double MinTradeDecisions = 1.0;

var content = new SimContent(dataDirectory);
var report = new StringBuilder();
var culture = CultureInfo.GetCultureInfo("pt-BR");
report.AppendLine("# BalanceSim — campanha de vereador (R3)");
report.AppendLine();
report.AppendLine(string.Format(culture, "Robô que marca, em cada turno livre, o compromisso de maior ganho previsto de votos (inclusive a entrevista em cada veículo e o tema em alta) e, toda semana, contrata, reparte a verba pelos canais, assume posições, faz promessas, delega caminhadas e responde aos eventos da equipe. Na R3 passa a responder às ofertas de doadores (metade das campanhas é \"limpa\": só aceita com recibo, até o teto; a outra metade é \"suja\": aceita todo dinheiro por fora), a responder aos ataques pela resposta de menor dano esperado e a atacar, de 2 em 2 semanas depois da primeira pesquisa pública, quem está logo à frente nela. Depois da eleição, o processo de contas de cada campanha (prestação final, julgamento e representação do art. 30-A) é simulado por dois anos em {3} réplicas, como se o candidato tivesse sido eleito. {0} campanhas por porte, em {1} cidades sorteadas por porte (semente {2}), metade no partido mais forte da cidade e metade num partido sorteado.", campaigns, cities, seed, CampaignRobot.AccountsReplicas));
report.AppendLine();
report.AppendLine("Critérios (GDD 17.1 e 17.12; ROADMAP, R3):");
report.AppendLine("- princípio a: nenhum tipo de compromisso é a melhor escolha em mais de 40% das semanas; nenhum par (tipo, zona) em mais de 25%;");
report.AppendLine("- verba: nenhum canal é o melhor destino (o da primeira parcela, com o plano vazio) em mais de 40% das semanas. Medido nas semanas da campanha oficial, em que os quatro canais estão abertos; na pré-campanha só há dois (Redes sem impulsionamento e Material institucional), e a divisão entre eles vai na tabela como informação;");
report.AppendLine("- princípio e: média de 1 ou mais decisões com troca por semana (pedido de liderança, evento, evento da equipe, conselho da semana, convite, contratação, mudança de posição, promessa local, oferta de doação, resposta a ataque, ataque);");
report.AppendLine("- contas: campanha com mais de 30% do limite de gastos em dinheiro \"por fora\" perde o mandato em 25% a 40% das carreiras em até 2 anos; campanha limpa, em menos de 1%. Medido no conjunto dos portes;");
report.AppendLine("- desempenho: a simulação semanal (a virada de domingo) abaixo de 200 ms numa metrópole.");
report.AppendLine();
bool allOk = true;
var everything = new List<CampaignTrace>();
var summary = new List<string>();

foreach (string porte in portes)
{
    List<Municipio> pool = content.Municipios.All.Where(m => m.Porte == porte && m.EleicoesMunicipais).OrderBy(m => m.CodigoIbge).ToList();
    List<Municipio> chosen = new Rng(seed + (ulong)porte.Length).Shuffled(pool).Take(Math.Min(cities, pool.Count)).ToList();
    var traces = new CampaignTrace[campaigns];
    var failures = new List<string>();
    var started = DateTime.UtcNow;
    Parallel.For(0, campaigns, i =>
    {
        Municipio m = chosen[i % chosen.Count];
        try
        {
            var engine = content.NewCareer(m, seed + (ulong)(1000 * (i + 1)), strongestParty: i % 2 == 0);
            traces[i] = new CampaignRobot(engine) { Diagnose = i == 0 && Environment.GetEnvironmentVariable("PLANALTO_SIM_DIAG") == "1", Dirty = i % 4 >= 2 }.Play();
            traces[i].Strongest = i % 2 == 0;
        }
        catch (Exception ex)
        {
            lock (failures)
            {
                failures.Add($"{m.CodigoIbge} {m.Nome}, campanha {i}: {ex.GetType().Name}: {ex.Message}");
            }
        }
    });

    List<CampaignTrace> done = traces.Where(t => t is not null).ToList();
    double playedSeconds = (DateTime.UtcNow - started).TotalSeconds;

    // Sem campanha: as mesmas cidades e sementes, com o robô parado.
    var idle = new CampaignTrace[idleCampaigns];
    Parallel.For(0, idleCampaigns, i =>
    {
        Municipio m = chosen[i % chosen.Count];
        try
        {
            var engine = content.NewCareer(m, seed + (ulong)(1000 * (i + 1)), strongestParty: i % 2 == 0);
            idle[i] = new CampaignRobot(engine).PlayIdle();
            idle[i].Strongest = i % 2 == 0;
            idle[i].Idle = true;
        }
        catch (Exception ex)
        {
            lock (failures)
            {
                failures.Add($"{m.CodigoIbge} {m.Nome}, campanha parada {i}: {ex.GetType().Name}: {ex.Message}");
            }
        }
    });
    List<CampaignTrace> idleDone = idle.Where(t => t is not null).ToList();
    report.AppendLine($"## Porte {porte}");
    report.AppendLine();
    report.AppendLine(string.Format(culture, "{0} campanhas concluídas, {1} com erro, em {2:0} s. Cidades: {3}.", done.Count, failures.Count, playedSeconds, string.Join(", ", chosen.Select(m => $"{m.Nome}/{m.Uf}"))));
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

    // Fatia de semanas em que cada tipo foi a melhor escolha, sobre todas as semanas de todas as campanhas.
    var byType = done.SelectMany(t => t.BestTypeByWeek).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
    double weeks = byType.Values.Sum();
    var byTurn = done.SelectMany(t => t.BestTypeByTurn).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
    double turns = Math.Max(1, byTurn.Values.Sum());
    var byPlurality = done.SelectMany(t => t.PluralityTypeByWeek).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
    double pluralityWeeks = Math.Max(1, byPlurality.Values.Sum());
    report.AppendLine();
    report.AppendLine("| Tipo de compromisso | Semanas em que teve a opção de maior ganho | Turnos em que foi a melhor escolha | Semanas em que foi o mais marcado |");
    report.AppendLine("|---|---|---|---|");
    foreach (string type in byTurn.Keys.Union(byType.Keys).OrderByDescending(k => byType.GetValueOrDefault(k)))
    {
        report.AppendLine(string.Format(culture, "| {0} | {1:P1} | {2:P1} | {3:P1} |", content.Campaign.Commitments.Get(type).Nome, byType.GetValueOrDefault(type) / weeks, byTurn.GetValueOrDefault(type) / turns, byPlurality.GetValueOrDefault(type) / pluralityWeeks));
    }

    // Critério (letra do GDD 17.1, a): a melhor escolha da semana é a opção de maior ganho previsto vista nela.
    // As outras duas colunas são informativas: o que o robô de fato marcou, turno a turno e por semana.
    double maxType = byType.Values.Max() / weeks;
    double maxPlurality = byPlurality.Count == 0 ? 1 : byPlurality.Values.Max() / pluralityWeeks;
    double maxTurn = byTurn.Count == 0 ? 1 : byTurn.Values.Max() / turns;

    // Par (tipo, zona): a fatia é medida dentro de cada campanha (as zonas são de cada cidade) e vale a pior campanha
    // e a média das piores.
    List<double> worstPair = done.Select(t => t.BestPairByWeek.Count == 0 ? 0 : t.BestPairByWeek.GroupBy(x => x).Max(g => g.Count()) / (double)t.BestPairByWeek.Count).ToList();
    double maxPair = worstPair.Max();
    // O critério do par vale sobre o conjunto das campanhas do porte: a fatia de semanas em que a melhor escolha foi o
    // par mais repetido da própria campanha (as zonas são de cada cidade, então o par só se compara dentro dela).
    double pooledPair = done.Sum(t => t.BestPairByTurn.Count == 0 ? 0 : t.BestPairByTurn.GroupBy(x => x).Max(g => g.Count())) / Math.Max(1.0, done.Sum(t => t.BestPairByTurn.Count));
    // Verba: o canal da primeira parcela de cada semana.
    var byChannel = done.SelectMany(t => t.BestChannelByWeek).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
    var byChannelPre = done.SelectMany(t => t.BestChannelByWeekPre).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
    double channelWeeks = Math.Max(1, byChannel.Values.Sum());
    double channelWeeksPre = Math.Max(1, byChannelPre.Values.Sum());
    double maxChannel = byChannel.Count == 0 ? 1 : byChannel.Values.Max() / channelWeeks;
    double totalWeeks = Math.Max(1, done.Sum(t => t.Weeks));
    var trades = done.SelectMany(t => t.TradeDecisions).GroupBy(d => d.Key).ToDictionary(g => g.Key, g => g.Sum(d => d.Value));
    double tradePerWeek = trades.Values.Sum() / totalWeeks;
    bool ok = maxType <= MaxTypeShare && pooledPair <= MaxPairShare && maxChannel <= MaxChannelShare && tradePerWeek >= MinTradeDecisions && failures.Count == 0;
    allOk &= ok;
    report.AppendLine();
    report.AppendLine(string.Format(culture, "- Tipo com a opção de maior ganho da semana: **{0:P1}** das semanas (limite 40%). Informativo: o tipo mais marcado foi a melhor escolha em {1:P1} dos turnos e o mais marcado da semana em {2:P1} das semanas.", maxType, maxTurn, maxPlurality));
    report.AppendLine(string.Format(culture, "- Par (tipo, zona) mais repetido: **{0:P1}** das semanas no conjunto das campanhas (limite 25%), medido turno a turno; {1:P1} na pior campanha isolada pela opção de maior ganho da semana.", pooledPair, maxPair));
    string Elected(IEnumerable<CampaignTrace> list) => $"{list.Count(t => t.Elected)} de {list.Count()}";
    List<CampaignTrace> strong = done.Where(t => t.Strongest).ToList();
    List<CampaignTrace> drawn = done.Where(t => !t.Strongest).ToList();
    report.AppendLine(string.Format(culture, "- Eleitos: {0} no partido mais forte; {1} em partido sorteado. Posição mediana entre os nomeados: {2}.",
        Elected(strong), Elected(drawn), done.Select(t => t.Position).Order().ElementAt(done.Count / 2)));
    report.AppendLine(string.Format(culture, "- Eleitos, campanhas limpas: {0} no partido mais forte e {1} em partido sorteado; campanhas \"sujas\": {2} e {3}.",
        Elected(strong.Where(t => !t.Dirty)), Elected(drawn.Where(t => !t.Dirty)), Elected(strong.Where(t => t.Dirty)), Elected(drawn.Where(t => t.Dirty))));
    string idleText = idleDone.Count == 0 ? "não medido" : $"{Elected(idleDone.Where(t => t.Strongest))} no partido mais forte; {Elected(idleDone.Where(t => !t.Strongest))} em partido sorteado";
    report.AppendLine($"- Eleitos sem campanha (robô parado, mesmas cidades e sementes): {idleText}.");
    string Hardness(List<CampaignTrace> list) => list.Count == 0 ? "sem campanhas" : string.Format(culture, "reconhecimento do jogador {0:0.0}% (rivais nomeados: média {1:0.0}%, o maior {2:0.0}%), votos de {3:P0} do quociente eleitoral, {4:0.0} cadeiras do partido, posição mediana {5} entre os nomeados da chapa",
        list.Average(t => t.FinalRecognition), list.Average(t => t.RivalRecognition), list.Average(t => t.RivalRecognitionMax), list.Average(t => t.VotesOverQuotient), list.Average(t => (double)t.PartySeats), list.Select(t => t.RankInParty).Order().ElementAt(list.Count / 2));
    report.AppendLine($"- No fim da campanha, no partido mais forte: {Hardness(strong)}. Em partido sorteado: {Hardness(drawn)}. Sem campanha: {Hardness(idleDone)}.");
    var pairs = done.Where(t => t.TopPairTurns > 0).GroupBy(t => t.TopPair).OrderByDescending(g => g.Sum(t => t.TopPairTurns)).ToList();
    double pairTurns = Math.Max(1.0, done.Sum(t => t.BestPairByTurn.Count));
    report.AppendLine(string.Format(culture, "- De que é feito o par mais repetido (pontos percentuais da fatia; campanhas em que foi o par mais repetido): {0}.",
        string.Join("; ", pairs.Take(8).Select(g => string.Format(culture, "{0}: {1:0.0} ({2} campanhas, {3:P1} dos turnos delas)", g.Key, 100 * g.Sum(t => t.TopPairTurns) / pairTurns, g.Count(), g.Sum(t => t.TopPairTurns) / Math.Max(1.0, g.Sum(t => t.BestPairByTurn.Count)))))));
    report.AppendLine(string.Format(culture, "- Por campanha, em média: {0:0} compromissos, {1:0.0} decisões de pedido de liderança, {2:0.0} promessas no Livro, {3:0.0} lideranças aliadas no fim, {4:0.0} eventos; reconhecimento final {5:0.0}%.",
        done.Average(t => t.Commitments), done.Average(t => t.LeaderDecisions), done.Average(t => t.Promises), done.Average(t => t.AlignedLeaders), done.Average(t => t.Events), done.Average(t => t.FinalRecognition)));
    report.AppendLine(string.Format(culture, "- Turnos de escolha forçada (arrecadar sem caixa ou descansar com estresse alto), fora da estatística acima: {0:P1}.", done.Sum(t => t.ForcedTurns) / (double)Math.Max(1, done.Sum(t => t.Turns))));
    report.AppendLine();
    report.AppendLine("| Canal de verba | Melhor destino na campanha oficial | Melhor destino na pré-campanha | Verba planejada por campanha | Votos por R$ 1.000 da primeira parcela (campanha oficial) |");
    report.AppendLine("|---|---|---|---|---|");
    foreach (var channel in content.Campaign.Channels.Canais.OrderByDescending(c => byChannel.GetValueOrDefault(c.Id)))
    {
        double value = done.Sum(t => t.ChannelValue.GetValueOrDefault(channel.Id)) / Math.Max(1, done.Sum(t => t.ChannelWeeks));
        report.AppendLine(string.Format(culture, "| {0} | {1:P1} | {2:P1} | R$ {3:N0} | {4:0.00} |", channel.Nome,
            byChannel.GetValueOrDefault(channel.Id) / channelWeeks, byChannelPre.GetValueOrDefault(channel.Id) / channelWeeksPre,
            done.Average(t => (double)t.Spent.GetValueOrDefault(channel.Id)), value * 1000));
    }

    report.AppendLine();
    report.AppendLine(string.Format(culture, "- Canal que mais vezes foi o melhor destino: **{0:P1}** das semanas da campanha oficial (limite 40%), em {1:0.0} semanas por campanha. Semanas com verba repartida, por campanha: {2:0.0} de {3:0.0}.",
        maxChannel, done.Average(t => (double)t.BestChannelByWeek.Count), done.Average(t => (double)t.BudgetWeeks), done.Average(t => (double)t.Weeks)));
    // Diagnóstico do canal: as contas da melhor primeira parcela de cada canal, nas semanas da campanha oficial.
    report.AppendLine();
    report.AppendLine("| Canal (campanha oficial) | Melhor destino | Votos por R$ 1.000 | Retorno | Disputa no melhor alvo | Verba de rival no melhor alvo | Fatores (produto) | Saturação | Verba dos rivais no canal, por semana | Alvos com verba de rival | Verba de rival por alvo com verba | Disputa média nesses alvos |");
    report.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
    var diag = new Dictionary<string, ChannelDiag>();
    foreach (var (id, d) in done.SelectMany(t => t.ChannelDiag))
    {
        if (!diag.TryGetValue(id, out ChannelDiag? sum))
        {
            diag[id] = sum = new ChannelDiag();
        }

        sum.Add(d);
    }

    foreach (var channel in content.Campaign.Channels.Canais.OrderByDescending(c => byChannel.GetValueOrDefault(c.Id)))
    {
        if (!diag.TryGetValue(channel.Id, out ChannelDiag? d) || d.Weeks == 0)
        {
            continue;
        }

        double n = Math.Max(1, d.OptionWeeks);
        report.AppendLine(string.Format(culture, "| {0} | {1:P1} | {2:0.00} | {3:0.000} | {4:P0} | R$ {5:N0} | {6:0.00} | {7:0.00} | R$ {8:N0} | {9:P0} | R$ {10:N0} | {11:P0} |", channel.Nome,
            d.Wins / channelWeeks, d.VotesPerReal / n * 1000, d.Return / n, d.Share / n, d.RivalAtTarget / n, d.Yield / n, d.Saturation / n,
            d.RivalWeekly / d.Weeks, d.TargetsWithRival / Math.Max(1, d.Targets), d.RivalWeekly / Math.Max(1, d.TargetsWithRival), d.ShareWhereRival / Math.Max(1, d.TargetsWithRival)));
    }

    report.AppendLine();
    report.AppendLine(string.Format(culture, "- Onde cada canal venceu: {0}.", string.Join("; ", diag.Where(d => d.Value.Wins > 0).OrderByDescending(d => d.Value.Wins)
        .Select(d => $"{content.Campaign.Channels.Get(d.Key).Nome}: " + string.Join(", ", d.Value.WinTargets.OrderByDescending(w => w.Value).Select(w => string.Format(culture, "{0} {1:P0}", w.Key, w.Value / (double)d.Value.Wins)))))));
    if (diag.TryGetValue(ChannelDefinition.Cabos, out ChannelDiag? cabos) && cabos.Weeks > 0)
    {
        report.AppendLine(string.Format(culture, "- Cabos e a lei: limite legal médio de {0:0.0} pessoas por candidatura (art. 100-A; mínimo {1}, máximo {2}), em grupos de {3}; {4:P1} dos alvos-semana bloqueados pelo limite legal e {5:P1} por falta de vaga na equipe; sem alvo permitido em {6:P1} das semanas; o melhor alvo estava sem material gráfico em {7:P0} das semanas e com liderança contrária em {8:P0}.",
            done.Average(t => (double)t.CabosLimit), done.Min(t => t.CabosLimit), done.Max(t => t.CabosLimit), content.Campaign.Channels.Regras.CabosPorGrupo,
            cabos.BlockedLaw / Math.Max(1, cabos.Targets), cabos.BlockedSlot / Math.Max(1, cabos.Targets), 1 - (cabos.OptionWeeks / (double)cabos.Weeks),
            cabos.SemMaterial / Math.Max(1.0, cabos.OptionWeeks), cabos.LiderancaContraria / Math.Max(1.0, cabos.OptionWeeks)));
    }

    report.AppendLine(string.Format(culture, "- Caixa e verba: parcela de R$ {0:N0}; caixa de R$ {1:N0} no começo da campanha oficial; verba de canais paga na pré-campanha R$ {2:N0} e na campanha oficial R$ {3:N0} por campanha ({4:P0} do gasto total da campanha).",
        done.Average(t => (double)t.ChannelStep), done.Average(t => (double)t.CashAtOfficialStart), done.Average(t => (double)t.BudgetSpentPre), done.Average(t => (double)(t.BudgetSpentTotal - t.BudgetSpentPre)),
        done.Sum(t => (double)t.BudgetSpentTotal) / Math.Max(1, done.Sum(t => (double)t.CampaignSpent))));
    report.AppendLine(string.Format(culture, "- Decisões com troca: **{0:0.00}** por semana (mínimo 1). Por tipo, por campanha: {1}.",
        tradePerWeek, string.Join("; ", trades.OrderByDescending(t => t.Value).Select(t => string.Format(culture, "{0} {1:0.0}", t.Key, t.Value / (double)done.Count)))));
    report.AppendLine(string.Format(culture, "- Equipe: {0:0.0} contratações por campanha, {1:0.0} pessoas na equipe no fim, {2:0.0} eventos da equipe, {3:0.0} compromissos delegados. Plataforma: {4:0.0} mudanças de posição e {5:0.0} promessas locais.",
        done.Average(t => (double)t.Hires), done.Average(t => (double)t.TeamAtEnd), done.Average(t => (double)t.StaffEvents), done.Average(t => (double)t.Delegations), done.Average(t => (double)t.PositionChanges), done.Average(t => (double)t.PromisesMade)));
    report.AppendLine(string.Format(culture, "- Objetivos: {0:0.0} tipos por campanha (mínimo entre as campanhas: {1}; o princípio h pede 4), {2:0.0} cumpridos.",
        done.Average(t => (double)t.ObjectiveKinds), done.Min(t => t.ObjectiveKinds), done.Average(t => (double)t.ObjectivesDone)));
    // ---- R3: dinheiro, mundo, ataques, contas e desempenho.
    everything.AddRange(done);
    string Sources(IEnumerable<CampaignTrace> list) => string.Join("; ", list.SelectMany(t => t.Raised).GroupBy(r => r.Key).OrderByDescending(g => g.Sum(r => r.Value))
        .Select(g => string.Format(culture, "{0} R$ {1:N0}", g.Key.Replace('_', ' '), g.Sum(r => r.Value) / Math.Max(1, list.Count()))));
    List<CampaignTrace> clean = done.Where(t => !t.Dirty).ToList();
    List<CampaignTrace> dirty = done.Where(t => t.Dirty).ToList();
    double ms = done.SelectMany(t => t.WeekMs).DefaultIfEmpty(0).Average();
    double msMax = done.SelectMany(t => t.WeekMs).DefaultIfEmpty(0).Max();
    List<double> sortedMs = done.SelectMany(t => t.WeekMs).Order().ToList();
    double ms95 = sortedMs.Count == 0 ? 0 : sortedMs[(int)(sortedMs.Count * 0.95)];
    report.AppendLine();
    report.AppendLine(string.Format(culture, "- Dinheiro, campanhas limpas, por campanha: {0}. Gasto de R$ {1:N0}, {2:P0} do limite de gastos (R$ {3:N0}); risco de contas médio {4:0.0}.",
        Sources(clean), clean.Count == 0 ? 0 : clean.Average(t => (double)t.CampaignSpent), clean.Count == 0 ? 0 : clean.Average(t => t.Limit <= 0 ? 0 : (double)(t.CampaignSpent / t.Limit)), done.Average(t => (double)t.Limit), clean.Count == 0 ? 0 : clean.Average(t => (double)t.Risk)));
    report.AppendLine(string.Format(culture, "- Dinheiro, campanhas \"sujas\", por campanha: {0}. Gasto de R$ {1:N0}, {2:P0} do limite; \"por fora\" de {3:P0} do limite em média ({4} de {5} campanhas acima de 30%); risco de contas médio {6:0.0}.",
        Sources(dirty), dirty.Count == 0 ? 0 : dirty.Average(t => (double)t.CampaignSpent), dirty.Count == 0 ? 0 : dirty.Average(t => t.Limit <= 0 ? 0 : (double)(t.CampaignSpent / t.Limit)), dirty.Count == 0 ? 0 : dirty.Average(t => t.OffBooksShare),
        dirty.Count(t => t.OffBooksShare > 0.3), dirty.Count, dirty.Count == 0 ? 0 : dirty.Average(t => (double)t.Risk)));
    report.AppendLine(string.Format(culture, "- Ofertas de doadores respondidas, por campanha: {0}.",
        string.Join("; ", done.SelectMany(t => t.DonationAnswers).GroupBy(d => d.Key).OrderByDescending(g => g.Sum(d => d.Value)).Select(g => string.Format(culture, "{0} {1:0.0}", g.Key, g.Sum(d => d.Value) / (double)done.Count)))));
    report.AppendLine(string.Format(culture, "- Mundo: {0:0.0} notícias por semana que não citam o jogador (semana mais fraca: {1}); tema em alta em {2:0.0} de {3:0.0} semanas; {4:0.0} compromissos em tema em alta e {5:0.0} entrevistas em veículo por campanha. Trocas de plano dos rivais por campanha: {6}.",
        done.SelectMany(t => t.NewsByWeek.Skip(1)).DefaultIfEmpty(0).Average(), done.SelectMany(t => t.NewsByWeek.Skip(1)).DefaultIfEmpty(0).Min(), done.Average(t => (double)t.HotThemeWeeks), done.Average(t => (double)t.Weeks),
        done.Average(t => (double)t.HotThemeCommitments), done.Average(t => (double)t.OutletInterviews),
        string.Join("; ", done.SelectMany(t => t.PlanChanges).GroupBy(d => d.Key).OrderByDescending(g => g.Sum(d => d.Value)).Select(g => string.Format(culture, "{0} {1:0.0}", g.Key, g.Sum(d => d.Value) / (double)done.Count)))));
    report.AppendLine(string.Format(culture, "- Ataques: {0:0.0} recebidos por campanha ({1:0.0} boatos), {2:0.0} feitos. Respostas por campanha: {3}.",
        done.Average(t => (double)t.AttacksReceived), done.Average(t => (double)t.RumorsReceived), done.Average(t => (double)t.AttacksMade),
        string.Join("; ", done.SelectMany(t => t.Responses).GroupBy(d => d.Key).OrderByDescending(g => g.Sum(d => d.Value)).Select(g => string.Format(culture, "{0} {1:0.0}", g.Key.Replace('_', ' '), g.Sum(d => d.Value) / (double)done.Count)))));
    string Lost(List<CampaignTrace> list) => list.Sum(t => t.Replicas) == 0 ? "sem campanhas" : string.Format(culture, "{0:P1} ({1} campanhas, {2} réplicas; representação aberta em {3:P1})",
        list.Sum(t => t.MandatesLost) / (double)list.Sum(t => t.Replicas), list.Count, list.Sum(t => t.Replicas), list.Sum(t => t.Representations) / (double)list.Sum(t => t.Replicas));
    report.AppendLine(string.Format(culture, "- Contas, mandato perdido em 2 anos: limpas {0}; \"por fora\" acima de 30% do limite {1}; \"por fora\" até 30% {2}.",
        Lost(done.Where(t => t.OffBooksShare <= 0).ToList()), Lost(done.Where(t => t.OffBooksShare > 0.3).ToList()), Lost(done.Where(t => t.OffBooksShare is > 0 and <= 0.3).ToList())));
    report.AppendLine(string.Format(culture, "- Julgamento das contas (réplicas): {0}.",
        string.Join("; ", done.SelectMany(t => t.Verdicts).GroupBy(d => d.Key).OrderByDescending(g => g.Sum(d => d.Value)).Select(g => string.Format(culture, "{0} {1:P0}", g.Key, g.Sum(d => d.Value) / (double)Math.Max(1, done.Sum(t => t.Replicas)))))));
    report.AppendLine(string.Format(culture, "- Simulação semanal (virada de domingo, com as campanhas rodando em paralelo): média de {0:0.0} ms, 95% abaixo de {1:0.0} ms, máximo de {2:0.0} ms.", ms, ms95, msMax));
    report.AppendLine($"- **{(ok ? "Dentro dos critérios" : "FORA dos critérios")}.**");
    report.AppendLine();
    string winner = byChannel.Count == 0 ? "-" : content.Campaign.Channels.Get(byChannel.MaxBy(c => c.Value).Key).Nome;
    List<CampaignTrace> cleanPorte = done.Where(t => t.OffBooksShare <= 0).ToList();
    List<CampaignTrace> dirtyPorte = done.Where(t => t.OffBooksShare > 0.3).ToList();
    string LostShare(List<CampaignTrace> list) => list.Sum(t => t.Replicas) == 0 ? "-" : string.Format(culture, "{0:P1}", list.Sum(t => t.MandatesLost) / (double)list.Sum(t => t.Replicas));
    summary.Add(string.Format(culture, "| {0} | {1:P1} | {2:P1} | {3} {4:P1} | {5:0.00} | {6} | {7} | {8} | {9:P1} | {10} | {11} | {12:0} s |", porte, maxType, pooledPair, winner, maxChannel, tradePerWeek,
        Elected(strong), Elected(drawn), idleDone.Count == 0 ? "não medido" : $"{Elected(idleDone.Where(t => t.Strongest))} e {Elected(idleDone.Where(t => !t.Strongest))}",
        done.Sum(t => t.ForcedTurns) / (double)Math.Max(1, done.Sum(t => t.Turns)), LostShare(cleanPorte), LostShare(dirtyPorte), playedSeconds));
    Console.WriteLine($"{porte}: eleitos {Elected(strong)} (mais forte), {Elected(drawn)} (sorteado), sem campanha {idleText}; canal {winner}");
    Console.WriteLine($"{porte}: tipo {maxType:P1}, par {pooledPair:P1}, canal {maxChannel:P1}, decisões {tradePerWeek:0.00}/semana, semana {ms:0.0} ms (máx. {msMax:0.0}), {(ok ? "ok" : "FORA")}");
    if (output is { Length: > 0 })
    {
        // O relatório parcial fica no arquivo a cada porte: a rodada inteira leva horas.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, report.ToString().ReplaceLineEndings("\n"), new UTF8Encoding(false));
    }
}

// Contas (GDD 17.12), no conjunto dos portes.
double Share(List<CampaignTrace> list) => list.Sum(t => t.Replicas) == 0 ? double.NaN : list.Sum(t => t.MandatesLost) / (double)list.Sum(t => t.Replicas);
List<CampaignTrace> cleanAll = everything.Where(t => t.OffBooksShare <= 0).ToList();
List<CampaignTrace> dirtyAll = everything.Where(t => t.OffBooksShare > 0.3).ToList();
double cleanLost = Share(cleanAll);
double dirtyLost = Share(dirtyAll);
bool accountsOk = cleanLost < 0.01 && dirtyLost is >= 0.25 and <= 0.40;
allOk &= accountsOk;
report.AppendLine("## Resumo");
report.AppendLine();
report.AppendLine("| Porte | Tipo de compromisso (até 40%) | Par tipo e zona (até 25%) | Canal vencedor e fatia (até 40%) | Decisões com troca por semana (1 ou mais) | Eleitos no partido mais forte | Eleitos em partido sorteado | Eleitos sem campanha (mais forte e sorteado) | Turnos forçados | Mandato perdido, contas limpas | Mandato perdido, \"por fora\" acima de 30% | Tempo de máquina |");
report.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
foreach (string line in summary)
{
    report.AppendLine(line);
}

report.AppendLine();
report.AppendLine(string.Format(culture, "- Contas, campanhas limpas: mandato perdido em **{0:P2}** das carreiras ({1} campanhas; critério: menos de 1%).", cleanLost, cleanAll.Count));
report.AppendLine(string.Format(culture, "- Contas, campanhas com mais de 30% do limite \"por fora\": mandato perdido em **{0:P1}** das carreiras em até 2 anos ({1} campanhas, \"por fora\" médio de {2:P0} do limite; critério: de 25% a 40%).", dirtyLost, dirtyAll.Count, dirtyAll.Count == 0 ? 0 : dirtyAll.Average(t => t.OffBooksShare)));
report.AppendLine($"- **Contas {(accountsOk ? "dentro do critério" : "FORA do critério")}.**");
report.AppendLine();
Console.WriteLine($"contas: limpas {cleanLost:P2} ({cleanAll.Count}), por fora > 30% {dirtyLost:P1} ({dirtyAll.Count}), {(accountsOk ? "ok" : "FORA")}");

string text = report.ToString().ReplaceLineEndings("\n");
if (output is { Length: > 0 })
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
    File.WriteAllText(output, text, new UTF8Encoding(false));
}

Console.WriteLine();
Console.WriteLine(text);
return allOk ? 0 : 1;
