using System.Globalization;
using System.Text;
using Planalto.Core.Campaign;
using Planalto.Core.Data;
using Planalto.Core.Parties;

namespace Planalto.Core.Media;

/// <summary>Linha editorial de um veículo em relação à prefeitura (GDD 17.13). Não tem relação com ideologia.</summary>
public static class EditorialLine
{
    public const string Governista = "governista";
    public const string Critica = "critica";
    public const string Independente = "independente";

    public static readonly IReadOnlyList<string> All = [Governista, Critica, Independente];
}

/// <summary>Tom de uma notícia: a linha decide o tom, nunca o fato.</summary>
public static class Tone
{
    public const string Favoravel = "favoravel";
    public const string Neutro = "neutro";
    public const string Critico = "critico";

    public static readonly IReadOnlyList<string> All = [Favoravel, Neutro, Critico];

    public static int Sign(string tone) => tone == Favoravel ? 1 : tone == Critico ? -1 : 0;
}

/// <summary>De que lado da prefeitura está quem fez o fato.</summary>
public static class Side
{
    public const string Situacao = "situacao";
    public const string Oposicao = "oposicao";
    public const string Nenhum = "nenhum";

    public static readonly IReadOnlyList<string> All = [Situacao, Oposicao, Nenhum];
}

/// <summary>Tipo de veículo: o que é, como se chama e o que pode (emissora, imprensa escrita, rede).</summary>
public sealed class OutletType
{
    public required string Id { get; init; }

    /// <summary>O que é, para aparecer depois do nome ("rádio local").</summary>
    public required string Nome { get; init; }

    /// <summary>Começos de nome possíveis ("Rádio", "Jornal").</summary>
    public required List<string> Prefixos { get; init; }

    /// <summary>Rádio ou TV: tem horário eleitoral gratuito e não vende propaganda (arts. 44 e 47).</summary>
    public bool Emissora { get; init; }

    /// <summary>Imprensa escrita: aceita anúncio pago dentro do art. 43.</summary>
    public bool Escrita { get; init; }

    /// <summary>Página de rede social: por ela circulam os boatos.</summary>
    public bool Rede { get; init; }

    /// <summary>Fração dos eleitores de uma zona que o veículo alcança (sorteada por zona).</summary>
    public required DoubleRange Alcance { get; init; }

    /// <summary>Custo de um anúncio no porte médio (só imprensa escrita).</summary>
    public decimal AnuncioCusto { get; init; }

    /// <summary>Públicos em que um veículo do tipo pode ser mais forte ("geral" ou grupos de eleitores).</summary>
    public List<string> Publicos { get; init; } = ["geral"];
}

public sealed class LineDefinition
{
    public required string Id { get; init; }

    /// <summary>Como aparece depois do tipo ("linha governista").</summary>
    public required string Nome { get; init; }

    public required double Weight { get; init; }

    /// <summary>Tons coerentes com a linha, por lado de quem fez o fato (lado → tom → peso).</summary>
    public required Dictionary<string, Dictionary<string, double>> Tons { get; init; }

    public bool Coerente(string side, string tone) => Tons.TryGetValue(side, out var tones) && tones.GetValueOrDefault(tone) > 0;
}

public sealed class MediaRules
{
    /// <summary>Chance de um veículo cobrir um fato, pela visibilidade (índice 1 a 3; o 0 nunca é coberto).</summary>
    public required List<double> CoberturaPorVisibilidade { get; init; }

    /// <summary>Multiplica a chance quando o tema do fato é de interesse do veículo.</summary>
    public required double InteresseFator { get; init; }

    /// <summary>Chance de o tom sair fora da linha (no máximo 5%: o teste exige 95% de coerência).</summary>
    public required double DesvioDeTom { get; init; }

    /// <summary>Imagem que uma notícia favorável (ou crítica, com sinal trocado) rende a quem fez o fato, no alcance cheio.</summary>
    public required double ImagemPorNoticia { get; init; }

    public required DoubleRange Sensacionalismo { get; init; }

    /// <summary>Notícias sobre um tema, numa semana, para ele ficar em alta nas zonas atingidas.</summary>
    public required int TemaEmAltaNoticias { get; init; }

    /// <summary>Fatos diferentes sobre o tema, na mesma semana, para ele ficar em alta (um fato só não faz uma semana de notícias).</summary>
    public required int TemaEmAltaFatos { get; init; }

    /// <summary>Bônus no rendimento do compromisso que trata do tema em alta (GDD 17.6, ocasião).</summary>
    public required double TemaEmAltaBonus { get; init; }

    public required int TemaEmAltaSemanas { get; init; }

    /// <summary>Alcance mínimo do veículo numa zona para o tema contar como em alta ali.</summary>
    public required double TemaEmAltaAlcanceMinimo { get; init; }

    /// <summary>Chance diária de um fato da cidade (problema de zona que vira notícia).</summary>
    public required double FatoDaCidadeChanceDiaria { get; init; }

    /// <summary>De quantas em quantas semanas sai um fato econômico (no máximo 8, princípio b).</summary>
    public required int EconomiaACadaSemanas { get; init; }

    /// <summary>Entrevista em veículo crítico ao candidato: chance do teste e crédito do alcance.</summary>
    public required double EntrevistaCriticoChance { get; init; }

    public required double EntrevistaCriticoCredito { get; init; }

    public required double EntrevistaAmigoChance { get; init; }

    public required double EntrevistaAmigoCredito { get; init; }

    /// <summary>Chance a mais (ou a menos) por ponto de relação com o jornalista.</summary>
    public required double EntrevistaChancePorRelacao { get; init; }

    public required double PautaChanceBase { get; init; }

    public required double PautaChancePorRelacao { get; init; }

    /// <summary>Presença que um anúncio rende nas zonas do veículo (pontos na cidade inteira, antes do alcance).</summary>
    public required double AnuncioPresenca { get; init; }

    public required DoubleRange Sorte { get; init; }

    /// <summary>Presença semanal, na cidade inteira, de um partido com todo o tempo do horário gratuito, por candidato.</summary>
    public required double HorarioGratuitoPresenca { get; init; }

    public required int MaxNoticias { get; init; }

    /// <summary>Passo relativo máximo de cada indicador econômico entre dois fatos (sem série histórica).</summary>
    public required double EconomiaPasso { get; init; }

    /// <summary>Chance de o dono de um veículo ser uma liderança da cidade, e de ser um doador.</summary>
    public required double DonoLiderancaChance { get; init; }

    public required double DonoDoadorChance { get; init; }

    /// <summary>Chance a mais de a pauta entrar quando o tema é de interesse do veículo.</summary>
    public required double PautaInteresse { get; init; }

    /// <summary>Dias em que um fato ainda serve de pauta.</summary>
    public required int PautaDias { get; init; }
}

/// <summary>data/media/outlets.json: tipos de veículo por porte, termos de nome, linhas editoriais e regras.</summary>
public sealed class OutletCatalog : IValidatable
{
    public const string File = "media/outlets.json";
    public const int MaxName = 20;

    public required string Note { get; init; }

    public required List<OutletType> Tipos { get; init; }

    /// <summary>Por porte, as combinações possíveis de tipos (uma é sorteada).</summary>
    public required Dictionary<string, List<List<string>>> PorPorte { get; init; }

    /// <summary>Termos neutros que completam o nome ("Vale", "Horizonte").</summary>
    public required List<string> Termos { get; init; }

    public required List<LineDefinition> Linhas { get; init; }

    public required MediaRules Regras { get; init; }

    public required Dictionary<string, string> Textos { get; init; }

    /// <summary>Lembranças de jornalista e de dono de veículo.</summary>
    public required Dictionary<string, MemoryDefinition> Lembrancas { get; init; }

    public OutletType Type(string id) => Tipos.First(t => t.Id == id);

    public LineDefinition Line(string id) => Linhas.First(l => l.Id == id);

    public string Texto(string key) => Textos.TryGetValue(key, out string? text) ? text : key;

    public static readonly IReadOnlyList<string> TextKeys =
    [
        "fator_veiculo_critico", "fator_veiculo_amigo", "fator_alcance", "fator_tema_em_alta", "pauta_explica", "pauta_sucesso", "pauta_falha",
        "anuncio_explica", "anuncio_limite", "anuncio_feito", "horario_gratuito", "horario_sem_emissora", "tema_em_alta", "pesquisa_fato",
    ];

    public static readonly IReadOnlyList<string> MemoryKeys = ["entrevista", "pauta_boa", "pauta_fraca", "anuncio"];

    public static readonly IReadOnlyDictionary<string, (int Min, int Max)> PorteCount = new Dictionary<string, (int, int)>(StringComparer.Ordinal)
    {
        ["pequeno"] = (2, 2),
        ["medio"] = (3, 4),
        ["grande"] = (5, 5),
        ["metropole"] = (6, 7),
    };

    public IEnumerable<string> Validate()
    {
        foreach (string error in DataChecks.UniqueIds(Tipos.Select(t => t.Id), "Tipo de veículo").Concat(DataChecks.UniqueIds(Linhas.Select(l => l.Id), "Linha")))
        {
            yield return error;
        }

        foreach (string id in EditorialLine.All.Where(id => Linhas.All(l => l.Id != id)))
        {
            yield return $"falta a linha '{id}'.";
        }

        foreach (LineDefinition line in Linhas)
        {
            foreach (string side in Side.All)
            {
                if (!line.Tons.TryGetValue(side, out var tones) || tones.Count == 0 || tones.Keys.Any(t => !Tone.All.Contains(t)) || tones.Values.Sum() <= 0)
                {
                    yield return $"linha '{line.Id}': tons do lado '{side}' ausentes ou inválidos.";
                }
            }
        }

        foreach (var (porte, (min, max)) in PorteCount)
        {
            if (!PorPorte.TryGetValue(porte, out var mixes) || mixes.Count == 0)
            {
                yield return $"por_porte.{porte}: falta a lista de combinações.";
                continue;
            }

            foreach (List<string> mix in mixes)
            {
                if (mix.Count < min || mix.Count > max || mix.Any(t => Tipos.All(x => x.Id != t)))
                {
                    yield return $"por_porte.{porte}: cada combinação tem de {min} a {max} veículos de tipos conhecidos (GDD 17.13).";
                }

                if (mix.All(t => !Type(t).Rede))
                {
                    yield return $"por_porte.{porte}: toda combinação precisa de uma página de rede (por ela circulam os boatos).";
                }
            }
        }

        if (Termos.Count < 12 || Termos.Distinct(StringComparer.Ordinal).Count() != Termos.Count)
        {
            yield return "termos: pelo menos 12, sem repetição.";
        }

        foreach (OutletType type in Tipos)
        {
            if (type.Prefixos.Count == 0 || type.Alcance.Min < 0 || type.Alcance.Max > 1 || type.Alcance.Max < type.Alcance.Min || (type.Escrita && type.AnuncioCusto <= 0))
            {
                yield return $"tipo '{type.Id}': prefixos, alcance em [0, 1] e custo de anúncio na imprensa escrita.";
            }

            foreach (string prefix in type.Prefixos)
            {
                foreach (string term in Termos.Where(term => $"{prefix} {term}".Length > MaxName))
                {
                    yield return $"nome '{prefix} {term}' com mais de {MaxName} caracteres.";
                }
            }
        }

        MediaRules r = Regras;
        if (r.CoberturaPorVisibilidade.Count != 4 || r.CoberturaPorVisibilidade[0] != 0 || r.CoberturaPorVisibilidade.Any(c => c is < 0 or > 1)
            || r.DesvioDeTom is < 0 or > 0.05 || r.TemaEmAltaNoticias < 1 || r.TemaEmAltaBonus <= 0 || r.TemaEmAltaSemanas < 1
            || r.EconomiaACadaSemanas is < 1 or > 8 || r.FatoDaCidadeChanceDiaria is < 0 or > 1 || r.Sorte.Min <= 0 || r.Sorte.Max < r.Sorte.Min
            || r.MaxNoticias < 50 || r.Sensacionalismo.Min < 0 || r.Sensacionalismo.Max > 1 || r.InteresseFator < 1)
        {
            yield return "regras: valores fora do intervalo.";
        }

        foreach (string key in TextKeys.Where(k => !Textos.ContainsKey(k)))
        {
            yield return $"textos: falta '{key}'.";
        }

        foreach (string key in MemoryKeys.Where(k => !Lembrancas.ContainsKey(k)))
        {
            yield return $"lembrancas: falta '{key}'.";
        }
    }
}

/// <summary>Tipo de fato: visibilidade padrão e manchetes por tom.</summary>
public sealed class FactTypeDefinition
{
    /// <summary>Tipo do fato, ou "tipo:subtipo" (ex.: "compromisso:comicio").</summary>
    public required string Id { get; init; }

    /// <summary>0 a 3: 0 só é conhecido por quem estava presente.</summary>
    public required int Visibilidade { get; init; }

    /// <summary>Manchetes por tom; aceitam {ator}, {zona}, {tema}, {alvo} e {texto}. Sem manchete, vale a padrão.</summary>
    public Dictionary<string, List<string>> Manchetes { get; init; } = [];
}

/// <summary>Problema ou acontecimento de zona que vira notícia e puxa um tema (GDD 17.13, "tema em alta").</summary>
public sealed class CityFactDefinition
{
    public required string Id { get; init; }

    public required string Tema { get; init; }

    /// <summary>"urbana", "rural" ou "qualquer".</summary>
    public required string Situacao { get; init; }

    /// <summary>Texto do fato, com {zona}.</summary>
    public required string Texto { get; init; }

    public double Weight { get; init; } = 1;
}

/// <summary>Fato econômico nacional, a partir de data/economy/start.json.</summary>
public sealed class EconomyFactDefinition
{
    public const string Selic = "selic";
    public const string Ipca = "ipca";
    public const string Cambio = "cambio";
    public const string Desocupacao = "desocupacao";

    public static readonly IReadOnlyList<string> Indicadores = [Selic, Ipca, Cambio, Desocupacao];

    public required string Indicador { get; init; }

    /// <summary>Textos conforme o indicador subiu, caiu ou ficou igual; aceitam {valor}.</summary>
    public required string Sobe { get; init; }

    public required string Cai { get; init; }

    public required string Estavel { get; init; }

    public string? Tema { get; init; }
}

/// <summary>data/media/templates.json: visibilidade e manchetes por tipo de fato e tom, fatos da cidade e da economia.</summary>
public sealed class TemplateCatalog : IValidatable
{
    public const string File = "media/templates.json";
    public const int MaxHeadline = 70;

    public required string Note { get; init; }

    public required List<FactTypeDefinition> Fatos { get; init; }

    /// <summary>Manchete padrão por tom, com {texto}.</summary>
    public required Dictionary<string, string> Padrao { get; init; }

    /// <summary>Marca de boato na manchete.</summary>
    public required string BoatoPrefixo { get; init; }

    public required List<CityFactDefinition> FatosDaCidade { get; init; }

    public required List<EconomyFactDefinition> FatosEconomicos { get; init; }

    private Dictionary<string, FactTypeDefinition>? _byId;

    public FactTypeDefinition? Find(string kind, string? sub)
    {
        _byId ??= Fatos.ToDictionary(f => f.Id, StringComparer.Ordinal);
        return sub is not null && _byId.TryGetValue($"{kind}:{sub}", out FactTypeDefinition? specific) ? specific : _byId.GetValueOrDefault(kind);
    }

    public int Visibility(string kind, string? sub) => Find(kind, sub)?.Visibilidade ?? 0;

    public IEnumerable<string> Validate()
    {
        foreach (string error in DataChecks.UniqueIds(Fatos.Select(f => f.Id.Replace(':', '_')), "Tipo de fato").Concat(DataChecks.UniqueIds(FatosDaCidade.Select(f => f.Id), "Fato da cidade")))
        {
            yield return error;
        }

        foreach (string tone in Tone.All.Where(t => !Padrao.ContainsKey(t) || !Padrao[t].Contains("{texto}", StringComparison.Ordinal)))
        {
            yield return $"padrao: falta a manchete do tom '{tone}' com {{texto}}.";
        }

        foreach (FactTypeDefinition f in Fatos)
        {
            if (f.Visibilidade is < 0 or > 3)
            {
                yield return $"fato '{f.Id}': visibilidade de 0 a 3.";
            }

            foreach (var (tone, headlines) in f.Manchetes)
            {
                if (!Tone.All.Contains(tone))
                {
                    yield return $"fato '{f.Id}': tom '{tone}' desconhecido.";
                }

                // O texto fixo cabe em 70 caracteres com nomes de tamanho comum (ator 16, zona 14, tema 12, alvo 16).
                foreach (string h in headlines.Where(h => Sample(h).Length > MaxHeadline))
                {
                    yield return $"fato '{f.Id}': manchete com mais de {MaxHeadline} caracteres: \"{h}\".";
                }
            }
        }

        foreach (CityFactDefinition f in FatosDaCidade)
        {
            if (f.Situacao is not ("urbana" or "rural" or "qualquer") || !f.Texto.Contains("{zona}", StringComparison.Ordinal) || f.Weight <= 0
                || f.Texto.Replace("{zona}", new string('x', 14), StringComparison.Ordinal).Length > MaxHeadline)
            {
                yield return $"fato da cidade '{f.Id}': situação válida, {{zona}} no texto, peso positivo e até {MaxHeadline} caracteres.";
            }
        }

        foreach (string indicador in EconomyFactDefinition.Indicadores.Where(i => FatosEconomicos.All(f => f.Indicador != i)))
        {
            yield return $"fatos_economicos: falta o indicador '{indicador}'.";
        }

        foreach (EconomyFactDefinition f in FatosEconomicos)
        {
            if (!EconomyFactDefinition.Indicadores.Contains(f.Indicador) || new[] { f.Sobe, f.Cai, f.Estavel }.Any(t => !t.Contains("{valor}", StringComparison.Ordinal) || t.Length > MaxHeadline))
            {
                yield return $"fato econômico '{f.Indicador}': indicador conhecido e três textos com {{valor}} de até {MaxHeadline} caracteres.";
            }
        }

        if (BoatoPrefixo.Trim().Length == 0)
        {
            yield return "boato_prefixo vazio.";
        }
    }

    private static string Sample(string template) => template
        .Replace("{ator}", new string('x', 16), StringComparison.Ordinal)
        .Replace("{alvo}", new string('x', 16), StringComparison.Ordinal)
        .Replace("{zona}", new string('x', 14), StringComparison.Ordinal)
        .Replace("{tema}", new string('x', 12), StringComparison.Ordinal)
        .Replace("{texto}", "", StringComparison.Ordinal);
}

/// <summary>data/media/blocklist.json: nomes de veículos reais que nenhum veículo gerado pode repetir.</summary>
public sealed class OutletBlocklist : IValidatable
{
    public const string File = "media/blocklist.json";

    public required string Note { get; init; }

    /// <summary>De onde veio a lista e o que falta conferir.</summary>
    public required string Fonte { get; init; }

    public required List<string> Nomes { get; init; }

    private HashSet<string>? _normalized;

    /// <summary>Sem acento, em minúsculas, só letras e dígitos separados por um espaço.</summary>
    public static string Normalize(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (char c in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// O nome coincide com um veículo real: igual, ou contém o nome inteiro de um veículo real como palavras
    /// seguidas (a mesma lógica da lista de partidos).
    /// </summary>
    public bool Blocks(string name)
    {
        _normalized ??= Nomes.Select(Normalize).Where(n => n.Length > 0).ToHashSet(StringComparer.Ordinal);
        string normalized = Normalize(name);
        string padded = $" {normalized} ";
        return _normalized.Any(real => normalized == real || padded.Contains($" {real} ", StringComparison.Ordinal));
    }

    public IEnumerable<string> Validate()
    {
        if (Nomes.Count == 0 || Nomes.Any(string.IsNullOrWhiteSpace))
        {
            yield return "nomes: lista vazia ou com nome em branco.";
        }

        if (string.IsNullOrWhiteSpace(Fonte))
        {
            yield return "fonte: diga de onde veio a lista.";
        }
    }
}

/// <summary>Conteúdo de mídia já validado.</summary>
public sealed class MediaContent
{
    public required OutletCatalog Outlets { get; init; }

    public required TemplateCatalog Templates { get; init; }

    public required OutletBlocklist Blocklist { get; init; }

    /// <summary>Erros de cruzamento entre os três arquivos: nenhum nome possível pode bater na lista de bloqueio.</summary>
    public static IEnumerable<string> CrossCheck(OutletCatalog outlets, OutletBlocklist blocklist)
    {
        foreach (OutletType type in outlets.Tipos)
        {
            foreach (string prefix in type.Prefixos)
            {
                foreach (string term in outlets.Termos.Where(term => blocklist.Blocks($"{prefix} {term}")))
                {
                    yield return $"nome '{prefix} {term}' coincide com a lista de bloqueio de veículos reais.";
                }
            }
        }
    }
}

/// <summary>Veículo de mídia local (GDD 17.13). Fica na cidade (atravessa eleições).</summary>
public sealed class Outlet
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Type { get; init; }

    public required string Line { get; init; }

    /// <summary>Dois temas de interesse.</summary>
    public required List<string> Themes { get; init; }

    /// <summary>0 a 1: quanto o veículo carrega no tom.</summary>
    public required double Sensationalism { get; init; }

    public required string OwnerId { get; init; }

    public required string JournalistId { get; init; }

    /// <summary>Fração dos eleitores alcançada em cada zona (na ordem das zonas do território).</summary>
    public required List<double> Reach { get; init; }

    /// <summary>Público em que o veículo é mais forte (grupo de eleitores), ou "geral".</summary>
    public required string Publico { get; init; }
}

/// <summary>Notícia: um fato coberto por um veículo, com o tom que a linha dele deu.</summary>
public sealed class NewsItem
{
    public required string Id { get; init; }

    /// <summary>Fato de origem: nenhuma notícia existe sem ele.</summary>
    public required string FactId { get; init; }

    public required string OutletId { get; init; }

    public required DateOnly Date { get; init; }

    public required string Tone { get; init; }

    public required string Headline { get; init; }

    /// <summary>Item de rede que pode ser falso.</summary>
    public bool Boato { get; init; }

    public string? Theme { get; init; }

    public string? ZoneId { get; init; }

    public string? ActorId { get; init; }

    public bool CitaJogador { get; init; }

    /// <summary>De que lado da prefeitura estava quem fez o fato quando a notícia saiu.</summary>
    public string Lado { get; init; } = Side.Nenhum;
}

/// <summary>Tema em alta nas zonas atingidas pelo noticiário de uma semana.</summary>
public sealed class HotTheme
{
    public required string Theme { get; init; }

    public required List<string> Zones { get; init; }

    public required DateOnly From { get; init; }

    public required DateOnly Until { get; init; }

    public required int News { get; init; }
}

/// <summary>Estado da mídia na campanha: notícias, temas em alta, anúncios e pautas.</summary>
public sealed class MediaState
{
    public List<NewsItem> News { get; init; } = [];

    public int NextId { get; set; } = 1;

    public List<HotTheme> Hot { get; init; } = [];

    /// <summary>Datas dos anúncios pagos em cada veículo (art. 43).</summary>
    public Dictionary<string, List<DateOnly>> AdDates { get; init; } = [];

    /// <summary>Última pauta sugerida a cada jornalista.</summary>
    public Dictionary<string, DateOnly> LastPitch { get; init; } = [];

    /// <summary>Indicadores econômicos de hoje, a partir de data/economy/start.json.</summary>
    public Dictionary<string, decimal> Economy { get; init; } = [];

    public DateOnly? LastEconomyFact { get; set; }

    /// <summary>Número do último fato já visto pelos veículos.</summary>
    public int CoveredUpTo { get; set; }
}
