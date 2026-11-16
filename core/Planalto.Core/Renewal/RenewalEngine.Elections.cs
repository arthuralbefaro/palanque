using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Elections;
using Planalto.Core.Legislature;
using Planalto.Core.People;
using Planalto.Core.State;

namespace Planalto.Core.Renewal;

/// <summary>Renovação a cada eleição (GDD 17.16): quem sai, quem tenta de novo, quem entra, e a posse em 1º de janeiro.</summary>
public sealed partial class RenewalEngine
{
    private sealed record ListEntry(string? Id, string Key, double Weight, bool Incumbent, int Age);

    // ---------------------------------------------------------------- eleição jogada

    /// <summary>
    /// Depois da apuração da eleição jogada (com a política local já gravada): guarda os eleitos para a posse em 1º de
    /// janeiro. Os suplentes de cada partido são os candidatos nomeados não eleitos, na ordem de votação, completados
    /// por nomes novos; o vice é gerado, de outro partido.
    /// </summary>
    public void OnElectionConcluded(CampaignState campaign)
    {
        RenewalState r = Ensure();
        ElectionOutcome outcome = campaign.Outcome ?? throw new InvalidOperationException("Eleição sem resultado.");
        LocalPolitics local = State.Local ?? throw new InvalidOperationException("Política local não registrada.");
        r.Eleitos = SlateFromLocal(local, campaign, outcome.Date);
        r.UltimaEleicaoAno = outcome.Date.Year;
    }

    private ElectedSlate SlateFromLocal(LocalPolitics local, CampaignState? campaign, DateOnly date)
    {
        _drafted.Clear();
        string PartyOf(string id) => (id == RaceSetup.PlayerId ? State.Player.PartyId : Npc(id)?.PartyId) ?? State.Parties[0].Id;
        var seats = local.Councillors.Select(id => new ElectedSeat { PessoaId = id, PartyId = PartyOf(id), Votos = local.CouncillorVotes.GetValueOrDefault(id) }).ToList();
        var substitutes = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var group in seats.GroupBy(s => s.PartyId).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var list = new List<string>();
            if (campaign?.Outcome is { } outcome)
            {
                list.AddRange(campaign.Candidates
                    .Where(c => c.Office == Office.Vereador && c.PartyId == group.Key && c.CharacterId is not null && !c.IsPlayer && !local.Councillors.Contains(c.CharacterId))
                    .OrderByDescending(c => outcome.Vereador.Votes.GetValueOrDefault(c.Id)).ThenBy(c => c.Id, StringComparer.Ordinal)
                    .Select(c => c.CharacterId!));
            }

            int wanted = Config.Suplentes.For(group.Count());
            while (list.Count < wanted)
            {
                list.Add(Newcomer(group.Key, date, local.ElectionYear));
            }

            substitutes[group.Key] = list;
        }

        string? mayorParty = local.MayorPartyId;
        string viceParty = PickParty(mayorParty);
        return new ElectedSlate
        {
            AnoEleicao = local.ElectionYear,
            Jogada = true,
            Vereadores = seats,
            Suplentes = substitutes,
            PrefeitoId = local.MayorId,
            PrefeitoPartyId = mayorParty,
            ViceId = Newcomer(viceParty, date, local.ElectionYear, minAge: Context.Candidacy.MinimumAgeFor(Office.Prefeito)),
            VicePartyId = viceParty,
            PrefeitoMandatos = local.MayorReelected ? 2 : 1,
        };
    }

    // ---------------------------------------------------------------- eleição automática

    /// <summary>
    /// Eleição municipal sem o jogador em campanha. Quem tem mandato decide se disputa (aposentadoria pela idade e
    /// pelas derrotas, desistência); cada partido monta a lista com quem fica, os suplentes e nomes novos; os votos
    /// são sorteados pela força das bancadas e a apuração é a proporcional do Core (quociente, sobras e cláusulas).
    /// Os não eleitos de cada partido, na ordem de votação, viram suplentes. O resultado toma posse em 1º de janeiro.
    /// </summary>
    private void RunAutomaticElection(DateOnly date, int year)
    {
        RenewalState r = Renewal;
        AutomaticElectionConfig config = Config.Eleicao;
        _drafted.Clear();
        var posse = new DateOnly(year + 1, 1, 1);
        int seats = Math.Max(1, Context.Projection?.Seats(Municipio, year) ?? (r.Cadeiras.Count > 0 ? r.Cadeiras.Count : Municipio.VagasVereador));

        // 1. Quem tem cadeira decide se disputa de novo.
        var lists = State.Parties.ToDictionary(p => p.Id, _ => new List<ListEntry>(), StringComparer.Ordinal);
        foreach (Seat seat in r.Cadeiras.Where(c => c.TitularId is not null && c.TitularId != RaceSetup.PlayerId))
        {
            string id = seat.TitularId!;
            if (!IsActive(id) || Npc(id) is not { } npc || !lists.TryGetValue(seat.PartyId, out List<ListEntry>? list))
            {
                continue;
            }

            if (DecidesNotToRun(id, npc, posse, date, CivicRole.Vereador))
            {
                continue;
            }

            list.Add(new ListEntry(id, id, config.PesoIncumbente, true, npc.AgeOn(posse)));
        }

        foreach (var (party, ids) in r.Suplentes)
        {
            if (!lists.TryGetValue(party, out List<ListEntry>? list))
            {
                continue;
            }

            foreach (string id in ids.Where(IsActive))
            {
                if (Npc(id) is { } npc && npc.AgeOn(posse) < Config.Aposentadoria.IdadeMaxima && !Rng.Chance(Config.Aposentadoria.Chance(npc.AgeOn(posse), Status(id).Derrotas)))
                {
                    list.Add(new ListEntry(id, id, config.PesoSuplente, false, npc.AgeOn(posse)));
                }
            }
        }

        // 2. Força de cada partido e tamanho da lista; os nomes novos só viram personagem se forem eleitos ou suplentes.
        var current = r.Cadeiras.GroupBy(c => c.PartyId).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var strength = State.Parties.OrderBy(p => p.Id, StringComparer.Ordinal)
            .ToDictionary(p => p.Id, p => (current.GetValueOrDefault(p.Id) + config.BasePartido) * LogNormal(config.DispersaoPartidos), StringComparer.Ordinal);
        double total = strength.Values.Sum();
        long valid = Math.Max(seats * 100L, (long)(Municipio.Eleitorado * config.Comparecimento));
        var partyLists = new List<PartyList>();
        var entries = new Dictionary<string, ListEntry>(StringComparer.Ordinal);
        foreach (var (party, weight) in strength)
        {
            List<ListEntry> list = lists[party];
            double expected = seats * weight / total;
            int size = Math.Max(list.Count, (int)Math.Ceiling(expected * config.CandidatosPorCadeira) + Config.Suplentes.MinimoPorPartido);
            for (int i = list.Count; i < size; i++)
            {
                list.Add(new ListEntry(null, $"novo_{party}_{i}", 1, false, EntryAge(18)));
            }

            var scored = list.Select(e => (Entry: e, Score: e.Weight * LogNormal(config.DispersaoCandidatos))).ToList();
            double scoreTotal = scored.Sum(s => s.Score);
            long partyVotes = (long)(valid * weight / total);
            partyLists.Add(new PartyList(party, 0, scored.Select(s =>
            {
                entries[s.Entry.Key] = s.Entry;
                return new CandidateVotes(s.Entry.Key, (long)(partyVotes * s.Score / scoreTotal), posse.AddYears(-s.Entry.Age));
            }).ToList()));
        }

        ProportionalResult result = ElectionRules.Proportional(seats, partyLists);

        // 3. Eleitos e suplentes; quem tinha mandato e não entrou soma uma derrota.
        var elected = new List<ElectedSeat>();
        var substitutes = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var electedKeys = result.Elected.Select(e => e.CandidateId).ToHashSet(StringComparer.Ordinal);

        // Se as cláusulas de desempenho deixarem cadeira sem dono, ela vai para o mais votado que sobrou: a Câmara
        // toma posse completa.
        foreach (CandidateVotes extra in partyLists.SelectMany(p => p.Candidates).Where(c => !electedKeys.Contains(c.CandidateId))
            .OrderByDescending(c => c.Votes).ThenBy(c => c.CandidateId, StringComparer.Ordinal).Take(Math.Max(0, seats - electedKeys.Count)).ToList())
        {
            electedKeys.Add(extra.CandidateId);
        }
        foreach (PartyList party in partyLists)
        {
            int won = party.Candidates.Count(c => electedKeys.Contains(c.CandidateId));
            int keep = won > 0 ? Config.Suplentes.For(won) : 0;
            foreach (CandidateVotes candidate in party.Candidates.OrderByDescending(c => c.Votes).ThenBy(c => c.CandidateId, StringComparer.Ordinal))
            {
                ListEntry entry = entries[candidate.CandidateId];
                bool wins = electedKeys.Contains(candidate.CandidateId);
                if (!wins && keep <= 0)
                {
                    if (entry.Id is not null)
                    {
                        Status(entry.Id).Derrotas++;
                    }

                    continue;
                }

                string id = entry.Id ?? Newcomer(party.PartyId, date, year, age: entry.Age);
                if (wins)
                {
                    elected.Add(new ElectedSeat { PessoaId = id, PartyId = party.PartyId, Votos = candidate.Votes });
                }
                else
                {
                    keep--;
                    if (entry.Incumbent)
                    {
                        Status(id).Derrotas++;
                    }

                    if (!substitutes.TryGetValue(party.PartyId, out List<string>? list))
                    {
                        substitutes[party.PartyId] = list = [];
                    }

                    list.Add(id);
                }
            }
        }

        // 4. Prefeitura.
        var (mayor, mayorParty, vice, viceParty, terms) = MayorRace(date, year, posse);
        int reelected = elected.Count(e => r.SeatOf(e.PessoaId) is not null);
        r.Eleitos = new ElectedSlate
        {
            AnoEleicao = year,
            Jogada = false,
            Vereadores = elected.OrderByDescending(e => e.Votos).ThenBy(e => e.PessoaId, StringComparer.Ordinal).ToList(),
            Suplentes = substitutes,
            PrefeitoId = mayor,
            PrefeitoPartyId = mayorParty,
            ViceId = vice,
            VicePartyId = viceParty,
            PrefeitoMandatos = terms,
        };
        r.UltimaEleicaoAno = year;
        Record(
            date, RenewalEventKind.EleicaoMunicipal, "eleicao_municipal", mayor, partyId: mayorParty,
            extra:
            [
                ("cadeiras", elected.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("reeleitos", reelected.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("novos", (elected.Count - reelected).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("prefeito", NameOf(mayor)),
            ]);
    }

    /// <summary>Aposentadoria (idade e derrotas) ou desistência de quem tem mandato; o anúncio vira notícia uma vez.</summary>
    private bool DecidesNotToRun(string id, Character npc, DateOnly posse, DateOnly date, CivicRole role)
    {
        PersonStatus status = Status(id);
        if (status.NaoDisputa is null)
        {
            if (Rng.Chance(Config.Aposentadoria.Chance(npc.AgeOn(posse), status.Derrotas)))
            {
                status.NaoDisputa = ExitKind.Aposentadoria;
            }
            else if (Rng.Chance(Config.DesistenciaPorEleicao))
            {
                status.NaoDisputa = ExitKind.Desistencia;
            }

            if (status.NaoDisputa is { } kind)
            {
                Record(
                    date, kind == ExitKind.Aposentadoria ? RenewalEventKind.Aposentadoria : RenewalEventKind.Desistencia,
                    kind == ExitKind.Aposentadoria ? "aposentadoria" : "desistencia", id, role);
            }
        }

        return status.NaoDisputa is not null;
    }

    /// <summary>
    /// Disputa da Prefeitura na eleição automática: o prefeito em primeiro mandato que disputa se reelege com a chance
    /// do arquivo; senão vence um dos desafiantes (o vice e um nome de cada uma das três maiores forças).
    /// </summary>
    private (string Mayor, string Party, string Vice, string ViceParty, int Terms) MayorRace(DateOnly date, int year, DateOnly posse)
    {
        RenewalState r = Renewal;
        int minAge = Context.Candidacy.MinimumAgeFor(Office.Prefeito);
        if (r.PrefeitoId is { } incumbent && incumbent != RaceSetup.PlayerId && IsActive(incumbent) && Npc(incumbent) is { } npc && r.PrefeitoPartyId is { } party)
        {
            if (r.PrefeitoMandatos >= 2)
            {
                // Segundo mandato seguido: não pode disputar de novo (a mesma regra da política local, D-007).
                Status(incumbent).NaoDisputa ??= ExitKind.FimDeMandato;
            }

            bool runs = r.PrefeitoMandatos < 2 && !DecidesNotToRun(incumbent, npc, posse, date, CivicRole.Prefeito);
            if (runs && Rng.Chance(Config.Eleicao.ChanceReeleicaoPrefeito))
            {
                bool keepVice = r.ViceId is { } v && v != RaceSetup.PlayerId && IsActive(v);
                string viceParty = keepVice ? r.VicePartyId ?? PickParty(party) : PickParty(party);
                return (incumbent, party, keepVice ? r.ViceId! : Newcomer(viceParty, date, year, minAge), viceParty, 2);
            }

            if (runs)
            {
                Status(incumbent).Derrotas++;
            }
        }

        var challengers = new List<(string? Id, string Party, double Weight)>();
        if (r.ViceId is { } sitting && sitting != RaceSetup.PlayerId && IsActive(sitting) && r.VicePartyId is { } sittingParty && r.ViceId != r.PrefeitoId)
        {
            challengers.Add((sitting, sittingParty, Config.Eleicao.PesoIncumbente * LogNormal(Config.Eleicao.DispersaoCandidatos)));
        }

        var seatsByParty = r.Cadeiras.GroupBy(c => c.PartyId).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        foreach (string challenger in State.Parties.Select(p => p.Id).Where(p => challengers.All(c => c.Party != p))
            .OrderByDescending(p => seatsByParty.GetValueOrDefault(p)).ThenBy(p => p, StringComparer.Ordinal).Take(3))
        {
            challengers.Add((null, challenger, (seatsByParty.GetValueOrDefault(challenger) + Config.Eleicao.BasePartido) * LogNormal(Config.Eleicao.DispersaoCandidatos)));
        }

        var winner = challengers.MaxBy(c => c.Weight);
        string mayor = winner.Id ?? Newcomer(winner.Party, date, year, minAge);
        string otherParty = PickParty(winner.Party);
        return (mayor, winner.Party, Newcomer(otherParty, date, year, minAge), otherParty, 1);
    }

    // ---------------------------------------------------------------- posse

    /// <summary>
    /// Posse de 1º de janeiro: os eleitos ocupam as cadeiras (quem morreu ou saiu entre a eleição e a posse dá lugar ao
    /// suplente), os suplentes ficam na lista, e quem não voltou sai da política local. Sem resultado guardado, vale a
    /// eleição jogada gravada na política local ou, em último caso, uma eleição automática feita na hora.
    /// </summary>
    private void Inaugurate(DateOnly date)
    {
        RenewalState r = Renewal;
        int year = date.Year - 1;
        if (r.Eleitos is null || r.Eleitos.AnoEleicao != year)
        {
            if (State.Local is { } local && local.Municipio == Municipio.CodigoIbge && local.ElectionYear == year)
            {
                r.Eleitos = SlateFromLocal(local, null, date);
            }
            else
            {
                RunAutomaticElection(date, year);
            }
        }

        ElectedSlate slate = r.Eleitos!;
        var before = r.Titulares.ToHashSet(StringComparer.Ordinal);
        var previous = Tracked().Where(t => t.Role is CivicRole.Vereador or CivicRole.Prefeito or CivicRole.VicePrefeito or CivicRole.Suplente).Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
        r.Suplentes = slate.Suplentes.ToDictionary(s => s.Key, s => s.Value.Where(IsActive).ToList(), StringComparer.Ordinal);
        r.Cadeiras.Clear();
        r.EleicoesPendentes.Clear();
        r.MandatoInicio = date;
        r.MandatoFim = new DateOnly(date.Year + 3, 12, 31);
        r.Legislatura++;
        r.UltimaEleicaoAno = year;
        r.ExecutivoVagoDesde = null;

        int number = 1;
        var debuts = new List<string>();
        var replaced = new List<(string Elected, string? Holder, Seat Seat)>();
        foreach (ElectedSeat elected in slate.Vereadores)
        {
            var seat = new Seat { Numero = number++, PartyId = elected.PartyId };
            r.Cadeiras.Add(seat);
            string? holder = IsActive(elected.PessoaId) ? elected.PessoaId : NextSubstitute(elected.PartyId);
            if (holder is null)
            {
                // Eleito morto antes da posse e sem suplente: a cadeira começa vaga, pela mesma regra.
                seat.VagaDesde = date;
                seat.VagaDestino = Rules.Council(false, r.MandatoFim, date);
                seat.VagaPrazo = seat.VagaDestino == CouncilSuccession.NovaEleicao ? date.AddDays(Config.Prazos.EleicaoCadeiraDias) : r.MandatoFim;
                if (seat.VagaDestino == CouncilSuccession.NovaEleicao)
                {
                    r.EleicoesPendentes.Add(new PendingElection
                    {
                        Id = r.NextEventId++, Cargo = CivicRole.Vereador, Cadeira = seat.Numero, Indireta = false, Data = seat.VagaPrazo.Value, ConvocadaEm = date, Causa = VacancyCause.Morte, Regra = Rules.BaseVereador.Artigo,
                    });
                }

                replaced.Add((elected.PessoaId, null, seat));
                continue;
            }

            if (holder != elected.PessoaId)
            {
                r.Suplentes[elected.PartyId].Remove(holder);
                replaced.Add((elected.PessoaId, holder, seat));
            }

            Seat(seat, holder, date, holder == elected.PessoaId ? elected.Votos : 0);
            if (holder != RaceSetup.PlayerId && Status(holder).Mandatos == 1)
            {
                debuts.Add(holder);
            }
        }

        // Prefeitura: quem foi eleito e segue vivo; se o prefeito eleito morreu antes da posse, assume o vice eleito.
        string? mayor = slate.PrefeitoId is { } m && IsActive(m) ? m : null;
        string? vice = slate.ViceId is { } v && IsActive(v) ? v : null;
        string? mayorParty = slate.PrefeitoPartyId;
        if (mayor is null && vice is not null)
        {
            (mayor, mayorParty, vice) = (vice, slate.VicePartyId, null);
        }

        r.PrefeitoId = mayor;
        r.PrefeitoPartyId = mayor is null ? null : mayorParty;
        r.PrefeitoMandatos = mayor == slate.PrefeitoId ? slate.PrefeitoMandatos : 1;
        r.ViceId = vice;
        r.VicePartyId = vice is null ? null : slate.VicePartyId;
        if (mayor is not null && mayor != RaceSetup.PlayerId)
        {
            Status(mayor).Mandatos++;
            EnsurePerson(mayor, null);
        }

        // Quem tinha cargo ou suplência e não voltou sai da política local: aposentado, desistente ou derrotado.
        var now = Tracked().Where(t => t.Role is CivicRole.Vereador or CivicRole.Prefeito or CivicRole.VicePrefeito or CivicRole.Suplente).Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
        foreach (string id in previous.Where(id => !now.Contains(id) && IsActive(id)))
        {
            PersonStatus status = Status(id);
            status.Saida = status.NaoDisputa ?? ExitKind.Derrota;
            status.SaidaEm = date;
        }

        foreach (string id in now)
        {
            Status(id).NaoDisputa = null;
        }

        r.Eleitos = null;
        _drafted.Clear();

        // Quem foi eleito e não chegou à posse sai também do mandato do jogador e da política local.
        foreach (var (electedId, holder, seat) in replaced)
        {
            BridgeSeat(electedId, holder, seat);
        }

        ChoosePresident();
        r.Legislaturas.Add(Snapshot(date, r.Titulares.Count(before.Contains), !slate.Jogada));
        if (mayor is null)
        {
            // Nem prefeito nem vice eleitos chegaram à posse: dupla vacância no primeiro dia do mandato.
            r.ExecutivoVagoDesde = date;
            ExecutiveSuccession destiny = Rules.Executive(VacancyCause.Morte, false, r.MandatoInicio, r.MandatoFim, date);
            r.EleicoesPendentes.Add(new PendingElection
            {
                Id = r.NextEventId++, Cargo = CivicRole.Prefeito, Indireta = destiny == ExecutiveSuccession.EleicaoIndireta, Data = Rules.ElectionDate(destiny, date), ConvocadaEm = date,
                Causa = VacancyCause.Morte, Regra = Rules.BaseDuplaVacancia.Artigo,
            });
        }

        Record(
            date, RenewalEventKind.PosseLegislatura, "posse_legislatura", mayor ?? r.PresidenteCamaraId, partyId: mayorParty,
            extra: [("cadeiras", r.Cadeiras.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("prefeito", NameOf(mayor ?? r.PresidenteCamaraId))]);
        foreach (string id in debuts)
        {
            Record(date, RenewalEventKind.Estreia, "estreia", id, CivicRole.Vereador, seat: r.SeatOf(id)?.Numero);
        }

        if (!slate.Jogada)
        {
            // Eleição automática: a política local passa a ser a da renovação, para a próxima campanha partir dela.
            LocalPolitics? old = State.Local?.Municipio == Municipio.CodigoIbge ? State.Local : null;
            State.Local = new LocalPolitics
            {
                Municipio = Municipio.CodigoIbge,
                ElectionYear = year,
                ChamberSeats = r.Cadeiras.GroupBy(c => c.PartyId).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal),
                Councillors = r.Titulares.ToList(),
                CouncillorVotes = r.Cadeiras.Where(c => c.TitularId is not null).ToDictionary(c => c.TitularId!, c => c.Votos, StringComparer.Ordinal),
                MayorId = r.PrefeitoId,
                MayorPartyId = r.PrefeitoPartyId,
                MayorReelected = r.PrefeitoMandatos >= 2,
                PlayerImage = old?.PlayerImage ?? 0,
            };
            foreach (var (group, value) in old?.PlayerSupport ?? [])
            {
                State.Local.PlayerSupport[group] = value;
            }
        }

        BridgeMayor();
        if (State.Indicators?.Municipio == Municipio.CodigoIbge && State.Executive is null)
        {
            Indicators.IndicatorEvolution.MarkMandateStart(State.Indicators, date);
        }
    }

    /// <summary>Retrato da legislatura no dia da posse.</summary>
    private LegislatureRecord Snapshot(DateOnly date, int reelected, bool automatic)
    {
        RenewalState r = Renewal;
        var holders = r.Titulares.Select(Npc).Where(c => c is not null).Select(c => c!).ToList();
        int seats = r.Cadeiras.Count;
        return new LegislatureRecord
        {
            Numero = r.Legislatura,
            Inicio = date,
            Cadeiras = seats,
            IdadeMedia = holders.Count == 0 ? 0 : holders.Average(c => (date.DayNumber - c.BirthDate.DayNumber) / 365.25),
            Reeleitos = reelected,
            Estreantes = r.Titulares.Count(id => id != RaceSetup.PlayerId && Status(id).Mandatos <= 1),
            RenovacaoPct = seats == 0 ? 0 : 100.0 * (seats - reelected) / seats,
            Automatica = automatic,
        };
    }
}
