using Planalto.Core.Data;

namespace Planalto.Core.Campaign;

/// <summary>Um efeito de função: o valor em cada qualidade (1 a 5) e o valor sem ninguém na função.</summary>
public sealed class StaffScale
{
    public required List<double> PorQualidade { get; init; }

    public required double Sem { get; init; }

    public double At(int quality) => PorQualidade[Math.Clamp(quality, 1, PorQualidade.Count) - 1];
}

/// <summary>Função da equipe (GDD 17.7).</summary>
public sealed class StaffRole
{
    public const string Coordenador = "coordenador";
    public const string Marqueteiro = "marqueteiro";
    public const string Tesoureiro = "tesoureiro";
    public const string Advogado = "advogado";
    public const string SocialMedia = "social_media";

    public const string Delegacoes = "delegacoes";
    public const string ErroConselho = "erro_conselho";
    public const string VeAgendaRival = "ve_agenda_rival";
    public const string Rendimento = "rendimento";
    public const string Faixa = "faixa";
    public const string ErroContabil = "erro_contabil";
    public const string Exito = "exito";
    public const string Alcance = "alcance";
    public const string Viral = "viral";

    /// <summary>Efeitos que cada função precisa trazer em data/.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Required = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        [Coordenador] = [Delegacoes, ErroConselho, VeAgendaRival],
        [Marqueteiro] = [Rendimento, Faixa],
        [Tesoureiro] = [ErroContabil],
        [Advogado] = [Exito],
        [SocialMedia] = [Alcance, Viral],
    };

    public required string Id { get; init; }

    public required string Nome { get; init; }

    /// <summary>O que a pessoa faz, para vir depois do nome ("coordena a sua campanha").</summary>
    public required string Papel { get; init; }

    public required string Descricao { get; init; }

    public required Dictionary<string, StaffScale> Efeitos { get; init; }
}

/// <summary>Tipo de vínculo de um candidato a vaga (GDD 17.7): com um rival, com uma liderança, familiar do jogador ou nenhum.</summary>
public sealed class StaffBondType
{
    public const string Nenhum = "nenhum";
    public const string Rival = "rival";
    public const string Lideranca = "lideranca";
    public const string Familiar = "familiar";

    public required string Id { get; init; }

    public required double Weight { get; init; }

    /// <summary>Quanto o vínculo puxa a qualidade (quem fez campanha de rival sabe mais; parente costuma saber menos).</summary>
    public required int QualidadeDelta { get; init; }

    public required IntRange Lealdade { get; init; }

    /// <summary>Texto do vínculo; aceita {nome} e {papel}.</summary>
    public required string Texto { get; init; }
}

/// <summary>O que uma opção de evento da equipe faz. "Outro" é o segundo envolvido (briga).</summary>
public sealed class StaffOptionEffects
{
    public int Lealdade { get; init; }

    public int LealdadeOutro { get; init; }

    public double SalarioFator { get; init; } = 1;

    /// <summary>Dinheiro que entra (ou sai) do caixa, no porte médio.</summary>
    public decimal Dinheiro { get; init; }

    public int Estresse { get; init; }

    /// <summary>Chance de a pessoa deixar a campanha.</summary>
    public double SaiChance { get; init; }

    public double OutroSaiChance { get; init; }

    /// <summary>A pessoa fica com chance igual à lealdade; senão, sai.</summary>
    public bool FicaPelaLealdade { get; init; }

    /// <summary>A promessa de cargo entra no Livro.</summary>
    public bool Promessa { get; init; }

    /// <summary>O favor entra no Livro como dívida com a pessoa.</summary>
    public bool Divida { get; init; }

    /// <summary>A liderança do vínculo passa a ser conhecida e ganha uma lembrança positiva.</summary>
    public bool ApresentaLideranca { get; init; }

    /// <summary>Quem sai vai para o rival do evento, que ganha presença nos redutos.</summary>
    public bool ReforcaRival { get; init; }

    public string? Lembranca { get; init; }

    public string? LembrancaOutro { get; init; }
}

public sealed class StaffEventOption
{
    public required string Id { get; init; }

    /// <summary>Rótulo da escolha (até 40 caracteres); aceita {nome} e {outro}.</summary>
    public required string Rotulo { get; init; }

    public required List<string> Ganho { get; init; }

    public required List<string> Custo { get; init; }

    public required StaffOptionEffects Efeitos { get; init; }

    public required string Resultado { get; init; }
}

/// <summary>Evento próprio da equipe (GDD 17.7): vira decisão pendente.</summary>
public sealed class StaffEventDefinition
{
    public const string Aumento = "aumento";
    public const string PropostaRival = "proposta_rival";
    public const string Briga = "briga";
    public const string TrazLideranca = "traz_lideranca";
    public const string TrazDoador = "traz_doador";

    public static readonly IReadOnlyList<string> Ids = [Aumento, PropostaRival, Briga, TrazLideranca, TrazDoador];

    public required string Id { get; init; }

    public required double Weight { get; init; }

    public required string Titulo { get; init; }

    public required string Texto { get; init; }

    public required List<StaffEventOption> Opcoes { get; init; }

    public StaffEventOption Option(string id) => Opcoes.First(o => o.Id == id);
}

public sealed class StaffRules
{
    /// <summary>Vagas da equipe por porte, contando a rede de cabos (GDD 17.7).</summary>
    public required Dictionary<string, int> VagasPorPorte { get; init; }

    public required IntRange CandidatosPorFuncao { get; init; }

    /// <summary>Semanas de trabalho até a qualidade deixar de ser mostrada como faixa.</summary>
    public required int SemanasParaConhecer { get; init; }

    /// <summary>Salário semanal no porte médio, da qualidade 1 à 5.</summary>
    public required List<decimal> SalarioPorQualidade { get; init; }

    public required Parties.DoubleRange SalarioVariacao { get; init; }

    public required int LealdadeMinimaInicial { get; init; }

    /// <summary>Quanto seguir (ou ignorar) o conselho da semana mexe na lealdade de quem o deu.</summary>
    public required int LealdadeConselho { get; init; }

    /// <summary>Compromissos na zona aconselhada, na semana, para o conselho contar como seguido.</summary>
    public required int ConselhoCompromissosMinimos { get; init; }

    public required int LealdadeSalarioAtrasado { get; init; }

    /// <summary>Pausa "caixa baixo": o caixa paga menos que tantas semanas de folha.</summary>
    public required int SemanasDeFolhaAlerta { get; init; }

    /// <summary>Abaixo desta lealdade, quem sai e conhece esqueleto pode contar a um rival.</summary>
    public required int LealdadeVazamento { get; init; }

    public required double ChanceVazamento { get; init; }

    public required double VazamentoImagemPorGravidade { get; init; }

    public required double ChanceEventoSemanal { get; init; }

    public required int SemanasParaPedirAumento { get; init; }

    /// <summary>A partir desta qualidade a pessoa recebe proposta de rival mesmo sem vínculo com um.</summary>
    public required int QualidadeAssediada { get; init; }

    /// <summary>Funções que ficam sabendo das irregularidades da campanha enquanto trabalham nela.</summary>
    public required List<string> FuncoesQueSabem { get; init; }

    /// <summary>Compromisso delegado rende esta fração (GDD 17.6).</summary>
    public required double DelegacaoRendimento { get; init; }

    public required int DelegacaoPontosVisita { get; init; }

    /// <summary>Imagem perdida quando uma gravação pega mal.</summary>
    public required double ViralImagem { get; init; }

    public required int GravidadeErroContabil { get; init; }
}

/// <summary>data/campaign/staff.json.</summary>
public sealed class StaffCatalog : IValidatable
{
    public const string File = "campaign/staff.json";

    public required string Note { get; init; }

    public required StaffRules Regras { get; init; }

    public required List<StaffRole> Funcoes { get; init; }

    public required List<StaffBondType> Vinculos { get; init; }

    public required List<StaffEventDefinition> Eventos { get; init; }

    public required Dictionary<string, MemoryDefinition> Lembrancas { get; init; }

    public required Dictionary<string, string> Textos { get; init; }

    public StaffRole Role(string id) => Funcoes.First(f => f.Id == id);

    public StaffBondType Bond(string id) => Vinculos.First(v => v.Id == id);

    public StaffEventDefinition Event(string id) => Eventos.First(e => e.Id == id);

    public MemoryDefinition Memory(string key) => Lembrancas[key];

    public string Texto(string key) => Textos.TryGetValue(key, out string? text) ? text : key;

    public static readonly IReadOnlyList<string> MemoryKeys =
    [
        "contratacao", "preterido", "conselho_seguido", "conselho_ignorado", "demissao", "saiu", "salario_atrasado", "vazamento",
        "apresentado", "visita_delegada", "objetivo_cumprido",
    ];

    public static readonly IReadOnlyList<string> TextKeys =
    [
        "conselho", "conselho_unico", "conselho_seguido", "conselho_ignorado", "previsao_contratar", "previsao_folha", "previsao_vinculo",
        "previsao_preterido", "previsao_demitir", "previsao_vazamento", "previsao_sem_vazamento", "contratado", "demitido", "vazou",
        "erro_contabil", "erro_contabil_livro", "folha_atrasada", "caixa_baixo", "cargo_livro", "divida_livro", "delegado", "reforco_rival",
        "viral", "previsao_opcao", "previsao_outro", "previsao_saida", "previsao_dinheiro", "previsao_livro",
    ];

    public IEnumerable<string> Validate()
    {
        foreach (string error in DataChecks.UniqueIds(Funcoes.Select(f => f.Id), "Função")
            .Concat(DataChecks.UniqueIds(Vinculos.Select(v => v.Id), "Vínculo"))
            .Concat(DataChecks.UniqueIds(Eventos.Select(e => e.Id), "Evento da equipe")))
        {
            yield return error;
        }

        StaffRules r = Regras;
        foreach (string porte in new[] { "pequeno", "medio", "grande", "metropole" })
        {
            if (!r.VagasPorPorte.TryGetValue(porte, out int vagas) || vagas < 1)
            {
                yield return $"regras.vagas_por_porte.{porte}: pelo menos 1.";
            }
        }

        if (r.CandidatosPorFuncao.Min < 2 || r.CandidatosPorFuncao.Max > 4 || r.CandidatosPorFuncao.Max < r.CandidatosPorFuncao.Min)
        {
            yield return "regras.candidatos_por_funcao: de 2 a 4.";
        }

        if (r.SalarioPorQualidade.Count != 5 || r.SalarioPorQualidade.Any(s => s <= 0) || r.SalarioVariacao.Min <= 0 || r.SalarioVariacao.Max < r.SalarioVariacao.Min)
        {
            yield return "regras: cinco salários positivos (qualidade 1 a 5) e variação válida.";
        }

        if (r.ChanceVazamento is < 0 or > 1 || r.ChanceEventoSemanal is < 0 or > 1 || r.DelegacaoRendimento is <= 0 or > 1 || r.SemanasParaConhecer < 1 || r.SemanasDeFolhaAlerta < 1)
        {
            yield return "regras: chances entre 0 e 1 e prazos positivos.";
        }

        foreach (var (roleId, effects) in StaffRole.Required)
        {
            StaffRole? role = Funcoes.FirstOrDefault(f => f.Id == roleId);
            if (role is null)
            {
                yield return $"falta a função '{roleId}'.";
                continue;
            }

            if (role.Nome.Length > 28)
            {
                yield return $"função '{roleId}': nome com mais de 28 caracteres.";
            }

            foreach (string effect in effects)
            {
                if (!role.Efeitos.TryGetValue(effect, out StaffScale? scale) || scale.PorQualidade.Count != 5)
                {
                    yield return $"função '{roleId}': efeito '{effect}' precisa de cinco valores (qualidade 1 a 5) e do valor 'sem'.";
                }
            }
        }

        foreach (string bond in new[] { StaffBondType.Nenhum, StaffBondType.Rival, StaffBondType.Lideranca, StaffBondType.Familiar }.Where(b => Vinculos.All(v => v.Id != b)))
        {
            yield return $"falta o vínculo '{bond}'.";
        }

        foreach (StaffBondType v in Vinculos.Where(v => v.Weight <= 0 || v.Lealdade.Min < 0 || v.Lealdade.Max > 100 || v.Lealdade.Max < v.Lealdade.Min))
        {
            yield return $"vínculo '{v.Id}': peso positivo e lealdade entre 0 e 100.";
        }

        foreach (string id in StaffEventDefinition.Ids.Where(id => Eventos.All(e => e.Id != id)))
        {
            yield return $"falta o evento da equipe '{id}'.";
        }

        foreach (StaffEventDefinition e in Eventos)
        {
            if (e.Opcoes.Count < 2 || e.Weight <= 0)
            {
                yield return $"evento '{e.Id}': pelo menos duas opções e peso positivo.";
            }

            foreach (string error in DataChecks.UniqueIds(e.Opcoes.Select(o => o.Id), $"Opção de '{e.Id}'"))
            {
                yield return error;
            }

            foreach (StaffEventOption o in e.Opcoes)
            {
                // Limite de texto do ART_GUIDE (Interface): rótulo de escolha com até 40 caracteres.
                if (o.Rotulo.Length > 40)
                {
                    yield return $"evento '{e.Id}', opção '{o.Id}': rótulo com mais de 40 caracteres.";
                }

                foreach (string error in DecisionNature.Check(o.Ganho, o.Custo, $"evento '{e.Id}', opção '{o.Id}'"))
                {
                    yield return error;
                }

                foreach (string? key in new[] { o.Efeitos.Lembranca, o.Efeitos.LembrancaOutro }.Where(k => k is not null && !Lembrancas.ContainsKey(k)))
                {
                    yield return $"evento '{e.Id}', opção '{o.Id}': lembrança '{key}' não existe.";
                }
            }
        }

        foreach (string key in MemoryKeys.Where(k => !Lembrancas.ContainsKey(k)))
        {
            yield return $"lembrancas: falta '{key}'.";
        }

        foreach (var (key, memory) in Lembrancas.Where(m => m.Value.Peso is < -3 or > 3 || string.IsNullOrWhiteSpace(m.Value.Texto)))
        {
            yield return $"lembrança '{key}': peso de −3 a 3 e texto ({memory.Peso}).";
        }

        foreach (string key in TextKeys.Where(k => !Textos.ContainsKey(k)))
        {
            yield return $"textos: falta '{key}'.";
        }
    }
}

/// <summary>
/// Candidato a uma vaga ou membro da equipe (GDD 17.7). Nome, aparência, interesses, temperamento e lembranças ficam
/// na <see cref="People.Person"/> e no personagem de mesmo id; aqui fica o que é do trabalho.
/// </summary>
public sealed class StaffMember
{
    public required string PersonId { get; init; }

    public required string Role { get; init; }

    /// <summary>Qualidade real (1 a 5).</summary>
    public required int Quality { get; init; }

    /// <summary>Faixa que o jogador vê até a pessoa completar as semanas de trabalho.</summary>
    public required int QualityShownMin { get; set; }

    public required int QualityShownMax { get; set; }

    /// <summary>Salário semanal, já no porte da cidade.</summary>
    public required decimal Salary { get; set; }

    /// <summary>0 a 100.</summary>
    public required int Loyalty { get; set; }

    public required string BondKind { get; init; }

    /// <summary>Rival ou liderança do vínculo (id), quando há.</summary>
    public string? BondPersonId { get; init; }

    public required string BondText { get; init; }

    public bool Hired { get; set; }

    public DateOnly? HiredOn { get; set; }

    public int WeeksWorked { get; set; }

    /// <summary>Saiu ou foi demitido: não volta a ser candidato.</summary>
    public bool Left { get; set; }
}

/// <summary>Decisão pendente de um evento da equipe.</summary>
public sealed class StaffDecision
{
    public required string Id { get; init; }

    /// <summary>Id do evento em data/campaign/staff.json.</summary>
    public required string EventId { get; init; }

    public required string PersonId { get; init; }

    public string? OtherPersonId { get; init; }

    public string? RivalId { get; init; }

    public string? LeaderId { get; init; }

    public required DateOnly Date { get; init; }

    /// <summary>Texto já com os nomes.</summary>
    public string Text { get; set; } = "";
}

public enum AdviceState
{
    Aberto,
    Seguido,
    Ignorado,
}

/// <summary>Conselho da semana do coordenador (GDD 17.7): onde concentrar, calculado da projeção real com erro.</summary>
public sealed class Advice
{
    public required string FromPersonId { get; init; }

    public required DateOnly Week { get; init; }

    public required string ZoneId { get; init; }

    public string? AvoidZoneId { get; init; }

    public required string Text { get; init; }

    public AdviceState State { get; set; }
}

/// <summary>Equipe na campanha em curso: decisões pendentes, conselho da semana e alerta da folha.</summary>
public sealed class TeamState
{
    public const int MaxAdviceLog = 12;

    public List<StaffDecision> Pending { get; init; } = [];

    public Advice? Advice { get; set; }

    public List<Advice> AdviceLog { get; init; } = [];

    /// <summary>O caixa paga menos que as semanas de folha do alerta (pausa "caixa baixo").</summary>
    public bool PayrollAlert { get; set; }

    /// <summary>Gasto de campanha da semana que passa pelas contas (folha e canais); zera na virada.</summary>
    public decimal RegisteredSpending { get; set; }

    public int NextId { get; set; } = 1;
}

/// <summary>Um candidato ou membro como a tela mostra: a qualidade só em faixa enquanto não for conhecida.</summary>
public sealed record StaffView(StaffMember Member, string Name, string RoleName, int QualityMin, int QualityMax, bool QualityKnown, decimal Salary, int Loyalty, string Bond, CommandFailure? HireBlocked);

/// <summary>Folha semanal e quantas semanas o caixa de hoje paga.</summary>
public sealed record PayrollView(decimal Weekly, double WeeksOfCash, bool Alert, IReadOnlyList<(string PersonId, decimal Salary)> Lines);

/// <summary>O que aconteceu quando alguém deixou a equipe.</summary>
public sealed record StaffDeparture(People.Memory Memory, bool Leaked, string? LeakedTo, IReadOnlyList<string> SkeletonsExposed);

/// <summary>Resultado de uma decisão de evento da equipe, já explicado.</summary>
public sealed record StaffDecisionResult(string Text, int Loyalty, int LoyaltyOther, decimal Money, bool Left, bool OtherLeft, bool LedgerEntry);
