using Godot;
using Planalto.Core.Indicators;
using Planalto.Core.Renewal;
using Planalto.Core.State;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// Indicadores em unidade real (GDD 17.17) e a renovação do mundo político (GDD 17.16): o painel com cada indicador no
/// valor real, unidade, fonte e ano, marca de estimativa, variação no mandato, comparação com a UF e "por que mudou";
/// e a composição da Câmara, com cadeiras, suplentes, idade e a história recente de posses e saídas.
/// </summary>
public partial class HubScreen
{
    private IndicatorDefinitions IndicatorDefs => _game.IndicatorModel.Definitions;

    /// <summary>Valor na unidade real, com as casas que a grandeza pede: "5,8", "72,4", "R$ 4.812".</summary>
    private static string IndicatorNumber(double value, string unit)
    {
        if (unit.StartsWith("R$", StringComparison.Ordinal))
        {
            return string.Create(PtBr, $"R$ {value:N0}");
        }

        double abs = Math.Abs(value);
        return value.ToString(abs >= 1000 ? "N0" : abs >= 100 ? "0" : "0.0", PtBr);
    }

    private static string IndicatorUnit(string unit) => unit.StartsWith("R$", StringComparison.Ordinal) ? unit[2..].Trim() : unit;

    /// <summary>Variação na unidade real, com uma casa a mais quando é pequena; o que arredonda para zero vira "0".</summary>
    private static string SignedIndicator(double delta, string unit)
    {
        double abs = Math.Abs(delta);
        string number = unit.StartsWith("R$", StringComparison.Ordinal) ? string.Create(PtBr, $"R$ {abs:N0}") : abs.ToString(abs >= 1000 ? "N0" : abs >= 100 ? "0" : abs >= 1 ? "0.0" : "0.00", PtBr);
        return number.Trim('R', '$', ' ', '0', ',', '.').Length == 0 ? "0" : (delta < 0 ? "−" : "+") + number;
    }

    private static bool RoundsToZero(double delta, string unit) => SignedIndicator(delta, unit) == "0";

    private string Verdict(bool? better) => better is null ? Tr("igual") : better.Value ? Tr("melhor") : Tr("pior");

    private string FactorText(IndicatorFactor f) => f.Texto.Length > 0 ? f.Texto : f.Tipo switch
    {
        IndicatorFactor.Tendencia => Tr("tendência dos últimos anos"),
        IndicatorFactor.Ciclo => Tr("ciclo da economia"),
        IndicatorFactor.Vizinho => Tr("efeito de outro indicador"),
        IndicatorFactor.Retorno => Tr("volta ao patamar de partida"),
        IndicatorFactor.Limite => Tr("limite plausível do indicador"),
        IndicatorFactor.Choque => Tr("acontecimento na cidade"),
        _ => Tr("ações do governo"),
    };

    private void BuildIndicatorsArea()
    {
        if (State.Indicators is not { } real)
        {
            Glossary.Rich(_left, Tr("Esta cidade não tem indicadores importados."));
            BuildSeatsBlock();
            return;
        }

        Core.Municipios.Municipio city = City;
        Glossary.Rich(_left, string.Format(PtBr, Tr("Ficha de {0} ({1}): {2:N0} habitantes. Cada número parte do dado oficial mais recente; daí em diante é simulado. À direita, o valor de hoje e a variação neste mandato."), city.Nome, city.Uf, real.Populacao), 18, Palette.Texto);
        foreach (IndicatorReading r in IndicatorQueries.All(real, IndicatorDefs))
        {
            IndicatorChange change = IndicatorQueries.MandateChange(real, IndicatorDefs, r.Id);
            IndicatorComparison compare = IndicatorQueries.CompareUf(real, IndicatorDefs, r.Id);
            bool still = change.Melhorou is null || RoundsToZero(change.Delta, r.Unidade);
            string arrow = still ? "→" : change.Melhorou!.Value ? "▲" : "▼";
            string id = r.Id;
            string source = r.AnoFonte > 0 ? string.Format(Tr("{0}, {1}"), r.Fonte, r.AnoFonte) : r.Fonte;
            PanelContainer card = ClickCard(_left, () => OpenGov("indicador", id),
                Palette.Tip(string.Format(Tr("{0}: {1} {2}. Fonte: {3}."), r.Nome, IndicatorNumber(r.Valor, r.Unidade), IndicatorUnit(r.Unidade), source),
                    Tr("abre o indicador: o dado de partida, a variação no mandato, a comparação com cidades parecidas do estado e por que ele mudou."), Tr("nada.")), "Indicador_" + r.Id);
            var col = Ui.Column(card, 2);
            Glossary.Plain(col, r.Nome + (r.Estimativa ? " · " + Tr("estimativa") : ""), 18, Palette.Texto, Palette.Weight(Palette.Inter, 500));
            Glossary.Rich(col, $"{IndicatorNumber(r.Valor, r.Unidade)} {IndicatorUnit(r.Unidade)}", 18, Palette.Texto);
            Glossary.Rich(col, string.Format(Tr("{0} no mandato: {1} · no estado: {2}"), arrow, still ? Tr("sem mudança") : SignedIndicator(change.Delta, r.Unidade),
                compare.Melhor is null || RoundsToZero(compare.Diferenca, r.Unidade) ? Tr("na mediana") : compare.Melhor.Value ? Tr("melhor que a mediana") : Tr("pior que a mediana")), 18, Palette.Texto2);
            Glossary.Rich(col, string.Format(Tr("Fonte: {0}"), source), 18, Palette.Texto3);
        }

        Glossary.Rich(_left, Tr("▲ melhorou e ▼ piorou desde o começo do mandato, no sentido de cada indicador (mortalidade menor é melhor)."), 18, Palette.Texto3);
        BuildSeatsBlock();
    }

    private void BuildIndicatorSheet(string id)
    {
        _govPick = null;
        MunicipalIndicatorsState real = State.Indicators!;
        IndicatorReading r = IndicatorQueries.Current(real, IndicatorDefs, id);
        GovBackButton();
        NameTitle(_left, r.Nome, 22);
        PanelContainer card = Card(_left, Palette.Cartao, Palette.Linha);
        card.Name = "ValorIndicador";
        var col = Ui.Column(card, 4);
        Label value = Ui.Value(col, IndicatorNumber(r.Valor, r.Unidade), 32);
        value.AddThemeFontOverride("font", Palette.Weight(Palette.Oswald, 600));
        value.AddThemeColorOverride("font_color", Palette.Texto);
        Glossary.Rich(col, char.ToUpper(IndicatorUnit(r.Unidade)[0], PtBr) + IndicatorUnit(r.Unidade)[1..] + ". " + (r.MaiorMelhor ? Tr("Quanto maior, melhor.") : Tr("Quanto menor, melhor.")));
        Glossary.Rich(col, string.Format(Tr("Fonte: {0}{1}."), r.Fonte, r.AnoFonte > 0 ? string.Format(Tr(", dado de {0}"), r.AnoFonte) : ""), 18, Palette.Texto3);
        Glossary.Rich(col, string.Format(Tr("Dado de partida: {0}. {1}"), IndicatorNumber(r.Partida, r.Unidade), r.Simulado ? Tr("A partir daí, simulado pelo jogo.") : Tr("O jogo ainda não mexeu neste número.")), 18, Palette.Texto3);
        if (r.Estimativa)
        {
            PanelContainer mark = Card(_left, Palette.CartaoAlto, Palette.Linha, 10);
            mark.Name = "Estimativa";
            Glossary.Rich(mark, string.Format(Tr("Estimativa: o dado de partida não é do próprio município. Regra usada: {0}"), LowerFirst(r.RegraEstimativa ?? Tr("preenchimento pela regra do importador")).TrimEnd('.') + "."), 18, Palette.Texto);
        }

        Heading(_left, Tr("Neste mandato"), 20);
        IndicatorChange change = IndicatorQueries.MandateChange(real, IndicatorDefs, id);
        ValueRow(_left, string.Format(Tr("Desde {0}: de {1} para {2}"), change.Desde.ToString("MM/yyyy", PtBr), IndicatorNumber(change.Antes, r.Unidade), IndicatorNumber(change.Agora, r.Unidade)),
            change.Melhorou is null || RoundsToZero(change.Delta, r.Unidade) ? Tr("igual") : $"{SignedIndicator(change.Delta, r.Unidade)} ({Verdict(change.Melhorou)})", highlight: true);
        Glossary.Rich(_left, Tr("O eleitor julga a variação no mandato e a diferença para cidades parecidas, não o nível absoluto."), 18, Palette.Texto3);

        Heading(_left, Tr("Comparação com o estado"), 20);
        IndicatorComparison compare = IndicatorQueries.CompareUf(real, IndicatorDefs, id);
        ValueRow(_left, string.Format(Tr("Mediana de {0} municípios de mesmo porte em {1}: {2}"), compare.Municipios, compare.Uf, IndicatorNumber(compare.Referencia, r.Unidade)),
            compare.Melhor is null || RoundsToZero(compare.Diferenca, r.Unidade) ? Tr("igual") : $"{SignedIndicator(compare.Diferenca, r.Unidade)} ({Verdict(compare.Melhor)})");

        Heading(_left, Tr("Por que mudou"), 20);
        bool any = false;
        foreach (var (variation, title) in new[] { (IndicatorQueries.YearChange(real, id), "Neste ano"), (IndicatorQueries.PreviousYearChange(real, id), "No ano passado") })
        {
            if (variation is null || variation.Fatores.Count == 0)
            {
                continue;
            }

            any = true;
            PanelContainer why = Card(_left, Palette.Cartao, Palette.Linha, 10);
            why.Name = title == "Neste ano" ? "PorQueMudou" : "PorQueMudouAntes";
            var list = Ui.Column(why, 4);
            Glossary.Rich(list, string.Format(Tr("{0}: de {1} para {2}."), Tr(title), IndicatorNumber(variation.Antes, r.Unidade), IndicatorNumber(variation.Depois, r.Unidade)), 18, Palette.Texto);
            foreach (IndicatorFactor f in variation.Fatores.Where(x => !RoundsToZero(x.Delta, r.Unidade)).Take(6))
            {
                Glossary.Rich(list, string.Format(Tr("· {0}: {1}"), char.ToUpper(FactorText(f)[0], PtBr) + FactorText(f)[1..], SignedIndicator(f.Delta, r.Unidade)));
            }
        }

        if (!any)
        {
            Glossary.Rich(_left, Tr("Ainda não mudou: o número é o de partida. Os fatores aparecem aqui quando a tendência, a economia, o orçamento, as obras ou uma crise mexerem nele."));
        }
    }

    // ------------------------------------------------------------------ Câmara: cadeiras, suplentes, idade

    private void BuildSeatsSummary(Node parent)
    {
        if (State.Renewal is not { } renewal)
        {
            Glossary.Rich(parent, Tr("Sem dados da Câmara."));
            return;
        }

        DateOnly today = Clock.Today(State);
        var seats = RenewalQueries.Chamber(State, today);
        ExecutiveView executive = RenewalQueries.Executive(State);
        int vacant = seats.Count(s => s.Vaga);
        Glossary.Rich(parent, string.Format(PtBr, Tr("{0} cadeiras, {1} vaga(s). Idade média: {2:0} anos. Legislatura até {3}."), seats.Count, vacant, RenewalQueries.AverageAge(State, today), renewal.MandatoFim.ToString("MM/yyyy", PtBr)));
        Glossary.Rich(parent, OneDot(executive.Vaga
            ? string.Format(Tr("Prefeitura vaga: responde {0}, que preside a Câmara."), executive.RespondeNome)
            : string.Format(Tr("Prefeitura: {0} ({1}); vice: {2}."), executive.PrefeitoId == Core.Campaign.RaceSetup.PlayerId ? Tr("você") : executive.PrefeitoNome, GovPartyOf(executive.PartyId), executive.ViceNome.Length == 0 ? Tr("cargo vago") : executive.ViceNome)));
        if (RenewalQueries.InMourning(renewal, State.Calendar.WeekStart))
        {
            Glossary.Rich(parent, string.Format(Tr("Luto oficial até {0}."), renewal.LutoAte?.ToString("d 'de' MMMM", PtBr)), 18, Palette.Texto, Palette.Serif);
        }

        var open = Ui.Button(parent, "Ver as cadeiras", () => OpenGov("cadeiras", null),
            Palette.Tip(Tr("Abre a composição da Câmara."), Tr("mostra cada cadeira com titular, partido e idade, os suplentes de cada partido e as posses e saídas recentes."), Tr("nada.")));
        open.Name = parent == _poll ? "VerCadeirasColuna" : "VerCadeiras";
    }

    private void BuildSeatsBlock()
    {
        Heading(_left, Tr("Composição da Câmara"), 20);
        BuildSeatsSummary(_left);
    }

    private void BuildSeatsSheet()
    {
        _govPick = null;
        RenewalState renewal = State.Renewal!;
        DateOnly today = Clock.Today(State);
        GovBackButton();
        NameTitle(_left, Tr("Câmara Municipal"), 22);
        var seats = RenewalQueries.Chamber(State, today);
        Glossary.Rich(_left, string.Format(PtBr, Tr("{0}ª legislatura do jogo, de {1} a {2}. Idade média dos vereadores: {3:0} anos."), renewal.Legislatura, renewal.MandatoInicio.ToString("MM/yyyy", PtBr), renewal.MandatoFim.ToString("MM/yyyy", PtBr), RenewalQueries.AverageAge(State, today)), 18, Palette.Texto);
        ExecutiveView executive = RenewalQueries.Executive(State);
        if (executive.Eleicao is { } election)
        {
            PanelContainer card = Card(_left, Palette.CartaoAlto, Palette.Linha, 10);
            card.Name = "EleicaoSuplementar";
            Glossary.Rich(card, string.Format(Tr("Nova eleição para a Prefeitura em {0}: {1}. Regra: {2}."), election.Data.ToString("dd/MM/yyyy", PtBr), election.Indireta ? Tr("indireta, pela Câmara") : Tr("direta, pelo voto"), election.Regra), 18, Palette.Texto);
        }

        foreach (var group in seats.GroupBy(s => s.PartyId).OrderByDescending(g => g.Count()))
        {
            Heading(_left, string.Format(Tr("{0} · {1} cadeira(s)"), GovPartyOf(group.Key), group.Count()), 20);
            foreach (SeatView seat in group.OrderBy(s => s.Numero))
            {
                if (seat.Vaga)
                {
                    Glossary.Rich(_left, string.Format(Tr("· Cadeira {0}: vaga desde {1}{2}."), seat.Numero, seat.VagaDesde?.ToString("dd/MM/yyyy", PtBr), seat.VagaPrazo is { } until ? string.Format(Tr(", até {0}"), until.ToString("dd/MM/yyyy", PtBr)) : ""), 18, Palette.Texto);
                    continue;
                }

                string name = seat.TitularId == Core.Campaign.RaceSetup.PlayerId ? Tr("Você") : seat.Nome;
                string marks = (seat.Presidente ? " · " + Tr("preside a Câmara") : "") + (seat.Idade is { } age ? " · " + string.Format(Tr("{0} anos"), age) : "");
                if (seat.TitularId is { } holder && holder != Core.Campaign.RaceSetup.PlayerId && State.City?.Person(holder) is not null)
                {
                    GovRow(_left, $"{name}{marks}", () => OpenGov("pessoa", holder), Palette.Tip(string.Format(Tr("{0} (vereador, {1})."), name, GovPartyOf(seat.PartyId)), Tr("abre a ficha: relação com você e lembranças."), Tr("nada.")), "Cadeira_" + seat.Numero);
                }
                else
                {
                    Glossary.Rich(_left, $"· {name}{marks}.");
                }
            }

            var substitutes = RenewalQueries.Substitutes(renewal, group.Key);
            Glossary.Rich(_left, substitutes.Count == 0 ? Tr("Sem suplentes: cadeira que vagar vai a eleição ou fica vaga, conforme o tempo de mandato que restar.")
                : string.Format(Tr("Suplentes, na ordem de chamada: {0}."), string.Join(", ", substitutes.Take(4).Select(GovName))), 18, Palette.Texto3);
        }

        Heading(_left, Tr("Posses e saídas recentes"), 20);
        var history = RenewalQueries.History(renewal, RenewalEventKind.Morte, RenewalEventKind.Renuncia, RenewalEventKind.Cassacao, RenewalEventKind.JusticaEleitoral, RenewalEventKind.PosseSuplente,
            RenewalEventKind.PosseVice, RenewalEventKind.Interino, RenewalEventKind.EleicaoConvocada, RenewalEventKind.EleicaoResolvida, RenewalEventKind.CadeiraVaga, RenewalEventKind.Aposentadoria);
        if (history.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nada ainda nesta legislatura."));
        }

        foreach (RenewalEvent e in history.Take(8))
        {
            bool death = e.Tipo == RenewalEventKind.Morte;
            Glossary.Rich(_left, string.Format(Tr("· {0}: {1}{2}"), e.Data.ToString("dd/MM/yyyy", PtBr), e.Texto.TrimEnd('.') + ".", e.Regra is null ? "" : " " + string.Format(Tr("Regra: {0}."), e.Regra)), 18, death ? Palette.Texto : Palette.Texto2, death ? Palette.Serif : null);
        }
    }
}
