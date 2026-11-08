using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace WorldEdit.Core.Formats.Mpq;

public sealed class MpqArchive : IDisposable
{
    private const uint FlagImplode = 0x00000100;
    private const uint FlagCompress = 0x00000200;
    private const uint FlagEncrypted = 0x00010000;
    private const uint FlagFixKey = 0x00020000;
    private const uint FlagSingleUnit = 0x01000000;
    private const uint FlagExists = 0x80000000;

    public readonly record struct Block(uint Offset, uint CompressedSize, uint FileSize, uint Flags);
    public readonly record struct HashEntry(uint NameA, uint NameB, ushort Locale, ushort Platform, uint BlockIndex);

    private readonly FileStream _stream;
    private readonly long _headerOffset;
    private readonly int _sectorSize;
    private readonly Block[] _blocks;
    private readonly HashEntry[] _hash;
    private readonly Dictionary<string, int> _names = new(StringComparer.OrdinalIgnoreCase);

    private static bool _traceDecode;
    private static int _traceBudget;

    public IReadOnlyList<Block> Blocks => _blocks;
    public int HashCount => _hash.Length;
    public string Path { get; }

    private MpqArchive(FileStream stream, long headerOffset, int sectorSize, Block[] blocks, HashEntry[] hash, string path)
    {
        _stream = stream;
        _headerOffset = headerOffset;
        _sectorSize = sectorSize;
        _blocks = blocks;
        _hash = hash;
        Path = path;
    }

    public static MpqArchive Open(string path)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        long headerOffset = FindHeaderOffset(stream);
        if (headerOffset < 0)
        {
            stream.Dispose();
            throw new InvalidDataException("找不到 MPQ 头");
        }

        stream.Position = headerOffset;
        var header = new byte[32];
        stream.ReadExactly(header);
        if (header[0] != (byte)'M' || header[1] != (byte)'P' || header[2] != (byte)'Q' || header[3] != 0x1A)
        {
            stream.Dispose();
            throw new InvalidDataException("MPQ 魔数不正确");
        }

        ushort sectorShift = BitConverter.ToUInt16(header, 14);
        uint hashPos = BitConverter.ToUInt32(header, 16);
        uint blockPos = BitConverter.ToUInt32(header, 20);
        uint hashCount = BitConverter.ToUInt32(header, 24);
        uint blockCount = BitConverter.ToUInt32(header, 28);

        var hashRaw = ReadAt(stream, headerOffset + hashPos, (int)(hashCount * 16));
        var blockRaw = ReadAt(stream, headerOffset + blockPos, (int)(blockCount * 16));

        var hash = ParseHash(hashRaw, hashCount, blockCount, out bool hashEncrypted);
        if (hashEncrypted)
        {
            var words = ToWords(hashRaw);
            MpqCrypto.DecryptBlock(words, MpqCrypto.Hash("(hash table)", MpqCrypto.HashFileKey));
            hash = ParseHash(FromWords(words), hashCount, blockCount, out _);
        }

        var blockWords = ToWords(blockRaw);
        var blocks = ParseBlocks(blockWords, blockCount, stream.Length - headerOffset, out bool blockEncrypted);
        if (blockEncrypted)
        {
            MpqCrypto.DecryptBlock(blockWords, MpqCrypto.Hash("(block table)", MpqCrypto.HashFileKey));
            blocks = ParseBlocks(blockWords, blockCount, stream.Length - headerOffset, out _);
        }

        return new MpqArchive(stream, headerOffset, 512 << sectorShift, blocks, hash, path);
    }

    public static byte[] ReadMapHeader(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var head = new byte[512];
        int read = stream.Read(head, 0, head.Length);
        if (read < 512 || head[0] != (byte)'H' || head[1] != (byte)'M' || head[2] != (byte)'3' || head[3] != (byte)'W')
        {
            return null;
        }
        return head;
    }

    private static long FindHeaderOffset(Stream stream)
    {
        var head = new byte[16];
        stream.Position = 0;
        int read = stream.Read(head, 0, head.Length);
        if (read >= 4 && head[0] == 'M' && head[1] == 'P' && head[2] == 'Q' && head[3] == 0x1A)
        {
            return 0;
        }
        if (read >= 4 && head[0] == 'M' && head[1] == 'P' && head[2] == 'Q' && head[3] == 0x1B)
        {
            return BitConverter.ToUInt32(head, 8);
        }
        if (read >= 4 && head[0] == 'H' && head[1] == 'M' && head[2] == '3' && head[3] == 'W')
        {

            return 512;
        }
        return -1;
    }

    private static byte[] ReadAt(Stream stream, long offset, int count)
    {
        var buffer = new byte[count];
        stream.Position = offset;
        stream.ReadExactly(buffer);
        return buffer;
    }

    private static uint[] ToWords(byte[] data)
    {
        var words = new uint[data.Length / 4];
        Buffer.BlockCopy(data, 0, words, 0, words.Length * 4);
        return words;
    }

    private static byte[] FromWords(uint[] words)
    {
        var data = new byte[words.Length * 4];
        Buffer.BlockCopy(words, 0, data, 0, data.Length);
        return data;
    }

    private static HashEntry[] ParseHash(byte[] raw, uint count, uint blockCount, out bool encrypted)
    {
        var entries = new HashEntry[count];
        int sane = 0;
        for (int i = 0; i < count; i++)
        {
            int o = i * 16;
            entries[i] = new HashEntry(
                BitConverter.ToUInt32(raw, o),
                BitConverter.ToUInt32(raw, o + 4),
                BitConverter.ToUInt16(raw, o + 8),
                BitConverter.ToUInt16(raw, o + 10),
                BitConverter.ToUInt32(raw, o + 12));
            if (entries[i].Platform == 0xFFFF || entries[i].BlockIndex < blockCount)
            {
                sane++;
            }
        }
        encrypted = sane < count / 2;
        return entries;
    }

    private static Block[] ParseBlocks(uint[] raw, uint count, long archiveBytes, out bool encrypted)
    {
        var blocks = new Block[count];
        int sane = 0;
        for (int i = 0; i < count; i++)
        {
            blocks[i] = new Block(raw[i * 4], raw[i * 4 + 1], raw[i * 4 + 2], raw[i * 4 + 3]);
            if (blocks[i].Offset < archiveBytes && blocks[i].CompressedSize <= archiveBytes)
            {
                sane++;
            }
        }
        encrypted = sane < count / 2;
        return blocks;
    }

    public void LoadListFile()
    {
        byte[] listfile = TryRead("(listfile)");
        if (listfile == null)
        {
            return;
        }
        foreach (string line in Encoding.UTF8.GetString(listfile).Split('\n'))
        {
            string name = line.Trim('\r', ' ', '\0');
            if (name.Length == 0 || name.StartsWith("//"))
            {
                continue;
            }
            int index = Find(name);
            if (index >= 0)
            {
                _names[name] = index;
            }
        }
    }

    public IReadOnlyDictionary<string, int> KnownNames => _names;

    private static readonly string[] StandardNames =
    {
        "(attributes)", "(signature)", "(user data)", "(listfile)",
    };

    private const int FallbackProbeCount = 16;

    public void ResolveUnnamedBlocks()
    {
        var used = new HashSet<int>(_names.Values);
        foreach (string candidate in StandardNames)
        {
            if (_names.ContainsKey(candidate))
            {
                continue;
            }
            int index = Find(candidate);
            if (index < 0 || index >= _blocks.Length || used.Contains(index) || (_blocks[index].Flags & FlagExists) == 0)
            {
                continue;
            }
            _names[candidate] = index;
            used.Add(index);
        }

        int probes = 0;
        for (int i = 0; i < _blocks.Length && probes < FallbackProbeCount; i++)
        {
            if (used.Contains(i))
            {
                continue;
            }
            foreach (string candidate in StandardNames)
            {
                if (_names.ContainsKey(candidate))
                {
                    continue;
                }
                probes++;
                byte[] data = TryReadUnchecked(candidate, i);
                if (data != null && data.Length == _blocks[i].FileSize)
                {
                    _names[candidate] = i;
                    used.Add(i);
                    break;
                }
            }
        }
    }

    private byte[] TryReadUnchecked(string name, int index)
    {
        try
        {
            return ReadBlock(index, name);
        }
        catch
        {
            return null;
        }
    }

    public int Find(string name)
    {
        uint hashA = MpqCrypto.Hash(name, MpqCrypto.HashNameA);
        uint hashB = MpqCrypto.Hash(name, MpqCrypto.HashNameB);
        uint start = MpqCrypto.Hash(name, MpqCrypto.HashTableOffset) & (uint)(_hash.Length - 1);
        for (int step = 0; step < _hash.Length; step++)
        {
            var entry = _hash[(start + step) & (uint)(_hash.Length - 1)];
            if (entry.Platform == 0xFFFF)
            {
                return -1;
            }
            if (entry.NameA == hashA && entry.NameB == hashB)
            {
                return (int)entry.BlockIndex;
            }
        }
        return -1;
    }

    public bool TryGetNameHashes(int blockIndex, out uint nameA, out uint nameB)
    {
        foreach (var entry in _hash)
        {
            if (entry.Platform != 0xFFFF && entry.BlockIndex == blockIndex)
            {
                nameA = entry.NameA;
                nameB = entry.NameB;
                return true;
            }
        }
        nameA = 0;
        nameB = 0;
        return false;
    }

    public bool Contains(string name)
    {
        return Find(name) >= 0;
    }

    public byte[] TryRead(string name)
    {
        int index = Find(name);
        return index < 0 ? null : TryReadUnchecked(name, index);
    }

    public byte[] ReadBlock(int index, string name = null)
    {
        if (index < 0 || index >= _blocks.Length)
        {
            return null;
        }
        Block block = _blocks[index];
        if ((block.Flags & FlagExists) == 0 || block.CompressedSize == 0)
        {
            return null;
        }

        bool encrypted = (block.Flags & FlagEncrypted) != 0;
        long start = _headerOffset + block.Offset;

        if ((block.Flags & (FlagCompress | FlagImplode)) == 0)
        {
            var plain = ReadAt(_stream, start, (int)block.CompressedSize);
            if (encrypted)
            {
                MpqCrypto.DecryptBytes(plain, ResolvePlainKey(block, name));
            }
            return plain.Length > block.FileSize ? plain[..(int)block.FileSize] : plain;
        }

        if ((block.Flags & FlagSingleUnit) != 0)
        {
            var whole = ReadAt(_stream, start, (int)block.CompressedSize);
            byte[] data = whole;
            if (encrypted)
            {
                foreach (uint candidate in KeyCandidates(block, name))
                {
                    var attempt = (byte[])whole.Clone();
                    MpqCrypto.DecryptBytes(attempt, candidate);
                    if (IsPlausibleCompression(attempt[0]))
                    {
                        data = attempt;
                        break;
                    }
                }
            }
            if ((block.Flags & FlagCompress) != 0)
            {
                data = Decompress(data, (int)block.FileSize);
            }
            else if ((block.Flags & FlagImplode) != 0)
            {
                data = PkwareExplode.Explode(data, (int)block.FileSize);
            }
            return data.Length > block.FileSize ? data[..(int)block.FileSize] : data;
        }

        int sectorCount = (int)((block.FileSize + (uint)_sectorSize - 1) / (uint)_sectorSize);
        var tableRaw = ReadAt(_stream, start, (sectorCount + 1) * 4);
        var offsets = ToWords(tableRaw);
        uint key = 0;
        if (encrypted)
        {
            key = ResolveFileKey(block, name, offsets, sectorCount);
            MpqCrypto.DecryptBlock(offsets, key - 1);
        }

        bool implodeOnly = (block.Flags & FlagImplode) != 0 && (block.Flags & FlagCompress) == 0;
        var result = new byte[block.FileSize];
        int written = 0;
        for (int i = 0; i < sectorCount; i++)
        {
            long sectorStart = start + offsets[i];
            int length = (int)(offsets[i + 1] - offsets[i]);
            var sector = ReadAt(_stream, sectorStart, length);
            if (encrypted)
            {
                MpqCrypto.DecryptBytes(sector, key + (uint)i);
            }
            uint expected = (uint)Math.Min(_sectorSize, block.FileSize - (uint)written);
            if (_traceDecode)
            {
                Console.WriteLine($"[TRACE] 解码 sector{i} mask=0x{sector[0]:X2} 压缩后={length} 期望={expected}");
            }
            byte[] plain;
            if (length == 3 && expected <= 4 && sector[0] == 0x40 && sector[1] == 0 && sector[2] == 0x04)
            {
                plain = new byte[expected];
            }
            else
            {
                plain = length == expected
                    ? sector
                    : (implodeOnly
                        ? PkwareExplode.Explode(sector, (int)expected)
                        : Decompress(sector, (int)expected));
            }
            if (_traceDecode && _traceBudget-- > 0)
            {
                Console.WriteLine($"[TRACE] sector{i} mask=0x{sector[0]:X2} 压缩后={length} 期望={expected} 实际={plain.Length} 已写={written}");
            }
            if (plain.Length > result.Length - written)
            {
                var clipped = new byte[result.Length - written];
                Array.Copy(plain, clipped, clipped.Length);
                plain = clipped;
            }
            Buffer.BlockCopy(plain, 0, result, written, plain.Length);
            written += plain.Length;
        }
        return result;
    }

    private static uint ResolvePlainKey(Block block, string name)
    {
        uint full = MpqCrypto.Hash(name ?? string.Empty, MpqCrypto.HashFileKey);
        return (block.Flags & FlagFixKey) != 0 ? MpqCrypto.FileKey(full, block.Offset, block.FileSize) : full;
    }

    private static List<uint> KeyCandidates(Block block, string name)
    {
        var keys = new List<uint>(2);
        uint full = MpqCrypto.Hash(name ?? string.Empty, MpqCrypto.HashFileKey);
        keys.Add((block.Flags & FlagFixKey) != 0 ? MpqCrypto.FileKey(full, block.Offset, block.FileSize) : full);

        string baseName = (name ?? string.Empty).Split('/', '\\')[^1];
        if (!string.Equals(baseName, name, StringComparison.Ordinal))
        {
            uint shortKey = MpqCrypto.Hash(baseName, MpqCrypto.HashFileKey);
            keys.Add((block.Flags & FlagFixKey) != 0 ? MpqCrypto.FileKey(shortKey, block.Offset, block.FileSize) : shortKey);
        }
        return keys;
    }

    private static bool IsPlausibleCompression(byte mask)
    {
        return mask is 0x00 or 0x01 or 0x02 or 0x08 or 0x10 or 0x12 or 0x20 or 0x40 or 0x80
            or 0x41 or 0x48 or 0x42 or 0x50 or 0x52 or 0x80 or 0x81 or 0x88 or 0x90 or 0xC0;
    }

    public int SectorSize => _sectorSize;

    public List<byte[]> ReadRawSectors(int index, string name = null)
    {
        var result = new List<byte[]>();
        Block block = _blocks[index];
        bool encrypted = (block.Flags & FlagEncrypted) != 0;
        long start = _headerOffset + block.Offset;
        if ((block.Flags & FlagSingleUnit) != 0)
        {
            byte[] whole = ReadAt(_stream, start, (int)block.CompressedSize);
            if (encrypted)
            {
                MpqCrypto.DecryptBytes(whole, ResolvePlainKey(block, name));
            }
            result.Add(whole);
            return result;
        }

        int sectorCount = (int)((block.FileSize + (uint)_sectorSize - 1) / (uint)_sectorSize);
        uint[] offsets = ToWords(ReadAt(_stream, start, (sectorCount + 1) * 4));
        uint key = 0;
        if (encrypted)
        {
            key = ResolveFileKey(block, name, offsets, sectorCount);
            MpqCrypto.DecryptBlock(offsets, key - 1);
        }
        for (int i = 0; i < sectorCount; i++)
        {
            byte[] sector = ReadAt(_stream, start + offsets[i], (int)(offsets[i + 1] - offsets[i]));
            if (encrypted)
            {
                MpqCrypto.DecryptBytes(sector, key + (uint)i);
            }
            result.Add(sector);
        }
        return result;
    }

    public void TraceBlock(int index, string name = null)
    {
        Block block = _blocks[index];
        bool encrypted = (block.Flags & FlagEncrypted) != 0;
        long start = _headerOffset + block.Offset;
        int sectorCount = (int)((block.FileSize + (uint)_sectorSize - 1) / (uint)_sectorSize);
        Console.WriteLine($"[TRACE] encrypted={encrypted} start={start} sectors={sectorCount} sectorSize={_sectorSize}");
        uint[] rawOffsets = ToWords(ReadAt(_stream, start, (sectorCount + 1) * 4));
        Console.WriteLine($"[TRACE] 原始偏移: {string.Join(",", rawOffsets[..Math.Min(rawOffsets.Length, 4)])}");

        _traceDecode = true;
        _traceBudget = 30;
        try
        {
            byte[] decoded = ReadBlock(index, name);
            string head = decoded == null || decoded.Length == 0
                ? "-"
                : BitConverter.ToString(decoded, 0, Math.Min(8, decoded.Length));
            Console.WriteLine($"[TRACE] ReadBlock -> {decoded?.Length ?? -1} 字节（期望 {block.FileSize}）头 {head}");
        }
        catch (Exception error)
        {
            Console.WriteLine($"[TRACE] ReadBlock 抛 {error}");
        }
        _traceDecode = false;

        string full = name ?? string.Empty;
        string baseName = full.Split('/', '\\')[^1];
        foreach (var (label, candidate) in new[] { ("全名", full), ("基名", baseName) })
        {
            uint key = MpqCrypto.Hash(candidate, MpqCrypto.HashFileKey);
            uint adjusted = (block.Flags & FlagFixKey) != 0 ? MpqCrypto.FileKey(key, block.Offset, block.FileSize) : key;
            uint[] trial = (uint[])rawOffsets.Clone();
            if (encrypted)
            {
                MpqCrypto.DecryptBlock(trial, adjusted - 1);
            }
            Console.WriteLine($"[TRACE] {label} key=0x{key:X8} adjusted=0x{adjusted:X8} 合理={SaneOffsets(trial, block.CompressedSize, sectorCount)} -> {string.Join(",", trial[..Math.Min(trial.Length, 4)])}");
        }
    }

    public byte PeekCompressionType(int index, string name = null)
    {
        if (index < 0 || index >= _blocks.Length)
        {
            return 0;
        }
        Block block = _blocks[index];
        if ((block.Flags & FlagExists) == 0 || block.CompressedSize == 0)
        {
            return 0;
        }
        if ((block.Flags & (FlagCompress | FlagImplode)) == 0)
        {
            return 0;
        }
        if ((block.Flags & FlagImplode) != 0 && (block.Flags & FlagCompress) == 0)
        {
            return 0xFF;
        }

        bool encrypted = (block.Flags & FlagEncrypted) != 0;
        long start = _headerOffset + block.Offset;
        try
        {
            if ((block.Flags & FlagSingleUnit) != 0)
            {
                byte[] head = ReadAt(_stream, start, (int)Math.Min(4u, block.CompressedSize));
                if (encrypted)
                {
                    MpqCrypto.DecryptBytes(head, ResolvePlainKey(block, name));
                }
                return head[0];
            }

            int sectorCount = (int)((block.FileSize + (uint)_sectorSize - 1) / (uint)_sectorSize);
            uint[] offsets = ToWords(ReadAt(_stream, start, (sectorCount + 1) * 4));
            uint key = 0;
            if (encrypted)
            {
                key = ResolveFileKey(block, name, offsets, sectorCount);
                MpqCrypto.DecryptBlock(offsets, key - 1);
            }
            int length = (int)Math.Min(4u, offsets[1] - offsets[0]);
            byte[] sector = ReadAt(_stream, start + offsets[0], length);
            if (encrypted)
            {
                MpqCrypto.DecryptBytes(sector, key);
            }
            return sector[0];
        }
        catch (Exception)
        {
            return 0xFE;
        }
    }

    private static uint ResolveFileKey(Block block, string name, out bool adjusted)
    {
        uint full = MpqCrypto.Hash(name ?? string.Empty, MpqCrypto.HashFileKey);
        string baseName = (name ?? string.Empty).Split('/', '\\')[^1];
        uint shortKey = string.Equals(baseName, name, StringComparison.Ordinal)
            ? full
            : MpqCrypto.Hash(baseName, MpqCrypto.HashFileKey);

        if ((block.Flags & FlagFixKey) == 0)
        {
            adjusted = false;
            return full;
        }

        adjusted = true;
        uint candidate = MpqCrypto.FileKey(shortKey, block.Offset, block.FileSize);
        return candidate;
    }

    private static uint ResolveFileKey(Block block, string name, uint[] rawOffsets, int sectorCount)
    {
        var candidates = new List<uint>(2);
        uint full = MpqCrypto.Hash(name ?? string.Empty, MpqCrypto.HashFileKey);
        string baseName = (name ?? string.Empty).Split('/', '\\')[^1];
        if ((block.Flags & FlagFixKey) != 0)
        {
            candidates.Add(MpqCrypto.FileKey(full, block.Offset, block.FileSize));
            if (!string.Equals(baseName, name, StringComparison.Ordinal))
            {
                candidates.Add(MpqCrypto.FileKey(MpqCrypto.Hash(baseName, MpqCrypto.HashFileKey), block.Offset, block.FileSize));
            }
        }
        else
        {
            candidates.Add(full);
            if (!string.Equals(baseName, name, StringComparison.Ordinal))
            {
                candidates.Add(MpqCrypto.Hash(baseName, MpqCrypto.HashFileKey));
            }
        }

        foreach (uint candidate in candidates)
        {
            uint[] trial = (uint[])rawOffsets.Clone();
            MpqCrypto.DecryptBlock(trial, candidate - 1);
            if (SaneOffsets(trial, block.CompressedSize, sectorCount))
            {
                return candidate;
            }
        }
        return candidates[0];
    }

    private static bool SaneOffsets(uint[] offsets, uint compressedSize, int sectorCount)
    {
        uint minimum = (uint)((sectorCount + 1) * 4);
        if (offsets[0] < minimum || offsets[0] > compressedSize)
        {
            return false;
        }
        for (int i = 0; i < sectorCount; i++)
        {
            if (offsets[i] > offsets[i + 1] || offsets[i + 1] > compressedSize)
            {
                return false;
            }
        }
        return true;
    }

    private static byte[] Decompress(byte[] data, int expected)
    {
        if (data.Length == 0)
        {
            return data;
        }
        byte mask = data[0];
        var payload = data.AsSpan(1).ToArray();
        if (_traceDecode)
        {
            Console.WriteLine($"[TRACE]   Decompress mask=0x{mask:X2} 输入={data.Length} 净荷={payload.Length} type={payload[0]}");
            if (mask == 0x41 || mask == 0x81)
            {
                try
                {
                    byte[] probe = HuffmanDecode.Decompress(payload, expected);
                    Console.WriteLine($"[TRACE]   huffman 解出 {probe.Length} 字节，前 6 字节 {BitConverter.ToString(probe, 0, Math.Min(6, probe.Length))}");
                }
                catch (Exception probeError)
                {
                    Console.WriteLine($"[TRACE]   huffman 失败 {probeError.GetType().Name}: {probeError.Message}");
                }
            }
        }
        switch (mask)
        {
            case 0x00:
                return payload;
            case 0x02:
                return Inflate(payload, expected);
            case 0x08:
                return PkwareExplode.Explode(payload, expected);
            case 0x01:
                return HuffmanDecode.Decompress(payload, expected);
            case 0x40:
                return AdpcmDecode.Decompress(payload, 1, expected);
            case 0x80:
                return AdpcmDecode.Decompress(payload, 2, expected);
            case 0x41:
                return AdpcmDecode.Decompress(HuffmanDecode.Decompress(payload, expected), 1, expected);
            case 0x81:
                return AdpcmDecode.Decompress(HuffmanDecode.Decompress(payload, expected), 2, expected);
            default:
                throw new NotSupportedException($"暂不支持的压缩类型 0x{mask:X2}");
        }
    }

    private static byte[] Inflate(byte[] payload, int expected)
    {
        using var input = new MemoryStream(payload);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream(expected > 0 ? expected : 0);
        zlib.CopyTo(output);
        return output.ToArray();
    }

    public static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        output.WriteByte(0x02);
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, true))
        {
            zlib.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    public void Dispose()
    {
        _stream.Dispose();
    }
}
