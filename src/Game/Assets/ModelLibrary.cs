using Godot;
using System;
using System.Collections.Generic;

namespace WorldEdit.Assets;

public static class ModelLibrary
{
    public const string Root = War3Assets.ConvertedRoot;

    public static string Find(string name)
    {
        string key = Normalize(name);
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        foreach (string extension in new[] { ".tscn", ".glb" })
        {
            string guessed = CapitalizePath(key, extension);
            if (ResourceLoader.Exists(guessed) || FileAccess.FileExists(guessed))
            {
                return ResolvePath(key, extension) ?? guessed;
            }
        }

        foreach (string extension in new[] { ".tscn", ".glb" })
        {
            string direct = Root + key + extension;
            if (ResourceLoader.Exists(direct) || FileAccess.FileExists(direct))
            {
                return ResolvePath(key, extension) ?? direct;
            }
        }

        foreach (string extension in new[] { ".tscn", ".glb" })
        {
            string resolved = ResolvePath(key, extension);
            if (resolved != null)
            {
                return resolved;
            }
        }
        return null;
    }

    public static bool Exists(string name)
    {
        return Find(name) != null;
    }

    public static string FindImage(string name)
    {
        string key = Normalize(name);
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        string direct = Root + key + ".png";
        if (ResourceLoader.Exists(direct) || FileAccess.FileExists(direct))
        {
            return direct;
        }

        string guessed = CapitalizePath(key, ".png");
        if (ResourceLoader.Exists(guessed) || FileAccess.FileExists(guessed))
        {
            return guessed;
        }

        return ResolvePath(key, ".png");
    }

    public static int ApplyMissingMaterials(Node root, string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath))
        {
            return 0;
        }

        string materialsDirectory = scenePath.GetBaseDir() + "/materials";
        if (!DirAccess.DirExistsAbsolute(materialsDirectory))
        {
            return 0;
        }

        var files = new List<string>();
        using (DirAccess access = DirAccess.Open(materialsDirectory))
        {
            if (access == null)
            {
                return 0;
            }
            access.ListDirBegin();
            string entry = access.GetNext();
            while (!string.IsNullOrEmpty(entry))
            {
                if (!access.CurrentIsDir() && entry.EndsWith(".tres", StringComparison.OrdinalIgnoreCase))
                {
                    files.Add(materialsDirectory + "/" + entry);
                }
                entry = access.GetNext();
            }
            access.ListDirEnd();
        }
        if (files.Count == 0)
        {
            return 0;
        }
        files.Sort(StringComparer.Ordinal);

        int index = 0;
        int applied = 0;
        foreach (Node node in root.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (node is not MeshInstance3D mesh || mesh.Mesh == null)
            {
                continue;
            }
            if (mesh.GetSurfaceOverrideMaterial(0) == null && index < files.Count)
            {
                string candidate = files[index];
                try
                {
                    if (ResourceLoader.Exists(candidate) && ResourceLoader.Load(candidate) is Material material)
                    {
                        mesh.SetSurfaceOverrideMaterial(0, material);
                        applied++;
                    }
                }
                catch (Exception error)
                {
                    GD.PushWarning($"[UNIT] 材质兜底失败 {candidate}: {error.Message}");
                }
            }
            index++;
        }
        return applied;
    }

    private static string Normalize(string name)
    {
        string key = name.Replace('\\', '/').TrimStart('/');
        if (key.EndsWith(".mdx", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith(".blp", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            key = key[..^4];
        }
        return key;
    }

    private static string CapitalizePath(string key, string extension)
    {
        string[] parts = key.Split('/');
        var builder = new System.Text.StringBuilder(Root.TrimEnd('/'));
        for (int i = 0; i < parts.Length; i++)
        {
            string segment = parts[i];
            if (segment.Length > 0 && char.IsLower(segment[0]))
            {
                segment = char.ToUpperInvariant(segment[0]) + segment[1..];
            }
            builder.Append('/');
            builder.Append(i == parts.Length - 1 ? segment + extension : segment);
        }
        return builder.ToString();
    }

    private static string ResolvePath(string key, string extension)
    {
        string[] parts = key.Split('/');
        string current = Root.TrimEnd('/');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            string directory = MatchEntry(current, parts[i], false);
            if (directory == null)
            {
                return null;
            }
            current += "/" + directory;
        }

        string file = MatchEntry(current, parts[^1] + extension, true);
        return file == null ? null : current + "/" + file;
    }

    private static string MatchEntry(string directory, string want, bool isFile)
    {
        string exact = directory + "/" + want;
        bool hit = isFile ? FileAccess.FileExists(exact) : DirAccess.DirExistsAbsolute(exact);
        if (hit)
        {
            return want;
        }

        using DirAccess access = DirAccess.Open(directory);
        if (access == null)
        {
            return null;
        }

        access.ListDirBegin();
        string name = access.GetNext();
        while (!string.IsNullOrEmpty(name))
        {

            string real = name.EndsWith(".remap", StringComparison.OrdinalIgnoreCase) ? name[..^6] : name;
            if (real != "." && real != ".." && string.Equals(real, want, StringComparison.OrdinalIgnoreCase))
            {
                access.ListDirEnd();
                return real;
            }
            name = access.GetNext();
        }
        access.ListDirEnd();
        return null;
    }
}
