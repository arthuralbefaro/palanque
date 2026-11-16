using Planalto.Core.Campaign;
using Planalto.Core.Elections;
using Xunit;

namespace Planalto.Core.Tests.Elections;

/// <summary>
/// Regras de lei da campanha em objetos de configuração: um teste por limite, com o padrão igual ao texto conferido
/// em docs/referencias/LEIS_CONFERIDAS.md (leitura de 16/11/2026), e trocando o valor no objeto a regra muda.
/// </summary>
public class CampaignLawTests
{
    private static readonly CampaignFinanceLawConfig Finance = CampaignFinanceLawConfig.Default;
    private static readonly PropagandaLawConfig Propaganda = PropagandaLawConfig.Default;
    private static readonly DateOnly Election = new(2028, 10, 1);

    [Fact]
    public void Art_23_par_1_doacao_de_pessoa_fisica_ate_10_por_cento_dos_rendimentos()
    {
        Assert.Contains("art. 23, § 1º", Finance.ArtigoDoacaoPessoaFisica);
        Assert.Equal(0.10m, Finance.DoacaoPessoaFisicaFracaoDosRendimentos);
        Assert.Equal(8_000m, Finance.LimiteDoacao(80_000m));
        Assert.Equal(0m, Finance.LimiteDoacao(0m));

        // § 3º: multa de até 100% do excesso.
        Assert.Equal(1.00m, Finance.MultaDoExcessoFracaoMaxima);
        Assert.Equal(2_000m, Finance.MultaMaximaPorExcesso(10_000m, 80_000m));
        Assert.Equal(0m, Finance.MultaMaximaPorExcesso(8_000m, 80_000m));
        Assert.Equal(4_000m, (Finance with { DoacaoPessoaFisicaFracaoDosRendimentos = 0.05m }).LimiteDoacao(80_000m));
    }

    [Fact]
    public void Art_23_par_2A_recursos_proprios_ate_10_por_cento_do_limite_de_gastos()
    {
        Assert.Contains("art. 23, § 2º-A", Finance.ArtigoRecursosProprios);
        Assert.Equal(0.10m, Finance.RecursosPropriosFracaoDoLimite);
        Assert.Equal(12_000m, Finance.LimiteRecursosProprios(120_000m));
        Assert.Equal(24_000m, (Finance with { RecursosPropriosFracaoDoLimite = 0.20m }).LimiteRecursosProprios(120_000m));
    }

    [Fact]
    public void Art_22A_par_3_financiamento_coletivo_desde_15_de_maio_liberado_com_o_registro()
    {
        Assert.Contains("art. 22-A, § 3º", Finance.ArtigoFinanciamentoColetivo);
        Assert.Equal(new DateOnly(2028, 5, 15), Finance.FinanciamentoColetivoAbre(2028));
        Assert.True(Finance.FinanciamentoColetivoLiberadoSoComRegistro);
        Assert.True(Finance.FinanciamentoColetivoDevolvidoSemRegistro);

        // Art. 11: registro até 15 de agosto.
        Assert.Contains("art. 11", Finance.ArtigoRegistro);
        Assert.Equal(new DateOnly(2028, 8, 15), Finance.Registro(2028));
    }

    [Fact]
    public void Arts_18_e_18C_limite_de_gastos_de_2016_corrigido_pelo_IPCA_e_40_por_cento_no_segundo_turno()
    {
        Assert.Contains("18", Finance.ArtigoLimiteDeGastos);
        Assert.Contains("18-C", Finance.ArtigoLimiteDeGastos);
        Assert.Equal(2016, Finance.AnoBaseDoLimite);
        Assert.Equal("IPCA", Finance.IndiceDeCorrecaoDoLimite);
        Assert.Equal(0.40m, Finance.SegundoTurnoFracaoDoPrimeiro);
        Assert.Equal(400_000m, Finance.LimiteSegundoTurno(1_000_000m));

        // A tabela do TSE não foi aberta: o objeto diz que falta, e o jogo usa a substituição marcada de data/.
        Assert.False(Finance.TabelaDoTseImportada);
        Assert.False(string.IsNullOrWhiteSpace(Finance.LacunaDoLimite));
    }

    [Fact]
    public void Art_30A_representacao_em_ate_15_dias_da_diplomacao_cassa_o_diploma()
    {
        Assert.Contains("art. 30-A", Finance.ArtigoRepresentacao);
        Assert.Equal(15, Finance.RepresentacaoDiasDaDiplomacao);
        Assert.True(Finance.RepresentacaoCassaODiploma);
        Assert.Equal(["partido", "coligacao"], Finance.RepresentacaoLegitimados);
        Assert.Equal(new DateOnly(2029, 1, 2), Finance.RepresentacaoAte(new DateOnly(2028, 12, 18)));
    }

    [Fact]
    public void Doacao_de_empresa_e_vedada_e_o_que_nao_foi_aberto_esta_marcado()
    {
        Assert.True(Finance.DoacaoDeEmpresaVedada);
        Assert.Contains("Lei 9.096/1995, art. 31, II", Finance.ArtigoDoacaoDeEmpresa);
        Assert.Contains("13.165/2015", Finance.ArtigoDoacaoDeEmpresa);

        // O acórdão da ADI 4650 e a Resolução TSE 23.607/2019 não foram abertos: falta conferir.
        Assert.False(Finance.Adi4650EResolucao23607Conferidas);

        // Nenhuma fonte de dinheiro do jogo é empresa.
        Assert.DoesNotContain("empresa", string.Join(' ', new[] { FinanceCatalog.Fundo, FinanceCatalog.PessoaFisica, FinanceCatalog.Proprios, FinanceCatalog.Coletivo, FinanceCatalog.PorFora, FinanceCatalog.Jantar }));
    }

    [Fact]
    public void Arts_28_29_e_30_prestacao_de_contas_parcial_em_15_de_setembro_e_final_em_30_dias()
    {
        Assert.Contains("art. 28, § 4º", Finance.ArtigoPrestacaoDeContas);
        Assert.Contains("art. 29", Finance.ArtigoPrestacaoDeContas);
        Assert.Contains("art. 30", Finance.ArtigoPrestacaoDeContas);
        Assert.True(Finance.PrazosDaPrestacaoConferidos);
        Assert.Equal(72, Finance.DivulgacaoDeDoacaoHoras);
        Assert.Equal(new DateOnly(2028, 9, 15), Finance.PrestacaoParcial(2028));
        Assert.Equal(30, Finance.PrestacaoFinalDiasDepoisDaEleicao);
        Assert.Equal(Election.AddDays(30), Finance.PrestacaoFinal(Election));
        Assert.Equal(20, Finance.PrestacaoFinalDiasDepoisDoSegundoTurno);
        Assert.Equal(new DateOnly(2028, 11, 18), Finance.PrestacaoFinalComSegundoTurno(new DateOnly(2028, 10, 29)));

        // Art. 30: quatro desfechos; contas não prestadas só depois de notificação com 72 horas.
        Assert.Equal(["aprovacao", "aprovacao_com_ressalvas", "desaprovacao", "nao_prestadas"], Finance.DesfechosDasContas);
        Assert.Equal(72, Finance.ContasNaoPrestadasNotificacaoHoras);
        Assert.Equal(Finance.DesfechosDasContas.Count, Enum.GetValues<AccountsVerdict>().Length);
    }

    [Fact]
    public void Art_58_par_1_direito_de_resposta_em_24_48_e_72_horas()
    {
        Assert.Contains("art. 58, § 1º", Propaganda.ArtigoDireitoDeResposta);
        Assert.Equal(24, Propaganda.DireitoDeRespostaHorasHorarioGratuito);
        Assert.Equal(48, Propaganda.DireitoDeRespostaHorasRadioETv);
        Assert.Equal(72, Propaganda.DireitoDeRespostaHorasImprensaEscrita);
        Assert.True(Propaganda.DireitoDeRespostaInternetEnquantoNoAr);
        Assert.Equal(72, Propaganda.DireitoDeRespostaHorasInternetDepoisDaRetirada);
        Assert.Equal(24, Propaganda.DireitoDeRespostaHoras(emissora: true, escrita: false, horarioGratuito: true));
        Assert.Equal(48, Propaganda.DireitoDeRespostaHoras(emissora: true, escrita: false));
        Assert.Equal(72, Propaganda.DireitoDeRespostaHoras(emissora: false, escrita: true));
        Assert.Equal(72, Propaganda.DireitoDeRespostaHoras(emissora: false, escrita: false));
    }

    [Fact]
    public void Art_36_propaganda_a_partir_de_16_de_agosto()
    {
        Assert.Contains("art. 36", Propaganda.ArtigoInicio);
        Assert.Equal(new DateOnly(2028, 8, 16), Propaganda.PrimeiroDiaDePropaganda(2028));
        Assert.Equal(5_000m, Propaganda.MultaAntecipadaMinima);
        Assert.Equal(25_000m, Propaganda.MultaAntecipadaMaxima);

        // O início da campanha do jogo (data/campaign/config.json) é o mesmo dia.
        CampaignConfig config = Tests.Campaign.CampaignKit.C.Campaign.Config;
        Assert.Equal(Propaganda.PrimeiroDiaDePropaganda(2028), config.DateIn(2028, config.CampaignStart));
    }

    [Fact]
    public void Art_43_imprensa_escrita_ate_10_anuncios_por_veiculo_em_datas_diversas_ate_a_antevespera()
    {
        Assert.Contains("art. 43", Propaganda.ArtigoImprensaEscrita);
        Assert.Equal(10, Propaganda.AnunciosPorVeiculo);
        Assert.True(Propaganda.AnunciosEmDatasDiversas);
        Assert.Equal(1.0 / 8, Propaganda.EspacoMaximoJornal);
        Assert.Equal(1.0 / 4, Propaganda.EspacoMaximoRevistaOuTabloide);
        Assert.True(Propaganda.AnuncioMostraValorPago);
        Assert.Equal(Election.AddDays(-2), Propaganda.UltimoDiaDeAnuncio(Election));

        var day = new DateOnly(2028, 9, 1);
        List<DateOnly> nine = Enumerable.Range(0, 9).Select(i => day.AddDays(-i - 1)).ToList();
        Assert.True(Propaganda.PodeAnunciar(nine, day, Election));
        Assert.False(Propaganda.PodeAnunciar([.. nine, day.AddDays(-10)], day, Election));          // o 11º
        Assert.False(Propaganda.PodeAnunciar([day], day, Election));                                // mesma data
        Assert.True(Propaganda.PodeAnunciar([], Election.AddDays(-2), Election));                   // antevéspera
        Assert.False(Propaganda.PodeAnunciar([], Election.AddDays(-1), Election));                  // véspera
        Assert.False(Propaganda.PodeAnunciar([], new DateOnly(2028, 8, 15), Election));             // antes da propaganda
        Assert.True((Propaganda with { AnunciosPorVeiculo = 12 }).PodeAnunciar([.. nine, day.AddDays(-10)], day, Election));
    }

    [Fact]
    public void Art_44_radio_e_tv_so_no_horario_gratuito()
    {
        Assert.Contains("art. 44", Propaganda.ArtigoRadioETv);
        Assert.True(Propaganda.RadioETvSoNoHorarioGratuito);
        ChannelLawConfig channels = ChannelLawConfig.Default;
        Assert.False(channels.GastoPermitido(ChannelLawConfig.Radio));
        Assert.False(channels.GastoPermitido(ChannelLawConfig.Tv));
        Assert.Contains("art. 44", channels.GastosVedados[ChannelLawConfig.Radio]);
    }

    [Fact]
    public void Art_47_horario_gratuito_35_dias_90_por_cento_pela_bancada_da_eleicao_e_10_igual()
    {
        Assert.Contains("art. 47", Propaganda.ArtigoHorarioGratuito);
        Assert.Equal(35, Propaganda.HorarioGratuitoDias);
        Assert.Equal(0.90, Propaganda.TempoProporcionalABancada);
        Assert.Equal(0.10, Propaganda.TempoIgualitario);
        Assert.True(Propaganda.BancadaResultanteDaEleicao);
        Assert.Equal(6, Propaganda.MaioresPartidosDaColigacao);

        // Os 35 dias anteriores à antevéspera.
        Assert.Equal(Election.AddDays(-37), Propaganda.HorarioGratuitoComeca(Election));
        Assert.Equal(Election.AddDays(-3), Propaganda.HorarioGratuitoTermina(Election));
        Assert.True(Propaganda.NoHorarioGratuito(Election.AddDays(-37), Election));
        Assert.False(Propaganda.NoHorarioGratuito(Election.AddDays(-38), Election));
        Assert.False(Propaganda.NoHorarioGratuito(Election.AddDays(-2), Election));

        // Municipais: 70 minutos diários de inserções, 60% prefeito e 40% vereador; bloco só de prefeito (10 minutos).
        Assert.Equal(70, Propaganda.InsercoesMinutosDiarios);
        Assert.Equal(42, Propaganda.MinutosDiarios(Office.Prefeito), 9);
        Assert.Equal(28, Propaganda.MinutosDiarios(Office.Vereador), 9);
        Assert.Equal(10, Propaganda.BlocoPrefeitoMinutos);
        Assert.Equal(["07:00", "12:00"], Propaganda.BlocosRadio);
        Assert.Equal(["13:00", "20:30"], Propaganda.BlocosTv);
        Assert.False(Propaganda.VereadorTemBloco);

        // § 1º-A e art. 48: só onde há emissora.
        Assert.True(Propaganda.SoOndeHaEmissora);
        Assert.Contains("art. 48", Propaganda.ArtigoEmissora);

        // O mesmo 90/10 no rateio do tempo.
        PartyFundingRulesConfig funding = PartyFundingRulesConfig.Default;
        Assert.Equal(Propaganda.TempoProporcionalABancada, funding.TvCamaraSeatsShare);
        Assert.Equal(Propaganda.TempoIgualitario, funding.TvEqualShare);
    }

    [Fact]
    public void Art_57C_impulsionamento_identificado_so_para_promover_com_multa_de_5_a_30_mil_ou_o_dobro()
    {
        ChannelLawConfig law = ChannelLawConfig.Default;
        Assert.Contains("57-C", law.ArtigoImpulsionamento);
        Assert.True(law.ConferidoNoTextoOficial);
        Assert.True(law.ImpulsionamentoIdentificadoEContratadoPelaCampanha);
        Assert.True(law.ImpulsionamentoSoParaPromover);
        Assert.True(law.ImpulsionamentoProvedorNoPais);
        Assert.Equal(5_000m, law.ImpulsionamentoMultaMinima);
        Assert.Equal(30_000m, law.ImpulsionamentoMultaMaxima);
        Assert.Equal(30_000m, law.ImpulsionamentoMultaMaximaPara(10_000m));
        Assert.Equal(50_000m, law.ImpulsionamentoMultaMaximaPara(25_000m));

        // Atacar nas redes é publicação comum, não impulsionamento: o canal de ataque não é o canal de verba.
        Assert.DoesNotContain(Tests.Campaign.CampaignKit.C.Campaign.Attacks.Canais, c => c.Id == ChannelDefinition.Redes && c.Custo > 1000);
    }

    [Theory]
    [InlineData(Office.Prefeito, 20_000, 200)]
    [InlineData(Office.Prefeito, 30_000, 300)]
    [InlineData(Office.Prefeito, 50_000, 320)]
    [InlineData(Office.Vereador, 20_000, 100)]
    [InlineData(Office.Vereador, 50_000, 160)]
    [InlineData(Office.Vereador, 10_150, 51)]     // 101,5 → 102 (prefeito); 51 (vereador)
    [InlineData(Office.Prefeito, 10_149, 101)]    // 101,49 → 101
    public void Art_100A_cabos_1_por_cento_ate_30_mil_eleitores_depois_1_por_mil_e_vereador_metade(Office office, long eleitorado, int expected)
    {
        ChannelLawConfig law = ChannelLawConfig.Default;
        Assert.Contains("100-A", law.ArtigoCabos);
        Assert.Equal(30_000, law.EleitoradoFaixaBase);
        Assert.Equal(0.01, law.FracaoAteFaixaBase);
        Assert.Equal(1_000, law.EleitoresPorContratacaoExtra);
        Assert.Equal(1.0, law.FracaoPorCargo[Office.Prefeito]);
        Assert.Equal(0.5, law.FracaoPorCargo[Office.Vereador]);
        Assert.Equal(0.8, law.TetoVereadorSobreDeputadoEstadual);
        Assert.Equal(expected, law.LimiteCabos(office, eleitorado));
        Assert.Contains("fiscais", law.ForaDoLimite);
        Assert.Contains("delegados", law.ForaDoLimite);
        Assert.Contains("advogados", law.ForaDoLimite);

        // § 1º: deputado federal 70% do limite do município de maior eleitorado; estadual 50% do federal (teto do vereador).
        Assert.True(law.FracoesDeDeputadoConferidas);
        Assert.Equal(0.7, law.FracaoDeputadoFederal);
        Assert.Equal(0.5, law.FracaoDeputadoEstadual);

        // § 2º: fração abaixo de 0,5 é desprezada; a partir de 0,5 vale 1.
        Assert.Equal(80, law.LimiteMunicipio(8_049));
        Assert.Equal(81, law.LimiteMunicipio(8_050));
    }

    [Fact]
    public void Art_39_som_das_8_as_22_comicio_das_8_as_24_e_rua_ate_a_vespera()
    {
        Assert.Contains("art. 39", Propaganda.ArtigoAtosDeRua);
        Assert.Equal(8, Propaganda.SomHoraInicio);
        Assert.Equal(22, Propaganda.SomHoraFim);
        Assert.Equal(200, Propaganda.SomDistanciaMinimaMetros);
        Assert.True(Propaganda.SomPermitido(8));
        Assert.True(Propaganda.SomPermitido(21));
        Assert.False(Propaganda.SomPermitido(22));
        Assert.False(Propaganda.SomPermitido(7));

        Assert.Equal(8, Propaganda.ComicioHoraInicio);
        Assert.Equal(24, Propaganda.ComicioHoraFim);
        Assert.Equal(2, Propaganda.ComicioEncerramentoHorasAMais);
        Assert.True(Propaganda.ComicioPermitido(23));
        Assert.False(Propaganda.ComicioPermitido(24));
        Assert.True(Propaganda.ComicioPermitido(25, encerramento: true));
        Assert.False(Propaganda.ComicioPermitido(26, encerramento: true));

        // § 9º: caminhada, carreata e carro de som até as 22h da véspera.
        Assert.Equal(22, Propaganda.RuaAteHoraDaVespera);
        Assert.Equal(Election.AddDays(-1), Propaganda.UltimoDiaDeRua(Election));

        // Brinde e outdoor continuam vedados (§§ 6º e 8º).
        ChannelLawConfig channels = ChannelLawConfig.Default;
        Assert.False(channels.GastoPermitido(ChannelLawConfig.Brinde));
        Assert.False(channels.GastoPermitido(ChannelLawConfig.Outdoor));
    }

    [Fact]
    public void Art_77_inauguracao_vedada_nos_3_meses_antes_do_pleito()
    {
        Assert.Contains("art. 77", Propaganda.ArtigoInauguracao);
        Assert.Equal(3, Propaganda.InauguracaoMesesAntesDoPleito);
        Assert.True(Propaganda.InauguracaoCassaRegistroOuDiploma);
        Assert.False(Propaganda.InauguracaoVedada(new DateOnly(2028, 7, 1), Election));
        Assert.True(Propaganda.InauguracaoVedada(new DateOnly(2028, 7, 2), Election));
        Assert.True(Propaganda.InauguracaoVedada(Election, Election));
        Assert.False(Propaganda.InauguracaoVedada(Election.AddDays(1), Election));
    }
}

/// <summary>
/// Sobras eleitorais no modelo de três fases (Código Eleitoral, arts. 106 a 109, com o efeito das ADIs 7228, 7263 e
/// 7325, lido em notícias oficiais do STF): um teste de cada fase, e a regra da fase 2 trocável no objeto.
/// </summary>
public class SobrasTresFasesTests
{
    private static readonly DateOnly Born = new(1980, 1, 1);

    private static PartyList Party(string id, params long[] votes) =>
        new(id, 0, votes.Select((v, i) => new CandidateVotes($"{id}{i + 1}", v, Born.AddDays(i))).ToList());

    [Fact]
    public void O_padrao_e_o_modelo_de_tres_fases_e_a_fase_2_esta_marcada_como_a_confirmar()
    {
        ProportionalRulesConfig rules = ProportionalRulesConfig.Default;
        Assert.Equal(10, rules.CandidateMinimumPercent);
        Assert.Equal(80, rules.PartyRemainderMinimumPercent);
        Assert.Equal(20, rules.CandidateRemainderMinimumPercent);
        Assert.True(rules.FinalStageOpenToAllParties);
        Assert.False(rules.Fase2ConferidaNoAcordao);
        Assert.Contains("7228", rules.Fonte);
    }

    [Fact]
    public void Fase_1_quociente_partidario_so_com_candidato_de_10_por_cento_do_quociente()
    {
        // 1.000 votos, 2 vagas: quociente 500. A tem QP 1 e elege a1 (600 ≥ 50). B não atinge o quociente.
        ProportionalResult result = ElectionRules.Proportional(2, [Party("a", 600, 30), Party("b", 370)]);
        Assert.Equal(500, result.ElectoralQuotient);
        ElectedCandidate first = result.Elected[0];
        Assert.Equal("a1", first.CandidateId);
        Assert.Equal(SeatStage.PartyQuotient, first.Stage);

        // Com QP, mas sem candidato de 10% do quociente, a vaga não é ocupada nesta fase.
        ProportionalResult weak = ElectionRules.Proportional(2, [new PartyList("a", 900, [new CandidateVotes("a1", 40, Born)]), Party("b", 400)]);
        Assert.Equal(670, weak.ElectoralQuotient);
        Assert.Equal(1, weak.PartyQuotients["a"]);
        Assert.DoesNotContain(weak.Elected, e => e.Stage == SeatStage.PartyQuotient);
    }

    [Fact]
    public void Fase_2_sobras_so_entre_partidos_com_80_por_cento_e_candidatos_com_20_por_cento()
    {
        // 1.120 votos, 2 vagas: quociente 560. A (750) passa dos 80% (448); B (370) não. a2 tem 150 ≥ 112 (20%).
        ProportionalResult result = ElectionRules.Proportional(2, [Party("a", 600, 150), Party("b", 370)]);
        Assert.Equal(560, result.ElectoralQuotient);
        Assert.Equal(["a1", "a2"], result.Elected.Select(e => e.CandidateId));
        Assert.Equal(SeatStage.Remainder, result.Elected[1].Stage);

        // Sem a cláusula de 80% (regra trocada no objeto), B disputa a sobra: 370 contra a média de A (750 ÷ 2 = 375).
        var open = ProportionalRulesConfig.Default with { PartyRemainderMinimumPercent = 0 };
        Assert.Equal("a2", ElectionRules.Proportional(2, [Party("a", 600, 150), Party("b", 370)], open).Elected[1].CandidateId);
        Assert.Equal("b1", ElectionRules.Proportional(2, [Party("a", 600, 100), Party("b", 420)], open).Elected[1].CandidateId);

        // Sem o piso de 20% do candidato, a2 ocupa a sobra mesmo com poucos votos.
        var noFloor = ProportionalRulesConfig.Default with { CandidateRemainderMinimumPercent = 0 };
        ProportionalResult low = ElectionRules.Proportional(2, [Party("a", 600, 30), Party("b", 370)], noFloor);
        Assert.Equal("a2", low.Elected[1].CandidateId);
        Assert.Equal(SeatStage.Remainder, low.Elected[1].Stage);
    }

    [Fact]
    public void Fase_3_vagas_restantes_pelas_maiores_medias_entre_todos_os_partidos()
    {
        // A tem a vaga do quociente; na fase 2 só A disputa, mas a2 (30 votos) não tem 20% do quociente (100).
        // Na fase 3 entram todos: a média de B (370) supera a de A (630 ÷ 2 = 315), e b1 leva a vaga.
        ProportionalResult result = ElectionRules.Proportional(2, [Party("a", 600, 30), Party("b", 370)]);
        Assert.Equal(["a1", "b1"], result.Elected.Select(e => e.CandidateId));
        Assert.Equal(SeatStage.FinalRemainder, result.Elected[1].Stage);
        Assert.Equal(0, result.VacantSeats);

        // Com a fase 3 fechada aos partidos sem 80% do quociente (texto do art. 109 antes da decisão), a vaga fica com A.
        var closed = ProportionalRulesConfig.Default with { FinalStageOpenToAllParties = false };
        ProportionalResult old = ElectionRules.Proportional(2, [Party("a", 600, 30), Party("b", 370)], closed);
        Assert.Equal(["a1", "a2"], old.Elected.Select(e => e.CandidateId));
        Assert.Equal(SeatStage.FinalRemainder, old.Elected[1].Stage);
    }
}
