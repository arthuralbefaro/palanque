using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Electorate;
using Planalto.Core.Parties;
using Planalto.Core.State;

namespace Planalto.Core.Legislature;

public enum MandateActionFailure
{
    SemEnergia,
    SemCapital,
    AlvoInvalido,
    SemEmendas,
}

public sealed record MandateActionResult(bool Ok, MandateActionFailure? Failure, string Text);

/// <summary>
/// Motor do mandato legislativo (GDD 5.8): pauta semanal, votos dos vereadores NPC, consequências do voto do
/// jogador (eleitores, imagem, fidelidade, relações e capital político), projetos e ações de gabinete.
/// Toda aleatoriedade vem do Rng do estado.
/// </summary>
public sealed class LegislatureEngine
{
    private readonly Dictionary<string, double> _groupShares;

    public LegislatureEngine(LegislatureContext ctx)
    {
        Context = ctx;
        _groupShares = GroupShares(ctx.Electorate);
    }

    public LegislatureContext Context { get; }

    public MandateState Mandate => State.Mandate ?? throw new InvalidOperationException("Sem mandato em curso.");

    private GameState State => Context.State;

    private LegislatureConfig Config => Context.Config;

    private Rng Rng => State.Rng;

    private DateOnly Week => State.Calendar.WeekStart;

    public bool InRecess => Config.RecessMonths.Contains(Week.Month);

    /// <summary>
    /// Luto oficial pela morte de vereador, prefeito ou vice (GDD 17.16): a Câmara não faz sessão enquanto ele alcança
    /// a semana. A pauta espera a semana seguinte; o gabinete continua aberto.
    /// </summary>
    public bool InMourning => State.Renewal is { } renewal && renewal.Municipio == Context.Municipio.CodigoIbge && Renewal.RenewalQueries.InMourning(renewal, Week);

    public bool Over => State.Calendar.WeekStart > Mandate.End;

    /// <summary>Projetos na pauta do plenário nesta semana.</summary>
    public IEnumerable<Bill> Pauta => Mandate.Bills.Where(b => b.Stage == BillStage.Plenario && b.CommitteeUntil <= Week);

    public BillTemplate Template(Bill bill) => Context.Content.MunicipalBills.Find(bill.TemplateId)!;

    // ---------------------------------------------------------------- votos

    /// <summary>Afinidade (-1 a 1) entre uma posição e o projeto: positiva perto, negativa longe.</summary>
    public double Alignment(Ideology.IdeologicalPosition position, BillTemplate bill) => ChamberVote.Alignment(Config.Vote, position, bill.Position);

    /// <summary>Governo (+1), oposição (-1) ou neutro (0) num projeto do Executivo; 0 nos demais.</summary>
    public int GovernmentSide(string? partyId, Bill bill) =>
        bill.AuthorKind != BillAuthorKind.Executivo || partyId is null ? 0 : Mandate.InBase(partyId) ? 1 : -1;

    /// <summary>Orientação da bancada (-1 não, 0 liberada, 1 sim), pela posição do partido e pelo lado no governo.</summary>
    public int Orientation(string? partyId, Bill bill) =>
        ChamberVote.PartyOrientation(Config, State, partyId, Template(bill).Position, GovernmentSide(partyId, bill));

    /// <summary>Orientação que o jogador recebe: a do partido dele, ou nenhuma se ele é o líder ou está sem partido.</summary>
    public int PlayerOrientation(Bill bill)
    {
        ChamberMember player = Mandate.Player;
        return player.Leader || player.PartyId is null ? 0 : Orientation(player.PartyId, bill);
    }

    public double Discipline(string? partyId) => ChamberVote.Discipline(Config, State, partyId);

    /// <summary>Logito do voto SIM de um vereador NPC (sem ruído).</summary>
    public double VoteLogit(ChamberMember member, Bill bill, int orientation) =>
        ChamberVote.Logit(Config, State, member, Template(bill).Position, orientation, GovernmentSide(member.PartyId, bill), bill.AuthorId == RaceSetup.PlayerId ? member.Relation : 0);

    /// <summary>Marca o voto do jogador num projeto da pauta (pode mudar até a semana avançar).</summary>
    public void CastVote(string billId, VoteChoice choice)
    {
        Bill bill = Pauta.FirstOrDefault(b => b.Id == billId) ?? throw new ArgumentException($"Projeto '{billId}' não está na pauta.", nameof(billId));
        bill.PlayerVote = choice == VoteChoice.Ausente ? null : choice;
    }

    public static bool Approved(Quorum quorum, int yes, int no, int members) => quorum switch
    {
        Quorum.Absoluta => yes > members / 2,
        Quorum.DoisTercos => yes * 3 >= members * 2,
        _ => yes > no,
    };

    // ---------------------------------------------------------------- ações

    public MandateActionFailure? CanExecute(MandateActionDefinition action, string? target)
    {
        if (Mandate.EnergyLeft < action.Energy)
        {
            return MandateActionFailure.SemEnergia;
        }

        if (State.Player.Resources.PoliticalCapital < action.Capital)
        {
            return MandateActionFailure.SemCapital;
        }

        return action.Kind switch
        {
            MandateActionKind.Propor => Context.Content.MunicipalBills.Find(target ?? "") is { Author: BillAuthorKind.Vereador } t
                && !Mandate.Bills.Any(b => b.TemplateId == t.Id && b.Stage is BillStage.Comissao or BillStage.Plenario)
                ? null : MandateActionFailure.AlvoInvalido,
            MandateActionKind.Urgencia => Mandate.Bills.Any(b => b.Id == target && b.AuthorId == RaceSetup.PlayerId && b.Stage == BillStage.Comissao)
                ? null : MandateActionFailure.AlvoInvalido,
            MandateActionKind.Articular => Mandate.Member(target ?? "") is { IsPlayer: false } ? null : MandateActionFailure.AlvoInvalido,
            MandateActionKind.Emenda => Mandate.EmendasLeft <= 0 ? MandateActionFailure.SemEmendas
                : action.Targets.Any(t => t.Id == target) ? null : MandateActionFailure.AlvoInvalido,
            _ => action.Targets.Count > 0 && action.Targets.All(t => t.Id != target) ? MandateActionFailure.AlvoInvalido : null,
        };
    }

    /// <summary>Projetos que o jogador ainda pode protocolar.</summary>
    public IEnumerable<BillTemplate> ProposableBills() =>
        Context.Content.MunicipalBills.Bills.Where(t => t.Author == BillAuthorKind.Vereador
            && !Mandate.Bills.Any(b => b.TemplateId == t.Id && b.Stage is BillStage.Comissao or BillStage.Plenario));

    public MandateActionResult Execute(string actionId, string? target)
    {
        MandateActionDefinition action = Context.Content.Actions.Find(actionId) ?? throw new ArgumentException($"Ação '{actionId}' inexistente.", nameof(actionId));
        if (CanExecute(action, target) is { } failure)
        {
            return new MandateActionResult(false, failure, "");
        }

        MandateState mandate = Mandate;
        mandate.EnergyLeft -= action.Energy;
        State.Player.Resources.PoliticalCapital -= action.Capital;
        Apply(action.Effects);
        mandate.MayorRelation = Math.Clamp(mandate.MayorRelation + action.RelationMayor, -100, 100);

        string text = "";
        if (action.Test is { } test)
        {
            double chance = Math.Clamp(State.Player.Attributes.Get(test.Attribute) / 100.0, 0.05, 0.95);
            bool success = Rng.NextDouble() < chance;
            Apply(success ? test.Success : test.Failure);
            text = success ? test.SuccessText : test.FailureText;
        }

        if (action.Targets.FirstOrDefault(t => t.Id == target) is { } chosen)
        {
            mandate.Support[chosen.Group] = Math.Clamp(mandate.Support.GetValueOrDefault(chosen.Group) + chosen.Support, 0, 1);
        }

        switch (action.Kind)
        {
            case MandateActionKind.Propor:
                BillTemplate template = Context.Content.MunicipalBills.Find(target!)!;
                Bill bill = File(template, RaceSetup.PlayerId);
                mandate.News.Add(new MandateNews(Week, MandateNewsKind.ProjetoProtocolado, bill.Title, PlayerAuthor: true));
                break;
            case MandateActionKind.Urgencia:
                Bill urgent = mandate.Bills.First(b => b.Id == target);
                urgent.Stage = BillStage.Plenario;
                urgent.CommitteeUntil = Week.AddDays(7);
                mandate.News.Add(new MandateNews(Week, MandateNewsKind.Urgencia, urgent.Title, PlayerAuthor: true));
                break;
            case MandateActionKind.Articular:
                ChamberMember colleague = mandate.Member(target!)!;
                colleague.Relation = Math.Clamp(colleague.Relation + action.Relation, -100, 100);
                mandate.News.Add(new MandateNews(Week, MandateNewsKind.Articulacao, colleague.Name));
                break;
            case MandateActionKind.Emenda:
                mandate.EmendasLeft--;
                mandate.News.Add(new MandateNews(Week, MandateNewsKind.Emenda, action.Targets.First(t => t.Id == target).Name));
                break;
        }

        return new MandateActionResult(true, null, text);
    }

    private void Apply(Effects effects)
    {
        MandateState mandate = Mandate;
        mandate.Recognition = EffectApplier.Gain(mandate.Recognition, effects.Recognition * Context.RecognitionScale);
        mandate.Image = Math.Clamp(mandate.Image + effects.Image, -1, 1);
        foreach (var (group, delta) in effects.Support)
        {
            mandate.Support[group] = Math.Clamp(mandate.Support.GetValueOrDefault(group) + delta, 0, 1);
        }

        State.Player.Resources.PoliticalCapital += effects.PoliticalCapital;
        State.Player.Resources.Stress += effects.Stress;
    }

    private Bill File(BillTemplate template, string authorId) => FileBill(Mandate, Config, template, authorId, Week, Rng);

    /// <summary>Protocola um projeto: entra na comissão com o prazo sorteado.</summary>
    public static Bill FileBill(MandateState mandate, LegislatureConfig config, BillTemplate template, string authorId, DateOnly week, Rng rng)
    {
        var bill = new Bill
        {
            Id = $"pl_{mandate.NextBillNumber++}",
            TemplateId = template.Id,
            Title = template.Title,
            AuthorId = authorId,
            AuthorKind = template.Author,
            Filed = week,
            CommitteeUntil = week.AddDays(7 * rng.NextInt(config.CommitteeWeeks.Min, config.CommitteeWeeks.Max + 1)),
        };
        mandate.Bills.Add(bill);
        return bill;
    }

    // ---------------------------------------------------------------- semana

    /// <summary>
    /// Resolve a semana: votações da pauta, tramitação nas comissões, novos projetos, decaimentos e energia.
    /// Com <paramref name="automatic"/>, o jogador segue a orientação sem consequências (ano de campanha).
    /// Não avança o calendário.
    /// </summary>
    public void ResolveWeek(bool automatic)
    {
        MandateState mandate = Mandate;
        if (!InRecess && !InMourning)
        {
            foreach (Bill bill in Pauta.ToList())
            {
                Vote(bill, automatic);
            }

            Committees();
            NewBills();
            Schedule();
        }

        Decay();
        mandate.EnergyLeft = Config.WeeklyEnergy;
        int nextYear = Week.AddDays(7).Year;
        if (nextYear != mandate.EmendasYear)
        {
            mandate.EmendasYear = nextYear;
            mandate.EmendasLeft = Config.EmendasPerYear;
        }
    }

    /// <summary>Resolve a semana e avança o calendário.</summary>
    public void AdvanceWeek()
    {
        ResolveWeek(automatic: false);
        State.Calendar.AdvanceWeek();
    }

    private void Vote(Bill bill, bool automatic)
    {
        MandateState mandate = Mandate;
        BillTemplate template = Template(bill);
        ChamberMember player = mandate.Player;
        int orientation = PlayerOrientation(bill);
        bill.Orientation = orientation;

        // Sem voto marcado, vale a instrução permanente (GDD 17.14); sem instrução, o jogador falta.
        VoteChoice playerVote = automatic
            ? orientation switch { 1 => VoteChoice.Sim, -1 => VoteChoice.Nao, _ => Alignment(player.Position, template) >= 0 ? VoteChoice.Sim : VoteChoice.Nao }
            : bill.PlayerVote ?? Governance.GovernanceEngine.InstructionVote(State, Context.Content, bill) ?? VoteChoice.Ausente;
        bill.PlayerVote = playerVote;

        // Como líder, o jogador orienta a própria bancada.
        int playerSign = playerVote switch { VoteChoice.Sim => 1, VoteChoice.Nao => -1, _ => 0 };
        int yes = playerVote == VoteChoice.Sim ? 1 : 0;
        int no = playerVote == VoteChoice.Nao ? 1 : 0;
        int abstain = playerVote == VoteChoice.Abstencao ? 1 : 0;
        foreach (ChamberMember m in mandate.Members.Where(m => !m.IsPlayer))
        {
            int o = player.Leader && m.PartyId == player.PartyId && player.PartyId is not null ? playerSign : Orientation(m.PartyId, bill);
            // Quem fechou acordo de voto com o jogador vota como combinado (GDD 17.14).
            switch (ChamberVote.Decide(Config, VoteLogit(m, bill, o) + (State.Governance?.DealLogit(bill.Id, m.Id) ?? 0), Rng))
            {
                case VoteChoice.Abstencao:
                    abstain++;
                    break;
                case VoteChoice.Sim:
                    yes++;
                    break;
                default:
                    no++;
                    break;
            }
        }

        bill.Yes = yes;
        bill.No = no;
        bill.Abstain = abstain;
        bill.Decided = Week;
        bool approved = Approved(template.Quorum, yes, no, mandate.Members.Count);
        bill.Stage = approved ? BillStage.Aprovado : BillStage.Rejeitado;
        bool playerAuthor = bill.AuthorId == RaceSetup.PlayerId;
        mandate.News.Add(new MandateNews(Week, approved ? MandateNewsKind.ProjetoAprovado : MandateNewsKind.ProjetoRejeitado, bill.Title, yes, no, playerAuthor));

        if (automatic)
        {
            return;
        }

        PlayerConsequences(bill, template, playerVote, orientation);
        if (playerAuthor && approved)
        {
            State.Player.Resources.PoliticalCapital += Config.Capital.OwnBillApproved;
            Opinion(template, 1, Config.Public.OwnBillMultiplier);
            mandate.Recognition = EffectApplier.Gain(mandate.Recognition, 2 * Context.RecognitionScale);
        }
    }

    private void PlayerConsequences(Bill bill, BillTemplate template, VoteChoice vote, int orientation)
    {
        MandateState mandate = Mandate;
        int sign = vote switch { VoteChoice.Sim => 1, VoteChoice.Nao => -1, _ => 0 };
        if (vote == VoteChoice.Ausente)
        {
            mandate.Image = Math.Clamp(mandate.Image + Config.Absence.Image, -1, 1);
            ChangeFidelity(-Config.Absence.Fidelity);
            mandate.News.Add(new MandateNews(Week, MandateNewsKind.Ausencia, bill.Title));
            return;
        }

        Opinion(template, sign, 1);

        // Fidelidade partidária: votar contra a orientação custa; seguir recupera aos poucos.
        if (orientation != 0)
        {
            ChangeFidelity(sign == orientation ? Config.Fidelity.GainPerFollow
                : sign == 0 ? -Config.Fidelity.LossPerAbstention
                : -Config.Fidelity.LossPerDefiance);
        }

        if (bill.AuthorKind == BillAuthorKind.Executivo)
        {
            mandate.MayorRelation = Math.Clamp(mandate.MayorRelation + (sign > 0 ? Config.Relations.MayorWith : sign < 0 ? Config.Relations.MayorAgainst : 0), -100, 100);
            if (sign > 0 && mandate.InBase(mandate.Player.PartyId))
            {
                State.Player.Resources.PoliticalCapital += Config.Capital.WithGovernmentInBase;
            }
        }
        else if (mandate.Member(bill.AuthorId) is { IsPlayer: false } author)
        {
            author.Relation = Math.Clamp(author.Relation + (sign > 0 ? Config.Relations.AuthorWith : sign < 0 ? Config.Relations.AuthorAgainst : 0), -100, 100);
        }
    }

    /// <summary>Reação dos eleitores a um voto: apoio de cada grupo e imagem pela reação líquida da cidade.</summary>
    private void Opinion(BillTemplate template, int sign, double multiplier)
    {
        if (sign == 0)
        {
            return;
        }

        MandateState mandate = Mandate;
        double net = 0;
        foreach (var (group, popularity) in template.Popularity)
        {
            double delta = sign * popularity * template.Salience * Config.Public.SupportPerPoint * multiplier;
            mandate.Support[group] = Math.Clamp(mandate.Support.GetValueOrDefault(group) + delta, 0, 1);
            net += popularity * _groupShares.GetValueOrDefault(group);
        }

        mandate.Image = Math.Clamp(mandate.Image + (sign * net * template.Salience * Config.Public.ImagePerPoint * multiplier), -1, 1);
    }

    private void ChangeFidelity(int delta)
    {
        MandateState mandate = Mandate;
        ChamberMember player = mandate.Player;
        if (player.PartyId is null)
        {
            return;
        }

        FidelityConfig f = Config.Fidelity;
        mandate.Fidelity = Math.Clamp(mandate.Fidelity + delta, 0, 100);
        if (mandate.Fidelity < f.WarningBelow && mandate.Warnings == 0)
        {
            mandate.Warnings = 1;
            mandate.News.Add(new MandateNews(Week, MandateNewsKind.Advertencia, State.Party(player.PartyId)?.Sigla ?? player.PartyId));
        }

        if (mandate.Fidelity < f.SanctionBelow && !mandate.Sanctioned)
        {
            mandate.Sanctioned = true;
            mandate.Warnings = Math.Max(mandate.Warnings, 2);
            mandate.Committee = null;
            player.Leader = false;
            if (mandate.Members.Where(m => m.PartyId == player.PartyId && !m.IsPlayer).OrderByDescending(m => m.Votes).FirstOrDefault() is { } next)
            {
                next.Leader = true;
            }

            mandate.News.Add(new MandateNews(Week, MandateNewsKind.Sancao, State.Party(player.PartyId)?.Sigla ?? player.PartyId));
        }

        if (mandate.Fidelity < f.ExpulsionBelow && !mandate.Expelled)
        {
            // Expulso, o vereador mantém o mandato e fica sem partido até se filiar a outro.
            string sigla = State.Party(player.PartyId)?.Sigla ?? player.PartyId;
            mandate.Expelled = true;
            player.PartyId = null;
            State.Player.PartyId = null;
            mandate.News.Add(new MandateNews(Week, MandateNewsKind.Expulsao, sigla));
        }
    }

    /// <summary>Comissões: projetos que cumpriram o prazo vão ao plenário ou são arquivados.</summary>
    private void Committees()
    {
        MandateState mandate = Mandate;
        foreach (Bill bill in mandate.Bills.Where(b => b.Stage == BillStage.Comissao && b.CommitteeUntil <= Week).ToList())
        {
            double chance = 1;
            if (bill.AuthorKind == BillAuthorKind.Vereador)
            {
                chance = Config.CommitteePassChance;
                if (bill.AuthorId == RaceSetup.PlayerId)
                {
                    // Assessor legislativo e presidência da Câmara ajudam o projeto na comissão (GDD 17.14).
                    chance += State.Governance?.CommitteeBonus ?? 0;
                    chance += Config.CommitteePassPerRelation * mandate.Members.Where(m => !m.IsPlayer).Average(m => m.Relation);
                    string? theme = Template(bill).Theme;
                    if (theme is not null && Config.Committees.FirstOrDefault(c => c.Id == mandate.Committee)?.Themes.Contains(theme) == true)
                    {
                        chance += Config.CommitteeMemberBonus;
                    }
                }
            }

            if (Rng.NextDouble() < Math.Clamp(chance, 0, 1))
            {
                bill.Stage = BillStage.Plenario;
                bill.CommitteeUntil = Week.AddDays(7);
            }
            else
            {
                bill.Stage = BillStage.Arquivado;
                bill.Decided = Week;
                mandate.News.Add(new MandateNews(Week, MandateNewsKind.ProjetoArquivado, bill.Title, PlayerAuthor: bill.AuthorId == RaceSetup.PlayerId));
            }
        }
    }

    /// <summary>Novos projetos do Executivo e dos colegas (Poisson por semana), sem repetir o que já tramita.</summary>
    private void NewBills()
    {
        MandateState mandate = Mandate;
        var active = mandate.Bills.Where(b => b.Stage is BillStage.Comissao or BillStage.Plenario).Select(b => b.TemplateId).ToHashSet(StringComparer.Ordinal);
        var colleagues = mandate.Members.Where(m => !m.IsPlayer).ToList();
        foreach (var (kind, rate) in new[] { (BillAuthorKind.Executivo, Config.ExecutiveBillsPerWeek), (BillAuthorKind.Vereador, Config.NpcBillsPerWeek) })
        {
            int count = Poisson(rate);
            for (int i = 0; i < count; i++)
            {
                var options = Context.Content.MunicipalBills.Bills.Where(t => t.Author == kind && !active.Contains(t.Id)).ToList();
                if (options.Count == 0 || (kind == BillAuthorKind.Vereador && colleagues.Count == 0))
                {
                    break;
                }

                BillTemplate template = Rng.Pick(options);
                File(template, kind == BillAuthorKind.Executivo ? Bill.MayorAuthor : Rng.Pick(colleagues).Id);
                active.Add(template.Id);
            }
        }
    }

    /// <summary>Limita a pauta da próxima semana; o que passar do limite espera a seguinte.</summary>
    private void Schedule()
    {
        int limit = Rng.NextInt(Config.PlenaryPerWeek.Min, Config.PlenaryPerWeek.Max + 1);
        DateOnly next = Week.AddDays(7);
        var ready = Mandate.Bills.Where(b => b.Stage == BillStage.Plenario).OrderBy(b => b.CommitteeUntil).ThenBy(b => b.Filed).ToList();
        for (int i = 0; i < ready.Count; i++)
        {
            ready[i].CommitteeUntil = i < limit ? next : next.AddDays(7);
        }
    }

    private void Decay()
    {
        MandateState mandate = Mandate;
        foreach (ChamberMember m in mandate.Members.Where(m => !m.IsPlayer))
        {
            m.Relation -= Math.Sign(m.Relation) * Math.Min(Math.Abs(m.Relation), Config.Relations.DriftPerWeek);
        }

        mandate.MayorRelation -= Math.Sign(mandate.MayorRelation) * Math.Min(Math.Abs(mandate.MayorRelation), Config.Relations.DriftPerWeek);
        foreach (string group in mandate.Support.Keys.ToList())
        {
            mandate.Support[group] = Math.Max(0, mandate.Support[group] - Config.Public.SupportDecayPerWeek);
        }

        mandate.Recognition = Math.Max(0, mandate.Recognition - Config.Public.RecognitionDecayPerWeek);
    }

    private int Poisson(double mean)
    {
        // Knuth: suficiente para médias pequenas como as daqui.
        double limit = Math.Exp(-mean), product = Rng.NextDouble();
        int count = 0;
        while (product > limit)
        {
            count++;
            product *= Rng.NextDouble();
        }

        return count;
    }

    /// <summary>Fatia de cada grupo no eleitorado (cada dimensão soma 1).</summary>
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
