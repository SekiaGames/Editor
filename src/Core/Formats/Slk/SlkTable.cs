using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WorldEdit.Core.Formats.Slk;

public sealed class SlkTable
{
    private readonly Dictionary<string, int> _columns = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, Dictionary<int, string>> _rows = new();

    public IReadOnlyDictionary<string, int> Columns => _columns;

    public static SlkTable Parse(byte[] data)
    {
        var table = new SlkTable();
        string text = Encoding.Latin1.GetString(data);
        int row = 0;
        int column = 0;

        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.Length < 2 || line[0] != 'C' || line[1] != ';')
            {
                continue;
            }

            foreach (string field in SplitFields(line))
            {
                if (field.Length == 0)
                {
                    continue;
                }

                char kind = field[0];
                string body = field[1..];
                switch (kind)
                {
                    case 'X' when int.TryParse(body, out int x):
                        column = x;
                        break;
                    case 'Y' when int.TryParse(body, out int y):
                        row = y;
                        break;
                    case 'K':
                        table.Set(row, column, Unquote(body));
                        break;
                }
            }
        }

        table.ReadHeader();
        return table;
    }

    public static SlkTable Load(string path)
    {
        return Parse(File.ReadAllBytes(path));
    }

    public static SlkTable FromRows(string[] columns, string[][] rows)
    {
        var table = new SlkTable();
        for (int column = 0; column < columns.Length; column++)
        {
            table._columns[columns[column]] = column + 1;
        }
        for (int row = 0; row < rows.Length; row++)
        {
            string[] cells = rows[row];
            for (int column = 0; column < cells.Length; column++)
            {
                if (!string.IsNullOrEmpty(cells[column]))
                {
                    table.Set(row + 2, column + 1, cells[column]);
                }
            }
        }
        return table;
    }

    private static IEnumerable<string> SplitFields(string line)
    {
        int start = 2;
        bool quoted = false;
        for (int i = 2; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                quoted = !quoted;
            }
            else if (c == ';' && !quoted)
            {
                yield return line[start..i];
                start = i + 1;
            }
        }
        if (start < line.Length)
        {
            yield return line[start..];
        }
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1].Replace("\"\"", "\"");
        }
        return value;
    }

    private void Set(int row, int column, string value)
    {
        if (!_rows.TryGetValue(row, out Dictionary<int, string> cells))
        {
            cells = new Dictionary<int, string>();
            _rows[row] = cells;
        }
        cells[column] = value;
    }

    private void ReadHeader()
    {
        if (!_rows.TryGetValue(1, out Dictionary<int, string> header))
        {
            return;
        }
        foreach (var pair in header)
        {
            _columns[pair.Value] = pair.Key;
        }
    }

    public IEnumerable<int> DataRows
    {
        get
        {
            foreach (int row in _rows.Keys)
            {
                if (row != 1)
                {
                    yield return row;
                }
            }
        }
    }

    public bool HasColumn(string name)
    {
        return _columns.ContainsKey(name);
    }

    public string Get(int row, string column)
    {
        if (!_columns.TryGetValue(column, out int index))
        {
            return null;
        }
        return _rows.TryGetValue(row, out Dictionary<int, string> cells) && cells.TryGetValue(index, out string value)
            ? value
            : null;
    }

    public int GetInt(int row, string column, int fallback = 0)
    {
        return int.TryParse(Get(row, column), out int value) ? value : fallback;
    }

    public float GetFloat(int row, string column, float fallback = 0f)
    {
        return float.TryParse(Get(row, column), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value)
            ? value
            : fallback;
    }

    public int FindRow(string column, string value)
    {
        if (!_columns.TryGetValue(column, out int index))
        {
            return -1;
        }
        foreach (var pair in _rows)
        {
            if (pair.Value.TryGetValue(index, out string cell) && string.Equals(cell, value, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Key;
            }
        }
        return -1;
    }
}
