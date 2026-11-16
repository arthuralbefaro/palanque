using Planalto.Core.Characters;
using Planalto.Core.People;
using Planalto.Core.State;
using Planalto.Core.Territory;

namespace Planalto.Core.Campaign;

/// <summary>Gera a cidade com gente quando o município entra em foco (GDD 17.10): lideranças nomeadas, com zona, público, influência, interesse e pedido.</summary>
public static class CityBuilder
{
    public static void Ensure(CampaignContext ctx, CampaignState campaign, Rng rng)
    {
        GameState state = ctx.State;
        if (state.City?.Municipio == ctx.Municipio.CodigoIbge)
        {
            // Cidade já gerada (eleição seguinte): os redutos novos mexem só no favor de partida.
            SeedFavor(ctx, campaign, state.City, rng);
            return;
        }

        LeaderCatalog catalog = ctx.Campaign.Leaders;
        List<Zona> zonas = ctx.Zonas.Zonas;
        IntRange range = catalog.Regras.QuantidadePorPorte[ctx.Municipio.Porte];
        int count = rng.NextInt(range.Min, range.Max + 1);
        var city = new CityState { Municipio = ctx.Municipio.CodigoIbge };
        int serial = state.Npcs.Count;

        // Fatia de cada religião entre os eleitores, para as lideranças religiosas seguirem a composição do Censo.
        double Voters(string group) => ctx.Electorate.Segments.Where(s => s.Has(group)).Sum(s => s.Voters);

        for (int i = 0; i < count; i++)
        {
            // Tipos que só atuam numa situação que a cidade não tem (rural sem zona rural) ficam de fora.
            List<LeaderType> types = catalog.Tipos.Where(t => t.Situacao == "qualquer" || zonas.Any(z => z.Situacao == t.Situacao)).ToList();
            LeaderType type = rng.PickWeighted(types, t => t.Weight);
            List<Zona> eligible = zonas.Where(z => type.Situacao == "qualquer" || z.Situacao == type.Situacao).ToList();
            Zona zone = rng.PickWeighted(eligible, z => Math.Max(1, z.Populacao));
            var zoneIds = new List<string> { zone.Id };
            if (zone.Vizinhas.Count > 0 && rng.Chance(0.2))
            {
                zoneIds.Add(rng.Pick(zone.Vizinhas));
            }

            List<string> publicos = type.Publicos.Keys.Where(g => Voters(g) > 0).ToList();
            if (publicos.Count == 0)
            {
                publicos = type.Publicos.Keys.ToList();
            }

            string publico = type.Id == LeaderType.Religiosa
                ? rng.PickWeighted(publicos, g => Math.Max(1e-9, Voters(g)))
                : rng.PickWeighted(publicos, g => type.Publicos[g]);
            string theme = rng.PickWeighted(type.Interesses.Keys.ToList(), t => type.Interesses[t]);
            LeaderRequestDefinition request = catalog.Request(rng.PickWeighted(type.Pedidos, p => catalog.Request(p).Weight));
            int influence = rng.PickWeighted([1, 2, 3, 4, 5], v => v is 1 or 5 ? 1.0 : v == 3 ? 3.0 : 2.0);

            Character npc = PersonGenerator.Generate(rng, ctx.People, new PersonRequest(
                $"npc_{ctx.Municipio.CodigoIbge}_{serial++}", ctx.Municipio.Uf, ctx.Municipio.CodigoIbge, state.Calendar.WeekStart, Age: rng.NextInt(32, 72)));
            state.Npcs.Add(npc);
            city.People.Add(new Person
            {
                Id = npc.Id,
                Roles = [PersonRole.Lideranca],
                Interests = [new Interest { Kind = Interest.Tema, Target = theme }, new Interest { Kind = Interest.ZonaKind, Target = zone.Id }],
                Temperament = (Temperament)rng.NextInt(5),
                Affinity = rng.NextInt(-10, 11),
            });
            city.Leaders.Add(new Leader
            {
                PersonId = npc.Id,
                Type = type.Id,
                Zones = zoneIds,
                Publico = publico,
                Influence = influence,
                Theme = theme,
                RequestId = request.Id,
                RequestText = request.Text.Replace("{zona}", zone.Nome, StringComparison.Ordinal).Replace("{tema}", ctx.Themes.Themes.First(t => t.Id == theme).Name, StringComparison.Ordinal),
                Price = 8 + (influence * 5) + rng.NextInt(0, 9),
            });
        }

        // Rivalidades: quem disputa o mesmo público na mesma zona.
        foreach (Leader a in city.Leaders)
        {
            a.Rivals.AddRange(city.Leaders.Where(b => b != a && b.Publico == a.Publico && b.Zones.Intersect(a.Zones).Any()).Select(b => b.PersonId));
        }

        // O jogador já conhece parte delas: primeiro as da zona em que mora.
        string home = campaign.Territory.Zones[campaign.Territory.HomeZone];
        int known = (int)Math.Ceiling(city.Leaders.Count * catalog.Regras.ConhecidasNoInicio);
        foreach (Leader l in city.Leaders.OrderByDescending(l => l.Zones.Contains(home)).ThenByDescending(l => l.Influence).Take(known))
        {
            l.Known = true;
        }

        state.City = city;
        SeedFavor(ctx, campaign, city, rng);
    }

    /// <summary>Favor de partida dos rivais com as lideranças dos seus redutos; quem passa do limiar já começa alinhado.</summary>
    private static void SeedFavor(CampaignContext ctx, CampaignState campaign, CityState city, Rng rng)
    {
        LeaderRules rules = ctx.Campaign.Leaders.Regras;
        foreach (Leader leader in city.Leaders)
        {
            leader.Favor.Clear();
            if (leader.AlignedWith is not null && leader.AlignedWith != RaceSetup.PlayerId)
            {
                leader.AlignedWith = null;
            }

            foreach (var (candidateId, redutos) in campaign.Territory.Redutos)
            {
                if (redutos.Any(z => leader.Zones.Contains(campaign.Territory.Zones[z])))
                {
                    leader.Favor[candidateId] = rng.NextInt(0, 46);
                }
            }

            if (leader.AlignedWith is null && leader.Favor.Count > 0 && leader.Favor.MaxBy(f => f.Value) is { Value: var best } top && best >= rules.LimiarAlinhamento)
            {
                leader.AlignedWith = top.Key;
            }
        }
    }
}

/// <summary>Lideranças na campanha (GDD 17.10): visita, pedido, apoio por zona, rivalidades, deriva sem contato e assédio dos rivais.</summary>
public sealed partial class CampaignEngine
{
    private LeaderCatalog LeaderContent => Context.Campaign.Leaders;

    private LeaderRules LeaderRules => LeaderContent.Regras;

    private PeopleConfig PeopleConfig => Context.Campaign.People;

    public CityState City => State.City ?? throw new InvalidOperationException("Cidade em foco não gerada.");

    public DateOnly Today => Clock.Today(State);

    /// <summary>Relação da pessoa com o jogador hoje.</summary>
    public double RelationOf(string personId) => City.Person(personId)?.Relation(Today, PeopleConfig) ?? 0;

    public string NameOf(string characterId) =>
        characterId == RaceSetup.PlayerId ? State.Player.BallotName
        : Campaign.Find(characterId)?.Name ?? State.Npcs.FirstOrDefault(n => n.Id == characterId)?.BallotName ?? characterId;

    public string ZoneName(string zoneId) => Context.Zonas.Zonas.FirstOrDefault(z => z.Id == zoneId)?.Nome ?? zoneId;

    private string LeaderText(string key, Leader leader, string? rivalId = null, string? other = null) =>
        LeaderContent.Texto(key)
            .Replace("{nome}", other ?? NameOf(leader.PersonId), StringComparison.Ordinal)
            .Replace("{papel}", LeaderContent.Type(leader.Type).Papel, StringComparison.Ordinal)
            .Replace("{zona}", ZoneName(leader.Zones[0]), StringComparison.Ordinal)
            .Replace("{rival}", rivalId is null ? "" : NameOf(rivalId), StringComparison.Ordinal)
            .Replace("{pedido}", leader.RequestText, StringComparison.Ordinal);

    private Memory RememberLeader(Leader leader, string fact, int peso, int pontos, string? text = null, bool contact = true)
    {
        Person person = City.Person(leader.PersonId)!;
        if (contact)
        {
            person.LastContact = Today;
        }

        return person.Remember(Today, fact, peso, pontos, text ?? LeaderText(fact, leader), PeopleConfig);
    }

    /// <summary>
    /// Resposta ao pedido da liderança visitada (decisão imediata). Aceitar grava a promessa no Livro; negociar é um
    /// teste de Articulação que, se der certo, troca a promessa por uma dívida; não se comprometer mantém a porta
    /// aberta; recusar custa relação. Devolve a lembrança gravada.
    /// </summary>
    public Memory AnswerLeader(LeaderAnswer answer)
    {
        string personId = Agenda.PendingRequest ?? throw new InvalidOperationException("Nenhum pedido esperando resposta.");
        Leader leader = City.Leader(personId)!;
        LeaderRequestDefinition request = LeaderContent.Request(leader.RequestId);
        Agenda.PendingRequest = null;
        Memory memory;
        switch (answer)
        {
            case LeaderAnswer.Aceitar:
                if (request.Kind == LeaderRequestDefinition.Dinheiro)
                {
                    // Pagar o evento é gasto irregular: sai do caixa sem registro e vira esqueleto que a liderança conhece.
                    decimal cost = RequestMoneyCost();
                    EffectApplier.Apply(Context, new Effects { Money = -cost });
                    State.Ledger.AddSkeleton("gasto_nao_declarado", LeaderText("esqueleto_dinheiro", leader), LeaderRules.GravidadeDinheiro, Today, [leader.PersonId]);
                }
                else
                {
                    DateOnly deadline = PromiseDeadline(request);
                    leader.PromiseId = State.Ledger.AddPromise(Promise.ParaPessoa, leader.PersonId, request.Id, LeaderText("promessa_livro", leader), leader.Zones[0], Today, deadline).Id;
                }

                leader.RequestMet = true;
                memory = RememberLeader(leader, "promessa", 3, LeaderRules.PontosPromessa);
                break;
            case LeaderAnswer.Negociar:
                double chance = Math.Clamp(State.Player.Attributes.Get(AttributeId.Articulacao) / 100.0, 0.05, 0.95);
                if (Rng.Chance(chance))
                {
                    leader.RequestMet = true;
                    State.Ledger.AddDebt(leader.PersonId, LeaderText("divida_livro", leader), Today);
                    memory = RememberLeader(leader, "negociacao", 2, LeaderRules.PontosNegociacao);
                }
                else
                {
                    memory = RememberLeader(leader, "negociacao_falha", -1, LeaderRules.PontosNegociacaoFalha);
                }

                break;
            case LeaderAnswer.NaoSeComprometer:
                memory = RememberLeader(leader, "visita_sem_compromisso", 1, LeaderRules.PontosVisitaSemCompromisso);
                break;
            default:
                memory = RememberLeader(leader, "recusa", -2, LeaderRules.PontosRecusa);
                break;
        }

        Realign(leader);
        RefreshSupport();
        return memory;
    }

    /// <summary>Chance de a negociação dar certo, para a previsão da escolha.</summary>
    public double NegotiationChance => Math.Clamp(State.Player.Attributes.Get(AttributeId.Articulacao) / 100.0, 0.05, 0.95);

    /// <summary>
    /// Com quem a liderança fica: o jogador, se o pedido foi atendido e a relação chegou ao preço dela, e desde que
    /// nenhum rival tenha favor maior; senão o rival de maior favor acima do limiar; senão, neutra. Mudança vira
    /// fato no feed, e fechar com o jogador esfria as lideranças rivais dela.
    /// </summary>
    private void Realign(Leader leader)
    {
        double relation = RelationOf(leader.PersonId);
        double bestFavor = leader.Favor.Count == 0 ? 0 : leader.Favor.Values.Max();
        string? rival = bestFavor >= LeaderRules.LimiarAlinhamento ? leader.Favor.MaxBy(f => f.Value).Key : null;
        bool player = leader.RequestMet && relation >= leader.Price && relation >= bestFavor;
        string? next = player ? RaceSetup.PlayerId : rival;
        if (next == leader.AlignedWith)
        {
            return;
        }

        string? before = leader.AlignedWith;
        leader.AlignedWith = next;
        if (before is not null && before != RaceSetup.PlayerId && Rivals.Record(before) is { } lost)
        {
            // Perder uma liderança faz o rival rever o plano na segunda seguinte (GDD 17.11).
            lost.PendingTrigger ??= RivalPlan.GatilhoLideranca;
        }

        string zone = leader.Zones[0];
        if (next == RaceSetup.PlayerId)
        {
            leader.Known = true;
            EmitFeed(Today, "lideranca", LeaderText("mudou_para_jogador", leader), zone, leader.PersonId, RaceSetup.PlayerId);
            foreach (Leader other in leader.Rivals.Select(id => City.Leader(id)!).Where(o => o is not null))
            {
                City.Person(other.PersonId)!.Remember(Today, "rivalidade", 2, LeaderRules.PontosRivalidade, LeaderText("rivalidade", other, other: NameOf(leader.PersonId)), PeopleConfig);
            }
        }
        else if (next is not null)
        {
            EmitFeed(Today, before == RaceSetup.PlayerId ? "lideranca_perdida" : "lideranca", LeaderText("mudou_para_rival", leader, next), zone, leader.PersonId, next);
        }
        else
        {
            EmitFeed(Today, before == RaceSetup.PlayerId ? "lideranca_perdida" : "lideranca", LeaderText("ficou_neutra", leader), zone, leader.PersonId);
        }
    }

    /// <summary>
    /// Recompõe o apoio por zona (derivado): liderança alinhada dá ao seu público, nas suas zonas, apoio igual a
    /// influência × relação (ou favor) ÷ 100 × fator; o jogador soma o que juntou em reuniões.
    /// </summary>
    public void RefreshSupport()
    {
        TerritoryState t = Territory;
        t.Support.Clear();
        List<Dictionary<string, double>> For(string candidateId)
        {
            if (!t.Support.TryGetValue(candidateId, out var list))
            {
                t.Support[candidateId] = list = t.Zones.Select(_ => new Dictionary<string, double>(StringComparer.Ordinal)).ToList();
            }

            return list;
        }

        var player = For(RaceSetup.PlayerId);
        for (int z = 0; z < t.MeetingSupport.Count; z++)
        {
            foreach (var (group, value) in t.MeetingSupport[z])
            {
                player[z][group] = value;
            }
        }

        foreach (Leader leader in City.Leaders.Where(l => l.AlignedWith is not null))
        {
            double strength = leader.AlignedWith == RaceSetup.PlayerId ? RelationOf(leader.PersonId) : leader.Favor.GetValueOrDefault(leader.AlignedWith!);
            double support = leader.Influence * Math.Clamp(strength, 0, 100) / 100 * LeaderRules.ApoioPorInfluencia;
            if (support <= 0 || Campaign.Find(leader.AlignedWith!) is null)
            {
                continue;
            }

            foreach (int z in leader.Zones.Select(t.IndexOf).Where(z => z >= 0))
            {
                var zone = For(leader.AlignedWith!)[z];
                zone[leader.Publico] = Math.Min(1, zone.GetValueOrDefault(leader.Publico) + support);
            }
        }
    }

    /// <summary>
    /// Virada da semana para as lideranças: sem contato por muito tempo, a relação com o jogador cai; rivais procuram
    /// as lideranças dos seus redutos (vira fato no feed); o favor antigo esfria; cada uma revê de que lado está; e
    /// as conhecidas e não hostis podem convidar o jogador para algo na semana que entra.
    /// </summary>
    private void LeadersWeek(DateOnly nextWeekStart)
    {
        LeaderRules rules = LeaderRules;
        foreach (Leader leader in City.Leaders)
        {
            Person person = City.Person(leader.PersonId)!;
            DateOnly last = person.LastContact ?? DateOnly.MinValue;
            if (person.Memories.Count > 0 && (Today.DayNumber - last.DayNumber) / 7 >= rules.SemanasSemContato)
            {
                person.Affinity = Math.Max(Person.MinRelation, person.Affinity - 1);
            }

            foreach (string id in leader.Favor.Keys.ToList())
            {
                leader.Favor[id] = Math.Max(0, leader.Favor[id] - rules.FavorDecaimento);
            }
        }

        // A procura dos rivais pelas lideranças acontece em dias, pelo plano de cada um (RivalEngine).
        foreach (Leader leader in City.Leaders)
        {
            Realign(leader);
        }

        // Convites para a semana que entra: dia e turno fixos, que podem bater com outro compromisso.
        foreach (Leader leader in City.Leaders.Where(l => l.Known && RelationOf(l.PersonId) > -20))
        {
            if (Agenda.Invites.Any(i => !i.Answered) || !Rng.Chance(rules.ChanceConvite))
            {
                continue;
            }

            Agenda.Invites.Add(new Invite
            {
                Id = $"cv{Agenda.NextId++}",
                FromPersonId = leader.PersonId,
                Text = LeaderText("convite", leader),
                ZoneId = leader.Zones[0],
                Date = nextWeekStart.AddDays(Rng.NextInt(0, 7)),
                Turno = (Turno)Rng.NextInt(1, 3),
                Publico = leader.Publico,
            });
        }

        RefreshSupport();
    }

    /// <summary>
    /// Promessa de comparecer ao evento da comunidade: fica cumprida quando o jogador vai à zona com a liderança
    /// (convite aceito, ou ela como acompanhante). Fica no Livro como cumprida, e a liderança lembra.
    /// </summary>
    private void FulfilPresence(Leader leader)
    {
        if (leader.PromiseId is null || State.Ledger.Promises.FirstOrDefault(p => p.Id == leader.PromiseId) is not { State: PromiseState.Aberta } promise
            || LeaderContent.Request(promise.Object).Kind != LeaderRequestDefinition.Presenca)
        {
            return;
        }

        promise.State = PromiseState.Cumprida;
        RememberLeader(leader, "promessa_cumprida", 2, LeaderRules.PontosPromessaCumprida);
    }

    /// <summary>Recusa um convite; quem convidou lembra.</summary>
    public void DeclineInvite(string inviteId)
    {
        Invite invite = Agenda.Invites.First(i => i.Id == inviteId);
        if (invite.Answered)
        {
            return;
        }

        invite.Answered = true;
        if (City.Leader(invite.FromPersonId) is { } leader)
        {
            RememberLeader(leader, "convite_recusado", -1, LeaderRules.PontosConviteRecusado, contact: false);
            Realign(leader);
            RefreshSupport();
        }
    }

    /// <summary>Convites cujo dia passou sem resposta contam como recusados.</summary>
    private void ExpireInvites()
    {
        foreach (Invite invite in Agenda.Invites.Where(i => !i.Answered && i.Date < Today).ToList())
        {
            DeclineInvite(invite.Id);
        }

        Agenda.Invites.RemoveAll(i => i.Answered && i.Date < Today);
    }
}
