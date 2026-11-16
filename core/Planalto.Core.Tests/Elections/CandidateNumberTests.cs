using Planalto.Core.Elections;
using Planalto.Core.State;

namespace Planalto.Core.Tests.Elections;

public class CandidateNumberTests
{
    private const string Cidade = "3550308";

    [Theory]
    [InlineData(Office.Prefeito, 2)]
    [InlineData(Office.Governador, 2)]
    [InlineData(Office.Presidente, 2)]
    [InlineData(Office.Senador, 3)]
    [InlineData(Office.DeputadoFederal, 4)]
    [InlineData(Office.Vereador, 5)]
    [InlineData(Office.DeputadoEstadual, 5)]
    [InlineData(Office.DeputadoDistrital, 5)]
    public void Formato_TemOsDigitosDoCargoEComecaPeloNumeroDoPartido(Office office, int digits)
    {
        var book = new CandidateNumberBook();
        CandidateNumber number = book.ReserveRandom(new Rng(1), Cidade, office, 47)!.Value;

        Assert.Equal(digits, number.Text.Length);
        Assert.StartsWith("47", number.Text);
        Assert.Equal(digits - 2, number.SuffixDigits);
    }

    [Fact]
    public void Texto_PreencheOsFinaisComZerosAEsquerda()
    {
        Assert.Equal("45007", CandidateNumbers.Create(Office.Vereador, 45, 7).Text);
        Assert.Equal("4501", CandidateNumbers.Create(Office.DeputadoFederal, 45, 1).Text);
        Assert.Equal("450", CandidateNumbers.Create(Office.Senador, 45, 0).Text);
        Assert.Equal("45", CandidateNumbers.Create(Office.Presidente, 45, 0).Text);
    }

    [Theory]
    [InlineData("45123", Office.Vereador, 45, null)]
    [InlineData("4512", Office.Vereador, 45, CandidateNumberIssue.QuantidadeDeDigitosErrada)]
    [InlineData("46123", Office.Vereador, 45, CandidateNumberIssue.PrefixoDeOutroPartido)]
    [InlineData("45a23", Office.Vereador, 45, CandidateNumberIssue.NaoNumerico)]
    [InlineData("", Office.Vereador, 45, CandidateNumberIssue.NaoNumerico)]
    [InlineData("451", Office.Senador, 45, null)]
    [InlineData("45", Office.Prefeito, 45, null)]
    [InlineData("451", Office.Prefeito, 45, CandidateNumberIssue.QuantidadeDeDigitosErrada)]
    [InlineData("09123", Office.Vereador, 9, CandidateNumberIssue.NumeroDePartidoInvalido)]
    public void TryParse_ValidaFormatoEPrefixo(string text, Office office, int party, CandidateNumberIssue? expected)
    {
        CandidateNumberIssue? issue = CandidateNumbers.TryParse(text, office, party, out CandidateNumber number);

        Assert.Equal(expected, issue);
        if (expected is null)
        {
            Assert.Equal(text, number.Text);
        }
    }

    [Theory]
    [InlineData("000", "45000")]
    [InlineData("111", "45111")]
    [InlineData("007", "45007")]
    [InlineData("999", "45999")]
    [InlineData("120", "45120")]
    public void TryParseSuffix_AceitaTresDigitosComZerosAEsquerdaERepetidos(string typed, string expected)
    {
        CandidateNumberIssue? issue = CandidateNumbers.TryParseSuffix(typed, Office.Vereador, 45, [], out CandidateNumber number);

        Assert.Null(issue);
        Assert.Equal(expected, number.Text);
        Assert.Equal(typed, CandidateNumbers.SuffixOf(number.Text, Office.Vereador));
    }

    [Theory]
    [InlineData("", CandidateNumberIssue.NaoNumerico)]
    [InlineData(null, CandidateNumberIssue.NaoNumerico)]
    [InlineData("1a3", CandidateNumberIssue.NaoNumerico)]
    [InlineData(" 12", CandidateNumberIssue.NaoNumerico)]
    [InlineData("-12", CandidateNumberIssue.NaoNumerico)]
    [InlineData("１２３", CandidateNumberIssue.NaoNumerico)]
    [InlineData("7", CandidateNumberIssue.QuantidadeDeDigitosErrada)]
    [InlineData("12", CandidateNumberIssue.QuantidadeDeDigitosErrada)]
    [InlineData("1234", CandidateNumberIssue.QuantidadeDeDigitosErrada)]
    public void TryParseSuffix_RecusaOQueNaoSaoExatamenteTresAlgarismos(string? typed, CandidateNumberIssue expected)
    {
        Assert.Equal(expected, CandidateNumbers.TryParseSuffix(typed, Office.Vereador, 45, [], out _));
    }

    [Fact]
    public void TryParseSuffix_RecusaNumeroJaOcupadoNoPartidoENumeroDePartidoInvalido()
    {
        var book = new CandidateNumberBook();
        book.TryReserve(Cidade, Office.Vereador, 45, 7, out _);
        book.TryReserve(Cidade, Office.Vereador, 46, 8, out _);
        IReadOnlyCollection<int> occupied = book.UsedSuffixes(Cidade, Office.Vereador, 45);

        Assert.Equal(CandidateNumberIssue.JaEmUso, CandidateNumbers.TryParseSuffix("007", Office.Vereador, 45, occupied, out _));
        Assert.Null(CandidateNumbers.TryParseSuffix("008", Office.Vereador, 45, occupied, out CandidateNumber free));
        Assert.Equal("45008", free.Text);
        Assert.Empty(book.UsedSuffixes("outra cidade", Office.Vereador, 45));
        Assert.Equal(CandidateNumberIssue.NumeroDePartidoInvalido, CandidateNumbers.TryParseSuffix("123", Office.Vereador, 9, [], out _));
        Assert.Equal("", CandidateNumbers.SuffixOf("4512", Office.Vereador));
        Assert.Equal("", CandidateNumbers.SuffixOf(null, Office.Vereador));
    }

    [Theory]
    [InlineData(9)]
    [InlineData(100)]
    [InlineData(0)]
    public void NumeroDePartido_ForaDeDezANoventaENove_ERejeitado(int party)
    {
        var book = new CandidateNumberBook();

        Assert.Equal(CandidateNumberIssue.NumeroDePartidoInvalido, book.TryReserve(Cidade, Office.Vereador, party, 1, out _));
        Assert.Empty(book.FreeSuffixes(Cidade, Office.Vereador, party));
    }

    [Theory]
    [InlineData(Office.Vereador, 1000)]
    [InlineData(Office.DeputadoFederal, 100)]
    [InlineData(Office.Senador, 10)]
    [InlineData(Office.Prefeito, 1)]
    public void Sorteio_NuncaRepeteNoPartidoEAcabaQuandoEsgota(Office office, int capacity)
    {
        var book = new CandidateNumberBook();
        var rng = new Rng(2);
        var texts = new HashSet<string>();

        for (int i = 0; i < capacity; i++)
        {
            CandidateNumber? number = book.ReserveRandom(rng, Cidade, office, 22);
            Assert.NotNull(number);
            Assert.True(texts.Add(number.Value.Text), $"Número repetido: {number}");
        }

        Assert.Null(book.ReserveRandom(rng, Cidade, office, 22));
        Assert.Empty(book.FreeSuffixes(Cidade, office, 22));
    }

    [Fact]
    public void FinaisLivres_ExcluemOsJaUsadosSoNoMesmoPartidoCargoECircunscricao()
    {
        var book = new CandidateNumberBook();
        Assert.Null(book.TryReserve(Cidade, Office.Vereador, 22, 123, out _));

        Assert.Equal(CandidateNumberIssue.JaEmUso, book.TryReserve(Cidade, Office.Vereador, 22, 123, out _));
        Assert.DoesNotContain(123, book.FreeSuffixes(Cidade, Office.Vereador, 22));
        Assert.Contains(123, book.FreeSuffixes(Cidade, Office.Vereador, 23));
        Assert.Contains(123, book.FreeSuffixes("3304557", Office.Vereador, 22));
        Assert.Equal(999, book.FreeSuffixes(Cidade, Office.Vereador, 22).Count);
    }

    [Fact]
    public void FinalForaDaFaixaDoCargo_ERejeitado()
    {
        var book = new CandidateNumberBook();

        Assert.Equal(CandidateNumberIssue.FinalForaDaFaixa, book.TryReserve(Cidade, Office.DeputadoFederal, 22, 100, out _));
        Assert.Equal(CandidateNumberIssue.FinalForaDaFaixa, book.TryReserve(Cidade, Office.Prefeito, 22, 1, out _));
        Assert.Equal(CandidateNumberIssue.FinalForaDaFaixa, book.TryReserve(Cidade, Office.Vereador, 22, -1, out _));
    }

    [Fact]
    public void TrocaDePartido_MantemOsFinaisQuandoLivres()
    {
        var book = new CandidateNumberBook();
        book.TryReserve(Cidade, Office.Vereador, 22, 123, out CandidateNumber current);

        CandidateNumberChange change = book.ChangeParty(new Rng(3), Cidade, current, 51)!;

        Assert.True(change.KeptSuffix);
        Assert.Equal("51123", change.Current.Text);
        Assert.True(book.IsFree(Cidade, Office.Vereador, 22, 123));
        Assert.False(book.IsFree(Cidade, Office.Vereador, 51, 123));
    }

    [Fact]
    public void TrocaDePartido_SorteiaOutroFinalQuandoOMesmoEstaOcupado()
    {
        var book = new CandidateNumberBook();
        book.TryReserve(Cidade, Office.Vereador, 22, 123, out CandidateNumber current);
        book.TryReserve(Cidade, Office.Vereador, 51, 123, out _);

        CandidateNumberChange change = book.ChangeParty(new Rng(4), Cidade, current, 51)!;

        Assert.False(change.KeptSuffix);
        Assert.Equal(51, change.Current.PartyNumber);
        Assert.NotEqual(123, change.Current.Suffix);
        Assert.Equal(5, change.Current.Text.Length);
    }

    [Fact]
    public void TrocaDePartido_SemNumeroLivreNaoLiberaOAntigo()
    {
        var book = new CandidateNumberBook();
        book.TryReserve(Cidade, Office.Prefeito, 22, 0, out CandidateNumber current);
        book.TryReserve(Cidade, Office.Prefeito, 51, 0, out _);

        Assert.Null(book.ChangeParty(new Rng(5), Cidade, current, 51));
        Assert.False(book.IsFree(Cidade, Office.Prefeito, 22, 0));
    }
}
