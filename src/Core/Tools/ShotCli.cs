using Godot;
using System;
using System.Collections.Generic;

namespace WorldEdit.Core.Tools;

public static class ShotCli
{
    private static Image LoadRgb(string path)
    {
        string full = CliPath.Resolve(path);
        if (!System.IO.File.Exists(full))
        {
            throw new System.IO.FileNotFoundException($"图片不存在: {path}（试过 {full}）");
        }

        var image = new Image();
        Error error = image.LoadPngFromBuffer(System.IO.File.ReadAllBytes(full));
        if (error != Error.Ok || image.IsEmpty())
        {
            throw new InvalidOperationException($"不是有效的 PNG: {path}（{error}）");
        }
        image.Convert(Image.Format.Rgb8);
        return image;
    }

    private static int ChannelCount(Image image)
    {
        return image.GetData().Length / (image.GetWidth() * image.GetHeight());
    }

    private static bool[] WaterMask(Image image)
    {
        int w = image.GetWidth();
        int h = image.GetHeight();
        byte[] px = image.GetData();
        int channels = ChannelCount(image);
        var mask = new bool[w * h];
        for (int i = 0; i < w * h; i++)
        {
            int offset = i * channels;
            byte r = px[offset];
            byte g = px[offset + 1];
            byte b = px[offset + 2];
            mask[i] = b > g + 25 && b > r + 10;
        }
        return mask;
    }

    public static void Diff(string refPath, string minePath, int block = 64)
    {
        Image a = LoadRgb(refPath);
        Image b = LoadRgb(minePath);
        int aw = a.GetWidth(), ah = a.GetHeight();
        int bw = b.GetWidth(), bh = b.GetHeight();
        Console.WriteLine($"参考 {aw}x{ah}  我方 {bw}x{bh}");

        int w = Math.Min(aw, bw);
        int h = Math.Min(ah, bh);
        byte[] pa = a.GetData();
        byte[] pb = b.GetData();
        int ca = ChannelCount(a);
        int cb = ChannelCount(b);

        long total = 0;
        long exact = 0;
        var perBlock = new List<(double Mean, int X, int Y, double Same)>();

        for (int by = 0; by < h; by += block)
        {
            int yEnd = Math.Min(by + block, h);
            for (int bx = 0; bx < w; bx += block)
            {
                int xEnd = Math.Min(bx + block, w);
                long diffSum = 0;
                int count = 0;
                int exactBlock = 0;

                for (int y = by; y < yEnd; y += 4)
                {
                    for (int x = bx; x < xEnd; x += 4)
                    {
                        int ia = (y * w + x) * ca;
                        int ib = (y * w + x) * cb;
                        int d = Math.Abs(pa[ia] - pb[ib])
                            + Math.Abs(pa[ia + 1] - pb[ib + 1])
                            + Math.Abs(pa[ia + 2] - pb[ib + 2]);
                        diffSum += d;
                        count++;
                        total++;
                        if (d == 0)
                        {
                            exactBlock++;
                            exact++;
                        }
                    }
                }

                if (count > 0)
                {
                    perBlock.Add(((double)diffSum / count, bx, by, (double)exactBlock / count));
                }
            }
        }

        Console.WriteLine($"完全相同的采样点 {exact}/{total} = {(total > 0 ? exact * 100.0 / total : 0):0.0}%");

        perBlock.Sort((p, q) => q.Mean.CompareTo(p.Mean));
        Console.WriteLine("\n差异最大的 12 块（块坐标 / 平均 |ΔRGB| / 相同比例）:");
        for (int i = 0; i < Math.Min(12, perBlock.Count); i++)
        {
            var item = perBlock[i];
            Console.WriteLine($"  ({item.X,4},{item.Y,4})  Δ={item.Mean,6:0.0}  相同={item.Same * 100,5:0.0}%");
        }

        Console.WriteLine("\n差异最小的 6 块:");
        for (int i = Math.Max(0, perBlock.Count - 6); i < perBlock.Count; i++)
        {
            var item = perBlock[i];
            Console.WriteLine($"  ({item.X,4},{item.Y,4})  Δ={item.Mean,6:0.0}  相同={item.Same * 100,5:0.0}%");
        }

        Console.WriteLine("\n几个区域的平均色 (参考 / 我方):");
        (string Name, int X0, int Y0, int X1, int Y1)[] regions =
        {
            ("海面(左上)", 100, 200, 300, 320),
            ("草地(右下)", 700, 700, 900, 800),
            ("中央陆地", 600, 300, 800, 400),
            ("上方岛屿", 1350, 60, 1500, 150),
            ("整幅", 0, 0, w, h),
        };
        foreach (var region in regions)
        {
            Console.WriteLine($"  {region.Name,-10} {AverageColor(pa, ca, w, h, region.X0, region.Y0, region.X1, region.Y1)}"
                + $"  /  {AverageColor(pb, cb, w, h, region.X0, region.Y0, region.X1, region.Y1)}");
        }
    }

    private static string AverageColor(byte[] px, int channels, int w, int h, int x0, int y0, int x1, int y1)
    {
        long r = 0, g = 0, b = 0;
        int n = 0;
        int yEnd = Math.Min(y1, h);
        int xEnd = Math.Min(x1, w);
        for (int y = y0; y < yEnd; y += 4)
        {
            for (int x = x0; x < xEnd; x += 4)
            {
                int i = (y * w + x) * channels;
                r += px[i];
                g += px[i + 1];
                b += px[i + 2];
                n++;
            }
        }
        if (n == 0)
        {
            return "[0, 0, 0]";
        }
        return $"[{Math.Round((double)r / n)}, {Math.Round((double)g / n)}, {Math.Round((double)b / n)}]";
    }

    public static void Mirror(string refPath, string minePath)
    {
        Image a = LoadRgb(refPath);
        Image b = LoadRgb(minePath);
        int aw = a.GetWidth(), ah = a.GetHeight();
        int bw = b.GetWidth(), bh = b.GetHeight();
        Console.WriteLine($"参考 {aw}x{ah}  我方 {bw}x{bh}");

        bool[] refMask = WaterMask(a);
        bool[] mineMask = WaterMask(b);

        int w = Math.Min(aw, bw);
        int h = Math.Min(ah, bh);
        double baseline = IoU(refMask, aw, mineMask, bw, w, h);

        bool[] flipped = FlipHorizontal(mineMask, bw, bh);
        double mirrored = IoU(refMask, aw, flipped, bw, w, h);

        Console.WriteLine($"原样重合度        {baseline * 100,5:0.0}%");
        Console.WriteLine($"我方水平翻转后    {mirrored * 100,5:0.0}%");
        if (mirrored > baseline + 0.05)
        {
            Console.WriteLine("=> 存在左右镜像，翻转后明显更吻合");
        }
        else if (baseline > mirrored + 0.05)
        {
            Console.WriteLine("=> 没有镜像，翻转后更差");
        }
        else
        {
            Console.WriteLine("=> 两者接近，无法从水域形状判断");
        }
    }

    private static bool[] FlipHorizontal(bool[] mask, int w, int h)
    {
        var flipped = new bool[w * h];
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                flipped[row + x] = mask[row + (w - 1 - x)];
            }
        }
        return flipped;
    }

    private static double IoU(bool[] a, int aw, bool[] b, int bw, int w, int h)
    {
        long inter = 0;
        long union = 0;
        for (int y = 0; y < h; y += 2)
        {
            int rowA = y * aw;
            int rowB = y * bw;
            for (int x = 0; x < w; x += 2)
            {
                bool p = a[rowA + x];
                bool q = b[rowB + x];
                if (p && q)
                {
                    inter++;
                }
                if (p || q)
                {
                    union++;
                }
            }
        }
        return union == 0 ? 0.0 : (double)inter / union;
    }

    private static double IoUWithShift(bool[] a, int aw, int ah, bool[] b, int bw, int bh, int dx, int dy)
    {
        long inter = 0;
        long union = 0;
        for (int y = 0; y < ah; y += 2)
        {
            int sy = y + dy;
            if (sy < 0 || sy >= bh)
            {
                continue;
            }
            int rowA = y * aw;
            int rowB = sy * bw;
            for (int x = 0; x < aw; x += 2)
            {
                int sx = x + dx;
                if (sx < 0 || sx >= bw)
                {
                    continue;
                }
                bool p = a[rowA + x];
                bool q = b[rowB + sx];
                if (p && q)
                {
                    inter++;
                }
                if (p || q)
                {
                    union++;
                }
            }
        }
        return union == 0 ? 0.0 : (double)inter / union;
    }

    public static void Water(string refPath, string minePath)
    {
        Image a = LoadRgb(refPath);
        Image b = LoadRgb(minePath);
        int aw = a.GetWidth(), ah = a.GetHeight();
        int bw = b.GetWidth(), bh = b.GetHeight();
        bool[] refMask = WaterMask(a);
        bool[] mineMask = WaterMask(b);

        Console.WriteLine($"参考 {aw}x{ah} 水域占比 {Ratio(refMask) * 100:0.0}%");
        Console.WriteLine($"我方 {bw}x{bh} 水域占比 {Ratio(mineMask) * 100:0.0}%");
        Console.WriteLine($"原位置 IoU = {IoUWithShift(refMask, aw, ah, mineMask, bw, bh, 0, 0) * 100:0.0}%");

        double bestValue = 0.0;
        int bestDx = 0, bestDy = 0;
        for (int dy = -24; dy <= 24; dy += 4)
        {
            for (int dx = -24; dx <= 24; dx += 4)
            {
                double value = IoUWithShift(refMask, aw, ah, mineMask, bw, bh, dx, dy);
                if (value > bestValue)
                {
                    bestValue = value;
                    bestDx = dx;
                    bestDy = dy;
                }
            }
        }
        Console.WriteLine($"最佳平移 dx={bestDx} dy={bestDy} 时 IoU = {bestValue * 100:0.0}%");

        for (int dy = bestDy - 4; dy <= bestDy + 4; dy++)
        {
            for (int dx = bestDx - 4; dx <= bestDx + 4; dx++)
            {
                double value = IoUWithShift(refMask, aw, ah, mineMask, bw, bh, dx, dy);
                if (value > bestValue)
                {
                    bestValue = value;
                    bestDx = dx;
                    bestDy = dy;
                }
            }
        }
        Console.WriteLine($"细化后 dx={bestDx} dy={bestDy} IoU = {bestValue * 100:0.0}%");
    }

    private static double Ratio(bool[] mask)
    {
        int count = 0;
        foreach (bool value in mask)
        {
            if (value)
            {
                count++;
            }
        }
        return mask.Length == 0 ? 0.0 : (double)count / mask.Length;
    }

    public static void Crop(string input, string output, int x, int y, int w, int h, int scale = 3)
    {
        Image source = LoadRgb(input);
        var wanted = new Rect2I(x, y, w, h);
        var available = new Rect2I(0, 0, source.GetWidth(), source.GetHeight());
        Rect2I rect = wanted.Intersection(available);
        if (rect.Size.X <= 0 || rect.Size.Y <= 0)
        {
            throw new ArgumentException($"裁剪区域 {wanted} 完全落在 {source.GetWidth()}x{source.GetHeight()} 之外");
        }

        Image cropped = Image.CreateEmpty(rect.Size.X, rect.Size.Y, false, Image.Format.Rgb8);
        cropped.BlitRect(source, rect, Vector2I.Zero);
        if (scale > 1)
        {
            cropped.Resize(rect.Size.X * scale, rect.Size.Y * scale, Image.Interpolation.Nearest);
        }

        string full = CliPath.ResolveOutput(output);
        CliPath.EnsureDirectory(full);
        Error error = cropped.SavePng(full);
        if (error != Error.Ok)
        {
            throw new System.IO.IOException($"保存失败 {output}: {error}");
        }
        Console.WriteLine($"{output} {cropped.GetWidth()}x{cropped.GetHeight()}"
            + $" (源 {rect.Position.X},{rect.Position.Y} {rect.Size.X}x{rect.Size.Y} x{scale})");
    }
}
