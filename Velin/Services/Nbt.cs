using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Velin.Services
{
    public enum NbtType : byte { End = 0, Byte = 1, Short = 2, Int = 3, Long = 4, Float = 5, Double = 6, ByteArray = 7, String = 8, List = 9, Compound = 10, IntArray = 11, LongArray = 12 }

    // Valor de un tag NBT. Value es: sbyte, short, int, long, float, double, byte[], string, NbtList, NbtCompound, int[] o long[].
    public sealed class NbtTag
    {
        public NbtType Type { get; }
        public object? Value { get; set; }
        public NbtTag(NbtType type, object? value) { Type = type; Value = value; }

        public static NbtTag String(string s) => new(NbtType.String, s);
        public static NbtTag Byte(sbyte b) => new(NbtType.Byte, b);
        public static NbtTag List(NbtList l) => new(NbtType.List, l);
        public static NbtTag Compound(NbtCompound c) => new(NbtType.Compound, c);
    }

    public sealed class NbtList
    {
        public NbtType ElementType { get; set; } = NbtType.Compound;
        public List<NbtTag> Items { get; } = new();
    }

    // Compound que conserva el orden de los tags.
    public sealed class NbtCompound
    {
        public List<KeyValuePair<string, NbtTag>> Entries { get; } = new();

        public NbtTag? Get(string name)
        {
            foreach (var e in Entries) if (e.Key == name) return e.Value;
            return null;
        }

        public string GetString(string name) => Get(name)?.Value as string ?? "";

        public void Set(string name, NbtTag tag)
        {
            var i = Entries.FindIndex(e => e.Key == name);
            var entry = new KeyValuePair<string, NbtTag>(name, tag);
            if (i >= 0) Entries[i] = entry; else Entries.Add(entry);
        }
    }

    // Lector/escritor mínimo de NBT sin comprimir (suficiente para servers.dat).
    public static class Nbt
    {
        private const int MaxDepth = 256;
        private const int MaxLength = 50_000_000;

        public static NbtCompound ReadRoot(Stream stream, out string rootName)
        {
            using var r = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var type = (NbtType)r.ReadByte();
            if (type != NbtType.Compound) throw new InvalidDataException("El archivo NBT no empieza con un compound.");
            rootName = ReadString(r);
            return (NbtCompound)ReadPayload(NbtType.Compound, r, 0)!;
        }

        public static void WriteRoot(Stream stream, string rootName, NbtCompound root)
        {
            using var w = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            w.Write((byte)NbtType.Compound);
            WriteString(w, rootName);
            WritePayload(NbtType.Compound, root, w);
        }

        // --- lectura ---
        private static byte[] Bytes(BinaryReader r, int n)
        {
            if (n < 0 || n > MaxLength) throw new InvalidDataException("Longitud NBT inválida.");
            var b = r.ReadBytes(n);
            if (b.Length != n) throw new InvalidDataException("El archivo NBT está cortado.");
            return b;
        }
        private static short I16(BinaryReader r) => BinaryPrimitives.ReadInt16BigEndian(Bytes(r, 2));
        private static int I32(BinaryReader r) => BinaryPrimitives.ReadInt32BigEndian(Bytes(r, 4));
        private static long I64(BinaryReader r) => BinaryPrimitives.ReadInt64BigEndian(Bytes(r, 8));

        private static string ReadString(BinaryReader r)
        {
            int len = BinaryPrimitives.ReadUInt16BigEndian(Bytes(r, 2));
            return Encoding.UTF8.GetString(Bytes(r, len));
        }

        private static object? ReadPayload(NbtType type, BinaryReader r, int depth)
        {
            if (depth > MaxDepth) throw new InvalidDataException("NBT demasiado anidado.");
            switch (type)
            {
                case NbtType.Byte: return (sbyte)r.ReadByte();
                case NbtType.Short: return I16(r);
                case NbtType.Int: return I32(r);
                case NbtType.Long: return I64(r);
                case NbtType.Float: return BitConverter.Int32BitsToSingle(I32(r));
                case NbtType.Double: return BitConverter.Int64BitsToDouble(I64(r));
                case NbtType.ByteArray: return Bytes(r, I32(r));
                case NbtType.String: return ReadString(r);
                case NbtType.List:
                {
                    var list = new NbtList { ElementType = (NbtType)r.ReadByte() };
                    int n = I32(r);
                    if (n < 0 || n > MaxLength) throw new InvalidDataException("Lista NBT inválida.");
                    for (int i = 0; i < n; i++) list.Items.Add(new NbtTag(list.ElementType, ReadPayload(list.ElementType, r, depth + 1)));
                    return list;
                }
                case NbtType.Compound:
                {
                    var c = new NbtCompound();
                    while (true)
                    {
                        var t = (NbtType)r.ReadByte();
                        if (t == NbtType.End) break;
                        var name = ReadString(r);
                        c.Entries.Add(new KeyValuePair<string, NbtTag>(name, new NbtTag(t, ReadPayload(t, r, depth + 1))));
                    }
                    return c;
                }
                case NbtType.IntArray:
                {
                    int n = I32(r); if (n < 0 || n > MaxLength) throw new InvalidDataException("Array NBT inválido.");
                    var a = new int[n]; for (int i = 0; i < n; i++) a[i] = I32(r); return a;
                }
                case NbtType.LongArray:
                {
                    int n = I32(r); if (n < 0 || n > MaxLength) throw new InvalidDataException("Array NBT inválido.");
                    var a = new long[n]; for (int i = 0; i < n; i++) a[i] = I64(r); return a;
                }
                default: throw new InvalidDataException("Tipo NBT desconocido: " + (int)type);
            }
        }

        // --- escritura ---
        private static void W16(BinaryWriter w, short v) { Span<byte> b = stackalloc byte[2]; BinaryPrimitives.WriteInt16BigEndian(b, v); w.Write(b); }
        private static void W32(BinaryWriter w, int v) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(b, v); w.Write(b); }
        private static void W64(BinaryWriter w, long v) { Span<byte> b = stackalloc byte[8]; BinaryPrimitives.WriteInt64BigEndian(b, v); w.Write(b); }

        private static void WriteString(BinaryWriter w, string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            if (bytes.Length > ushort.MaxValue) throw new InvalidDataException("Texto NBT demasiado largo.");
            Span<byte> b = stackalloc byte[2]; BinaryPrimitives.WriteUInt16BigEndian(b, (ushort)bytes.Length); w.Write(b);
            w.Write(bytes);
        }

        private static void WritePayload(NbtType type, object? value, BinaryWriter w)
        {
            switch (type)
            {
                case NbtType.Byte: w.Write((byte)(sbyte)value!); break;
                case NbtType.Short: W16(w, (short)value!); break;
                case NbtType.Int: W32(w, (int)value!); break;
                case NbtType.Long: W64(w, (long)value!); break;
                case NbtType.Float: W32(w, BitConverter.SingleToInt32Bits((float)value!)); break;
                case NbtType.Double: W64(w, BitConverter.DoubleToInt64Bits((double)value!)); break;
                case NbtType.ByteArray: { var a = (byte[])value!; W32(w, a.Length); w.Write(a); break; }
                case NbtType.String: WriteString(w, (string)value!); break;
                case NbtType.List:
                {
                    var l = (NbtList)value!;
                    w.Write((byte)(l.Items.Count == 0 ? NbtType.End : l.ElementType));
                    W32(w, l.Items.Count);
                    foreach (var item in l.Items) WritePayload(l.ElementType, item.Value, w);
                    break;
                }
                case NbtType.Compound:
                {
                    foreach (var e in ((NbtCompound)value!).Entries)
                    {
                        w.Write((byte)e.Value.Type);
                        WriteString(w, e.Key);
                        WritePayload(e.Value.Type, e.Value.Value, w);
                    }
                    w.Write((byte)NbtType.End);
                    break;
                }
                case NbtType.IntArray: { var a = (int[])value!; W32(w, a.Length); foreach (var x in a) W32(w, x); break; }
                case NbtType.LongArray: { var a = (long[])value!; W32(w, a.Length); foreach (var x in a) W64(w, x); break; }
                default: throw new InvalidDataException("Tipo NBT no se puede escribir: " + type);
            }
        }
    }
}
