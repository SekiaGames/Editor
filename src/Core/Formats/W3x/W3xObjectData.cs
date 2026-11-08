using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WorldEdit.Core.Formats.W3x;

public sealed class W3xObjectModification
{
    public string FieldId = "";
    public int Type;
    public int Level;
    public int DataPointer;
    public byte[] ValueBytes = Array.Empty<byte>();
    public byte[] EndToken = Array.Empty<byte>();

    public bool IsText => Type == 3;

    public int IntegerValue => ValueBytes.Length >= 4 ? BitConverter.ToInt32(ValueBytes, 0) : 0;

    public float RealValue => ValueBytes.Length >= 4 ? BitConverter.ToSingle(ValueBytes, 0) : 0f;

    public string TextValue
    {
        get
        {
            if (Type != 3 || ValueBytes.Length == 0)
            {
                return "";
            }
            int length = ValueBytes.Length;
            if (ValueBytes[length - 1] == 0)
            {
                length--;
            }
            return Encoding.UTF8.GetString(ValueBytes, 0, length);
        }
    }
}

public sealed class W3xObjectDefinition
{
    public string BaseId = "";
    public string NewId = "";
    public byte[] FlagsBytes = Array.Empty<byte>();
    public List<W3xObjectModification> Modifications { get; } = new();

    public string Id => string.IsNullOrEmpty(NewId) ? BaseId : NewId;

    public bool IsCustom => !string.Equals(Id, BaseId, StringComparison.Ordinal);

    public W3xObjectModification Find(string fieldId, int level = 0, int dataPointer = 0)
    {
        for (int i = Modifications.Count - 1; i >= 0; i--)
        {
            W3xObjectModification modification = Modifications[i];
            if (string.Equals(modification.FieldId, fieldId, StringComparison.Ordinal)
                && modification.Level == level
                && modification.DataPointer == dataPointer)
            {
                return modification;
            }
        }
        return null;
    }
}

public sealed class W3xObjectData
{
    public const string UnitFile = "war3map.w3u";
    public const string DoodadFile = "war3map.w3d";
    public const string AbilityFile = "war3map.w3a";
    public const string UpgradeFile = "war3map.w3q";
    public const string ItemFile = "war3map.w3t";
    public const string BuffFile = "war3map.w3b";

    public static readonly string[] AllFiles =
    {
        UnitFile, DoodadFile, AbilityFile, UpgradeFile, ItemFile, BuffFile,
    };

    public int Version { get; private set; }
    public string FileName { get; private set; }
    public bool Extended { get; private set; }
    public byte[] TrailingBytes { get; private set; } = Array.Empty<byte>();
    public byte[] RawBytes { get; private set; }

    public List<W3xObjectDefinition> OriginalObjects { get; } = new();
    public List<W3xObjectDefinition> CustomObjects { get; } = new();

    public IEnumerable<W3xObjectDefinition> Objects
    {
        get
        {
            foreach (W3xObjectDefinition definition in OriginalObjects)
            {
                yield return definition;
            }
            foreach (W3xObjectDefinition definition in CustomObjects)
            {
                yield return definition;
            }
        }
    }

    public static bool UsesExtendedFields(string fileName)
    {
        return string.Equals(fileName, DoodadFile, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, AbilityFile, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, UpgradeFile, StringComparison.OrdinalIgnoreCase);
    }

    public W3xObjectDefinition Find(string id)
    {
        W3xObjectDefinition found = null;
        foreach (W3xObjectDefinition definition in Objects)
        {
            if (string.Equals(definition.Id, id, StringComparison.Ordinal))
            {
                found = definition;
            }
        }
        return found;
    }

    public string ResolveBaseId(string id, int depth = 0)
    {
        if (depth > 16)
        {
            return id;
        }
        W3xObjectDefinition definition = Find(id);
        if (definition == null || !definition.IsCustom)
        {
            return id;
        }
        return ResolveBaseId(definition.BaseId, depth + 1);
    }

    public static W3xObjectData Parse(byte[] data, string fileName)
    {
        if (data == null || data.Length < 8)
        {
            throw new InvalidDataException($"{fileName} 太短");
        }

        var reader = new W3xBinaryReader(data);
        var result = new W3xObjectData
        {
            RawBytes = data,
            FileName = fileName,
            Extended = UsesExtendedFields(fileName),
            Version = reader.I32(),
        };
        if (result.Version < 1 || result.Version > 3)
        {
            throw new InvalidDataException($"{fileName} 物体数据版本 {result.Version} 不受支持");
        }

        result.ReadSet(reader, result.OriginalObjects);
        result.ReadSet(reader, result.CustomObjects);
        if (reader.Remaining > 0)
        {
            result.TrailingBytes = reader.Bytes(reader.Remaining);
        }
        return result;
    }

    private void ReadSet(W3xBinaryReader reader, List<W3xObjectDefinition> target)
    {
        int count = reader.Count(100000);
        for (int i = 0; i < count; i++)
        {
            var definition = new W3xObjectDefinition
            {
                BaseId = reader.FourCc(),
                NewId = reader.FourCc(),
            };
            if (Version == 3)
            {
                int flagCount = reader.Count(64);
                definition.FlagsBytes = reader.Bytes(flagCount * 4);
            }

            int modifications = reader.Count(200000);
            for (int modification = 0; modification < modifications; modification++)
            {
                var entry = new W3xObjectModification
                {
                    FieldId = reader.FourCc(),
                    Type = reader.I32(),
                };
                if (entry.Type < 0 || entry.Type > 3)
                {
                    throw new InvalidDataException($"{FileName} 字段 {entry.FieldId} 的值类型 {entry.Type} 不受支持");
                }
                if (Extended)
                {
                    entry.Level = reader.I32();
                    entry.DataPointer = reader.I32();
                }
                entry.ValueBytes = entry.Type == 3 ? reader.NullStringBytes() : reader.Bytes(4);
                entry.EndToken = reader.Bytes(4);
                definition.Modifications.Add(entry);
            }
            target.Add(definition);
        }
    }

    public byte[] ToBytes()
    {
        throw new NotSupportedException($"{FileName} 写入尚未实现");
    }
}
