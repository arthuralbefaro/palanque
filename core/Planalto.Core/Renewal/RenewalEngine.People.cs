using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Legislature;
using Planalto.Core.People;
using Planalto.Core.State;
using Calendar = Planalto.Core.State.Calendar;

namespace Planalto.Core.Renewal;

/// <summary>Gente: o mundo de partida, quem entra (GDD 17.16, "Entradas"), o luto e a renovação de lideranças e equipe.</summary>
public sealed partial class RenewalEngine
{
    // ---------------------------------------------------------------- mundo de partida

    /// <summary>
    /// Estado de partida. Com eleição já jogada no município, a Câmara e a Prefeitura são as da política local; sem
    /// ela, são geradas para o mandato em curso (cadeiras por partido do contexto ou sorteadas), com vice e suplentes.
    /// </summary>
    private RenewalState Create()
    {
        DateOnly today = State.Calendar.WeekStart;
        LocalPolitics? local = State.Local?.Municipio == Municipio.CodigoIbge ? State.Local : null;
        int electionYear = local?.ElectionYear ?? LastMunicipalElectionBefore(today);
        var start = new DateOnly(electionYear + 1, 1, 1);
        ulong seed = State.World?.Seed ?? State.Rng.Seed;
        var r = new RenewalState
        {
            Municipio = Municipio.CodigoIbge,
            Uf = Municipio.Uf,
            Rng = new Rng(unchecked((seed * 0xD1B54A32D192ED03UL) ^ (ulong)Municipio.CodigoIbge ^ 0x5EED0FC1DADEUL)),
            UltimoPasso = today,
            UltimoMes = (today.Year * 12) + today.Month,
            MandatoInicio = start,
            MandatoFim = new DateOnly(electionYear + 4, 12, 31),
            UltimaEleicaoAno = electionYear,
        };
        State.Renewal = r;

        if (local is not null)
        {
            int number = 1;
            foreach (string id in local.Councillors)
            {
                string party = (id == RaceSetup.PlayerId ? State.Player.PartyId : Npc(id)?.PartyId) ?? State.Parties[0].Id;
                var seat = new Seat { Numero = number++, PartyId = party };
                r.Cadeiras.Add(seat);
                Seat(seat, id, start, local.CouncillorVotes.GetValueOrDefault(id));
            }

            r.PrefeitoId = local.MayorId;
            r.PrefeitoPartyId = local.MayorPartyId;
            r.PrefeitoMandatos = local.MayorReelected ? 2 : 1;
        }
        else
        {
            Dictionary<string, int> seats = InitialSeats(electionYear);
            int number = 1;
            foreach (var (party, count) in seats.OrderBy(s => s.Key, StringComparer.Ordinal))
            {
                for (int i = 0; i < count; i++)
                {
                    var seat = new Seat { Numero = number++, PartyId = party };
                    r.Cadeiras.Add(seat);
                    Seat(seat, Veteran(party, start), start, 0);
                }
            }

            r.PrefeitoPartyId = Context.InitialMayorParty is { } p && State.Party(p) is not null ? p
                : seats.OrderByDescending(s => s.Value).ThenBy(s => s.Key, StringComparer.Ordinal).First().Key;
            r.PrefeitoId = Veteran(r.PrefeitoPartyId, start, Context.Candidacy.MinimumAgeFor(Elections.Office.Prefeito));
        }

        if (r.PrefeitoId is not null && r.PrefeitoId != RaceSetup.PlayerId)
        {
            Status(r.PrefeitoId).Mandatos = Math.Max(1, Status(r.PrefeitoId).Mandatos);
        }

        r.VicePartyId = PickParty(r.PrefeitoPartyId);
        r.ViceId = Newcomer(r.VicePartyId, start, electionYear, minAge: Context.Candidacy.MinimumAgeFor(Elections.Office.Prefeito));
        Status(r.ViceId);
        foreach (var group in r.Cadeiras.GroupBy(c => c.PartyId).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            r.Suplentes[group.Key] = Enumerable.Range(0, Config.Suplentes.For(group.Count())).Select(_ => Newcomer(group.Key, start, electionYear)).ToList();
            foreach (string id in r.Suplentes[group.Key])
            {
                Status(id);
            }
        }

        ChoosePresident();
        r.Legislaturas.Add(Snapshot(start, reelected: 0, automatic: local is null));
        return r;
    }

    private static int LastMunicipalElectionBefore(DateOnly today)
    {
        for (int year = today.Year - 1; ; year--)
        {
            if (Calendar.ElectionKindOf(year) == ElectionKind.Municipal)
            {
                return year;
            }
        }
    }

    /// <summary>Cadeiras por partido da Câmara de partida: as do contexto ou, sem elas, sorteadas pelo tamanho dos partidos.</summary>
    private Dictionary<string, int> InitialSeats(int electionYear)
    {
        if (Context.InitialSeats is { } given)
        {
            var known = given.Where(s => s.Value > 0 && State.Party(s.Key) is not null).ToDictionary(s => s.Key, s => s.Value, StringComparer.Ordinal);
            if (known.Count > 0)
            {
                return known;
            }
        }

        int total = Context.Projection?.Seats(Municipio, electionYear) ?? Municipio.VagasVereador;
        var weights = State.Parties.OrderBy(p => p.Id, StringComparer.Ordinal)
            .ToDictionary(p => p.Id, p => ((int)p.Size + 1) * LogNormal(Config.Eleicao.DispersaoPartidos), StringComparer.Ordinal);
        return LargestAverages(weights, Math.Max(1, total));
    }

    /// <summary>Reparte cadeiras pelas maiores médias (só para o mundo de partida; as eleições usam a apuração do Core).</summary>
    private static Dictionary<string, int> LargestAverages(Dictionary<string, double> weights, int seats)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < seats; i++)
        {
            string best = weights.OrderByDescending(w => w.Value / (result.GetValueOrDefault(w.Key) + 1)).ThenBy(w => w.Key, StringComparer.Ordinal).First().Key;
            result[best] = result.GetValueOrDefault(best) + 1;
        }

        return result;
    }

    /// <summary>Quem já está no mandato quando o mundo começa: idade de entrada mais os mandatos que já cumpriu.</summary>
    private string Veteran(string partyId, DateOnly posse, int minAge = 18)
    {
        int terms = 0;
        while (terms < Config.MundoInicial.MandatosAnterioresMax && Rng.Chance(Config.MundoInicial.ChanceMandatoAnterior))
        {
            terms++;
        }

        int age = Math.Max(minAge, Rng.NextInt(Config.IdadeEntrada.Min, Config.IdadeEntrada.Max + 1)) + (4 * terms);
        string id = Generate(partyId, posse, Math.Min(age, Config.Aposentadoria.IdadeMaxima - 5), null);
        Status(id, EntryOrigin.Gerado).Mandatos = terms;
        return id;
    }

    // ---------------------------------------------------------------- entradas

    /// <summary>
    /// Um nome novo para o partido, pela origem sorteada em data/demography/entries.json: liderança local que vira
    /// candidata, parente de quem saiu, alguém da equipe disponível, comunicador ou gerado. Sem ninguém disponível na
    /// origem sorteada, vale o gerado. A origem não escolhe o partido.
    /// </summary>
    private string Newcomer(string partyId, DateOnly date, int electionYear, int minAge = 18, int? age = null)
    {
        RenewalState r = Renewal;
        EntryOrigin origin = Rng.PickWeighted(Context.Content.Entries.Origens, o => o.Peso);
        int max = Config.Aposentadoria.Idade;
        bool Free(string id) => IsActive(id) && r.SeatOf(id) is null && r.PrefeitoId != id && r.ViceId != id && !r.Suplentes.Values.Any(l => l.Contains(id))
            && (r.Eleitos is null || (r.Eleitos.Vereadores.All(v => v.PessoaId != id) && !r.Eleitos.Suplentes.Values.Any(l => l.Contains(id)) && r.Eleitos.PrefeitoId != id && r.Eleitos.ViceId != id))
            && Npc(id) is { } npc && npc.AgeOn(date) >= minAge && npc.AgeOn(date) < max;

        switch (origin.Id)
        {
            case EntryOrigin.Lideranca when City is { } city:
                List<Leader> leaders = city.Leaders.Where(l => Free(l.PersonId) && !_drafted.Contains(l.PersonId)).ToList();
                if (leaders.Count > 0)
                {
                    Leader leader = Rng.PickWeighted(leaders, l => l.Influence);
                    Npc(leader.PersonId)!.PartyId = partyId;
                    Status(leader.PersonId).Origem ??= origin.Id;
                    _drafted.Add(leader.PersonId);
                    return leader.PersonId;
                }

                break;
            case EntryOrigin.Equipe when City is { } city && State.Campaign is null:
                List<StaffMember> staff = city.Staff.Where(s => !s.Left && !s.Hired && Free(s.PersonId) && !_drafted.Contains(s.PersonId)).ToList();
                if (staff.Count > 0)
                {
                    StaffMember member = Rng.Pick(staff);
                    Npc(member.PersonId)!.PartyId = partyId;
                    Status(member.PersonId).Origem ??= origin.Id;
                    _drafted.Add(member.PersonId);
                    StaffLeaves(member.PersonId, null, date);
                    return member.PersonId;
                }

                break;
            case EntryOrigin.Familia:
                List<PersonStatus> gone = r.Pessoas
                    .Where(p => p.Saida is ExitKind.Morte or ExitKind.Aposentadoria && p.Mandatos > 0 && Npc(p.Id) is not null).TakeLast(12).ToList();
                if (gone.Count > 0)
                {
                    PersonStatus relative = Rng.Pick(gone);
                    string heir = Generate(partyId, date, age ?? EntryAge(minAge), null, Surname(Npc(relative.Id)!));
                    Status(heir, origin.Id, relative.Id);
                    return heir;
                }

                break;
            case EntryOrigin.Midia:
                string known = Generate(partyId, date, age ?? EntryAge(minAge), origin.Profissao);
                Status(known, origin.Id);
                return known;
        }

        string id = Generate(partyId, date, age ?? EntryAge(minAge), null);
        Status(id, EntryOrigin.Gerado);
        return id;
    }

    /// <summary>Quem já foi chamado nesta eleição, para a mesma pessoa não entrar em duas listas.</summary>
    private readonly HashSet<string> _drafted = new(StringComparer.Ordinal);

    private int EntryAge(int minAge) => Math.Max(minAge, Rng.NextInt(Config.IdadeEntrada.Min, Config.IdadeEntrada.Max + 1));

    private static string Surname(Character c) => c.CivilName.Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1];

    /// <summary>Gera o personagem (sempre fictício) com o gerador de pessoas do Core e o põe no estado.</summary>
    private string Generate(string? partyId, DateOnly date, int age, string? professionId, string? surname = null)
    {
        ProfessionDefinition? profession = professionId is null ? null : Context.People.Professions.Professions.FirstOrDefault(p => p.Id == professionId && p.AvailableAt(age));
        Character npc = PersonGenerator.Generate(Rng, Context.People, new PersonRequest(
            NewNpcId(), Municipio.Uf, Municipio.CodigoIbge, date, Age: age, ProfessionId: profession?.Id, PartyId: partyId));
        if (surname is not null)
        {
            string first = npc.CivilName.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            npc.CivilName = $"{first} {surname}";
            npc.BallotName = npc.CivilName;
        }

        State.Npcs.Add(npc);
        return npc.Id;
    }

    /// <summary>Ficha de pessoa (memória, interesses) para quem passa a ter cargo, se a cidade já tem gente.</summary>
    private void EnsurePerson(string id, PersonRole? role)
    {
        if (City is not { } city || id == RaceSetup.PlayerId)
        {
            return;
        }

        Person? person = city.Person(id);
        if (person is null)
        {
            var interests = new List<Interest>();
            if (Context.Themes.Count > 0)
            {
                interests.Add(new Interest { Kind = Interest.Tema, Target = Rng.Pick(Context.Themes) });
            }

            person = new Person { Id = id, Roles = [], Interests = interests, Temperament = (Temperament)Rng.NextInt(5), Affinity = Rng.NextInt(-10, 11) };
            city.People.Add(person);
        }

        if (role is { } r && !person.Roles.Contains(r))
        {
            person.Roles.Add(r);
        }
    }

    // ---------------------------------------------------------------- luto

    /// <summary>
    /// Luto (GDD 17.16): quem tinha vínculo com quem morreu guarda a lembrança. Vínculo é ser do mesmo partido com
    /// cargo ou suplência, disputar o mesmo público como liderança, ou ser da equipe ligada à pessoa.
    /// </summary>
    private void Mourn(string deadId, DateOnly date, RenewalEvent death)
    {
        if (City is not { } city)
        {
            return;
        }

        RenewalState r = Renewal;
        string? party = Npc(deadId)?.PartyId;
        var linked = new HashSet<string>(StringComparer.Ordinal);
        if (party is not null)
        {
            IEnumerable<string> officials = r.Titulares.Concat(r.Suplentes.Values.SelectMany(l => l));
            officials = r.PrefeitoId is null ? officials : officials.Append(r.PrefeitoId);
            officials = r.ViceId is null ? officials : officials.Append(r.ViceId);
            linked.UnionWith(officials.Where(id => Npc(id)?.PartyId == party));
        }

        linked.UnionWith(city.Leaders.Where(l => l.Rivals.Contains(deadId)).Select(l => l.PersonId));
        if (city.Leader(deadId) is { } dead)
        {
            linked.UnionWith(dead.Rivals);
        }

        linked.UnionWith(city.Staff.Where(s => s.BondPersonId == deadId && !s.Left).Select(s => s.PersonId));
        linked.Remove(deadId);
        linked.Remove(RaceSetup.PlayerId);
        string text = Texts.Fill(Config.Texto("lembranca_luto"), ("nome", NameOf(deadId)));
        foreach (string id in linked.Order(StringComparer.Ordinal))
        {
            if (city.Person(id) is { } person && IsActive(id))
            {
                person.Remember(date, "luto", Config.Luto.PesoLembranca, 0, text, Context.Content.Memory);
                death.Enlutados.Add(id);
            }
        }
    }

    // ---------------------------------------------------------------- lideranças

    /// <summary>A liderança sai (morte, aposentadoria ou mudança): o lugar dela fica à espera de sucessor.</summary>
    private void LeaderLeaves(string personId, ExitKind kind, DateOnly date)
    {
        if (City is not { } city || city.Leader(personId) is not { } leader)
        {
            return;
        }

        city.Leaders.Remove(leader);
        foreach (Leader other in city.Leaders)
        {
            other.Rivals.Remove(personId);
        }

        Renewal.LiderancasVagas.Add(new LeaderVacancy
        {
            Desde = date, AntecessorId = personId, Type = leader.Type, Zones = [.. leader.Zones], Publico = leader.Publico, Theme = leader.Theme,
            RequestId = leader.RequestId, RequestText = leader.RequestText, Price = leader.Price, Influence = leader.Influence,
        });
        if (kind == ExitKind.Morte)
        {
            return;
        }

        // Quem tem cadeira, suplência ou cargo segue na política: só deixa a liderança.
        bool official = Renewal.SeatOf(personId) is not null || Renewal.PrefeitoId == personId || Renewal.ViceId == personId || Renewal.Suplentes.Values.Any(l => l.Contains(personId));
        if (!official)
        {
            PersonStatus status = Status(personId);
            status.Saida = kind;
            status.SaidaEm = date;
        }

        Record(
            date, kind == ExitKind.Aposentadoria ? RenewalEventKind.Aposentadoria : RenewalEventKind.Mudanca,
            kind == ExitKind.Aposentadoria ? "aposentadoria_lideranca" : "mudanca_lideranca", personId, CivicRole.Lideranca);
    }

    /// <summary>Com o tempo, alguém assume o lugar de cada liderança que saiu: mesmo tipo, zona e público, pessoa nova.</summary>
    private void FillLeaderVacancies(DateOnly date)
    {
        if (City is not { } city)
        {
            return;
        }

        foreach (LeaderVacancy vacancy in Renewal.LiderancasVagas.ToList())
        {
            if (!Rng.Chance(Config.Lideranca.ChanceMensalSucessor))
            {
                continue;
            }

            Renewal.LiderancasVagas.Remove(vacancy);
            string id = Generate(null, date, Rng.NextInt(Config.Lideranca.IdadeSucessor.Min, Config.Lideranca.IdadeSucessor.Max + 1), null);
            Status(id);
            city.People.Add(new Person
            {
                Id = id,
                Roles = [PersonRole.Lideranca],
                Interests = [new Interest { Kind = Interest.Tema, Target = vacancy.Theme }, new Interest { Kind = Interest.ZonaKind, Target = vacancy.Zones[0] }],
                Temperament = (Temperament)Rng.NextInt(5),
                Affinity = Rng.NextInt(-10, 11),
            });

            // Quem chega começa com menos influência do que quem saiu tinha, e ninguém a conhece ainda.
            var leader = new Leader
            {
                PersonId = id,
                Type = vacancy.Type,
                Zones = [.. vacancy.Zones],
                Publico = vacancy.Publico,
                Influence = Math.Max(1, vacancy.Influence - Rng.NextInt(0, Config.Lideranca.PerdaDeInfluenciaMax + 1)),
                Theme = vacancy.Theme,
                RequestId = vacancy.RequestId,
                RequestText = vacancy.RequestText,
                Price = vacancy.Price,
            };
            foreach (Leader other in city.Leaders.Where(o => o.Publico == leader.Publico && o.Zones.Intersect(leader.Zones).Any()))
            {
                other.Rivals.Add(id);
                leader.Rivals.Add(other.PersonId);
            }

            city.Leaders.Add(leader);
            Record(date, RenewalEventKind.NovaLideranca, "nova_lideranca", vacancy.AntecessorId, CivicRole.Lideranca, successorId: id);
        }
    }

    // ---------------------------------------------------------------- equipe

    /// <summary>Alguém da equipe disponível na cidade sai (morte, mudança ou porque virou candidato: <paramref name="kind"/> null).</summary>
    private void StaffLeaves(string personId, ExitKind? kind, DateOnly date)
    {
        if (City is not { } city || city.Staff.FirstOrDefault(s => s.PersonId == personId && !s.Left) is not { } member)
        {
            return;
        }

        member.Left = true;
        member.Hired = false;
        State.Campaign?.Team.Pending.RemoveAll(d => d.PersonId == personId || d.OtherPersonId == personId);
        Renewal.EquipeVagas.Add(new StaffVacancy { Desde = date, Role = member.Role, Salary = member.Salary });
        if (kind is null or ExitKind.Morte)
        {
            return;
        }

        PersonStatus status = Status(personId);
        status.Saida = kind;
        status.SaidaEm = date;
        Record(date, RenewalEventKind.Mudanca, "mudanca_equipe", personId, CivicRole.Equipe);
    }

    /// <summary>Gente nova para as vagas que a equipe deixou; precisa do catálogo da equipe no contexto.</summary>
    private void FillStaffVacancies(DateOnly date)
    {
        if (City is not { } city || Context.Staff is not { } catalog || catalog.Vinculos.FirstOrDefault(v => v.Id == StaffBondType.Nenhum) is not { } bond)
        {
            return;
        }

        foreach (StaffVacancy vacancy in Renewal.EquipeVagas.ToList())
        {
            if (!Rng.Chance(Config.Equipe.ChanceMensalNovato))
            {
                continue;
            }

            Renewal.EquipeVagas.Remove(vacancy);
            string id = Generate(null, date, Rng.NextInt(Config.Equipe.IdadeNovato.Min, Config.Equipe.IdadeNovato.Max + 1), null);
            Status(id);
            var interests = new List<Interest>();
            if (Context.Themes.Count > 0)
            {
                interests.Add(new Interest { Kind = Interest.Tema, Target = Rng.Pick(Context.Themes) });
            }

            city.People.Add(new Person { Id = id, Roles = [PersonRole.Equipe], Interests = interests, Temperament = (Temperament)Rng.NextInt(5), Affinity = Rng.NextInt(-5, 11) });
            int quality = Rng.NextInt(1, 6);
            int shownMin = Math.Clamp(quality - 1, 1, 4);
            city.Staff.Add(new StaffMember
            {
                PersonId = id,
                Role = vacancy.Role,
                Quality = quality,
                QualityShownMin = shownMin,
                QualityShownMax = shownMin + 1,
                Salary = vacancy.Salary,
                Loyalty = Rng.NextInt(bond.Lealdade.Min, bond.Lealdade.Max + 1),
                BondKind = bond.Id,
                BondText = bond.Texto,
            });
            Record(date, RenewalEventKind.NovoNaEquipe, "novo_equipe", id, CivicRole.Equipe);
        }
    }
}
