using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Simitone.Windows
{
    public struct IsoExtractProgress
    {
        public string CurrentFile;
        public long BytesCopied;
        public long BytesTotal;
    }

    /// <summary>
    /// Reads an ISO 9660 disc image, including Joliet names and raw 2352-byte BIN dumps,
    /// and copies out an installed The Sims tree.
    /// </summary>
    public static class Iso9660Image
    {
        const int LogicalBlockSize = 2048;

        public static void ExtractSimsInstall(string imagePath, string destination, IProgress<IsoExtractProgress> progress, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) throw new InvalidDataException("Choose a disc image.");
            if (!File.Exists(imagePath)) throw new FileNotFoundException("That disc image could not be opened.", imagePath);

            using (var stream = File.Open(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var image = Open(stream);
                var gameRoot = FindGameRoot(image.Root);
                if (gameRoot == null)
                {
                    var top = new List<string>();
                    if (image.Root.Children != null)
                    {
                        foreach (var child in image.Root.Children)
                        {
                            top.Add(child.Name);
                            if (top.Count == 8) break;
                        }
                    }
                    var seen = top.Count == 0 ? "no files" : string.Join(", ", top);
                    throw new InvalidDataException(
                        "This disc image does not contain GameData\\Behavior.iff. " +
                        "Simitone can extract an image of the installed game. A setup disc has to be installed first. " +
                        "Top level: " + seen + ".");
                }

                var files = new List<IsoFile>();
                long total = 0;
                CollectFiles(gameRoot, "", files, ref total);
                long copied = 0;
                var buffer = new byte[1024 * 256];
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var relative = file.RelativePath.Replace('/', Path.DirectorySeparatorChar);
                    if (relative.Length == 0 || relative.Contains(".." + Path.DirectorySeparatorChar) || Path.IsPathRooted(relative))
                        throw new InvalidDataException("The disc image contains an unsafe path.");
                    var target = Path.Combine(destination, relative);
                    progress?.Report(new IsoExtractProgress
                    {
                        CurrentFile = file.RelativePath,
                        BytesCopied = copied,
                        BytesTotal = total
                    });
                    if (file.Directory)
                    {
                        Directory.CreateDirectory(target);
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using (var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        long remaining = file.Length;
                        long offset = 0;
                        while (remaining > 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var chunk = (int)Math.Min(buffer.Length, remaining);
                            image.Read(file.Extent, offset, buffer, 0, chunk);
                            output.Write(buffer, 0, chunk);
                            remaining -= chunk;
                            offset += chunk;
                            copied += chunk;
                        }
                    }
                }
                progress?.Report(new IsoExtractProgress
                {
                    CurrentFile = "",
                    BytesCopied = copied,
                    BytesTotal = total
                });
            }
        }

        static IsoDirectory FindGameRoot(IsoDirectory root)
        {
            IsoDirectory found = null;
            Walk(root, delegate (IsoDirectory directory)
            {
                if (found != null || directory.Children == null) return;
                foreach (var child in directory.Children)
                {
                    if (child.Directory || !child.Name.Equals("Behavior.iff", StringComparison.OrdinalIgnoreCase)) continue;
                    if (directory.Name.Equals("GameData", StringComparison.OrdinalIgnoreCase))
                        found = directory.Parent ?? directory;
                }
            });
            return found;
        }

        static void Walk(IsoDirectory directory, Action<IsoDirectory> visit)
        {
            visit(directory);
            if (directory.Children == null) return;
            foreach (var child in directory.Children)
            {
                if (child.Directory) Walk(child, visit);
            }
        }

        static void CollectFiles(IsoDirectory directory, string relative, List<IsoFile> files, ref long total)
        {
            if (!string.IsNullOrEmpty(relative))
            {
                files.Add(new IsoFile { RelativePath = relative, Directory = true });
            }
            if (directory.Children == null) return;
            foreach (var child in directory.Children)
            {
                var path = string.IsNullOrEmpty(relative) ? child.Name : relative + "/" + child.Name;
                if (child.Directory)
                {
                    CollectFiles(child, path, files, ref total);
                }
                else
                {
                    files.Add(new IsoFile
                    {
                        RelativePath = path,
                        Extent = child.Extent,
                        Length = child.Length
                    });
                    total += child.Length;
                }
            }
        }

        static OpenImage Open(Stream stream)
        {
            var layout = DetectLayout(stream);
            var pvd = ReadDescriptor(stream, layout, 1);
            var joliet = FindJoliet(stream, layout);
            if (pvd == null && joliet == null)
                throw new InvalidDataException("This file is not an ISO 9660 or BIN disc image.");
            var descriptor = joliet ?? pvd;
            var seen = new HashSet<uint>();
            var root = ReadTree(stream, layout, descriptor, 156, joliet != null, seen);
            return new OpenImage(stream, layout, root);
        }

        static byte[] ReadDescriptor(Stream stream, ImageLayout layout, byte type)
        {
            for (int sector = 16; sector < 64; sector++)
            {
                var data = new byte[LogicalBlockSize];
                if (!TryReadLogical(stream, layout, (uint)sector, data)) return null;
                if (data[0] == 255) return type == 255 ? data : null;
                if (Encoding.ASCII.GetString(data, 1, 5) != "CD001") continue;
                if (data[0] == type) return data;
            }
            return null;
        }

        static byte[] FindJoliet(Stream stream, ImageLayout layout)
        {
            for (int sector = 16; sector < 64; sector++)
            {
                var data = new byte[LogicalBlockSize];
                if (!TryReadLogical(stream, layout, (uint)sector, data)) return null;
                if (data[0] == 255) return null;
                if (data[0] != 2 || Encoding.ASCII.GetString(data, 1, 5) != "CD001") continue;
                if (data[88] == 0x25 && data[89] == 0x2F) return data;
            }
            return null;
        }

        static bool TryReadLogical(Stream stream, ImageLayout layout, uint extent, byte[] data)
        {
            long pos = ((long)extent * layout.SectorStride) + layout.HeaderSkip;
            if (pos < 0 || pos + data.Length > stream.Length) return false;
            stream.Position = pos;
            int read = 0;
            while (read < data.Length)
            {
                int n = stream.Read(data, read, data.Length - read);
                if (n <= 0) return false;
                read += n;
            }
            return true;
        }

        static ImageLayout DetectLayout(Stream stream)
        {
            if (HasIdentifier(stream, 32768))
                return new ImageLayout { SectorStride = LogicalBlockSize, HeaderSkip = 0 };
            if (HasIdentifier(stream, 16L * 2352 + 16))
                return new ImageLayout { SectorStride = 2352, HeaderSkip = 16 };
            throw new InvalidDataException("This file is not an ISO 9660 or BIN disc image.");
        }

        static bool HasIdentifier(Stream stream, long offset)
        {
            if (offset + 6 > stream.Length) return false;
            var id = new byte[5];
            stream.Position = offset + 1;
            if (stream.Read(id, 0, 5) != 5) return false;
            return Encoding.ASCII.GetString(id) == "CD001";
        }

        static IsoDirectory ReadTree(Stream stream, ImageLayout layout, byte[] descriptor, int recordOffset, bool joliet, HashSet<uint> seen)
        {
            var record = ReadRecord(descriptor, recordOffset, joliet);
            var directory = new IsoDirectory { Name = record.Name ?? "", Extent = record.Extent, Length = record.Length };
            directory.Children = ReadChildren(stream, layout, record.Extent, record.Length, joliet, directory, seen, 0);
            return directory;
        }

        static List<IsoDirectory> ReadChildren(Stream stream, ImageLayout layout, uint extent, uint length, bool joliet, IsoDirectory parent, HashSet<uint> seen, int depth)
        {
            if (depth > 32) throw new InvalidDataException("The disc image directory is too deep.");
            if (!seen.Add(extent)) return new List<IsoDirectory>();
            if (length > int.MaxValue) throw new InvalidDataException("The disc image has a directory that is too large.");
            var data = new byte[(int)length];
            ReadLogical(stream, layout, extent, 0, data, 0, data.Length);
            var children = new List<IsoDirectory>();
            int pos = 0;
            while (pos < data.Length)
            {
                if (data[pos] == 0)
                {
                    pos = ((pos / LogicalBlockSize) + 1) * LogicalBlockSize;
                    continue;
                }
                var child = ReadRecord(data, pos, joliet);
                pos += data[pos];
                if (child.Skip) continue;
                child.Parent = parent;
                if (child.Directory)
                    child.Children = ReadChildren(stream, layout, child.Extent, child.Length, joliet, child, seen, depth + 1);
                children.Add(child);
            }
            return children;
        }

        static IsoDirectory ReadRecord(byte[] data, int offset, bool joliet)
        {
            int length = data[offset];
            if (length < 34 || offset + length > data.Length)
                throw new InvalidDataException("The disc image has a broken directory entry.");
            var record = new IsoDirectory
            {
                Extent = ReadUInt32(data, offset + 2),
                Length = ReadUInt32(data, offset + 10),
                Directory = (data[offset + 25] & 0x02) != 0
            };
            int nameLength = data[offset + 32];
            int nameStart = offset + 33;
            if (nameLength == 1 && (data[nameStart] == 0 || data[nameStart] == 1))
            {
                record.Skip = true;
                return record;
            }
            string name;
            if (joliet)
                name = Encoding.BigEndianUnicode.GetString(data, nameStart, nameLength).TrimEnd('\0');
            else
                name = Encoding.ASCII.GetString(data, nameStart, nameLength).Trim();
            var version = name.LastIndexOf(';');
            if (version > 0) name = name.Substring(0, version);
            if (!joliet)
            {
                var rock = RockRidgeName(data, offset, nameLength);
                if (!string.IsNullOrEmpty(rock)) name = rock;
            }
            record.Name = SanitizeName(name);
            if (string.IsNullOrEmpty(record.Name) || record.Name == "." || record.Name == "..")
                record.Skip = true;
            return record;
        }

        static string RockRidgeName(byte[] data, int offset, int nameLength)
        {
            int sys = offset + 33 + nameLength;
            if ((nameLength % 2) == 0) sys++;
            int end = offset + data[offset];
            var builder = new StringBuilder();
            while (sys + 4 <= end)
            {
                if (data[sys] == 0) break;
                int entryLength = data[sys + 2];
                if (entryLength < 4 || sys + entryLength > end) break;
                if (data[sys] == (byte)'N' && data[sys + 1] == (byte)'M' && entryLength > 5)
                {
                    byte flags = data[sys + 4];
                    if ((flags & 0x06) == 0)
                        builder.Append(Encoding.ASCII.GetString(data, sys + 5, entryLength - 5));
                }
                sys += entryLength;
            }
            var name = builder.ToString().TrimEnd('\0', ' ');
            return name.Length == 0 ? null : name;
        }

        static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                if (c < 32 || c == '<' || c == '>' || c == ':' || c == '"' || c == '|' || c == '?' || c == '*' || c == '\\' || c == '/')
                    chars[i] = '_';
            }
            return new string(chars).Trim().TrimEnd('.', ' ');
        }

        static void ReadLogical(Stream stream, ImageLayout layout, uint extent, long offset, byte[] buffer, int bufferOffset, int count)
        {
            long logical = ((long)extent * LogicalBlockSize) + offset;
            while (count > 0)
            {
                long block = logical / LogicalBlockSize;
                int within = (int)(logical % LogicalBlockSize);
                int chunk = Math.Min(count, LogicalBlockSize - within);
                stream.Position = (block * layout.SectorStride) + layout.HeaderSkip + within;
                int read = 0;
                while (read < chunk)
                {
                    int n = stream.Read(buffer, bufferOffset + read, chunk - read);
                    if (n <= 0) throw new EndOfStreamException("The disc image ended before the file did.");
                    read += n;
                }
                logical += chunk;
                bufferOffset += chunk;
                count -= chunk;
            }
        }

        static uint ReadUInt32(byte[] data, int offset)
        {
            return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        }

        sealed class OpenImage
        {
            readonly Stream _stream;
            readonly ImageLayout _layout;
            public IsoDirectory Root { get; }

            public OpenImage(Stream stream, ImageLayout layout, IsoDirectory root)
            {
                _stream = stream;
                _layout = layout;
                Root = root;
            }

            public void Read(uint extent, long offset, byte[] buffer, int bufferOffset, int count)
            {
                ReadLogical(_stream, _layout, extent, offset, buffer, bufferOffset, count);
            }
        }

        sealed class IsoDirectory
        {
            public string Name;
            public bool Directory;
            public bool Skip;
            public uint Extent;
            public uint Length;
            public IsoDirectory Parent;
            public List<IsoDirectory> Children;
        }

        sealed class IsoFile
        {
            public string RelativePath;
            public bool Directory;
            public uint Extent;
            public uint Length;
        }

        struct ImageLayout
        {
            public int SectorStride;
            public int HeaderSkip;
        }
    }
}
