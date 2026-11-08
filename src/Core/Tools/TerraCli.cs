using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WorldEdit.Core.Formats.W3x;

namespace WorldEdit.Core.Tools;

public static class TerraCli
{
    private const float CellSize = 128f;

    private sealed class W3eHeader
    {
        public int Version;
        public int Version2;
        public char Tileset;
        public int Width;
        public int Height;
        public float OffsetX;
        public float OffsetY;
        public readonly List<string> Ground = new();
        public readonly List<string> Cliffs = new();
    }

    private static W3eHeader ReadHeader(byte[] data)
    {
        if (data.Length < 69 || Encoding.ASCII.GetString(data, 0, 4) != "W3E!")
        {
            throw new InvalidDataException("不是 war3map.w3e");
        }

        var header = new W3eHeader
        {
            Version = BitConverter.ToInt32(data, 4),
            Tileset = (char)data[8],
            Version2 = BitConverter.ToInt32(data, 9),
        };

        int pos = 13;
        int groundCount = BitConverter.ToInt32(data, pos);
        pos += 4;
        for (int i = 0; i < groundCount; i++)
        {
            header.Ground.Add(Encoding.ASCII.GetString(data, pos, 4));
            pos += 4;
        }

        int cliffCount = BitConverter.ToInt32(data, pos);
        pos += 4;
        for (int i = 0; i < cliffCount; i++)
        {
            header.Cliffs.Add(Encoding.ASCII.GetString(data, pos, 4));
            pos += 4;
        }

        header.Width = BitConverter.ToInt32(data, pos);
        header.Height = BitConverter.ToInt32(data, pos + 4);
        header.OffsetX = BitConverter.ToSingle(data, pos + 8);
        header.OffsetY = BitConverter.ToSingle(data, pos + 12);
        return header;
    }

    private static string ResolveW3e(string path)
    {
        string full = CliPath.Resolve(path);
        if (System.IO.Directory.Exists(full))
        {
            return System.IO.Path.Combine(full, "war3map.w3e");
        }
        return full;
    }

    private static W3eTerrain LoadTerrain(string path)
    {
        string file = ResolveW3e(path);
        if (!System.IO.File.Exists(file))
        {
            throw new System.IO.FileNotFoundException($"找不到 {file}");
        }
        return W3eTerrain.Parse(System.IO.File.ReadAllBytes(file));
    }

    private static float FinalGround(in W3eTerrain.Corner corner)
    {
        return corner.HeightWorld + corner.LayerHeight - 2f;
    }

    private static bool IsRampEntrance(W3eTerrain terrain, int i, int j)
    {
        if (i + 1 >= terrain.Width || j + 1 >= terrain.Height)
        {
            return false;
        }
        int w = terrain.Width;
        W3eTerrain.Corner bl = terrain.Corners[j * w + i];
        W3eTerrain.Corner br = terrain.Corners[j * w + i + 1];
        W3eTerrain.Corner tl = terrain.Corners[(j + 1) * w + i];
        W3eTerrain.Corner tr = terrain.Corners[(j + 1) * w + i + 1];
        if (!(bl.Ramp && tl.Ramp && br.Ramp && tr.Ramp))
        {
            return false;
        }
        return !(bl.LayerHeight == tr.LayerHeight && tl.LayerHeight == br.LayerHeight);
    }

    private static float RenderHeight(W3eTerrain terrain, float x, float y, bool withRampHalf)
    {
        int tx = Math.Min((int)x, terrain.Width - 2);
        int ty = Math.Min((int)y, terrain.Height - 2);
        tx = Math.Max(tx, 0);
        ty = Math.Max(ty, 0);
        float u = x - tx;
        float v = y - ty;

        float Height(int i, int j)
        {
            float value = FinalGround(terrain.Corners[j * terrain.Width + i]);
            if (withRampHalf && IsRampEntrance(terrain, i, j))
            {
                value += 0.5f;
            }
            return value;
        }

        float h00 = Height(tx, ty);
        float h10 = Height(tx + 1, ty);
        float h01 = Height(tx, ty + 1);
        float h11 = Height(tx + 1, ty + 1);

        if (u + v >= 1f)
        {
            return h11 * (u + v - 1f) + h10 * (1f - v) + h01 * (1f - u);
        }
        return h00 * (1f - u - v) + h10 * u + h01 * v;
    }

    public static void Probe(string path, string mode, string spec)
    {
        W3eTerrain terrain = LoadTerrain(path);
        Console.WriteLine($"w3e {terrain.Width}x{terrain.Height} tileset={terrain.Tileset} "
            + $"地表 {terrain.GroundTextures.Count} 种，悬崖 {terrain.CliffTextures.Count} 种");

        if (mode == "ramps")
        {
            int count = 0;
            for (int j = 0; j < terrain.Height - 1; j++)
            {
                for (int i = 0; i < terrain.Width - 1; i++)
                {
                    if (!IsRampEntrance(terrain, i, j))
                    {
                        continue;
                    }
                    count++;
                    int w = terrain.Width;
                    W3eTerrain.Corner bl = terrain.Corners[j * w + i];
                    W3eTerrain.Corner br = terrain.Corners[j * w + i + 1];
                    W3eTerrain.Corner tl = terrain.Corners[(j + 1) * w + i];
                    W3eTerrain.Corner tr = terrain.Corners[(j + 1) * w + i + 1];
                    Console.WriteLine($"ramp entrance at {i},{j} layers=[{bl.LayerHeight},{br.LayerHeight},{tl.LayerHeight},{tr.LayerHeight}] "
                        + $"raw_h=[{bl.HeightWorld:0.00},{br.HeightWorld:0.00},{tl.HeightWorld:0.00},{tr.HeightWorld:0.00}] "
                        + $"tex={bl.GroundTexture} cliff_tex={bl.CliffTexture}");
                }
            }
            Console.WriteLine($"合计 {count} 个斜坡入口格");
            return;
        }

        string[] parts = (spec ?? "").Split(',');
        if (parts.Length < 4)
        {
            throw new ArgumentException("剖面需要 x0,y0,x1,y1[,steps]");
        }
        float x0 = float.Parse(parts[0]);
        float y0 = float.Parse(parts[1]);
        float x1 = float.Parse(parts[2]);
        float y1 = float.Parse(parts[3]);
        int steps = parts.Length > 4 && int.TryParse(parts[4], out int parsed) ? parsed : 32;

        for (int s = 0; s <= steps; s++)
        {
            float t = (float)s / steps;
            float x = x0 + (x1 - x0) * t;
            float y = y0 + (y1 - y0) * t;
            float height = RenderHeight(terrain, x, y, true);
            int tx = Math.Clamp((int)x, 0, terrain.Width - 2);
            int ty = Math.Clamp((int)y, 0, terrain.Height - 2);
            Console.WriteLine($"{s,3} {x,8:0.000} {y,8:0.000}  h={height,7:0.0000}  "
                + $"tile=({tx},{ty}) layer={terrain.Corners[ty * terrain.Width + tx].LayerHeight}");
        }
    }

    private static Vector3 TileToWorld(float tx, float ty, float heightTiles)
    {
        return new Vector3(-tx * CellSize, heightTiles * CellSize, ty * CellSize);
    }

    private static (Vector3 Forward, Vector3 Right, Vector3 Up) CameraBasis(float yawDegrees, float pitchRadians)
    {
        float yaw = Mathf.DegToRad(yawDegrees);
        var forward = new Vector3(
            -Mathf.Cos(pitchRadians) * Mathf.Sin(yaw),
            Mathf.Sin(pitchRadians),
            -Mathf.Cos(pitchRadians) * Mathf.Cos(yaw));
        var right = new Vector3(-forward.Z, 0f, forward.X).Normalized();
        Vector3 up = right.Cross(forward);
        return (forward, right, up);
    }

    private static Vector2? Project(Vector3 eye, Vector3 point, int width, int height,
        float fovDegrees, float yawDegrees, float pitchRadians)
    {
        (Vector3 forward, Vector3 right, Vector3 up) = CameraBasis(yawDegrees, pitchRadians);
        Vector3 d = point - eye;
        float depth = d.Dot(forward);
        if (depth <= 1e-6f)
        {
            return null;
        }
        float xc = d.Dot(right);
        float yc = d.Dot(up);
        float t = Mathf.Tan(Mathf.DegToRad(fovDegrees) * 0.5f);
        float aspect = (float)width / height;
        return new Vector2(
            (xc / (depth * t * aspect) * 0.5f + 0.5f) * width,
            (0.5f - yc / (depth * t) * 0.5f) * height);
    }

    private static void DrawLine(Image image, Vector2 a, Vector2 b, Color color, int width)
    {
        int steps = (int)MathF.Max(MathF.Abs(b.X - a.X), MathF.Abs(b.Y - a.Y));
        steps = Math.Max(steps, 1);
        int half = Math.Max(width / 2, 0);
        int imageWidth = image.GetWidth();
        int imageHeight = image.GetHeight();

        for (int s = 0; s <= steps; s++)
        {
            float t = (float)s / steps;
            int x = Mathf.RoundToInt(a.X + (b.X - a.X) * t);
            int y = Mathf.RoundToInt(a.Y + (b.Y - a.Y) * t);
            for (int oy = -half; oy <= half; oy++)
            {
                for (int ox = -half; ox <= half; ox++)
                {
                    int px = x + ox;
                    int py = y + oy;
                    if (px >= 0 && py >= 0 && px < imageWidth && py < imageHeight)
                    {
                        image.SetPixel(px, py, color);
                    }
                }
            }
        }
    }

    public static void DrawTiles(string png, string rows, string range, string w3e, string eyeSpec)
    {
        string imagePath = CliPath.Resolve(png);
        var image = new Image();
        Error error = image.LoadPngFromBuffer(System.IO.File.ReadAllBytes(imagePath));
        if (error != Error.Ok || image.IsEmpty())
        {
            throw new InvalidOperationException($"读不了 {png}（{error}）");
        }
        image.Convert(Image.Format.Rgb8);

        string w3ePath = string.IsNullOrEmpty(w3e)
            ? "HiveWE/Data/Maps/(2)BootyBay/war3map.w3e"
            : w3e;
        W3eTerrain terrain = LoadTerrain(w3ePath);

        float x0 = 50f, x1 = 75f;
        if (!string.IsNullOrEmpty(range))
        {
            string[] pair = range.Split(',');
            if (pair.Length == 2)
            {
                x0 = float.Parse(pair[0]);
                x1 = float.Parse(pair[1]);
            }
        }

        Vector3 eye = TileToWorld(61.922f, 33f, 5.52745f);
        if (!string.IsNullOrEmpty(eyeSpec))
        {
            string[] triplet = eyeSpec.Split(',');
            if (triplet.Length == 3)
            {
                eye = TileToWorld(float.Parse(triplet[0]), float.Parse(triplet[1]), float.Parse(triplet[2]));
            }
        }

        Color[] colors =
        {
            new(1f, 0f, 1f), new(1f, 1f, 0f), new(0f, 1f, 1f), new(1f, 0.5f, 0f),
        };

        int width = image.GetWidth();
        int height = image.GetHeight();
        string[] lines = rows.Split(',');
        for (int index = 0; index < lines.Length; index++)
        {
            float ty = float.Parse(lines[index]);
            Vector2? previous = null;
            Vector2? middle = null;
            const int steps = 40;
            for (int s = 0; s <= steps; s++)
            {
                float tx = x0 + (x1 - x0) * s / steps;
                float groundHeight = RenderHeight(terrain, tx, ty, true);
                Vector3 point = TileToWorld(tx, ty, groundHeight);
                Vector2? projected = Project(eye, point, width, height, 50.705f, 180f, -0.977f);
                if (projected.HasValue && previous.HasValue)
                {
                    DrawLine(image, previous.Value, projected.Value, colors[index % colors.Length], 3);
                }
                if (s == steps / 2)
                {
                    middle = projected;
                }
                previous = projected;
            }
            Console.WriteLine($"y={ty} -> {(middle.HasValue ? ((int)middle.Value.X, (int)middle.Value.Y).ToString() : "屏外")}");
        }

        string output = imagePath.Replace(".png", "_lines.png");
        CliPath.EnsureDirectory(output);
        image.SavePng(output);
        Console.WriteLine($"已写出 {output}");
    }

    public static void DumpUnits(string mapPath)
    {
        var document = Core.Map.MapDocument.Load(CliPath.Resolve(mapPath));
        var units = document.Units;
        if (units == null)
        {
            Console.WriteLine("[UNIT] 这张地图没有 war3mapUnits.doo");
            document.Release();
            return;
        }

        var unitIds = new Dictionary<string, int>(StringComparer.Ordinal);
        var itemIds = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (UnitPlacement entry in units.Entries)
        {
            var counter = entry.IsItem ? itemIds : unitIds;
            counter[entry.Id] = counter.TryGetValue(entry.Id, out int n) ? n + 1 : 1;
        }

        Console.WriteLine($"[UNIT] version={units.Version} subversion={units.SubVersion} "
            + $"共 {units.Entries.Count} 条：单位 {CountOf(unitIds)} 个（{unitIds.Count} 种）、"
            + $"物品 {CountOf(itemIds)} 个（{itemIds.Count} 种）");

        foreach (var pair in Sorted(unitIds))
        {
            Console.WriteLine($"  {pair.Key,-6} {pair.Value,3} 个");
        }
        foreach (var pair in Sorted(itemIds))
        {
            Console.WriteLine($"  {pair.Key,-6} {pair.Value,3} 个  (物品)");
        }

        Console.WriteLine("[UNIT] 全部坐标：");
        int shown = 0;
        float offsetX = document.Terrain.OffsetX;
        float offsetY = document.Terrain.OffsetY;
        foreach (UnitPlacement entry in units.Entries)
        {
            shown++;
            float gx = (entry.X - offsetX) / 128f;
            float gy = (entry.Y - offsetY) / 128f;
            Console.WriteLine($"  {entry.Id,-6} 玩家{entry.Player,-3} 格({gx,7:0.00},{gy,7:0.00}) "
                + $"高{entry.Z,7:0.0} 朝向{entry.Angle,5:0.00} {(entry.IsItem ? "物品" : "单位")}");
        }
        document.Release();
    }

    private static int CountOf(Dictionary<string, int> counter)
    {
        int total = 0;
        foreach (var pair in counter)
        {
            total += pair.Value;
        }
        return total;
    }

    private static List<KeyValuePair<string, int>> Sorted(Dictionary<string, int> counter)
    {
        var list = new List<KeyValuePair<string, int>>(counter);
        list.Sort((a, b) => b.Value.CompareTo(a.Value));
        return list;
    }

    public static void DumpDoodads(string mapPath)
    {
        var document = WorldEdit.Core.Map.MapDocument.Load(CliPath.Resolve(mapPath));
        var doodads = document.Doodads;
        if (doodads == null)
        {
            Console.WriteLine("[DOODAD] 这张地图没有 war3map.doo");
            document.Release();
            return;
        }

        Console.WriteLine($"[DOODAD] version={doodads.Version} subversion={doodads.SubVersion} "
            + $"声明 {doodads.DeclaredCount} 条，步长 {doodads.Stride}，尾部余 {doodads.Trailing} 字节");
        Console.WriteLine($"[DOODAD] 地形原点 ({document.Terrain.OffsetX:0.0}, {document.Terrain.OffsetY:0.0})，"
            + $"{document.Terrain.Width - 1}x{document.Terrain.Height - 1} 格");

        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        var flags = new Dictionary<byte, int>();
        var lives = new Dictionary<byte, int>();
        float minZ = float.MaxValue, maxZ = float.MinValue;
        float minScale = float.MaxValue, maxScale = float.MinValue;
        int hidden = 0;
        int dead = 0;
        int badId = 0;
        foreach (DoodadPlacement entry in doodads.Entries)
        {
            ids[entry.Id] = ids.TryGetValue(entry.Id, out int n) ? n + 1 : 1;
            flags[entry.Flags] = flags.TryGetValue(entry.Flags, out int f) ? f + 1 : 1;
            lives[entry.Life] = lives.TryGetValue(entry.Life, out int l) ? l + 1 : 1;
            minZ = MathF.Min(minZ, entry.Z);
            maxZ = MathF.Max(maxZ, entry.Z);
            minScale = MathF.Min(minScale, entry.UniformScale);
            maxScale = MathF.Max(maxScale, entry.UniformScale);
            if (entry.Hidden)
            {
                hidden++;
            }
            if (entry.Life == 0)
            {
                dead++;
            }
            foreach (char c in entry.Id)
            {
                if (c < 'A' || c > 'Z')
                {
                    if (c < 'a' || c > 'z')
                    {
                        badId++;
                    }
                    break;
                }
            }
        }

        Console.WriteLine($"[DOODAD] {doodads.Entries.Count} 条，{ids.Count} 种，隐藏 {hidden}，生命 0 的 {dead}，ID 非字母 {badId}");
        Console.WriteLine($"[DOODAD] Z 范围 {minZ:0.0} ~ {maxZ:0.0}，缩放 {minScale:0.000} ~ {maxScale:0.000}");
        Console.Write("[DOODAD] flags 分布:");
        foreach (var pair in flags)
        {
            Console.Write($" {pair.Key}={pair.Value}");
        }
        Console.WriteLine();
        Console.Write("[DOODAD] life 分布:");
        int shown = 0;
        foreach (var pair in lives)
        {
            if (shown++ >= 12)
            {
                Console.Write(" ...");
                break;
            }
            Console.Write($" {pair.Key}={pair.Value}");
        }
        Console.WriteLine();

        Console.WriteLine("[DOODAD] 种类（按数量）:");
        int row = 0;
        foreach (var pair in Sorted(ids))
        {
            if (row++ >= 40)
            {
                Console.WriteLine($"  ... 还有 {ids.Count - 40} 种");
                break;
            }
            Console.WriteLine($"  {pair.Key,-6} {pair.Value,4} 个  {DescribeDoodad(pair.Key)}");
        }

        Console.WriteLine("[DOODAD] 前 8 条原始数据:");
        for (int i = 0; i < Math.Min(8, doodads.Entries.Count); i++)
        {
            DoodadPlacement entry = doodads.Entries[i];
            Console.WriteLine($"  {entry.Id,-6} var={entry.Variation,-3} 世界({entry.X,9:0.0},{entry.Y,9:0.0},{entry.Z,7:0.0}) "
                + $"角度{entry.Angle,6:0.000} 缩放({entry.ScaleX:0.00},{entry.ScaleY:0.00},{entry.ScaleZ:0.00}) "
                + $"flags={entry.Flags} life={entry.Life} creation={entry.CreationNumber}");
        }
        document.Release();
    }

    private static string DescribeDoodad(string id)
    {
        var destructables = WorldEdit.Assets.Generated.War3Tables.DestructableData();
        int row = destructables.FindRow("DestructableID", id);
        if (row >= 0)
        {
            return $"破坏物 {destructables.Get(row, "file")} var={destructables.Get(row, "numVar")}";
        }
        var doodads = WorldEdit.Assets.Generated.War3Tables.Doodads();
        row = doodads.FindRow("doodID", id);
        if (row < 0)
        {
            return "(两张表里都没有)";
        }
        return $"装饰物 {doodads.Get(row, "file")} var={doodads.Get(row, "numVar")}";
    }

    private static List<string> GroundTileIds(char tileset)
    {
        string directory = tileset switch
        {
            'L' => "LordaeronSummer",
            'A' => "Ashenvale",
            'B' => "Barrens",
            'F' => "Felwood",
            'N' => "Northrend",
            'D' => "Dungeon",
            'G' => "Dalaran",
            'X' => "Village",
            'Y' => "VillageFall",
            'Z' => "DalaranRuins",
            'C' => "Cityscape",
            'I' => "Icecrown",
            _ => "LordaeronSummer",
        };

        var table = WorldEdit.Assets.Generated.War3Tables.Terrain();
        var ids = new List<string>();
        foreach (int row in table.DataRows)
        {
            string tile = table.Get(row, "tileID");
            string dir = table.Get(row, "dir") ?? "";
            if (!string.IsNullOrEmpty(tile) && dir.Contains(directory, StringComparison.Ordinal))
            {
                ids.Add(tile);
            }
        }
        return ids;
    }

    public static void PaintMap(string directory, string tileset, string cliffList)
    {
        string root = CliPath.Resolve(directory);
        string path = System.IO.Path.Combine(root, "war3map.w3e");
        if (!System.IO.File.Exists(path))
        {
            throw new System.IO.FileNotFoundException($"找不到 {path}");
        }

        byte[] original = System.IO.File.ReadAllBytes(path);
        W3eHeader header = ReadHeader(original);

        char targetTileset = string.IsNullOrEmpty(tileset) ? header.Tileset : tileset[0];
        List<string> cliffs = string.IsNullOrEmpty(cliffList)
            ? new List<string>(header.Cliffs)
            : new List<string>(cliffList.Split(',', StringSplitOptions.RemoveEmptyEntries));

        List<string> ground = GroundTileIds(targetTileset);
        Console.WriteLine($"{path}: {header.Width}x{header.Height} tileset={header.Tileset}->{targetTileset} "
            + $"地表 {ground.Count} 种 [{string.Join(",", ground)}]");

        using var stream = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(stream);
        writer.Write(Encoding.ASCII.GetBytes("W3E!"));
        writer.Write(header.Version);
        writer.Write((byte)targetTileset);
        writer.Write(header.Version2);

        writer.Write(ground.Count);
        foreach (string tile in ground)
        {
            writer.Write(Encoding.ASCII.GetBytes(tile.Length >= 4 ? tile[..4] : tile.PadRight(4)));
        }

        writer.Write(cliffs.Count);
        foreach (string cliff in cliffs)
        {
            writer.Write(Encoding.ASCII.GetBytes(cliff.Length >= 4 ? cliff[..4] : cliff.PadRight(4)));
        }

        writer.Write(header.Width);
        writer.Write(header.Height);
        writer.Write(header.OffsetX);
        writer.Write(header.OffsetY);

        int bandCount = ground.Count;
        int width = header.Width;
        int height = header.Height;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int band = y / 4;
                int groundTexture = 4;
                int flags = 0;
                int layerHeight = 2;
                int variation = (x * 7 + y * 3) & 0x1F;
                int rawHeight = 8192;
                int rawWater = 8192;

                if (band < bandCount)
                {
                    groundTexture = band;
                }
                else if (band == bandCount)
                {
                    groundTexture = 4;
                    flags |= 0x20;
                }
                else if (band == bandCount + 1)
                {
                    groundTexture = 4;
                    layerHeight = 2 + (x / 6) % 3;
                }
                else if (band == bandCount + 2)
                {
                    groundTexture = 0;
                    flags |= 0x40;
                    layerHeight = 0;
                    rawHeight = 8192 - 512 * 2;
                }
                else if (band == bandCount + 3)
                {
                    groundTexture = 0;
                    flags |= 0x40;
                    rawHeight = 8192 - 40;
                }
                else
                {
                    groundTexture = 4;
                    rawHeight = 8192 + (int)(500f * (x / (float)width - 0.5f) * 2f) + 150 * (y % 8);
                }

                flags |= groundTexture & 0x0F;
                writer.Write((ushort)Math.Clamp(rawHeight, 0, 65535));
                writer.Write((ushort)Math.Clamp(rawWater, 0, 0x3FFF));
                writer.Write((byte)flags);
                writer.Write((byte)variation);
                writer.Write((byte)(layerHeight & 0x0F));
            }
        }

        writer.Flush();
        System.IO.File.WriteAllBytes(path, stream.ToArray());
        Console.WriteLine($"已写入：{bandCount} 条地表带 + blight + 悬崖阶梯 + 深水/浅水 + 高度起伏，共 {stream.Length} 字节");
    }
}
