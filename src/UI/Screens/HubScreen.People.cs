using Godot;
using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Elections;
using Planalto.Core.People;
using Planalto.Core.State;
using Planalto.UI.Portrait;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// Área Pessoas e Ficha da pessoa (GDD 17.2 e 17.10): as lideranças que o jogador conhece, com o que são, a relação e
/// de que lado estão; na ficha, retrato, interesses, lembranças por extenso, o pedido e o que dá para fazer.
/// </summary>
public partial class HubScreen
{
    private LeaderCatalog LeaderContent => _engine.Context.Campaign.Leaders;

    /// <summary>De que lado a liderança está, por extenso.</summary>
    private string SideText(Leader leader) =>
        leader.AlignedWith == RaceSetup.PlayerId ? Tr("apoia você")
        : leader.AlignedWith is not null && Campaign.Find(leader.AlignedWith) is { } rival ? string.Format(Tr("apoia {0}"), CandidateLabel(rival))
        : Tr("neutra");

    private string TemperamentName(Temperament t) => t switch
    {
        Temperament.Conciliador => Tr("conciliador: prefere acordo a briga"),
        Temperament.Combativo => Tr("combativo: reage forte a recusa"),
        Temperament.Calculista => Tr("calculista: pesa o que ganha"),
        Temperament.Leal => Tr("leal: demora a mudar de lado"),
        _ => Tr("volúvel: muda de lado com facilidade"),
    };

    private PortraitView? Portrait(Node parent, string characterId, int size)
    {
        Appearance? appearance = State.Npcs.FirstOrDefault(n => n.Id == characterId)?.Appearance;
        if (appearance is null)
        {
            return null;
        }

        var view = parent.Add(new PortraitView { CustomMinimumSize = new Vector2(size, size), MouseFilter = MouseFilterEnum.Ignore });
        view.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        view.Show(appearance, new PortraitOptions(), _game.People.Appearance);
        return view;
    }

    private void BuildPeople()
    {
        var known = _engine.City.Leaders.Where(l => l.Known).OrderByDescending(l => _engine.RelationOf(l.PersonId)).ToList();
        int unknown = _engine.City.Leaders.Count - known.Count;
        Heading(_left, Tr("Lideranças que você conhece"), 20);
        if (known.Count == 0)
        {
            Glossary.Rich(_left, Tr("Você ainda não conhece nenhuma liderança. Uma caminhada numa zona pode revelar quem atua ali."));
        }

        foreach (Leader leader in known)
        {
            LeaderRow(_left, leader);
        }

        if (unknown > 0)
        {
            Glossary.Rich(_left, string.Format(Tr("Há mais {0} na cidade que você ainda não conhece: aparecem como ? na ficha da zona em que atuam."), unknown), 18, Palette.Texto3);
        }

        Office office = Campaign.Player.Office;
        Heading(_left, Tr(office == Office.Prefeito ? "Na disputa pela prefeitura" : "Principais rivais"), 20);
        foreach (Candidate rival in Campaign.ForOffice(office).Where(c => !c.IsPlayer && !c.IsBulk).OrderByDescending(c => c.Recognition).Take(6))
        {
            PersonRow(rival);
        }

        if (office != Office.Prefeito)
        {
            Heading(_left, Tr("Prefeitura"), 20);
            foreach (Candidate mayor in Campaign.ForOffice(Office.Prefeito))
            {
                PersonRow(mayor);
            }
        }
    }

    /// <summary>Linha de liderança: nome com o que é, zona, relação e lado; o cartão inteiro abre a ficha.</summary>
    private void LeaderRow(Node parent, Leader leader)
    {
        PanelContainer card = ClickCard(parent, () => OpenSheet(Sheet.Pessoa, leader.PersonId),
            Palette.Tip(LeaderLabel(leader) + ".", Tr("abre a ficha: interesses, relação, lembranças, o pedido e o que dá para fazer."), Tr("nada.")),
            "Lideranca_" + leader.PersonId);
        HBoxContainer row = Ui.Row(card, 10);
        Portrait(row, leader.PersonId, 56);
        var col = Ui.Column(row, 2);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Glossary.Rich(col, LeaderLabel(leader), 18, Palette.Texto, Palette.Weight(Palette.Inter, 500));
        Glossary.Rich(col, string.Format(Tr("{0} · relação {1} · {2}"), ZoneName(leader.Zones[0]), Signed(_engine.RelationOf(leader.PersonId), "0"), SideText(leader)));
    }

    private void PersonRow(Candidate c)
    {
        PanelContainer card = Card(_left, margin: 8);
        HBoxContainer row = Ui.Row(card, 10);
        Appearance? appearance = State.Npcs.FirstOrDefault(n => n.Id == c.CharacterId)?.Appearance;
        if (appearance is not null)
        {
            var view = row.Add(new PortraitView { CustomMinimumSize = new Vector2(56, 56) });
            view.SizeFlagsVertical = SizeFlags.ShrinkBegin;
            GameParty? party = State.Party(c.PartyId);
            view.Show(appearance, new PortraitOptions(Planalto.Core.Characters.Expression.Neutra, WardrobeContext.Campanha, party?.Colors.Primary, "background_solido", party?.Colors.Secondary), _game.People.Appearance);
        }

        var col = Ui.Column(row, 2);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Glossary.Plain(col, CandidateLabel(c), 18, Palette.Texto, Palette.Weight(Palette.Inter, 500));
        Glossary.Rich(col, string.Format(PtBr, "{0}{1} {2:0}%", c.Incumbent ? Tr("tem mandato") + " · " : "", Tr("reconhecimento"), c.Recognition));
    }

    // ------------------------------------------------------------------ ficha da pessoa

    private void BuildPersonSheet(Leader leader)
    {
        Person person = _engine.City.Person(leader.PersonId)!;
        PeopleConfig config = _engine.Context.Campaign.People;
        DateOnly today = _engine.Today;
        string name = _engine.NameOf(leader.PersonId);
        LeaderType type = LeaderContent.Type(leader.Type);
        BackButton();

        HBoxContainer top = Ui.Row(_left, 12);
        Portrait(top, leader.PersonId, 120);
        var head = Ui.Column(top, 4);
        head.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        NameTitle(head, name, 22);
        Glossary.Rich(head, string.Format(Tr("{0}. Pessoa fictícia."), char.ToUpper(type.Papel[0], PtBr) + type.Papel[1..]));
        Glossary.Rich(head, char.ToUpper(SideText(leader)[0], PtBr) + SideText(leader)[1..] + ".", 18, Palette.Texto);

        var visit = Ui.Button(_left, "Marcar visita", () =>
        {
            var (date, turno) = _engine.NextFreeSlot();
            StartSchedule(date, turno, visit: leader.PersonId);
        }, Palette.Tip(
            string.Format(Tr("Abre a marcação de uma visita a {0}."), name),
            string.Format(Tr("a visita rende {0} de relação e ela faz o pedido dela; começa no primeiro turno livre."), Signed(LeaderContent.Regras.PontosVisita, "0")),
            CostText(Catalog.Get(CommitmentCatalog.Visita))));
        visit.Name = "MarcarVisita";
        Palette.Primary(visit);
        if (BlockReason() is { } block)
        {
            visit.Disabled = true;
            visit.TooltipText = Palette.Blocked(string.Format(Tr("{0}; resolva antes de marcar."), block), visit.TooltipText);
        }

        Heading(_left, Tr("Onde atua"), 20);
        string publico = EffectSummary.GroupName(leader.Publico);
        Glossary.Rich(_left, string.Format(Tr("Zona: {0}. Move votos do grupo {1}. Influência: {2} de 5."), string.Join(", ", leader.Zones.Select(ZoneName)), publico, leader.Influence));
        Glossary.Rich(_left, string.Format(PtBr, Tr("Do seu lado, ela leva o apoio desse grupo na zona (mais com mais influência e mais relação) e, indo junto, faz o compromisso de rua render {0}."), Percent(1 + Catalog.Regras.LiderancaBonus)), 18, Palette.Texto3);

        Heading(_left, Tr("Interesses"), 20);
        foreach (Interest interest in person.Interests)
        {
            string text = interest.Kind switch
            {
                Interest.Tema => string.Format(Tr("· Tema: {0}"), _engine.Context.Themes.Themes.FirstOrDefault(t => t.Id == interest.Target)?.Name ?? interest.Target),
                Interest.ZonaKind => string.Format(Tr("· A zona dela: {0}"), ZoneName(interest.Target)),
                _ => "· " + interest.Target,
            };
            Glossary.Rich(_left, text);
        }

        Glossary.Rich(_left, string.Format(Tr("· Temperamento {0}"), TemperamentName(person.Temperament)));

        Heading(_left, Tr("Pedido"), 20);
        if (!leader.RequestKnown)
        {
            Glossary.Rich(_left, Tr("Você ainda não ouviu o que ela quer. Marque uma visita."));
        }
        else
        {
            Glossary.Rich(_left, leader.RequestText + ".", 18, Palette.Texto);
            Glossary.Rich(_left, leader.RequestMet
                ? Tr("Atendido.")
                : Tr("Sem resposta definitiva: numa próxima visita ela pergunta de novo."));
            Glossary.Rich(_left, string.Format(PtBr, Tr("Para apoiar você, ela quer o pedido atendido e relação de {0} ou mais (hoje: {1:0})."), leader.Price, _engine.RelationOf(leader.PersonId)), 18, Palette.Texto3);
        }

        Heading(_left, Tr("Relação com você"), 20);
        ValueRow(_left, Tr("Hoje (de −100 a 100)"), Signed(person.Relation(today, config), "0"), highlight: true);
        Glossary.Rich(_left, string.Format(Tr("Afinidade de partida {0}, mais as lembranças abaixo. Sem contato por {1} semanas, a relação começa a cair."), Signed(person.Affinity, "0"), LeaderContent.Regras.SemanasSemContato), 18, Palette.Texto3);
        if (person.Memories.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nenhuma lembrança de você ainda."));
        }

        foreach (Memory memory in person.Memories.AsEnumerable().Reverse())
        {
            double weight = Person.Weight(memory, today, config);
            string faded = Math.Abs(weight - memory.Pontos) >= 0.5 ? " " + string.Format(Tr("(hoje vale {0})"), Signed(weight, "0")) : "";
            Glossary.Rich(_left, string.Format(Tr("· {0}: {1}. {2} de relação{3}"), memory.Date.ToString("d MMM yyyy", PtBr), memory.Text, Signed(memory.Pontos, "0"), faded));
        }

        var rivals = leader.Rivals.Select(id => _engine.City.Leader(id)).Where(r => r is { Known: true }).ToList();
        if (rivals.Count > 0)
        {
            Heading(_left, Tr("Com quem disputa"), 20);
            Glossary.Rich(_left, string.Format(Tr("Disputa o mesmo público na mesma zona com {0}. Se uma fechar com você, a outra esfria ({1} de relação)."),
                string.Join(", ", rivals.Select(r => LeaderLabel(r!))), Signed(LeaderContent.Regras.PontosRivalidade, "0")));
        }
    }
}
