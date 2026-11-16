using System.IO.Compression;
using System.Text;
using Planalto.DataImporter.Rd;

namespace Planalto.DataImporter.Tests;

public class XlsxTests
{
    private const string Workbook = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets>
            <sheet name="Dados" sheetId="7" r:id="rId2"/>
            <sheet name="NOTAS" sheetId="9" r:id="rId1"/>
          </sheets>
        </workbook>
        """;

    // De propósito, a primeira planilha da pasta de trabalho aponta para sheet2.xml.
    private const string Relationships = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="/xl/worksheets/sheet2.xml"/>
          <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/>
        </Relationships>
        """;

    private const string SharedStrings = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="4" uniqueCount="4">
          <si><t>IDADE</t></si>
          <si><t xml:space="preserve"> São Paulo </t></si>
          <si><r><t>90 </t></r><r><rPr><b/></rPr><t>ou mais</t></r><rPh sb="0" eb="1"><t>fonético</t></rPh></si>
          <si><t>Nota</t></si>
        </sst>
        """;

    private const string Notes = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <sheetData>
            <row r="1"><c r="A1" t="s"><v>3</v></c></row>
          </sheetData>
        </worksheet>
        """;

    private const string Data = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <sheetData>
            <row r="1"><c r="A1" t="s"><v>0</v></c><c r="B1" s="3"/><c r="C1" t="s"><v>1</v></c></row>
            <row r="2"/>
            <row r="4"><c r="B4"><v>2.4436067529077898E-2</v></c><c r="AB4" t="inlineStr"><is><t>em linha</t></is></c></row>
            <row r="5"><c t="s"><v>2</v></c><c><v>1000</v></c><c r="E5" t="str"><f>A1&amp;"x"</f><v>IDADEx</v></c><c r="F5" t="b"><v>1</v></c></row>
          </sheetData>
        </worksheet>
        """;

    private static MemoryStream Build()
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, string content)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }

            Add("xl/workbook.xml", Workbook);
            Add("xl/_rels/workbook.xml.rels", Relationships);
            Add("xl/sharedStrings.xml", SharedStrings);
            Add("xl/worksheets/sheet1.xml", Notes);
            Add("xl/worksheets/sheet2.xml", Data);
        }

        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void Nomes_das_planilhas_na_ordem_da_pasta_de_trabalho()
    {
        using MemoryStream xlsx = Build();
        Assert.Equal(["Dados", "NOTAS"], Xlsx.SheetNames(xlsx));
    }

    [Fact]
    public void Linhas_com_celulas_na_coluna_da_referencia_e_vazias_como_null()
    {
        using MemoryStream xlsx = Build();
        List<string?[]> rows = Xlsx.ReadRows(xlsx, "Dados").ToList();

        Assert.Equal(4, rows.Count);

        // String compartilhada, célula só com estilo (null) e espaço preservado.
        Assert.Equal(new string?[] { "IDADE", null, " São Paulo " }, rows[0].AsEnumerable());
        Assert.Empty(rows[1]);

        // Número como o texto gravado; coluna AB é a 28ª; string em linha.
        Assert.Equal(28, rows[2].Length);
        Assert.Null(rows[2][0]);
        Assert.Equal("2.4436067529077898E-2", rows[2][1]);
        Assert.All(rows[2][2..27], cell => Assert.Null(cell));
        Assert.Equal("em linha", rows[2][27]);

        // Células sem referência seguem em sequência; texto rico é concatenado sem a leitura fonética;
        // resultado de fórmula e booleano vêm pelo valor.
        Assert.Equal(new string?[] { "90 ou mais", "1000", null, null, "IDADEx", "1" }, rows[3].AsEnumerable());
    }

    [Fact]
    public void A_planilha_e_achada_pela_relacao_e_nao_pela_ordem_dos_arquivos()
    {
        using MemoryStream xlsx = Build();
        Assert.Equal("Nota", Assert.Single(Xlsx.ReadRows(xlsx, "NOTAS"))[0]);
    }

    [Fact]
    public void Planilha_inexistente_interrompe_a_importacao()
    {
        using MemoryStream xlsx = Build();
        ImportException ex = Assert.Throws<ImportException>(() => Xlsx.ReadRows(xlsx, "Outra").ToList());
        Assert.Contains("Dados", ex.Message);
    }

    [Fact]
    public void Abre_pelo_caminho_e_de_dentro_de_um_zip()
    {
        string path = Path.Combine(Path.GetTempPath(), $"planalto_xlsx_{Guid.NewGuid():N}.xlsx");
        try
        {
            using (MemoryStream xlsx = Build())
            {
                File.WriteAllBytes(path, xlsx.ToArray());
            }

            Assert.Equal(["Dados", "NOTAS"], Xlsx.SheetNames(path));
            Assert.Equal(4, Xlsx.ReadRows(path, "Dados").Count());

            // Um .xlsx dentro de um .zip: a entrada não tem posição, então o leitor a copia para a memória.
            var outer = new MemoryStream();
            using (var zip = new ZipArchive(outer, ZipArchiveMode.Create, leaveOpen: true))
            {
                zip.CreateEntryFromFile(path, "pasta/planilha.xlsx");
            }

            outer.Position = 0;
            using var read = new ZipArchive(outer, ZipArchiveMode.Read);
            using (Stream entry = read.GetEntry("pasta/planilha.xlsx")!.Open())
            {
                Assert.Equal(["Dados", "NOTAS"], Xlsx.SheetNames(entry));
            }

            using (Stream entry = read.GetEntry("pasta/planilha.xlsx")!.Open())
            {
                Assert.Equal("IDADE", Xlsx.ReadRows(entry, "Dados").First()[0]);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("A1", 0)]
    [InlineData("Z9", 25)]
    [InlineData("AA10", 26)]
    [InlineData("BG7", 58)]
    public void Indice_da_coluna_pela_referencia(string reference, int expected) =>
        Assert.Equal(expected, Xlsx.ColumnIndex(reference));
}
