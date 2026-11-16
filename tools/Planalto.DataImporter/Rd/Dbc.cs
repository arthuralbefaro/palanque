using System.Buffers.Binary;
using System.Text;

namespace Planalto.DataImporter.Rd;

/// <summary>Campo de um DBF: nome, tipo (C, N, D...), tamanho em bytes e posição dentro do registro.</summary>
public sealed record DbfField(string Name, char Type, int Length, int Offset);

/// <summary>Cabeçalho de um DBF (dBase III): quantidade e tamanho dos registros e os campos.</summary>
public sealed record DbfHeader(int RecordCount, int HeaderSize, int RecordSize, IReadOnlyList<DbfField> Fields);

/// <summary>
/// Leitor dos arquivos .dbc do DATASUS (SIM, SINASC, CNES...). Um .dbc é um .dbf cujo cabeçalho vai sem
/// compressão, seguido de 4 bytes de CRC32 e do corpo de registros comprimido com o "implode" da PKWare
/// (ver <see cref="BlastStream"/>). Os registros são descomprimidos e percorridos em sequência, sem
/// carregar o arquivo inteiro.
/// </summary>
public static class Dbc
{
    private const int DescriptorSize = 32;
    private const byte HeaderTerminator = 0x0D;
    private const byte Deleted = (byte)'*';

    private static readonly Encoding Latin1 = Encoding.Latin1;

    /// <summary>Lê só o cabeçalho DBF de um .dbc (ou de um .dbf puro).</summary>
    public static DbfHeader ReadHeader(string path)
    {
        using FileStream file = File.OpenRead(path);
        return ReadHeader(file, Path.GetFileName(path));
    }

    /// <summary>
    /// Percorre os registros de um .dbc e devolve, de cada um, só as colunas pedidas, na ordem pedida,
    /// sem os espaços de preenchimento. Registros marcados como apagados são pulados.
    /// </summary>
    public static IEnumerable<string[]> ReadColumns(string path, params string[] columns)
    {
        string name = Path.GetFileName(path);
        using FileStream file = File.OpenRead(path);
        DbfHeader header = ReadHeader(file, name);

        // Entre o cabeçalho e o fluxo comprimido há 4 bytes de CRC32.
        file.Seek(header.HeaderSize + 4, SeekOrigin.Begin);
        using var records = new BlastStream(file, leaveOpen: true);
        foreach (string[] row in ReadRecords(header, records, name, columns))
        {
            yield return row;
        }
    }

    /// <summary>O mesmo que <see cref="ReadColumns"/>, para um .dbf sem compressão.</summary>
    public static IEnumerable<string[]> ReadDbfColumns(Stream dbf, string name, params string[] columns)
    {
        DbfHeader header = ReadHeader(dbf, name);
        return ReadRecords(header, dbf, name, columns);
    }

    private static DbfHeader ReadHeader(Stream stream, string name)
    {
        var prefix = new byte[DescriptorSize];
        if (stream.ReadAtLeast(prefix, prefix.Length, throwOnEndOfStream: false) < prefix.Length)
        {
            throw new ImportException($"{name}: arquivo menor que um cabeçalho DBF.");
        }

        int recordCount = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(prefix.AsSpan(4)));
        int headerSize = BinaryPrimitives.ReadUInt16LittleEndian(prefix.AsSpan(8));
        int recordSize = BinaryPrimitives.ReadUInt16LittleEndian(prefix.AsSpan(10));
        if (headerSize < DescriptorSize + 1 || recordSize < 1)
        {
            throw new ImportException($"{name}: cabeçalho DBF inválido (cabeçalho de {headerSize} bytes, registro de {recordSize}).");
        }

        var descriptors = new byte[headerSize - DescriptorSize];
        if (stream.ReadAtLeast(descriptors, descriptors.Length, throwOnEndOfStream: false) < descriptors.Length)
        {
            throw new ImportException($"{name}: cabeçalho DBF truncado.");
        }

        var fields = new List<DbfField>();
        int offset = 1; // o primeiro byte do registro é a marca de apagado
        for (int at = 0; at + DescriptorSize <= descriptors.Length && descriptors[at] != HeaderTerminator; at += DescriptorSize)
        {
            ReadOnlySpan<byte> descriptor = descriptors.AsSpan(at, DescriptorSize);
            int end = descriptor[..11].IndexOf((byte)0);
            string fieldName = Latin1.GetString(descriptor[..(end < 0 ? 11 : end)]).Trim();
            int length = descriptor[16];
            fields.Add(new DbfField(fieldName, (char)descriptor[11], length, offset));
            offset += length;
        }

        if (offset != recordSize)
        {
            throw new ImportException($"{name}: os campos somam {offset} bytes, mas o registro tem {recordSize}.");
        }

        return new DbfHeader(recordCount, headerSize, recordSize, fields);
    }

    private static IEnumerable<string[]> ReadRecords(DbfHeader header, Stream records, string name, string[] columns)
    {
        var selected = new DbfField[columns.Length];
        for (int i = 0; i < columns.Length; i++)
        {
            selected[i] = header.Fields.FirstOrDefault(f => string.Equals(f.Name, columns[i], StringComparison.OrdinalIgnoreCase))
                ?? throw new ImportException($"{name}: coluna '{columns[i]}' não encontrada. Colunas: {string.Join(", ", header.Fields.Select(f => f.Name))}.");
        }

        var record = new byte[header.RecordSize];
        for (int n = 0; n < header.RecordCount; n++)
        {
            int read;
            try
            {
                read = records.ReadAtLeast(record, record.Length, throwOnEndOfStream: false);
            }
            catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException)
            {
                throw new ImportException($"{name}: {ex.Message} (registro {n + 1} de {header.RecordCount}).");
            }

            if (read < record.Length)
            {
                throw new ImportException($"{name}: os dados acabaram no registro {n + 1} de {header.RecordCount}.");
            }

            if (record[0] == Deleted)
            {
                continue;
            }

            var row = new string[selected.Length];
            for (int i = 0; i < selected.Length; i++)
            {
                row[i] = Latin1.GetString(record.AsSpan(selected[i].Offset, selected[i].Length).Trim((byte)' '));
            }

            yield return row;
        }
    }
}
