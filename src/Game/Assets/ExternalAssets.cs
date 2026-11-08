using Godot;
using System;
using System.IO;

namespace WorldEdit.Assets;

public static class ExternalAssets
{
    public const string PackName = "extract.pck";

    public static void Mount()
    {
        if (DirAccess.DirExistsAbsolute("res://assets/converted"))
        {
            return;
        }

        string directory = Path.GetDirectoryName(OS.GetExecutablePath());
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return;
        }

        string pack = Path.Combine(directory, PackName);
        if (!File.Exists(pack))
        {
            GD.PushError($"[ASSETS] 资源没有内嵌，也找不到 {PackName}：{directory}。先用 export.bat 打包。");
            return;
        }

        bool ok = ProjectSettings.LoadResourcePack(pack, false);
        GD.Print($"[ASSETS] 挂载 {PackName} -> {ok}");
    }
}
