using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using WorldEdit.Core.Formats.Mpq;
using WorldEdit.Core.Formats.W3x;

namespace WorldEdit.Core.Map;

public sealed class MapDocument
{
    public sealed class Entry
    {
        public string Name;
        public uint NameA;
        public uint NameB;
        public byte[] Data;
        public bool Compress;
    }

    public string SourcePath { get; private set; }
    public List<Entry> Entries { get; } = new();
    public W3eTerrain Terrain { get; private set; }
    public W3iInfo Info { get; private set; }

    public W3oUnits Units { get; private set; }

    public W3oDoodads Doodads { get; private set; }

    public W3mPathing Pathing { get; private set; }

    public Dictionary<string, W3xObjectData> ObjectData { get; } = new(StringComparer.OrdinalIgnoreCase);

    public W3xImports Imports { get; private set; }

    public string MapName { get; private set; }

    public byte[] Header { get; private set; }

    public List<string> Warnings { get; } = new();

    private static readonly string[] StandardMapFileNames =
    {
        "war3map.w3e", "war3map.w3i", "war3map.wts", "war3map.wpm", "war3map.doo",
        "war3mapUnits.doo", "war3map.j", "scripts\\war3map.j", "war3map.lua", "scripts\\war3map.lua",
        "war3map.imp", "war3map.w3u", "war3map.w3d", "war3map.w3a", "war3map.w3q",
        "war3map.w3t", "war3map.w3b", "war3map.w3h", "war3map.w3r", "war3map.w3c",
        "war3map.w3s", "war3map.wtg", "war3map.wct", "war3map.shd", "war3map.mmp",
        "war3mapMap.tga", "war3mapPreview.tga", "war3mapMap.blp", "war3mapSkin.txt",
        "war3mapMisc.txt", "war3mapExtra.txt",
    };

    public static MapDocument Load(string path)
    {
        var document = new MapDocument { SourcePath = path };
        document.Header = MpqArchive.ReadMapHeader(path);
        using var archive = MpqArchive.Open(path);
        archive.LoadListFile();
        archive.ResolveUnnamedBlocks();

        var byIndex = new Dictionary<int, string>();
        foreach (var pair in archive.KnownNames)
        {
            byIndex[pair.Value] = pair.Key;
        }
        ResolveStandardNames(archive, byIndex);

        int unnamed = 0;
        for (int i = 0; i < archive.Blocks.Count; i++)
        {
            string name = byIndex.TryGetValue(i, out string known) ? known : null;
            if (name == null && (archive.Blocks[i].Flags & 0x00010000) != 0)
            {
                unnamed++;
                continue;
            }
            byte[] data;
            try
            {
                data = archive.ReadBlock(i, name);
            }
            catch (Exception e)
            {
                document.Warnings.Add($"{name ?? $"#{i}"}: {e.Message}");
                continue;
            }
            if (data == null)
            {
                continue;
            }
            if (!archive.TryGetNameHashes(i, out uint nameA, out uint nameB))
            {
                continue;
            }
            document.Entries.Add(new Entry
            {
                Name = name,
                NameA = nameA,
                NameB = nameB,
                Data = data,
                Compress = (archive.Blocks[i].Flags & 0x00000200) != 0,
            });
        }

        document.Build();
        if (unnamed > 0)
        {
            document.Warnings.Add($"{unnamed} 个加密块没有任何文件名记录，已跳过（被清理过 listfile 的地图属正常现象）");
        }
        return document;
    }

    private static void ResolveStandardNames(MpqArchive archive, Dictionary<int, string> byIndex)
    {
        var known = new HashSet<string>(byIndex.Values, StringComparer.OrdinalIgnoreCase);
        foreach (string name in StandardMapFileNames)
        {
            if (known.Contains(name))
            {
                continue;
            }
            int index = archive.Find(name);
            if (index < 0)
            {
                continue;
            }
            byIndex[index] = name;
            known.Add(name);
        }
    }

    public static MapDocument LoadDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"目录不存在: {directory}");
        }

        var document = new MapDocument { SourcePath = directory };
        foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetRelativePath(directory, file).Replace('\\', '/');
            document.Entries.Add(new Entry
            {
                Name = name,
                Data = File.ReadAllBytes(file),
                Compress = false,
            });
        }

        document.Build();
        return document;
    }

    private void Build()
    {
        byte[] w3i = Find("war3map.w3i");
        byte[] w3e = Find("war3map.w3e");
        byte[] wts = Find("war3map.wts");
        if (w3e == null)
        {
            throw new InvalidDataException("地图缺少 war3map.w3e，无法显示地形");
        }

        Info = w3i != null ? W3iInfo.Parse(w3i) : W3iInfo.CreateMissing();
        if (w3i == null)
        {
            Warnings.Add("地图缺少 war3map.w3i，按默认值处理（被清理过 listfile 的地图可能出现）");
        }
        Terrain = W3eTerrain.Parse(w3e);
        LoadObjectData();
        LoadImports();

        byte[] unitData = Find("war3mapUnits.doo");
        if (unitData != null)
        {
            try
            {
                Units = W3oUnits.Parse(unitData, IsItemId, Info.GameVersionNumber);
            }
            catch (Exception exception)
            {
                Warnings.Add($"war3mapUnits.doo 解析失败: {exception.Message}");
            }
        }

        byte[] doodadData = Find("war3map.doo");
        if (doodadData != null)
        {
            try
            {
                Doodads = W3oDoodads.Parse(doodadData);
            }
            catch (Exception exception)
            {
                Warnings.Add($"war3map.doo 解析失败: {exception.Message}");
            }
        }

        byte[] pathingData = Find("war3map.wpm");
        if (pathingData != null)
        {
            try
            {
                Pathing = W3mPathing.Parse(pathingData);
            }
            catch (Exception exception)
            {
                Warnings.Add($"war3map.wpm 解析失败: {exception.Message}");
            }
        }

        string resolved = wts != null ? Info.ResolveName(WtsDocument.Parse(wts)) : Info.NameRef;
        if (string.IsNullOrWhiteSpace(resolved) || resolved.StartsWith("TRIGSTR_", StringComparison.OrdinalIgnoreCase))
        {
            resolved = HeaderName(Header) ?? resolved;
        }
        MapName = resolved;
    }

    private void LoadObjectData()
    {
        foreach (string fileName in W3xObjectData.AllFiles)
        {
            byte[] data = Find(fileName);
            if (data == null)
            {
                continue;
            }
            try
            {
                ObjectData[fileName] = W3xObjectData.Parse(data, fileName);
            }
            catch (Exception exception)
            {
                Warnings.Add($"{fileName} 解析失败: {exception.Message}");
            }
        }
    }

    private void LoadImports()
    {
        byte[] data = Find(W3xImports.FileName);
        if (data == null)
        {
            return;
        }
        try
        {
            Imports = W3xImports.Parse(data);
        }
        catch (Exception exception)
        {
            Warnings.Add($"{W3xImports.FileName} 解析失败: {exception.Message}");
        }
    }

    private bool IsItemId(string id)
    {
        var items = WorldEdit.Assets.Generated.War3Tables.ItemData();
        var units = WorldEdit.Assets.Generated.War3Tables.UnitData();
        if (items.FindRow("itemID", id) >= 0 && units.FindRow("unitID", id) < 0)
        {
            return true;
        }

        string baseId = ResolveObjectBaseId(id);
        return !string.Equals(baseId, id, StringComparison.Ordinal)
            && items.FindRow("itemID", baseId) >= 0
            && units.FindRow("unitID", baseId) < 0;
    }

    private string ResolveObjectBaseId(string id)
    {
        if (ObjectData.TryGetValue(W3xObjectData.ItemFile, out W3xObjectData itemData))
        {
            string baseId = itemData.ResolveBaseId(id);
            if (!string.Equals(baseId, id, StringComparison.Ordinal))
            {
                return baseId;
            }
        }
        if (ObjectData.TryGetValue(W3xObjectData.UnitFile, out W3xObjectData unitData))
        {
            return unitData.ResolveBaseId(id);
        }
        return id;
    }

    private static string HeaderName(byte[] header)
    {
        if (header == null || header.Length < 16)
        {
            return null;
        }
        int end = 8;
        while (end < header.Length && header[end] != 0)
        {
            end++;
        }
        return end > 8 ? Encoding.UTF8.GetString(header, 8, end - 8) : null;
    }

    public void Release()
    {
        Entries.Clear();
        Terrain = null;
        Info = null;
        Units = null;
        Doodads = null;
        Pathing = null;
        Imports = null;
        ObjectData.Clear();
        Header = null;
    }

    public byte[] Find(string name)
    {
        foreach (var entry in Entries)
        {
            if (string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Data;
            }
        }
        return null;
    }

    private void Replace(string name, byte[] data)
    {
        foreach (var entry in Entries)
        {
            if (string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                entry.Data = data;
                return;
            }
        }
        Entries.Add(new Entry
        {
            Name = name,
            NameA = MpqCrypto.Hash(name, MpqCrypto.HashNameA),
            NameB = MpqCrypto.Hash(name, MpqCrypto.HashNameB),
            Data = data,
            Compress = true,
        });
    }

    private void EnsureListFileComplete()
    {
        var missing = new List<string>();
        foreach (var entry in Entries)
        {
            if (entry.Name != null && !entry.Name.StartsWith("_Locales", StringComparison.OrdinalIgnoreCase))
            {
                missing.Add(entry.Name);
            }
        }

        var existing = Find("(listfile)");
        string text = existing != null ? Encoding.UTF8.GetString(existing) : string.Empty;
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in text.Split('\n'))
        {
            known.Add(line.Trim('\r', ' ', '\0'));
        }

        var builder = new StringBuilder(text);
        if (builder.Length > 0 && builder[^1] != '\n')
        {
            builder.Append("\r\n");
        }
        bool added = false;
        foreach (string name in missing)
        {
            if (known.Contains(name))
            {
                continue;
            }
            builder.Append(name).Append("\r\n");
            added = true;
        }

        if (added || existing == null)
        {
            Replace("(listfile)", Encoding.UTF8.GetBytes(builder.ToString()));
        }
    }

    public void SetMapName(string name)
    {
        int stringId = W3iInfo.ExtractStringId(Info.NameRef);
        if (stringId < 0)
        {
            throw new InvalidOperationException($"w3i 里的地图名不是 TRIGSTR 引用：{Info.NameRef}");
        }

        bool changed = false;
        foreach (var entry in Entries)
        {
            if (entry.Name == null || !entry.Name.EndsWith("war3map.wts", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var strings = WtsDocument.Parse(entry.Data);
            if (strings.Get(stringId) == null)
            {
                continue;
            }
            strings.Set(stringId, name);
            entry.Data = strings.ToBytes();
            changed = true;
        }

        if (!changed)
        {
            throw new InvalidOperationException("没有可修改的 war3map.wts");
        }
        MapName = name;
    }

    public void Save(string path)
    {
        EnsureListFileComplete();

        var writer = new MpqWriter();
        foreach (var entry in Entries)
        {
            if (entry.Name != null)
            {
                writer.Add(entry.Name, entry.Data, entry.Compress);
            }
            else
            {
                writer.AddHashed(entry.NameA, entry.NameB, entry.Data, entry.Compress);
            }
        }
        writer.Write(path, Header);
    }
}
