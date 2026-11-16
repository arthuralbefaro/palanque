using System.Text;
using Planalto.Core.Data;

namespace Planalto.Core.Tests.Data;

public class InternalReferenceLintTests
{
    private static string DataDirectory =>
        DataPaths.FindDataDirectory(AppContext.BaseDirectory)
        ?? throw new InvalidOperationException("Pasta data/ não encontrada a partir dos testes.");

    [Theory]
    [InlineData("Vereador: número do partido + 3 dígitos (GDD 16.2).", "GDD")]
    [InlineData("Ver DECISOES para o motivo.", "DECISOES")]
    [InlineData("Regra da D-036.", "D-036")]
    [InlineData("Previsto no ROADMAP.", "ROADMAP")]
    [InlineData("Anotado em PENDENCIAS (P-24).", "PENDENCIAS")]
    [InlineData("Item P-09 ainda aberto.", "P-09")]
    [InlineData("Segue o ART_GUIDE.", "ART_GUIDE")]
    public void Texto_que_cita_documento_interno_e_apontado(string text, string expected) =>
        Assert.Equal(expected, InternalReferenceLint.Find(text));

    [Theory]
    [InlineData("Vereador: número do partido + 3 dígitos.")]
    [InlineData("Ordem e progresso. As decisões do partido e as pendências da cidade.")]
    [InlineData("Lei 9.504/1997, art. 39, § 8º.")]
    [InlineData("BR-116, PP-2 e a rodovia SP-270.")]
    [InlineData("")]
    public void Texto_de_jogo_comum_passa(string text) =>
        Assert.Null(InternalReferenceLint.Find(text));

    [Fact]
    public void Campo_de_anotacao_fica_de_fora_e_campo_de_texto_e_conferido()
    {
        const string json = """
            {
              "note": "Regras de campanha (GDD 17.6).",
              "method": { "detalhe": "ver D-005" },
              "itens": [
                { "id": "a", "nome": "Caminhada", "descricao": "Como no GDD 17.6." },
                { "id": "b", "nome": "Comício", "notes": ["P-04 pendente"] }
              ]
            }
            """;
        var errors = new List<string>();

        int texts = InternalReferenceLint.Scan(Encoding.UTF8.GetBytes(json), "teste.json", errors);

        string error = Assert.Single(errors);
        Assert.Contains("'descricao'", error, StringComparison.Ordinal);
        Assert.Contains("GDD", error, StringComparison.Ordinal);
        Assert.Equal(5, texts);
    }

    [Fact]
    public void Nenhum_texto_de_data_cita_documento_interno()
    {
        IReadOnlyList<string> errors = InternalReferenceLint.Run(DataDirectory, out int texts);

        Assert.True(errors.Count == 0, string.Join("\n", errors.Take(20)));
        Assert.True(texts > 1000, $"só {texts} textos lidos em data/");
    }
}
