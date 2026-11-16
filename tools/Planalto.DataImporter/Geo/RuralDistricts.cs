using Planalto.Core.Territory;

namespace Planalto.DataImporter.Geo;

/// <summary>Parte rural de um distrito do IBGE: população dos setores rurais e centro deles.</summary>
public sealed record DistritoRural(string Codigo, long Populacao, double X, double Y);

/// <summary>Resultado de <see cref="RuralDistricts.Plan"/>: o grupo de cada distrito e quantas zonas rurais cabem a cada grupo.</summary>
public sealed record RuralPlan(IReadOnlyDictionary<string, int> GrupoDoDistrito, IReadOnlyList<int> ZonasPorGrupo, int Total);

/// <summary>
/// Divisão da zona rural pelos distritos do IBGE (GDD 17.5, regra 2): cada distrito com zona rural vira uma zona, ou
/// é agrupado com o vizinho até o mínimo de população. Só contas; a geometria fica no <see cref="ZonasImport"/>.
/// </summary>
public static class RuralDistricts
{
    /// <summary>
    /// Agrupa os distritos pequenos e reparte as zonas rurais. Com um grupo só (município sem distrito além da sede, ou
    /// com os demais abaixo do mínimo), o total é o de antes (<paramref name="zonasBase"/>). Com dois ou mais, há ao
    /// menos uma zona por grupo, até <paramref name="zonasBase"/> mais o máximo de zonas extras; o que passar do
    /// número de grupos vai para os grupos mais populosos (maiores médias).
    /// </summary>
    /// <param name="distritos">Distritos com setores rurais.</param>
    /// <param name="divisas">Número de pares de setores rurais vizinhos entre dois distritos (chave de <see cref="Key"/>).</param>
    /// <param name="regra">População mínima e máximo de zonas extras.</param>
    /// <param name="zonasBase">Zonas rurais que o município teria sem a divisão por distrito.</param>
    public static RuralPlan Plan(IReadOnlyList<DistritoRural> distritos, IReadOnlyDictionary<(string, string), int> divisas, RuralPorDistrito regra, int zonasBase)
    {
        // Cada distrito começa no próprio grupo; o grupo de menor população abaixo do mínimo se junta ao vizinho.
        var grupos = distritos.OrderBy(d => d.Codigo, StringComparer.Ordinal).Select(d => new List<DistritoRural> { d }).ToList();
        while (grupos.Count > 1)
        {
            List<DistritoRural> menor = grupos.OrderBy(Pop).ThenBy(g => g[0].Codigo, StringComparer.Ordinal).First();
            if (Pop(menor) >= regra.PopulacaoMinima)
            {
                break;
            }

            List<DistritoRural> destino = grupos.Where(g => g != menor)
                .OrderByDescending(g => Divisa(menor, g, divisas))
                .ThenBy(g => Divisa(menor, g, divisas) > 0 ? 0 : Distancia(menor, g))
                .ThenBy(Pop)
                .ThenBy(g => g[0].Codigo, StringComparer.Ordinal)
                .First();
            destino.AddRange(menor);
            destino.Sort((a, b) => string.CompareOrdinal(a.Codigo, b.Codigo));
            grupos.Remove(menor);
        }

        grupos = grupos.OrderBy(g => g[0].Codigo, StringComparer.Ordinal).ToList();
        var grupoDoDistrito = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int g = 0; g < grupos.Count; g++)
        {
            foreach (DistritoRural d in grupos[g])
            {
                grupoDoDistrito[d.Codigo] = g;
            }
        }

        if (grupos.Count <= 1)
        {
            return new RuralPlan(grupoDoDistrito, grupos.Count == 0 ? [] : [zonasBase], zonasBase);
        }

        int total = Math.Clamp(grupos.Count, zonasBase, zonasBase + regra.ZonasExtrasMax);
        int[] zonas = grupos.Select(_ => 1).ToArray();
        for (int extra = grupos.Count; extra < total; extra++)
        {
            int best = Enumerable.Range(0, grupos.Count).OrderByDescending(g => Pop(grupos[g]) / (double)zonas[g]).ThenBy(g => g).First();
            zonas[best]++;
        }

        return new RuralPlan(grupoDoDistrito, zonas, total);
    }

    /// <summary>Chave das divisas: o menor código primeiro.</summary>
    public static (string, string) Key(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);

    private static long Pop(List<DistritoRural> grupo) => grupo.Sum(d => d.Populacao);

    private static int Divisa(List<DistritoRural> a, List<DistritoRural> b, IReadOnlyDictionary<(string, string), int> divisas) =>
        a.Sum(x => b.Sum(y => divisas.GetValueOrDefault(Key(x.Codigo, y.Codigo))));

    private static double Distancia(List<DistritoRural> a, List<DistritoRural> b) =>
        a.Min(x => b.Min(y => Math.Pow(x.X - y.X, 2) + Math.Pow(x.Y - y.Y, 2)));
}
