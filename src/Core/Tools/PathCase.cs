namespace WorldEdit.Core.Tools;

public static class PathCase
{
    public static string Normalize(string relative)
    {
        if (string.IsNullOrEmpty(relative))
        {
            return relative;
        }

        string[] parts = relative.Replace('/', '\\').Split('\\');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            string part = parts[i];
            if (part.Length == 0 || part != part.ToLowerInvariant())
            {
                continue;
            }
            parts[i] = char.ToUpperInvariant(part[0]) + part.Substring(1);
        }
        return string.Join('\\', parts);
    }
}
