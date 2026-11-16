using System.Globalization;
using Planalto.Core.Campaign;
using Planalto.Core.Elections;
using Planalto.Core.Executive;
using Planalto.Core.Legislature;
using Planalto.Core.State;
using Calendar = Planalto.Core.State.Calendar;

namespace Planalto.Core.Career;

/// <summary>Em que momento da carreira o jogador está (decide o que o hub mostra).</summary>
public enum CareerMode
{
    /// <summary>Campanha sem mandato (a primeira, ou depois de uma derrota).</summary>
    Campanha,

    /// <summary>Campanha com um mandato em curso (vereador ou prefeito), que segue sozinho em segundo plano.</summary>
    CampanhaEmMandato,

    Mandato,

    Prefeitura,

    ForaDeMandato,
}

/// <summary>
/// Costura da carreira (GDD 6.2): campanha → apuração → posse ou derrota → mandato → nova campanha no ano
/// da eleição seguinte. Sem textos: a interface decide como mostrar cada passo.
/// </summary>
public static class CareerFlow
{
    public static CareerMode Mode(GameState state) => state.Campaign is not null
        ? state.Mandate is not null || state.Executive is not null ? CareerMode.CampanhaEmMandato : CareerMode.Campanha
        : state.Executive is not null ? CareerMode.Prefeitura
        : state.Mandate is not null ? CareerMode.Mandato : CareerMode.ForaDeMandato;

    /// <summary>
    /// Depois da apuração: grava a política local, dá posse (se eleito) em 1º de janeiro ou encerra o mandato
    /// anterior, leva o reconhecimento da campanha para os recursos do personagem e salta o calendário até a posse.
    /// </summary>
    /// <param name="campaignContext">Contexto da campanha apurada.</param>
    /// <param name="legislatureContext">Contexto do mandato de vereador.</param>
    /// <param name="executiveContext">Contexto da prefeitura; obrigatório se o jogador foi eleito prefeito.</param>
    /// <param name="renewal">Renovação do mundo político (GDD 17.16): recebe os eleitos e os suplentes para a posse.</param>
    public static void ConcludeElection(CampaignContext campaignContext, LegislatureContext legislatureContext, ExecutiveContext? executiveContext = null, Renewal.RenewalEngine? renewal = null)
    {
        GameState state = campaignContext.State;
        CampaignState campaign = state.Campaign ?? throw new InvalidOperationException("Sem campanha para concluir.");
        ElectionOutcome outcome = campaign.Outcome ?? throw new InvalidOperationException("A eleição ainda não foi apurada.");

        ChamberSetup.RecordLocalPolitics(legislatureContext, campaign, state.Rng);
        renewal?.OnElectionConcluded(campaign);
        string constituency = campaign.Municipio.ToString(CultureInfo.InvariantCulture);
        state.Player.Resources.SetRecognition(constituency, (int)Math.Round(campaign.Player.Recognition));

        bool mayor = campaign.Player.Office == Office.Prefeito;
        state.Mandate = outcome.PlayerElected && !mayor ? ChamberSetup.StartMandate(legislatureContext, campaign, state.Rng) : null;
        state.Executive = outcome.PlayerElected && mayor
            ? ExecutiveEngine.Start(executiveContext ?? throw new ArgumentNullException(nameof(executiveContext), "Prefeito eleito sem contexto do Executivo."), campaign, state.Rng)
            : null;
        state.Campaign = null;
        state.Calendar.JumpTo(new DateOnly(outcome.Date.Year + 1, 1, 1));
    }

    /// <summary>
    /// Abertura da pré-campanha numa eleição seguinte à primeira: o prazo de filiação (6 meses antes), depois do
    /// qual o partido de cada um está definido. A primeira campanha da carreira começa direto na criação.
    /// </summary>
    public static DateOnly CampaignOpens(int year) => Calendar.ElectionFor(year).PartyAffiliationDeadline;

    /// <summary>Ano de eleição municipal, já no prazo de filiação e antes da propaganda, sem campanha criada.</summary>
    public static bool ShouldStartCampaign(GameState state)
    {
        int year = state.Calendar.Year;
        return state.Campaign is null
            && Calendar.ElectionKindOf(year) == ElectionKind.Municipal
            && state.Calendar.WeekEnd >= CampaignOpens(year)
            && state.Calendar.WeekStart < Calendar.ElectionFor(year).CampaignStart;
    }

    /// <summary>
    /// Pode disputar a eleição municipal do ano: partido e filiação no prazo e idade mínima para vereador (a
    /// candidatura padrão; com 18 anos, aferida no prazo do registro).
    /// </summary>
    public static bool CanRun(GameState state, int electionYear) =>
        PartyAffiliation.CanRun(state, electionYear) && MeetsAge(state, Office.Vereador, electionYear);

    public static bool MeetsAge(GameState state, Office office, int electionYear) =>
        CandidacyRulesConfig.Default.MeetsAge(state.Player.BirthDate, office, electionYear);

    /// <summary>Primeira eleição municipal, a partir da atual, em que o jogador já tem idade para vereador.</summary>
    public static int NextEligibleElectionYear(GameState state)
    {
        int year = NextMunicipalElectionYear(state);
        while (!MeetsAge(state, Office.Vereador, year))
        {
            year = RaceSetup.NextMunicipalYear(year + 1);
        }

        return year;
    }

    /// <summary>Próximo ano de eleição municipal em que ainda dá para fazer campanha.</summary>
    public static int NextMunicipalElectionYear(GameState state)
    {
        int year = RaceSetup.NextMunicipalYear(state.Calendar.Year);
        if (state.Calendar.WeekStart >= Calendar.ElectionFor(year).CampaignStart)
        {
            year = RaceSetup.NextMunicipalYear(year + 1);
        }

        return year;
    }

    /// <summary>Fora de mandato: salta para a abertura da pré-campanha da próxima eleição municipal.</summary>
    public static void SkipToNextElection(GameState state)
    {
        if (state.Mandate is not null || state.Executive is not null || state.Campaign is not null)
        {
            throw new InvalidOperationException("Só dá para pular o tempo fora de mandato e de campanha.");
        }

        state.Calendar.JumpTo(CampaignOpens(NextMunicipalElectionYear(state)));
    }

    /// <summary>
    /// Uma semana da carreira: na campanha de reeleição o mandato vota sozinho (seguindo a orientação) e a
    /// campanha avança; no mandato, só ele; fora de mandato, só o calendário.
    /// </summary>
    /// <param name="state">Estado do jogo.</param>
    /// <param name="campaign">Motor da campanha em curso, se houver.</param>
    /// <param name="legislature">Motor do mandato de vereador, se houver.</param>
    /// <param name="founding">Fundação de partido em curso, se houver.</param>
    /// <param name="executive">Motor da prefeitura, se houver.</param>
    /// <param name="world">O mundo que anda sozinho (indicadores e renovação); anda até a semana do calendário antes de tudo.</param>
    public static void AdvanceWeek(
        GameState state, CampaignEngine? campaign, LegislatureEngine? legislature, PartyFoundingEngine? founding = null, ExecutiveEngine? executive = null, World.WorldEngine? world = null)
    {
        world?.AdvanceTo(state.Calendar.WeekStart);
        founding?.Tick();
        if (state.Campaign is not null)
        {
            if (campaign is null)
            {
                throw new InvalidOperationException("Campanha em curso sem motor.");
            }

            if (state.Mandate is not null && legislature is not null && !legislature.Over)
            {
                legislature.ResolveWeek(automatic: legislature.Context.Config.AutoVoteInCampaign);
            }

            if (state.Executive is not null && executive is not null && !executive.Over)
            {
                executive.ResolveWeek(automatic: true);
            }

            campaign.AdvanceWeek();
            return;
        }

        if (state.Executive is not null)
        {
            ExecutiveEngine engine = executive ?? throw new InvalidOperationException("Prefeitura sem motor.");
            engine.AdvanceWeek();
            if (engine.Over)
            {
                state.Executive = null;
            }

            return;
        }

        if (state.Mandate is not null)
        {
            LegislatureEngine engine = legislature ?? throw new InvalidOperationException("Mandato em curso sem motor.");
            engine.AdvanceWeek();
            if (engine.Over)
            {
                // Mandato terminado sem disputa de reeleição (ex.: sem partido no prazo de filiação).
                state.Mandate = null;
            }

            return;
        }

        state.Calendar.AdvanceWeek();
    }

    /// <summary>
    /// Troca o cargo disputado na pré-campanha. Prefeito em exercício que disputa outro cargo renuncia ao mandato
    /// (desincompatibilização, CF art. 14, § 6º); devolve true quando houve renúncia.
    /// </summary>
    public static (CandidacyIssue? Issue, bool Renounced) SwitchCandidacy(CampaignContext ctx, Office office)
    {
        GameState state = ctx.State;
        if (RaceSetup.SwitchPlayerOffice(ctx, office, state.Rng) is { } issue)
        {
            return (issue, false);
        }

        bool renounce = state.Executive is not null && office != Office.Prefeito;
        if (renounce)
        {
            state.Executive = null;
        }

        return (null, renounce);
    }
}
