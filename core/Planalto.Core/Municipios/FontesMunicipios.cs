using Planalto.Core.Data;

namespace Planalto.Core.Municipios;

/// <summary>Conteúdo de data/municipios/_fontes.json: procedência de cada campo e totais oficiais.</summary>
public sealed class FontesMunicipios : IValidatable
{
    public required DateTimeOffset GeradoEm { get; init; }

    public required List<FonteCampo> Campos { get; init; }

    public required TotaisOficiais TotaisOficiais { get; init; }

    public required List<Lacuna> Lacunas { get; init; }

    /// <summary>Municípios cujas vagas reais de 2024 passam do teto atual (ex.: perderam população).</summary>
    public required List<ExcecaoVagas> ExcecoesVagas { get; init; }

    public IEnumerable<string> Validate()
    {
        if (Campos is null || Campos.Count == 0)
        {
            yield return "a lista 'campos' está vazia.";
        }
        else
        {
            foreach (FonteCampo campo in Campos)
            {
                string where = $"campos '{campo?.Campo}'";
                if (campo is null)
                {
                    yield return "campos: item null.";
                    continue;
                }

                foreach (string error in DataChecks.Text(campo.Campo, "campo", where)
                    .Concat(DataChecks.Text(campo.Fonte, "fonte", where))
                    .Concat(DataChecks.Text(campo.Url, "url", where)))
                {
                    yield return error;
                }
            }
        }

        if (TotaisOficiais is null)
        {
            yield return "totais_oficiais ausente.";
        }
        else
        {
            if (TotaisOficiais.TotalMunicipios <= 0)
            {
                yield return "totais_oficiais.total_municipios deve ser maior que zero.";
            }

            if (TotaisOficiais.PopulacaoPorUf is null || TotaisOficiais.PopulacaoPorUf.Count != 27)
            {
                yield return "totais_oficiais.populacao_por_uf deve ter as 27 UFs.";
            }
        }
    }
}

public sealed record FonteCampo
{
    public required string Campo { get; init; }

    public required string Fonte { get; init; }

    public required int Ano { get; init; }

    public required string Url { get; init; }

    public required DateTimeOffset BaixadoEm { get; init; }

    public string? Observacao { get; init; }
}

public sealed class TotaisOficiais
{
    /// <summary>Total de municípios na lista do IBGE (API de Localidades).</summary>
    public required int TotalMunicipios { get; init; }

    /// <summary>População estimada por UF, da tabela da própria UF (não da soma dos municípios).</summary>
    public required Dictionary<string, long> PopulacaoPorUf { get; init; }

    /// <summary>Soma das vagas de vereador na eleição ordinária de 2024, direto do arquivo do TSE.</summary>
    public required long VagasVereadorTse2024 { get; init; }
}

public sealed class Lacuna
{
    public required int CodigoIbge { get; init; }

    public required string Nome { get; init; }

    public required string Campo { get; init; }

    public required string Descricao { get; init; }

    public required string Alternativa { get; init; }
}

public sealed class ExcecaoVagas
{
    public required int CodigoIbge { get; init; }

    public required string Nome { get; init; }

    public required string Uf { get; init; }

    public required int VagasVereador { get; init; }

    public required int VagasVereadorMax { get; init; }
}
