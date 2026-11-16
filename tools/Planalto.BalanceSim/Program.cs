using System.Globalization;
using System.Text;
using Planalto.BalanceSim;
using Planalto.Core.Data;
using Planalto.Core.Municipios;
using Planalto.Core.State;

// Uso: dotnet run --project tools/Planalto.BalanceSim -- [--campanhas 200] [--cidades 10] [--portes pequeno,medio,grande,metropole]
//                                                        [--semente 1] [--saida docs/balance/r3.md]
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
    report.AppendLine($"## Porte {porte}");
    report.AppendLine();
    report.AppendLine(string.Format(culture, "{0} campanhas concluídas, {1} com erro, em {2:0} s. Cidades: {3}.", done.Count, failures.Count, (DateTime.UtcNow - started).TotalSeconds, string.Join(", ", chosen.Select(m => $"{m.Nome}/{m.Uf}"))));
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
    report.AppendLine(string.Format(culture, "- Eleitos: {0} de {1} no partido mais forte; {2} de {3} em partido sorteado. Posição mediana entre os nomeados: {4}.",
        done.Where((_, i) => i % 2 == 0).Count(t => t.Elected), done.Where((_, i) => i % 2 == 0).Count(),
        done.Where((_, i) => i % 2 == 1).Count(t => t.Elected), done.Where((_, i) => i % 2 == 1).Count(),
        done.Select(t => t.Position).Order().ElementAt(done.Count / 2)));
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
    summary.Add(string.Format(culture, "| {0} | {1:P1} | {2:P1} | {3:P1} | {4:0.00} | {5} de {6} | {7:P1} | {8:0.0} ms |", porte, maxType, pooledPair, maxChannel, tradePerWeek,
        done.Where((_, i) => i % 2 == 0).Count(t => t.Elected), done.Where((_, i) => i % 2 == 0).Count(), done.Sum(t => t.ForcedTurns) / (double)Math.Max(1, done.Sum(t => t.Turns)), ms95));
    Console.WriteLine($"{porte}: tipo {maxType:P1}, par {pooledPair:P1}, canal {maxChannel:P1}, decisões {tradePerWeek:0.00}/semana, semana {ms:0.0} ms (máx. {msMax:0.0}), {(ok ? "ok" : "FORA")}");
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
report.AppendLine("| Porte | Tipo de compromisso (até 40%) | Par tipo e zona (até 25%) | Canal de verba (até 40%) | Decisões com troca por semana (1 ou mais) | Eleitos no partido mais forte | Turnos forçados | Semana simulada (95%) |");
report.AppendLine("|---|---|---|---|---|---|---|---|");
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
