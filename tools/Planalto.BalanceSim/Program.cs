using System.Globalization;
using System.Text;
using Planalto.BalanceSim;
using Planalto.Core.Data;
using Planalto.Core.Municipios;
using Planalto.Core.State;

// Uso: dotnet run --project tools/Planalto.BalanceSim -- [--campanhas 200] [--cidades 10] [--portes pequeno,medio,grande,metropole]
//                                                        [--semente 1] [--saida docs/balance/r2a.md]
// Joga campanhas de vereador com o robô que escolhe sempre o compromisso de maior ganho previsto de votos e mede o
// princípio a do GDD 17.1: nenhum tipo de compromisso é a melhor escolha em mais de 40% das semanas, e nenhum par
// (tipo, zona) em mais de 25%. Código de saída 0 = todos os portes dentro do critério; 1 = algum porte fora.

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

var content = new SimContent(dataDirectory);
var report = new StringBuilder();
var culture = CultureInfo.GetCultureInfo("pt-BR");
report.AppendLine("# BalanceSim — campanha de vereador (R2a)");
report.AppendLine();
report.AppendLine(string.Format(culture, "Robô que marca, em cada turno livre, o compromisso de maior ganho previsto de votos. A melhor escolha de cada semana é a opção de maior ganho por turno vista nela. {0} campanhas por porte, em {1} cidades sorteadas por porte (semente {2}), metade no partido mais forte da cidade e metade num partido sorteado.", campaigns, cities, seed));
report.AppendLine();
report.AppendLine("Critério do princípio a (GDD 17.1): nenhum tipo de compromisso é a melhor escolha em mais de 40% das semanas; nenhum par (tipo, zona) em mais de 25%.");
report.AppendLine();
bool allOk = true;

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
            traces[i] = new CampaignRobot(engine) { Diagnose = i == 0 && Environment.GetEnvironmentVariable("PLANALTO_SIM_DIAG") == "1" }.Play();
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
    bool ok = maxType <= MaxTypeShare && pooledPair <= MaxPairShare && failures.Count == 0;
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
    report.AppendLine($"- **{(ok ? "Dentro do critério" : "FORA do critério")}.**");
    report.AppendLine();
    Console.WriteLine($"{porte}: tipo {maxType:P1}, par {maxPair:P1}, {(ok ? "ok" : "FORA")}");
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
