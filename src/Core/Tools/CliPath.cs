using Godot;
using System;

namespace WorldEdit.Core.Tools;

internal static class CliPath
{
    public static string Resolve(string path)
    {
        if (System.IO.Path.IsPathRooted(path))
        {
            return path;
        }

        string direct = System.IO.Path.GetFullPath(path);
        if (System.IO.File.Exists(direct) || System.IO.Directory.Exists(direct))
        {
            return direct;
        }

        string project = ProjectSettings.GlobalizePath("res://");
        string inProject = System.IO.Path.GetFullPath(System.IO.Path.Combine(project, path));
        if (System.IO.File.Exists(inProject) || System.IO.Directory.Exists(inProject))
        {
            return inProject;
        }

        string inRepository = System.IO.Path.GetFullPath(System.IO.Path.Combine(project, "..", path));
        if (System.IO.File.Exists(inRepository) || System.IO.Directory.Exists(inRepository))
        {
            return inRepository;
        }
        return direct;
    }

    public static string ResolveOutput(string path)
    {
        if (System.IO.Path.IsPathRooted(path))
        {
            return path;
        }
        string project = ProjectSettings.GlobalizePath("res://");
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(project, "..", path));
    }

    public static void EnsureDirectory(string filePath)
    {
        DirAccess.MakeDirRecursiveAbsolute(System.IO.Path.GetDirectoryName(filePath));
    }
}
