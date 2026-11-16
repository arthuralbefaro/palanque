namespace Planalto.Core.Renewal;

/// <summary>Por que um cargo ficou vago.</summary>
public enum VacancyCause
{
    Morte,
    Renuncia,

    /// <summary>Cassação do mandato pela Câmara Municipal.</summary>
    Cassacao,

    /// <summary>Decisão da Justiça Eleitoral: indeferimento de registro, cassação de diploma ou perda de mandato.</summary>
    JusticaEleitoral,

    /// <summary>Vereador que deixa a cadeira para assumir a Prefeitura (eleito em nova eleição).</summary>
    Investidura,
}

/// <summary>O que acontece com a Prefeitura quando o prefeito sai.</summary>
public enum ExecutiveSuccession
{
    ViceAssume,
    EleicaoDireta,
    EleicaoIndireta,
}

/// <summary>O que acontece com uma cadeira vaga na Câmara.</summary>
public enum CouncilSuccession
{
    SuplenteAssume,
    NovaEleicao,
    VagaAteOFimDoMandato,
}

/// <summary>Base legal de uma regra, com a marca de conferência no texto oficial.</summary>
/// <param name="Artigo">Dispositivo citado.</param>
/// <param name="Conferido">True só quando o texto consta de docs/referencias/LEIS_CONFERIDAS.md.</param>
/// <param name="Nota">O que falta conferir ou o que é simplificação do jogo.</param>
public sealed record LegalBasis(string Artigo, bool Conferido, string Nota);

/// <summary>
/// Regras de substituição de mandatários no município (GDD 17.16 e 17.20, item 12). O padrão reproduz a regra descrita
/// no GDD; mudar aqui não exige mexer no motor. Os prazos em dias que não vêm do dispositivo citado estão marcados
/// como parâmetro de jogo.
/// </summary>
public sealed record SuccessionRulesConfig
{
    public static SuccessionRulesConfig Default { get; } = new();

    // ---------------------------------------------------------------- vereador

    /// <summary>Vaga de vereador: assume o suplente do mesmo partido ou federação.</summary>
    public LegalBasis BaseVereador { get; init; } = new(
        "Constituição Federal, art. 56, §§ 1º e 2º (aplicado aos municípios por simetria)",
        false,
        "O GDD 17.20 registra leitura do art. 56 na pesquisa, mas o artigo não consta de docs/referencias/LEIS_CONFERIDAS.md: a conferir.");

    /// <summary>Sem suplente, há eleição para a cadeira se faltarem mais de tantos meses para o fim do mandato (art. 56, § 2º).</summary>
    public int MesesParaEleicaoSemSuplente { get; init; } = 15;

    // ---------------------------------------------------------------- prefeito: morte, renúncia, cassação pela Câmara

    /// <summary>
    /// Morte, renúncia ou cassação pela Câmara: assume o vice. Vagos os dois, o presidente da Câmara responde pela
    /// Prefeitura e há nova eleição: direta na primeira parte do mandato, indireta, pela Câmara, na segunda.
    /// </summary>
    public LegalBasis BaseDuplaVacancia { get; init; } = new(
        "Constituição Federal, art. 81, caput e § 1º (padrão do jogo, espelhado para o município; decisão do autor no GDD 17.20, item 12)",
        false,
        "Simplificação: a Lei Orgânica de cada município pode dispor de outro modo. O GDD 17.20 registra leitura do art. 81 na pesquisa, mas o artigo não consta de docs/referencias/LEIS_CONFERIDAS.md: a conferir.");

    public bool ViceAssume { get; init; } = true;

    /// <summary>Fração do mandato em que a dupla vacância leva a eleição direta (0,5 = a primeira metade, os dois primeiros anos).</summary>
    public double FracaoDoMandatoComEleicaoDireta { get; init; } = 0.5;

    /// <summary>Dias entre a última vaga e a eleição direta (art. 81, caput: noventa dias).</summary>
    public int PrazoEleicaoDiretaDias { get; init; } = 90;

    /// <summary>Dias entre a última vaga e a eleição indireta (art. 81, § 1º: trinta dias).</summary>
    public int PrazoEleicaoIndiretaDias { get; init; } = 30;

    // ---------------------------------------------------------------- prefeito: decisão da Justiça Eleitoral

    /// <summary>
    /// Vacância por decisão da Justiça Eleitoral (indeferimento de registro, cassação de diploma ou perda de mandato):
    /// cai a chapa, o vice não assume, e há nova eleição; indireta só se a vacância ocorrer a menos de seis meses do
    /// fim do mandato, direta nos demais casos.
    /// </summary>
    public LegalBasis BaseJusticaEleitoral { get; init; } = new(
        "Código Eleitoral (Lei 4.737/1965), art. 224, §§ 3º e 4º",
        false,
        "NÃO conferido: o artigo não consta de docs/referencias/LEIS_CONFERIDAS.md (o GDD 17.20 registra leitura na pesquisa e a ADI 5525 só por resumo). Os prazos em dias da nova eleição não vêm destes parágrafos: o jogo usa os mesmos do art. 81 da Constituição, como parâmetro de jogo.");

    /// <summary>A menos de tantos meses do fim do mandato, a nova eleição é indireta (art. 224, § 4º, I).</summary>
    public int MesesFinaisComEleicaoIndireta { get; init; } = 6;

    // ---------------------------------------------------------------- decisões

    /// <summary>Destino de uma cadeira vaga na Câmara em <paramref name="vacancia"/>, num mandato que vai até <paramref name="fim"/>.</summary>
    public CouncilSuccession Council(bool haSuplente, DateOnly fim, DateOnly vacancia)
    {
        if (haSuplente)
        {
            return CouncilSuccession.SuplenteAssume;
        }

        return vacancia.AddMonths(MesesParaEleicaoSemSuplente) <= fim ? CouncilSuccession.NovaEleicao : CouncilSuccession.VagaAteOFimDoMandato;
    }

    /// <summary>
    /// Destino da Prefeitura quando o prefeito sai em <paramref name="vacancia"/>, num mandato de <paramref name="inicio"/>
    /// a <paramref name="fim"/> (último dia). <paramref name="viceDisponivel"/>: há vice para assumir.
    /// </summary>
    public ExecutiveSuccession Executive(VacancyCause cause, bool viceDisponivel, DateOnly inicio, DateOnly fim, DateOnly vacancia)
    {
        if (cause == VacancyCause.JusticaEleitoral)
        {
            return vacancia.AddMonths(MesesFinaisComEleicaoIndireta) > fim ? ExecutiveSuccession.EleicaoIndireta : ExecutiveSuccession.EleicaoDireta;
        }

        if (ViceAssume && viceDisponivel)
        {
            return ExecutiveSuccession.ViceAssume;
        }

        return vacancia < EndOfDirectPeriod(inicio, fim) ? ExecutiveSuccession.EleicaoDireta : ExecutiveSuccession.EleicaoIndireta;
    }

    /// <summary>Primeiro dia em que a dupla vacância já leva a eleição indireta (num mandato de quatro anos, o início do terceiro ano).</summary>
    public DateOnly EndOfDirectPeriod(DateOnly inicio, DateOnly fim)
    {
        int days = fim.DayNumber - inicio.DayNumber + 1;
        return inicio.AddDays((int)Math.Round(days * FracaoDoMandatoComEleicaoDireta));
    }

    /// <summary>Data da nova eleição para a Prefeitura.</summary>
    public DateOnly ElectionDate(ExecutiveSuccession kind, DateOnly vacancia) =>
        vacancia.AddDays(kind == ExecutiveSuccession.EleicaoIndireta ? PrazoEleicaoIndiretaDias : PrazoEleicaoDiretaDias);

    internal void Validate()
    {
        if (MesesParaEleicaoSemSuplente < 0 || MesesFinaisComEleicaoIndireta < 0 || PrazoEleicaoDiretaDias < 1 || PrazoEleicaoIndiretaDias < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MesesParaEleicaoSemSuplente), "Prazos não podem ser negativos.");
        }

        if (FracaoDoMandatoComEleicaoDireta is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(FracaoDoMandatoComEleicaoDireta), FracaoDoMandatoComEleicaoDireta, "Fração entre 0 e 1.");
        }
    }
}
