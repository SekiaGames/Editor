using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WorldEdit.Core.Formats.W3x;

public sealed class WtsDocument
{
    private sealed record Entry(int Id, int ValueStart, int ValueEnd);

    private readonly List<Entry> _entries = new();
    private string _text;

    public static WtsDocument Parse(byte[] data)
    {
        string text = Encoding.UTF8.GetString(data);
        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            text = text[1..];
        }

        var document = new WtsDocument { _text = text };
        var matches = Regex.Matches(text, @"(?m)^STRING\s+(\d+)\s*$");
        for (int i = 0; i < matches.Count; i++)
        {
            int braceOpen = text.IndexOf('{', matches[i].Index);
            if (braceOpen < 0)
            {
                continue;
            }
            int valueStart = braceOpen + 1;
            if (valueStart < text.Length && text[valueStart] == '\r')
            {
                valueStart++;
            }
            if (valueStart < text.Length && text[valueStart] == '\n')
            {
                valueStart++;
            }
            int braceClose = text.IndexOf('}', valueStart);
            if (braceClose < 0)
            {
                continue;
            }
            int valueEnd = braceClose;
            if (valueEnd > valueStart && text[valueEnd - 1] == '\n')
            {
                valueEnd--;
            }
            if (valueEnd > valueStart && text[valueEnd - 1] == '\r')
            {
                valueEnd--;
            }
            document._entries.Add(new Entry(int.Parse(matches[i].Groups[1].Value, CultureInfo.InvariantCulture), valueStart, valueEnd));
        }
        return document;
    }

    public string Get(int id)
    {
        foreach (var entry in _entries)
        {
            if (entry.Id == id)
            {
                return _text[entry.ValueStart..entry.ValueEnd];
            }
        }
        return null;
    }

    public void Set(int id, string value)
    {
        foreach (var entry in _entries)
        {
            if (entry.Id != id)
            {
                continue;
            }
            string original = _text[entry.ValueStart..entry.ValueEnd];
            string lineEnding = original.Contains("\r\n") || _text.Contains("\r\n") ? "\r\n" : "\n";
            _text = _text[..entry.ValueStart] + value + _text[entry.ValueEnd..];
            int delta = value.Length - (entry.ValueEnd - entry.ValueStart);
            for (int i = 0; i < _entries.Count; i++)
            {
                var other = _entries[i];
                if (other.ValueStart > entry.ValueStart)
                {
                    _entries[i] = other with { ValueStart = other.ValueStart + delta, ValueEnd = other.ValueEnd + delta };
                }
            }
            _entries[_entries.FindIndex(e => e.Id == id)] = entry with { ValueEnd = entry.ValueStart + value.Length };
            _ = lineEnding;
            return;
        }
        throw new ArgumentOutOfRangeException(nameof(id), $"wts 里没有 STRING {id}");
    }

    public byte[] ToBytes()
    {
        return Encoding.UTF8.GetBytes("\uFEFF" + _text);
    }
}
