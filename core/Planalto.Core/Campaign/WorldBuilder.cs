using Planalto.Core.Characters;
using Planalto.Core.Elections;
using Planalto.Core.Media;
using Planalto.Core.People;
using Planalto.Core.State;

namespace Planalto.Core.Campaign;

/// <summary>
/// Gera o que a R3 acrescenta à cidade e à disputa quando o município entra em foco: veículos de mídia com dono e
/// jornalista, doadores, o dirigente do partido do jogador, e a pessoa, o perfil e os pontos fracos de cada rival
/// nomeado. Não sorteia nada do que já existe (carregar um save não muda o estado).
/// </summary>
public static class WorldBuilder
{
    public static void Ensure(CampaignContext ctx, CampaignState campaign, Rng rng)
    {
        if (ctx.State.City is not { } city || city.Municipio != ctx.Municipio.CodigoIbge)
        {
            return;
        }

        Donors(ctx, campaign, city, rng);
        Outlets(ctx, campaign, city, rng);
        Boss(ctx, campaign, city, rng);
        Rivals(ctx, campaign, city, rng);
        if (campaign.Media.Economy.Count == 0)
        {
            Economy.EconomyStart start = ctx.Campaign.Economy;
            campaign.Media.Economy[EconomyFactDefinition.Selic] = start.Selic;
            campaign.Media.Economy[EconomyFactDefinition.Ipca] = start.Ipca12Meses;
            campaign.Media.Economy[EconomyFactDefinition.Cambio] = start.Cambio;
            campaign.Media.Economy[EconomyFactDefinition.Desocupacao] = start.Desocupacao;
        }
    }

    /// <summary>Partido da prefeitura: o do prefeito candidato à reeleição, o da última eleição jogada ou o do cenário.</summary>
    public static string? GovernmentParty(CampaignContext ctx, CampaignState campaign) =>
        campaign.Candidates.FirstOrDefault(c => c.Office == Office.Prefeito && c.Incumbent)?.PartyId
        ?? (ctx.State.Local?.Municipio == ctx.Municipio.CodigoIbge ? ctx.State.Local.MayorPartyId : null)
        ?? ctx.Local?.Prefeito;

    private static Person NewPerson(CampaignContext ctx, CityState city, Rng rng, PersonRole role, List<Interest> interests, int ageMin, int ageMax, string? partyId = null)
    {
        Character npc = PersonGenerator.Generate(rng, ctx.People, new PersonRequest(
            $"npc_{ctx.Municipio.CodigoIbge}_{ctx.State.Npcs.Count}", ctx.Municipio.Uf, ctx.Municipio.CodigoIbge, ctx.State.Calendar.WeekStart, Age: rng.NextInt(ageMin, ageMax + 1), PartyId: partyId));
        ctx.State.Npcs.Add(npc);
        var person = new Person
        {
            Id = npc.Id,
            Roles = [role],
            Interests = interests,
            Temperament = (Temperament)rng.NextInt(5),
            Affinity = rng.NextInt(-10, 11),
        };
        city.People.Add(person);
        return person;
    }

    private static List<string> Themes(CampaignContext ctx) => ctx.Themes.Themes.Select(t => t.Id).ToList();

    private static void Donors(CampaignContext ctx, CampaignState campaign, CityState city, Rng rng)
    {
        FinanceCatalog catalog = ctx.Campaign.Finance;
        if (campaign.Finance.Since is null)
        {
            // Eleição nova: a contabilidade recomeça, e o que cada doador já deu na anterior não conta no teto desta.
            campaign.Finance.Since = Clock.Today(ctx.State);
            foreach (Donor old in city.Donors)
            {
                old.Donated = 0;
                old.OffBooks = 0;
                old.DebtId = null;
            }
        }

        if (city.Donors.Count > 0)
        {
            return;
        }

        IntRange range = catalog.Regras.DoadoresPorPorte[ctx.Municipio.Porte];
        double scale = ctx.Config.PorteCostMultiplier.GetValueOrDefault(ctx.Municipio.Porte, 1);
        List<string> themes = Themes(ctx);
        int count = rng.NextInt(range.Min, range.Max + 1);
        for (int i = 0; i < count; i++)
        {
            DonorRequestDefinition request = rng.PickWeighted(catalog.Pedidos, p => p.Weight);
            string theme = rng.Pick(themes);
            string zone = rng.Pick(campaign.Territory.Zones);
            Person person = NewPerson(ctx, city, rng, PersonRole.Doador,
                [new Interest { Kind = Interest.Tema, Target = theme }, new Interest { Kind = Interest.ZonaKind, Target = zone }], 35, 75);
            string zoneName = ctx.Zonas.Zonas.FirstOrDefault(z => z.Id == zone)?.Nome ?? zone;
            city.Donors.Add(new Donor
            {
                PersonId = person.Id,
                Income = Math.Round((decimal)(rng.NextDouble(catalog.Regras.RendaAnual.Min, catalog.Regras.RendaAnual.Max) * scale) / 100) * 100,
                RequestId = request.Id,
                RequestText = Texts.Fill(request.Texto, ("zona", zoneName), ("tema", ctx.Themes.Themes.First(t => t.Id == theme).Name)),
                ZoneId = zone,
                Theme = theme,
            });
        }
    }

    private static void Outlets(CampaignContext ctx, CampaignState campaign, CityState city, Rng rng)
    {
        if (city.Outlets.Count > 0)
        {
            return;
        }

        MediaContent media = ctx.Campaign.Media;
        OutletCatalog catalog = media.Outlets;
        List<string> mix = rng.Pick(catalog.PorPorte[ctx.Municipio.Porte]);
        List<string> themes = Themes(ctx);
        var usedTerms = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (string typeId in mix)
        {
            OutletType type = catalog.Type(typeId);
            string name = "";
            for (int attempt = 0; attempt < 200 && name.Length == 0; attempt++)
            {
                string term = rng.Pick(catalog.Termos);
                string candidate = $"{rng.Pick(type.Prefixos)} {term}";
                if (!usedTerms.Contains(term) && candidate.Length <= OutletCatalog.MaxName && !media.Blocklist.Blocks(candidate))
                {
                    usedTerms.Add(term);
                    name = candidate;
                }
            }

            if (name.Length == 0)
            {
                throw new InvalidOperationException("Não foi possível gerar um nome de veículo fora da lista de bloqueio.");
            }

            List<string> interest = rng.Shuffled(themes).Take(2).ToList();
            Person journalist = NewPerson(ctx, city, rng, PersonRole.Jornalista, interest.Select(t => new Interest { Kind = Interest.Tema, Target = t }).ToList(), 24, 60);

            // O dono pode ser uma liderança ou um doador da cidade (GDD 17.13); senão é uma pessoa nova.
            Person? owner = null;
            double roll = rng.NextDouble();
            if (roll < catalog.Regras.DonoLiderancaChance && city.Leaders.Count > 0)
            {
                owner = city.Person(rng.Pick(city.Leaders).PersonId);
            }
            else if (roll < catalog.Regras.DonoLiderancaChance + catalog.Regras.DonoDoadorChance && city.Donors.Count > 0)
            {
                owner = city.Person(rng.Pick(city.Donors).PersonId);
            }

            if (owner is null || owner.Has(PersonRole.DonoDeVeiculo))
            {
                owner = NewPerson(ctx, city, rng, PersonRole.DonoDeVeiculo, [new Interest { Kind = Interest.Tema, Target = interest[0] }], 40, 75);
            }
            else
            {
                owner.Roles.Add(PersonRole.DonoDeVeiculo);
            }

            city.Outlets.Add(new Outlet
            {
                Id = $"veiculo_{++index}",
                Name = name,
                Type = type.Id,
                Line = rng.PickWeighted(catalog.Linhas, l => l.Weight).Id,
                Themes = interest,
                Sensationalism = rng.NextDouble(catalog.Regras.Sensacionalismo.Min, catalog.Regras.Sensacionalismo.Max),
                OwnerId = owner.Id,
                JournalistId = journalist.Id,
                Reach = campaign.Territory.Zones.Select(_ => rng.NextDouble(type.Alcance.Min, type.Alcance.Max)).ToList(),
                Publico = rng.Pick(type.Publicos),
            });
        }
    }

    private static void Boss(CampaignContext ctx, CampaignState campaign, CityState city, Rng rng)
    {
        string partyId = campaign.Player.PartyId;
        if (city.Bosses.ContainsKey(partyId))
        {
            return;
        }

        Person boss = NewPerson(ctx, city, rng, PersonRole.Dirigente, [new Interest { Kind = Interest.Objetivo, Target = "eleger_a_chapa" }], 40, 72, partyId);
        city.Bosses[partyId] = boss.Id;
    }

    private static void Rivals(CampaignContext ctx, CampaignState campaign, CityState city, Rng rng)
    {
        RivalCatalog catalog = ctx.Campaign.Rivals;
        RivalRules rules = catalog.Regras;
        double scale = ctx.Config.PorteCostMultiplier.GetValueOrDefault(ctx.Municipio.Porte, 1);
        string? government = GovernmentParty(ctx, campaign);
        List<string> themes = Themes(ctx);
        campaign.Rivals.Records.RemoveAll(r => campaign.Find(r.CandidateId) is null);
        foreach (Candidate c in campaign.Candidates.Where(c => !c.IsPlayer && !c.IsBulk))
        {
            Person? person = city.Person(c.Id);
            if (person is null)
            {
                // A pessoa do rival: o mesmo modelo de liderança e equipe. O interesse é um tema e a zona em que ele é forte.
                var interests = new List<Interest> { new() { Kind = Interest.Tema, Target = rng.Pick(themes) } };
                if (campaign.Territory.Redutos.TryGetValue(c.Id, out List<int>? redutos) && redutos.Count > 0)
                {
                    interests.Add(new Interest { Kind = Interest.ZonaKind, Target = campaign.Territory.Zones[redutos[0]] });
                }

                person = new Person
                {
                    Id = c.Id,
                    Roles = [PersonRole.Rival],
                    Interests = interests,
                    Temperament = (Temperament)rng.NextInt(5),
                    Affinity = rng.NextInt(-10, 11) + (c.PartyId == campaign.Player.PartyId ? 10 : 0),
                };
                city.People.Add(person);
            }
            else if (!person.Has(PersonRole.Rival))
            {
                person.Roles.Add(PersonRole.Rival);
            }

            if (campaign.Rivals.Record(c.Id) is not null)
            {
                continue;
            }

            // O perfil é um estilo de campanha: o sorteio não olha partido nem ideologia; o de máquina só exige que o
            // rival esteja no cargo ou seja do partido da prefeitura.
            bool situacao = c.Incumbent || (government is not null && c.PartyId == government);
            RivalProfile profile = rng.PickWeighted(catalog.Perfis.Where(p => !p.ExigeSituacao || situacao).ToList(), p => p.Weight);
            var record = new RivalRecord
            {
                CandidateId = c.Id,
                ProfileId = profile.Id,
                Money = Math.Round(rules.CaixaInicial * (decimal)(scale * c.Drive * profile.VerbaFator)),
            };
            int weaknesses = rng.NextInt(rules.FraquezasPorRival.Min, rules.FraquezasPorRival.Max + 1);
            for (int i = 0; i < weaknesses; i++)
            {
                string type = rng.Pick<string>([AttackType.Incoerencia, AttackType.Esqueleto]);
                record.Weaknesses.Add(new RivalWeakness
                {
                    Id = $"{c.Id}_w{i + 1}",
                    Type = type,
                    Severity = rng.NextInt(1, 4),
                    Text = rng.Pick(catalog.Fraquezas[type]),
                });
            }

            campaign.Rivals.Records.Add(record);
        }
    }
}
