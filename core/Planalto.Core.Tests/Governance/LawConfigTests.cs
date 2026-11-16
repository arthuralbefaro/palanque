using Planalto.Core.Governance;
using Xunit;

namespace Planalto.Core.Tests.Governance;

/// <summary>
/// Regras de lei dos mandatos em objetos de configuração, com o padrão da lei vigente, o artigo citado e a marca de
/// conferência no texto oficial (docs/referencias/LEIS_CONFERIDAS.md).
/// </summary>
public class LawConfigTests
{
    [Fact]
    public void Folha_alerta_prudencial_e_limite_da_LRF()
    {
        FiscalLawConfig law = FiscalLawConfig.Default;
        Assert.True(law.ConferidoNoTextoOficial);
        Assert.Equal(0.60, law.LimitePessoalMunicipio, 9);
        Assert.Equal(0.54, law.LimitePessoalExecutivo, 9);
        Assert.Equal(0.06, law.LimitePessoalLegislativo, 9);
        Assert.Equal(law.LimitePessoalMunicipio, law.LimitePessoalExecutivo + law.LimitePessoalLegislativo, 9);
        Assert.Equal(0.486, law.AlertaExecutivo, 9);
        Assert.Equal(0.513, law.PrudencialExecutivo, 9);
        Assert.Contains("art. 59", law.ArtigoAlerta);
        Assert.Contains("art. 22", law.ArtigoPrudencial);
        Assert.Contains("art. 23", law.ArtigoExcesso);
        Assert.Contains("20, III", law.ArtigoLimitePessoal);

        Assert.Equal(PayrollBand.Normal, law.Faixa(0.486));
        Assert.Equal(PayrollBand.Alerta, law.Faixa(0.49));
        Assert.Equal(PayrollBand.Alerta, law.Faixa(0.513));
        Assert.Equal(PayrollBand.Prudencial, law.Faixa(0.52));
        Assert.Equal(PayrollBand.Prudencial, law.Faixa(0.54));
        Assert.Equal(PayrollBand.Excedido, law.Faixa(0.541));

        // Art. 22, parágrafo único: as cinco vedações valem acima do prudencial, não antes.
        Assert.Equal(5, law.VedacoesPrudencial.Count);
        Assert.False(law.VedadoAumentar(0.51));
        Assert.True(law.VedadoAumentar(0.52));
        Assert.True(law.VedadoAumentar(0.56));
    }

    [Fact]
    public void Excesso_da_folha_tem_dois_quadrimestres_e_consequencias_do_art_23()
    {
        FiscalLawConfig law = FiscalLawConfig.Default;
        var start = new DateOnly(2030, 5, 6);
        Assert.Equal(2, law.QuadrimestresParaEliminar);
        Assert.Equal(1.0 / 3.0, law.FracaoNoPrimeiroQuadrimestre, 9);
        Assert.Equal(new DateOnly(2030, 9, 6), law.PrazoDoPrimeiroTerco(start));
        Assert.Equal(new DateOnly(2031, 1, 6), law.PrazoParaEliminar(start));
        Assert.Equal(3, law.ConsequenciasExcesso.Count);
        Assert.Contains(law.ConsequenciasExcesso, c => c.Contains("transferências voluntárias"));

        // Dentro do prazo não há restrição; vencido o prazo com o excesso de pé, há; sem excesso, nunca.
        Assert.False(law.RestricoesAtivas(0.56, start, new DateOnly(2030, 12, 1), 2032));
        Assert.True(law.RestricoesAtivas(0.56, start, new DateOnly(2031, 1, 7), 2032));
        Assert.False(law.RestricoesAtivas(0.53, start, new DateOnly(2031, 6, 1), 2032));
        Assert.False(law.RestricoesAtivas(0.56, null, new DateOnly(2031, 6, 1), 2032));

        // § 4º: no primeiro quadrimestre do último ano do mandato, as restrições valem de imediato.
        Assert.True(law.RestricoesAtivas(0.56, new DateOnly(2032, 3, 1), new DateOnly(2032, 3, 8), 2032));
        Assert.False(law.RestricoesAtivas(0.56, new DateOnly(2032, 6, 1), new DateOnly(2032, 6, 8), 2032));
    }

    [Fact]
    public void Minimos_de_saude_e_educacao_incidem_sobre_impostos_e_transferencias()
    {
        FiscalLawConfig law = FiscalLawConfig.Default;
        Assert.Equal(0.15, law.MinimoSaude, 9);
        Assert.Equal(0.25, law.MinimoEducacao, 9);
        Assert.Contains("198", law.ArtigoSaude);
        Assert.Contains("LC 141", law.ArtigoSaude);
        Assert.Contains("212", law.ArtigoEducacao);
        Assert.Contains("impostos", law.BaseDosMinimos);

        // A base não é o orçamento todo: com 60% da receita vindo de impostos e transferências, 15% viram 9%.
        Assert.Equal(0.09, law.MinimoSobreOrcamento("saude", 0.6), 9);
        Assert.Equal(0.15, law.MinimoSobreOrcamento("educacao", 0.6), 9);
        Assert.Equal(0, law.MinimoSobreOrcamento("obras", 0.6), 9);
        Assert.Equal(150_000m, law.MinimoEmReais("saude", 1_000_000m));
        Assert.Equal(250_000m, law.MinimoEmReais("educacao", 1_000_000m));
    }

    [Theory]
    [InlineData(20_000, 0.07)]
    [InlineData(100_000, 0.07)]
    [InlineData(100_001, 0.06)]
    [InlineData(300_000, 0.06)]
    [InlineData(300_001, 0.05)]
    [InlineData(500_000, 0.05)]
    [InlineData(500_001, 0.045)]
    [InlineData(3_000_000, 0.045)]
    [InlineData(3_000_001, 0.04)]
    [InlineData(8_000_000, 0.04)]
    [InlineData(8_000_001, 0.035)]
    public void Teto_de_gasto_da_Camara_por_faixa_de_populacao(long population, double cap)
    {
        FiscalLawConfig law = FiscalLawConfig.Default;
        Assert.Equal(cap, law.TetoCamara(population), 9);
        Assert.Contains("29-A", law.ArtigoCamara);
        Assert.Equal(0.70, law.FolhaMaximaDaCamara, 9);
        Assert.Equal(700_000m, law.FolhaMaximaDaCamaraEmReais(1_000_000m));
        Assert.Equal(1_000_000m * (decimal)cap, law.TetoCamaraEmReais(population, 1_000_000m));
    }

    [Fact]
    public void Dispensa_de_licitacao_por_valor_e_por_emergencia()
    {
        ProcurementLawConfig law = ProcurementLawConfig.Default;
        Assert.True(law.ConferidoNoTextoOficial);
        Assert.Equal(130_984.20m, law.DispensaObras);
        Assert.Equal(65_492.11m, law.DispensaOutros);
        Assert.Equal(2026, law.AnoDosValores);
        Assert.Contains("art. 75", law.ArtigoDispensaValor);
        Assert.Contains("12.807", law.ArtigoDispensaValor);
        Assert.Contains("art. 75, VIII", law.ArtigoEmergencia);
        Assert.Contains("pregão", law.Modalidades);
        Assert.Contains("concorrência", law.Modalidades);
        Assert.DoesNotContain("convite", law.Modalidades);
        Assert.DoesNotContain("tomada de preços", law.Modalidades);

        Assert.True(law.CabeDispensaPorValor(130_984.20m, engenharia: true));
        Assert.False(law.CabeDispensaPorValor(130_984.21m, engenharia: true));
        Assert.True(law.CabeDispensaPorValor(65_492.11m, engenharia: false));
        Assert.False(law.CabeDispensaPorValor(65_492.12m, engenharia: false));
        Assert.False(law.Permitida(ProcurementMode.DispensaPorValor, 200_000m, true, emergenciaReconhecida: true, 10));

        // Emergência: só com emergência reconhecida e parcela concluída em até 1 ano; sem prorrogar nem recontratar.
        Assert.Equal(12, law.EmergenciaPrazoMaximoMeses);
        Assert.True(law.EmergenciaVedaProrrogacao);
        Assert.True(law.EmergenciaVedaRecontratacao);
        Assert.True(law.Permitida(ProcurementMode.Emergencial, 5_000_000m, true, emergenciaReconhecida: true, 40));
        Assert.False(law.Permitida(ProcurementMode.Emergencial, 5_000_000m, true, emergenciaReconhecida: false, 40));
        Assert.False(law.Permitida(ProcurementMode.Emergencial, 5_000_000m, true, emergenciaReconhecida: true, 60));
        Assert.True(law.Permitida(ProcurementMode.Concorrencia, 5_000_000m, true, emergenciaReconhecida: false, 60));
    }

    [Fact]
    public void Inauguracao_vedada_nos_tres_meses_antes_do_pleito()
    {
        InaugurationLawConfig law = InaugurationLawConfig.Default;
        Assert.True(law.ConferidoNoTextoOficial);
        Assert.Equal(3, law.MesesAntesDoPleito);
        Assert.Contains("art. 77", law.Artigo);
        Assert.Contains("Cassação", law.Pena);

        // Eleição municipal de 2032: primeiro domingo de outubro, dia 3.
        DateOnly pleito = Planalto.Core.State.Calendar.ElectionFor(2032).FirstRound;
        Assert.Equal(new DateOnly(2032, 10, 3), pleito);
        Assert.Equal(new DateOnly(2032, 7, 3), law.InicioDoPeriodo(pleito));
        Assert.False(law.NoPeriodoVedado(new DateOnly(2032, 7, 2)));
        Assert.True(law.NoPeriodoVedado(new DateOnly(2032, 7, 3)));
        Assert.True(law.NoPeriodoVedado(pleito));
        Assert.False(law.NoPeriodoVedado(pleito.AddDays(1)));
        // Ano sem eleição não tem período vedado.
        Assert.False(law.NoPeriodoVedado(new DateOnly(2031, 8, 15)));
    }

    [Fact]
    public void Condutas_vedadas_em_ano_de_eleicao_e_a_regra_da_reeleicao()
    {
        ElectionConductLawConfig law = ElectionConductLawConfig.Default;
        Assert.True(law.ConferidoNoTextoOficial);
        Assert.Contains("art. 73", law.Artigo);
        Assert.Contains("art. 73, V", law.ArtigoPessoal);
        Assert.Contains("art. 73, VI", law.ArtigoTransferencias);
        Assert.Contains("art. 73, VII", law.ArtigoPublicidade);
        Assert.Contains("art. 73, VIII", law.ArtigoRevisao);
        Assert.Equal(3, law.MesesAntesDoPleito);
        Assert.Contains(law.VedacoesNosTresMeses, v => v.Contains("Transferências voluntárias"));
        Assert.Contains(law.VedacoesNosTresMeses, v => v.Contains("Publicidade institucional"));

        // Inciso VI: três meses antes do pleito de 3 de outubro de 2032.
        DateOnly pleito = Planalto.Core.State.Calendar.ElectionFor(2032).FirstRound;
        Assert.False(law.NosTresMeses(new DateOnly(2032, 7, 2)));
        Assert.True(law.NosTresMeses(new DateOnly(2032, 7, 3)));
        Assert.True(law.NosTresMeses(pleito));
        Assert.False(law.NosTresMeses(pleito.AddDays(1)));
        Assert.False(law.NosTresMeses(new DateOnly(2031, 8, 1)));

        // Inciso V: dos três meses antes até a posse.
        Assert.False(law.PessoalVedado(new DateOnly(2032, 7, 2), pleito));
        Assert.True(law.PessoalVedado(new DateOnly(2032, 11, 20), pleito));
        Assert.True(law.PessoalVedado(new DateOnly(2032, 12, 31), pleito));
        Assert.False(law.PessoalVedado(new DateOnly(2033, 1, 1), pleito));

        // Inciso VII: no primeiro semestre, empenho de publicidade até 6 vezes a média mensal dos 3 últimos anos.
        Assert.Equal(6, law.PublicidadeVezesAMediaMensal);
        Assert.Equal(3, law.PublicidadeAnosDaMedia);
        Assert.Equal(60_000m, law.TetoPublicidadeNoPrimeiroSemestre(10_000m));

        // CF, art. 14, § 5º: uma única reeleição seguida.
        Assert.Contains("art. 14, § 5º", law.ArtigoReeleicao);
        Assert.Equal(1, law.ReeleicoesSeguidas);
        Assert.True(law.PodeDisputarReeleicao(1));
        Assert.False(law.PodeDisputarReeleicao(2));

        // Art. 77: a pena é a cassação do registro ou do diploma.
        Assert.Contains("registro", InaugurationLawConfig.Default.Pena);
        Assert.Contains("diploma", InaugurationLawConfig.Default.Pena);
    }

    [Fact]
    public void Regras_da_Camara_ainda_nao_conferidas_ficam_marcadas()
    {
        ChamberRulesConfig rules = ChamberRulesConfig.Default;
        Assert.False(rules.ConferidoNoTextoOficial);
        Assert.Equal(3, rules.AssinaturasNecessarias(9));
        Assert.Equal(4, rules.AssinaturasNecessarias(11));
        Assert.Equal(6, rules.VotosParaCassar(9));
        Assert.Equal(8, rules.VotosParaCassar(11));
        Assert.Equal(2, rules.MandatoDaMesaAnos);
    }
}
