using Godot;
using System;
using System.Collections.Generic;

namespace WorldEdit.Assets;

public sealed class CliffGeometry
{
    public Vector3[] Vertices = Array.Empty<Vector3>();
    public Vector2[] Uvs = Array.Empty<Vector2>();
    public int[] Indices = Array.Empty<int>();
}

public static class CliffMeshFactory
{
    private const string CliffDirectory = "Doodads/Terrain/Cliffs/";
    private const string TransDirectory = "Doodads/Terrain/CliffTrans/";

    private static readonly Dictionary<string, CliffGeometry> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> Resolved = new(StringComparer.OrdinalIgnoreCase);

    public static string Find(string fileName)
    {
        foreach (string directory in new[] { CliffDirectory, TransDirectory })
        {
            string relative = $"{directory}{fileName}";
            if (ModelLibrary.Exists(relative))
            {
                return relative;
            }
        }
        return null;
    }

    public static string ResolveCliff(string variant, int variation)
    {
        string key = "Cliffs" + variant + "|" + variation;
        if (Resolved.TryGetValue(key, out string cached))
        {
            return cached;
        }

        string result = null;
        for (int v = Math.Clamp(variation, 0, 7); v >= 0; v--)
        {
            result = Find("Cliffs" + variant + v);
            if (result != null)
            {
                break;
            }
        }
        Resolved[key] = result;
        return result;
    }

    public static CliffGeometry Get(string relativePath)
    {
        if (Cache.TryGetValue(relativePath, out CliffGeometry cached))
        {
            return cached;
        }

        var geometry = new CliffGeometry();
        string path = ModelLibrary.Find(relativePath);
        if (path == null)
        {
            GD.PrintErr($"[CLIFF] 找不到模型 {relativePath}");
            Cache[relativePath] = geometry;
            return geometry;
        }

        var packed = GD.Load<PackedScene>(path);
        if (packed == null)
        {
            GD.PrintErr($"[CLIFF] 加载失败 {path}");
            Cache[relativePath] = geometry;
            return geometry;
        }

        Node root = packed.Instantiate();
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var indices = new List<int>();
        CollectMeshes(root, vertices, uvs, indices);
        root.QueueFree();

        if (vertices.Count > 0)
        {
            Vector3 min = vertices[0], max = vertices[0];
            foreach (Vector3 v in vertices) { min = min.Min(v); max = max.Max(v); }
            GD.Print($"[CLIFF] {relativePath} verts={vertices.Count} tris={indices.Count / 3} bbox={min}~{max}");
        }
        else
        {
            GD.Print($"[CLIFF] {relativePath} 没有几何");
        }
        geometry.Vertices = vertices.ToArray();
        geometry.Uvs = uvs.ToArray();
        geometry.Indices = indices.ToArray();
        Cache[relativePath] = geometry;
        return geometry;
    }

    private static void CollectMeshes(Node node, List<Vector3> vertices, List<Vector2> uvs, List<int> indices)
    {
        if (node is MeshInstance3D instance && instance.Mesh != null)
        {
            Mesh mesh = instance.Mesh;
            for (int surface = 0; surface < mesh.GetSurfaceCount(); surface++)
            {
                Godot.Collections.Array arrays = mesh.SurfaceGetArrays(surface);
                var positions = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var texture = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
                var triangles = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();

                int offset = vertices.Count;
                for (int i = 0; i < positions.Length; i++)
                {
                    vertices.Add(new Vector3(positions[i].X, positions[i].Y, -positions[i].Z));
                    uvs.Add(i < texture.Length ? texture[i] : Vector2.Zero);
                }
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    indices.Add(offset + triangles[i]);
                    indices.Add(offset + triangles[i + 1]);
                    indices.Add(offset + triangles[i + 2]);
                }
            }
        }

        foreach (Node child in node.GetChildren())
        {
            CollectMeshes(child, vertices, uvs, indices);
        }
    }
}
