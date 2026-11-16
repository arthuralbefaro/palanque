using Planalto.Core.Elections;
using Planalto.Core.Media;
using Planalto.Core.People;
using Planalto.Core.State;

namespace Planalto.Core.Campaign;

/// <summary>Cota do fundo eleitoral que o dirigente daria hoje, com os três fatores da decisão dele.</summary>
public sealed record FundView(string? BossId, string BossName, decimal Quota, double Relation, double Viability, double LineDistance, DateOnly PaidOn, bool Paid, IReadOnlyList<string> Explanation);

/// <summary>
/// Doadores e prestação de contas (GDD 17.12): fundo eleitoral pela direção do partido, doadores pessoa física com
/// pedido e teto legal, recursos próprios, financiamento coletivo e dinheiro "por fora" (que vira esqueleto com a
/// lista de quem sabe). O risco de contas soma as irregularidades; esqueletos podem vir à tona toda semana.
/// </summary>
public sealed partial class CampaignEngine
{
    private FinanceCatalog FinanceContent => Context.Campaign.Finance;

    private FinanceRules FinanceRules => FinanceContent.Regras;

    private CampaignFinanceLawConfig FinanceLaw => Context.FinanceLaw;

    public FinanceState Finance => Campaign.Finance;

    /// <summary>
    /// Limite de gastos do cargo no município. A tabela do TSE não foi importada: vale a substituição marcada de
    /// data/campaign/finance.json (valor por eleitor, com piso e teto, por porte), que é parâmetro de jogo.
    /// </summary>
    public decimal SpendingLimit
    {
        get
        {
            SpendingLimitRule rule = FinanceContent.LimiteVereador[Context.Municipio.Porte];
            decimal limit = Math.Clamp(rule.PorEleitor * Context.Municipio.Eleitorado, rule.Piso, rule.Teto);
            if (Campaign.Player.Office == Office.Prefeito)
            {
                limit *= FinanceContent.LimitePrefeitoFator;
            }

            return Math.Round(limit / 100) * 100;
        }
    }

    /// <summary>Gasto de campanha: sai do caixa e entra na conta do limite.</summary>
    private void Pay(decimal amount)
    {
        if (amount <= 0)
        {
            return;
        }

        State.Player.Resources.Money -= amount;
        Campaign.Player.Money = State.Player.Resources.Money;
        Finance.Spent += amount;
    }

    private void RegisterSpending(decimal amount)
    {
        if (amount > 0)
        {
            Finance.Spent += amount;
        }
    }

    private void Receive(string source, decimal amount, string? donorId = null)
    {
        if (amount <= 0)
        {
            return;
        }

        State.Player.Resources.Money += amount;
        Campaign.Player.Money = State.Player.Resources.Money;
        RegisterReceipt(source, amount, donorId);
    }

    /// <summary>Anota a entrada (o dinheiro já está no caixa).</summary>
    private void RegisterReceipt(string source, decimal amount, string? donorId = null)
    {
        if (amount <= 0)
        {
            return;
        }

        Finance.Receipts.Add(new FinanceEntry(Today, source, amount, donorId, source != FinanceCatalog.PorFora));
        if (Finance.Receipts.Count > FinanceState.MaxEntries)
        {
            Finance.Receipts.RemoveRange(0, Finance.Receipts.Count - FinanceState.MaxEntries);
        }

        Finance.BySource[source] = Finance.BySource.GetValueOrDefault(source) + amount;
    }

    /// <summary>Recursos próprios usados: o que foi gasto além de tudo o que entrou (art. 23, § 2º-A: até 10% do limite).</summary>
    public decimal OwnResourcesUsed => Math.Max(0, Finance.Spent - Finance.Declared - Finance.OffBooks);

    private IEnumerable<LedgerSkeleton> AccountSkeletons() =>
        State.Ledger.Skeletons.Where(s => FinanceCatalog.Irregularidades.Contains(s.Type) && (Finance.Since is not { } since || s.Created >= since));

    private double IrregularityRisk(string type, decimal amount, int severity, decimal limit) =>
        amount > 0 && FinanceRules.RiscoPorPercentual.TryGetValue(type, out double perPercent)
            ? perPercent * (double)(amount / Math.Max(1, limit)) * 100
            : FinanceRules.RiscoPorGravidade.GetValueOrDefault(type) * Math.Max(1, severity);

    /// <summary>
    /// Painel Contas: arrecadado por fonte, gasto, barra do limite, irregularidades com "quem sabe" e o risco de
    /// contas (0 a 100), que soma gasto não declarado, doação acima do teto, gasto acima do limite, recursos
    /// próprios acima do permitido e erro contábil.
    /// </summary>
    public AccountsView Accounts() => Accounts(0, 0, null);

    /// <summary>
    /// O painel como ficaria se entrasse mais esse dinheiro (com recibo ou "por fora") e mais essa irregularidade:
    /// é a conta que a previsão de uma doação mostra antes do clique.
    /// </summary>
    private AccountsView Accounts(decimal moreDeclared, decimal moreOffBooks, (string Type, string Text, decimal Amount, int Severity)? more)
    {
        decimal limit = SpendingLimit;
        decimal declared = Finance.Declared + moreDeclared;
        decimal received = Finance.OffBooks + moreOffBooks;
        var list = new List<IrregularityView>();
        foreach (LedgerSkeleton s in AccountSkeletons())
        {
            list.Add(new IrregularityView(s.Type, s.Text, s.Amount, s.Severity, IrregularityRisk(s.Type, s.Amount, s.Severity, limit), s.KnownBy, s.KnownBy.Select(NameOf).ToList(), s.Exposed, s.Id));
        }

        if (more is { } extra)
        {
            list.Add(new IrregularityView(extra.Type, extra.Text, extra.Amount, extra.Severity, IrregularityRisk(extra.Type, extra.Amount, extra.Severity, limit), [], [], false, null));
        }

        // Irregularidades que nascem da conta, não de um ato: recursos próprios acima de 10% do limite e gasto acima do limite.
        decimal own = Math.Max(0, Finance.Spent - declared - received);
        decimal ownLimit = FinanceLaw.LimiteRecursosProprios(limit);
        if (own > ownLimit)
        {
            decimal excess = own - ownLimit;
            list.Add(new IrregularityView(FinanceCatalog.RecursosPropriosAcima, FinanceContent.Texto("esqueleto_proprios"), excess, 1, IrregularityRisk(FinanceCatalog.RecursosPropriosAcima, excess, 1, limit), [], [], false, null));
        }

        decimal declaredSpent = Math.Max(0, Finance.Spent - received);
        if (declaredSpent > limit)
        {
            decimal excess = declaredSpent - limit;
            list.Add(new IrregularityView(FinanceCatalog.GastoAcimaDoLimite, FinanceContent.Texto("esqueleto_limite"), excess, 2, IrregularityRisk(FinanceCatalog.GastoAcimaDoLimite, excess, 2, limit), [], [], false, null));
        }

        int risk = (int)Math.Round(Math.Clamp(list.Sum(i => i.Risk), 0, 100));
        decimal offBooks = list.Where(i => FinanceCatalog.PorForaTipos.Contains(i.Type)).Sum(i => i.Amount);
        return new AccountsView(
            declared + received, new Dictionary<string, decimal>(Finance.BySource, StringComparer.Ordinal), Finance.Spent, limit, limit <= 0 ? 0 : (double)(Finance.Spent / limit),
            own, ownLimit, offBooks, limit <= 0 ? 0 : (double)(offBooks / limit), list, risk,
            FinanceLaw.PrestacaoParcial(Campaign.ElectionDate.Year), FinanceLaw.PrestacaoFinal(Campaign.ElectionDate), !FinanceLaw.TabelaDoTseImportada);
    }

    public int AccountsRisk => Accounts().Risk;

    // ------------------------------------------------------------------ doadores

    public decimal DonorLegalCap(Donor donor) => FinanceLaw.LimiteDoacao(donor.Income);

    public decimal DonorCapLeft(Donor donor) => Math.Max(0, DonorLegalCap(donor) - donor.Donated);

    /// <summary>Doadores da cidade, com o teto legal, o que cada um pede e a oferta em aberto.</summary>
    public IReadOnlyList<DonorView> Donors() => City.Donors
        .Select(d => new DonorView(d, NameOf(d.PersonId), RelationOf(d.PersonId), DonorLegalCap(d), DonorCapLeft(d), d.RequestText, Finance.Offers.FirstOrDefault(o => o.DonorId == d.PersonId)))
        .ToList();

    private static string AnswerKey(DonationAnswer answer) => answer switch
    {
        DonationAnswer.Oficial => "oficial",
        DonationAnswer.OficialIntegral => "oficial_integral",
        DonationAnswer.PorFora => "por_fora",
        _ => "recusar",
    };

    /// <summary>O que a resposta à oferta ganha e custa, por natureza (princípio e).</summary>
    public DecisionTrade DonationTrade(DonationAnswer answer) => FinanceContent.Respostas[AnswerKey(answer)];

    /// <summary>Quanto entra no caixa com cada resposta à oferta.</summary>
    private decimal DonationAmount(DonorOffer offer, Donor donor, DonationAnswer answer) => answer switch
    {
        DonationAnswer.PorFora => offer.PorFora ? offer.Amount : 0,
        DonationAnswer.OficialIntegral => offer.PorFora ? 0 : offer.Amount,
        DonationAnswer.Oficial => Math.Floor(Math.Min(DonorCapLeft(donor), offer.PorFora ? offer.Amount * (decimal)FinanceRules.OficialNoLugarDoPorFora : offer.Amount)),
        _ => 0,
    };

    private int OffBooksSeverity(decimal amount) => Math.Clamp(1 + (int)Math.Floor((double)(amount / Math.Max(1, SpendingLimit)) / FinanceRules.GravidadePasso), 1, 3);

    public CommandFailure? CanAnswerDonation(string offerId, DonationAnswer answer)
    {
        if (Finance.Offers.FirstOrDefault(o => o.Id == offerId) is not { } offer || City.Donor(offer.DonorId) is not { } donor)
        {
            return CommandFailure.NaoEncontrado;
        }

        if (offer.Deadline < Today)
        {
            return CommandFailure.PrazoVencido;
        }

        return answer switch
        {
            DonationAnswer.PorFora when !offer.PorFora => CommandFailure.ValorInvalido,
            DonationAnswer.OficialIntegral when offer.PorFora || offer.Amount <= DonorCapLeft(donor) => CommandFailure.ValorInvalido,
            DonationAnswer.Oficial when DonationAmount(offer, donor, answer) <= 0 => CommandFailure.LimiteLegal,
            _ => null,
        };
    }

    /// <summary>Quem fica sabendo de dinheiro "por fora": quem entregou e quem cuida do caixa e da campanha.</summary>
    private List<string> OffBooksWitnesses(string donorId) =>
        new[] { donorId }.Concat(HiredStaff.Where(s => StaffRules.FuncoesQueSabem.Contains(s.Role)).Select(s => s.PersonId)).ToList();

    /// <summary>
    /// Previsão da resposta a uma oferta de doação: o dinheiro que entra, o risco de contas depois, a dívida que
    /// fica no Livro e, no "por fora", quem fica sabendo.
    /// </summary>
    public CommandForecast ForecastDonation(string offerId, DonationAnswer answer)
    {
        CommandFailure? blocked = CanAnswerDonation(offerId, answer);
        if (Finance.Offers.FirstOrDefault(o => o.Id == offerId) is not { } offer || City.Donor(offer.DonorId) is not { } donor)
        {
            return new CommandForecast(blocked ?? CommandFailure.NaoEncontrado, [], [], [FinanceContent.Texto("explica_recusa")]);
        }

        decimal amount = DonationAmount(offer, donor, answer);
        int risk = AccountsRisk;
        int after = risk;
        string name = NameOf(donor.PersonId);
        var explanation = new List<string>();
        string memoryKey = "doacao_recusada";
        switch (answer)
        {
            case DonationAnswer.Oficial:
                memoryKey = "doacao_aceita";
                after = Accounts(amount, 0, null).Risk;
                explanation.Add(Texts.Fill(FinanceContent.Texto("explica_oficial"), ("nome", name), ("valor", Texts.Money(amount))));
                explanation.Add(Texts.Fill(FinanceContent.Texto("explica_teto"), ("artigo", FinanceLaw.ArtigoDoacaoPessoaFisica), ("teto", Texts.Money(DonorLegalCap(donor))), ("resta", Texts.Money(DonorCapLeft(donor)))));
                explanation.Add(Texts.Fill(FinanceContent.Texto("explica_divida"), ("nome", name), ("pedido", donor.RequestText)));
                break;
            case DonationAnswer.OficialIntegral:
            {
                memoryKey = "doacao_aceita";
                decimal excess = Math.Max(0, amount - DonorCapLeft(donor));
                after = Accounts(amount, 0, excess > 0 ? (FinanceCatalog.DoacaoAcimaDoLimite, "", excess, 1) : null).Risk;
                explanation.Add(Texts.Fill(FinanceContent.Texto("explica_oficial"), ("nome", name), ("valor", Texts.Money(amount))));
                explanation.Add(Texts.Fill(FinanceContent.Texto("explica_acima"), ("excesso", Texts.Money(excess)), ("multa", Texts.Money(FinanceLaw.MultaMaximaPorExcesso(donor.Donated + amount, donor.Income))), ("artigo", FinanceLaw.ArtigoDoacaoPessoaFisica)));
                explanation.Add(Texts.Fill(FinanceContent.Texto("explica_divida"), ("nome", name), ("pedido", donor.RequestText)));
                break;
            }

            case DonationAnswer.PorFora:
                memoryKey = "doacao_por_fora";
                after = Accounts(0, amount, (FinanceCatalog.CaixaDois, "", amount, OffBooksSeverity(amount))).Risk;
                explanation.Add(Texts.Fill(FinanceContent.Texto("explica_por_fora"), ("nome", name), ("valor", Texts.Money(amount)), ("fracao", Texts.Percent((double)(amount / Math.Max(1, SpendingLimit))))));
                explanation.Add(Texts.Fill(FinanceContent.Texto("explica_quem_sabe"), ("quem", string.Join(", ", OffBooksWitnesses(donor.PersonId).Select(NameOf)))));
                explanation.Add(Texts.Fill(FinanceContent.Texto("explica_divida"), ("nome", name), ("pedido", donor.RequestText)));
                break;
            default:
                explanation.Add(Texts.Fill(FinanceContent.Texto("explica_recusa"), ("nome", name)));
                break;
        }

        MemoryDefinition memory = FinanceContent.Memory(memoryKey);
        var lines = new List<ForecastLine>
        {
            new("money", null, (double)amount, (double)amount, (double)amount),
            new("risk", null, after - risk, after - risk, after - risk),
            new("relation", donor.PersonId, memory.Pontos, memory.Pontos, memory.Pontos),
        };
        return new CommandForecast(blocked, lines, [], explanation);
    }

    /// <summary>
    /// Responde à oferta de um doador: com recibo até o teto legal, com recibo o valor inteiro (acima do teto vira
    /// irregularidade), "por fora" (vira esqueleto com quem sabe) ou recusar. Aceitar grava a dívida no Livro.
    /// </summary>
    public (CommandFailure? Failure, DonationResult? Result) AnswerDonation(string offerId, DonationAnswer answer)
    {
        if (CanAnswerDonation(offerId, answer) is { } failure)
        {
            return (failure, null);
        }

        DonorOffer offer = Finance.Offers.First(o => o.Id == offerId);
        Donor donor = City.Donor(offer.DonorId)!;
        Person person = City.Person(donor.PersonId)!;
        Finance.Offers.Remove(offer);
        int riskBefore = AccountsRisk;
        decimal amount = DonationAmount(offer, donor, answer);
        string name = NameOf(donor.PersonId);
        LedgerSkeleton? skeleton = null;
        Debt? debt = null;
        string memoryKey;
        switch (answer)
        {
            case DonationAnswer.Oficial:
            case DonationAnswer.OficialIntegral:
            {
                decimal excess = Math.Max(0, amount - DonorCapLeft(donor));
                donor.Donated += amount;
                Receive(FinanceCatalog.PessoaFisica, amount, donor.PersonId);
                if (excess > 0)
                {
                    skeleton = State.Ledger.AddSkeleton(FinanceCatalog.DoacaoAcimaDoLimite, Texts.Fill(FinanceContent.Texto("esqueleto_acima"), ("nome", name)), 1, Today, [donor.PersonId], excess);
                }

                memoryKey = "doacao_aceita";
                break;
            }

            case DonationAnswer.PorFora:
                donor.OffBooks += amount;
                Receive(FinanceCatalog.PorFora, amount, donor.PersonId);
                skeleton = State.Ledger.AddSkeleton(FinanceCatalog.CaixaDois, Texts.Fill(FinanceContent.Texto("esqueleto_por_fora"), ("nome", name)), OffBooksSeverity(amount), Today, OffBooksWitnesses(donor.PersonId), amount);
                memoryKey = "doacao_por_fora";
                break;
            default:
                memoryKey = "doacao_recusada";
                break;
        }

        if (amount > 0 && donor.DebtId is null)
        {
            // O doador cobra no mandato: a dívida fica no Livro uma vez por eleição.
            debt = State.Ledger.AddDebt(donor.PersonId, Texts.Fill(FinanceContent.Texto("divida_livro"), ("nome", name), ("pedido", donor.RequestText)), Today);
            donor.DebtId = debt.Id;
        }

        MemoryDefinition definition = FinanceContent.Memory(memoryKey);
        person.LastContact = Today;
        Memory memory = person.Remember(Today, memoryKey, definition.Peso, definition.Pontos, definition.Texto, PeopleConfig);
        string text = Texts.Fill(FinanceContent.Texto(answer == DonationAnswer.Recusar ? "explica_recusa" : answer == DonationAnswer.PorFora ? "explica_por_fora" : "explica_oficial"),
            ("nome", name), ("valor", Texts.Money(amount)), ("fracao", Texts.Percent((double)(amount / Math.Max(1, SpendingLimit)))));
        if (answer != DonationAnswer.Recusar)
        {
            // Doação com recibo é pública na prestação de contas; a "por fora" só é conhecida por quem estava presente.
            Emit(FactKind.Doacao, text, donor.PersonId, target: RaceSetup.PlayerId, sub: AnswerKey(answer), visibility: answer == DonationAnswer.PorFora ? 0 : null, feed: true);
        }

        return (null, new DonationResult(amount, riskBefore, AccountsRisk, debt, skeleton, memory, text));
    }

    // ------------------------------------------------------------------ fundo eleitoral

    private string? BossId => City.Bosses.GetValueOrDefault(Campaign.Player.PartyId);

    /// <summary>Distância entre as posições assumidas pelo jogador e a linha do partido (0 a 1).</summary>
    private double LineDistance()
    {
        if (Platform.Positions.Count == 0)
        {
            return 0;
        }

        double span = Ideology.ThemeDefinition.MaxPosition - Ideology.ThemeDefinition.MinPosition;
        return Math.Clamp(Platform.Positions.Average(p => Math.Abs(p.Value - PartyPosition(p.Key))) / span, 0, 1);
    }

    /// <summary>Viabilidade: os votos do jogador hoje em relação ao mais votado da própria chapa (0 a 1).</summary>
    private double Viability(Dictionary<string, double>? projected = null)
    {
        Dictionary<string, double> votes = projected ?? ProjectedVotes();
        Candidate player = Campaign.Player;
        double best = Campaign.ForOffice(player.Office).Where(c => !c.IsBulk && c.PartyId == player.PartyId).Select(c => votes.GetValueOrDefault(c.Id)).DefaultIfEmpty(0).Max();
        return best <= 0 ? 0 : Math.Clamp(votes.GetValueOrDefault(player.Id) / best, 0, 1);
    }

    /// <summary>
    /// Cota do fundo eleitoral que o dirigente do partido daria hoje: decide pela relação com o jogador, pela
    /// viabilidade na pesquisa e pela distância da linha do partido (GDD 17.12).
    /// </summary>
    public FundView FundQuota() => FundQuota(null);

    private FundView FundQuota(Dictionary<string, double>? votes)
    {
        FinanceRules r = FinanceRules;
        string? bossId = BossId;
        double relation = bossId is null ? 0 : RelationOf(bossId);
        double viability = Viability(votes);
        double distance = LineDistance();
        double share = Math.Clamp(r.FundoPiso + (r.FundoPesoRelacao * (relation + 100) / 200) + (r.FundoPesoViabilidade * viability) + (r.FundoPesoLinha * (1 - distance)), 0, 1);
        decimal quota = Math.Round(r.FundoBase * (decimal)(CostScale * share) / 10) * 10;
        string name = bossId is null ? "" : NameOf(bossId);
        var explanation = new List<string>
        {
            Texts.Fill(FinanceContent.Texto("fundo_explica"), ("nome", name), ("valor", Texts.Money(quota)), ("relacao", Texts.Number(relation, "0")), ("viabilidade", Texts.Percent(viability)), ("linha", Texts.Percent(distance))),
        };
        return new FundView(bossId, name, quota, relation, viability, distance, Campaign.CampaignStart, Finance.FundPaid, explanation);
    }

    // ------------------------------------------------------------------ o dia e a semana do dinheiro

    private void FinanceDay(DateOnly day)
    {
        foreach (DonorOffer offer in Finance.Offers.Where(o => o.Deadline < day).ToList())
        {
            Finance.Offers.Remove(offer);
            MemoryDefinition memory = FinanceContent.Memory("oferta_ignorada");
            City.Person(offer.DonorId)?.Remember(day, "oferta_ignorada", memory.Peso, memory.Pontos, memory.Texto, PeopleConfig);
        }
    }

    /// <summary>
    /// Segunda-feira: a direção paga a cota do fundo no início da campanha oficial; o financiamento coletivo junta
    /// (e é liberado com o registro); doadores fazem ofertas; a prestação parcial sai em setembro; e cada esqueleto
    /// pode vir à tona.
    /// </summary>
    /// <param name="votes">Projeção de votos já feita nesta virada (a dos planos dos rivais), se houver.</param>
    private void FinanceWeek(Dictionary<string, double>? votes)
    {
        if (!CampaignRunning)
        {
            return;
        }

        FinanceRules r = FinanceRules;
        int year = Campaign.ElectionDate.Year;
        DateOnly weekEnd = State.Calendar.WeekEnd;

        if (!Finance.FundPaid && weekEnd >= Campaign.CampaignStart)
        {
            Finance.FundPaid = true;
            FundView fund = FundQuota(votes);
            if (fund.Quota > 0 && fund.BossId is not null)
            {
                Receive(FinanceCatalog.Fundo, fund.Quota);
                MemoryDefinition memory = FinanceContent.Memory("fundo");
                City.Person(fund.BossId)?.Remember(Today, "fundo", memory.Peso, memory.Pontos, memory.Texto, PeopleConfig);
                State.Ledger.AddDebt(fund.BossId, Texts.Fill(FinanceContent.Texto("fundo_divida"), ("nome", fund.BossName)), Today);
                Emit(FactKind.Fundo, Texts.Fill(FinanceContent.Texto("fundo"), ("nome", fund.BossName), ("valor", Texts.Money(fund.Quota))), fund.BossId, target: RaceSetup.PlayerId);
            }
        }

        // Financiamento coletivo (art. 22-A, § 3º): desde 15 de maio; o que entra antes do registro fica retido.
        if (weekEnd >= FinanceLaw.FinanciamentoColetivoAbre(year))
        {
            decimal weekly = Math.Round(r.ColetivoSemanal * (decimal)(CostScale * Math.Clamp(Campaign.Player.Recognition / 100, 0, 1)));
            bool registered = State.Calendar.WeekStart > FinanceLaw.Registro(year) || !FinanceLaw.FinanciamentoColetivoLiberadoSoComRegistro;
            if (!registered)
            {
                Finance.CrowdHeld += weekly;
            }
            else
            {
                if (!Finance.CrowdReleased)
                {
                    Finance.CrowdReleased = true;
                    if (Finance.CrowdHeld > 0)
                    {
                        Emit(FactKind.Doacao, Texts.Fill(FinanceContent.Texto("coletivo_liberado"), ("valor", Texts.Money(Finance.CrowdHeld))), RaceSetup.PlayerId, sub: FinanceCatalog.Coletivo, visibility: 0);
                        Receive(FinanceCatalog.Coletivo, Finance.CrowdHeld);
                        Finance.CrowdHeld = 0;
                    }
                }

                Receive(FinanceCatalog.Coletivo, weekly);
            }
        }

        // Ofertas de doadores: mais prováveis de quem tem boa relação; algumas vêm "por fora".
        decimal limit = SpendingLimit;
        foreach (Donor donor in City.Donors)
        {
            if (Finance.Offers.Count >= r.MaxOfertasAbertas || Finance.Offers.Any(o => o.DonorId == donor.PersonId))
            {
                continue;
            }

            double chance = Math.Clamp(r.ChanceOfertaSemanal + (RelationOf(donor.PersonId) * r.ChanceOfertaPorRelacao), 0, 1);
            if (!Rng.Chance(chance))
            {
                continue;
            }

            bool offBooks = Rng.Chance(r.ChancePorFora);
            decimal amount;
            if (offBooks)
            {
                amount = Math.Round(limit * (decimal)Rng.NextDouble(r.PorForaFracaoDoLimite.Min, r.PorForaFracaoDoLimite.Max) / 10) * 10;
            }
            else
            {
                decimal left = DonorCapLeft(donor);
                amount = Math.Round(left * (decimal)Rng.NextDouble(r.OfertaFracaoDoTeto.Min, r.OfertaFracaoDoTeto.Max) / 10) * 10;
                if (Rng.Chance(r.ChanceAcimaDoTeto))
                {
                    amount = Math.Round(Math.Max(left, DonorLegalCap(donor) * (decimal)r.OfertaFracaoDoTeto.Min) * (decimal)r.AcimaDoTetoFator / 10) * 10;
                }
            }

            if (amount <= 0)
            {
                continue;
            }

            var offer = new DonorOffer { Id = $"of{Finance.NextId++}", DonorId = donor.PersonId, Amount = amount, PorFora = offBooks, Deadline = Today.AddDays(r.OfertaPrazoDias) };
            Finance.Offers.Add(offer);
            Emit(FactKind.Doacao, Texts.Fill(FinanceContent.Texto(offBooks ? "oferta_por_fora" : "oferta_oficial"), ("nome", NameOf(donor.PersonId)), ("valor", Texts.Money(amount)), ("pedido", donor.RequestText)),
                donor.PersonId, target: RaceSetup.PlayerId, person: donor.PersonId, sub: "oferta", visibility: 0);
        }

        // Prestação de contas parcial, em setembro: o que está irregular pode chamar a atenção de um jornalista.
        if (!Finance.PartialDone && weekEnd >= FinanceLaw.PrestacaoParcial(year))
        {
            Finance.PartialDone = true;
            AccountsView accounts = Accounts();
            Emit(FactKind.Contas, Texts.Fill(FinanceContent.Texto("parcial"), ("arrecadado", Texts.Money(Finance.Declared)), ("gasto", Texts.Money(Math.Max(0, Finance.Spent - Finance.OffBooks)))), RaceSetup.PlayerId, sub: "parcial");
            List<Outlet> outlets = City.Outlets.Where(o => !OutletContent.Type(o.Type).Rede).ToList();
            foreach (LedgerSkeleton skeleton in AccountSkeletons().Where(s => !s.Exposed).ToList())
            {
                if (outlets.Count > 0 && Rng.Chance(Math.Min(1, r.ParcialChancePorRisco * accounts.Risk)))
                {
                    Outlet outlet = Rng.Pick(outlets);
                    if (!skeleton.KnownBy.Contains(outlet.JournalistId))
                    {
                        skeleton.KnownBy.Add(outlet.JournalistId);
                        Emit(FactKind.Contas, Texts.Fill(FinanceContent.Texto("parcial_jornalista"), ("nome", NameOf(outlet.JournalistId)), ("veiculo", OutletLabel(outlet))), outlet.JournalistId,
                            target: RaceSetup.PlayerId, person: outlet.JournalistId, sub: "apuracao", visibility: 0);
                    }
                }
            }
        }

        SkeletonsWeek(votes);
    }

    /// <summary>Quem sabe do esqueleto e tem relação (ou lealdade, na equipe) baixa o bastante para falar.</summary>
    public IReadOnlyList<string> HostileKnowers(LedgerSkeleton skeleton) => skeleton.KnownBy.Where(IsHostile).ToList();

    private bool IsHostile(string personId)
    {
        if (City.Staff.FirstOrDefault(s => s.PersonId == personId && s.Hired) is { } member)
        {
            return member.Loyalty < FinanceRules.LealdadeHostil;
        }

        return City.Person(personId) is { } person && person.Relation(Today, PeopleConfig) < FinanceRules.RelacaoHostil;
    }

    /// <summary>Exposição do candidato: o cargo disputado e a fatia de votos na projeção de hoje.</summary>
    public double Exposure() => Exposure(ProjectedVotes());

    private double Exposure(Dictionary<string, double> votes)
    {
        Candidate player = Campaign.Player;
        double total = Campaign.ForOffice(player.Office).Sum(c => votes.GetValueOrDefault(c.Id));
        double share = total <= 0 ? 0 : votes.GetValueOrDefault(player.Id) / total;
        return FinanceRules.ExposicaoPorCargo[player.Office == Office.Prefeito ? "prefeito" : "vereador"] * (1 + (FinanceRules.ExposicaoPorFatia * share));
    }

    /// <summary>Chance de o esqueleto vir à tona nesta semana = base × gravidade × pessoas hostis que sabem × exposição.</summary>
    public double SurfaceChance(LedgerSkeleton skeleton) => SurfaceChance(skeleton, Exposure());

    private double SurfaceChance(LedgerSkeleton skeleton, double exposure) =>
        skeleton.Exposed ? 0 : Math.Clamp(FinanceRules.EsqueletoBase * skeleton.Severity * HostileKnowers(skeleton).Count * exposure, 0, 1);

    private void SkeletonsWeek(Dictionary<string, double>? votes)
    {
        double? exposure = null;
        foreach (LedgerSkeleton skeleton in State.Ledger.Skeletons.Where(s => !s.Exposed).ToList())
        {
            IReadOnlyList<string> hostile = HostileKnowers(skeleton);
            if (hostile.Count == 0)
            {
                continue;
            }

            // A projeção da cidade só é feita se houver quem possa falar.
            exposure ??= votes is null ? Exposure() : Exposure(votes);
            if (Rng.Chance(SurfaceChance(skeleton, exposure.Value)))
            {
                ExposeSkeleton(skeleton, Rng.Pick(hostile), announce: true);
            }
        }
    }

    /// <summary>O esqueleto vem à tona: vira fato de visibilidade máxima, custa imagem pela gravidade e, se for das contas, conta como denúncia.</summary>
    private void ExposeSkeleton(LedgerSkeleton skeleton, string informantId, bool announce)
    {
        if (skeleton.Exposed)
        {
            return;
        }

        skeleton.Exposed = true;
        if (FinanceCatalog.Irregularidades.Contains(skeleton.Type) && !Finance.Denuncias.Contains(informantId))
        {
            Finance.Denuncias.Add(informantId);
        }

        if (announce)
        {
            Campaign.Player.Image = Math.Clamp(Campaign.Player.Image - (FinanceRules.EsqueletoImagem * skeleton.Severity), -1, 1);
            Emit(FactKind.Esqueleto, Texts.Fill(FinanceContent.Texto("veio_a_tona"), ("texto", skeleton.Text), ("nome", NameOf(informantId))), informantId, target: RaceSetup.PlayerId, person: informantId, sub: skeleton.Type, visibility: 3);
        }
    }

    /// <summary>
    /// Depois da apuração: fecha a contabilidade da campanha num processo de contas que segue no estado do jogo
    /// (prestação final, julgamento e, havendo irregularidade grave e denúncia, a representação do art. 30-A).
    /// </summary>
    private void OpenAccountsCase(bool elected)
    {
        AccountsView accounts = Accounts();
        List<LedgerSkeleton> offBooks = AccountSkeletons().Where(s => FinanceCatalog.PorForaTipos.Contains(s.Type)).ToList();
        State.Accounts = new AccountsCase
        {
            Municipio = Campaign.Municipio,
            ElectionDate = Campaign.ElectionDate,
            Elected = elected,
            Limit = accounts.Limit,
            Spent = accounts.Spent,
            OffBooks = accounts.OffBooks,
            Risk = accounts.Risk,
            HostileKnowers = offBooks.SelectMany(s => s.KnownBy).Distinct().Count(IsHostile),
            Exposed = offBooks.Any(s => s.Exposed),
            Denuncias = Finance.Denuncias.Count,
        };
    }
}

/// <summary>
/// As contas depois da eleição (GDD 17.12): prestação final 30 dias depois, julgamento (aprovadas, com ressalvas,
/// desaprovadas) e, com irregularidade grave e denúncia, a representação do art. 30-A, aberta em até 15 dias da
/// diplomação, que pode cassar o diploma depois da posse. Anda por data, sem depender da campanha (que já acabou).
/// </summary>
public static class AccountsEngine
{
    /// <summary>Chance de alguém denunciar: a imprensa (cresce com o tamanho do "por fora"), cada pessoa hostil que sabe e o que já veio à tona.</summary>
    public static double DenunciationChance(AccountsCase c, FinanceRules r)
    {
        if (c.OffBooks <= 0)
        {
            return 0;
        }

        double quiet = 1 - Math.Clamp(r.DenunciaImprensa * Math.Min(1, c.OffBooksShare), 0, 1);
        quiet *= Math.Pow(1 - Math.Clamp(r.DenunciaPorHostil, 0, 1), c.HostileKnowers + c.Denuncias);
        if (c.Exposed)
        {
            quiet *= 1 - Math.Clamp(r.DenunciaSeExposto, 0, 1);
        }

        return 1 - quiet;
    }

    public static bool Grave(AccountsCase c, FinanceRules r) => c.OffBooksShare >= r.GraveFracaoDoLimite;

    public static double CassationChance(AccountsCase c, FinanceRules r) =>
        Math.Clamp(r.CassacaoBase + (r.CassacaoPorFracao * c.OffBooksShare), 0, r.CassacaoMaxima);

    /// <summary>Avança o processo de contas da última eleição do save até a data. Devolve as manchetes do que aconteceu agora.</summary>
    public static List<Headline> Advance(GameState state, FinanceCatalog content, CampaignFinanceLawConfig law, DateOnly today, Rng rng) =>
        state.Accounts is { } accounts ? Advance(accounts, content, law, today, rng) : [];

    /// <summary>Avança um processo de contas até a data. Devolve as manchetes do que aconteceu agora.</summary>
    public static List<Headline> Advance(AccountsCase c, FinanceCatalog content, CampaignFinanceLawConfig law, DateOnly today, Rng rng)
    {
        var news = new List<Headline>();
        if (c.Stage == AccountsStage.Encerrado)
        {
            return news;
        }

        FinanceRules r = content.Regras;
        void Log(string key, string kind)
        {
            var headline = new Headline(today, content.Texto(key), kind);
            c.Log.Add(headline);
            news.Add(headline);
        }

        if (c.Stage == AccountsStage.AguardandoPrestacao && today >= law.PrestacaoFinal(c.ElectionDate))
        {
            c.Stage = AccountsStage.AguardandoJulgamento;
            c.JudgmentOn = today.AddDays(7 * rng.NextInt(r.JulgamentoSemanas.Min, r.JulgamentoSemanas.Max + 1));
            Log("prestacao_final", "contas");
        }

        // Representação do art. 30-A: até 15 dias da diplomação, se houver irregularidade grave e quem denuncie.
        DateOnly diplomacao = DateOnly.ParseExact($"{c.ElectionDate.Year}-{r.Diplomacao}", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        if (!c.RepresentationChecked && today >= diplomacao)
        {
            c.RepresentationChecked = true;
            if (c.Elected && Grave(c, r) && today <= law.RepresentacaoAte(diplomacao).AddDays(6) && rng.Chance(DenunciationChance(c, r)))
            {
                c.RepresentationOpen = true;
                c.RulingOn = today.AddDays(7 * rng.NextInt(r.RepresentacaoSemanas.Min, r.RepresentacaoSemanas.Max + 1));
                Log("representacao", "representacao");
            }
        }

        if (c.Stage == AccountsStage.AguardandoJulgamento && c.JudgmentOn is { } judgment && today >= judgment)
        {
            c.Verdict = c.Risk >= r.RiscoDesaprovacao ? AccountsVerdict.Desaprovadas : c.Risk >= r.RiscoRessalvas ? AccountsVerdict.AprovadasComRessalvas : AccountsVerdict.Aprovadas;
            c.Stage = AccountsStage.Julgadas;
            Log(c.Verdict == AccountsVerdict.Desaprovadas ? "desaprovadas" : c.Verdict == AccountsVerdict.AprovadasComRessalvas ? "ressalvas" : "aprovadas", "contas");
        }

        if (c.RepresentationOpen && c.RulingOn is { } ruling && today >= ruling)
        {
            c.RepresentationOpen = false;
            c.RulingOn = null;
            if (law.RepresentacaoCassaODiploma && rng.Chance(CassationChance(c, r)))
            {
                c.MandateLost = true;
                c.MandateLostOn = today;
                Log("cassado", "cassacao");
            }
            else
            {
                Log("absolvido", "representacao");
            }
        }

        if (c.Stage == AccountsStage.Julgadas && c.RepresentationChecked && !c.RepresentationOpen)
        {
            c.Stage = AccountsStage.Encerrado;
        }

        return news;
    }
}
