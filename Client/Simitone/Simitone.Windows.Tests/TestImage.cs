using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Simitone.Windows.Tests
{
    /// <summary>
    /// Writes small ISO 9660 images for the tests: plain 2048-byte sectors or raw 2352-byte
    /// Mode 1 / Mode 2 dumps, with optional Joliet names, Rock Ridge names and extended attribute records.
    /// </summary>
    public class TestImage
    {
        const int B = 2048;

        public class Entry
        {
            public string Name;            // ISO name, e.g. "BEHAVIOR.IFF;1" or "GAMEDATA"
            public string LongName;        // Joliet / Rock Ridge name, if different
            public byte[] Data;            // null for a directory
            public byte XaBlocks;
            public byte Flags;             // extra file flags, e.g. 0x80 for a multi-extent record
            public List<Entry> Children = new List<Entry>();
            internal uint Extent;
            internal uint JolietExtent;
            internal int Blocks;
            public bool IsDirectory => Data == null;
        }

        public Entry Root = new Entry { Name = "" };
        public bool Joliet;
        public bool RockRidge;

        public static Entry Dir(string name, params Entry[] children)
        {
            var e = new Entry { Name = name };
            e.Children.AddRange(children);
            return e;
        }

        public static Entry File(string name, byte[] data, byte xaBlocks = 0, string longName = null)
        {
            return new Entry { Name = name, Data = data, XaBlocks = xaBlocks, LongName = longName };
        }

        public byte[] BuildIso()
        {
            // 16 system area, 16 PVD, 17 optional SVD, then terminator, then directories, then files.
            uint next = 16;
            uint pvd = next++;
            uint svd = Joliet ? next++ : 0;
            uint term = next++;
            var dirs = new List<Entry>();
            Collect(Root, dirs);
            foreach (var d in dirs) d.Extent = next++;
            if (Joliet) foreach (var d in dirs) d.JolietExtent = next++;
            var files = new List<Entry>();
            CollectFiles(Root, files);
            foreach (var f in files)
            {
                f.Extent = next;
                f.Blocks = f.XaBlocks + (f.Data.Length + B - 1) / B;
                next += (uint)Math.Max(1, f.Blocks);
            }

            var img = new byte[next * B];
            Put(img, pvd, Descriptor(1, next, Record(Root, Root.Extent, false, root: true), false));
            if (Joliet) Put(img, svd, Descriptor(2, next, Record(Root, Root.JolietExtent, true, root: true), true));
            var t = new byte[B];
            t[0] = 255;
            Encoding.ASCII.GetBytes("CD001").CopyTo(t, 1);
            t[6] = 1;
            Put(img, term, t);

            foreach (var d in dirs)
            {
                Put(img, d.Extent, DirectoryBlock(d, false));
                if (Joliet) Put(img, d.JolietExtent, DirectoryBlock(d, true));
            }
            foreach (var f in files)
            {
                if (f.XaBlocks > 0)
                {
                    var xar = new byte[B * f.XaBlocks];
                    for (int i = 0; i < xar.Length; i++) xar[i] = 0xEE;
                    Put(img, f.Extent, xar);
                }
                Put(img, f.Extent + f.XaBlocks, f.Data);
            }
            return img;
        }

        public byte[] BuildRaw(int headerSkip)
        {
            var iso = BuildIso();
            var sectors = iso.Length / B;
            var raw = new byte[sectors * 2352];
            for (int i = 0; i < sectors; i++)
            {
                var o = i * 2352;
                for (int j = 1; j < 11; j++) raw[o + j] = 0xFF; // sync pattern
                raw[o + 15] = (byte)(headerSkip == 16 ? 1 : 2);
                Buffer.BlockCopy(iso, i * B, raw, o + headerSkip, B);
            }
            return raw;
        }

        static void Collect(Entry dir, List<Entry> dirs)
        {
            dirs.Add(dir);
            foreach (var c in dir.Children) if (c.IsDirectory) Collect(c, dirs);
        }

        static void CollectFiles(Entry dir, List<Entry> files)
        {
            foreach (var c in dir.Children)
            {
                if (c.IsDirectory) CollectFiles(c, files);
                else files.Add(c);
            }
        }

        static void Put(byte[] img, uint sector, byte[] data)
        {
            Buffer.BlockCopy(data, 0, img, (int)(sector * B), data.Length);
        }

        byte[] Descriptor(byte type, uint blocks, byte[] rootRecord, bool joliet)
        {
            var d = new byte[B];
            d[0] = type;
            Encoding.ASCII.GetBytes("CD001").CopyTo(d, 1);
            d[6] = 1;
            Both32(blocks).CopyTo(d, 80);
            if (joliet)
            {
                d[88] = 0x25;
                d[89] = 0x2F;
                d[90] = 0x45;
            }
            Both16(B).CopyTo(d, 128);
            rootRecord.CopyTo(d, 156);
            return d;
        }

        byte[] DirectoryBlock(Entry dir, bool joliet)
        {
            var parent = FindParent(Root, dir) ?? Root;
            var block = new List<byte>();
            block.AddRange(Raw(new byte[] { 0 }, joliet ? dir.JolietExtent : dir.Extent, B, 0x02, 0, null));
            block.AddRange(Raw(new byte[] { 1 }, joliet ? parent.JolietExtent : parent.Extent, B, 0x02, 0, null));
            foreach (var c in dir.Children) block.AddRange(Record(c, joliet ? (c.IsDirectory ? c.JolietExtent : c.Extent) : c.Extent, joliet));
            if (block.Count > B) throw new InvalidOperationException("Test directory is larger than one block.");
            return block.ToArray();
        }

        static Entry FindParent(Entry dir, Entry target)
        {
            foreach (var c in dir.Children)
            {
                if (c == target) return dir;
                if (c.IsDirectory)
                {
                    var found = FindParent(c, target);
                    if (found != null) return found;
                }
            }
            return null;
        }

        byte[] Record(Entry e, uint extent, bool joliet, bool root = false)
        {
            uint length = e.IsDirectory ? (uint)B : (uint)e.Data.Length;
            byte flags = (byte)((e.IsDirectory ? 0x02 : 0) | e.Flags);
            if (root) return Raw(new byte[] { 0 }, extent, length, flags, 0, null);
            byte[] name;
            byte[] system = null;
            if (joliet)
            {
                var n = e.LongName ?? e.Name;
                if (!e.IsDirectory && !n.Contains(";")) n += ";1";
                name = Encoding.BigEndianUnicode.GetBytes(n);
            }
            else
            {
                name = Encoding.ASCII.GetBytes(e.Name);
                if (RockRidge && e.LongName != null)
                {
                    var nm = Encoding.ASCII.GetBytes(e.LongName);
                    system = new byte[5 + nm.Length];
                    system[0] = (byte)'N';
                    system[1] = (byte)'M';
                    system[2] = (byte)system.Length;
                    system[3] = 1;
                    system[4] = 0;
                    nm.CopyTo(system, 5);
                }
            }
            return Raw(name, extent, length, flags, e.XaBlocks, system);
        }

        static byte[] Raw(byte[] name, uint extent, uint length, byte flags, byte xa, byte[] system)
        {
            int len = 33 + name.Length;
            if ((name.Length % 2) == 0) len++;
            int nameEnd = len;
            if (system != null) len += system.Length;
            if ((len % 2) != 0) len++;
            var r = new byte[len];
            r[0] = (byte)len;
            r[1] = xa;
            Both32(extent).CopyTo(r, 2);
            Both32(length).CopyTo(r, 10);
            r[25] = flags;
            Both16(1).CopyTo(r, 28);
            r[32] = (byte)name.Length;
            name.CopyTo(r, 33);
            system?.CopyTo(r, nameEnd);
            return r;
        }

        static byte[] Both16(int v)
        {
            return new[] { (byte)v, (byte)(v >> 8), (byte)(v >> 8), (byte)v };
        }

        static byte[] Both32(uint v)
        {
            return new[]
            {
                (byte)v, (byte)(v >> 8), (byte)(v >> 16), (byte)(v >> 24),
                (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v
            };
        }
    }
}
