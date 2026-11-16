using Planalto.Core.Data;
using Planalto.Core.Elections;

namespace Planalto.Core.Campaign;

/// <summary>
/// Regras de lei dos canais pagos de campanha, com o padrão da lei vigente e o artigo citado.
/// ATENÇÃO: os artigos abaixo foram citados de memória. A conferência item por item no texto oficial é tarefa da R3
/// (ROADMAP); até lá <see cref="ConferidoNoTextoOficial"/> fica falso.
/// </summary>
public sealed class ChannelLawConfig
{
    public static ChannelLawConfig Default { get; } = new();

    /// <summary>Os números e artigos deste objeto já foram conferidos no texto oficial.</summary>
    public bool ConferidoNoTextoOficial { get; init; }

    public string Conferencia { get; init; } = "Artigos citados de memória; a conferência no texto oficial é da R3.";

    // ---- Contratação de pessoal de rua (cabos eleitorais)

    public string ArtigoCabos { get; init; } = "Lei 9.504/1997, art. 100-A";

    /// <summary>Até este eleitorado o limite é uma fração do eleitorado (art. 100-A, I).</summary>
    public long EleitoradoFaixaBase { get; init; } = 30_000;

    public double FracaoAteFaixaBase { get; init; } = 0.01;

    /// <summary>Acima da faixa: o máximo da faixa mais 1 contratação a cada tantos eleitores excedentes (art. 100-A, II).</summary>
    public long EleitoresPorContratacaoExtra { get; init; } = 1_000;

    /// <summary>Fração do limite do município que vale para cada cargo municipal (art. 100-A, § 1º, V e VI).</summary>
    public IReadOnlyDictionary<Office, double> FracaoPorCargo { get; init; } = new Dictionary<Office, double>
    {
        [Office.Prefeito] = 1.0,
        [Office.Vereador] = 0.5,
    };

    /// <summary>Deputado federal: fração do limite do município com mais eleitores do estado (§ 1º, III).</summary>
    public double FracaoDeputadoFederal { get; init; } = 0.7;

    /// <summary>Deputado estadual: fração do limite do deputado federal (§ 1º, IV).</summary>
    public double FracaoDeputadoEstadual { get; init; } = 0.5;

    /// <summary>Vereador: teto como fração do limite do deputado estadual (§ 1º, VI, parte final).</summary>
    public double TetoVereadorSobreDeputadoEstadual { get; init; } = 0.8;

    /// <summary>Limite do município (caput): contratações para uma candidatura a prefeito.</summary>
    public int LimiteMunicipio(long eleitorado) =>
        eleitorado <= EleitoradoFaixaBase
            ? (int)Math.Floor(eleitorado * FracaoAteFaixaBase)
            : (int)Math.Floor(EleitoradoFaixaBase * FracaoAteFaixaBase) + (int)((eleitorado - EleitoradoFaixaBase) / EleitoresPorContratacaoExtra);

    /// <summary>
    /// Máximo de pessoas contratadas para rua por uma candidatura no município. O teto do vereador depende do
    /// município com mais eleitores do estado; sem esse número, vale só a fração do cargo.
    /// </summary>
    public int LimiteCabos(Office office, long eleitorado, long? maiorEleitoradoDaUf = null)
    {
        int limit = (int)Math.Floor(LimiteMunicipio(eleitorado) * FracaoPorCargo.GetValueOrDefault(office, 1.0));
        if (office == Office.Vereador && maiorEleitoradoDaUf is { } maior)
        {
            double deputadoEstadual = Math.Floor(Math.Floor(LimiteMunicipio(maior) * FracaoDeputadoFederal) * FracaoDeputadoEstadual);
            limit = Math.Min(limit, (int)Math.Floor(deputadoEstadual * TetoVereadorSobreDeputadoEstadual));
        }

        return Math.Max(0, limit);
    }

    // ---- Impulsionamento de conteúdo na internet

    public string ArtigoImpulsionamento { get; init; } = "Lei 9.504/1997, art. 57-C";

    /// <summary>Propaganda paga na internet é vedada, salvo o impulsionamento identificado, contratado por candidato, partido ou coligação (caput).</summary>
    public bool ImpulsionamentoIdentificadoEContratadoPelaCampanha { get; init; } = true;

    /// <summary>Só para promover o próprio candidato ou partido, nunca contra adversário (§ 3º).</summary>
    public bool ImpulsionamentoSoParaPromover { get; init; } = true;

    /// <summary>Impulsionamento é propaganda eleitoral: só a partir do início oficial da campanha (art. 36).</summary>
    public bool ImpulsionamentoSoNaCampanha { get; init; } = true;

    // ---- Gastos vedados

    public const string Radio = "radio";
    public const string Tv = "tv";
    public const string Outdoor = "outdoor";
    public const string Brinde = "brinde";

    /// <summary>Tipos de gasto que a lei veda, com o artigo: nunca existem como canal de verba.</summary>
    public IReadOnlyDictionary<string, string> GastosVedados { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Radio] = "Lei 9.504/1997, art. 44 (propaganda paga em rádio)",
        [Tv] = "Lei 9.504/1997, art. 44 (propaganda paga em televisão)",
        [Outdoor] = "Lei 9.504/1997, art. 39, § 8º (outdoor)",
        [Brinde] = "Lei 9.504/1997, art. 39, § 6º (brindes)",
    };

    public bool GastoPermitido(string tipo) => !GastosVedados.ContainsKey(tipo);
}

/// <summary>O que o canal pode na pré-campanha: um teto de verba (sem impulsionamento, material institucional) e o quanto rende.</summary>
public sealed class ChannelPreCampaign
{
    /// <summary>Verba semanal máxima no porte médio.</summary>
    public required decimal Teto { get; init; }

    public required double Fator { get; init; }
}

/// <summary>Canal pago de campanha (GDD 17.8).</summary>
public sealed class ChannelDefinition
{
    public const string Rua = "rua";
    public const string Redes = "redes";
    public const string Material = "material";
    public const string Cabos = "cabos";

    public const string AlvoZona = "zona";
    public const string AlvoIdade = "idade";

    public required string Id { get; init; }

    public required string Nome { get; init; }

    public required string Descricao { get; init; }

    /// <summary>"zona" ou "idade".</summary>
    public required string Alvo { get; init; }

    /// <summary>Presença que o efeito 1 rende (pontos na cidade inteira, como os compromissos).</summary>
    public required double KPresenca { get; init; }

    /// <summary>Apoio que o efeito 1 rende.</summary>
    public required double KApoio { get; init; }

    /// <summary>A verba acima do teto da pré-campanha é impulsionamento pago (regras do art. 57-C).</summary>
    public bool Impulsionamento { get; init; }

    /// <summary>Contratado em grupos de pessoas (cabos), com limite legal.</summary>
    public bool EmGrupos { get; init; }

    public string? EquipeFuncao { get; init; }

    public string? EquipeEfeito { get; init; }

    /// <summary>null = o canal só abre com a campanha oficial.</summary>
    public ChannelPreCampaign? PreCampanha { get; init; }
}

/// <summary>Faixa de idade que o canal Redes pode mirar.</summary>
public sealed class AgeBand
{
    public required string Id { get; init; }

    public required string Nome { get; init; }

    /// <summary>Grupos de eleitores (dimensão idade) da faixa.</summary>
    public required List<string> Grupos { get; init; }

    /// <summary>Quanto as redes rendem nessa faixa (1 = melhor).</summary>
    public required double Peso { get; init; }
}

public sealed class ChannelRules
{
    /// <summary>Verba semanal, no porte médio, em que o retorno vale 1.</summary>
    public required decimal Escala { get; init; }

    public required double ExpoenteDisputa { get; init; }

    public required Parties.DoubleRange Sorte { get; init; }

    /// <summary>Múltiplo da verba que a tela oferece, no porte médio.</summary>
    public required decimal Passo { get; init; }

    /// <summary>Teto do apoio que a verba junta de um público; perto dele, rende menos.</summary>
    public required double ApoioTeto { get; init; }

    public required int CabosPorGrupo { get; init; }

    /// <summary>Custo semanal de um grupo de cabos, no porte médio.</summary>
    public required decimal CabosCustoGrupo { get; init; }

    /// <summary>Com cabos na zona, a presença do jogador ali cai só esta fração do normal.</summary>
    public required double CabosFatorDecaimento { get; init; }

    public required double CabosSemMaterial { get; init; }

    public required int LiderancaContrariaInfluencia { get; init; }

    public required double LiderancaContrariaFator { get; init; }

    public required double RuaFatorRural { get; init; }

    /// <summary>Bônus dos compromissos de <see cref="RuaMultiplica"/> na zona com verba de Rua (no retorno 1).</summary>
    public required double RuaBonusAgenda { get; init; }

    public required List<string> RuaMultiplica { get; init; }

    /// <summary>Verba de Rua na zona, no porte médio, que o comício exige.</summary>
    public required decimal ComicioVerbaRua { get; init; }

    /// <summary>Verba semanal de cada rival nomeado, no porte médio, antes do empenho.</summary>
    public required decimal RivalVerba { get; init; }

    public required Dictionary<string, double> RivalReparte { get; init; }

    public required double RivalPreCampanha { get; init; }

    /// <summary>Quanto sobra, a cada semana, da verba a mais que um rival ganhou (cabos dispensados pelo jogador).</summary>
    public required double RivalExtraDecaimento { get; init; }

    /// <summary>
    /// Saturação (princípio g): cada semana de verba no mesmo canal e alvo soma 1; o efeito é multiplicado por
    /// 1 ÷ (1 + fator × saturação).
    /// </summary>
    public required double SaturacaoFator { get; init; }

    /// <summary>Quanto a saturação de cada canal e alvo cai por semana.</summary>
    public required double SaturacaoDecaimentoSemanal { get; init; }
}

/// <summary>data/campaign/channels.json.</summary>
public sealed class ChannelCatalog : IValidatable
{
    public const string File = "campaign/channels.json";

    public required string Note { get; init; }

    public required ChannelRules Regras { get; init; }

    public required List<ChannelDefinition> Canais { get; init; }

    public required List<AgeBand> FaixasIdade { get; init; }

    public required Dictionary<string, string> Textos { get; init; }

    public ChannelDefinition? Find(string id) => Canais.FirstOrDefault(c => c.Id == id);

    public ChannelDefinition Get(string id) => Canais.First(c => c.Id == id);

    public AgeBand? Band(string id) => FaixasIdade.FirstOrDefault(b => b.Id == id);

    public string Texto(string key) => Textos.TryGetValue(key, out string? text) ? text : key;

    public static readonly IReadOnlyList<string> TextKeys =
    [
        "conta_retorno", "conta_disputa", "efeito", "presenca", "presenca_cidade", "apoio", "apoio_zona", "cobranca", "fator_rural", "fator_faixa",
        "fator_pre_campanha", "fator_sem_material", "fator_lideranca_contraria", "fator_verba_rua", "fator_saturacao", "sem_estrutura", "cabos_seguram", "cabos_limite",
        "cabos_dispensados", "sem_caixa", "resultado",
    ];

    public IEnumerable<string> Validate()
    {
        foreach (string error in DataChecks.UniqueIds(Canais.Select(c => c.Id), "Canal").Concat(DataChecks.UniqueIds(FaixasIdade.Select(b => b.Id), "Faixa de idade")))
        {
            yield return error;
        }

        foreach (string id in new[] { ChannelDefinition.Rua, ChannelDefinition.Redes, ChannelDefinition.Material, ChannelDefinition.Cabos }.Where(id => Canais.All(c => c.Id != id)))
        {
            yield return $"falta o canal '{id}'.";
        }

        foreach (ChannelDefinition c in Canais)
        {
            // Rádio, TV, outdoor e brinde não podem virar canal de verba: a lei veda o gasto.
            if (!ChannelLawConfig.Default.GastoPermitido(c.Id))
            {
                yield return $"canal '{c.Id}': gasto vedado ({ChannelLawConfig.Default.GastosVedados[c.Id]}).";
            }

            if (c.Nome.Length > 28)
            {
                yield return $"canal '{c.Id}': nome com mais de 28 caracteres.";
            }

            if (c.Alvo is not (ChannelDefinition.AlvoZona or ChannelDefinition.AlvoIdade) || c.KPresenca < 0 || c.KApoio < 0 || (c.KPresenca <= 0 && c.KApoio <= 0))
            {
                yield return $"canal '{c.Id}': alvo 'zona' ou 'idade' e algum rendimento positivo.";
            }

            if ((c.EquipeFuncao is null) != (c.EquipeEfeito is null)
                || (c.EquipeFuncao is not null && (!StaffRole.Required.TryGetValue(c.EquipeFuncao, out string[]? effects) || !effects.Contains(c.EquipeEfeito))))
            {
                yield return $"canal '{c.Id}': função e efeito da equipe desconhecidos.";
            }

            if (c.PreCampanha is { } pre && (pre.Teto <= 0 || pre.Fator is <= 0 or > 1))
            {
                yield return $"canal '{c.Id}': pré-campanha com teto positivo e fator em (0, 1].";
            }

            if (!Regras.RivalReparte.ContainsKey(c.Id))
            {
                yield return $"regras.rival_reparte: falta o canal '{c.Id}'.";
            }
        }

        ChannelRules r = Regras;
        if (r.Escala <= 0 || r.ExpoenteDisputa is <= 0 or > 1 || r.Sorte.Min <= 0 || r.Sorte.Max < r.Sorte.Min || r.Passo <= 0 || r.ApoioTeto is <= 0 or > 1
            || r.CabosPorGrupo < 1 || r.CabosCustoGrupo <= 0 || r.CabosFatorDecaimento is < 0 or > 1 || r.CabosSemMaterial is <= 0 or > 1
            || r.RuaFatorRural <= 0 || r.ComicioVerbaRua < 0 || r.RivalVerba < 0 || r.RivalExtraDecaimento is < 0 or >= 1
            || r.SaturacaoFator < 0 || r.SaturacaoDecaimentoSemanal <= 0)
        {
            yield return "regras: valores fora do intervalo.";
        }

        if (FaixasIdade.Count == 0 || FaixasIdade.Any(b => b.Grupos.Count == 0 || b.Peso <= 0 || b.Nome.Length > 28))
        {
            yield return "faixas_idade: grupos, peso positivo e nome com até 28 caracteres.";
        }

        foreach (string key in TextKeys.Where(k => !Textos.ContainsKey(k)))
        {
            yield return $"textos: falta '{key}'.";
        }
    }
}

/// <summary>Verba semanal num canal e num alvo (zona ou faixa de idade).</summary>
public sealed class ChannelAllocation
{
    public required string ChannelId { get; init; }

    public required string TargetId { get; init; }

    public decimal Amount { get; set; }
}

/// <summary>O que a verba de um canal rendeu numa semana, já explicado.</summary>
public sealed class ChannelResult
{
    public required DateOnly Week { get; init; }

    public required string ChannelId { get; init; }

    public required string TargetId { get; init; }

    public required decimal Spent { get; init; }

    /// <summary>Verba dos rivais no mesmo canal e alvo.</summary>
    public required decimal RivalBudget { get; init; }

    /// <summary>√(verba ÷ escala).</summary>
    public required double Return { get; init; }

    /// <summary>(verba própria ÷ verba total)^expoente.</summary>
    public required double Share { get; init; }

    public double Effect => Return * Share;

    /// <summary>Mudança de presença por zona (id → pontos).</summary>
    public required Dictionary<string, double> Presence { get; init; }

    /// <summary>Apoio ganho por grupo de eleitores.</summary>
    public required Dictionary<string, double> Support { get; init; }

    public required List<YieldFactor> Factors { get; init; }
}

/// <summary>Verba da campanha: o plano semanal do jogador, o que já foi pago para a semana em curso e o último resultado.</summary>
public sealed class BudgetState
{
    /// <summary>O que o jogador quer gastar por semana; cobrado toda segunda-feira.</summary>
    public List<ChannelAllocation> Plan { get; init; } = [];

    /// <summary>O que já está pago para a semana em curso (o que de fato rende).</summary>
    public List<ChannelAllocation> Active { get; init; } = [];

    /// <summary>Verba a mais de rivais por "canal|alvo" (ex.: cabos que o jogador dispensou); esfria a cada semana.</summary>
    public Dictionary<string, double> RivalExtra { get; init; } = [];

    public List<ChannelResult> LastResults { get; init; } = [];

    /// <summary>Saturação por "canal|alvo": semanas seguidas de verba no mesmo lugar rendem cada vez menos.</summary>
    public Dictionary<string, double> Saturation { get; init; } = [];

    public decimal Spent { get; set; }

    public static string Key(string channelId, string targetId) => $"{channelId}|{targetId}";

    public decimal PlanOf(string channelId, string targetId) => Plan.FirstOrDefault(a => a.ChannelId == channelId && a.TargetId == targetId)?.Amount ?? 0;

    public decimal ActiveOf(string channelId, string targetId) => Active.FirstOrDefault(a => a.ChannelId == channelId && a.TargetId == targetId)?.Amount ?? 0;
}

/// <summary>Um alvo possível de um canal, para a tela.</summary>
public sealed record ChannelTarget(string Id, string Name);

/// <summary>Resumo da verba: plano semanal, o que está pago nesta semana e quantas semanas o caixa aguenta plano e folha.</summary>
public sealed record BudgetView(decimal PlanWeekly, decimal ActiveWeekly, decimal PayrollWeekly, double WeeksOfCash, int CabosPeople, int CabosLimit);
