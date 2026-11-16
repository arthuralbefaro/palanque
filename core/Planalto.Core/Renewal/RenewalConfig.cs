using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Data;
using Planalto.Core.Demography;
using Planalto.Core.People;

namespace Planalto.Core.Renewal;

public sealed class RenewalHealthConfig
{
    public required IntRange Inicial { get; init; }

    /// <summary>Idade a partir da qual a saúde cai todo ano.</summary>
    public required int IdadeDaPerda { get; init; }

    public required double PerdaAnual { get; init; }

    /// <summary>Saúde abaixo da qual quem tem mandato renuncia por doença.</summary>
    public required int LimiteRenuncia { get; init; }
}

public sealed class RetirementConfig
{
    public required int Idade { get; init; }

    public required double ChanceBase { get; init; }

    public required double ChancePorAno { get; init; }

    public required double ChancePorDerrota { get; init; }

    public required int IdadeMaxima { get; init; }

    /// <summary>Chance de não disputar a próxima eleição, pela idade e pelas derrotas.</summary>
    public double Chance(int idade, int derrotas) => idade >= IdadeMaxima
        ? 1
        : idade < Idade ? 0 : Math.Clamp(ChanceBase + (ChancePorAno * (idade - Idade)) + (ChancePorDerrota * derrotas), 0, 1);
}

/// <summary>Eleição municipal automática, usada quando o jogador não está em campanha.</summary>
public sealed class AutomaticElectionConfig
{
    public required double BasePartido { get; init; }

    public required double DispersaoPartidos { get; init; }

    public required double PesoIncumbente { get; init; }

    public required double PesoSuplente { get; init; }

    public required double DispersaoCandidatos { get; init; }

    public required double CandidatosPorCadeira { get; init; }

    public required double ChanceReeleicaoPrefeito { get; init; }

    /// <summary>Peso de quem responde pela Prefeitura numa nova eleição direta.</summary>
    public required double PesoInterino { get; init; }

    /// <summary>Fração do eleitorado que dá voto válido; só dá escala aos votos de cada eleito.</summary>
    public required double Comparecimento { get; init; }
}

public sealed class SubstitutesConfig
{
    public required int MinimoPorPartido { get; init; }

    public required double PorCadeira { get; init; }

    public int For(int seats) => Math.Max(MinimoPorPartido, (int)Math.Ceiling(seats * PorCadeira));
}

/// <summary>Chances por ano das saídas entre eleições.</summary>
public sealed class AnnualExitsConfig
{
    public required double RenunciaVereador { get; init; }

    public required double CassacaoVereador { get; init; }

    public required double RenunciaPrefeito { get; init; }

    public required double CassacaoPrefeito { get; init; }

    public required double JusticaEleitoralPrefeito { get; init; }

    public required double MudancaLideranca { get; init; }

    public required double MudancaEquipe { get; init; }

    public IEnumerable<double> All => [RenunciaVereador, CassacaoVereador, RenunciaPrefeito, CassacaoPrefeito, JusticaEleitoralPrefeito, MudancaLideranca, MudancaEquipe];
}

public sealed class LeaderRenewalConfig
{
    public required int IdadeAposentadoria { get; init; }

    public required double ChanceAposentadoriaAnual { get; init; }

    public required double ChanceMensalSucessor { get; init; }

    public required IntRange IdadeSucessor { get; init; }

    /// <summary>Quanto de influência (1 a 5) o sucessor pode ter a menos que o antecessor.</summary>
    public required int PerdaDeInfluenciaMax { get; init; }
}

public sealed class StaffRenewalConfig
{
    public required double ChanceMensalNovato { get; init; }

    public required IntRange IdadeNovato { get; init; }
}

/// <summary>Quem já está no mandato quando o mundo começa, sem eleição jogada.</summary>
public sealed class InitialWorldConfig
{
    /// <summary>Chance de já ter cumprido mais um mandato antes (sorteada em sequência).</summary>
    public required double ChanceMandatoAnterior { get; init; }

    public required int MandatosAnterioresMax { get; init; }
}

public sealed class MourningConfig
{
    /// <summary>Dias de luto oficial (sem sessão na Câmara).</summary>
    public required int DiasOficiais { get; init; }

    /// <summary>Peso (1 a 3) da lembrança do luto em quem tinha vínculo.</summary>
    public required int PesoLembranca { get; init; }
}

public sealed class RenewalDeadlines
{
    /// <summary>Dias para o suplente tomar posse; parâmetro de jogo (depende do regimento de cada Câmara).</summary>
    public required int PosseSuplenteDias { get; init; }

    /// <summary>Dias até a eleição da cadeira sem suplente; parâmetro de jogo.</summary>
    public required int EleicaoCadeiraDias { get; init; }
}

/// <summary>data/demography/renewal.json: parâmetros de jogo da renovação do mundo político (GDD 17.16).</summary>
public sealed class RenewalConfig : IValidatable
{
    public const string File = "demography/renewal.json";

    public static readonly IReadOnlyList<string> Papeis = ["vereador", "prefeito", "vice_prefeito", "suplente", "lideranca", "equipe"];

    public static readonly IReadOnlyList<string> TextKeys =
    [
        "morte_vereador", "morte_prefeito", "morte_vice_prefeito", "morte_suplente", "morte_lideranca", "morte_equipe", "lembranca_luto",
        "posse_suplente", "posse_vice", "interino", "interino_ate_o_fim", "eleicao_direta_convocada", "eleicao_indireta_convocada",
        "eleicao_direta_resultado", "eleicao_indireta_resultado", "renuncia_vereador", "renuncia_prefeito", "renuncia_vice_prefeito",
        "cassacao_vereador", "cassacao_prefeito", "cassacao_vice_prefeito", "justica_eleitoral", "investidura", "aposentadoria", "desistencia",
        "aposentadoria_lideranca", "mudanca_lideranca", "nova_lideranca", "mudanca_equipe", "novo_equipe", "estreia", "eleicao_municipal",
        "posse_legislatura", "cadeira_vaga", "eleicao_cadeira_convocada", "eleicao_cadeira_resultado",
    ];

    public required string Note { get; init; }

    public required RenewalHealthConfig Saude { get; init; }

    public required Dictionary<string, int> EstressePorPapel { get; init; }

    public required IntRange IdadeEntrada { get; init; }

    public required RetirementConfig Aposentadoria { get; init; }

    public required double DesistenciaPorEleicao { get; init; }

    public required AutomaticElectionConfig Eleicao { get; init; }

    public required SubstitutesConfig Suplentes { get; init; }

    public required AnnualExitsConfig SaidasAnuais { get; init; }

    public required LeaderRenewalConfig Lideranca { get; init; }

    public required InitialWorldConfig MundoInicial { get; init; }

    public required StaffRenewalConfig Equipe { get; init; }

    public required MourningConfig Luto { get; init; }

    public required RenewalDeadlines Prazos { get; init; }

    public required int HistoricoMaximo { get; init; }

    public required int NoticiasMaximo { get; init; }

    public required Dictionary<string, string> Textos { get; init; }

    public string Texto(string key) => Textos.TryGetValue(key, out string? text) ? text : key;

    public IEnumerable<string> Validate()
    {
        foreach (string papel in Papeis.Where(p => !EstressePorPapel.TryGetValue(p, out int v) || v is < 0 or > 100))
        {
            yield return $"estresse_por_papel.{papel}: de 0 a 100.";
        }

        foreach (string key in TextKeys.Where(k => !Textos.TryGetValue(k, out string? t) || string.IsNullOrWhiteSpace(t)))
        {
            yield return $"textos: falta '{key}'.";
        }

        if (Saude.Inicial.Min < 1 || Saude.Inicial.Max > 100 || Saude.Inicial.Min > Saude.Inicial.Max || Saude.PerdaAnual < 0)
        {
            yield return "saude: inicial entre 1 e 100 e perda não negativa.";
        }

        if (IdadeEntrada.Min < 18 || IdadeEntrada.Max < IdadeEntrada.Min)
        {
            yield return "idade_entrada: mínimo de 18 anos e máximo maior ou igual ao mínimo.";
        }

        if (Aposentadoria.IdadeMaxima <= Aposentadoria.Idade || Aposentadoria.ChanceBase < 0 || Aposentadoria.ChancePorAno < 0 || Aposentadoria.ChancePorDerrota < 0)
        {
            yield return "aposentadoria: idade_maxima acima de idade e chances não negativas.";
        }

        if (new[] { DesistenciaPorEleicao, Eleicao.ChanceReeleicaoPrefeito, Eleicao.Comparecimento, Lideranca.ChanceAposentadoriaAnual, Lideranca.ChanceMensalSucessor, Equipe.ChanceMensalNovato }
            .Concat(SaidasAnuais.All).Any(p => p is < 0 or > 1))
        {
            yield return "chances devem estar entre 0 e 1.";
        }

        if (MundoInicial.ChanceMandatoAnterior is < 0 or >= 1 || MundoInicial.MandatosAnterioresMax < 0 || Lideranca.PerdaDeInfluenciaMax < 0 || Eleicao.PesoInterino <= 0)
        {
            yield return "mundo_inicial: chance menor que 1 e máximo não negativo; perda_de_influencia_max não negativa; peso_interino positivo.";
        }

        if (Eleicao.PesoIncumbente <= 0 || Eleicao.PesoSuplente <= 0 || Eleicao.BasePartido <= 0 || Eleicao.CandidatosPorCadeira < 1 || Eleicao.DispersaoPartidos < 0 || Eleicao.DispersaoCandidatos < 0)
        {
            yield return "eleicao: pesos e base positivos, candidatos_por_cadeira de 1 ou mais e dispersões não negativas.";
        }

        if (Suplentes.MinimoPorPartido < 0 || Suplentes.PorCadeira < 0 || Prazos.PosseSuplenteDias < 0 || Prazos.EleicaoCadeiraDias < 1)
        {
            yield return "suplentes e prazos não negativos.";
        }

        if (Luto.DiasOficiais < 0 || Luto.PesoLembranca is < 1 or > 3 || HistoricoMaximo < 50 || NoticiasMaximo < 10)
        {
            yield return "luto: dias não negativos e peso de 1 a 3; historico_maximo de 50 ou mais; noticias_maximo de 10 ou mais.";
        }

        if (Lideranca.IdadeSucessor.Min < 18 || Lideranca.IdadeSucessor.Max < Lideranca.IdadeSucessor.Min || Equipe.IdadeNovato.Min < 18 || Equipe.IdadeNovato.Max < Equipe.IdadeNovato.Min)
        {
            yield return "idades de sucessor e de novato: mínimo de 18 anos.";
        }
    }
}

/// <summary>De onde vem quem estreia (GDD 17.16, "Entradas").</summary>
public sealed class EntryOrigin
{
    public const string Lideranca = "lideranca";
    public const string Familia = "familia";
    public const string Equipe = "equipe";
    public const string Midia = "midia";
    public const string Gerado = "gerado";

    public required string Id { get; init; }

    public required string Nome { get; init; }

    public required double Peso { get; init; }

    /// <summary>Profissão de quem vem desta origem (id de data/professions.json); null = sorteada.</summary>
    public string? Profissao { get; init; }
}

/// <summary>data/demography/entries.json: origens de quem entra na política e os pesos, iguais para todos os partidos.</summary>
public sealed class EntriesCatalog : IValidatable
{
    public const string File = "demography/entries.json";

    public static readonly IReadOnlyList<string> Known = [EntryOrigin.Lideranca, EntryOrigin.Familia, EntryOrigin.Equipe, EntryOrigin.Midia, EntryOrigin.Gerado];

    public required string Note { get; init; }

    public required List<EntryOrigin> Origens { get; init; }

    public IEnumerable<string> Validate()
    {
        foreach (string error in DataChecks.UniqueIds(Origens.Select(o => o.Id), "Origem"))
        {
            yield return error;
        }

        foreach (EntryOrigin o in Origens.Where(o => !Known.Contains(o.Id) || o.Peso < 0 || string.IsNullOrWhiteSpace(o.Nome)))
        {
            yield return $"origem '{o.Id}': id conhecido, nome e peso não negativo.";
        }

        if (Origens.FirstOrDefault(o => o.Id == EntryOrigin.Gerado) is not { Peso: > 0 })
        {
            yield return "a origem 'gerado' é obrigatória e precisa de peso positivo (é a reserva das demais).";
        }
    }
}

/// <summary>Conteúdo da renovação: parâmetros, origens, tábuas de mortalidade e memória das pessoas.</summary>
public sealed class RenewalContent
{
    public required RenewalConfig Config { get; init; }

    public required EntriesCatalog Entries { get; init; }

    public required MortalityTable Mortality { get; init; }

    public required DemographyConfig Demography { get; init; }

    public required PeopleConfig Memory { get; init; }

    public static RenewalContent LoadOrThrow(string dataDirectory) => new()
    {
        Config = JsonData.LoadFile<RenewalConfig>(Path.Combine(dataDirectory, RenewalConfig.File)).GetValueOrThrow(),
        Entries = JsonData.LoadFile<EntriesCatalog>(Path.Combine(dataDirectory, EntriesCatalog.File)).GetValueOrThrow(),
        Mortality = JsonData.LoadFile<MortalityTable>(Path.Combine(dataDirectory, MortalityTable.File)).GetValueOrThrow(),
        Demography = JsonData.LoadFile<DemographyConfig>(Path.Combine(dataDirectory, DemographyConfig.File)).GetValueOrThrow(),
        Memory = JsonData.LoadFile<PeopleConfig>(Path.Combine(dataDirectory, PeopleConfig.File)).GetValueOrThrow(),
    };

    /// <summary>Relatórios do DataValidator; confere também a profissão indicada em cada origem.</summary>
    public static IEnumerable<DataFileReport> Validate(string dataDirectory)
    {
        DataLoadResult<RenewalConfig> config = JsonData.LoadFile<RenewalConfig>(Path.Combine(dataDirectory, RenewalConfig.File));
        yield return new DataFileReport(RenewalConfig.File, config.Value?.Textos.Count ?? 0, config.Errors);

        DataLoadResult<EntriesCatalog> entries = JsonData.LoadFile<EntriesCatalog>(Path.Combine(dataDirectory, EntriesCatalog.File));
        var errors = entries.Errors.ToList();
        ProfessionCatalog? professions = JsonData.LoadFile<ProfessionCatalog>(Path.Combine(dataDirectory, "professions.json")).Value;
        if (entries.Value is not null && professions is not null)
        {
            errors.AddRange(entries.Value.Origens
                .Where(o => o.Profissao is not null && professions.Professions.All(p => p.Id != o.Profissao))
                .Select(o => $"origem '{o.Id}': profissão '{o.Profissao}' não existe."));
        }

        yield return new DataFileReport(EntriesCatalog.File, entries.Value?.Origens.Count ?? 0, errors);
    }
}
