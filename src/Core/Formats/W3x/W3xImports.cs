using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WorldEdit.Core.Formats.W3x;

public sealed class W3xImport
{
    public byte Flags = 8;
    public string Path = "";

    public bool IsCustomPath => Flags is 10 or 13;

    public string FullPath => IsCustomPath ? Path : "war3mapImported\\" + Path;
}

public sealed class W3xImports
{
    public const string FileName = "war3map.imp";

    public int Version { get; private set; } = 1;
    public byte[] RawBytes { get; private set; }

    public List<W3xImport> Entries { get; } = new();

    public static W3xImports Parse(byte[] data)
    {
        if (data == null || data.Length < 8)
        {
            throw new InvalidDataException("war3map.imp 太短");
        }

        var reader = new W3xBinaryReader(data);
        var result = new W3xImports
        {
            RawBytes = data,
            Version = reader.I32(),
        };
        int count = reader.Count(100000);
        for (int i = 0; i < count; i++)
        {
            result.Entries.Add(new W3xImport
            {
                Flags = reader.U8(),
                Path = reader.NullString(Encoding.UTF8),
            });
        }
        return result;
    }

    public byte[] ToBytes()
    {
        var writer = new W3xBinaryWriter();
        writer.I32(Version);
        writer.I32(Entries.Count);
        foreach (W3xImport entry in Entries)
        {
            writer.U8(entry.Flags);
            writer.NullString(entry.Path);
        }
        return writer.ToArray();
    }

    public bool RoundTrips()
    {
        byte[] encoded = ToBytes();
        return RawBytes != null && encoded.AsSpan().SequenceEqual(RawBytes);
    }
}
