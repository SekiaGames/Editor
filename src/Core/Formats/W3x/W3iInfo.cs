using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace WorldEdit.Core.Formats.W3x;

public sealed record W3iPlayer(
    int Id,
    int Controller,
    int Race,
    int Flags,
    string Name,
    float StartX,
    float StartY,
    int AllyLow,
    int AllyHigh,
    int EnemyLow,
    int EnemyHigh)
{
    public const int FixedStartPositionFlag = 1;

    public bool FixedStartPosition => (Flags & FixedStartPositionFlag) != 0;
}

public sealed record W3iForce(int Flags, uint PlayerMask, string Name)
{
    public const int AlliedFlag = 1;
    public const int AlliedVictoryFlag = 2;
    public const int SharedVisionFlag = 8;
    public const int SharedControlFlag = 16;

    public bool ContainsPlayer(int playerId)
    {
        return playerId is >= 0 and < 32 && (PlayerMask & (1u << playerId)) != 0;
    }
}

public sealed class W3iEnvironment
{
    public bool UseTerrainFog { get; init; }
    public float FogHeightStart { get; init; }
    public float FogHeightEnd { get; init; }
    public float FogDensity { get; init; }
    public byte[] FogColorBgra { get; init; } = new byte[4];
    public string GlobalWeather { get; init; } = "";
    public string SoundEnvironment { get; init; } = "";
    public char LightEnvTileset { get; init; }
    public byte[] WaterVertexColorBgra { get; init; } = new byte[4];
}

public sealed class W3iInfo
{
    public const int MeleeMapFlag = 0x0004;
    public const int MaskedAreasPartiallyVisibleFlag = 0x0010;
    public const int FixedPlayerSettingsFlag = 0x0020;
    public const int CustomForcesFlag = 0x0040;

    public int FormatVersion { get; private set; }
    public int SaveCount { get; private set; }
    public int EditorVersion { get; private set; }

    public bool HasGameVersion => FormatVersion > 27;
    public int GameMajor { get; private set; }
    public int GameMinor { get; private set; }
    public int GamePatch { get; private set; }
    public int GameBuild { get; private set; }
    public int GameVersionNumber => GameMajor * 100 + GameMinor;

    public string NameRef { get; private set; }
    public string AuthorRef { get; private set; }
    public string DescriptionRef { get; private set; }
    public string PlayersRef { get; private set; }

    public float[] CameraBounds { get; private set; } = new float[8];
    public int[] CameraBoundsComplements { get; private set; } = new int[4];

    public int PlayableWidth { get; private set; }
    public int PlayableHeight { get; private set; }
    public int Flags { get; private set; }
    public char Tileset { get; private set; }
    public int CampaignBackground { get; private set; }

    public string LoadingScreenModel { get; private set; } = "";
    public string LoadingScreenText { get; private set; } = "";
    public string LoadingScreenTitle { get; private set; } = "";
    public string LoadingScreenSubtitle { get; private set; } = "";
    public int GameDataSet { get; private set; }
    public string PrologueScreenModel { get; private set; } = "";
    public string PrologueText { get; private set; } = "";
    public string PrologueTitle { get; private set; } = "";
    public string PrologueSubtitle { get; private set; } = "";

    public W3iEnvironment Environment { get; private set; } = new();

    public string MinimapTgaPath { get; private set; } = "";
    public int MinimapTgaWidth { get; private set; }
    public int MinimapTgaHeight { get; private set; }

    public List<W3iPlayer> Players { get; } = new();
    public List<W3iForce> Forces { get; } = new();

    public byte[] RawBytes { get; private set; }

    public bool Missing { get; private set; }

    public bool IsMelee => (Flags & MeleeMapFlag) != 0;
    public bool UsesCustomForces => (Flags & CustomForcesFlag) != 0;
    public bool HasFixedPlayerSettings => (Flags & FixedPlayerSettingsFlag) != 0;
    public bool MaskedAreasPartiallyVisible => (Flags & MaskedAreasPartiallyVisibleFlag) != 0;

    public static W3iInfo CreateMissing()
    {
        return new W3iInfo
        {
            Missing = true,
            NameRef = "",
            AuthorRef = "",
            DescriptionRef = "",
            PlayersRef = "",
        };
    }

    public static W3iInfo Parse(byte[] data)
    {
        if (data == null || data.Length < 16)
        {
            throw new InvalidDataException("war3map.w3i 太短");
        }

        var reader = new W3xBinaryReader(data);
        var info = new W3iInfo
        {
            RawBytes = data,
            FormatVersion = reader.I32(),
            SaveCount = reader.I32(),
            EditorVersion = reader.I32(),
        };

        if (info.HasGameVersion)
        {
            info.GameMajor = reader.I32();
            info.GameMinor = reader.I32();
            info.GamePatch = reader.I32();
            info.GameBuild = reader.I32();
        }

        info.NameRef = reader.NullString(Encoding.UTF8);
        info.AuthorRef = reader.NullString(Encoding.UTF8);
        info.DescriptionRef = reader.NullString(Encoding.UTF8);
        info.PlayersRef = reader.NullString(Encoding.UTF8);

        for (int i = 0; i < 8; i++)
        {
            info.CameraBounds[i] = reader.F32();
        }
        for (int i = 0; i < 4; i++)
        {
            info.CameraBoundsComplements[i] = reader.I32();
        }

        info.PlayableWidth = reader.I32();
        info.PlayableHeight = reader.I32();
        info.Flags = reader.I32();
        info.Tileset = (char)reader.U8();
        info.CampaignBackground = reader.I32();

        if (info.FormatVersion > 24)
        {
            info.LoadingScreenModel = reader.NullString(Encoding.UTF8);
        }
        info.LoadingScreenText = reader.NullString(Encoding.UTF8);
        info.LoadingScreenTitle = reader.NullString(Encoding.UTF8);
        info.LoadingScreenSubtitle = reader.NullString(Encoding.UTF8);

        info.GameDataSet = reader.I32();
        if (info.FormatVersion > 24)
        {
            info.PrologueScreenModel = reader.NullString(Encoding.UTF8);
        }
        info.PrologueText = reader.NullString(Encoding.UTF8);
        info.PrologueTitle = reader.NullString(Encoding.UTF8);
        info.PrologueSubtitle = reader.NullString(Encoding.UTF8);

        if (info.FormatVersion > 24)
        {
            info.Environment = ReadEnvironment(reader);
        }

        if (info.FormatVersion >= 28)
        {
            reader.I32();
        }
        if (info.FormatVersion >= 31)
        {
            reader.I32();
            reader.I32();
        }
        if (info.FormatVersion >= 32)
        {
            reader.I32();
            reader.I32();
        }
        if (info.FormatVersion >= 33)
        {
            reader.I32();
        }

        ReadMinimap(reader, info);
        ReadPlayers(reader, info);
        ReadForces(reader, info);
        return info;
    }

    private static W3iEnvironment ReadEnvironment(W3xBinaryReader reader)
    {
        bool useFog = reader.I32() != 0;
        float fogStart = reader.F32();
        float fogEnd = reader.F32();
        float fogDensity = reader.F32();
        byte[] fogColor = reader.Bytes(4);
        string weather = reader.FourCc();
        string sound = reader.NullString(Encoding.UTF8);
        char light = (char)reader.U8();
        byte[] water = reader.Bytes(4);
        return new W3iEnvironment
        {
            UseTerrainFog = useFog,
            FogHeightStart = fogStart,
            FogHeightEnd = fogEnd,
            FogDensity = fogDensity,
            FogColorBgra = fogColor,
            GlobalWeather = weather,
            SoundEnvironment = sound,
            LightEnvTileset = light,
            WaterVertexColorBgra = water,
        };
    }

    private static void ReadMinimap(W3xBinaryReader reader, W3iInfo info)
    {
        if (reader.AtEnd)
        {
            return;
        }
        int probe = reader.Position;
        string path = reader.NullString(Encoding.UTF8);
        if (!string.IsNullOrEmpty(path)
            && path.EndsWith(".tga", StringComparison.OrdinalIgnoreCase)
            && reader.Remaining >= 8)
        {
            info.MinimapTgaPath = path;
            info.MinimapTgaWidth = reader.I32();
            info.MinimapTgaHeight = reader.I32();
            return;
        }
        reader.Position = probe;
    }

    private static void ReadPlayers(W3xBinaryReader reader, W3iInfo info)
    {
        if (reader.Remaining < 4)
        {
            return;
        }
        int count = reader.Count(32);
        for (int i = 0; i < count; i++)
        {
            int id = reader.I32();
            int controller = reader.I32();
            int race = reader.I32();
            int flags = reader.I32();
            string name = reader.NullString(Encoding.UTF8);
            float startX = reader.F32();
            float startY = reader.F32();
            int allyLow = reader.I32();
            int allyHigh = reader.I32();
            int enemyLow = 0;
            int enemyHigh = 0;
            if (info.FormatVersion >= 31)
            {
                enemyLow = reader.I32();
                enemyHigh = reader.I32();
            }
            info.Players.Add(new W3iPlayer(id, controller, race, flags, name, startX, startY, allyLow, allyHigh, enemyLow, enemyHigh));
        }
    }

    private static void ReadForces(W3xBinaryReader reader, W3iInfo info)
    {
        if (reader.Remaining < 4)
        {
            return;
        }
        int count = reader.Count(32);
        for (int i = 0; i < count; i++)
        {
            int flags = reader.I32();
            uint mask = reader.U32();
            string name = reader.NullString(Encoding.UTF8);
            info.Forces.Add(new W3iForce(flags, mask, name));
        }
    }

    public static int ExtractStringId(string reference)
    {
        const string prefix = "TRIGSTR_";
        if (reference != null && reference.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(reference[prefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
        {
            return id;
        }
        return -1;
    }

    public string ResolveName(WtsDocument strings)
    {
        return Resolve(NameRef, strings);
    }

    public static string Resolve(string reference, WtsDocument strings)
    {
        int id = ExtractStringId(reference);
        return id >= 0 ? strings?.Get(id) ?? reference : reference;
    }

    public void ResolveStrings(WtsDocument strings)
    {
        if (strings == null)
        {
            return;
        }
        LoadingScreenText = Resolve(LoadingScreenText, strings);
        LoadingScreenTitle = Resolve(LoadingScreenTitle, strings);
        LoadingScreenSubtitle = Resolve(LoadingScreenSubtitle, strings);
        PrologueText = Resolve(PrologueText, strings);
        PrologueTitle = Resolve(PrologueTitle, strings);
        PrologueSubtitle = Resolve(PrologueSubtitle, strings);
    }

    public static int NameStringId(byte[] w3i)
    {
        return ExtractStringId(Parse(w3i).NameRef);
    }

    public byte[] ToBytes()
    {
        throw new NotSupportedException("war3map.w3i 写入尚未实现");
    }
}
