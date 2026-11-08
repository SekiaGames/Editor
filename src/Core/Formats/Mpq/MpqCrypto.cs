using System;

namespace WorldEdit.Core.Formats.Mpq;

public static class MpqCrypto
{
    public const int HashTableOffset = 0;
    public const int HashNameA = 1;
    public const int HashNameB = 2;
    public const int HashFileKey = 3;

    private static readonly uint[] CryptTable = BuildCryptTable();

    private static uint[] BuildCryptTable()
    {
        var table = new uint[0x500];
        uint seed = 0x00100001;
        for (int index1 = 0; index1 < 0x100; index1++)
        {
            int index2 = index1;
            for (int i = 0; i < 5; i++)
            {
                seed = (seed * 125 + 3) % 0x2AAAAB;
                uint high = (seed & 0xFFFF) << 16;
                seed = (seed * 125 + 3) % 0x2AAAAB;
                uint low = seed & 0xFFFF;
                table[index2] = high | low;
                index2 += 0x100;
            }
        }
        return table;
    }

    public static uint Hash(string name, int hashType)
    {
        uint seed1 = 0x7FED7FED;
        uint seed2 = 0xEEEEEEEE;
        foreach (char raw in name.ToUpperInvariant())
        {
            byte ch = raw == '/' ? (byte)'\\' : (byte)raw;
            seed1 = CryptTable[(hashType << 8) + ch] ^ (seed1 + seed2);
            seed2 = ch + seed1 + seed2 + (seed2 << 5) + 3;
        }
        return seed1;
    }

    public static uint FileKey(uint baseKey, uint blockOffset, uint fileSize)
    {
        return (baseKey + blockOffset) ^ fileSize;
    }

    public static void DecryptBlock(Span<uint> data, uint key)
    {
        uint seed2 = 0xEEEEEEEE;
        for (int i = 0; i < data.Length; i++)
        {
            seed2 += CryptTable[0x400 + (key & 0xFF)];
            uint value = data[i] ^ (key + seed2);
            key = ((~key << 0x15) + 0x11111111) | (key >> 0x0B);
            seed2 = value + seed2 + (seed2 << 5) + 3;
            data[i] = value;
        }
    }

    public static void EncryptBlock(Span<uint> data, uint key)
    {
        uint seed2 = 0xEEEEEEEE;
        for (int i = 0; i < data.Length; i++)
        {
            seed2 += CryptTable[0x400 + (key & 0xFF)];
            uint plain = data[i];
            data[i] = plain ^ (key + seed2);
            key = ((~key << 0x15) + 0x11111111) | (key >> 0x0B);
            seed2 = plain + seed2 + (seed2 << 5) + 3;
        }
    }

    public static void DecryptBytes(Span<byte> data, uint key)
    {
        int words = data.Length / 4;
        var span = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(data[..(words * 4)]);
        DecryptBlock(span, key);
    }

    public static void EncryptBytes(Span<byte> data, uint key)
    {
        int words = data.Length / 4;
        var span = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(data[..(words * 4)]);
        EncryptBlock(span, key);
    }
}
