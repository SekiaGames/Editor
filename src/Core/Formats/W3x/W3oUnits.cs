using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WorldEdit.Core.Formats.W3x;

public sealed record UnitInventoryItem(int Slot, string ItemId);

public sealed record UnitAbilityModification(string AbilityId, bool ActiveForAutocast, int Level);

public sealed record UnitItemDrop(string ItemId, int Chance);

public sealed class UnitItemDropSet
{
    public List<UnitItemDrop> Items { get; } = new();
}

public sealed class UnitPlacement
{
    public string Id = "";
    public uint Variation;

    public float X;
    public float Y;
    public float Z;

    public float Angle;

    public float ScaleX = 1f;
    public float ScaleY = 1f;
    public float ScaleZ = 1f;

    public string SkinId = "";

    public byte Flags;

    public uint Player;

    public byte Unknown1;
    public byte Unknown2;

    public int Hitpoints;
    public int Mana;

    public int DroppedItemTable = -1;
    public readonly List<UnitItemDropSet> DroppedItemSets = new();

    public int Gold;

    public float TargetAcquisition;

    public int HeroLevel;
    public int HeroStrength;
    public int HeroAgility;
    public int HeroIntelligence;

    public readonly List<UnitInventoryItem> Inventory = new();
    public readonly List<UnitAbilityModification> Abilities = new();

    public int RandomMode = -1;
    public int RandomLevel;
    public int RandomClass;
    public uint RandomTable;
    public uint RandomColumn;
    public readonly List<UnitItemDrop> RandomChoices = new();

    public int CustomTeamColor = -1;
    public int Waygate;
    public int CreationNumber = -1;

    public bool IsItem;

    public bool IsStartingLocation => !IsItem && string.Equals(Id, "sloc", StringComparison.OrdinalIgnoreCase);

    public bool IsScriptRandomUnit => Id is "uDNR" or "bDNR";
}

public sealed class W3oUnits
{
    public int Version { get; private set; }
    public int SubVersion { get; private set; }
    public int GameVersion { get; private set; }
    public bool HasSkinId => GameVersion >= 132;
    public byte[] RawBytes { get; private set; }

    public readonly List<UnitPlacement> Entries = new();

    public IEnumerable<UnitPlacement> Units
    {
        get
        {
            foreach (UnitPlacement entry in Entries)
            {
                if (!entry.IsItem)
                {
                    yield return entry;
                }
            }
        }
    }

    public IEnumerable<UnitPlacement> Items
    {
        get
        {
            foreach (UnitPlacement entry in Entries)
            {
                if (entry.IsItem)
                {
                    yield return entry;
                }
            }
        }
    }

    public static W3oUnits Parse(byte[] data, Func<string, bool> classify)
    {
        return Parse(data, classify, 0);
    }

    public static W3oUnits Parse(byte[] data, Func<string, bool> classify, int gameVersion)
    {
        if (data == null || data.Length < 16 || Encoding.ASCII.GetString(data, 0, 4) != "W3do")
        {
            throw new InvalidDataException("不是 war3mapUnits.doo");
        }

        var reader = new W3xBinaryReader(data, 4);
        var result = new W3oUnits
        {
            RawBytes = data,
            Version = reader.I32(),
            SubVersion = reader.I32(),
            GameVersion = gameVersion,
        };
        int count = reader.Count(100000);

        for (int i = 0; i < count; i++)
        {
            var entry = new UnitPlacement
            {
                Id = reader.FourCc(),
                Variation = reader.U32(),
                X = reader.F32(),
                Y = reader.F32(),
                Z = reader.F32(),
                Angle = reader.F32(),
                ScaleX = reader.F32(),
                ScaleY = reader.F32(),
                ScaleZ = reader.F32(),
                SkinId = result.HasSkinId ? reader.FourCc() : "",
                Flags = reader.U8(),
                Player = reader.U32(),
                Unknown1 = reader.U8(),
                Unknown2 = reader.U8(),
                Hitpoints = reader.I32(),
                Mana = reader.I32(),
            };

            if (result.Version > 7)
            {
                entry.DroppedItemTable = reader.I32();
            }

            int setCount = reader.Count(256);
            for (int set = 0; set < setCount; set++)
            {
                var dropSet = new UnitItemDropSet();
                int dropCount = reader.Count(256);
                for (int drop = 0; drop < dropCount; drop++)
                {
                    dropSet.Items.Add(new UnitItemDrop(reader.FourCc(), reader.I32()));
                }
                entry.DroppedItemSets.Add(dropSet);
            }

            entry.Gold = reader.I32();
            entry.TargetAcquisition = reader.F32();
            entry.HeroLevel = reader.I32();

            if (result.Version > 7)
            {
                entry.HeroStrength = reader.I32();
                entry.HeroAgility = reader.I32();
                entry.HeroIntelligence = reader.I32();
            }

            int inventory = reader.Count(64);
            for (int slot = 0; slot < inventory; slot++)
            {
                int index = reader.I32();
                string itemId = reader.FourCc();
                entry.Inventory.Add(new UnitInventoryItem(index, itemId));
            }

            int abilities = reader.Count(256);
            for (int ability = 0; ability < abilities; ability++)
            {
                string abilityId = reader.FourCc();
                bool autocast = reader.I32() != 0;
                int level = reader.I32();
                entry.Abilities.Add(new UnitAbilityModification(abilityId, autocast, level));
            }

            entry.RandomMode = reader.I32();
            switch (entry.RandomMode)
            {
                case 0:
                    entry.RandomLevel = reader.U8() | (reader.U8() << 8) | (reader.U8() << 16);
                    entry.RandomClass = reader.U8();
                    break;
                case 1:
                    entry.RandomTable = reader.U32();
                    entry.RandomColumn = reader.U32();
                    break;
                case 2:
                    int choices = reader.Count(256);
                    for (int choice = 0; choice < choices; choice++)
                    {
                        entry.RandomChoices.Add(new UnitItemDrop(reader.FourCc(), reader.I32()));
                    }
                    break;
            }

            entry.CustomTeamColor = reader.I32();
            entry.Waygate = reader.I32();
            entry.CreationNumber = reader.I32();

            entry.IsItem = classify != null && classify(entry.Id);
            result.Entries.Add(entry);
        }

        return result;
    }

    public byte[] ToBytes()
    {
        throw new NotSupportedException("war3mapUnits.doo 写入尚未实现");
    }
}
