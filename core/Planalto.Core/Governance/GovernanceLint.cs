using Planalto.Core.Campaign;

namespace Planalto.Core.Governance;

/// <summary>
/// Lint das decisões de mandato (GDD 17.1, princípios d e e): todo instrumento, toda saída de fiscalização, todo
/// pedido de voto e toda opção de evento de mandato tem ganho e custo de naturezas diferentes, o que se declara bate
/// com o que os números de fato fazem, e a escolha deixa rastro (lembrança em alguém ou entrada no Livro).
/// </summary>
public static class GovernanceLint
{
    public static IEnumerable<string> Instruments(InstrumentCatalog catalog)
    {
        foreach (InstrumentDefinition i in catalog.Instrumentos)
        {
            string where = $"instrumento '{i.Id}'";
            foreach (string error in DecisionNature.Check(i.Ganho, i.Custo, where))
            {
                yield return error;
            }

            var gains = new HashSet<string>(StringComparer.Ordinal);
            var costs = new HashSet<string>(StringComparer.Ordinal);
            Add(i.Presenca > 0 || i.Id is InstrumentDefinition.Indicacao or InstrumentDefinition.Emenda or InstrumentDefinition.Fiscalizacao, gains, DecisionNature.Zona);
            Add(i.Imagem > 0 || i.Id is InstrumentDefinition.Requerimento or InstrumentDefinition.NegociarVoto, gains, DecisionNature.Posicao);
            // Projeto aprovado rende o apoio dos grupos a quem ele agrada; acordo de voto rende a relação com o colega.
            Add(i.Id is InstrumentDefinition.Projeto or InstrumentDefinition.NegociarVoto || i.RelacaoPrefeito > 0, gains, DecisionNature.Relacao);
            Add(i.Turnos > 0, costs, DecisionNature.Tempo);
            Add(i.Capital > 0 || i.Imagem < 0, costs, DecisionNature.Posicao);
            Add(i.RelacaoPrefeito < 0, costs, DecisionNature.Relacao);
            // Depende de outro (prefeito, comissão) ou deixa promessa no Livro.
            Add(i.ChanceBase < 1 || i.Id is InstrumentDefinition.NegociarVoto or InstrumentDefinition.Emenda or InstrumentDefinition.Projeto, costs, DecisionNature.Risco);
            foreach (string error in Declared(i.Ganho, i.Custo, gains, costs, where))
            {
                yield return error;
            }

            if (i.Turnos < 1 || i.Capital < 0 || i.ChanceBase is <= 0 or > 1 || i.Nome.Length is 0 or > 28 || (i.PrazoSemanas is { } p && (p.Min < 1 || p.Max < p.Min)))
            {
                yield return $"{where}: turnos e chance positivos, capital não negativo, prazo válido e nome de até 28 caracteres.";
            }
        }

        foreach (FindingOption f in catalog.Achados)
        {
            string where = $"achado '{f.Id}'";
            foreach (string error in DecisionNature.Check(f.Ganho, f.Custo, where))
            {
                yield return error;
            }

            var gains = new HashSet<string>(StringComparer.Ordinal);
            var costs = new HashSet<string>(StringComparer.Ordinal);
            Add(f.Imagem > 0, gains, DecisionNature.Posicao);
            Add(f.Presenca > 0 || f.GanhaObra, gains, DecisionNature.Zona);
            Add(f.RelacaoPrefeito > 0, gains, DecisionNature.Relacao);
            Add(f.RelacaoPrefeito < 0 || f.Lembranca.Pontos < 0, costs, DecisionNature.Relacao);
            Add(f.Esqueleto > 0, costs, DecisionNature.Risco);
            Add(f.Semanas is not null, costs, DecisionNature.Tempo);
            Add(f.Imagem < 0, costs, DecisionNature.Posicao);
            foreach (string error in Declared(f.Ganho, f.Custo, gains, costs, where))
            {
                yield return error;
            }

            if (f.Esqueleto is < 0 or > 3 || f.Nome.Length is 0 or > 28)
            {
                yield return $"{where}: gravidade de 0 a 3 e nome de até 28 caracteres.";
            }
        }

        foreach (VoteRequest r in catalog.Pedidos)
        {
            string where = $"pedido de voto '{r.Id}'";
            foreach (string error in DecisionNature.Check(r.Ganho, r.Custo, where))
            {
                yield return error;
            }

            // Atender rende o voto (posição) e a relação; custa a promessa no Livro (risco) e, no cargo, a folha.
            var gains = new HashSet<string>(StringComparer.Ordinal) { DecisionNature.Posicao, DecisionNature.Relacao };
            var costs = new HashSet<string>(StringComparer.Ordinal) { DecisionNature.Risco };
            Add(r.Id == VoteRequest.Cargo, costs, DecisionNature.Dinheiro);
            Add(r.Id is VoteRequest.EmendaReduto or VoteRequest.ObraReduto or VoteRequest.LiberarEmenda, costs, DecisionNature.Zona);
            foreach (string error in Declared(r.Ganho, r.Custo, gains, costs, where))
            {
                yield return error;
            }

            if (r.PrazoSemanas < 1 || r.Weight < 0 || r.Papeis.Count == 0)
            {
                yield return $"{where}: prazo positivo, peso não negativo e ao menos um papel.";
            }
        }
    }

    public static IEnumerable<string> Events(TermEventCatalog catalog)
    {
        foreach (TermEventDefinition e in catalog.Eventos)
        {
            bool othersBind = e.Opcoes.Any(o => o.Efeitos.Esqueleto > 0 || o.Efeitos.PromessaSemanas > 0 || o.Efeitos.Divida);
            bool othersCostMoney = e.Opcoes.Any(o => o.Efeitos.Dinheiro < 0 || o.Efeitos.Investimento < 0 || o.Efeitos.Folha > 0);
            foreach (TermEventOption o in e.Opcoes)
            {
                string where = $"evento de mandato '{e.Id}', opção '{o.Id}'";
                foreach (string error in DecisionNature.Check(o.Ganho, o.Custo, where))
                {
                    yield return error;
                }

                TermEffects fx = o.Efeitos;
                bool binds = fx.Esqueleto > 0 || fx.PromessaSemanas > 0 || fx.Divida;
                bool costsMoney = fx.Dinheiro < 0 || fx.Investimento < 0 || fx.Folha > 0;
                var gains = new HashSet<string>(StringComparer.Ordinal);
                var costs = new HashSet<string>(StringComparer.Ordinal);
                Add(fx.Dinheiro > 0 || fx.Investimento > 0 || fx.Folha < 0 || (othersCostMoney && !costsMoney), gains, DecisionNature.Dinheiro);
                Add(costsMoney, costs, DecisionNature.Dinheiro);
                Add(fx.Estresse < 0 || fx.ObraSemanas < 0, gains, DecisionNature.Tempo);
                Add(fx.Estresse > 0 || fx.ObraSemanas > 0, costs, DecisionNature.Tempo);
                Add(fx.Relacao > 0 || fx.RelacaoPrefeito > 0 || fx.Lealdade > 0 || fx.Lembranca is { Pontos: > 0 }, gains, DecisionNature.Relacao);
                Add(fx.Relacao < 0 || fx.RelacaoPrefeito < 0 || fx.Lealdade < 0 || fx.Sai || fx.Lembranca is { Pontos: < 0 }, costs, DecisionNature.Relacao);
                Add(fx.Presenca > 0 || fx.AprovacaoZona > 0, gains, DecisionNature.Zona);
                Add(fx.Presenca < 0 || fx.AprovacaoZona < 0, costs, DecisionNature.Zona);
                // Não se amarrar ao que outra opção poria no Livro é manter a posição livre.
                Add(fx.Imagem > 0 || fx.Capital > 0 || fx.Aprovacao > 0 || fx.CpiProva < 0 || (othersBind && !binds), gains, DecisionNature.Posicao);
                Add(fx.Imagem < 0 || fx.Capital < 0 || fx.Aprovacao < 0 || fx.CpiProva > 0, costs, DecisionNature.Posicao);
                Add(binds, costs, DecisionNature.Risco);
                foreach (string error in Declared(o.Ganho, o.Custo, gains, costs, where))
                {
                    yield return error;
                }

                // Princípio d: a escolha grava lembrança em quem estava envolvido ou uma entrada no Livro.
                if (fx.Lembranca is null && !binds)
                {
                    yield return $"{where}: decisão sem rastro (nem lembrança, nem entrada no Livro).";
                }

                if (fx.Esqueleto is < 0 or > 3 || fx.PromessaSemanas < 0 || o.Rotulo.Length is 0 or > 60)
                {
                    yield return $"{where}: gravidade de 0 a 3, prazo de promessa não negativo e rótulo de até 60 caracteres.";
                }
            }
        }
    }

    /// <summary>Opções de decisão que o lint cobre (para o relatório do validador).</summary>
    public static int Count(GovernanceContent content) =>
        content.Instruments.Instrumentos.Count + content.Instruments.Achados.Count + content.Instruments.Pedidos.Count + content.Events.Eventos.Sum(e => e.Opcoes.Count)
        + content.Personnel.Respostas.Count + content.Procurement.Modalidades.Count;

    private static IEnumerable<string> Declared(IEnumerable<string> gain, IEnumerable<string> cost, HashSet<string> gains, HashSet<string> costs, string where)
    {
        foreach (string nature in gain.Where(g => !gains.Contains(g)))
        {
            yield return $"{where}: ganho '{nature}' declarado sem efeito que o sustente.";
        }

        foreach (string nature in cost.Where(c => !costs.Contains(c)))
        {
            yield return $"{where}: custo '{nature}' declarado sem efeito que o sustente.";
        }
    }

    private static void Add(bool condition, HashSet<string> set, string nature)
    {
        if (condition)
        {
            set.Add(nature);
        }
    }
}
