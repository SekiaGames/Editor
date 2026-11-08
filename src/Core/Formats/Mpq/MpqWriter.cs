using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace WorldEdit.Core.Formats.Mpq;

public sealed class MpqWriter
{
    private const uint FlagCompress = 0x00000200;
    private const uint FlagExists = 0x80000000;

    private sealed record Entry(uint Slot, uint NameA, uint NameB, byte[] Data, bool Compress, string Name);

    private readonly List<Entry> _entries = new();

    public void Add(string name, byte[] data, bool compress = true)
    {
        _entries.Add(new Entry(
            MpqCrypto.Hash(name, MpqCrypto.HashTableOffset),
            MpqCrypto.Hash(name, MpqCrypto.HashNameA),
            MpqCrypto.Hash(name, MpqCrypto.HashNameB),
            data, compress, name));
    }

    public void AddHashed(uint nameA, uint nameB, byte[] data, bool compress = true)
    {
        _entries.Add(new Entry(nameA, nameA, nameB, data, compress, null));
    }

    public void Write(string path, byte[] prefix = null)
    {
        int sectorSize = 4096;
        var bodies = new List<byte[]>(_entries.Count);
        var blocks = new List<(uint Offset, uint CompressedSize, uint FileSize, uint Flags)>(_entries.Count);

        long offset = 32;
        foreach (var entry in _entries)
        {
            byte[] body = BuildBody(entry.Data, entry.Compress, sectorSize);
            bodies.Add(body);
            uint flags = FlagExists;
            if (entry.Compress)
            {
                flags |= FlagCompress;
            }
            blocks.Add(((uint)offset, (uint)body.Length, (uint)entry.Data.Length, flags));
            offset += body.Length;
        }

        int hashCount = NextPowerOfTwo(Math.Max(4, _entries.Count * 2));
        uint hashTablePos = (uint)offset;
        uint blockTablePos = (uint)(hashTablePos + hashCount * 16);
        uint archiveSize = (uint)(blockTablePos + blocks.Count * 16);

        var hashTable = new uint[hashCount * 4];
        for (int i = 0; i < hashCount * 4; i++)
        {
            hashTable[i] = 0xFFFFFFFF;
        }

        for (int i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            int slot = (int)(entry.Slot & (uint)(hashCount - 1));
            for (int step = 0; step < hashCount; step++)
            {
                int index = (slot + step) & (hashCount - 1);
                if (hashTable[index * 4 + 3] == 0xFFFFFFFF)
                {
                    hashTable[index * 4] = entry.NameA;
                    hashTable[index * 4 + 1] = entry.NameB;
                    hashTable[index * 4 + 2] = 0;
                    hashTable[index * 4 + 3] = (uint)i;
                    break;
                }
            }
        }

        var blockTable = new uint[blocks.Count * 4];
        for (int i = 0; i < blocks.Count; i++)
        {
            blockTable[i * 4] = blocks[i].Offset;
            blockTable[i * 4 + 1] = blocks[i].CompressedSize;
            blockTable[i * 4 + 2] = blocks[i].FileSize;
            blockTable[i * 4 + 3] = blocks[i].Flags;
        }

        MpqCrypto.EncryptBlock(hashTable, MpqCrypto.Hash("(hash table)", MpqCrypto.HashFileKey));
        MpqCrypto.EncryptBlock(blockTable, MpqCrypto.Hash("(block table)", MpqCrypto.HashFileKey));

        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, Encoding.UTF8, true))
        {
            writer.Write(new byte[] { 0x4D, 0x50, 0x51, 0x1A });
            writer.Write((uint)32);
            writer.Write(archiveSize);
            writer.Write((ushort)0);
            writer.Write((ushort)3);
            writer.Write(hashTablePos);
            writer.Write(blockTablePos);
            writer.Write((uint)hashCount);
            writer.Write((uint)blocks.Count);

            foreach (byte[] body in bodies)
            {
                writer.Write(body);
            }

            foreach (uint value in hashTable)
            {
                writer.Write(value);
            }

            foreach (uint value in blockTable)
            {
                writer.Write(value);
            }
        }

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        if (prefix != null && prefix.Length > 0)
        {
            stream.Write(prefix, 0, prefix.Length);
        }
        byte[] archive = buffer.ToArray();
        stream.Write(archive, 0, archive.Length);
    }

    private static byte[] BuildBody(byte[] data, bool compress, int sectorSize)
    {
        if (data.Length == 0)
        {
            return Array.Empty<byte>();
        }

        if (!compress)
        {
            return data;
        }

        int sectorCount = (data.Length + sectorSize - 1) / sectorSize;
        var payloads = new List<byte[]>(sectorCount);
        int total = 4 * (sectorCount + 1);

        for (int i = 0; i < sectorCount; i++)
        {
            int start = i * sectorSize;
            int length = Math.Min(sectorSize, data.Length - start);
            byte[] raw = data[start..(start + length)];
            byte[] payload = raw;
            if (compress)
            {
                byte[] packed = Compress(raw);
                if (packed.Length < raw.Length)
                {
                    payload = packed;
                }
            }
            payloads.Add(payload);
            total += payload.Length;
        }

        var body = new byte[total];
        int offset = 4 * (sectorCount + 1);
        for (int i = 0; i < sectorCount; i++)
        {
            BitConverter.GetBytes((uint)offset).CopyTo(body, i * 4);
            Buffer.BlockCopy(payloads[i], 0, body, offset, payloads[i].Length);
            offset += payloads[i].Length;
        }
        BitConverter.GetBytes((uint)offset).CopyTo(body, sectorCount * 4);
        return body;
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        output.WriteByte(0x02);
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, true))
        {
            zlib.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    private static int NextPowerOfTwo(int value)
    {
        int result = 1;
        while (result < value)
        {
            result <<= 1;
        }
        return result;
    }
}
