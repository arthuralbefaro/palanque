using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Elections;
using Planalto.Core.Ideology;
using Planalto.Core.Legislature;
using Planalto.Core.State;

namespace Planalto.Core.Renewal;

/// <summary>Substituição pela regra: suplente na Câmara, vice na Prefeitura, dupla vacância e nova eleição (GDD 17.16).</summary>
public sealed partial class RenewalEngine
{
    // ---------------------------------------------------------------- morte

    /// <summary>
    /// Morte de uma pessoa acompanhada: luto (lembrança em quem tinha vínculo, notícia sóbria) e, para cada papel que
    /// ela tinha, a substituição pela regra. Pode ser chamada por outro sistema (um evento, por exemplo).
    /// </summary>
    public void Die(string personId, DateOnly date)
    {
        RenewalState r = Renewal;
        if (personId == RaceSetup.PlayerId || !IsActive(personId))
        {
            return;
        }

        // O papel de maior peso dá o texto da notícia; todos os papéis são tratados.
        CivicRole main = new[] { CivicRole.Prefeito, CivicRole.VicePrefeito, CivicRole.Vereador, CivicRole.Suplente, CivicRole.Lideranca, CivicRole.Governo, CivicRole.Equipe }
            .FirstOrDefault(role => HasRole(personId, role), CivicRole.Equipe);
        RenewalEvent death = Record(date, RenewalEventKind.Morte, "morte_" + RoleKey(main), personId, main, VacancyCause.Morte, seat: r.SeatOf(personId)?.Numero);
        Mourn(personId, date, death);
        PersonStatus status = Status(personId);
        status.Saida = ExitKind.Morte;
        status.SaidaEm = date;
        if (main is CivicRole.Prefeito or CivicRole.VicePrefeito or CivicRole.Vereador)
        {
            DateOnly until = date.AddDays(Config.Luto.DiasOficiais);
            r.LutoAte = r.LutoAte is { } current && current > until ? current : until;
        }

        if (r.PrefeitoId == personId)
        {
            VacateExecutive(VacancyCause.Morte, date, death);
        }

        if (r.ViceId == personId)
        {
            VacateVice(VacancyCause.Morte, date, death);
        }

        if (r.SeatOf(personId) is not null)
        {
            VacateSeat(personId, VacancyCause.Morte, date, death);
        }

        foreach (List<string> list in r.Suplentes.Values)
        {
            list.Remove(personId);
        }

        if (City?.Leader(personId) is not null)
        {
            LeaderLeaves(personId, ExitKind.Morte, date);
        }

        if (City?.Staff.Any(s => s.PersonId == personId && !s.Left) == true)
        {
            StaffLeaves(personId, ExitKind.Morte, date);
        }
    }

    // ---------------------------------------------------------------- Câmara

    /// <summary>
    /// Vaga numa cadeira da Câmara (morte, renúncia, cassação): toma posse o suplente do mesmo partido; sem suplente,
    /// há eleição para a cadeira se faltarem mais de 15 meses de mandato, e senão ela fica vaga até o fim. Outro
    /// sistema (CPI, cassação, Justiça Eleitoral) chama este método para tirar um vereador.
    /// </summary>
    /// <returns>O fato registrado, com o desfecho e a regra aplicada; null se a pessoa não ocupa cadeira.</returns>
    public RenewalEvent? VacateSeat(string personId, VacancyCause cause, DateOnly date) => VacateSeat(personId, cause, date, null);

    private RenewalEvent? VacateSeat(string personId, VacancyCause cause, DateOnly date, RenewalEvent? origin)
    {
        RenewalState r = Renewal;
        if (r.SeatOf(personId) is not { } seat)
        {
            return null;
        }

        RenewalEvent fact = origin ?? Record(
            date,
            cause switch
            {
                VacancyCause.Renuncia => RenewalEventKind.Renuncia,
                VacancyCause.Investidura => RenewalEventKind.Investidura,
                VacancyCause.JusticaEleitoral => RenewalEventKind.JusticaEleitoral,
                _ => RenewalEventKind.Cassacao,
            },
            cause switch { VacancyCause.Renuncia => "renuncia_vereador", VacancyCause.Investidura => "investidura", VacancyCause.JusticaEleitoral => "justica_eleitoral_vereador", _ => "cassacao_vereador" },
            personId, CivicRole.Vereador, cause, seat: seat.Numero);
        MarkExit(personId, cause, date);
        seat.TitularId = null;
        seat.VagaDesde = date;
        if (personId == RaceSetup.PlayerId)
        {
            State.Mandate = null;
        }

        string? successor = NextSubstitute(seat.PartyId);
        CouncilSuccession destiny = Rules.Council(successor is not null, r.MandatoFim, date);
        seat.VagaDestino = destiny;
        fact.Regra = Rules.BaseVereador.Artigo;
        switch (destiny)
        {
            case CouncilSuccession.SuplenteAssume:
                seat.VagaPrazo = date.AddDays(Config.Prazos.PosseSuplenteDias);
                r.Suplentes[seat.PartyId].Remove(successor!);
                fact.Desfecho = SuccessionOutcome.SuplenteAssumiu;
                fact.SucessorId = successor;
                Seat(seat, successor!, date, 0);
                BridgeSeat(personId, successor, seat);
                Record(date, RenewalEventKind.PosseSuplente, "posse_suplente", personId, CivicRole.Vereador, cause, SuccessionOutcome.SuplenteAssumiu, Rules.BaseVereador.Artigo, seat.Numero, successor);
                break;
            case CouncilSuccession.NovaEleicao:
                DateOnly electionOn = date.AddDays(Config.Prazos.EleicaoCadeiraDias);
                if (electionOn > r.MandatoFim)
                {
                    goto default;
                }

                seat.VagaPrazo = electionOn;
                fact.Desfecho = SuccessionOutcome.EleicaoParaACadeira;
                fact.EleicaoEm = electionOn;
                r.EleicoesPendentes.Add(new PendingElection
                {
                    Id = r.NextEventId++, Cargo = CivicRole.Vereador, Cadeira = seat.Numero, Indireta = false, Data = electionOn, ConvocadaEm = date, Causa = cause, Regra = Rules.BaseVereador.Artigo,
                });
                BridgeSeat(personId, null, seat);
                Record(date, RenewalEventKind.EleicaoConvocada, "eleicao_cadeira_convocada", personId, CivicRole.Vereador, cause, SuccessionOutcome.EleicaoParaACadeira, Rules.BaseVereador.Artigo, seat.Numero, electionOn: electionOn);
                break;
            default:
                seat.VagaDestino = CouncilSuccession.VagaAteOFimDoMandato;
                seat.VagaPrazo = r.MandatoFim;
                fact.Desfecho = SuccessionOutcome.CadeiraVagaAteOFim;
                BridgeSeat(personId, null, seat);
                Record(
                    date, RenewalEventKind.CadeiraVaga, "cadeira_vaga", personId, CivicRole.Vereador, cause, SuccessionOutcome.CadeiraVagaAteOFim, Rules.BaseVereador.Artigo, seat.Numero,
                    extra: [("meses", Rules.MesesParaEleicaoSemSuplente.ToString(System.Globalization.CultureInfo.InvariantCulture))]);
                break;
        }

        if (r.PresidenteCamaraId == personId)
        {
            ChoosePresident();
        }

        return fact;
    }

    /// <summary>Primeiro suplente vivo e disponível do partido; null se a lista acabou.</summary>
    private string? NextSubstitute(string partyId)
    {
        if (!Renewal.Suplentes.TryGetValue(partyId, out List<string>? list))
        {
            return null;
        }

        list.RemoveAll(id => !IsActive(id));
        return list.FirstOrDefault();
    }

    /// <summary>Dá posse numa cadeira.</summary>
    private void Seat(Seat seat, string personId, DateOnly date, long votes)
    {
        seat.TitularId = personId;
        seat.Votos = votes;
        seat.PosseEm = date;
        seat.VagaDesde = null;
        seat.VagaPrazo = null;
        seat.VagaDestino = null;
        if (personId != RaceSetup.PlayerId)
        {
            Status(personId).Mandatos++;
            EnsurePerson(personId, People.PersonRole.Vereador);
        }
    }

    /// <summary>Presidente da Câmara: o mais votado da maior bancada (desempate pelo id). Responde pela Prefeitura na dupla vacância.</summary>
    private void ChoosePresident()
    {
        RenewalState r = Renewal;
        var seated = r.Cadeiras.Where(c => c.TitularId is not null).ToList();
        if (State.Mandate is { } mandate && mandate.Municipio == Municipio.CodigoIbge && mandate.PresidentId is { } current && seated.Any(c => c.TitularId == current))
        {
            r.PresidenteCamaraId = current;
            return;
        }

        string? largest = seated.GroupBy(c => c.PartyId).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).FirstOrDefault()?.Key;
        r.PresidenteCamaraId = seated.Where(c => c.PartyId == largest)
            .OrderByDescending(c => c.TitularId != RaceSetup.PlayerId).ThenByDescending(c => c.Votos).ThenBy(c => c.TitularId, StringComparer.Ordinal)
            .FirstOrDefault()?.TitularId;
        if (State.Mandate is { } m && m.Municipio == Municipio.CodigoIbge && r.PresidenteCamaraId != RaceSetup.PlayerId)
        {
            m.PresidentId = r.PresidenteCamaraId;
        }

        if (r.ExecutivoVagoDesde is not null && r.PrefeitoId is null)
        {
            // Com a Prefeitura vaga, quem preside a Câmara responde por ela.
            BridgeMayor();
        }
    }

    // ---------------------------------------------------------------- Prefeitura

    /// <summary>
    /// O prefeito sai. Por morte, renúncia ou cassação pela Câmara, assume o vice; sem vice, o presidente da Câmara
    /// responde pela Prefeitura e há nova eleição, direta na primeira metade do mandato e indireta na segunda. Por
    /// decisão da Justiça Eleitoral, cai a chapa e há nova eleição, indireta só a menos de 6 meses do fim do mandato.
    /// </summary>
    /// <returns>O fato registrado, com o desfecho e a regra aplicada; null se não há prefeito.</returns>
    public RenewalEvent? VacateExecutive(VacancyCause cause, DateOnly date) => VacateExecutive(cause, date, null);

    private RenewalEvent? VacateExecutive(VacancyCause cause, DateOnly date, RenewalEvent? origin)
    {
        RenewalState r = Renewal;
        if (r.PrefeitoId is not { } mayor)
        {
            return null;
        }

        bool court = cause == VacancyCause.JusticaEleitoral;
        string? vice = !court && r.ViceId is { } v && IsActive(v) ? v : null;
        ExecutiveSuccession destiny = Rules.Executive(cause, vice is not null, r.MandatoInicio, r.MandatoFim, date);
        LegalBasis basis = court ? Rules.BaseJusticaEleitoral : Rules.BaseDuplaVacancia;
        RenewalEvent fact = origin ?? Record(
            date,
            cause switch { VacancyCause.Renuncia => RenewalEventKind.Renuncia, VacancyCause.JusticaEleitoral => RenewalEventKind.JusticaEleitoral, _ => RenewalEventKind.Cassacao },
            cause switch { VacancyCause.Renuncia => "renuncia_prefeito", VacancyCause.JusticaEleitoral => "justica_eleitoral", _ => "cassacao_prefeito" },
            mayor, CivicRole.Prefeito, cause);
        fact.Regra = basis.Artigo;
        MarkExit(mayor, cause, date);
        r.PrefeitoId = null;
        if (mayor == RaceSetup.PlayerId)
        {
            State.Executive = null;
        }

        if (court && r.ViceId is { } fallen)
        {
            // A decisão da Justiça Eleitoral atinge a chapa: o vice cai junto e não assume.
            MarkExit(fallen, cause, date);
            r.ViceId = null;
            r.VicePartyId = null;
        }

        if (destiny == ExecutiveSuccession.ViceAssume)
        {
            r.PrefeitoId = vice;
            r.PrefeitoPartyId = r.VicePartyId ?? Npc(vice!)?.PartyId ?? r.PrefeitoPartyId;
            r.PrefeitoMandatos = 1;
            r.ViceId = null;
            r.VicePartyId = null;
            Status(vice!).Mandatos++;
            EnsurePerson(vice!, null);
            fact.Desfecho = SuccessionOutcome.ViceAssumiu;
            fact.SucessorId = vice;
            Record(date, RenewalEventKind.PosseVice, "posse_vice", mayor, CivicRole.Prefeito, cause, SuccessionOutcome.ViceAssumiu, basis.Artigo, successorId: vice);
            BridgeMayor();
            return fact;
        }

        // Dupla vacância: o presidente da Câmara responde pela Prefeitura até a nova eleição.
        r.ExecutivoVagoDesde = date;
        r.PrefeitoPartyId = null;
        if (r.PresidenteCamaraId is null || !IsActive(r.PresidenteCamaraId))
        {
            ChoosePresident();
        }

        DateOnly electionOn = Rules.ElectionDate(destiny, date);
        if (electionOn > r.MandatoFim)
        {
            fact.Desfecho = SuccessionOutcome.InterinoAteOFim;
            Record(date, RenewalEventKind.Interino, "interino_ate_o_fim", r.PresidenteCamaraId, CivicRole.Prefeito, cause, SuccessionOutcome.InterinoAteOFim, basis.Artigo);
            BridgeMayor();
            return fact;
        }

        bool indirect = destiny == ExecutiveSuccession.EleicaoIndireta;
        fact.Desfecho = indirect ? SuccessionOutcome.EleicaoIndireta : SuccessionOutcome.EleicaoDireta;
        fact.EleicaoEm = electionOn;
        r.EleicoesPendentes.Add(new PendingElection
        {
            Id = r.NextEventId++, Cargo = CivicRole.Prefeito, Indireta = indirect, Data = electionOn, ConvocadaEm = date, Causa = cause, Regra = basis.Artigo,
        });
        Record(date, RenewalEventKind.Interino, "interino", r.PresidenteCamaraId, CivicRole.Prefeito, cause, fact.Desfecho, basis.Artigo);
        Record(
            date, RenewalEventKind.EleicaoConvocada, indirect ? "eleicao_indireta_convocada" : "eleicao_direta_convocada", null, CivicRole.Prefeito, cause, fact.Desfecho, basis.Artigo,
            electionOn: electionOn);
        BridgeMayor();
        return fact;
    }

    /// <summary>O vice sai: o cargo não tem substituto e fica vago; se o prefeito sair depois, é dupla vacância.</summary>
    public RenewalEvent? VacateVice(VacancyCause cause, DateOnly date) => VacateVice(cause, date, null);

    private RenewalEvent? VacateVice(VacancyCause cause, DateOnly date, RenewalEvent? origin)
    {
        RenewalState r = Renewal;
        if (r.ViceId is not { } vice)
        {
            return null;
        }

        RenewalEvent fact = origin ?? Record(
            date, cause == VacancyCause.Renuncia ? RenewalEventKind.Renuncia : RenewalEventKind.Cassacao,
            cause == VacancyCause.Renuncia ? "renuncia_vice_prefeito" : "cassacao_vice_prefeito", vice, CivicRole.VicePrefeito, cause);
        MarkExit(vice, cause, date);
        r.ViceId = null;
        r.VicePartyId = null;
        fact.Desfecho = SuccessionOutcome.ViceFicaVago;
        fact.Regra = Rules.BaseDuplaVacancia.Artigo;
        return fact;
    }

    private void MarkExit(string personId, VacancyCause cause, DateOnly date)
    {
        if (personId == RaceSetup.PlayerId || cause == VacancyCause.Investidura)
        {
            return;
        }

        PersonStatus status = Status(personId);
        status.Saida ??= cause switch
        {
            VacancyCause.Morte => ExitKind.Morte,
            VacancyCause.Renuncia => ExitKind.Renuncia,
            VacancyCause.JusticaEleitoral => ExitKind.JusticaEleitoral,
            _ => ExitKind.Cassacao,
        };
        status.SaidaEm ??= date;
    }

    // ---------------------------------------------------------------- eleições fora de época

    private void ResolveElection(PendingElection election)
    {
        if (election.Cargo == CivicRole.Vereador)
        {
            ResolveSeatElection(election);
        }
        else if (Renewal.PrefeitoId is null)
        {
            ResolveMayorElection(election);
        }
    }

    /// <summary>Eleição da cadeira sem suplente: um nome novo, do partido sorteado pela força das bancadas.</summary>
    private void ResolveSeatElection(PendingElection election)
    {
        RenewalState r = Renewal;
        if (r.Cadeiras.FirstOrDefault(c => c.Numero == election.Cadeira) is not { TitularId: null } seat)
        {
            return;
        }

        string party = PickParty(null);
        string winner = Newcomer(party, election.Data, election.Data.Year);
        seat.PartyId = party;
        Seat(seat, winner, election.Data, 0);
        BridgeSeat(null, winner, seat);
        Record(election.Data, RenewalEventKind.EleicaoResolvida, "eleicao_cadeira_resultado", winner, CivicRole.Vereador, election.Causa, SuccessionOutcome.EleicaoParaACadeira, election.Regra, seat.Numero);
    }

    /// <summary>
    /// Nova eleição para a Prefeitura. Indireta: os vereadores escolhem entre os mais votados das duas maiores
    /// bancadas, cada um votando no nome do próprio partido ou no do partido mais próximo. Direta: disputa entre quem
    /// responde pela Prefeitura, nomes das bancadas e um nome novo. Quem vence completa o mandato.
    /// </summary>
    private void ResolveMayorElection(PendingElection election)
    {
        RenewalState r = Renewal;
        DateOnly date = election.Data;
        var seated = r.Cadeiras.Where(c => c.TitularId is not null && c.TitularId != RaceSetup.PlayerId && MeetsMayorAge(c.TitularId, date)).ToList();
        string winner;
        string party;
        int yes = 0, no = 0;
        if (election.Indireta && seated.Count > 0)
        {
            var candidates = seated.GroupBy(c => c.PartyId).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Take(2)
                .Select(g => g.OrderByDescending(c => c.Votos).ThenBy(c => c.TitularId, StringComparer.Ordinal).First()).ToList();
            var votes = candidates.ToDictionary(c => c.TitularId!, _ => 0, StringComparer.Ordinal);
            foreach (Seat voter in r.Cadeiras.Where(c => c.TitularId is not null))
            {
                Seat choice = candidates.FirstOrDefault(c => c.PartyId == voter.PartyId)
                    ?? candidates.OrderBy(c => PartyDistance(voter.PartyId, c.PartyId)).ThenBy(_ => Rng.NextDouble()).First();
                votes[choice.TitularId!]++;
            }

            // Empate: vence o mais idoso, o mesmo desempate da apuração do Core.
            Seat elected = candidates.OrderByDescending(c => votes[c.TitularId!]).ThenBy(c => Npc(c.TitularId!)?.BirthDate ?? DateOnly.MaxValue).First();
            winner = elected.TitularId!;
            party = elected.PartyId;
            yes = votes[winner];
            no = votes.Values.Sum() - yes;
        }
        else
        {
            var candidates = new List<(string? Id, string Party, double Weight)>();
            if (r.PresidenteCamaraId is { } interim && seated.FirstOrDefault(c => c.TitularId == interim) is { } interimSeat)
            {
                candidates.Add((interim, interimSeat.PartyId, Config.Eleicao.PesoInterino * LogNormal(Config.Eleicao.DispersaoCandidatos)));
            }

            foreach (Seat top in seated.Where(c => c.TitularId != r.PresidenteCamaraId).GroupBy(c => c.PartyId)
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Take(2)
                .Select(g => g.OrderByDescending(c => c.Votos).ThenBy(c => c.TitularId, StringComparer.Ordinal).First()))
            {
                candidates.Add((top.TitularId, top.PartyId, LogNormal(Config.Eleicao.DispersaoCandidatos)));
            }

            candidates.Add((null, PickParty(null), LogNormal(Config.Eleicao.DispersaoCandidatos)));
            var chosen = candidates.MaxBy(c => c.Weight);
            party = chosen.Party;
            winner = chosen.Id ?? Newcomer(party, date, date.Year, minAge: Context.Candidacy.MinimumAgeFor(Office.Prefeito));
        }

        // Vereador eleito prefeito deixa a cadeira, e o suplente dele assume.
        if (r.SeatOf(winner) is not null)
        {
            VacateSeat(winner, VacancyCause.Investidura, date);
        }

        string viceParty = PickParty(party);
        r.PrefeitoId = winner;
        r.PrefeitoPartyId = party;
        r.PrefeitoMandatos = 1;
        r.ViceId = Newcomer(viceParty, date, date.Year, minAge: Context.Candidacy.MinimumAgeFor(Office.Prefeito));
        r.VicePartyId = viceParty;
        r.ExecutivoVagoDesde = null;
        Status(winner).Mandatos++;
        Status(r.ViceId);
        EnsurePerson(winner, null);
        Record(
            date, RenewalEventKind.EleicaoResolvida, election.Indireta ? "eleicao_indireta_resultado" : "eleicao_direta_resultado", winner, CivicRole.Prefeito, election.Causa,
            election.Indireta ? SuccessionOutcome.EleicaoIndireta : SuccessionOutcome.EleicaoDireta, election.Regra, partyId: party,
            extra: [("sim", yes.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("nao", no.ToString(System.Globalization.CultureInfo.InvariantCulture))]);
        BridgeMayor();
        if (State.Indicators?.Municipio == Municipio.CodigoIbge && State.Executive is null)
        {
            Indicators.IndicatorEvolution.MarkMandateStart(State.Indicators, date);
        }
    }

    private bool MeetsMayorAge(string? personId, DateOnly date) =>
        personId is not null && Npc(personId) is { } npc && npc.AgeOn(date) >= Context.Candidacy.MinimumAgeFor(Office.Prefeito);

    private double PartyDistance(string a, string b) =>
        ChamberSetup.Distance(State.Party(a)?.Position ?? new IdeologicalPosition(0, 0), State.Party(b)?.Position ?? new IdeologicalPosition(0, 0));

    /// <summary>Partido sorteado pela força das bancadas (cadeiras + base), opcionalmente diferente de um dado partido.</summary>
    private string PickParty(string? differentFrom)
    {
        var seats = Renewal.Cadeiras.GroupBy(c => c.PartyId).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        List<string> parties = State.Parties.Select(p => p.Id).Where(p => p != differentFrom).ToList();
        if (parties.Count == 0)
        {
            return differentFrom ?? State.Parties[0].Id;
        }

        return Rng.PickWeighted(parties, p => seats.GetValueOrDefault(p) + Config.Eleicao.BasePartido);
    }

    // ---------------------------------------------------------------- jogador

    /// <summary>
    /// O jogador que deixa o cargo no meio do mandato (renúncia para disputar outro cargo, por exemplo) abre vaga como
    /// qualquer um: o vice assume a Prefeitura, o suplente assume a cadeira. Depois da eleição, quem não se reelegeu
    /// só espera o fim do mandato.
    /// </summary>
    private void DetectPlayerExit(DateOnly week)
    {
        RenewalState r = Renewal;
        if (r.Eleitos is not null || r.UltimaEleicaoAno >= r.MandatoFim.Year
            || (Planalto.Core.State.Calendar.ElectionKindOf(r.MandatoFim.Year) is not null && week >= Planalto.Core.State.Calendar.ElectionFor(r.MandatoFim.Year).FirstRound))
        {
            return;
        }

        if (r.PrefeitoId == RaceSetup.PlayerId && State.Executive is null)
        {
            VacateExecutive(VacancyCause.Renuncia, week);
        }

        if (r.SeatOf(RaceSetup.PlayerId) is not null && State.Mandate is null && State.Executive is null)
        {
            VacateSeat(RaceSetup.PlayerId, VacancyCause.Renuncia, week);
        }
    }

    // ---------------------------------------------------------------- ligação com o mandato do jogador

    /// <summary>Leva a troca numa cadeira para o mandato do jogador (vereador ou prefeito) e para a política local.</summary>
    private void BridgeSeat(string? oldId, string? newId, Seat seat)
    {
        // Entre a eleição jogada e a posse, o mandato do jogador no estado já é o novo: a Câmara que sai não mexe nele.
        if (Renewal.Eleitos is { Jogada: true })
        {
            return;
        }

        List<ChamberMember>? members = State.Mandate is { } mandate && mandate.Municipio == Municipio.CodigoIbge ? mandate.Members
            : State.Executive is { } executive && executive.Municipio == Municipio.CodigoIbge ? executive.Chamber : null;
        if (members is not null)
        {
            bool wasLeader = false;
            if (oldId is not null && oldId != RaceSetup.PlayerId && members.FirstOrDefault(m => m.Id == oldId) is { } old)
            {
                wasLeader = old.Leader;
                members.Remove(old);
            }

            if (newId is not null && newId != RaceSetup.PlayerId && members.All(m => m.Id != newId) && Npc(newId) is { } npc)
            {
                IdeologicalPosition center = State.Party(seat.PartyId)?.Position ?? new IdeologicalPosition(0, 0);
                members.Add(new ChamberMember
                {
                    Id = npc.Id,
                    Name = npc.BallotName,
                    PartyId = seat.PartyId,
                    Position = new IdeologicalPosition(
                        Math.Clamp(center.Economic + Rng.NextInt(-8, 9), AxisRange.AxisMin, AxisRange.AxisMax),
                        Math.Clamp(center.Social + Rng.NextInt(-8, 9), AxisRange.AxisMin, AxisRange.AxisMax)),
                    Votes = seat.Votos,
                });
            }

            if (wasLeader && members.Where(m => m.PartyId == seat.PartyId).OrderByDescending(m => m.Votes).ThenBy(m => m.Id, StringComparer.Ordinal).FirstOrDefault() is { } next)
            {
                next.Leader = true;
            }
        }

        if (State.Local is { } local && local.Municipio == Municipio.CodigoIbge && Renewal.Eleitos is null && local.ElectionYear == Renewal.UltimaEleicaoAno)
        {
            if (oldId is not null)
            {
                local.Councillors.Remove(oldId);
                local.CouncillorVotes.Remove(oldId);
            }

            if (newId is not null && !local.Councillors.Contains(newId))
            {
                local.Councillors.Add(newId);
                local.CouncillorVotes[newId] = seat.Votos;
            }
        }
    }

    /// <summary>Leva a troca na Prefeitura para o mandato de vereador do jogador e para a política local.</summary>
    private void BridgeMayor()
    {
        RenewalState r = Renewal;
        if (r.Eleitos is { Jogada: true })
        {
            return;
        }

        // Com a Prefeitura vaga, quem responde por ela é o presidente da Câmara.
        string? acting = r.PrefeitoId ?? r.PresidenteCamaraId;
        string? party = r.PrefeitoId is not null ? r.PrefeitoPartyId : r.Cadeiras.FirstOrDefault(c => c.TitularId == acting)?.PartyId;
        if (State.Mandate is { } mandate && mandate.Municipio == Municipio.CodigoIbge && acting is not null && party is not null)
        {
            mandate.MayorId = acting;
            mandate.MayorName = NameOf(acting);
            mandate.MayorPartyId = party;
        }

        if (State.Local is { } local && local.Municipio == Municipio.CodigoIbge && Renewal.Eleitos is null && local.ElectionYear == Renewal.UltimaEleicaoAno)
        {
            local.MayorId = r.PrefeitoId;
            local.MayorPartyId = r.PrefeitoPartyId;
            local.MayorReelected = r.PrefeitoMandatos >= 2;
        }
    }
}
