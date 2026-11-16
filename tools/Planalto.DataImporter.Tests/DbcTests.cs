using System.Text;
using Planalto.DataImporter.Rd;

namespace Planalto.DataImporter.Tests;

public class DbcTests
{
    [Fact]
    public void Blast_VetorDeReferencia()
    {
        // Exemplo do cabeçalho do blast.c.
        byte[] comprimido = [0x00, 0x04, 0x82, 0x24, 0x25, 0x8f, 0x80, 0x7f];

        Assert.Equal("AIAIAIAIAIAIA", Encoding.ASCII.GetString(BlastStream.Decompress(comprimido)));
    }

    [Fact]
    public void Blast_FluxoTruncado_Falha()
    {
        byte[] truncado = [0x00, 0x04, 0x82, 0x24];

        Assert.Throws<EndOfStreamException>(() => BlastStream.Decompress(truncado));
    }

    [Fact]
    public void Blast_JanelaLonga_CopiasAtravessamOFimDaJanela()
    {
        // Literais seguidos de cópias com distâncias de 1 a 4096 e comprimentos de 2 a 518, por várias
        // janelas de 4096 bytes: cobre a volta da janela, a cópia sobreposta e a distância máxima.
        var rng = new Random(20261116);
        var esperado = new List<byte>();
        var codificador = new ImplodeEncoder(dictionaryBits: 6);

        for (int i = 0; i < 5000; i++)
        {
            byte b = (byte)rng.Next(256);
            esperado.Add(b);
            codificador.Literal(b);
        }

        int[] comprimentos = [2, 3, 4, 9, 10, 11, 12, 15, 16, 23, 24, 39, 40, 71, 72, 135, 136, 263, 264, 400, 518];
        int[] distancias = [1, 2, 3, 64, 65, 255, 256, 1000, 4095, 4096];
        for (int rodada = 0; rodada < 400; rodada++)
        {
            int comprimento = comprimentos[rng.Next(comprimentos.Length)];
            int distancia = distancias[rng.Next(distancias.Length)];
            if (comprimento == 2)
            {
                // Com comprimento 2 a distância só tem 2 bits baixos: no máximo 256.
                distancia = Math.Min(distancia, 256);
            }

            codificador.Copia(comprimento, distancia);
            for (int i = 0; i < comprimento; i++)
            {
                esperado.Add(esperado[^distancia]);
            }

            byte b = (byte)rng.Next(256);
            esperado.Add(b);
            codificador.Literal(b);
        }

        byte[] comprimido = codificador.Fim();
        Assert.True(esperado.Count > 4096 * 8);

        Assert.Equal(esperado.ToArray(), BlastStream.Decompress(comprimido));

        // Lido em pedaços pequenos, como faz o leitor de registros.
        using var blast = new BlastStream(new MemoryStream(comprimido));
        var saida = new MemoryStream();
        var pedaco = new byte[37];
        int lidos;
        while ((lidos = blast.Read(pedaco, 0, pedaco.Length)) > 0)
        {
            saida.Write(pedaco, 0, lidos);
        }

        Assert.Equal(esperado.ToArray(), saida.ToArray());
    }

    [Fact]
    public void Dbf_LeColunasPeloNome()
    {
        byte[] dbf = DbfSintetico(
            [("CODMUNRES", 7), ("IDADE", 3), ("CAUSABAS", 4)],
            [["1200401", "401", "X954"], ["120040", "", "I219"], ["1200013", "205", "P220"]],
            apagados: [1]);

        List<string[]> linhas = Dbc.ReadDbfColumns(new MemoryStream(dbf), "sintetico.dbf", "causabas", "CODMUNRES").ToList();

        // O registro marcado como apagado não aparece; a ordem é a das colunas pedidas.
        Assert.Equal(2, linhas.Count);
        Assert.Equal(["X954", "1200401"], linhas[0]);
        Assert.Equal(["P220", "1200013"], linhas[1]);
    }

    [Fact]
    public void Dbf_ColunaInexistente_FalhaComONomeDaColuna()
    {
        byte[] dbf = DbfSintetico([("A", 2)], [["1"]], apagados: []);

        ImportException erro = Assert.Throws<ImportException>(
            () => Dbc.ReadDbfColumns(new MemoryStream(dbf), "sintetico.dbf", "B").ToList());
        Assert.Contains("'B'", erro.Message);
    }

    [Fact]
    public void Dbc_CabecalhoSemCompressaoECorpoComprimido()
    {
        byte[] dbf = DbfSintetico(
            [("CODUFMUN", 6), ("QT_EXIST", 4)],
            [["120040", "12"], ["120040", "3"], ["120020", "40"]],
            apagados: []);
        int tamanhoCabecalho = dbf[8] | (dbf[9] << 8);

        var codificador = new ImplodeEncoder(dictionaryBits: 4);
        foreach (byte b in dbf.AsSpan(tamanhoCabecalho))
        {
            codificador.Literal(b);
        }

        string caminho = Path.Combine(Path.GetTempPath(), $"planalto_teste_{Guid.NewGuid():N}.dbc");
        try
        {
            using (FileStream arquivo = File.Create(caminho))
            {
                arquivo.Write(dbf, 0, tamanhoCabecalho);
                arquivo.Write([0, 0, 0, 0]); // CRC32, que o leitor não confere
                arquivo.Write(codificador.Fim());
            }

            DbfHeader cabecalho = Dbc.ReadHeader(caminho);
            Assert.Equal(3, cabecalho.RecordCount);
            Assert.Equal(["CODUFMUN", "QT_EXIST"], cabecalho.Fields.Select(f => f.Name));

            List<string[]> linhas = Dbc.ReadColumns(caminho, "CODUFMUN", "QT_EXIST").ToList();
            Assert.Equal(3, linhas.Count);
            Assert.Equal(["120020", "40"], linhas[2]);
        }
        finally
        {
            File.Delete(caminho);
        }
    }

    /// <summary>Monta um DBF (dBase III) mínimo, só com campos de texto.</summary>
    private static byte[] DbfSintetico((string Nome, int Tamanho)[] campos, string[][] registros, int[] apagados)
    {
        int tamanhoCabecalho = 32 + 32 * campos.Length + 1;
        int tamanhoRegistro = 1 + campos.Sum(c => c.Tamanho);
        using var saida = new MemoryStream();
        using var w = new BinaryWriter(saida, Encoding.Latin1);

        w.Write((byte)0x03);
        w.Write(new byte[] { 126, 11, 16 });
        w.Write((uint)registros.Length);
        w.Write((ushort)tamanhoCabecalho);
        w.Write((ushort)tamanhoRegistro);
        w.Write(new byte[20]);

        foreach ((string nome, int tamanho) in campos)
        {
            var descritor = new byte[32];
            Encoding.Latin1.GetBytes(nome).CopyTo(descritor, 0);
            descritor[11] = (byte)'C';
            descritor[16] = (byte)tamanho;
            w.Write(descritor);
        }

        w.Write((byte)0x0D);

        for (int r = 0; r < registros.Length; r++)
        {
            w.Write((byte)(apagados.Contains(r) ? '*' : ' '));
            for (int c = 0; c < campos.Length; c++)
            {
                w.Write(Encoding.Latin1.GetBytes(registros[r][c].PadRight(campos[c].Tamanho)));
            }
        }

        w.Write((byte)0x1A);
        w.Flush();
        return saida.ToArray();
    }

    /// <summary>
    /// Codificador "implode" só para os testes: literais sem código de Huffman e cópias com os códigos
    /// de comprimento e de distância do formato, montados aqui de forma independente do descompressor.
    /// </summary>
    private sealed class ImplodeEncoder
    {
        // Comprimento em bits do código de cada símbolo (tabelas do formato, por extenso).
        private static readonly int[] BitsDoComprimento = [2, 3, 3, 3, 4, 4, 4, 5, 5, 5, 5, 6, 6, 6, 7, 7];

        private static readonly int[] BaseDoComprimento = [3, 2, 4, 5, 6, 7, 8, 9, 10, 12, 16, 24, 40, 72, 136, 264];

        private static readonly int[] ExtraDoComprimento = [0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8];

        private static readonly int[] BitsDaDistancia =
        [
            2, 4, 4, 5, 5, 5, 5, 6, 6, 6, 6, 6, 6, 6, 6, 6,
            6, 6, 6, 6, 6, 6, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
            7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
            8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
        ];

        private static readonly (int Codigo, int Bits)[] CodigosDeComprimento = Canonico(BitsDoComprimento);

        private static readonly (int Codigo, int Bits)[] CodigosDeDistancia = Canonico(BitsDaDistancia);

        private readonly List<byte> _bytes = [];
        private readonly int _dictionaryBits;
        private int _acumulador;
        private int _bitsNoAcumulador;

        public ImplodeEncoder(int dictionaryBits)
        {
            _dictionaryBits = dictionaryBits;
            _bytes.Add(0); // literais sem código
            _bytes.Add((byte)dictionaryBits);
        }

        public void Literal(byte b)
        {
            Bits(0, 1);
            Bits(b, 8);
        }

        public void Copia(int comprimento, int distancia)
        {
            // O símbolo 1 é o do comprimento 2; os demais vão em ordem crescente de base.
            int simbolo = 1;
            if (comprimento != 2)
            {
                simbolo = 15;
                while (simbolo == 1 || BaseDoComprimento[simbolo] > comprimento)
                {
                    simbolo--;
                }
            }

            Bits(1, 1);
            Huffman(CodigosDeComprimento[simbolo]);
            Bits(comprimento - BaseDoComprimento[simbolo], ExtraDoComprimento[simbolo]);

            int baixos = comprimento == 2 ? 2 : _dictionaryBits;
            Huffman(CodigosDeDistancia[(distancia - 1) >> baixos]);
            Bits((distancia - 1) & ((1 << baixos) - 1), baixos);
        }

        public byte[] Fim()
        {
            // Código de fim: comprimento 519 (símbolo 15 com os 8 bits extras em 1).
            Bits(1, 1);
            Huffman(CodigosDeComprimento[15]);
            Bits(255, 8);
            if (_bitsNoAcumulador > 0)
            {
                _bytes.Add((byte)_acumulador);
            }

            return [.. _bytes];
        }

        private static (int Codigo, int Bits)[] Canonico(int[] bits)
        {
            var codigos = new (int, int)[bits.Length];
            int codigo = 0;
            int anterior = 0;
            foreach (int simbolo in Enumerable.Range(0, bits.Length).OrderBy(s => bits[s]).ThenBy(s => s))
            {
                codigo <<= bits[simbolo] - anterior;
                anterior = bits[simbolo];
                codigos[simbolo] = (codigo++, bits[simbolo]);
            }

            return codigos;
        }

        /// <summary>Código de Huffman: do bit mais alto para o mais baixo, com os bits invertidos.</summary>
        private void Huffman((int Codigo, int Bits) codigo)
        {
            for (int i = codigo.Bits - 1; i >= 0; i--)
            {
                Bits(((codigo.Codigo >> i) & 1) ^ 1, 1);
            }
        }

        private void Bits(int valor, int quantidade)
        {
            _acumulador |= valor << _bitsNoAcumulador;
            _bitsNoAcumulador += quantidade;
            while (_bitsNoAcumulador >= 8)
            {
                _bytes.Add((byte)_acumulador);
                _acumulador >>= 8;
                _bitsNoAcumulador -= 8;
            }
        }
    }
}
