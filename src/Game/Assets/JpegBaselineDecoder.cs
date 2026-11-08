using System;

namespace WorldEdit.Assets;

public sealed class JpegBaselineDecoder
{
    private static readonly int[] ZigZag =
    {
        0, 1, 8, 16, 9, 2, 3, 10, 17, 24, 32, 25, 18, 11, 4, 5,
        12, 19, 26, 33, 40, 48, 41, 34, 27, 20, 13, 6, 7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36, 29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46, 53, 60, 61, 54, 47, 55, 62, 63,
    };

    private static readonly float[,] Cos = BuildCos();

    private static float[,] BuildCos()
    {
        var table = new float[8, 8];
        for (int x = 0; x < 8; x++)
        {
            for (int u = 0; u < 8; u++)
            {
                table[x, u] = (float)Math.Cos((2 * x + 1) * u * Math.PI / 16.0);
            }
        }
        return table;
    }

    private readonly byte[] _data;
    private int _pos;

    private int _width;
    private int _height;
    private int _componentCount;
    private readonly Component[] _components = new Component[4];
    private readonly ushort[][] _quantTables = new ushort[4][];
    private readonly HuffmanTable[] _dcTables = new HuffmanTable[4];
    private readonly HuffmanTable[] _acTables = new HuffmanTable[4];

    private sealed class Component
    {
        public int Id;
        public int H;
        public int V;
        public int QuantTable;
        public int DcTable;
        public int AcTable;
        public int Stride;
        public int Rows;
        public byte[] Plane;
        public int PredictedDc;
    }

    private sealed class HuffmanTable
    {
        public readonly int[] MaxCode = new int[18];
        public readonly int[] MinCode = new int[18];
        public readonly int[] ValPtr = new int[18];
        public readonly byte[] Values = new byte[256];

        public void Build(int[] counts, byte[] symbols, int symbolCount)
        {
            Array.Copy(symbols, Values, symbolCount);
            int code = 0;
            int k = 0;
            for (int length = 1; length <= 16; length++)
            {
                int count = counts[length];
                if (count > 0)
                {
                    MinCode[length] = code;
                    ValPtr[length] = k;
                    code += count;
                    k += count;
                    MaxCode[length] = code - 1;
                }
                else
                {
                    MaxCode[length] = -1;
                }
                code <<= 1;
            }
        }
    }

    private JpegBaselineDecoder(byte[] data)
    {
        _data = data;
    }

    public static byte[] Decode(byte[] jpeg, out int width, out int height)
    {
        var decoder = new JpegBaselineDecoder(jpeg);
        decoder.Parse();
        return decoder.BuildRgba(out width, out height);
    }

    private void Parse()
    {
        if (ReadU8() != 0xFF || ReadU8() != 0xD8)
        {
            throw new InvalidOperationException("缺少 JPEG SOI");
        }

        while (_pos < _data.Length)
        {
            int marker = NextMarker();
            switch (marker)
            {
                case 0xD9:
                    return;
                case 0xDB:
                    ReadQuantTable();
                    break;
                case 0xC4:
                    ReadHuffmanTable();
                    break;
                case 0xC0:
                case 0xC1:
                    ReadStartOfFrame();
                    break;
                case 0xC2:
                    throw new InvalidOperationException("不支持渐进式 JPEG（SOF2）");
                case 0xDD:
                    SkipSegment();
                    break;
                case 0xDA:
                    ReadStartOfScan();
                    return;
                default:
                    SkipSegment();
                    break;
            }
        }
    }

    private int NextMarker()
    {
        while (_pos < _data.Length - 1)
        {
            if (_data[_pos] != 0xFF)
            {
                _pos++;
                continue;
            }
            byte marker = _data[_pos + 1];
            _pos += 2;
            if (marker == 0xFF)
            {
                _pos--;
                continue;
            }
            return marker;
        }
        return 0xD9;
    }

    private int SegmentLength()
    {
        int length = (_data[_pos] << 8) | _data[_pos + 1];
        _pos += 2;
        return length - 2;
    }

    private void SkipSegment()
    {
        int length = SegmentLength();
        _pos += length;
    }

    private void ReadQuantTable()
    {
        int length = SegmentLength();
        int end = _pos + length;
        while (_pos < end)
        {
            int byteInfo = _data[_pos++];
            int precision = byteInfo >> 4;
            int id = byteInfo & 0x0F;
            var table = new ushort[64];
            for (int i = 0; i < 64; i++)
            {
                table[i] = precision == 0 ? _data[_pos++] : (ushort)((_data[_pos++] << 8) | _data[_pos++]);
            }
            _quantTables[id] = table;
        }
    }

    private void ReadHuffmanTable()
    {
        int length = SegmentLength();
        int end = _pos + length;
        var counts = new int[17];
        var symbols = new byte[256];
        while (_pos < end)
        {
            int byteInfo = _data[_pos++];
            int tableClass = byteInfo >> 4;
            int id = byteInfo & 0x0F;
            int total = 0;
            for (int i = 1; i <= 16; i++)
            {
                counts[i] = _data[_pos++];
                total += counts[i];
            }
            for (int i = 0; i < total; i++)
            {
                symbols[i] = _data[_pos++];
            }
            var table = new HuffmanTable();
            table.Build(counts, symbols, total);
            if (tableClass == 0)
            {
                _dcTables[id] = table;
            }
            else
            {
                _acTables[id] = table;
            }
        }
    }

    private void ReadStartOfFrame()
    {
        SegmentLength();
        _pos++;
        _height = (_data[_pos] << 8) | _data[_pos + 1];
        _pos += 2;
        _width = (_data[_pos] << 8) | _data[_pos + 1];
        _pos += 2;
        _componentCount = _data[_pos++];
        if (_componentCount is < 1 or > 4)
        {
            throw new InvalidOperationException($"不支持的分量数 {_componentCount}");
        }

        for (int i = 0; i < _componentCount; i++)
        {
            var component = new Component { Id = _data[_pos] };
            int hv = _data[_pos + 1];
            component.H = hv >> 4;
            component.V = hv & 0x0F;
            component.QuantTable = _data[_pos + 2];
            _pos += 3;
            _components[i] = component;
        }
    }

    private void ReadStartOfScan()
    {
        SegmentLength();
        int count = _data[_pos++];
        for (int i = 0; i < count; i++)
        {
            int id = _data[_pos];
            int tables = _data[_pos + 1];
            _pos += 2;

            Component component = FindComponent(id) ?? _components[i];
            component.DcTable = tables >> 4;
            component.AcTable = tables & 0x0F;
        }
        _pos += 3;

        int mcuWidth = 8 * Math.Max(1, _components[0].H);
        int mcuHeight = 8 * Math.Max(1, _components[0].V);
        int mcusX = (_width + mcuWidth - 1) / mcuWidth;
        int mcusY = (_height + mcuHeight - 1) / mcuHeight;

        foreach (Component component in _components)
        {
            if (component == null)
            {
                continue;
            }
            component.Stride = mcusX * 8 * component.H;
            component.Rows = mcusY * 8 * component.V;
            component.Plane = new byte[component.Stride * component.Rows];
            component.PredictedDc = 0;
        }

        var reader = new BitReader(_data, _pos);
        var block = new float[64];
        var samples = new byte[64];

        for (int my = 0; my < mcusY; my++)
        {
            for (int mx = 0; mx < mcusX; mx++)
            {
                for (int ci = 0; ci < _componentCount; ci++)
                {
                    Component component = _components[ci];
                    for (int v = 0; v < component.V; v++)
                    {
                        for (int h = 0; h < component.H; h++)
                        {
                            DecodeBlock(reader, component, block, samples);
                            int originX = (mx * component.H + h) * 8;
                            int originY = (my * component.V + v) * 8;
                            Blit(component, samples, originX, originY);
                        }
                    }
                }
            }
        }
    }

    private Component FindComponent(int id)
    {
        foreach (Component component in _components)
        {
            if (component != null && component.Id == id)
            {
                return component;
            }
        }
        return null;
    }

    private void DecodeBlock(BitReader reader, Component component, float[] block, byte[] samples)
    {
        Array.Clear(block);

        HuffmanTable dc = _dcTables[component.DcTable];
        HuffmanTable ac = _acTables[component.AcTable];

        int t = DecodeHuffman(reader, dc);
        int diff = t == 0 ? 0 : Extend(reader.ReadBits(t), t);
        component.PredictedDc += diff;
        block[0] = component.PredictedDc * _quantTables[component.QuantTable][0];

        for (int k = 1; k < 64;)
        {
            int rs = DecodeHuffman(reader, ac);
            int r = rs >> 4;
            int s = rs & 0x0F;
            if (s == 0)
            {
                if (r == 15)
                {
                    k += 16;
                    continue;
                }
                break;
            }
            k += r;
            if (k > 63)
            {
                break;
            }
            int value = Extend(reader.ReadBits(s), s);
            block[ZigZag[k]] = value * _quantTables[component.QuantTable][k];
            k++;
        }

        Idct(block);

        for (int i = 0; i < 64; i++)
        {
            int v = (int)(block[i] + 128.5f);
            samples[i] = (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
        }
    }

    private static int DecodeHuffman(BitReader reader, HuffmanTable table)
    {
        int code = reader.ReadBit();
        int length = 1;
        while (length <= 16 && code > table.MaxCode[length])
        {
            code = (code << 1) | reader.ReadBit();
            length++;
        }
        if (length > 16)
        {
            throw new InvalidOperationException("哈夫曼码无效");
        }
        return table.Values[table.ValPtr[length] + code - table.MinCode[length]];
    }

    private static int Extend(int value, int bits)
    {
        return value < 1 << (bits - 1) ? value - (1 << bits) + 1 : value;
    }

    private static void Blit(Component component, byte[] samples, int originX, int originY)
    {
        for (int y = 0; y < 8; y++)
        {
            int row = originY + y;
            if (row >= component.Rows)
            {
                break;
            }
            int offset = row * component.Stride + originX;
            for (int x = 0; x < 8; x++)
            {
                int column = originX + x;
                if (column >= component.Stride)
                {
                    break;
                }
                component.Plane[offset + x] = samples[y * 8 + x];
            }
        }
    }

    private static void Idct(float[] block)
    {
        var temp = new float[64];

        for (int y = 0; y < 8; y++)
        {
            int row = y * 8;
            for (int x = 0; x < 8; x++)
            {
                float sum = 0f;
                for (int u = 0; u < 8; u++)
                {
                    float cu = u == 0 ? 0.70710678f : 1f;
                    sum += cu * block[row + u] * Cos[x, u];
                }
                temp[row + x] = sum * 0.5f;
            }
        }

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                float sum = 0f;
                for (int v = 0; v < 8; v++)
                {
                    float cv = v == 0 ? 0.70710678f : 1f;
                    sum += cv * temp[v * 8 + x] * Cos[y, v];
                }
                block[y * 8 + x] = sum * 0.5f;
            }
        }
    }

    private byte[] BuildRgba(out int width, out int height)
    {
        width = _width;
        height = _height;
        var rgba = new byte[width * height * 4];
        Component[] planes = new Component[_componentCount];
        Array.Copy(_components, planes, _componentCount);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                byte r, g, b, a = 255;
                if (_componentCount == 1)
                {
                    r = g = b = planes[0].Plane[y * planes[0].Stride + x];
                }
                else if (_componentCount == 3)
                {
                    float yy = planes[0].Plane[y * planes[0].Stride + x];
                    float cb = planes[1].Plane[y * planes[1].Stride + x] - 128f;
                    float cr = planes[2].Plane[y * planes[2].Stride + x] - 128f;
                    r = Clamp(yy + 1.402f * cr);
                    g = Clamp(yy - 0.344136f * cb - 0.714136f * cr);
                    b = Clamp(yy + 1.772f * cb);
                }
                else
                {

                    b = planes[0].Plane[y * planes[0].Stride + x];
                    g = planes[1].Plane[y * planes[1].Stride + x];
                    r = planes[2].Plane[y * planes[2].Stride + x];
                    a = planes[3].Plane[y * planes[3].Stride + x];
                }

                rgba[index * 4] = r;
                rgba[index * 4 + 1] = g;
                rgba[index * 4 + 2] = b;
                rgba[index * 4 + 3] = a;
            }
        }

        return rgba;
    }

    private static byte Clamp(float value)
    {
        int v = (int)(value + 0.5f);
        return (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
    }

    private byte ReadU8()
    {
        return _data[_pos++];
    }

    private sealed class BitReader
    {
        private readonly byte[] _data;
        private int _pos;
        private int _buffer;
        private int _count;

        public BitReader(byte[] data, int position)
        {
            _data = data;
            _pos = position;
        }

        public int ReadBit()
        {
            if (_count == 0)
            {
                int value = _data[_pos++];
                if (value == 0xFF)
                {
                    byte next = _data[_pos];
                    if (next == 0x00)
                    {
                        _pos++;
                    }
                    else if (next is >= 0xD0 and <= 0xD7)
                    {
                        _pos++;
                        value = _data[_pos++];
                    }
                }
                _buffer = value;
                _count = 8;
            }
            _count--;
            return (_buffer >> _count) & 1;
        }

        public int ReadBits(int bits)
        {
            int value = 0;
            for (int i = 0; i < bits; i++)
            {
                value = (value << 1) | ReadBit();
            }
            return value;
        }
    }
}
