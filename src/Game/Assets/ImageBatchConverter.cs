using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WorldEdit.Assets;

public static class ImageBatchConverter
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        ".blp", ".jpg", ".jpeg",
    };

    public static int Run(string sourceRoot, string targetRoot)
    {
        if (!Directory.Exists(sourceRoot))
        {
            GD.PrintErr($"[IMG] 源目录不存在: {sourceRoot}");
            return 0;
        }

        string source = Path.GetFullPath(sourceRoot);
        string target = Path.GetFullPath(targetRoot);
        Directory.CreateDirectory(target);

        var files = new List<string>();
        foreach (string path in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            if (Supported.Contains(Path.GetExtension(path)))
            {
                files.Add(path);
            }
        }
        files.Sort(StringComparer.OrdinalIgnoreCase);
        GD.Print($"[IMG] 待转换 {files.Count} 个文件：{source} → {target}");

        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var failures = new List<string>();
        long sourceBytes = 0;
        long targetBytes = 0;
        int done = 0;

        foreach (string path in files)
        {
            string relative = WorldEdit.Core.Tools.PathCase.Normalize(Path.GetRelativePath(source, path));
            string outputRelative = Path.ChangeExtension(relative, ".png");
            if (!taken.Add(outputRelative))
            {
                outputRelative = relative + ".png";
                taken.Add(outputRelative);
            }
            string output = Path.Combine(target, outputRelative);
            try
            {
                byte[] data = File.ReadAllBytes(path);
                sourceBytes += data.Length;

                Image image = Decode(path, data);
                if (image == null)
                {
                    failures.Add($"{relative}\t不支持的图片格式");
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                byte[] png = image.SavePngToBuffer();
                File.WriteAllBytes(output, png);
                targetBytes += png.Length;
                image.Dispose();
            }
            catch (Exception exception)
            {
                failures.Add($"{relative}\t{exception.GetType().Name}: {exception.Message}");
            }

            done++;
            if (done % 200 == 0)
            {
                GD.Print($"[IMG] {done}/{files.Count}，已写出 {targetBytes / 1048576.0:0.0} MB，失败 {failures.Count}");
            }
        }

        var report = new StringBuilder();
        report.AppendLine($"源目录\t{source}");
        report.AppendLine($"输出目录\t{target}");
        report.AppendLine($"文件数\t{files.Count}");
        report.AppendLine($"成功\t{files.Count - failures.Count}");
        report.AppendLine($"失败\t{failures.Count}");
        report.AppendLine($"源体积\t{sourceBytes / 1048576.0:0.0} MB");
        report.AppendLine($"PNG 体积\t{targetBytes / 1048576.0:0.0} MB");
        report.AppendLine();
        foreach (string failure in failures)
        {
            report.AppendLine(failure);
        }
        File.WriteAllText(Path.Combine(target, "_convert-report.txt"), report.ToString(), new UTF8Encoding(false));

        GD.Print($"[IMG] 完成：成功 {files.Count - failures.Count}，失败 {failures.Count}，"
            + $"PNG {targetBytes / 1048576.0:0.0} MB（源 {sourceBytes / 1048576.0:0.0} MB）");
        return files.Count - failures.Count;
    }

    private static Image Decode(string path, byte[] data)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".blp":
                return BlpDecoder.Decode(data);
            case ".jpg":
            case ".jpeg":
                var jpeg = new Image();
                jpeg.LoadJpgFromBuffer(data);
                return jpeg;
            default:
                return null;
        }
    }
}
