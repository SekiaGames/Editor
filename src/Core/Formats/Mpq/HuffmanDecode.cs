using System;
using System.IO;

namespace WorldEdit.Core.Formats.Mpq;

internal sealed class HuffmanNode
{
    public readonly int Value;
    public int Weight;
    public HuffmanNode Next;
    public HuffmanNode Prev;
    public HuffmanNode Parent;
    public HuffmanNode Child0;

    public HuffmanNode(int value, int weight)
    {
        Value = value;
        Weight = weight;
    }

    public HuffmanNode Child1 => Child0?.Prev;

    public HuffmanNode Insert(HuffmanNode other)
    {
        if (other.Weight <= Weight)
        {
            if (Next != null)
            {
                Next.Prev = other;
                other.Next = Next;
            }
            Next = other;
            other.Prev = this;
            return other;
        }

        if (Prev == null)
        {
            other.Prev = null;
            Prev = other;
            other.Next = this;
        }
        else
        {
            Prev.Insert(other);
        }
        return this;
    }
}

public static class HuffmanDecode
{
    public static byte[] Decompress(byte[] data, int expected)
    {
        if (data == null || data.Length < 2)
        {
            throw new InvalidDataException("huffman 数据太短");
        }

        int comptype = data[0];
        if (comptype < 0 || comptype >= HuffmanTables.Prime.Length)
        {
            throw new InvalidDataException($"huffman 压缩类型越界: {comptype}");
        }

        HuffmanNode tail = BuildList(HuffmanTables.Prime[comptype]);
        HuffmanNode head = BuildTree(tail);
        var bitstream = new LsbBitReader(data, 1);
        using var outputstream = new MemoryStream(expected > 0 ? expected : 0);

        while (true)
        {
            HuffmanNode node = Decode(bitstream, head);
            int decoded = node.Value;
            if (decoded == 256)
            {
                return outputstream.ToArray();
            }
            if (decoded == 257)
            {
                int newvalue = bitstream.ReadBits(8);
                if (newvalue < 0)
                {
                    return outputstream.ToArray();
                }
                outputstream.WriteByte((byte)newvalue);
                tail = InsertNode(tail, newvalue);
                continue;
            }

            outputstream.WriteByte((byte)decoded);
            if (comptype == 0)
            {
                AdjustTree(node);
            }
        }
    }

        private static HuffmanNode Decode(LsbBitReader input, HuffmanNode head)
        {
            var node = head;
            while (node.Child0 != null)
            {
                var bit = input.ReadBits(1);
                if (bit == -1)
                {
                    throw new InvalidDataException($"Unexpected end of file");
                }

                node = bit == 0 ? node.Child0! : node.Child1!;
            }

            return node;
        }

        private static HuffmanNode BuildList(byte[] primeData)
        {
            HuffmanNode root;

            root = new HuffmanNode(256, 1);
            root = root.Insert(new HuffmanNode(257, 1));

            for (var i = 0; i < primeData.Length; i++)
            {
                if (primeData[i] != 0)
                {
                    root = root.Insert(new HuffmanNode(i, primeData[i]));
                }
            }

            return root;
        }

        private static HuffmanNode BuildTree(HuffmanNode tail)
        {
            var current = tail;

            while (current != null)
            {
                var child0 = current;
                var child1 = current.Prev;
                if (child1 == null)
                {
                    break;
                }

                var parent = new HuffmanNode(0, child0.Weight + child1.Weight);
                parent.Child0 = child0;
                child0.Parent = parent;
                child1.Parent = parent;

                current.Insert(parent);
                current = current.Prev.Prev;
            }

            return current;
        }

        private static HuffmanNode InsertNode(HuffmanNode tail, int decomp)
        {
            var parent = tail;
            var result = tail.Prev!;

            var temp = new HuffmanNode(parent.Value, parent.Weight);
            temp.Parent = parent;

            var newnode = new HuffmanNode(decomp, 0);
            newnode.Parent = parent;

            parent.Child0 = newnode;

            tail.Next = temp;
            temp.Prev = tail;
            newnode.Prev = temp;
            temp.Next = newnode;

            AdjustTree(newnode);
            AdjustTree(newnode);
            return result;
        }

        private static void AdjustTree(HuffmanNode newNode)
        {
            HuffmanNode? current = newNode;

            while (current != null)
            {
                current.Weight++;
                HuffmanNode? prev;

                var insertpoint = current;
                while (true)
                {
                    prev = insertpoint.Prev;
                    if (prev == null)
                    {
                        break;
                    }

                    if (prev.Weight >= current.Weight)
                    {
                        break;
                    }

                    insertpoint = prev;
                }

                if (insertpoint == current)
                {
                    current = current.Parent;
                    continue;
                }

                if (insertpoint.Prev != null)
                {
                    insertpoint.Prev.Next = insertpoint.Next;
                }

                insertpoint.Next.Prev = insertpoint.Prev;

                insertpoint.Next = current.Next;
                insertpoint.Prev = current;
                if (current.Next != null)
                {
                    current.Next.Prev = insertpoint;
                }

                current.Next = insertpoint;

                current.Prev.Next = current.Next;
                current.Next.Prev = current.Prev;

                var temp = prev.Next;
                current.Next = temp;
                current.Prev = prev;
                temp.Prev = current;
                prev.Next = current;

                var currentparent = current.Parent;
                var insertparent = insertpoint.Parent;

                if (currentparent.Child0 == current)
                {
                    currentparent.Child0 = insertpoint;
                }

                if (currentparent != insertparent && insertparent.Child0 == insertpoint)
                {
                    insertparent.Child0 = current;
                }

                current.Parent = insertparent;
                insertpoint.Parent = currentparent;

                current = current.Parent;
            }
        }
}
