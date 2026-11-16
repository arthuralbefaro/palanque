using System.Globalization;
using Godot;
using Planalto.Autoload;
using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Debate;
using Planalto.Core.State;
using Planalto.UI.Portrait;
using Expression = Planalto.Core.Characters.Expression;

namespace Planalto.UI.Screens;

/// <summary>
/// Debate de TV (GDD 5.7): os candidatos lado a lado com expressões que reagem à última jogada, o tema da rodada,
/// a mão de cartas do jogador e o placar. No fim, o resultado e a volta à campanha.
/// </summary>
public partial class DebateScreen : Control
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private Game _game = null!;
    private CampaignEngine _engine = null!;
    private VBoxContainer _root = null!;
    private string? _target;
    private IReadOnlyList<DebatePlay> _lastRound = [];

    private CampaignState Campaign => _game.State!.Campaign!;

    private DebateState Debate => Campaign.Debate!;

    public override void _Ready()
    {
        _game = Game.Instance;
        if (_game.Engine is null || _game.State?.Campaign?.Debate is null)
        {
            if (_game.State is null)
            {
                _game.GoTo(Game.Scenes.MainMenu);
            }
            else
            {
                _game.GoToCareer();
            }

            return;
        }

        _engine = _game.Engine;
        _root = Ui.Screen(this, 24);
        Refresh();
    }

    private string ThemeName(string? id) => _game.World.Parties.Themes.Themes.FirstOrDefault(t => t.Id == id)?.Name ?? id ?? "";

    private void Refresh()
    {
        Ui.Clear(_root);
        HBoxContainer header = Ui.Row(_root, 20);
        Ui.Value(header, string.Format(Tr("Debate na TV · {0}"), _engine.Context.Municipio.Nome), 32);
        header.Add(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        if (!Debate.Finished)
        {
            Ui.Value(header, string.Format(Tr("Rodada {0} de {1} · tema: {2}"), Debate.Round + 1, Debate.Topics.Count, ThemeName(Debate.Topic)), 22);
        }

        BuildStage();
        if (Debate.Finished)
        {
            BuildResult();
        }
        else
        {
            BuildHand();
        }

        BuildLastRound();
    }

    /// <summary>Retratos com a expressão da última jogada e o placar de cada um.</summary>
    private void BuildStage()
    {
        HBoxContainer stage = Ui.Row(_root, 28);
        foreach (string id in Debate.Participants)
        {
            Candidate c = Campaign.Find(id)!;
            VBoxContainer box = Ui.Column(stage, 6);
            Appearance? face = c.IsPlayer ? _game.State!.Player.Appearance : _game.State!.Npcs.FirstOrDefault(n => n.Id == c.CharacterId)?.Appearance;
            if (face is not null)
            {
                var portrait = box.Add(new PortraitView { CustomMinimumSize = new Vector2(200, 200) });
                portrait.Show(face, new PortraitOptions(ExpressionFor(id), WardrobeContext.Debate), _game.People.Appearance);
            }

            Label name = Ui.Value(box, $"{c.Name} ({_game.State!.Party(c.PartyId)?.Sigla})", 18);
            if (c.IsPlayer)
            {
                name.AddThemeColorOverride("font_color", Ui.Warning);
            }

            string style = c.IsPlayer ? Tr("você") : _game.Debate.Styles.FirstOrDefault(s => s.Id == Debate.Styles.GetValueOrDefault(id))?.Name ?? "";
            Ui.Value(box, string.Format(PtBr, "{0} · {1:+0.0;-0.0;0.0} {2}", style, Debate.Scores[id], Tr("pontos")), 16);
        }
    }

    private Expression ExpressionFor(string id)
    {
        if (_lastRound.Any(p => p.TargetId == id && p.Success && _game.Debate.Card(p.CardId).Targeted))
        {
            return Expression.Bravo;
        }

        DebatePlay? own = _lastRound.FirstOrDefault(p => p.ParticipantId == id);
        return own is null ? Expression.Neutra : own.Success ? Expression.Sorriso : Expression.Preocupado;
    }

    private void BuildHand()
    {
        Ui.Label(_root, "Sua mão", 24);
        var rivals = Debate.Participants.Skip(1).ToList();
        _target ??= rivals.FirstOrDefault();
        if (Debate.Hand.Any(c => _game.Debate.Card(c).Targeted))
        {
            Ui.Options(Ui.Field(_root, "Alvo do ataque"), rivals.Select(r => (r, Campaign.Find(r)!.Name)), _target, id => _target = id, translate: false);
        }

        HBoxContainer hand = Ui.Row(_root, 18);
        foreach (string cardId in Debate.Hand)
        {
            DebateCard card = _game.Debate.Card(cardId);
            PanelContainer panel = Ui.Panel(hand, 14);
            panel.CustomMinimumSize = new Vector2(420, 0);
            VBoxContainer box = Ui.Column(panel, 8);
            Ui.Value(box, card.Name, 24);
            Ui.Paragraph(box, card.Description, 390, 16);
            Ui.Button(box, "Jogar", () =>
            {
                _lastRound = DebateEngine.Play(_engine.Context, _game.Debate, card.Id, card.Targeted ? _target : null, _game.State!.Rng);
                Refresh();
            });
        }
    }

    private void BuildLastRound()
    {
        if (_lastRound.Count == 0)
        {
            return;
        }

        Ui.Label(_root, "Última rodada", 20);
        foreach (DebatePlay play in _lastRound)
        {
            string who = Campaign.Find(play.ParticipantId)!.Name;
            string card = _game.Debate.Card(play.CardId).Name;
            string target = play.TargetId is null ? "" : " → " + Campaign.Find(play.TargetId)!.Name;
            string result = play.Success ? Tr("acertou") : Tr("errou");
            Ui.Value(_root, string.Format(PtBr, "{0}: {1}{2} · {3} ({4:+0.0;-0.0;0.0})", who, card, target, result, play.Points), 16);
        }
    }

    private void BuildResult()
    {
        string winner = Campaign.Find(Debate.Scores.MaxBy(s => s.Value).Key)!.Name;
        Ui.Value(_root, string.Format(Tr("Quem ganhou o debate, para quem assistiu: {0}."), winner), 26);
        foreach (string id in Debate.Viral)
        {
            Ui.Label(_root, string.Format(Tr("Um trecho de {0} viralizou depois do debate."), Campaign.Find(id)!.Name), 18, Ui.Warning);
        }

        Ui.Button(_root, "Voltar à campanha", () =>
        {
            DebateEngine.Close(Campaign);
            SaveManager.Instance.Autosave();
            _game.GoTo(Game.Scenes.Hub);
        });
    }
}
