using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Electorate;
using Planalto.Core.Legislature;
using Planalto.Core.Municipios;
using Planalto.Core.State;

namespace Planalto.Core.Executive;

/// <summary>Tudo o que o mandato de prefeito precisa, derivado do estado e do conteúdo.</summary>
public sealed class ExecutiveContext
{
    public required GameState State { get; init; }

    public required Municipio Municipio { get; init; }

    public required ExecutiveContent Content { get; init; }

    /// <summary>Regras de voto da Câmara (as mesmas do mandato de vereador).</summary>
    public required LegislatureConfig Legislature { get; init; }

    public required MunicipalElectorate Electorate { get; init; }

    public required double RecognitionScale { get; init; }

    /// <summary>
    /// Ponto de partida oficial do município (data/indicadores_reais, GDD 17.17). Sem ele, a receita cai na estimativa
    /// por habitante e por porte da D-031.
    /// </summary>
    public Indicators.IndicadoresMunicipio? Indicadores { get; init; }

    /// <summary>
    /// Evolução dos indicadores reais (GDD 17.17). Com ela e com <see cref="GameState.Indicators"/> no estado, os
    /// indicadores da prefeitura passam a ser a escala de jogo derivada dos valores reais; sem ela, vale a regra da D-031.
    /// </summary>
    public Indicators.IndicatorEvolution? IndicatorEvolution { get; init; }

    public ExecutiveConfig Config => Content.Config;
}

public enum ExecutiveFailure
{
    SemEnergia,
    SemReserva,
    JaAdotada,
    CrisePendente,
    AlvoInvalido,
    SemSecretarias,
    OrcamentoInvalido,
}

public sealed record EnactResult(bool Ok, ExecutiveFailure? Failure, bool Approved, int Yes, int No);

/// <summary>
/// Motor do mandato de prefeito (GDD 5.9, DECISOES D-030): orçamento por área com mínimos legais, indicadores com
/// atraso, políticas (por decreto ou com lei votada na Câmara), coalizão por secretarias, aprovação ponderada pelos
/// grupos de eleitores e crises. Toda aleatoriedade vem do Rng do estado.
/// </summary>
public sealed partial class ExecutiveEngine
{
    private readonly Dictionary<string, double> _groupShares;

    public ExecutiveEngine(ExecutiveContext ctx)
    {
        Context = ctx;
        _groupShares = GroupShares(ctx.Electorate);
    }

    public ExecutiveContext Context { get; }

    public ExecutiveState Executive => State.Executive ?? throw new InvalidOperationException("Sem mandato de prefeito.");

    private GameState State => Context.State;

    private ExecutiveConfig Config => Context.Config;

    private Rng Rng => State.Rng;

    private DateOnly Week => State.Calendar.WeekStart;

    public bool Over => Week > Executive.End;

    /// <summary>
    /// Receita anual em reais (escala da tela; as regras usam as parcelas): a receita corrente do município no SICONFI
    /// (GDD 17.17) ou, sem o dado importado, a estimativa por habitante e por porte da D-031.
    /// </summary>
    public decimal AnnualRevenue => Context.Indicadores?.Financas.ReceitaCorrente
        ?? Context.Municipio.Populacao * Config.RevenuePerCapita.GetValueOrDefault(Context.Municipio.Porte, 4000m);

    /// <summary>True quando a receita é a declarada pelo município ao Tesouro; false quando é estimativa (do importador ou da D-031).</summary>
    public bool RevenueIsDeclared => Context.Indicadores is { } i && i.Financas.Fonte != Indicators.FinancasEnte.Estimativa;

    // ---------------------------------------------------------------- posse

    /// <summary>Posse em 1º de janeiro: orçamento na referência, indicadores perto da média, Câmara e coalizão inicial.</summary>
    public static ExecutiveState Start(ExecutiveContext ctx, CampaignState campaign, Rng rng)
    {
        GameState state = ctx.State;
        ExecutiveConfig config = ctx.Config;
        LocalPolitics local = state.Local ?? throw new InvalidOperationException("Política local não registrada.");
        int year = campaign.Outcome!.Date.Year;
        string party = state.Player.PartyId ?? throw new InvalidOperationException("Prefeito sem partido.");

        List<ChamberMember> chamber = ChamberSetup.BuildMembers(state, campaign, local, rng);
        List<string> allies = ChamberSetup.BuildBase(state, chamber, party, ctx.Legislature.Base);
        var secretarias = new Dictionary<string, int>(StringComparer.Ordinal) { [party] = config.Secretarias - (allies.Count - 1) };
        foreach (string ally in allies.Where(a => a != party))
        {
            secretarias[ally] = 1;
        }

        foreach (ChamberMember m in chamber)
        {
            double relation = rng.NextDouble(-10, 10) + (m.PartyId == party ? 20 : 0) + (allies.Contains(m.PartyId ?? "") ? 8 : -5);
            m.Relation = Math.Clamp(relation, -100, 100);
        }

        Candidate player = campaign.Player;
        var executive = new ExecutiveState
        {
            Municipio = campaign.Municipio,
            Start = new DateOnly(year + 1, 1, 1),
            End = new DateOnly(year + 4, 12, 31),
            Term = local.MayorReelected ? 2 : 1,
            Allocation = config.Areas.ToDictionary(a => a.Id, a => a.ReferenceShare, StringComparer.Ordinal),
            Indicators = config.Indicators.ToDictionary(i => i.Id, _ => 50 + rng.NextDouble(-8, 8), StringComparer.Ordinal),
            Chamber = chamber,
            Secretarias = secretarias,
            PartyId = party,
            Image = player.Image,
            Recognition = player.Recognition,
            Support = new Dictionary<string, double>(player.Support, StringComparer.Ordinal),
            EnergyLeft = config.WeeklyEnergy,
        };
        LinkRealIndicators(ctx, executive);
        return executive;
    }

    // ---------------------------------------------------------------- orçamento e indicadores

    /// <summary>Custo anual das políticas em vigor (o que sai da reserva; negativo = libera).</summary>
    public double PolicyCost => Executive.Policies.Where(p => p.Counts).Sum(p => Context.Content.Policies.Find(p.PolicyId)!.Cost);

    /// <summary>Reserva livre: a parcela da reserva menos o custo das políticas.</summary>
    public double FreeReserve => Executive.Allocation.GetValueOrDefault(ExecutiveConfig.ReserveArea) - PolicyCost;

    /// <summary>Áreas abaixo do mínimo legal (saúde 15%, educação 25%).</summary>
    public IEnumerable<BudgetArea> BelowMinimum => Config.Areas.Where(a => Executive.Allocation.GetValueOrDefault(a.Id) + 1e-9 < a.MinimumShare);

    public ExecutiveFailure? SetAllocation(IReadOnlyDictionary<string, double> allocation)
    {
        if (Config.Areas.Any(a => !allocation.ContainsKey(a.Id)) || allocation.Values.Any(v => v < 0) || Math.Abs(allocation.Values.Sum() - 1) > 0.001)
        {
            return ExecutiveFailure.OrcamentoInvalido;
        }

        foreach (BudgetArea area in Config.Areas)
        {
            Executive.Allocation[area.Id] = allocation[area.Id];
        }

        return null;
    }

    /// <summary>Para onde cada indicador caminha: gasto relativo à referência, políticas em vigor e reserva não usada.</summary>
    public Dictionary<string, double> Targets()
    {
        ExecutiveState e = Executive;
        var targets = Config.Indicators.ToDictionary(i => i.Id, _ => 50.0, StringComparer.Ordinal);
        foreach (BudgetArea area in Config.Areas.Where(a => a.ReferenceShare > 0))
        {
            double relative = (e.Allocation.GetValueOrDefault(area.Id) / area.ReferenceShare) - 1;
            foreach (var (indicator, weight) in area.Effects)
            {
                targets[indicator] += weight * Config.TargetSensitivity * relative;
            }
        }

        foreach (EnactedPolicy p in e.Policies.Where(p => p.Status == PolicyStatus.Ativa))
        {
            foreach (var (indicator, boost) in Context.Content.Policies.Find(p.PolicyId)!.Boosts)
            {
                targets[indicator] += boost;
            }
        }

        if (targets.ContainsKey("contas"))
        {
            targets["contas"] += Config.UnusedReserveToAccounts * Math.Max(0, FreeReserve) - (Config.UnusedReserveToAccounts * Math.Max(0, -FreeReserve) * 2);
        }

        return targets.ToDictionary(t => t.Key, t => Math.Clamp(t.Value, Config.IndicatorBounds.Min, Config.IndicatorBounds.Max), StringComparer.Ordinal);
    }

    /// <summary>Aprovação de um grupo: desvio médio dos indicadores que ele pesa, imagem, apoio e lua de mel.</summary>
    public double GroupApproval(string group)
    {
        ExecutiveState e = Executive;
        ApprovalConfig a = Config.Approval;
        Dictionary<string, double> weights = Config.GroupWeights.GetValueOrDefault(group) ?? Config.Indicators.ToDictionary(i => i.Id, _ => 1.0);
        double score = weights.Sum(w => w.Value * (e.Indicators.GetValueOrDefault(w.Key, 50) - 50)) / Math.Max(1e-9, weights.Values.Sum());
        bool honeymoon = Week < e.Start.AddDays(7 * a.HoneymoonWeeks);
        double approval = a.Base + (a.IndicatorWeight * score) + (a.ImageWeight * e.Image) + (a.SupportWeight * e.Support.GetValueOrDefault(group)) + (honeymoon ? a.HoneymoonBonus : 0);
        return Math.Clamp(approval, 0, 100);
    }

    /// <summary>Aprovação geral: média das aprovações dos grupos pesada pelo tamanho de cada um no eleitorado.</summary>
    public double ComputeApproval()
    {
        var groups = Config.GroupWeights.Keys.Where(g => _groupShares.ContainsKey(g)).ToList();
        double total = groups.Sum(g => _groupShares[g]);
        return total <= 0 ? GroupApproval("") : groups.Sum(g => _groupShares[g] * GroupApproval(g)) / total;
    }

    // ---------------------------------------------------------------- políticas

    public ExecutiveFailure? CanEnact(PolicyDefinition policy)
    {
        ExecutiveState e = Executive;
        if (e.Crisis is not null)
        {
            return ExecutiveFailure.CrisePendente;
        }

        if (e.EnergyLeft < Config.EnactEnergy)
        {
            return ExecutiveFailure.SemEnergia;
        }

        if (e.Policies.Any(p => p.PolicyId == policy.Id && p.Counts))
        {
            return ExecutiveFailure.JaAdotada;
        }

        return policy.Cost > 0 && FreeReserve + 1e-9 < policy.Cost ? ExecutiveFailure.SemReserva : null;
    }

    /// <summary>Adota a política: por decreto, ou mandando o projeto à Câmara (votação na hora, pela base e pela ideologia).</summary>
    public EnactResult Enact(string policyId)
    {
        PolicyDefinition policy = Context.Content.Policies.Find(policyId) ?? throw new ArgumentException($"Política '{policyId}' inexistente.", nameof(policyId));
        if (CanEnact(policy) is { } failure)
        {
            return new EnactResult(false, failure, false, 0, 0);
        }

        ExecutiveState e = Executive;
        e.EnergyLeft -= Config.EnactEnergy;
        int yes = 0, no = 0;
        if (policy.RequiresLaw)
        {
            foreach (ChamberMember m in e.Chamber)
            {
                int side = e.InBase(m.PartyId) ? 1 : -1;
                int orientation = m.PartyId == e.PartyId ? 1 : ChamberVote.PartyOrientation(Context.Legislature, State, m.PartyId, policy.Position, side);
                double logit = ChamberVote.Logit(Context.Legislature, State, m, policy.Position, orientation, side, m.Relation);
                switch (ChamberVote.Decide(Context.Legislature, logit, Rng))
                {
                    case VoteChoice.Sim:
                        yes++;
                        break;
                    case VoteChoice.Nao:
                        no++;
                        break;
                }
            }

            if (!LegislatureEngine.Approved(policy.Quorum, yes, no, e.Chamber.Count))
            {
                e.Policies.Add(new EnactedPolicy { PolicyId = policy.Id, Started = Week, ActiveFrom = Week, Status = PolicyStatus.Rejeitada, Yes = yes, No = no });
                e.News.Add(new ExecutiveNews(Week, ExecutiveNewsKind.PoliticaRejeitada, policy.Name, yes, no));
                return new EnactResult(true, null, false, yes, no);
            }
        }

        e.Policies.Add(new EnactedPolicy { PolicyId = policy.Id, Started = Week, ActiveFrom = Week.AddDays(7 * policy.ImplementationWeeks), Status = PolicyStatus.Implantando, Yes = yes, No = no });
        e.News.Add(new ExecutiveNews(Week, policy.RequiresLaw ? ExecutiveNewsKind.PoliticaAprovada : ExecutiveNewsKind.PoliticaDecretada, policy.Name, yes, no));
        Opinion(policy.Popularity, policy.Salience, 1);
        return new EnactResult(true, null, true, yes, no);
    }

    /// <summary>Revoga uma política adotada: o custo volta para a reserva e a reação dos grupos se inverte pela metade.</summary>
    public bool Revoke(string policyId)
    {
        EnactedPolicy? enacted = Executive.Policies.FirstOrDefault(p => p.PolicyId == policyId && p.Counts);
        if (enacted is null)
        {
            return false;
        }

        PolicyDefinition policy = Context.Content.Policies.Find(policyId)!;
        enacted.Status = PolicyStatus.Revogada;
        Executive.News.Add(new ExecutiveNews(Week, ExecutiveNewsKind.PoliticaRevogada, policy.Name));
        Opinion(policy.Popularity, policy.Salience, -0.5);
        return true;
    }

    private void Opinion(IReadOnlyDictionary<string, int> popularity, double salience, double sign)
    {
        ExecutiveState e = Executive;
        double net = 0;
        foreach (var (group, value) in popularity)
        {
            e.Support[group] = Math.Clamp(e.Support.GetValueOrDefault(group) + (sign * value * salience * Config.SupportPerPoint), 0, 1);
            net += value * _groupShares.GetValueOrDefault(group);
        }

        e.Image = Math.Clamp(e.Image + (sign * net * salience * Config.ImagePerPoint), -1, 1);
    }

    // ---------------------------------------------------------------- coalizão

    public int SecretariasFree => Config.Secretarias - Executive.Secretarias.Values.Sum();

    /// <summary>Entrega uma secretaria a um partido com cadeiras: ele entra na base e a bancada melhora a relação.</summary>
    public ExecutiveFailure? GiveSecretaria(string partyId)
    {
        ExecutiveState e = Executive;
        if (!e.Chamber.Any(m => m.PartyId == partyId) && partyId != e.PartyId)
        {
            return ExecutiveFailure.AlvoInvalido;
        }

        if (SecretariasFree <= 0)
        {
            // Sem secretaria vaga: tira uma do próprio partido, se houver mais de uma.
            if (e.Secretarias.GetValueOrDefault(e.PartyId) <= 1 || partyId == e.PartyId)
            {
                return ExecutiveFailure.SemSecretarias;
            }

            e.Secretarias[e.PartyId]--;
        }

        e.Secretarias[partyId] = e.Secretarias.GetValueOrDefault(partyId) + 1;
        foreach (ChamberMember m in e.Chamber.Where(m => m.PartyId == partyId))
        {
            m.Relation = Math.Clamp(m.Relation + Config.SecretariaRelation, -100, 100);
        }

        e.News.Add(new ExecutiveNews(Week, ExecutiveNewsKind.SecretariaEntregue, State.Party(partyId)?.Sigla ?? partyId));
        return null;
    }

    /// <summary>Retira uma secretaria de um aliado (volta para o partido do prefeito); sem secretaria, ele sai da base.</summary>
    public ExecutiveFailure? TakeSecretaria(string partyId)
    {
        ExecutiveState e = Executive;
        if (partyId == e.PartyId || e.Secretarias.GetValueOrDefault(partyId) <= 0)
        {
            return ExecutiveFailure.AlvoInvalido;
        }

        e.Secretarias[partyId]--;
        e.Secretarias[e.PartyId] = e.Secretarias.GetValueOrDefault(e.PartyId) + 1;
        foreach (ChamberMember m in e.Chamber.Where(m => m.PartyId == partyId))
        {
            m.Relation = Math.Clamp(m.Relation - (Config.SecretariaRelation * 1.5), -100, 100);
        }

        e.News.Add(new ExecutiveNews(Week, ExecutiveNewsKind.SecretariaRetirada, State.Party(partyId)?.Sigla ?? partyId));
        return null;
    }

    // ---------------------------------------------------------------- gabinete

    public ExecutiveFailure? CanExecute(MandateActionDefinition action, string? target)
    {
        ExecutiveState e = Executive;
        if (e.EnergyLeft < action.Energy)
        {
            return ExecutiveFailure.SemEnergia;
        }

        return action.Kind switch
        {
            MandateActionKind.Articular => e.Chamber.Any(m => m.Id == target) ? null : ExecutiveFailure.AlvoInvalido,
            _ => action.Targets.Count > 0 && action.Targets.All(t => t.Id != target) ? ExecutiveFailure.AlvoInvalido : null,
        };
    }

    public string Execute(string actionId, string? target)
    {
        MandateActionDefinition action = Context.Content.Actions.Find(actionId) ?? throw new ArgumentException($"Ação '{actionId}' inexistente.", nameof(actionId));
        if (CanExecute(action, target) is not null)
        {
            return "";
        }

        ExecutiveState e = Executive;
        e.EnergyLeft -= action.Energy;
        Apply(action.Effects);
        string text = "";
        if (action.Test is { } test)
        {
            bool success = Rng.NextDouble() < Math.Clamp(State.Player.Attributes.Get(test.Attribute) / 100.0, 0.05, 0.95);
            Apply(success ? test.Success : test.Failure);
            text = success ? test.SuccessText : test.FailureText;
        }

        if (action.Targets.FirstOrDefault(t => t.Id == target) is { } chosen)
        {
            e.Support[chosen.Group] = Math.Clamp(e.Support.GetValueOrDefault(chosen.Group) + chosen.Support, 0, 1);
        }

        if (action.Kind == MandateActionKind.Articular && e.Chamber.FirstOrDefault(m => m.Id == target) is { } member)
        {
            member.Relation = Math.Clamp(member.Relation + action.Relation, -100, 100);
            e.News.Add(new ExecutiveNews(Week, ExecutiveNewsKind.Articulacao, member.Name));
        }

        return text;
    }

    private void Apply(Effects effects)
    {
        ExecutiveState e = Executive;
        e.Recognition = EffectApplier.Gain(e.Recognition, effects.Recognition * Context.RecognitionScale);
        e.Image = Math.Clamp(e.Image + effects.Image, -1, 1);
        foreach (var (group, delta) in effects.Support)
        {
            e.Support[group] = Math.Clamp(e.Support.GetValueOrDefault(group) + delta, 0, 1);
        }
    }

    // ---------------------------------------------------------------- crises

    /// <summary>Responde à crise pendente: custo único (pesa nas contas), efeitos nos indicadores e na imagem.</summary>
    public string ResolveCrisis(int choiceIndex)
    {
        ExecutiveState e = Executive;
        PendingCrisis pending = e.Crisis ?? throw new InvalidOperationException("Sem crise pendente.");
        CrisisDefinition crisis = Context.Content.Crises.Find(pending.CrisisId)!;
        CrisisChoice choice = crisis.Choices[Math.Clamp(choiceIndex, 0, crisis.Choices.Count - 1)];
        foreach (var (indicator, delta) in choice.Indicators)
        {
            Shift(indicator, delta);
        }

        Shift("contas", -choice.Cost * Config.UnusedReserveToAccounts);
        e.Image = Math.Clamp(e.Image + choice.Image, -1, 1);
        e.Crisis = null;
        e.News.Add(new ExecutiveNews(Week, ExecutiveNewsKind.CriseResolvida, crisis.Name));
        return choice.Result;
    }

    private void Shift(string indicator, double delta)
    {
        ExecutiveState e = Executive;
        if (ShiftReal(indicator, delta))
        {
            return;
        }

        if (e.Indicators.ContainsKey(indicator))
        {
            e.Indicators[indicator] = Math.Clamp(e.Indicators[indicator] + delta, Config.IndicatorBounds.Min, Config.IndicatorBounds.Max);
        }
    }

    private void MaybeCrisis()
    {
        ExecutiveState e = Executive;
        if (e.Crisis is not null || Rng.NextDouble() >= Config.CrisisChancePerWeek)
        {
            return;
        }

        var candidates = Context.Content.Crises.Crises
            .Where(c => c.Months.Count == 0 || c.Months.Contains(Week.Month))
            .Select(c => (Crisis: c, Weight: c.Weight * (c.WorseWhenBelow.Any(w => e.Indicators.GetValueOrDefault(w.Key, 50) < w.Value) ? 2 : 1)))
            .Where(c => c.Weight > 0)
            .ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        CrisisDefinition crisis = Rng.PickWeighted(candidates, c => c.Weight).Crisis;
        foreach (var (indicator, delta) in crisis.Shock)
        {
            Shift(indicator, delta);
        }

        e.Crisis = new PendingCrisis(crisis.Id, Week);
        e.News.Add(new ExecutiveNews(Week, ExecutiveNewsKind.Crise, crisis.Name));
    }

    // ---------------------------------------------------------------- semana

    /// <summary>
    /// Resolve a semana: crise pendente (sozinha no modo automático), indicadores rumo ao alvo, políticas que
    /// entram em vigor, mínimos legais, contas do ano, decaimentos e energia. Não avança o calendário.
    /// </summary>
    public void ResolveWeek(bool automatic)
    {
        ExecutiveState e = Executive;
        if (e.Crisis is not null)
        {
            if (!automatic)
            {
                throw new InvalidOperationException("Há uma crise esperando decisão.");
            }

            ResolveCrisis(Config.AutoCrisisChoice);
        }

        foreach (EnactedPolicy p in e.Policies.Where(p => p.Status == PolicyStatus.Implantando && p.ActiveFrom <= Week))
        {
            p.Status = PolicyStatus.Ativa;
            e.News.Add(new ExecutiveNews(Week, ExecutiveNewsKind.PoliticaEmVigor, Context.Content.Policies.Find(p.PolicyId)!.Name));
        }

        if (!StepRealIndicators())
        {
            foreach (var (indicator, target) in Targets())
            {
                e.Indicators[indicator] += Config.IndicatorSpeedPerWeek * (target - e.Indicators[indicator]);
            }
        }

        if (BelowMinimum.Any() && !e.MinimumViolatedThisYear)
        {
            e.MinimumViolatedThisYear = true;
            e.News.Add(new ExecutiveNews(Week, ExecutiveNewsKind.MinimoDescumprido, string.Join(", ", BelowMinimum.Select(a => a.Name))));
        }

        // Fim do ano: o Tribunal de Contas rejeita as contas de quem descumpriu os mínimos.
        if (Week.AddDays(7).Year != Week.Year)
        {
            if (e.MinimumViolatedThisYear)
            {
                e.Image = Math.Clamp(e.Image + Config.MinimumViolationImage, -1, 1);
                e.News.Add(new ExecutiveNews(Week, ExecutiveNewsKind.ContasRejeitadas, Week.Year.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }

            e.MinimumViolatedThisYear = BelowMinimum.Any();
        }

        foreach (ChamberMember m in e.Chamber)
        {
            m.Relation -= Math.Sign(m.Relation) * Math.Min(Math.Abs(m.Relation), Config.RelationDriftPerWeek);
        }

        foreach (string group in e.Support.Keys.ToList())
        {
            e.Support[group] = Math.Max(0, e.Support[group] - Config.SupportDecayPerWeek);
        }

        e.Recognition = Math.Max(0, e.Recognition - Config.RecognitionDecayPerWeek);
        e.EnergyLeft = Config.WeeklyEnergy;
        MaybeCrisis();
        if (automatic && e.Crisis is not null)
        {
            ResolveCrisis(Config.AutoCrisisChoice);
        }

        e.Approval = ComputeApproval();
    }

    public void AdvanceWeek()
    {
        ResolveWeek(automatic: false);
        State.Calendar.AdvanceWeek();
    }

    private static Dictionary<string, double> GroupShares(MunicipalElectorate electorate)
    {
        double total = electorate.Voters;
        var shares = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (Segment segment in electorate.Segments)
        {
            foreach (string group in segment.Groups.Values)
            {
                shares[group] = shares.GetValueOrDefault(group) + (total <= 0 ? 0 : segment.Voters / total);
            }
        }

        return shares;
    }
}
