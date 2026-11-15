using Planalto.Core.Elections;

namespace Planalto.Core.Tests.Elections;

/// <summary>Casos proporcionais calculados à mão; as contas estão nos comentários de cada teste.</summary>
public class ProportionalTests
{
    private static readonly DateOnly Nascimento = new(1980, 1, 1);

    private static PartyList Party(string id, long legend, params (string Id, long Votes)[] candidates) =>
        new(id, legend, candidates.Select(c => new CandidateVotes(c.Id, c.Votes, Nascimento)).ToList());

    [Theory]
    [InlineData(1000, 10, 100)] // 100,0
    [InlineData(1005, 10, 100)] // 100,5: fração igual a meio é desprezada
    [InlineData(1006, 10, 101)] // 100,6: fração superior a meio vira 1
    [InlineData(10900, 9, 1211)] // 1211,11
    public void QuocienteEleitoral_ArredondaPelaRegraDoArt106(long validVotes, int seats, long expected)
    {
        Assert.Equal(expected, ElectionRules.ElectoralQuotient(validVotes, seats));
    }

    [Fact]
    public void CasoComSobras_DistribuiPelasMaioresMedias()
    {
        // 9 vagas. Válidos = 5500 + 3000 + 1800 + 600 = 10900. QE = 10900 / 9 = 1211,11 → 1211.
        // Pisos: 10% = 121,1; 20% = 242,2; 80% = 968,8.
        // QP: A = 5500/1211 = 4,54 → 4; B = 3000/1211 = 2,48 → 2; C = 1800/1211 = 1,49 → 1; D = 0.
        // 1ª etapa: a1, a2, a3, a4, b1, b2, c1 (7 vagas). Restam 2.
        // 2ª etapa (partido ≥ 968,8 e candidato ≥ 242,2):
        //   A fica de fora: o próximo, a5, tem 100 < 242,2. D fica de fora: 600 < 968,8.
        //   Médias: B = 3000/3 = 1000; C = 1800/2 = 900 → B elege b3.
        //   Depois: B sem candidatos; C = 900 → C elege c2.
        var parties = new[]
        {
            Party("A", 300, ("a1", 2500), ("a2", 1200), ("a3", 900), ("a4", 500), ("a5", 100)),
            Party("B", 0, ("b1", 1800), ("b2", 700), ("b3", 500)),
            Party("C", 100, ("c1", 1100), ("c2", 600)),
            Party("D", 50, ("d1", 400), ("d2", 150)),
        };

        ProportionalResult result = ElectionRules.Proportional(9, parties);

        Assert.Equal(10900, result.ValidVotes);
        Assert.Equal(1211, result.ElectoralQuotient);
        Assert.Equal(4, result.PartyQuotients["A"]);
        Assert.Equal(2, result.PartyQuotients["B"]);
        Assert.Equal(1, result.PartyQuotients["C"]);
        Assert.Equal(0, result.PartyQuotients["D"]);
        Assert.Equal(
            ["a1", "a2", "a3", "a4", "b1", "b2", "c1", "b3", "c2"],
            result.Elected.Select(e => e.CandidateId));
        Assert.Equal(SeatStage.Remainder, result.Elected.Single(e => e.CandidateId == "b3").Stage);
        Assert.Equal(SeatStage.Remainder, result.Elected.Single(e => e.CandidateId == "c2").Stage);
        Assert.Equal(0, result.VacantSeats);
    }

    [Fact]
    public void CandidatoBemVotado_NaoSeElegeSemQuocientePartidario()
    {
        // 5 vagas. Válidos = 10000 + 1600 = 11600. QE = 2320. 80% = 1856; 20% = 464.
        // QP: A = 10000/2320 = 4,31 → 4; B = 1600/2320 → 0.
        // 1ª etapa: a1..a4. Resta 1.
        // 2ª etapa: B fica de fora (1600 < 1856). A tem a5 com 800 ≥ 464 → a5 eleito.
        // b1, com 1500 votos, não se elege; a5 se elege com 800.
        var parties = new[]
        {
            Party("A", 0, ("a1", 4000), ("a2", 2500), ("a3", 1500), ("a4", 1200), ("a5", 800)),
            Party("B", 0, ("b1", 1500), ("b2", 100)),
        };

        ProportionalResult result = ElectionRules.Proportional(5, parties);

        Assert.Equal(2320, result.ElectoralQuotient);
        Assert.Equal(0, result.PartyQuotients["B"]);
        Assert.False(result.IsElected("b1"));
        Assert.True(result.IsElected("a5"));
        Assert.Equal(5, result.SeatsOf("A"));
    }

    [Fact]
    public void CandidatoAbaixoDeDezPorCento_NaoOcupaVagaDoQuociente()
    {
        // 4 vagas. Válidos = 6050 + 2950 + 1000 = 10000. QE = 2500. 10% = 250; 20% = 500; 80% = 2000.
        // QP: A = 6050/2500 = 2,42 → 2; B = 2950/2500 = 1,18 → 1; C = 0.
        // 1ª etapa: A elege a1; a2 tem 50 < 250 e é barrado (1 vaga de A vira sobra). B elege b1.
        // 2ª etapa: A não tem candidato com 20% (a2 = 50); C fica de fora (1000 < 2000).
        //   B: 2950/(1+1) = 1475 → b2 (900 ≥ 500). Depois B: 2950/3 = 983,3 → b3 (550 ≥ 500).
        var parties = new[]
        {
            Party("A", 1000, ("a1", 5000), ("a2", 50)),
            Party("B", 0, ("b1", 1500), ("b2", 900), ("b3", 550)),
            Party("C", 0, ("c1", 900), ("c2", 100)),
        };

        ProportionalResult result = ElectionRules.Proportional(4, parties);

        Assert.Equal(2, result.PartyQuotients["A"]);
        Assert.Equal(1, result.SeatsOf("A"));
        Assert.False(result.IsElected("a2"));
        Assert.Equal(["a1", "b1", "b2", "b3"], result.Elected.Select(e => e.CandidateId));
    }

    [Fact]
    public void TerceiraEtapa_AbertaATodosOsPartidosSemPisoIndividual()
    {
        // 5 vagas. Válidos = 3400 + 2600 + 1800 + 1400 = 9200. QE = 1840. 20% = 368; 80% = 1472.
        // QP: A = 3400/1840 = 1,85 → 1; B = 2600/1840 = 1,41 → 1; C = 0; D = 0.
        // 1ª etapa: a1, b1. Restam 3.
        // 2ª etapa (D fica de fora: 1400 < 1472):
        //   A = 3400/2 = 1700 (a2 = 400 ≥ 368); C = 1800/1 = 1800 (c1 = 1500) → c1.
        //   A = 1700; C não tem mais candidato com 20% (c2 = 300) → a2.
        //   Ninguém mais atende às duas exigências → 3ª etapa.
        // 3ª etapa: C = 1800/2 = 900; D = 1400/1 = 1400 → d1, mesmo com D abaixo de 80%.
        var parties = new[]
        {
            Party("A", 0, ("a1", 3000), ("a2", 400)),
            Party("B", 0, ("b1", 2600)),
            Party("C", 0, ("c1", 1500), ("c2", 300)),
            Party("D", 0, ("d1", 900), ("d2", 500)),
        };

        ProportionalResult result = ElectionRules.Proportional(5, parties);

        Assert.Equal(1840, result.ElectoralQuotient);
        Assert.Equal(["a1", "b1", "c1", "a2", "d1"], result.Elected.Select(e => e.CandidateId));
        Assert.Equal(SeatStage.FinalRemainder, result.Elected.Single(e => e.CandidateId == "d1").Stage);
    }

    [Fact]
    public void NenhumPartidoAtingeOQuociente_TodasAsVagasViramSobras()
    {
        // 3 vagas. Válidos = 900 + 800 + 700 + 600 = 3000. QE = 1000. 20% = 200; 80% = 800.
        // Todos os QP = 0. 2ª etapa só com A (900) e B (800); C e D abaixo de 800.
        //   A = 900/1; B = 800/1 → a1. A = 900/2 = 450, mas a2 = 100 < 200; B = 800 → b1.
        //   B = 800/2 = 400 → b2 (300 ≥ 200).
        // d1, com 600 votos, fica de fora (a regra dos "mais votados" do art. 111 foi derrubada pelo STF).
        var parties = new[]
        {
            Party("A", 0, ("a1", 800), ("a2", 100)),
            Party("B", 0, ("b1", 500), ("b2", 300)),
            Party("C", 0, ("c1", 400), ("c2", 300)),
            Party("D", 0, ("d1", 600)),
        };

        ProportionalResult result = ElectionRules.Proportional(3, parties);

        Assert.All(result.PartyQuotients.Values, qp => Assert.Equal(0, qp));
        Assert.Equal(["a1", "b1", "b2"], result.Elected.Select(e => e.CandidateId));
        Assert.All(result.Elected, e => Assert.Equal(SeatStage.Remainder, e.Stage));
        Assert.False(result.IsElected("d1"));
    }

    [Fact]
    public void SobraObtidaENaoPreenchida_ContaNoDivisorDasMediasSeguintes()
    {
        // 4 vagas. Válidos = 3200 + 2500 + 1400 = 7100. QE = 1775. 10% = 177,5; 20% = 355; 80% = 1420.
        // QP: A = 3200/1775 = 1,80 → 1; B = 2500/1775 = 1,41 → 1; C = 0. 1ª etapa: a1, b1. Restam 2.
        // 2ª etapa (C fica de fora: 1400 < 1420):
        //   A = 3200/(1+1) = 1600; B = 2500/(1+1) = 1250 → A obtém a sobra, mas a2 tem 200 < 355:
        //   sobra obtida e NÃO preenchida; o divisor de A passa a 1+1+1 = 3.
        //   A = 3200/3 = 1066,7; B = 1250 → B elege b2 (700 ≥ 355).
        //   Ninguém mais tem candidato com 20% → 3ª etapa.
        // 3ª etapa: A = 3200/3 = 1066,7 (contando a sobra não preenchida); B = 2500/3 = 833,3; C = 1400/1 = 1400
        //   → C elege c1. Se a sobra não preenchida não contasse, A teria 3200/2 = 1600 e elegeria a2.
        var parties = new[]
        {
            Party("A", 0, ("a1", 3000), ("a2", 200)),
            Party("B", 0, ("b1", 1800), ("b2", 700)),
            Party("C", 0, ("c1", 1300), ("c2", 100)),
        };

        ProportionalResult result = ElectionRules.Proportional(4, parties);

        Assert.Equal(1775, result.ElectoralQuotient);
        Assert.Equal(1, result.UnfilledRemainders["A"]);
        Assert.Equal(["a1", "b1", "b2", "c1"], result.Elected.Select(e => e.CandidateId));
        Assert.Equal(SeatStage.FinalRemainder, result.Elected.Single(e => e.CandidateId == "c1").Stage);
        Assert.False(result.IsElected("a2"));
    }

    [Fact]
    public void QuocientePartidarioNaoPreenchido_ContaNoDivisor()
    {
        // Mesmo caso de CandidatoAbaixoDeDezPorCento: A tem QP = 2, mas só elege a1.
        // Na 2ª etapa a média de A é 6050/(2+1) = 2016,7, e não 6050/(1+1): o QP inteiro conta.
        // A vence as médias sem candidato com 20% e acumula sobras não preenchidas:
        //   2016,7 > 1475 (B) → não preenchida; 6050/4 = 1512,5 > 1475 → não preenchida;
        //   6050/5 = 1210 < 1475 → b2; 1210 > 2950/3 = 983,3 → não preenchida;
        //   6050/6 = 1008,3 > 983,3 → não preenchida; 6050/7 = 864,3 < 983,3 → b3. Total: 4.
        var parties = new[]
        {
            Party("A", 1000, ("a1", 5000), ("a2", 50)),
            Party("B", 0, ("b1", 1500), ("b2", 900), ("b3", 550)),
            Party("C", 0, ("c1", 900), ("c2", 100)),
        };

        ProportionalResult result = ElectionRules.Proportional(4, parties);

        Assert.Equal(4, result.UnfilledRemainders["A"]);
        Assert.Equal(["a1", "b1", "b2", "b3"], result.Elected.Select(e => e.CandidateId));
    }

    [Fact]
    public void EmpateDeMedias_FavoreceOPartidoMaisVotado()
    {
        // 2 vagas. Válidos = 2000 + 1000 = 3000. QE = 1500. 20% = 300; 80% = 1200.
        // QP: A = 2000/1500 = 1,33 → 1; B = 0. 1ª etapa: a1.
        // 2ª etapa: A não tem candidato com 20% (a2 = 100); B fica de fora (1000 < 1200).
        // 3ª etapa: A = 2000/2 = 1000; B = 1000/1 = 1000 → empate de médias.
        //   Desempate pela votação do partido: A (2000) > B (1000) → a2.
        var parties = new[]
        {
            Party("A", 0, ("a1", 1900), ("a2", 100)),
            Party("B", 0, ("b1", 1000)),
        };

        ProportionalResult result = ElectionRules.Proportional(2, parties);

        Assert.Equal(1500, result.ElectoralQuotient);
        Assert.Equal(["a1", "a2"], result.Elected.Select(e => e.CandidateId));
        Assert.Equal(SeatStage.FinalRemainder, result.Elected[1].Stage);
    }

    [Fact]
    public void EmpateDeMediasEDeVotos_FavoreceOCandidatoMaisIdoso()
    {
        // 1 vaga. Válidos = 1000 + 1000 = 2000. QE = 2000. QP = 0 para os dois.
        // 2ª etapa: ninguém tem 80% (1600). 3ª etapa: médias 1000 e 1000, votos 1000 e 1000.
        // Desempate pelo candidato mais idoso: b1 (1970) é mais velho que a1 (1985).
        var parties = new[]
        {
            new PartyList("A", 0, [new CandidateVotes("a1", 1000, new DateOnly(1985, 5, 1))]),
            new PartyList("B", 0, [new CandidateVotes("b1", 1000, new DateOnly(1970, 5, 1))]),
        };

        ProportionalResult result = ElectionRules.Proportional(1, parties);

        Assert.Equal(["b1"], result.Elected.Select(e => e.CandidateId));
    }

    [Fact]
    public void EmpateNaListaDoPartido_ElegeOMaisIdoso()
    {
        // 1 vaga, QE = 2000, QP A = 1. a1 e a2 empatados com 1000; a2 é mais idoso (art. 110).
        var parties = new[]
        {
            new PartyList("A", 0,
            [
                new CandidateVotes("a1", 1000, new DateOnly(1990, 1, 1)),
                new CandidateVotes("a2", 1000, new DateOnly(1960, 1, 1)),
            ]),
        };

        ProportionalResult result = ElectionRules.Proportional(1, parties);

        Assert.Equal(["a2"], result.Elected.Select(e => e.CandidateId));
    }

    [Fact]
    public void FaltaDeCandidatos_DeixaVagasEmAberto()
    {
        var parties = new[] { Party("A", 500, ("a1", 1000)) };

        ProportionalResult result = ElectionRules.Proportional(3, parties);

        Assert.Single(result.Elected);
        Assert.Equal(2, result.VacantSeats);
    }

    [Fact]
    public void EntradaInvalida_LancaExcecao()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ElectionRules.Proportional(0, [Party("A", 0, ("a1", 1))]));
        Assert.Throws<ArgumentException>(() => ElectionRules.Proportional(1, [Party("A", 0, ("x", 1)), Party("B", 0, ("x", 1))]));
        Assert.Throws<ArgumentException>(() => ElectionRules.Proportional(1, [Party("A", 0, ("a1", 1)), Party("A", 0, ("a2", 1))]));
    }
}
