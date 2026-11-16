namespace Planalto.DataImporter.Rd;

/// <summary>
/// Descompressor do formato "implode" da PKWare Data Compression Library, usado no corpo dos arquivos .dbc
/// do DATASUS. É um porte para C# do blast.c (versão 1.3, de 24/08/2013), de Mark Adler, distribuído em
/// zlib/contrib/blast sob a licença zlib:
///
///   Copyright (C) 2003, 2012, 2013 Mark Adler
///
///   This software is provided 'as-is', without any express or implied warranty. In no event will the
///   authors be held liable for any damages arising from the use of this software.
///
///   Permission is granted to anyone to use this software for any purpose, including commercial
///   applications, and to alter it and redistribute it freely, subject to the following restrictions:
///
///   1. The origin of this software must not be misrepresented; you must not claim that you wrote the
///      original software. If you use this software in a product, an acknowledgment in the product
///      documentation would be appreciated but is not required.
///   2. Altered source versions must be plainly marked as such, and must not be misrepresented as being
///      the original software.
///   3. This notice may not be removed or altered from any source distribution.
///
/// Esta é uma versão alterada: reescrita em C# e convertida de "empurra a saída" para um Stream de leitura
/// que descomprime sob demanda, uma janela de 4096 bytes por vez.
/// </summary>
public sealed class BlastStream : Stream
{
    private const int MaxBits = 13;
    private const int MaxWindow = 4096;

    private sealed class Huffman(int symbols)
    {
        public short[] Count { get; } = new short[MaxBits + 1];

        public short[] Symbol { get; } = new short[symbols];
    }

    // Comprimentos dos códigos em forma compacta (repetições no nibble alto), como no formato original.
    private static readonly byte[] LiteralLengths =
    [
        11, 124, 8, 7, 28, 7, 188, 13, 76, 4, 10, 8, 12, 10, 12, 10, 8, 23, 8,
        9, 7, 6, 7, 8, 7, 6, 55, 8, 23, 24, 12, 11, 7, 9, 11, 12, 6, 7, 22, 5,
        7, 24, 6, 11, 9, 6, 7, 22, 7, 11, 38, 7, 9, 8, 25, 11, 8, 11, 9, 12,
        8, 12, 5, 38, 5, 38, 5, 11, 7, 5, 6, 21, 6, 10, 53, 8, 7, 24, 10, 27,
        44, 253, 253, 253, 252, 252, 252, 13, 12, 45, 12, 45, 12, 61, 12, 45,
        44, 173,
    ];

    private static readonly byte[] LengthLengths = [2, 35, 36, 53, 38, 23];

    private static readonly byte[] DistanceLengths = [2, 20, 53, 230, 247, 151, 248];

    private static readonly short[] LengthBase = [3, 2, 4, 5, 6, 7, 8, 9, 10, 12, 16, 24, 40, 72, 136, 264];

    private static readonly byte[] LengthExtra = [0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8];

    private static readonly Huffman LiteralCode = Construct(LiteralLengths, 256);

    private static readonly Huffman LengthCode = Construct(LengthLengths, 16);

    private static readonly Huffman DistanceCode = Construct(DistanceLengths, 64);

    private readonly Stream _input;
    private readonly bool _leaveOpen;
    private readonly byte[] _inputBuffer = new byte[64 * 1024];
    private int _inputPosition;
    private int _inputLength;
    private int _bitBuffer;
    private int _bitCount;

    private readonly byte[] _window = new byte[MaxWindow];
    private int _next;
    private int _served;
    private bool _firstWindow = true;
    private bool _headerRead;
    private bool _finished;
    private int _codedLiterals;
    private int _dictionaryBits;
    private int _pendingLength;
    private int _pendingDistance;

    public BlastStream(Stream input, bool leaveOpen = false)
    {
        _input = input;
        _leaveOpen = leaveOpen;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>Descomprime um bloco inteiro em memória; para dados pequenos e testes.</summary>
    public static byte[] Decompress(byte[] compressed)
    {
        using var blast = new BlastStream(new MemoryStream(compressed, writable: false));
        using var output = new MemoryStream();
        blast.CopyTo(output);
        return output.ToArray();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (buffer.Length == 0)
        {
            return 0;
        }

        if (_served == _next)
        {
            if (_finished)
            {
                return 0;
            }

            Fill();
            if (_served == _next)
            {
                return 0;
            }
        }

        int n = Math.Min(buffer.Length, _next - _served);
        _window.AsSpan(_served, n).CopyTo(buffer);
        _served += n;
        return n;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen)
        {
            _input.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Descomprime até encher a janela ou chegar ao código de fim.</summary>
    private void Fill()
    {
        if (!_headerRead)
        {
            _codedLiterals = Bits(8);
            if (_codedLiterals > 1)
            {
                throw new InvalidDataException("Fluxo implode inválido: indicador de literais fora de 0 e 1.");
            }

            _dictionaryBits = Bits(8);
            if (_dictionaryBits is < 4 or > 6)
            {
                throw new InvalidDataException("Fluxo implode inválido: tamanho de dicionário fora de 4 a 6.");
            }

            _headerRead = true;
        }

        if (_next == MaxWindow)
        {
            _next = 0;
            _served = 0;
            _firstWindow = false;
        }

        while (_next < MaxWindow)
        {
            if (_pendingLength > 0)
            {
                // A cópia pode sobrepor a própria saída (distância menor que o comprimento): byte a byte.
                int copy = Math.Min(_pendingLength, MaxWindow - _next);
                _pendingLength -= copy;
                int from = _next - _pendingDistance;
                while (copy-- > 0)
                {
                    _window[_next++] = _window[from++ & (MaxWindow - 1)];
                }

                continue;
            }

            if (Bits(1) != 0)
            {
                int symbol = Decode(LengthCode);
                int length = LengthBase[symbol] + Bits(LengthExtra[symbol]);
                if (length == 519)
                {
                    _finished = true;
                    return;
                }

                int low = length == 2 ? 2 : _dictionaryBits;
                int distance = Decode(DistanceCode) << low;
                distance += Bits(low);
                distance++;
                if (_firstWindow && distance > _next)
                {
                    throw new InvalidDataException("Fluxo implode inválido: distância antes do início dos dados.");
                }

                _pendingLength = length;
                _pendingDistance = distance;
            }
            else
            {
                int literal = _codedLiterals != 0 ? Decode(LiteralCode) : Bits(8);
                _window[_next++] = (byte)literal;
            }
        }
    }

    private int NextByte()
    {
        if (_inputPosition == _inputLength)
        {
            _inputLength = _input.Read(_inputBuffer, 0, _inputBuffer.Length);
            _inputPosition = 0;
            if (_inputLength <= 0)
            {
                _inputLength = 0;
                throw new EndOfStreamException("Fluxo implode truncado: a entrada acabou antes do código de fim.");
            }
        }

        return _inputBuffer[_inputPosition++];
    }

    private int Bits(int need)
    {
        int value = _bitBuffer;
        while (_bitCount < need)
        {
            value |= NextByte() << _bitCount;
            _bitCount += 8;
        }

        _bitBuffer = value >> need;
        _bitCount -= need;
        return value & ((1 << need) - 1);
    }

    /// <summary>
    /// Decodifica um símbolo de Huffman canônico. Os bits vêm invertidos em relação à ordem "natural"
    /// (o primeiro código do menor comprimento é todo de uns), por isso o XOR.
    /// </summary>
    private int Decode(Huffman h)
    {
        int bitBuffer = _bitBuffer;
        int left = _bitCount;
        int code = 0;
        int first = 0;
        int index = 0;
        int length = 1;
        while (true)
        {
            while (left-- > 0)
            {
                code |= (bitBuffer & 1) ^ 1;
                bitBuffer >>= 1;
                int count = h.Count[length];
                if (code < first + count)
                {
                    _bitBuffer = bitBuffer;
                    _bitCount = (_bitCount - length) & 7;
                    return h.Symbol[index + (code - first)];
                }

                index += count;
                first += count;
                first <<= 1;
                code <<= 1;
                length++;
            }

            left = MaxBits + 1 - length;
            if (left == 0)
            {
                break;
            }

            bitBuffer = NextByte();
            if (left > 8)
            {
                left = 8;
            }
        }

        throw new InvalidDataException("Fluxo implode inválido: código de Huffman inexistente.");
    }

    private static Huffman Construct(byte[] compact, int symbols)
    {
        var h = new Huffman(symbols);
        var lengths = new short[256];
        int n = 0;
        foreach (byte rep in compact)
        {
            int times = (rep >> 4) + 1;
            int length = rep & 15;
            while (times-- > 0)
            {
                lengths[n++] = (short)length;
            }
        }

        for (int symbol = 0; symbol < n; symbol++)
        {
            h.Count[lengths[symbol]]++;
        }

        var offsets = new short[MaxBits + 1];
        for (int length = 1; length < MaxBits; length++)
        {
            offsets[length + 1] = (short)(offsets[length] + h.Count[length]);
        }

        for (int symbol = 0; symbol < n; symbol++)
        {
            if (lengths[symbol] != 0)
            {
                h.Symbol[offsets[lengths[symbol]]++] = (short)symbol;
            }
        }

        return h;
    }
}
