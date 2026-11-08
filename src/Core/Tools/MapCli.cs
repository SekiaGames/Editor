using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using WorldEdit.Core.Formats.Mpq;
using WorldEdit.Core.Formats.W3x;
using WorldEdit.Core.Map;

namespace WorldEdit.Core.Tools;

public static class MapCli
{
    public static void Report(string path)
    {
        var document = MapDocument.Load(path);
        Console.WriteLine($"[MAP] 文件 {path}");
        Console.WriteLine($"[MAP] 名称='{document.MapName}' 条目={document.Entries.Count} 源={document.SourcePath}");

        W3xInfoLines(document);
        W3xTerrainLines(document);
        W3xActorLines(document);
        W3xDataLines(document);

        foreach (string warning in document.Warnings)
        {
            Console.WriteLine($"[WARN] {warning}");
        }
    }

    private static void W3xInfoLines(MapDocument document)
    {
        if (document.Info == null || document.Info.Missing)
        {
            Console.WriteLine("[W3I] 缺失");
        }
        else
        {
            W3xInfoDetail(document);
        }
    }

    private static void W3xInfoDetail(MapDocument document)
    {
        var info = document.Info;
        string game = info.HasGameVersion
            ? $"{info.GameMajor}.{info.GameMinor}.{info.GamePatch}.{info.GameBuild}"
            : "未记录";
        Console.WriteLine($"[W3I] 格式 v{info.FormatVersion} 保存次数 {info.SaveCount} 编辑器版本 {info.EditorVersion} 游戏版本 {game}");
        Console.WriteLine($"[W3I] 可玩面积 {info.PlayableWidth}x{info.PlayableHeight} 地形集 {info.Tileset} flags=0x{info.Flags:X4} 战役背景 {info.CampaignBackground}");
        Console.WriteLine($"[W3I] 名称={info.NameRef} 作者={info.AuthorRef}");
        if (!string.IsNullOrEmpty(info.MinimapTgaPath))
        {
            Console.WriteLine($"[W3I] 小地图底图 {info.MinimapTgaPath} {info.MinimapTgaWidth}x{info.MinimapTgaHeight}");
        }
        Console.WriteLine($"[W3I] 环境 雾={info.Environment.UseTerrainFog} 高度 {info.Environment.FogHeightStart}~{info.Environment.FogHeightEnd} "
            + $"密度 {info.Environment.FogDensity} 天气={info.Environment.GlobalWeather} 光照集={info.Environment.LightEnvTileset} 声音={info.Environment.SoundEnvironment}");
        foreach (W3iPlayer player in info.Players)
        {
            Console.WriteLine($"[W3I] 玩家{player.Id} 控制={player.Controller} 种族={player.Race} 固定点={player.FixedStartPosition} 起点({player.StartX},{player.StartY}) 名称={player.Name}");
        }
        foreach (W3iForce force in info.Forces)
        {
            Console.WriteLine($"[W3I] 势力 flags={force.Flags} 掩码=0x{force.PlayerMask:X8} 名称={force.Name}");
        }
    }

    private static void W3xTerrainLines(MapDocument document)
    {
        if (document.Terrain == null)
        {
            Console.WriteLine("[W3E] 缺失");
            return;
        }
        var terrain = document.Terrain;
        Console.WriteLine($"[W3E] v{terrain.Version} 地形集 {terrain.Tileset} 角点 {terrain.Width}x{terrain.Height} "
            + $"原点({terrain.OffsetX},{terrain.OffsetY}) 地表表 {terrain.GroundTextures.Count} 悬崖表 {terrain.CliffTextures.Count}");
        if (document.Pathing != null)
        {
            Console.WriteLine($"[WPM] {document.Pathing.Width}x{document.Pathing.Height} 单元 不可走={document.Pathing.Count(W3mPathing.Unwalkable)}");
        }
    }

    private static void W3xActorLines(MapDocument document)
    {
        if (document.Units != null)
        {
            int units = 0;
            int items = 0;
            int scaled = 0;
            int colored = 0;
            foreach (UnitPlacement entry in document.Units.Entries)
            {
                if (entry.IsItem)
                {
                    items++;
                }
                else
                {
                    units++;
                }
                if (entry.ScaleX != 1f || entry.ScaleY != 1f || entry.ScaleZ != 1f)
                {
                    scaled++;
                }
                if (entry.CustomTeamColor >= 0)
                {
                    colored++;
                }
            }
            string skin = document.Units.HasSkinId ? "有" : "无";
            Console.WriteLine($"[DOO] 单位 {units} 物品 {items} 记录版本 v{document.Units.Version} skinId {skin} "
                + $"非1缩放 {scaled} 自定义颜色 {colored}");
        }

        if (document.Doodads != null)
        {
            int hidden = 0;
            int dead = 0;
            foreach (DoodadPlacement entry in document.Doodads.Entries)
            {
                if (entry.Hidden)
                {
                    hidden++;
                }
                if (entry.Dead)
                {
                    dead++;
                }
            }
            Console.WriteLine($"[DOO] 装饰物 {document.Doodads.Entries.Count} 记录版本 v{document.Doodads.Version}.{document.Doodads.SubVersion} "
                + $"步长 {document.Doodads.Stride} 尾部 {document.Doodads.Trailing} 字节 隐藏 {hidden} 死亡 {dead}");
        }
    }

    private static void W3xDataLines(MapDocument document)
    {
        foreach (var pair in document.ObjectData)
        {
            W3xObjectData data = pair.Value;
            int modifications = 0;
            foreach (W3xObjectDefinition definition in data.Objects)
            {
                modifications += definition.Modifications.Count;
            }
            Console.WriteLine($"[OBJ] {pair.Key} v{data.Version} 扩展字段={(data.Extended ? "有" : "无")} "
                + $"原始 {data.OriginalObjects.Count} 自定义 {data.CustomObjects.Count} 字段 {modifications}");
        }

        if (document.Imports != null)
        {
            Console.WriteLine($"[IMP] v{document.Imports.Version} 条目 {document.Imports.Entries.Count} 原字节回写一致={document.Imports.RoundTrips()}");
        }
    }

    public static void SaveAs(string source, string target, string newName)
    {
        var document = MapDocument.Load(source);
        Console.WriteLine($"[MAP] 载入 {document.Entries.Count} 个文件, 名称='{document.MapName}', 尺寸={document.Terrain.Width}x{document.Terrain.Height}");

        var before = new Dictionary<string, string>();
        foreach (var entry in document.Entries)
        {
            before[Key(entry)] = Md5(entry.Data);
        }

        document.SetMapName(newName);
        document.Save(target);
        Console.WriteLine($"[MAP] 已写入 {target} ({new FileInfo(target).Length} bytes)");

        var reloaded = MapDocument.Load(target);
        Console.WriteLine($"[MAP] 重新打开: 名称='{reloaded.MapName}', 文件数={reloaded.Entries.Count}");

        int same = 0;
        int diff = 0;
        foreach (var entry in reloaded.Entries)
        {
            string key = Key(entry);
            string md5 = Md5(entry.Data);
            if (before.TryGetValue(key, out string old) && old == md5)
            {
                same++;
            }
            else
            {
                diff++;
                Console.WriteLine($"[MAP] 内容变化: {key} {before.GetValueOrDefault(key, "缺失")} -> {md5}");
            }
        }
        Console.WriteLine($"[MAP] 逐文件比对: 相同 {same}, 变化 {diff}");
    }

    private static string Key(MapDocument.Entry entry)
    {
        return entry.Name ?? $"#{entry.NameA:X8}";
    }

    private static string Md5(byte[] data)
    {
        return data == null ? "null" : Convert.ToHexString(MD5.HashData(data)).ToLowerInvariant();
    }
}
