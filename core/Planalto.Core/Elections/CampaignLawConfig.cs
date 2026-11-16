namespace Planalto.Core.Elections;

/// <summary>
/// Regras de lei do dinheiro de campanha (Lei 9.504/1997 e Lei 9.096/1995), com o padrão igual ao texto conferido em
/// docs/referencias/LEIS_CONFERIDAS.md (leitura de 16/11/2026 no texto compilado do Planalto). Mudar um valor aqui não
/// exige mexer nas regras do jogo. O que o arquivo de conferência marca como "não aberto" tem um campo dizendo isso.
/// </summary>
public sealed record CampaignFinanceLawConfig
{
    public static CampaignFinanceLawConfig Default { get; } = new();

    public string DataDaConferencia { get; init; } = "2026-11-16";

    // ---- Doação de pessoa física

    public string ArtigoDoacaoPessoaFisica { get; init; } = "Lei 9.504/1997, art. 23, § 1º";

    /// <summary>Teto da doação de pessoa física: fração dos rendimentos brutos do doador no ano anterior.</summary>
    public decimal DoacaoPessoaFisicaFracaoDosRendimentos { get; init; } = 0.10m;

    /// <summary>Multa pelo excesso: até esta fração do valor em excesso (art. 23, § 3º).</summary>
    public decimal MultaDoExcessoFracaoMaxima { get; init; } = 1.00m;

    public decimal LimiteDoacao(decimal rendimentosBrutosAnoAnterior) =>
        Math.Max(0, Math.Floor(rendimentosBrutosAnoAnterior * DoacaoPessoaFisicaFracaoDosRendimentos));

    public decimal MultaMaximaPorExcesso(decimal doado, decimal rendimentosBrutosAnoAnterior) =>
        Math.Max(0, doado - LimiteDoacao(rendimentosBrutosAnoAnterior)) * MultaDoExcessoFracaoMaxima;

    // ---- Recursos próprios

    public string ArtigoRecursosProprios { get; init; } = "Lei 9.504/1997, art. 23, § 2º-A";

    /// <summary>O candidato usa recursos próprios até esta fração do limite de gastos do cargo.</summary>
    public decimal RecursosPropriosFracaoDoLimite { get; init; } = 0.10m;

    public decimal LimiteRecursosProprios(decimal limiteDeGastos) => Math.Floor(limiteDeGastos * RecursosPropriosFracaoDoLimite);

    // ---- Financiamento coletivo

    public string ArtigoFinanciamentoColetivo { get; init; } = "Lei 9.504/1997, art. 22-A, § 3º";

    /// <summary>Arrecadação prévia por financiamento coletivo a partir desta data do ano eleitoral ("MM-dd").</summary>
    public string FinanciamentoColetivoInicio { get; init; } = "05-15";

    /// <summary>O dinheiro arrecadado antes só é liberado com o registro da candidatura (§ 3º).</summary>
    public bool FinanciamentoColetivoLiberadoSoComRegistro { get; init; } = true;

    /// <summary>Sem registro, o valor é devolvido aos doadores (§ 4º).</summary>
    public bool FinanciamentoColetivoDevolvidoSemRegistro { get; init; } = true;

    public string ArtigoRegistro { get; init; } = "Lei 9.504/1997, art. 11";

    /// <summary>Prazo do pedido de registro ("MM-dd", até as 19h).</summary>
    public string RegistroAte { get; init; } = "08-15";

    public DateOnly FinanciamentoColetivoAbre(int anoEleitoral) => Data(anoEleitoral, FinanciamentoColetivoInicio);

    public DateOnly Registro(int anoEleitoral) => Data(anoEleitoral, RegistroAte);

    // ---- Limite de gastos

    public string ArtigoLimiteDeGastos { get; init; } = "Lei 9.504/1997, arts. 18 e 18-C";

    /// <summary>Regra da lei: o limite do cargo no município em 2016, atualizado pelo IPCA; o TSE divulga a tabela.</summary>
    public int AnoBaseDoLimite { get; init; } = 2016;

    public string IndiceDeCorrecaoDoLimite { get; init; } = "IPCA";

    /// <summary>No 2º turno de prefeito, o limite é esta fração do limite do 1º turno (art. 18-C, parágrafo único).</summary>
    public decimal SegundoTurnoFracaoDoPrimeiro { get; init; } = 0.40m;

    /// <summary>
    /// A tabela de limites por município do TSE não foi importada (o site bloqueou o acesso automatizado). Enquanto
    /// for falso, o jogo usa a substituição de data/campaign/finance.json (valor por eleitor, parâmetro de jogo).
    /// </summary>
    public bool TabelaDoTseImportada { get; init; }

    public string LacunaDoLimite { get; init; } = "Tabela de limites de gastos por município (TSE) não aberta; ver docs/referencias/LEIS_CONFERIDAS.md, \"O que não foi aberto\".";

    public decimal LimiteSegundoTurno(decimal limitePrimeiroTurno) => Math.Floor(limitePrimeiroTurno * SegundoTurnoFracaoDoPrimeiro);

    // ---- Representação por captação ou gasto ilícito

    public string ArtigoRepresentacao { get; init; } = "Lei 9.504/1997, art. 30-A";

    /// <summary>Prazo para a representação, em dias contados da diplomação.</summary>
    public int RepresentacaoDiasDaDiplomacao { get; init; } = 15;

    /// <summary>Comprovada a captação ou o gasto ilícito, o diploma é negado ou cassado (§ 2º).</summary>
    public bool RepresentacaoCassaODiploma { get; init; } = true;

    /// <summary>Quem pode representar (caput).</summary>
    public IReadOnlyList<string> RepresentacaoLegitimados { get; init; } = ["partido", "coligacao"];

    public DateOnly RepresentacaoAte(DateOnly diplomacao) => diplomacao.AddDays(RepresentacaoDiasDaDiplomacao);

    // ---- Doação de empresa

    public string ArtigoDoacaoDeEmpresa { get; init; } = "Lei 9.096/1995, art. 31, II; Lei 9.504/1997, art. 81 (revogado pela Lei 13.165/2015)";

    /// <summary>Pessoa jurídica não doa a partido nem a candidato.</summary>
    public bool DoacaoDeEmpresaVedada { get; init; } = true;

    /// <summary>O acórdão da ADI 4650 e a Resolução TSE 23.607/2019 não foram abertos: falta conferir.</summary>
    public bool Adi4650EResolucao23607Conferidas { get; init; }

    // ---- Prestação de contas

    public string ArtigoPrestacaoDeContas { get; init; } = "Lei 9.504/1997, art. 28, § 4º, art. 29, III e IV, e art. 30";

    /// <summary>Prazos conferidos no texto oficial (segunda leitura, 16/11/2026). A Resolução TSE 23.607/2019 não foi aberta.</summary>
    public bool PrazosDaPrestacaoConferidos { get; init; } = true;

    /// <summary>Dinheiro recebido é divulgado em até estas horas do recebimento (art. 28, § 4º, I).</summary>
    public int DivulgacaoDeDoacaoHoras { get; init; } = 72;

    /// <summary>Relatório parcial das contas nesta data ("MM-dd"; art. 28, § 4º, II).</summary>
    public string PrestacaoParcialEm { get; init; } = "09-15";

    /// <summary>Contas finais até este dia depois da eleição (art. 29, III).</summary>
    public int PrestacaoFinalDiasDepoisDaEleicao { get; init; } = 30;

    /// <summary>Havendo 2º turno, até este dia depois dele (art. 29, IV).</summary>
    public int PrestacaoFinalDiasDepoisDoSegundoTurno { get; init; } = 20;

    /// <summary>Os quatro desfechos do julgamento das contas (art. 30).</summary>
    public IReadOnlyList<string> DesfechosDasContas { get; init; } = ["aprovacao", "aprovacao_com_ressalvas", "desaprovacao", "nao_prestadas"];

    /// <summary>Contas não prestadas: só depois de notificação com este prazo, em horas (art. 30, IV).</summary>
    public int ContasNaoPrestadasNotificacaoHoras { get; init; } = 72;

    public DateOnly PrestacaoFinalComSegundoTurno(DateOnly segundoTurno) => segundoTurno.AddDays(PrestacaoFinalDiasDepoisDoSegundoTurno);

    public DateOnly PrestacaoParcial(int anoEleitoral) => Data(anoEleitoral, PrestacaoParcialEm);

    public DateOnly PrestacaoFinal(DateOnly eleicao) => eleicao.AddDays(PrestacaoFinalDiasDepoisDaEleicao);

    private static DateOnly Data(int ano, string mesDia) =>
        DateOnly.ParseExact($"{ano}-{mesDia}", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// Regras de lei da propaganda (Lei 9.504/1997), com o padrão igual ao texto conferido em
/// docs/referencias/LEIS_CONFERIDAS.md: imprensa escrita, rádio e TV, horário gratuito, atos de rua e inauguração.
/// </summary>
public sealed record PropagandaLawConfig
{
    public static PropagandaLawConfig Default { get; } = new();

    public string DataDaConferencia { get; init; } = "2026-11-16";

    // ---- Início da propaganda

    public string ArtigoInicio { get; init; } = "Lei 9.504/1997, art. 36";

    /// <summary>A propaganda é permitida depois desta data ("MM-dd"), isto é, a partir do dia seguinte.</summary>
    public string PropagandaDepoisDe { get; init; } = "08-15";

    public decimal MultaAntecipadaMinima { get; init; } = 5_000m;

    public decimal MultaAntecipadaMaxima { get; init; } = 25_000m;

    public DateOnly PrimeiroDiaDePropaganda(int ano) => Data(ano, PropagandaDepoisDe).AddDays(1);

    // ---- Imprensa escrita

    public string ArtigoImprensaEscrita { get; init; } = "Lei 9.504/1997, art. 43";

    /// <summary>Máximo de anúncios pagos por veículo, por candidato, em datas diversas.</summary>
    public int AnunciosPorVeiculo { get; init; } = 10;

    public bool AnunciosEmDatasDiversas { get; init; } = true;

    /// <summary>Anúncios até a antevéspera da eleição: dias antes do pleito em que saem pela última vez.</summary>
    public int AnuncioUltimoDiaAntesDaEleicao { get; init; } = 2;

    /// <summary>Espaço máximo por edição: 1/8 de página de jornal padrão; 1/4 de revista ou tabloide.</summary>
    public double EspacoMaximoJornal { get; init; } = 1.0 / 8;

    public double EspacoMaximoRevistaOuTabloide { get; init; } = 1.0 / 4;

    /// <summary>O anúncio mostra, de forma visível, o valor pago (§ 1º).</summary>
    public bool AnuncioMostraValorPago { get; init; } = true;

    public DateOnly UltimoDiaDeAnuncio(DateOnly eleicao) => eleicao.AddDays(-AnuncioUltimoDiaAntesDaEleicao);

    /// <summary>O candidato ainda pode anunciar no veículo nesta data, dadas as datas já usadas nele.</summary>
    public bool PodeAnunciar(IReadOnlyCollection<DateOnly> datasNoVeiculo, DateOnly data, DateOnly eleicao) =>
        datasNoVeiculo.Count < AnunciosPorVeiculo
        && data <= UltimoDiaDeAnuncio(eleicao)
        && data >= PrimeiroDiaDePropaganda(eleicao.Year)
        && (!AnunciosEmDatasDiversas || !datasNoVeiculo.Contains(data));

    // ---- Rádio e televisão

    public string ArtigoRadioETv { get; init; } = "Lei 9.504/1997, art. 44 (e art. 36, § 2º)";

    /// <summary>Propaganda em rádio e TV só no horário gratuito; a paga é vedada.</summary>
    public bool RadioETvSoNoHorarioGratuito { get; init; } = true;

    public string ArtigoHorarioGratuito { get; init; } = "Lei 9.504/1997, art. 47";

    /// <summary>O horário gratuito vai ao ar nos dias anteriores à antevéspera da eleição.</summary>
    public int HorarioGratuitoDias { get; init; } = 35;

    /// <summary>Divisão do tempo: proporcional à bancada na Câmara dos Deputados resultante da eleição.</summary>
    public double TempoProporcionalABancada { get; init; } = 0.90;

    /// <summary>Divisão do tempo: parte repartida igualmente.</summary>
    public double TempoIgualitario { get; init; } = 0.10;

    /// <summary>A bancada que conta é a resultante da eleição, não a da data da propaganda.</summary>
    public bool BancadaResultanteDaEleicao { get; init; } = true;

    /// <summary>Em coligação majoritária, soma-se a bancada dos maiores partidos, até este número.</summary>
    public int MaioresPartidosDaColigacao { get; init; } = 6;

    /// <summary>Eleições municipais: minutos diários de inserções.</summary>
    public int InsercoesMinutosDiarios { get; init; } = 70;

    public double InsercoesFracaoPrefeito { get; init; } = 0.60;

    public double InsercoesFracaoVereador { get; init; } = 0.40;

    /// <summary>Blocos só para prefeito: minutos de cada bloco, e os horários (rádio 7h e 12h; TV 13h e 20h30).</summary>
    public int BlocoPrefeitoMinutos { get; init; } = 10;

    public IReadOnlyList<string> BlocosRadio { get; init; } = ["07:00", "12:00"];

    public IReadOnlyList<string> BlocosTv { get; init; } = ["13:00", "20:30"];

    public bool VereadorTemBloco { get; init; }

    public string ArtigoEmissora { get; init; } = "Lei 9.504/1997, art. 47, § 1º-A, e art. 48";

    /// <summary>Inserções só onde há emissora geradora; sem ela, só em município apto a 2º turno com retransmissão viável.</summary>
    public bool SoOndeHaEmissora { get; init; } = true;

    public DateOnly HorarioGratuitoComeca(DateOnly eleicao) => eleicao.AddDays(-2 - HorarioGratuitoDias);

    public DateOnly HorarioGratuitoTermina(DateOnly eleicao) => eleicao.AddDays(-3);

    public bool NoHorarioGratuito(DateOnly data, DateOnly eleicao) => data >= HorarioGratuitoComeca(eleicao) && data <= HorarioGratuitoTermina(eleicao);

    /// <summary>Minutos diários de inserções das candidaturas a vereador (todas juntas) ou a prefeito.</summary>
    public double MinutosDiarios(Office office) =>
        InsercoesMinutosDiarios * (office == Office.Prefeito ? InsercoesFracaoPrefeito : InsercoesFracaoVereador);

    // ---- Atos de rua

    public string ArtigoAtosDeRua { get; init; } = "Lei 9.504/1997, art. 39, §§ 3º, 4º e 9º";

    /// <summary>Alto-falante e carro de som: das 8h às 22h.</summary>
    public int SomHoraInicio { get; init; } = 8;

    public int SomHoraFim { get; init; } = 22;

    /// <summary>Distância mínima, em metros, de hospitais, escolas, igrejas e sedes de Poder.</summary>
    public int SomDistanciaMinimaMetros { get; init; } = 200;

    /// <summary>Comício: das 8h às 24h; o de encerramento pode ir mais estas horas.</summary>
    public int ComicioHoraInicio { get; init; } = 8;

    public int ComicioHoraFim { get; init; } = 24;

    public int ComicioEncerramentoHorasAMais { get; init; } = 2;

    /// <summary>Caminhada, carreata, material gráfico e carro de som: até esta hora da véspera da eleição.</summary>
    public int RuaAteHoraDaVespera { get; init; } = 22;

    public bool SomPermitido(int hora) => hora >= SomHoraInicio && hora < SomHoraFim;

    public bool ComicioPermitido(int hora, bool encerramento = false) =>
        hora >= ComicioHoraInicio && hora < ComicioHoraFim + (encerramento ? ComicioEncerramentoHorasAMais : 0);

    /// <summary>Último dia de ato de rua: a véspera da eleição.</summary>
    public DateOnly UltimoDiaDeRua(DateOnly eleicao) => eleicao.AddDays(-1);

    // ---- Direito de resposta

    public string ArtigoDireitoDeResposta { get; init; } = "Lei 9.504/1997, art. 58, § 1º";

    /// <summary>Prazo para pedir o direito de resposta, em horas, contra ofensa no horário eleitoral gratuito.</summary>
    public int DireitoDeRespostaHorasHorarioGratuito { get; init; } = 24;

    /// <summary>Na programação normal de rádio e TV.</summary>
    public int DireitoDeRespostaHorasRadioETv { get; init; } = 48;

    /// <summary>Na imprensa escrita.</summary>
    public int DireitoDeRespostaHorasImprensaEscrita { get; init; } = 72;

    /// <summary>Na internet: a qualquer tempo enquanto o conteúdo estiver no ar, ou neste prazo depois da retirada.</summary>
    public bool DireitoDeRespostaInternetEnquantoNoAr { get; init; } = true;

    public int DireitoDeRespostaHorasInternetDepoisDaRetirada { get; init; } = 72;

    /// <summary>Prazo, em horas, para pedir o direito de resposta conforme onde a ofensa saiu.</summary>
    public int DireitoDeRespostaHoras(bool emissora, bool escrita, bool horarioGratuito = false) =>
        horarioGratuito ? DireitoDeRespostaHorasHorarioGratuito
        : emissora ? DireitoDeRespostaHorasRadioETv
        : escrita ? DireitoDeRespostaHorasImprensaEscrita
        : DireitoDeRespostaHorasInternetDepoisDaRetirada;

    // ---- Inauguração de obra pública (a R4 usa)

    public string ArtigoInauguracao { get; init; } = "Lei 9.504/1997, art. 77";

    /// <summary>Candidato não comparece a inauguração de obra pública nos meses que antecedem o pleito.</summary>
    public int InauguracaoMesesAntesDoPleito { get; init; } = 3;

    /// <summary>Pena: cassação do registro ou do diploma.</summary>
    public bool InauguracaoCassaRegistroOuDiploma { get; init; } = true;

    public bool InauguracaoVedada(DateOnly data, DateOnly eleicao) => data > eleicao.AddMonths(-InauguracaoMesesAntesDoPleito) && data <= eleicao;

    private static DateOnly Data(int ano, string mesDia) =>
        DateOnly.ParseExact($"{ano}-{mesDia}", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
