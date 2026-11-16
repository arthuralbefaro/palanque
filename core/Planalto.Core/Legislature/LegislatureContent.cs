using Planalto.Core.Campaign;
using Planalto.Core.Data;
using Planalto.Core.Ideology;

namespace Planalto.Core.Legislature;

/// <summary>Quórum de aprovação: maioria dos presentes, maioria absoluta da casa ou dois terços.</summary>
public enum Quorum
{
    Simples,
    Absoluta,
    DoisTercos,
}

public enum BillAuthorKind
{
    Executivo,
    Vereador,
}

/// <summary>Modelo de projeto de lei (data/bills/). Popularidade: reação de cada grupo de eleitores ao voto SIM (-2 a 2).</summary>
public sealed class BillTemplate
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }

    public required BillAuthorKind Author { get; init; }

    /// <summary>Tema da seção 5.2 afetado, ou null.</summary>
    public string? Theme { get; init; }

    /// <summary>Onde o projeto fica nos eixos econômico e de costumes.</summary>
    public required IdeologicalPosition Position { get; init; }

    public required Dictionary<string, int> Popularity { get; init; }

    /// <summary>Quanto o eleitor presta atenção (0 a 1).</summary>
    public required double Salience { get; init; }

    public required Quorum Quorum { get; init; }
}

public sealed class BillCatalog : IValidatable
{
    public required List<BillTemplate> Bills { get; init; }

    public BillTemplate? Find(string id) => Bills.FirstOrDefault(b => b.Id == id);

    public IEnumerable<string> Validate()
    {
        foreach (string id in Bills.GroupBy(b => b.Id).Where(g => g.Count() > 1).Select(g => g.Key))
        {
            yield return $"projeto '{id}' repetido.";
        }

        foreach (BillTemplate b in Bills)
        {
            if (b.Salience is < 0 or > 1)
            {
                yield return $"{b.Id}: salience entre 0 e 1.";
            }

            if (b.Popularity.Values.Any(v => v is < -2 or > 2))
            {
                yield return $"{b.Id}: popularidade de -2 a 2.";
            }

            if (b.Position.Economic is < AxisRange.AxisMin or > AxisRange.AxisMax || b.Position.Social is < AxisRange.AxisMin or > AxisRange.AxisMax)
            {
                yield return $"{b.Id}: posição fora dos eixos.";
            }
        }

        if (!Bills.Any(b => b.Author == BillAuthorKind.Executivo) || !Bills.Any(b => b.Author == BillAuthorKind.Vereador))
        {
            yield return "é preciso ao menos um projeto do Executivo e um de vereador.";
        }
    }
}

public sealed class LegislatureVoteConfig
{
    public required double IdeologyWeight { get; init; }

    public required double OrientationWeight { get; init; }

    public required double GovernmentWeight { get; init; }

    public required double RelationWeight { get; init; }

    /// <summary>Distância ideológica (pontos nos eixos) em que o vereador fica indiferente ao projeto.</summary>
    public required double NeutralDistance { get; init; }

    public required double NoiseSd { get; init; }

    /// <summary>Faixa de indecisão (no logito) em que o NPC prefere se abster.</summary>
    public required double AbstainBand { get; init; }

    /// <summary>Abaixo deste valor absoluto, o partido libera a bancada.</summary>
    public required double OrientationThreshold { get; init; }
}

public sealed class FidelityConfig
{
    public required int Start { get; init; }

    public required int LossPerDefiance { get; init; }

    public required int LossPerAbstention { get; init; }

    public required int GainPerFollow { get; init; }

    public required int WarningBelow { get; init; }

    public required int SanctionBelow { get; init; }

    public required int ExpulsionBelow { get; init; }
}

public sealed class RelationConfig
{
    public required int MayorWith { get; init; }

    public required int MayorAgainst { get; init; }

    public required int AuthorWith { get; init; }

    public required int AuthorAgainst { get; init; }

    /// <summary>Quanto as relações voltam para zero por semana.</summary>
    public required double DriftPerWeek { get; init; }
}

public sealed class CapitalConfig
{
    public required int Start { get; init; }

    public required int OwnBillApproved { get; init; }

    public required int WithGovernmentInBase { get; init; }

    public required int ProposeCost { get; init; }

    public required int UrgencyCost { get; init; }
}

public sealed class PublicOpinionConfig
{
    /// <summary>Apoio do grupo por ponto de popularidade × saliência.</summary>
    public required double SupportPerPoint { get; init; }

    /// <summary>Imagem por ponto de popularidade líquida × saliência.</summary>
    public required double ImagePerPoint { get; init; }

    public required double OwnBillMultiplier { get; init; }

    public required double SupportDecayPerWeek { get; init; }

    public required double RecognitionDecayPerWeek { get; init; }
}

public sealed class AbsenceConfig
{
    public required double Image { get; init; }

    public required int Fidelity { get; init; }
}

public sealed class BaseConfig
{
    /// <summary>Distância ideológica máxima para um partido entrar na base do prefeito.</summary>
    public required double MaxDistance { get; init; }

    /// <summary>Cadeiras além da metade que o prefeito busca ao montar a base.</summary>
    public required int MajorityMargin { get; init; }
}

/// <summary>Parâmetros do mandato legislativo (data/legislature/config.json).</summary>
public sealed class LegislatureConfig : IValidatable
{
    public required int WeeklyEnergy { get; init; }

    public required List<int> RecessMonths { get; init; }

    public required IntRange PlenaryPerWeek { get; init; }

    public required IntRange CommitteeWeeks { get; init; }

    public required double ExecutiveBillsPerWeek { get; init; }

    public required double NpcBillsPerWeek { get; init; }

    public required double CommitteePassChance { get; init; }

    public required double CommitteePassPerRelation { get; init; }

    public required double CommitteeMemberBonus { get; init; }

    public required LegislatureVoteConfig Vote { get; init; }

    public required Dictionary<string, double> Discipline { get; init; }

    public required BaseConfig Base { get; init; }

    public required FidelityConfig Fidelity { get; init; }

    public required RelationConfig Relations { get; init; }

    public required CapitalConfig Capital { get; init; }

    public required PublicOpinionConfig Public { get; init; }

    public required AbsenceConfig Absence { get; init; }

    public required int EmendasPerYear { get; init; }

    public required List<CommitteeDefinition> Committees { get; init; }

    /// <summary>No ano da eleição, o mandato segue sozinho enquanto o jogador faz campanha.</summary>
    public required bool AutoVoteInCampaign { get; init; }

    public IEnumerable<string> Validate()
    {
        if (WeeklyEnergy <= 0 || PlenaryPerWeek.Min < 0 || PlenaryPerWeek.Max < PlenaryPerWeek.Min || CommitteeWeeks.Min < 1 || CommitteeWeeks.Max < CommitteeWeeks.Min)
        {
            yield return "energia positiva; faixas de plenário e de comissão válidas.";
        }

        if (RecessMonths.Any(m => m is < 1 or > 12))
        {
            yield return "recess_months: meses de 1 a 12.";
        }

        if (ExecutiveBillsPerWeek < 0 || NpcBillsPerWeek < 0 || CommitteePassChance is < 0 or > 1)
        {
            yield return "taxas de projetos não negativas e chance de comissão entre 0 e 1.";
        }

        if (Vote.NeutralDistance <= 0 || Vote.NoiseSd < 0 || Vote.AbstainBand < 0)
        {
            yield return "vote: neutral_distance positiva, ruído e faixa de abstenção não negativos.";
        }

        foreach (string key in new[] { "rigida", "moderada", "frouxa" }.Where(k => !Discipline.ContainsKey(k)))
        {
            yield return $"discipline: falta '{key}'.";
        }

        if (Fidelity.ExpulsionBelow > Fidelity.SanctionBelow || Fidelity.SanctionBelow > Fidelity.WarningBelow || Fidelity.WarningBelow > Fidelity.Start)
        {
            yield return "fidelity: expulsão <= sanção <= advertência <= início.";
        }

        if (Committees.Count == 0 || Committees.Select(c => c.Id).Distinct().Count() != Committees.Count)
        {
            yield return "committees: ao menos uma, com ids únicos.";
        }

        if (EmendasPerYear < 0 || Capital.ProposeCost < 0 || Capital.UrgencyCost < 0)
        {
            yield return "emendas e custos de capital não negativos.";
        }
    }
}

/// <summary>Comissão permanente da Câmara; projetos dos seus temas passam mais fácil se o autor é membro.</summary>
public sealed class CommitteeDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required List<string> Themes { get; init; }
}

public enum MandateActionKind
{
    Geral,
    Articular,
    Propor,
    Urgencia,
    Emenda,
}

/// <summary>Ação de gabinete durante o mandato (data/legislature/actions.json).</summary>
public sealed class MandateActionDefinition
{
    public required string Id { get; init; }

    public required MandateActionKind Kind { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required int Energy { get; init; }

    /// <summary>Custo em capital político (negativo = ganha).</summary>
    public required int Capital { get; init; }

    public required Effects Effects { get; init; }

    public List<ActionTarget> Targets { get; init; } = [];

    public ActionTest? Test { get; init; }

    /// <summary>Mudança na relação com o colega escolhido (ação de articulação).</summary>
    public int Relation { get; init; }

    public int RelationMayor { get; init; }
}

public sealed class MandateActionCatalog : IValidatable
{
    public required List<MandateActionDefinition> Actions { get; init; }

    public MandateActionDefinition? Find(string id) => Actions.FirstOrDefault(a => a.Id == id);

    public IEnumerable<string> Validate()
    {
        foreach (string id in Actions.GroupBy(a => a.Id).Where(g => g.Count() > 1).Select(g => g.Key))
        {
            yield return $"ação '{id}' repetida.";
        }

        foreach (MandateActionDefinition a in Actions)
        {
            if (a.Energy <= 0)
            {
                yield return $"{a.Id}: energia positiva.";
            }

            if (a.Kind == MandateActionKind.Emenda && a.Targets.Count == 0)
            {
                yield return $"{a.Id}: emenda precisa de destinos.";
            }
        }
    }
}

/// <summary>Textos da previsão do voto do vereador (data/legislature/texts.json).</summary>
public sealed class LegislatureTextCatalog : IValidatable
{
    public static readonly IReadOnlyList<string> TextKeys =
    [
        "previsao_voto", "previsao_voto_segue", "previsao_voto_contraria", "previsao_voto_abstem", "previsao_voto_liberado", "previsao_voto_sem_partido",
        "previsao_voto_limites", "previsao_voto_executivo", "previsao_voto_autor", "previsao_voto_publico", "previsao_voto_abstencao_publico", "previsao_voto_acordo",
    ];

    public required string Note { get; init; }

    public required Dictionary<string, string> Textos { get; init; }

    public string Texto(string key) => Textos.TryGetValue(key, out string? text) ? text : key;

    public IEnumerable<string> Validate()
    {
        foreach (string key in TextKeys.Where(k => !Textos.ContainsKey(k) || string.IsNullOrWhiteSpace(Textos[k])))
        {
            yield return $"textos: falta '{key}'.";
        }
    }
}

/// <summary>Conteúdo do mandato legislativo: configuração, ações, projetos municipais e os textos da previsão do voto.</summary>
public sealed class LegislatureContent
{
    public const string ConfigFile = "legislature/config.json";
    public const string ActionsFile = "legislature/actions.json";
    public const string MunicipalBillsFile = "bills/municipal.json";
    public const string TextsFile = "legislature/texts.json";

    public required LegislatureConfig Config { get; init; }

    public required MandateActionCatalog Actions { get; init; }

    public required BillCatalog MunicipalBills { get; init; }

    public required LegislatureTextCatalog Texts { get; init; }

    /// <param name="groups">Ids de grupos de segmento válidos (data/electorate/segments.json).</param>
    /// <param name="themes">Ids de temas válidos (data/themes.json).</param>
    public static IReadOnlyList<DataFileReport> Load(string dataDirectory, IReadOnlySet<string>? groups, IReadOnlySet<string>? themes, out LegislatureContent? content)
    {
        var config = JsonData.LoadFile<LegislatureConfig>(Path.Combine(dataDirectory, ConfigFile));
        var actions = JsonData.LoadFile<MandateActionCatalog>(Path.Combine(dataDirectory, ActionsFile));
        var bills = JsonData.LoadFile<BillCatalog>(Path.Combine(dataDirectory, MunicipalBillsFile));
        var texts = JsonData.LoadFile<LegislatureTextCatalog>(Path.Combine(dataDirectory, TextsFile));

        var actionErrors = actions.Errors.ToList();
        var billErrors = bills.Errors.ToList();
        if (groups is not null)
        {
            actionErrors.AddRange((actions.Value?.Actions ?? [])
                .SelectMany(a => a.Targets.Select(t => t.Group).Concat(a.Effects.Support.Keys).Select(g => (a.Id, g)))
                .Where(x => !groups.Contains(x.g))
                .Select(x => $"{x.Id}: grupo '{x.g}' inexistente."));
            billErrors.AddRange((bills.Value?.Bills ?? [])
                .SelectMany(b => b.Popularity.Keys.Select(g => (b.Id, g)))
                .Where(x => !groups.Contains(x.g))
                .Select(x => $"{x.Id}: grupo '{x.g}' inexistente."));
        }

        if (themes is not null)
        {
            billErrors.AddRange((bills.Value?.Bills ?? [])
                .Where(b => b.Theme is not null && !themes.Contains(b.Theme))
                .Select(b => $"{b.Id}: tema '{b.Theme}' inexistente."));
        }

        var reports = new List<DataFileReport>
        {
            new(ConfigFile, 1, config.Errors),
            new(ActionsFile, actions.Value?.Actions.Count ?? 0, actionErrors),
            new(MunicipalBillsFile, bills.Value?.Bills.Count ?? 0, billErrors),
            new(TextsFile, texts.Value?.Textos.Count ?? 0, texts.Errors),
        };
        content = reports.All(r => r.IsValid) && config.Value is not null && actions.Value is not null && bills.Value is not null && texts.Value is not null
            ? new LegislatureContent { Config = config.Value, Actions = actions.Value, MunicipalBills = bills.Value, Texts = texts.Value }
            : null;
        return reports;
    }

    public static LegislatureContent LoadOrThrow(string dataDirectory, IReadOnlySet<string>? groups = null, IReadOnlySet<string>? themes = null)
    {
        IReadOnlyList<DataFileReport> reports = Load(dataDirectory, groups, themes, out LegislatureContent? content);
        return content ?? throw new DataValidationException(reports.SelectMany(r => r.Errors).ToList());
    }
}
