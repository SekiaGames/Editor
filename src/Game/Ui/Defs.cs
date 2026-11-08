using Godot;

namespace WorldEdit;

public static class Defs
{
    public static readonly (string Name, Color Color)[] GroundTextures =
    {
        ("草地", Color.FromHtml("475c2c")),
        ("深草", Color.FromHtml("384a24")),
        ("泥土", Color.FromHtml("736244")),
        ("岩石", Color.FromHtml("615c54")),
        ("粗草", Color.FromHtml("545733")),
        ("枯草", Color.FromHtml("786d45")),
        ("藤蔓", Color.FromHtml("3d5238")),
        ("疫病", Color.FromHtml("4c472c")),
    };

    public static readonly (string Name, Color Color)[] CliffTypes =
    {
        ("泥土悬崖", Color.FromHtml("6b6157")),
        ("草地悬崖", Color.FromHtml("4e5a33")),
        ("岩石悬崖", Color.FromHtml("5a5a5c")),
        ("城市悬崖", Color.FromHtml("6a6350")),
    };

    public static readonly (string Cat, string[] Items)[] DoodadTree =
    {
        ("树木", new[] { "洛丹伦大树", "松树", "白桦", "枯树", "棕榈" }),
        ("灌木", new[] { "灌木", "草簇", "蕨类", "藤蔓丛" }),
        ("岩石", new[] { "小石块", "巨石", "岩石堆", "悬崖石" }),
        ("建筑", new[] { "房屋", "塔楼", "城墙", "拱门", "废墟" }),
        ("道具", new[] { "木桶", "木箱", "栅栏", "路灯", "路牌", "水井", "长椅", "桌子" }),
        ("装饰", new[] { "花", "蘑菇", "南瓜", "稻草人", "旗帜", "地毯" }),
        ("特效", new[] { "篝火", "火把", "熔岩裂缝", "烟雾" }),
        ("可破坏物", new[] { "可破坏树", "可破坏岩石", "可破坏门", "可破坏桥" }),
    };

    public static readonly (string Name, string Id)[] Units =
    {
        ("步兵", "hfoo"), ("弓箭手", "earc"), ("骑士", "hkni"), ("火枪手", "hrif"),
        ("法师", "hmpr"), ("牧师", "hmpr"), ("农民", "hpea"), ("苦工", "opeo"),
        ("食尸鬼", "ugho"), ("憎恶", "uabo"), ("蜘蛛", "uabc"), ("冰霜巨龙", "ufro"),
        ("女猎手", "esen"), ("山岭巨人", "emtg"), ("树妖", "edry"), ("奇美拉", "echm"),
    };

    public static readonly string[] TriggerTree =
    {
        "地图初始化", "对战初始化", "近战 AI", "英雄复活", "物品掉落", "自定义脚本",
    };
}
