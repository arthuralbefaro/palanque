using System.Text.Json.Serialization;
using Planalto.Core.Campaign;
using Planalto.Core.State;

namespace Planalto.Core.Renewal;

/// <summary>Papel de uma pessoa na política do município; uma pessoa pode ter mais de um.</summary>
public enum CivicRole
{
    Vereador,
    Prefeito,
    VicePrefeito,
    Suplente,
    Lideranca,
    Equipe,

    /// <summary>Secretário municipal ou assessor de gabinete do governo acompanhado de perto (GDD 17.14 e 17.15).</summary>
    Governo,
}

/// <summary>Como alguém saiu da política local (GDD 17.16, "Saídas").</summary>
public enum ExitKind
{
    Morte,
    Aposentadoria,
    Desistencia,
    Mudanca,
    Renuncia,
    Cassacao,
    JusticaEleitoral,
    Derrota,

    /// <summary>Prefeito no segundo mandato seguido, sem direito a nova reeleição.</summary>
    FimDeMandato,
}

public enum RenewalEventKind
{
    Morte,
    Renuncia,
    Cassacao,
    JusticaEleitoral,

    /// <summary>Vereador que deixa a cadeira para assumir a Prefeitura.</summary>
    Investidura,
    Aposentadoria,
    Desistencia,
    Mudanca,
    PosseSuplente,
    PosseVice,
    Interino,
    EleicaoConvocada,
    EleicaoResolvida,
    CadeiraVaga,
    Estreia,
    EleicaoMunicipal,
    PosseLegislatura,
    NovaLideranca,
    NovoNaEquipe,
}

/// <summary>O que a regra de substituição determinou depois de uma saída.</summary>
public enum SuccessionOutcome
{
    /// <summary>A saída não abre vaga com regra de substituição (liderança, equipe, suplente, anúncio de aposentadoria).</summary>
    Nenhum,
    SuplenteAssumiu,
    ViceAssumiu,
    EleicaoDireta,
    EleicaoIndireta,
    EleicaoParaACadeira,
    CadeiraVagaAteOFim,

    /// <summary>O mandato acaba antes do prazo da nova eleição: o presidente da Câmara responde pela Prefeitura até a posse.</summary>
    InterinoAteOFim,

    /// <summary>O cargo de vice não tem substituto: fica vago.</summary>
    ViceFicaVago,
}

/// <summary>Fato da renovação: vai para o histórico, vira notícia e é o que os testes conferem contra a regra.</summary>
public sealed class RenewalEvent
{
    public required int Id { get; init; }

    public required DateOnly Data { get; init; }

    public required RenewalEventKind Tipo { get; init; }

    public CivicRole? Papel { get; init; }

    public string? PessoaId { get; init; }

    public string? SucessorId { get; set; }

    public VacancyCause? Causa { get; init; }

    public SuccessionOutcome Desfecho { get; set; }

    /// <summary>Dispositivo que fundamenta o desfecho.</summary>
    public string? Regra { get; set; }

    /// <summary>Número da cadeira, quando o fato é de uma cadeira da Câmara.</summary>
    public int? Cadeira { get; init; }

    /// <summary>Data da eleição convocada, quando há.</summary>
    public DateOnly? EleicaoEm { get; set; }

    /// <summary>Quem guardou lembrança de luto (ids de pessoas da cidade).</summary>
    public List<string> Enlutados { get; init; } = [];

    public required string Texto { get; init; }
}

/// <summary>Cadeira da Câmara Municipal.</summary>
public sealed class Seat
{
    public required int Numero { get; init; }

    public required string PartyId { get; set; }

    /// <summary>Quem ocupa; null = vaga.</summary>
    public string? TitularId { get; set; }

    /// <summary>Votos de quem ocupa (ordem de liderança e de presidência).</summary>
    public long Votos { get; set; }

    public DateOnly PosseEm { get; set; }

    public DateOnly? VagaDesde { get; set; }

    /// <summary>Último dia em que a cadeira pode seguir vaga pela regra: a posse do suplente, a eleição da cadeira ou o fim do mandato.</summary>
    public DateOnly? VagaPrazo { get; set; }

    public CouncilSuccession? VagaDestino { get; set; }
}

/// <summary>Eleição fora de época, convocada por vacância.</summary>
public sealed class PendingElection
{
    public required int Id { get; init; }

    /// <summary>Prefeito, ou Vereador para a eleição de uma cadeira sem suplente.</summary>
    public required CivicRole Cargo { get; init; }

    public int? Cadeira { get; init; }

    public required bool Indireta { get; init; }

    public required DateOnly Data { get; init; }

    public required DateOnly ConvocadaEm { get; init; }

    public required VacancyCause Causa { get; init; }

    public required string Regra { get; init; }
}

public sealed class ElectedSeat
{
    public required string PessoaId { get; init; }

    public required string PartyId { get; init; }

    public required long Votos { get; init; }
}

/// <summary>Resultado de uma eleição municipal à espera da posse em 1º de janeiro.</summary>
public sealed class ElectedSlate
{
    public required int AnoEleicao { get; init; }

    /// <summary>True quando veio da eleição jogada (apuração da campanha); false na eleição automática.</summary>
    public required bool Jogada { get; init; }

    public required List<ElectedSeat> Vereadores { get; init; }

    /// <summary>Suplentes por partido, na ordem de votação.</summary>
    public required Dictionary<string, List<string>> Suplentes { get; init; }

    public string? PrefeitoId { get; init; }

    public string? PrefeitoPartyId { get; init; }

    public string? ViceId { get; init; }

    public string? VicePartyId { get; init; }

    public int PrefeitoMandatos { get; init; } = 1;
}

/// <summary>Retrato de uma legislatura na posse (critério informativo do GDD 17.16).</summary>
public sealed class LegislatureRecord
{
    public required int Numero { get; init; }

    public required DateOnly Inicio { get; init; }

    public required int Cadeiras { get; init; }

    /// <summary>Idade média dos vereadores no dia da posse, em anos.</summary>
    public required double IdadeMedia { get; init; }

    /// <summary>Quem já ocupava cadeira ao fim da legislatura anterior.</summary>
    public required int Reeleitos { get; init; }

    /// <summary>Quem nunca tinha tido mandato.</summary>
    public required int Estreantes { get; init; }

    /// <summary>Não reeleitos ÷ cadeiras, em %.</summary>
    public required double RenovacaoPct { get; init; }

    public required bool Automatica { get; init; }
}

/// <summary>O que a renovação sabe de cada pessoa além do personagem: saúde, histórico eleitoral e saída.</summary>
public sealed class PersonStatus
{
    public required string Id { get; init; }

    /// <summary>Saúde (0 a 100), usada no fator pessoal de mortalidade.</summary>
    public required double Saude { get; set; }

    public int Derrotas { get; set; }

    /// <summary>Mandatos de vereador ou prefeito já iniciados.</summary>
    public int Mandatos { get; set; }

    /// <summary>Origem de quem estreou (id de data/demography/entries.json).</summary>
    public string? Origem { get; set; }

    /// <summary>De quem é parente, na origem "familia".</summary>
    public string? ParenteDe { get; init; }

    /// <summary>Tábua de mortalidade usada no último sorteio: a UF ou "BR".</summary>
    public string? TabuaUsada { get; set; }

    /// <summary>Anunciou que não disputa a próxima eleição.</summary>
    public ExitKind? NaoDisputa { get; set; }

    public ExitKind? Saida { get; set; }

    public DateOnly? SaidaEm { get; set; }
}

/// <summary>Lugar de liderança sem dono, à espera de sucessor.</summary>
public sealed class LeaderVacancy
{
    public required DateOnly Desde { get; init; }

    public required string AntecessorId { get; init; }

    public required string Type { get; init; }

    public required List<string> Zones { get; init; }

    public required string Publico { get; init; }

    public required string Theme { get; init; }

    public required string RequestId { get; init; }

    public required string RequestText { get; init; }

    public required int Price { get; init; }

    public required int Influence { get; init; }
}

/// <summary>Vaga no mercado de equipe de campanha deixada por quem saiu.</summary>
public sealed class StaffVacancy
{
    public required DateOnly Desde { get; init; }

    public required string Role { get; init; }

    public required decimal Salary { get; init; }
}

/// <summary>
/// Renovação do mundo político do município em foco (GDD 17.16): cadeiras da Câmara e suplentes, prefeito e vice,
/// eleições pendentes, saúde e saída de cada pessoa, histórico e notícias. Tem gerador próprio, para o mundo andar
/// sem mexer na sequência de sorteios da campanha.
/// </summary>
public sealed class RenewalState
{
    public required int Municipio { get; init; }

    public required string Uf { get; init; }

    public required Rng Rng { get; init; }

    /// <summary>Segunda-feira da última semana simulada.</summary>
    public required DateOnly UltimoPasso { get; set; }

    /// <summary>Mês do último sorteio mensal (ano × 12 + mês).</summary>
    public required int UltimoMes { get; set; }

    public required DateOnly MandatoInicio { get; set; }

    /// <summary>Último dia do mandato em curso (31 de dezembro).</summary>
    public required DateOnly MandatoFim { get; set; }

    public int Legislatura { get; set; } = 1;

    public required int UltimaEleicaoAno { get; set; }

    public List<Seat> Cadeiras { get; init; } = [];

    /// <summary>Suplentes por partido, na ordem em que são chamados.</summary>
    public Dictionary<string, List<string>> Suplentes { get; set; } = [];

    public string? PrefeitoId { get; set; }

    public string? PrefeitoPartyId { get; set; }

    /// <summary>1 no primeiro mandato, 2 na reeleição (sem direito a outra).</summary>
    public int PrefeitoMandatos { get; set; } = 1;

    public string? ViceId { get; set; }

    public string? VicePartyId { get; set; }

    public string? PresidenteCamaraId { get; set; }

    /// <summary>Prefeitura sem prefeito nem vice desde esta data; o presidente da Câmara responde por ela.</summary>
    public DateOnly? ExecutivoVagoDesde { get; set; }

    public List<PendingElection> EleicoesPendentes { get; init; } = [];

    public ElectedSlate? Eleitos { get; set; }

    public List<PersonStatus> Pessoas { get; init; } = [];

    public List<LeaderVacancy> LiderancasVagas { get; init; } = [];

    public List<StaffVacancy> EquipeVagas { get; init; } = [];

    public List<RenewalEvent> Historico { get; init; } = [];

    public List<LegislatureRecord> Legislaturas { get; init; } = [];

    /// <summary>Notícias da renovação, no formato do feed da campanha.</summary>
    public List<FeedItem> Noticias { get; init; } = [];

    /// <summary>Luto oficial até esta data (sem sessão na Câmara).</summary>
    public DateOnly? LutoAte { get; set; }

    public int NextEventId { get; set; } = 1;

    public int NextSerial { get; set; } = 1;

    public Seat? SeatOf(string personId) => Cadeiras.FirstOrDefault(c => c.TitularId == personId);

    public PersonStatus? Status(string personId) => Pessoas.FirstOrDefault(p => p.Id == personId);

    [JsonIgnore]
    public IEnumerable<string> Titulares => Cadeiras.Where(c => c.TitularId is not null).Select(c => c.TitularId!);
}
