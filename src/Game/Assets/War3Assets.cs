using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.Slk;

namespace WorldEdit.Assets;

public static class War3Assets
{

    public static string GameRoot { get; private set; } = "D:/War3_1.27a";

    public const string ConvertedRoot = "res://assets/converted/";

    private static readonly string[] ArchiveNames =
    {
        "War3Patch.mpq", "War3xLocal.mpq", "War3x.mpq", "war3.mpq",
    };

    private static WorldEdit.Core.Formats.Mpq.MpqArchive[] _archives;

    public static void SetGameRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        GameRoot = path.Replace('\\', '/').TrimEnd('/');
        _archives = null;
        GD.Print($"[ASSET] 游戏目录 = {GameRoot}");
    }

    private static WorldEdit.Core.Formats.Mpq.MpqArchive[] Archives
    {
        get
        {
            if (_archives != null)
            {
                return _archives;
            }

            var opened = new List<WorldEdit.Core.Formats.Mpq.MpqArchive>();
            foreach (string name in ArchiveNames)
            {
                string path = $"{GameRoot}/{name}";
                if (!System.IO.File.Exists(path))
                {
                    continue;
                }
                try
                {
                    opened.Add(WorldEdit.Core.Formats.Mpq.MpqArchive.Open(path));
                }
                catch (Exception e)
                {
                    GD.PushWarning($"[ASSET] 打不开 {name}: {e.Message}");
                }
            }

            GD.Print($"[ASSET] 已挂载 {opened.Count} 个 MPQ：{string.Join(",", opened.ConvertAll(a => System.IO.Path.GetFileName(a.Path)))}");
            _archives = opened.ToArray();
            return _archives;
        }
    }

    public static bool Exists(string relative)
    {
        if (ConvertedPath(relative) != null)
        {
            return true;
        }

        string rel = relative.Replace('/', '\\');
        foreach (var archive in Archives)
        {
            if (archive.Contains(rel))
            {
                return true;
            }
        }
        return false;
    }

    private static string ConvertedPath(string relative)
    {
        string rel = relative.Replace('\\', '/');
        int slash = rel.LastIndexOf('/');
        string directory = slash >= 0 ? rel[..(slash + 1)] : "";
        string file = slash >= 0 ? rel[(slash + 1)..] : rel;
        int dot = file.LastIndexOf('.');
        if (dot > 0)
        {
            file = file[..dot];
        }

        string plain = ConvertedRoot + directory + file + ".png";
        if (ResourceLoader.Exists(plain))
        {
            return plain;
        }

        string withExtension = ConvertedRoot + rel + ".png";
        return ResourceLoader.Exists(withExtension) ? withExtension : null;
    }

    private static Image LoadConverted(string relative)
    {
        string path = ConvertedPath(relative);
        if (path == null)
        {
            return null;
        }

        var texture = ResourceLoader.Load<Texture2D>(path);
        Image image = texture?.GetImage();
        if (image == null)
        {
            return null;
        }
        if (image.IsCompressed())
        {
            image.Decompress();
        }
        image.Convert(Image.Format.Rgba8);
        return image;
    }

    public static byte[] Read(string relative)
    {
        string rel = relative.Replace('/', '\\');
        foreach (var archive in Archives)
        {
            byte[] data = archive.TryRead(rel);
            if (data != null && data.Length > 0)
            {
                return data;
            }
        }
        throw new System.IO.FileNotFoundException($"资源不存在: {rel}（游戏目录 {GameRoot}）");
    }

    public static Image LoadImage(string relative)
    {
        Image converted = LoadConverted(relative);
        if (converted != null)
        {
            return converted;
        }
        return BlpDecoder.Decode(Read(relative));
    }
}
